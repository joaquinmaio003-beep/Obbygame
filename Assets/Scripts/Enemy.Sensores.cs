using System.Collections;
using UnityEngine;

// Parte de Enemy: mirar al jugador, piso, paredes y pendientes. Las variables y el Update estan en Enemy.cs
public partial class Enemy
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

    void Flip()
    {
        dir = -dir;
        ApplyFacing();
    }

    void ApplyFacing()
    {
        var s = transform.localScale;
        float x = Mathf.Abs(s.x) * dir; // conserva la escala que le pusiste
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

    // deteccion relativa al tamano real (bounds) -> funciona a cualquier escala
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

    // Raycast contra el piso pero IGNORANDO los cajones empujables (PushableBox); devuelve el hit MAS CERCANO.
    // Asi el enemigo no se trepa ni se queda parado arriba de una caja.
    RaycastHit2D GroundRayNoBox(Vector2 origin, Vector2 dir, float dist)
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
            if (h.collider.GetComponentInParent<PushableBox>() != null) continue; // es una caja, no piso
            if (h.distance < bestD) { bestD = h.distance; best = h; }
        }
        return best;
    }

    // ¿El enemigo esta metido DENTRO de una caja? (para poder salir en vez de quedar trabado)
    bool OverlappingBox()
    {
        int n = Physics2D.OverlapBox(col.bounds.center, col.bounds.size, 0f, GroundFilter(), s_colBuf);
        for (int i = 0; i < n; i++)
            if (s_colBuf[i] != null && s_colBuf[i].GetComponentInParent<PushableBox>() != null) return true;
        return false;
    }

    // ¿Puede seguir caminando hacia 'dir'? Detecta pared/escalon alto y precipicio,
    // PERO deja bajar y subir pendientes (colinas). La caja cuenta como obstaculo (no la atraviesa),
    // salvo que el enemigo ya este metido adentro de una caja -> la ignora para poder salir.
    bool CanAdvance()
    {
        Bounds b = col.bounds;
        float ahead = b.extents.x + wallCheckMargin;
        bool insideBox = OverlappingBox();
        Vector2 wdir = new Vector2(dir, 0f);

        // pared vertical alta justo adelante (por encima de un escalon tolerable) -> girar
        Vector2 wallEye = new Vector2(b.center.x, b.min.y + maxStepHeight + 0.05f);
        if (ParedAdelante(wallEye, wdir, ahead + 0.05f, insideBox)) return false;

        // pinchos adelante (en el piso o abajo, en el pozo que va a bajar): no se mete solo
        if (SpikeHazard.HayAdelante(b, dir, maxStepHeight, maxDropHeight)) return false;

        // buscar el piso adelante: desde un poco arriba del pie hacia abajo.
        // si hay piso dentro de [subida tolerable .. bajada tolerable] -> puede avanzar (pendiente incluida)
        Vector2 probe = new Vector2(b.center.x + dir * (ahead + 0.05f), b.min.y + maxStepHeight);
        float len = maxStepHeight + maxDropHeight;
        RaycastHit2D hit = insideBox ? GroundRayNoBox(probe, Vector2.down, len)
                                     : Physics2D.Raycast(probe, Vector2.down, len, groundLayer);
        return hit.collider != null; // no hay piso dentro del alcance -> precipicio -> girar
    }

    // Apoyarse en el piso o CAERSE con gravedad (ej: la roca lo empujo fuera de un borde).
    // Antes no tenia caida: sin piso quedaba flotando en el aire y dandose vuelta.
    void SnapToGround()
    {
        if (SuperficieDebajo(out float superficieY))
        {
            enElAire = false;
            fallVel = 0f;
            float pivotToFoot = transform.position.y - col.bounds.min.y; // pivote respecto a la base del collider
            transform.position = new Vector3(transform.position.x, superficieY + pivotToFoot, transform.position.z);
            return;
        }

        // nada abajo: se cae acelerando, como la sierra
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

    // Hay una PARED de verdad adelante? Una COLINA no cuenta: si la superficie esta
    // inclinada menos que maxSlopeAngle la sube caminando en vez de darse vuelta.
    bool ParedAdelante(Vector2 origen, Vector2 dirv, float dist, bool ignorarCaja)
    {
        int n = Physics2D.Raycast(origen, dirv, GroundFilter(), s_hitBuf, dist);
        for (int i = 0; i < n; i++)
        {
            var h = s_hitBuf[i];
            if (h.collider == null) continue;
            bool esCaja = h.collider.GetComponentInParent<PushableBox>() != null;
            if (ignorarCaja && esCaja) continue;                 // ya esta adentro: lo deja salir
            if (!esCaja && EsPendienteCaminable(h)) continue;    // es una cuesta, no una pared
            return true;
        }
        return false;
    }

    // Busca el piso bajo el cuerpo con TRES rayos (talon, centro y punta) y se queda con la
    // superficie mas ALTA. Con un solo rayo al centro, en una colina el cuerpo queda medio
    // hundido (subiendo) o medio flotando en el aire (bajando) en vez de seguir la pendiente.
    bool SuperficieDebajo(out float superficieY)
    {
        Bounds b = col.bounds;
        // alcance corto: si no hay piso cerca se CAE con gravedad (ver SnapToGround)
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
            RaycastHit2D h = GroundRayNoBox(new Vector2(x, b.center.y), Vector2.down, reach);
            if (h.collider == null) continue;
            if (h.point.y > b.min.y + subidaMax) continue;   // escalon alto: no se teletransporta arriba
            if (!hay || h.point.y > superficieY) { superficieY = h.point.y; hay = true; }
        }
        return hay;
    }
}
