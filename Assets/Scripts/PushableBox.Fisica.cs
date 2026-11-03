using UnityEngine;

// Parte de PushableBox: caida, contacto con Obby, apoyo y pendiente. Las variables y el Update estan en PushableBox.cs
public partial class PushableBox
{
    // Empuje, freno y pendiente: lo que hace la roca en cada paso de fisica.
    void Fisica()
    {
        if (alignToSlope)
        {
            rb.angularVelocity = 0f; // ningun choque la hace girar (Obby no se trepa por un borde inclinado)
            AlignToSlope();
        }

        // EN EL AIRE no la tocamos: cae y vuela con su propia fisica (gravedad, rebote,
        // aceleracion al bajar la colina). Si le metiamos mano aca, parecia que flotaba.
        bool estabaEnElPiso = enElPiso;
        enElPiso = ApoyadaEnPiso();
        // venia cayendo y toco el piso: golpe seco (solo se escucha cerca de la camara)
        if (enElPiso && !estabaEnElPiso && velPrevia.y < -4f) Sonidos.PlayEn(impactSound, "roca_golpe", rb.position);

        // Apoyada pesa MUCHO: el choque de un salto de abajo o de un dash la empujaba ANTES de
        // que el script llegara a frenarla, y despues seguia de largo como si viniera rodando.
        // El empuje no cambia (le pone la velocidad directo). En el aire vuelve a su peso normal,
        // asi si le cae encima a Obby no lo hunde en el piso.
        float masa = enElPiso ? Mathf.Max(groundedMass, masaNormal) : masaNormal;
        if (rb.mass != masa) rb.mass = masa;

        if (!enElPiso)
        {
            rb.constraints &= ~RigidbodyConstraints2D.FreezePositionX;
            empujadaHasta = 0f;   // en el aire nadie la empuja
            return;
        }

        LeerContactoConObby(out var jugador, out int lado, out bool encima);

        // Empuje VALIDO: tocandola de costado, pisando el piso, sin dashear y apretando
        // hacia ella. Asi no la mueve con el dash, ni en el aire, ni parado encima o subido al borde.
        bool empujando = lado != 0 && !encima && jugador != null
                      && jugador.IsPushing   // pisando el SUELO contra la caja: parado en su borde no cuenta
                      && !jugador.IsDashing
                      && Mathf.Abs(jugador.MoveInput) > 0.1f
                      && Mathf.Sign(jugador.MoveInput) == -lado;
        if (empujando) empujadaHasta = Time.time + 0.3f;

        // Si ya viene rodando (la tiraste por una colina) NO la frenamos aunque Obby la roce:
        // antes se quedaba clavada en el medio de la bajada en vez de seguir de largo.
        bool rodando = Mathf.Abs(rb.linearVelocity.x) > 0.5f;

        // Se bloquea el eje X si camina encima, o si la toca de costado SIN empuje valido.
        // No alcanza con poner la velocidad en cero: despues corre el solver y la friccion
        // la volveria a mover en el mismo paso de fisica.
        bool bloquear = (lockWhileStoodOn && encima)
                     || (lado != 0 && !empujando && !rodando);

        if (bloquear)
        {
            rb.constraints |= RigidbodyConstraints2D.FreezePositionX;
            var v = rb.linearVelocity;
            v.x = 0f;                 // la caida (y) sigue normal
            rb.linearVelocity = v;
            return;
        }

        rb.constraints &= ~RigidbodyConstraints2D.FreezePositionX;

        // Empuje: le pone un PISO de velocidad, nunca un techo. Asi arranca facil aunque
        // sea pesada, pero si agarra una bajada puede ir mas rapido que Obby (y atropellar).
        if (empujando)
        {
            float objetivo = -lado * pushSpeed;
            float vx = rb.linearVelocity.x;
            if (objetivo > 0f ? vx < objetivo : vx > objetivo)
                rb.linearVelocity = new Vector2(objetivo, rb.linearVelocity.y);

            // raspa contra el piso mientras avanza (contra una pared no se mueve: no suena)
            if (Time.time >= proximoRaspe && Mathf.Abs(velPrevia.x) > 0.5f)
            {
                proximoRaspe = Time.time + 0.34f;
                Sonidos.PlayEn(pushSound, "roca_arrastra", rb.position);
            }
        }
    }

