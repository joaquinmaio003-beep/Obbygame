using UnityEngine;
using UnityEngine.InputSystem;

// Parte de PlayerController2D: piso, pendientes, paredes, cajas, sonido de salto y dash. Las variables y el Update estan en PlayerController2D.cs
public partial class PlayerController2D
{
    // El piso de abajo esta inclinado (no plano, pero tampoco una pared).
    bool EnPendiente()
    {
        if (col == null) return false;
        Bounds b = col.bounds;
        int n = Physics2D.Raycast(new Vector2(b.center.x, b.min.y + 0.05f), Vector2.down, LayerFilter(groundLayer), s_hitBuf, 0.4f);
        for (int i = 0; i < n; i++)
        {
            var h = s_hitBuf[i];
            if (h.collider == null || h.collider.isTrigger) continue;
            return h.normal.y < 0.995f && h.normal.y > 0.35f;
        }
        return false;
    }

    // Piso: 3 rayos hacia ABAJO (izq/centro/der), pero SIEMPRE dentro del cuerpo de Obby
    // (borde del collider con un margen), asi nunca se meten en una pared pegada al costado
    // y confunden pared con piso. groundCheckSize.y = largo de los rayos (que tan abajo mira).
    bool GroundCheck()
    {
        float len = Mathf.Max(0.02f, groundCheckSize.y);
        float originY = groundCheck != null ? groundCheck.position.y
                       : (col != null ? col.bounds.min.y : rb.position.y);

        float inset = 0.03f;
        float leftX, rightX, midX;
        if (col != null)
        {
            leftX = col.bounds.min.x + inset;   // apenas adentro del borde izquierdo
            rightX = col.bounds.max.x - inset;  // apenas adentro del borde derecho
            midX = col.bounds.center.x;
        }
        else
        {
            float halfW = groundCheckSize.x * 0.5f;
            leftX = rb.position.x - halfW; rightX = rb.position.x + halfW; midX = rb.position.x;
        }

        // 3 rayos (con el buffer reutilizable, no generan basura)
        if (PisoEn(leftX, originY, len)) return true;
        if (PisoEn(midX, originY, len)) return true;
        if (PisoEn(rightX, originY, len)) return true;
        return false;
    }

    // Un rayo de piso hacia abajo.
    bool PisoEn(float x, float y, float len)
    {
        int n = Physics2D.Raycast(new Vector2(x, y), Vector2.down, LayerFilter(groundLayer), s_hitBuf, len);
        for (int i = 0; i < n; i++)
        {
            var c = s_hitBuf[i].collider;
            if (c == null) continue;
            if (!c.usedByEffector) return true;   // piso comun

            // Plataforma de UN SOLO LADO (el taladro): se atraviesa desde abajo. Cuenta como piso
            // solo con los pies ARRIBA de ella y sin venir subiendo rapido (el taladro sube a 2 al
            // volver). Si no, al atravesarla de un salto lo daba por parado un instante y podia
            // volver a saltar en el aire (doble salto).
            bool piesArriba = col == null || col.bounds.min.y >= c.bounds.max.y - 0.05f;
            if (piesArriba && rb.linearVelocity.y < 4f) return true;
        }
        return false;
    }

    // Buffer y filtro reutilizables para las consultas de fisica (no reservan memoria por frame).
    static readonly RaycastHit2D[] s_hitBuf = new RaycastHit2D[8];
    static ContactFilter2D LayerFilter(LayerMask mask)
    {
        var f = new ContactFilter2D();
        f.useTriggers = Physics2D.queriesHitTriggers;
        f.SetLayerMask(mask);
        return f;
    }

    // Pared a ese lado (dir: -1 izq, +1 der). Tira 3 rayos desde el BORDE del collider
    // (arriba/medio/abajo), no desde el centro, asi engancha durante todo el deslizamiento
    // y no solo cuando Obby queda calzado en una esquina.
    // NO cuentan como pared: cajones empujables (PushableBox) ni pinchos (FallingSpike/SpikeHazard),
    // asi no te podes colgar de ellos.
    bool WallOnSide(int dir)
    {
        LayerMask mask = wallLayer.value != 0 ? wallLayer : groundLayer;

        Bounds b = col != null ? col.bounds : new Bounds(rb.position, Vector3.one * 0.5f);
        float edgeX = dir > 0 ? b.max.x : b.min.x;         // borde de Obby de ese lado
        float inset = 0.02f;                                // arranca apenas adentro del borde
        Vector2 baseO = new Vector2(edgeX - dir * inset, b.center.y);
        float yTop = b.extents.y * 0.8f;                    // rayos arriba, centro y abajo

        var filter = LayerFilter(mask);
        for (int i = -1; i <= 1; i++)
        {
            Vector2 o = baseO + new Vector2(0f, i * yTop);
            int n = Physics2D.Raycast(o, new Vector2(dir, 0f), filter, s_hitBuf, wallCheckDistance);
            for (int j = 0; j < n; j++)
            {
                var hit = s_hitBuf[j];
                if (hit.collider == null) continue;
                if (!IsClingable(hit.collider)) continue;  // cajon o pincho -> no es pared, seguir mirando atras
                if (hit.distance <= 0.0001f) continue;     // rayo nacido adentro de un collider: dato basura
                if (Mathf.Abs(hit.normal.y) > 0.4f) continue;  // superficie tumbada = colina/piso, NO una pared:
                                                               // bajando una cuesta la loma de atras contaba como
                                                               // pared y le daba vuelta la cara a Obby
                return true;                                // pared de verdad
            }
        }
        return false;
    }

