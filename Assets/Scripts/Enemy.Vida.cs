using System.Collections;
using UnityEngine;

// Parte de Enemy: golpes, stun, muerte y reaparecer. Las variables y el Update estan en Enemy.cs
public partial class Enemy
{
    /// <summary>
    /// La roca lo esta empujando: se asusta mientras dure el empujon y un ratito mas. Deja de
    /// disparar (corta el tiro que estaba por soltar) y mira para todos lados.
    /// </summary>
    public void Asustar()
    {
        if (muerto || isStunned || isRecovering) return;
        if (isShooting)
        {
            StopAllCoroutines();
            isShooting = false;
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
        isShooting = false;
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
        isShooting = false;
        isStunned = false;
        isRecovering = false;
        enElAire = false;
        fallVel = 0f;
        shootCdTimer = 0f;
        flipCd = 0f;
        giroCd = 0f;
        recuerdo = 0f;
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

    public void Stun() { Stun(defaultStunTime); }

    public void Stun(float duration)
    {
        if (muerto) return;
        StopAllCoroutines();
        isShooting = false;
        StartCoroutine(StunRoutine(duration));
    }

    IEnumerator StunRoutine(float duration)
    {
        // 1) stuneado: loop de los frames 00-03 mientras dura
        isStunned = true;
        isRecovering = false;
        rb.linearVelocity = Vector2.zero;
        SetAnim(stun);
        yield return new WaitForSeconds(duration);

        // 2) recuperacion: se levanta y agarra el rifle (frames 04-05, una vez)
        isStunned = false;
        isRecovering = true;
        SetAnim(stunRecover);
        yield return new WaitForSeconds(AnimDuration(stunRecover));

        isRecovering = false;
    }

    // duracion de una animacion (frames / fps)
    float AnimDuration(Anim a)
    {
        if (a == null || a.frames == null || a.frames.Length == 0) return 0f;
        return a.frames.Length / Mathf.Max(1f, a.fps);
    }

    [ContextMenu("Test Stun")]
    void TestStun() { Stun(); }
}