    // Lee el contacto con Obby: de que lado la toca (-1 izq, 1 der, 0 no la toca) y si esta encima.
    void LeerContactoConObby(out PlayerController2D jugador, out int lado, out bool encima)
    {
        jugador = null; lado = 0; encima = false;
        if (rb == null) return;

        // Contactos REALES del motor de fisica. Antes usaba un OverlapBox alrededor de la
        // caja y con eso Obby la empujaba desde lejos, sin llegar a tocarla.
        int n = rb.GetContacts(s_contacts);
        for (int i = 0; i < n; i++)
        {
            var cp = s_contacts[i];
            PlayerController2D p = null;
            if (cp.collider != null) p = cp.collider.GetComponentInParent<PlayerController2D>();
            if (p == null && cp.otherCollider != null) p = cp.otherCollider.GetComponentInParent<PlayerController2D>();
            if (p == null) continue;
            jugador = p;

            // La caja se inclina con la colina, asi que no sirve mirar si la normal es
            // horizontal "pura": comparamos cual manda. Mas horizontal que vertical = la
            // toca de COSTADO (empuje). Mas vertical = esta parado ENCIMA.
            if (Mathf.Abs(cp.normal.x) > Mathf.Abs(cp.normal.y))
            {
                if (lado == 0) lado = p.transform.position.x < transform.position.x ? -1 : 1;
            }
            else if (p.transform.position.y > transform.position.y)
            {
                encima = true;
            }
        }
    }

    // Buffer y filtro reutilizables (no reservan memoria por frame).
    static readonly Collider2D[] s_topBuf = new Collider2D[8];

    static readonly ContactPoint2D[] s_contacts = new ContactPoint2D[16];
    static readonly RaycastHit2D[] s_rayBuf = new RaycastHit2D[8];

    // Esta apoyada sobre algo solido? (si no, esta cayendo y no le tocamos la fisica)
    bool ApoyadaEnPiso()
    {
        if (col == null) return false;
        Bounds b = col.bounds;

        var filter = new ContactFilter2D();
        filter.NoFilter();
        filter.useTriggers = false;

        Vector2 centro = new Vector2(b.center.x, b.min.y - 0.03f);
        Vector2 tam = new Vector2(b.size.x * 0.9f, 0.14f);

        int n = Physics2D.OverlapBox(centro, tam, 0f, filter, s_topBuf);
        for (int i = 0; i < n; i++)
        {
            var c = s_topBuf[i];
            if (c == null || c == col) continue;
            if (c.GetComponentInParent<PlayerController2D>() != null) continue;  // pararse al lado no cuenta como piso
            return true;
        }
        return false;
    }

    // Raycast hacia abajo para leer la inclinacion del piso y rotar la caja para
    // quedar paralela a la pendiente.
    void AlignToSlope()
    {
        if (col == null) return;

        var filter = new ContactFilter2D();
        filter.SetLayerMask(groundMask.value != 0 ? groundMask.value : Physics2D.AllLayers);
        filter.useTriggers = false;   // ignora enemigos/balas/checkpoints (son trigger)
        Bounds b = col.bounds;
        // rayo desde el centro del cajon hacia abajo; ignora su propio collider.
        // Sin RaycastAll: ese creaba un array nuevo en CADA paso de fisica.
        float dist = b.extents.y + 0.4f;
        int n = Physics2D.Raycast(b.center, Vector2.down, filter, s_rayBuf, dist);

        Vector2 normal = Vector2.zero;
        float masCerca = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var h = s_rayBuf[i];
            if (h.collider == null || h.collider == col) continue;           // no contar el propio cajon
            if (h.collider.GetComponent<PushableBox>() != null) continue;    // ni otros cajones
            if (h.distance < masCerca) { masCerca = h.distance; normal = h.normal; }
        }
        if (normal == Vector2.zero) return; // en el aire / no encontro piso -> deja la rotacion como esta

        float pendiente = Vector2.SignedAngle(Vector2.up, normal);   // cuanto esta inclinado el piso
        if (Mathf.Abs(pendiente) > maxSlopeAngle) return;            // pendiente demasiado empinada -> no la sigue

        // Se inclina con la pendiente A PARTIR de como la pusiste en la escena: si la diste vuelta
        // (ej. 180 grados) se queda dada vuelta. Antes siempre la llevaba a 0 grados y, apoyada en
        // el piso, la hacia girar media vuelta contra el suelo: flotaba y se volvia loca.
        float targetAngle = rotInicial + pendiente;

        // fijamos la rotacion nosotros hacia el angulo de la pendiente (sin dejar que un choque la vuelque)
        float newAngle = Mathf.LerpAngle(rb.rotation, targetAngle, alignSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(newAngle);
    }
}
