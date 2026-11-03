using System.Collections;
using UnityEngine;

// Parte de WarriorEnemy: golpes, stun, muerte y reaparecer. Las variables y el Update estan en WarriorEnemy.cs
public partial class WarriorEnemy
{
    /// <summary>
    /// La roca lo esta empujando: se asusta mientras dure el empujon y un ratito mas. Deja de
    /// atacar (corta el golpe que estaba por dar) y mira para todos lados.
    /// </summary>
    public void Asustar()
    {
        if (muerto || isStunned || isRecovering) return;
        if (isAttacking)
        {
            StopAllCoroutines();
            isAttacking = false;
            sr.color = colorBase;
        }
        if (!Asustado && Time.time >= proximoSusto)
        {
            proximoSusto = Time.time + 1.5f;
            Sonidos.PlayEn(scareSound, "enemigo_susto", transform.position);
        }
        sustoHasta = Time.time + 0.3f;
    }

    // ---- la piedra de Obby: flash + knockback chico + stun ----
    public void HitByRock(Vector2 fromPos)
    {
        if (inmunePiedras) return;   // ni lo aturde ni parpadea: la piedra le rebota sin hacerle nada
        if (hitFX != null) hitFX.Flash();
        Sonidos.PlayEn(stunSound, transform.position, Sonidos.EnPantalla);
        Stun(); // solo stunea, sin moverlo (para no buguearlo contra paredes/bordes)
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
        Sonidos.PlayEn(deathSound, "muerte_enemigo", transform.position, Sonidos.EnPantalla);
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
        if (porDelante) PonerDelante(false);
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
        aireVelX = 0f;
        atravesarHastaY = float.PositiveInfinity;
        PonerDelante(false);
        quietoHasta = 0f;
        amenaza = false;
        retirada = false;
        attackCdTimer = 0f;
        flipCd = 0f;
        giroCd = 0f;
        recuerdo = 0f;
        atento = false;
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
        if (muerto) return;
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
