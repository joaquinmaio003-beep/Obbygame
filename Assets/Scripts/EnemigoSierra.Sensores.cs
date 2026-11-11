using System.Collections;
using UnityEngine;

// Parte de EnemigoSierra: mirar al jugador, caja de golpe, romper adelante, piso y paredes. Las variables y el Update estan en EnemigoSierra.cs
public partial class EnemigoSierra
{
    // ---- facing / patrulla ----
    // Encara a Obby. Devuelve true si se dio vuelta.
    bool FacePlayer()
    {
        if (player == null) return false;
        float dx = player.position.x - CentroX();
        if (Mathf.Abs(dx) < 0.15f) return false; // casi alineado -> no gira (evita spin)
        int want = dx >= 0f ? 1 : -1;
        if (want == dir) return false;
        dir = want;
        ApplyFacing();
        return true;
    }

    // Mientras esta atento no gira mas de una vez cada tanto: si Obby le pasa justo por encima
    // (o por debajo), no se pone a dar vueltas.
    void EncararConCalma()
    {
        if (giroCd <= 0f && FacePlayer()) giroCd = 0.25f;
    }

    // Centro del CUERPO (el collider) en X. No es el centro del dibujo: adelante lleva el arma.
    float CentroX() { return transform.position.x + col.offset.x * transform.lossyScale.x; }

    void Flip() { dir = -dir; ApplyFacing(); }

    void ApplyFacing()
    {
        var s = transform.localScale;
        int sign = spriteFacesRight ? dir : -dir;
        float x = Mathf.Abs(s.x) * sign;
        if (x == s.x) return;   // ya mira para ese lado
        // El dibujo se espeja, pero el CUERPO se queda donde estaba. Antes se espejaba todo sobre
        // el centro del dibujo y el cuerpo (que esta corrido hacia atras) saltaba al otro lado:
        // mirando para un lado te veia y para el otro no, y contra una pared quedaba metido
        // adentro. En las esquinas eso lo dejaba girando sin parar.
        float cuerpoX = CentroX();
        s.x = x;
        transform.localScale = s;
        var p = transform.position;
        p.x = 2f * cuerpoX - p.x;
        transform.position = p;
    }

    // Buffers y filtro reutilizables (no reservan memoria por frame).
    static readonly RaycastHit2D[] s_hitBuf = new RaycastHit2D[8];
    static readonly Collider2D[] s_colBuf = new Collider2D[8];
    ContactFilter2D GroundFilter()
    {
        var f = new ContactFilter2D();
        f.useTriggers = Physics2D.queriesHitTriggers;
        f.SetLayerMask(groundLayer);
        return f;
    }


