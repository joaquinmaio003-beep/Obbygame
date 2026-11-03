using System.Collections;
using UnityEngine;

// Parte de EnemigoSierra: embestida, sobrecarga, explotar y colocarlo. Las variables y el Update estan en EnemigoSierra.cs
public partial class EnemigoSierra
{
    /// <summary>
    /// Embestida: corre hacia 'direccion' (1 derecha, -1 izquierda) a 'velocidad', sin patrullar
    /// ni frenar ante nada. Se puede llamar seguido para cambiarle la velocidad. Lo usa PersecucionSierra.
    /// </summary>
    public void Embestir(int direccion, float velocidad)
    {
        if (muerto) return;
        embistiendo = true;
        velEmbestida = velocidad;
        int d = direccion >= 0 ? 1 : -1;
        if (d != dir) { dir = d; ApplyFacing(); }
    }

    /// <summary>Termina la embestida: vuelve a comportarse como siempre.</summary>
    public void TerminarEmbestida() { embistiendo = false; }

    /// <summary>
    /// En una embestida: a cuantas unidades por segundo sube cuando el piso esta mas alto que sus
    /// pies. 0 = de golpe (lo normal, para seguir las cuestas). Con un valor bajo, si arranca
    /// escondido mas abajo que el piso, se lo ve salir de a poco.
    /// </summary>
    public float SubidaSuave { get; set; }

    /// <summary>
    /// Se SOBRECARGA: se clava en el lugar con la sierra girando, titila y tiembla cada vez mas
    /// rapido durante 'segundos', y explota (muere deshecho en pedazos). Final de la persecucion.
    /// </summary>
    public void Sobrecargar(float segundos)
    {
        if (muerto || sobrecargado) return;
        StopAllCoroutines();
        embistiendo = false;
        isAttacking = false;
        isStunned = false;
        isRecovering = false;
        sobrecargado = true;
        StartCoroutine(SobrecargaRoutine(segundos));
    }

    IEnumerator SobrecargaRoutine(float segundos)
    {
        Vector3 lugar = transform.position;
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            float k = t / Mathf.Max(0.01f, segundos);   // 0 -> 1: cada vez peor
            sr.color = Color.Lerp(colorBase, attackWarnColor, Mathf.PingPong(t * Mathf.Lerp(8f, 30f, k), 1f));
            transform.position = lugar + (Vector3)(Random.insideUnitCircle * (0.08f * k));
            yield return null;
        }
        transform.position = lugar;
        Explotar();
    }

    // Muere de golpe, con mas fuerza que una muerte comun: mas polvo y los pedazos salen lejos.
    void Explotar()
    {
        if (muerto) return;
        Bounds b = col.bounds;   // antes de apagar los colliders
        EmpezarMuerte();
        sobrecargado = false;
        Sonidos.PlayEn(deathSound, "muerte_sierra", transform.position, Sonidos.EnPantalla);
        EnemigoFX.Polvo(b, sr, 12);
        EnemigoFX.Pixelar(sr, hitFX != null ? hitFX.NormalMaterial : null, 2.2f);
        Desaparecer();
    }

    /// <summary>Muestra el sierrazo un momento sin frenarse (al cortar un arbol corriendo).</summary>
    public void AnimarCorte(float segundos)
    {
        if (muerto) return;
        tiempoCorte = Mathf.Max(tiempoCorte, segundos);
        Sonidos.PlayEn(attackSound, "sierra_ataque", transform.position, Sonidos.EnPantalla);
    }

    /// <summary>Vuelve a su lugar y a su estado del principio del nivel (queda prendido).</summary>
    public void VolverAlInicio() { Reaparecer(); }

    /// <summary>
    /// Lo pone con el cuerpo en esa X, ya apoyado en el piso que haya debajo de la altura 'desdeY'
    /// (los arboles y rocas no cuentan como piso). Devuelve false si ahi abajo no hay piso (o el
    /// punto cae adentro de una pared): en ese caso no lo mueve.
    /// </summary>
    public bool ColocarEn(float cuerpoX, float desdeY)
    {
        RaycastHit2D h = GroundRayPisable(new Vector2(cuerpoX, desdeY), Vector2.down, 40f, float.NegativeInfinity);
        if (h.collider == null) return false;

        // del pivote a los pies. Con la caja se calcula directo: recien prendido, los bounds del
        // collider todavia pueden no estar listos.
        float pivotToFoot = col is BoxCollider2D caja
            ? (caja.size.y * 0.5f - caja.offset.y) * Mathf.Abs(transform.lossyScale.y)
            : transform.position.y - col.bounds.min.y;
        float pivotX = cuerpoX - col.offset.x * transform.lossyScale.x;
        transform.position = new Vector3(pivotX, h.point.y + pivotToFoot, transform.position.z);
        enElAire = false;
        fallVel = 0f;
        // que el collider ya este en el lugar nuevo: si no, el proximo acomodo al piso se calculaba
        // con el lugar viejo y pegaba un salto
        Physics2D.SyncTransforms();
        return true;
    }
}
