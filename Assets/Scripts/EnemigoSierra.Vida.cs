using System.Collections;
using UnityEngine;

// Parte de EnemigoSierra: golpes, stun, muerte y reaparecer. Las variables y el Update estan en EnemigoSierra.cs
public partial class EnemigoSierra
{
    /// <summary>La sierra NO se asusta: es una maquina, a la roca la parte con el sierrazo.</summary>
    public void Asustar() { }

    // ---- la piedra de Obby: flash + knockback chico + stun ----
    // Solo se stunea si la piedra le pega ARRIBA, donde va el soldado.
    // Si le pega en la parte robotica (el carro), no le hace nada.
    public void HitByRock(Vector2 fromPos)
    {
        Bounds b = col.bounds;
        float corte = b.min.y + b.size.y * stunZoneFromHeight;   // altura a partir de la cual cuenta
        if (fromPos.y < corte) return;                           // pego en el carro: rebota y listo

        if (hitFX != null) hitFX.Flash();
        Sonidos.PlayEn(stunSound, "sierra_stun", transform.position, Sonidos.EnPantalla);
        Stun(); // la piedra NUNCA lo mata, solo lo aturde
    }

    // ---- eliminado (roca, pincho): flash, polvo y se deshace en pixeles ----
    public void Defeat()
    {
        if (muerto) return;
        Bounds b = col.bounds;   // antes de apagar los colliders (apagados dan bounds vacios)
        EmpezarMuerte();
        StartCoroutine(MuerteRoutine(b));
    }

    IEnumerator MuerteRoutine(Bounds b)
    {
        if (hitFX != null) hitFX.Flash();
        Sonidos.PlayEn(deathSound, "muerte_sierra", transform.position, Sonidos.EnPantalla);
        EnemigoFX.Polvo(b, sr, 6);
        yield return new WaitForSeconds(0.08f);   // que se alcance a ver el destello
        // se deshace en cuadraditos de su propio dibujo (con sus colores, no los del destello);
        // si no se puede recortar el sprite, se aplasta contra el piso como antes
        if (!EnemigoFX.Pixelar(sr, hitFX != null ? hitFX.NormalMaterial : null))
            yield return EnemigoFX.Aplastar(transform, transform.position.y - b.min.y, 0.15f);
        Desaparecer();
    }

    /// <summary>Se rompio la plataforma donde estaba: cae con ella atravesando todo y desaparece.</summary>
    public void CaerYMorir(float gravedad, float tiempo)
    {
        if (muerto) return;
        EmpezarMuerte();
        StartCoroutine(CaidaRoutine(gravedad, tiempo));
    }

    IEnumerator CaidaRoutine(float gravedad, float tiempo)
    {
        yield return EnemigoFX.CaerAtravesando(transform, gravedad, maxFallSpeed, tiempo);
        Desaparecer();
    }

    // Deja de hacer todo: no ataca, nadie lo puede tocar y se apaga el "!".
    void EmpezarMuerte()
    {
        muerto = true;
        StopAllCoroutines();
        isAttacking = false;
        isStunned = false;
        isRecovering = false;
        rb.linearVelocity = Vector2.zero;
        sr.color = colorBase;
        if (alertIcon != null) alertIcon.SetActive(false);
        foreach (var c in colliders) if (c != null) c.enabled = false;
    }

    // Se apaga en vez de destruirse: asi puede volver si PlayerRespawn repone a los enemigos.
    void Desaparecer() { gameObject.SetActive(false); }

    // ---- reaparecer (lo llama PlayerRespawn al volver a un checkpoint, si esta activado) ----
    public static void ResetAll()
    {
        for (int i = 0; i < todos.Count; i++)
            if (todos[i] != null) todos[i].Reaparecer();
    }

    void Reaparecer()
    {
        gameObject.SetActive(true);
        StopAllCoroutines();
        muerto = false;
        isAttacking = false;
        isStunned = false;
        isRecovering = false;
        enElAire = false;
        fallVel = 0f;
        attackCdTimer = 0f;
        flipCd = 0f;
        giroCd = 0f;
        recuerdo = 0f;
        persiguiendo = false;
        embistiendo = false;
        tiempoCorte = 0f;
        golpeCd = 0f;
        sobrecargado = false;
        SubidaSuave = 0f;
        transform.position = posInicial;
        transform.localScale = escalaInicial;
        dir = dirInicial;
        rb.linearVelocity = Vector2.zero;
        sr.color = colorBase;
        foreach (var c in colliders) if (c != null) c.enabled = true;
        if (alertIcon != null) alertIcon.SetActive(false);
        SetAnim(idle);
    }

    void OnDestroy() { todos.Remove(this); }

    // Lejos de Obby se pausa: no piensa ni se mueve (ver sleepDistance).
    bool Dormido()
    {
        if (sleepDistance <= 0f || player == null) return false;
        return ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude > sleepDistance * sleepDistance;
    }

    // ---- stun ----
    public void Stun() { Stun(defaultStunTime); }

    public void Stun(float duration)
    {
        if (muerto || sobrecargado) return;   // a punto de explotar ya no se aturde
        StopAllCoroutines();
        isAttacking = false;
        sr.color = colorBase;   // por si lo agarro en medio del amague del golpe
        StartCoroutine(StunRoutine(duration));
    }

    IEnumerator StunRoutine(float duration)
    {
        isStunned = true;
        isRecovering = false;
        rb.linearVelocity = Vector2.zero;
        SetAnim(stun);
        yield return new WaitForSeconds(duration);

        isStunned = false;
        isRecovering = true;
        SetAnim(stunRecover);
        yield return new WaitForSeconds(AnimDuration(stunRecover));

        isRecovering = false;
    }

    [ContextMenu("Test Stun")]
    void TestStun() { Stun(); }
}