    // Piso "pisable": como GroundRayNoBox, pero una roca/rompible SI cuenta como piso
    // cuando su superficie esta a la altura de los pies o mas abajo (o sea, cuando venis
    // cayendo desde arriba). Si esta mas alta que los pies, la ignora y la rompe de frente.
    RaycastHit2D GroundRayPisable(Vector2 origin, Vector2 dir, float dist, float feetY)
    {
        int n = Physics2D.Raycast(origin, dir, GroundFilter(), s_hitBuf, dist);
        RaycastHit2D best = default; float bestD = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var h = s_hitBuf[i];
            if (h.collider == null) continue;
            if (h.distance <= 0.0001f) continue;   // el rayo arranco DENTRO del collider: Unity
                                                   // devuelve el propio origen como punto y el
                                                   // snap lo mandaba volando para arriba

            bool rompible = h.collider.GetComponentInParent<PushableBox>() != null
                         || h.collider.GetComponentInParent<Destructible>() != null;
            if (rompible && h.point.y > feetY + 0.1f) continue;   // esta a su altura: la parte, no se le sube

            if (h.distance < bestD) { bestD = h.distance; best = h; }
        }
        return best;
    }


    // ¿Puede seguir caminando hacia 'dir'? Detecta pared/escalon alto y precipicio,


    // Caja de golpe delante del enemigo: mas fiable que medir distancia, porque cubre
    // el alto del cuerpo + un extra arriba (para el que salta) y todo el frente.
    // ¿Obby esta dentro de la caja de golpe? Cubre el cuerpo del enemigo, un poco hacia
    // adelante (attackRange) y hacia arriba (attackUpReach), asi tambien alcanza al que
    // salta o esta parado en una roca.
    bool PlayerEnCajaDeGolpe()
    {
        if (col == null) return false;
        if (playerHealth != null && playerHealth.EstaMuerto) return false;   // muerto: no le sigue serruchando el cuerpo
        Bounds b = col.bounds;

        float ancho = b.size.x + attackRange;
        float alto  = b.size.y + attackUpReach;
        Vector2 centro = new Vector2(
            b.center.x + dir * (ancho - b.size.x) * 0.5f,   // corrido hacia donde mira
            b.center.y + attackUpReach * 0.5f);             // un poco mas arriba

        var f = new ContactFilter2D();
        f.NoFilter();
        f.useTriggers = false;   // Obby tiene collider solido

        int n = Physics2D.OverlapBox(centro, new Vector2(ancho, alto), 0f, f, s_colBuf);
        for (int i = 0; i < n; i++)
        {
            if (s_colBuf[i] == null) continue;
            if (s_colBuf[i].GetComponentInParent<PlayerController2D>() != null) return true;
        }
        return false;
    }
    // ---- romper obstaculos ----

    // ¿Hay algo ROMPIBLE justo adelante? (arbol, roca, pinchos con el componente Destructible)
    // Devuelve el GameObject rompible que tenga justo adelante: algo con Destructible
    // (arbol, pincho) o una roca empujable (PushableBox). Si no hay nada, null.
    GameObject RompibleAdelante()
    {
        if (col == null) return null;
        Bounds b = col.bounds;

        var f = new ContactFilter2D();
        f.NoFilter();
        f.useTriggers = true;   // sirve tanto si el obstaculo es solido como trigger

        float largo = b.extents.x + breakRange;
        int n = Physics2D.Raycast(b.center, new Vector2(dir, 0f), f, s_hitBuf, largo);
        for (int i = 0; i < n; i++)
        {
            var c = s_hitBuf[i].collider;
            if (c == null) continue;

            var d = c.GetComponentInParent<Destructible>();
            if (d != null) return d.gameObject;

            var caja = c.GetComponentInParent<PushableBox>();   // la roca grande tambien la parte
            if (caja != null) return caja.gameObject;
        }
        return null;
    }

    // Parte lo que tenga adelante (si es algo rompible).
    void RomperAdelante()
    {
        var go = RompibleAdelante();
        if (go == null) return;

        var d = go.GetComponent<Destructible>();
        if (d != null) { d.Break(); return; }

        go.SetActive(false);   // la roca empujable se parte (se apaga para poder volver en el checkpoint)
    }

    // Pared de VERDAD adelante. Lo rompible NO cuenta como pared: en vez de girarse,
    // se le planta adelante y lo parte para seguir avanzando.
    bool ParedRealAdelante(Vector2 origen, Vector2 dirv, float dist)
    {
        int n = Physics2D.Raycast(origen, dirv, GroundFilter(), s_hitBuf, dist);
        for (int i = 0; i < n; i++)
        {
            var c = s_hitBuf[i].collider;
            if (c == null) continue;
            if (c.GetComponentInParent<Destructible>() != null) continue;   // lo rompe, no lo esquiva
            if (c.GetComponentInParent<PushableBox>() != null) continue;    // la roca grande tambien la parte
            if (EsPendienteCaminable(s_hitBuf[i])) continue;                // es una cuesta: la sube caminando
            return true;
        }
        return false;
    }
    // PERO deja bajar y subir pendientes (colinas). La caja cuenta como obstaculo (no la atraviesa),
    // salvo que ya este metido adentro de una caja -> la ignora para poder salir.
    bool CanAdvance()
    {
        Bounds b = col.bounds;
        float ahead = b.extents.x + wallCheckMargin;
        Vector2 wdir = new Vector2(dir, 0f);

        // pared vertical alta justo adelante -> girar. OJO: lo ROMPIBLE no cuenta como pared,
        // asi se le planta adelante y lo parte en vez de darse vuelta.
        Vector2 wallEye = new Vector2(b.center.x, b.min.y + maxStepHeight + 0.05f);
        if (ParedRealAdelante(wallEye, wdir, ahead + 0.05f)) return false;

        // buscar el piso adelante: desde un poco arriba del pie hacia abajo.
        // si hay piso dentro de [subida tolerable .. bajada tolerable] -> puede avanzar (pendiente incluida)
        Vector2 probe = new Vector2(b.center.x + dir * (ahead + 0.05f), b.min.y + maxStepHeight);
        // persiguiendo se anima a bajar mucho mas (se tira a buscarte de las plataformas)
        float len = maxStepHeight + (persiguiendo ? chaseDropHeight : maxDropHeight);
        // la roca cuenta como piso solo si la tiene DEBAJO de los pies (cayo encima);
        // si la tiene a su altura, la ignora como piso y la rompe de frente
        RaycastHit2D hit = GroundRayPisable(probe, Vector2.down, len, b.min.y);
        return hit.collider != null; // no hay piso dentro del alcance -> precipicio -> girar
    }

    // Apoyarse en el piso o CAER con gravedad de verdad.
    // Antes se teletransportaba al piso de abajo (bajaba de golpe y quedaba feo);
    // ahora, si no hay piso justo debajo, se cae acelerando como corresponde.
    void SnapToGround()
    {
        if (SuperficieDebajo(out float superficieY))
        {
            // apoyado: se pega al piso
            if (enElAire) Sonidos.PlayEn(landSound, "sierra_aterriza", transform.position, Sonidos.EnPantalla);   // golpe al aterrizar
            enElAire = false;
            fallVel = 0f;
            float pivotToFoot = transform.position.y - col.bounds.min.y;
            float y = superficieY + pivotToFoot;
            // saliendo de un escondite mas bajo que el piso: sube de a poco, no aparece de golpe arriba
            if (embistiendo && SubidaSuave > 0f && y > transform.position.y)
                y = Mathf.MoveTowards(transform.position.y, y, SubidaSuave * Time.fixedDeltaTime);
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
            return;
        }

        // nada abajo: se cae con gravedad (acelerando, no de una)
        enElAire = true;
        fallVel = Mathf.Min(fallVel + fallGravity * Time.fixedDeltaTime, maxFallSpeed);
        transform.position += Vector3.down * (fallVel * Time.fixedDeltaTime);
        if (transform.position.y < killY) { EmpezarMuerte(); Desaparecer(); }   // se fue al pozo (fuera de pantalla: sin efectos)
    }

    // Superficie lo bastante horizontal como para caminarla (normal mirando para arriba).
    bool EsPendienteCaminable(RaycastHit2D h)
    {
        return h.normal.y >= Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad);
    }

    // Busca el piso bajo el cuerpo con TRES rayos (talon, centro y punta) y se queda con la
    // superficie mas ALTA. Con un solo rayo al centro, en una colina el cuerpo queda medio
    // hundido (subiendo) o medio flotando en el aire (bajando) en vez de seguir la pendiente.
    bool SuperficieDebajo(out float superficieY)
    {
        Bounds b = col.bounds;
        float reach = b.extents.y + Mathf.Max(groundSnapDistance, maxStepHeight);
        // cuanto puede SUBIR de un saque: mas que esto es un escalon/pared, no una cuesta
        float subidaMax = maxStepHeight + b.extents.x * Mathf.Tan(maxSlopeAngle * Mathf.Deg2Rad);
        float offset = b.extents.x - Mathf.Min(0.05f, b.extents.x * 0.5f);
        superficieY = 0f; bool hay = false;

        // Tres rayos (centro, punta y talon) y se apoya en la superficie MAS ALTA, como
        // apoyaria una caja de verdad. Asi nunca queda enterrado en la colina: si se guiaba
        // por la punta del pie, bajando se hundia un poco cada frame hasta que el rayo salia
        // desde adentro del piso y se trababa sin poder bajar.
        for (int i = 0; i < 3; i++)
        {
            float x = b.center.x + ((i == 0) ? 0f : ((i == 1) ? offset : -offset));
            // la roca cuenta como piso solo si la tiene DEBAJO de los pies (cayo encima)
            RaycastHit2D h = GroundRayPisable(new Vector2(x, b.center.y), Vector2.down, reach, b.min.y);
            if (h.collider == null) continue;
            if (h.point.y > b.min.y + subidaMax) continue;   // escalon alto: no se teletransporta arriba
            if (!hay || h.point.y > superficieY) { superficieY = h.point.y; hay = true; }
        }
        return hay;   // nada valido debajo -> se cae con gravedad
    }
}