    // Da vuelta el sprite hacia 'dir' (1 der, -1 izq).
    void SetFacing(int dir)
    {
        if (dir == 0 || dir == facing) return;
        facing = dir;
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * facing;
        transform.localScale = s;
    }

    // ¿Hay una caja empujable justo adelante (para no empujarla de mas con el dash)?
    bool BoxAhead(int d)
    {
        if (col == null) return false;
        Bounds b = col.bounds;
        float edgeX = d > 0 ? b.max.x : b.min.x;
        float inset = 0.06f;
        // Tres alturas (pies, medio y casi la cabeza): asi detecta cajas bajas Y altas.
        // Antes tiraba un solo rayo al centro y fallaba con cajas mas bajas que Obby.
        if (BoxAtHeight(edgeX, b.min.y + inset, d, b.min.y)) return true;
        if (BoxAtHeight(edgeX, b.center.y, d, b.min.y)) return true;
        if (BoxAtHeight(edgeX, b.max.y - inset, d, b.min.y)) return true;
        return false;
    }

    // Un rayo horizontal a cierta altura buscando una caja empujable.
    // Ignora la caja sobre la que Obby esta PARADO (esa esta debajo de sus pies, no se empuja).
    bool BoxAtHeight(float x, float y, int d, float feetY)
    {
        int n = Physics2D.Raycast(new Vector2(x, y), new Vector2(d, 0f),
                                  LayerFilter(Physics2D.DefaultRaycastLayers), s_hitBuf, 0.3f);
        for (int j = 0; j < n; j++)
        {
            var c = s_hitBuf[j].collider;
            if (c == null) continue;
            if (c.GetComponentInParent<PushableBox>() == null) continue;
            if (c.bounds.max.y <= feetY + 0.05f) continue; // esta parado ENCIMA de esa caja
            return true;
        }
        return false;
    }


    // ¿Obby esta parado ENCIMA de una caja empujable? (ahi no corresponde la pose de empujar)
    bool StandingOnBox()
    {
        if (col == null) return false;
        Bounds b = col.bounds;
        float len = Mathf.Max(0.05f, groundCheckSize.y) + 0.1f;
        float y = b.min.y + 0.02f;
        float inset = 0.04f;
        if (BoxBelowAt(b.min.x + inset, y, len)) return true;
        if (BoxBelowAt(b.center.x, y, len)) return true;
        if (BoxBelowAt(b.max.x - inset, y, len)) return true;
        return false;
    }

    bool BoxBelowAt(float x, float y, float len)
    {
        int n = Physics2D.Raycast(new Vector2(x, y), Vector2.down,
                                  LayerFilter(Physics2D.DefaultRaycastLayers), s_hitBuf, len);
        for (int j = 0; j < n; j++)
        {
            var c = s_hitBuf[j].collider;
            if (c != null && c.GetComponentInParent<PushableBox>() != null) return true;
        }
        return false;
    }
    // ¿A este collider te podes colgar (es pared de verdad)? No a cajones ni pinchos.
    bool IsClingable(Collider2D c)
    {
        if (c.usedByEffector) return false;   // plataforma de un solo lado (el taladro): de costado se atraviesa
        if (c.GetComponentInParent<PushableBox>() != null) return false;
        if (c.GetComponentInParent<FallingSpike>() != null) return false;
        if (c.GetComponentInParent<SpikeHazard>() != null) return false;
        return true;
    }

    void PlayJumpSound()
    {
        if (jumpSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(jumpSound);
    }

    void StartDash()
    {
        isDashing = true;
        dashTimer = dashDuration;
        Sonidos.Play(dashSound, "dash");
        dashCdTimer = dashCooldown;
        // dashea hacia donde apunta el input; si no hay, hacia donde mira.
        // Colgado de una pared y sin tocar direccion: hacia AFUERA de la pared (antes se
        // estrellaba contra la pared y el dash se desperdiciaba).
        if (Mathf.Abs(moveInput) > 0.01f) dashDir = moveInput > 0 ? 1 : -1;
        else if (wallContact != 0 && !isGrounded) dashDir = -wallContact;
        else dashDir = facing;
        SetFacing(dashDir);   // que mire hacia donde sale disparado
    }
}
