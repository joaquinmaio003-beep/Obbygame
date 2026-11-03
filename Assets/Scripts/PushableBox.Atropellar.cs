using UnityEngine;

// Parte de PushableBox: aplastar y arrastrar enemigos. Las variables y el Update estan en PushableBox.cs
public partial class PushableBox
{
    // enemigos con collider trigger (kinematic) -> entran por aca
    void OnTriggerEnter2D(Collider2D other) { TryRunOver(other); }
    void OnTriggerStay2D(Collider2D other)  { TryRunOver(other); }

    // por si el enemigo tuviera un collider solido -> entra por aca
    void OnCollisionEnter2D(Collision2D c)  { TryRunOver(c.collider); CaeSobreObby(c); }
    void OnCollisionStay2D(Collision2D c)   { TryRunOver(c.collider); }

    void TryRunOver(Collider2D other)
    {
        if (encajada) return;   // hecha plataforma: no atropella ni arrastra a nadie

        // Quieta no atropella, no arrastra ni le cae encima a nadie: se corta antes de buscar nada
        // (esto corre en cada paso de fisica por cada cosa que este tocando la roca).
        Vector2 v = rb.linearVelocity;
        if (v.sqrMagnitude < 0.0025f) return;

        var enemy = other.GetComponentInParent<IStunnable>();
        if (enemy == null) return;

        // Parado ENCIMA de la roca (la sierra se puede subir si le cae arriba): no lo atropella ni
        // lo arrastra. Antes, al empujarla, lo tiraba de golpe al costado o lo mataba "de la nada"
        // contra una pared, y rodando por una bajada lo mataba aunque fuera arriba.
        if (ParadoEncima(other)) return;


        // ATROPELLO: va rapido de costado POR SU CUENTA (rodando por una colina, o suelta despues
        // de un empujon fuerte). Mientras Obby la esta empujando NO cuenta: eso es empujar.
        if (!EmpujadaPorObby && Mathf.Abs(v.x) >= runOverSpeed) { Aplastar(enemy); return; }

        // LE CAE ENCIMA: viene cayendo EN EL AIRE (mas para abajo que de costado) y el bicho esta
        // debajo. Bajando una colina apoyada no cuenta: en las cuestas de 45 grados la roca baja casi
        // tan rapido como avanza, y eso mataba a los bichos de la nada al empujarla cuesta abajo.
        // Antes alcanzaba con que bajara un poquito: empujandola por una leve bajada ya mataba.
        bool cayendo = !enElPiso && v.y <= -crushFallSpeed && -v.y >= Mathf.Abs(v.x);
        if (cayendo)
        {
            Vector2 boxC = col.bounds.center;
            Vector2 enemyC = other.bounds.center;
            bool below = enemyC.y < boxC.y;   // el bicho esta debajo de la roca
            bool under = Mathf.Abs(enemyC.x - boxC.x) <= col.bounds.extents.x + other.bounds.extents.x;
            if (below && under) Aplastar(enemy);
            return;
        }

        // A paso lento (Obby empujando): no lo mata al tocarlo, lo va ARRASTRANDO.
        // Si lo deja contra una pared, ahi si lo aplasta.
        Arrastrar(other, enemy, v);
    }

    void Aplastar(IStunnable enemy)
    {
        enemy.Defeat();
        Sonidos.PlayEn(runOverSound, "aplastar", rb.position, Sonidos.EnPantalla);
        // temblor al aplastar, solo si pasa a la vista (lejos de la camara no tiene por que temblar)
        if (shakeOnCrush && cam != null && Pantalla.Ve(rb.position, col.bounds.extents, 3f))
            cam.Shake(crushShakeDuration, crushShakeIntensity);
    }

