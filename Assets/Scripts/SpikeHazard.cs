using UnityEngine;

/// <summary>
/// Pincho fijo: si Obby lo toca, le baja una vida (flash + knockback + breve
/// invulnerabilidad), pero NO lo teleporta al checkpoint. A los enemigos los mata.
/// Poner en un GameObject con un Collider2D (Trigger) sobre las puas.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SpikeHazard : MonoBehaviour
{
    [Tooltip("Que tan alto hace saltar a Obby cuando lo pincha, en unidades (su salto normal es 3.2). 0 = no lo hace saltar.")]
    public float bounceHeight = 4.5f;

    static readonly Collider2D[] s_buf = new Collider2D[8];

    /// <summary>
    /// Hay pinchos adelante de un enemigo que camina (en el piso de adelante o abajo, en el pozo
    /// que va a bajar): asi frena y se da vuelta en vez de meterse solo.
    /// </summary>
    public static bool HayAdelante(Bounds cuerpo, int dir, float subida, float bajada)
    {
        float ancho = 0.4f;
        float x = cuerpo.center.x + dir * (cuerpo.extents.x + ancho * 0.5f);
        float arriba = cuerpo.min.y + subida + 0.2f;
        float abajo = cuerpo.min.y - bajada;
        var filtro = new ContactFilter2D { useTriggers = true };
        int n = Physics2D.OverlapBox(new Vector2(x, (arriba + abajo) * 0.5f),
                                     new Vector2(ancho, arriba - abajo), 0f, filtro, s_buf);
        for (int i = 0; i < n; i++)
            if (s_buf[i] != null && s_buf[i].GetComponentInParent<SpikeHazard>() != null) return true;
        return false;
    }

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        var respawn = other.GetComponentInParent<PlayerRespawn>();
        if (respawn != null)
        {
            // si le saco una vida, lo hace saltar para arriba (asi no se queda clavado en las puas)
            if (respawn.HurtPincho(transform.position) && bounceHeight > 0f)
            {
                var obby = respawn.GetComponent<PlayerController2D>();
                if (obby != null) obby.Rebotar(bounceHeight);
            }
            return;
        }

        // un enemigo que cae en los pinchos se muere (se deshace en pixeles)
        var enemigo = other.GetComponentInParent<IStunnable>();
        if (enemigo != null) enemigo.Defeat();
    }
}