    // Corre al bicho lo que la roca se le metio, DE A POCO (lo va empujando). Si detras tiene una
    // pared no tiene adonde ir: queda aplastado entre la roca y la pared.
    void Arrastrar(Collider2D other, IStunnable enemy, Vector2 v)
    {
        Bounds roca = col.bounds;
        Bounds bicho = other.bounds;
        int d = bicho.center.x >= roca.center.x ? 1 : -1;   // de que lado de la roca esta el bicho
        if (v.x * d <= 0.05f) return;                        // la roca no va hacia el: no lo empuja

        // Hasta donde llega la roca DE VERDAD a la altura del bicho. Antes se usaba el recuadro
        // que la envuelve, y la roca es irregular (abajo es mas angosta): lo mandaba lejos de
        // donde lo tocaba, la roca lo volvia a alcanzar y lo mandaba lejos otra vez, a los saltos.
        if (!BordeReal(bicho, d, out float borde)) return;
        float solape = d > 0 ? borde - bicho.min.x : bicho.max.x - borde;
        if (solape <= 0f) return;

        // lo corre como mucho un poco mas rapido de lo que avanza la roca: si quedo muy metido
        // adentro, lo va sacando mientras lo empuja en vez de teletransportarlo al borde
        float paso = Mathf.Min(solape, Mathf.Abs(v.x) * Time.fixedDeltaTime * 2f + 0.02f);

        if (ParedDetras(bicho, d, paso + 0.02f)) { Aplastar(enemy); return; }

        enemy.Asustar();   // lo viene empujando: se asusta

        var comp = enemy as Component;
        if (comp != null) comp.transform.position += new Vector3(d * paso, 0f, 0f);
    }

    // El borde de la roca del lado 'd' (1 derecha, -1 izquierda), medido a la altura del bicho
    // (pies, medio y cabeza): rayos desde afuera hacia la roca. false si a esa altura no hay roca.
    bool BordeReal(Bounds bicho, int d, out float borde)
    {
        Bounds roca = col.bounds;
        var filter = new ContactFilter2D();
        filter.NoFilter();
        filter.useTriggers = false;   // los enemigos (trigger) no cuentan

        float desdeX = d > 0 ? roca.max.x + 0.5f : roca.min.x - 0.5f;
        float largo = roca.size.x + 1f;
        borde = 0f;
        bool hay = false;
        for (int i = 0; i < 3; i++)
        {
            float y = i == 0 ? bicho.min.y + 0.05f : i == 1 ? bicho.center.y : bicho.max.y - 0.05f;
            int n = Physics2D.Raycast(new Vector2(desdeX, y), new Vector2(-d, 0f), filter, s_rayBuf, largo);
            for (int j = 0; j < n; j++)
            {
                if (s_rayBuf[j].collider != col) continue;
                float x = s_rayBuf[j].point.x;
                if (!hay || (d > 0 ? x > borde : x < borde)) { borde = x; hay = true; }   // el punto que mas sobresale
                break;
            }
        }
        return hay;
    }

    // El bicho esta parado encima de ESTA roca? Mira lo primero solido que tiene bajo los pies.
    // (Con los bounds no alcanza: la roca se inclina con las colinas.)
    bool ParadoEncima(Collider2D bicho)
    {
        Bounds b = bicho.bounds;
        var filter = new ContactFilter2D();
        filter.SetLayerMask(Physics2D.AllLayers);
        filter.useTriggers = false;   // los enemigos (trigger) no cuentan
        int n = Physics2D.Raycast(new Vector2(b.center.x, b.min.y + 0.05f), Vector2.down, filter, s_rayBuf, 0.3f);
        for (int i = 0; i < n; i++)
        {
            var c = s_rayBuf[i].collider;
            if (c == null) continue;
            if (s_rayBuf[i].distance <= 0.0001f) continue;   // arranco adentro: la roca se le metio encima, no esta arriba
            if (c.GetComponentInParent<PlayerController2D>() != null) continue;   // Obby no es piso
            return c == col;
        }
        return false;
    }

    // Hay una PARED de verdad justo detras del bicho, del lado hacia donde lo empujan?
    bool ParedDetras(Bounds bicho, int d, float dist)
    {
        var filter = new ContactFilter2D();
        filter.SetLayerMask(groundMask.value != 0 ? groundMask.value : Physics2D.AllLayers);
        filter.useTriggers = false;   // los enemigos (trigger) no cuentan
        Vector2 origen = new Vector2(d > 0 ? bicho.max.x : bicho.min.x, bicho.center.y);
        int n = Physics2D.Raycast(origen, new Vector2(d, 0f), filter, s_rayBuf, dist);
        for (int i = 0; i < n; i++)
        {
            var h = s_rayBuf[i];
            if (h.collider == null || h.collider == col) continue;                       // la propia roca
            if (h.collider.GetComponentInParent<PlayerController2D>() != null) continue; // Obby no es pared
            if (Mathf.Abs(h.normal.x) < 0.9f) continue;                                 // piso o colina, no pared
            return true;
        }
        return false;
    }
}
