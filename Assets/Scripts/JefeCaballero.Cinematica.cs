using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// Parte de JefeCaballero: la cinematica del comienzo y saltearla con espacio. Las variables y el Update estan en JefeCaballero.cs
public partial class JefeCaballero
{
    IEnumerator Cinematica()
    {
        // espera a que Obby llegue (o arranca enseguida)
        // espera a que Obby llegue cerca (y pase Intro From, si pusiste uno)
        while (player == null || (!introOnLevelStart && !ObbyLlego()))
            yield return null;

        if (obby != null) obby.ControlBloqueado = true;
        Vector2 entrada = player.position;   // por donde entro Obby: ahi va la roca que tapa la entrada
        entradaX = entrada.x;
        entradaCinematica = entrada;
        cinematicaArranco = true;

        // Obby camina solo hasta la arena: siempre hacia el jefe (nunca para atras), salta al
        // llegar al borde y para cuando paso el ultimo punto de la intro y esta en el piso.
        if (obby != null && (introWalkTo != null || introJumpAt != null))
        {
            float lado = Mathf.Sign(transform.position.x - player.position.x);
            float meta = UltimoPuntoIntro();
            bool saltoEnPunto = introJumpAt == null;
            bool saltoEnBorde = false;
            for (float t = 0f; t < 8f; t += Time.deltaTime)
            {
                obby.MoverSolo = lado;
                if (!saltoEnPunto && (player.position.x - introJumpAt.position.x) * lado >= 0f)
                {
                    saltoEnPunto = true;
                    if (obby.IsGrounded) { obby.SaltarSolo(); saltoEnBorde = true; }
                }
                else if (!saltoEnBorde && obby.IsGrounded && SeTerminaElPiso(lado)) { obby.SaltarSolo(); saltoEnBorde = true; }
                if ((player.position.x - meta) * lado >= 0f && obby.IsGrounded) break;
                yield return null;
            }
            obby.MoverSolo = 0f;
            for (float t = 0f; t < 2f && !obby.IsGrounded; t += Time.deltaTime) yield return null;
        }

        // se da vuelta, lo mira y se le rie
        if (cam != null) cam.EnfocarDos(transform, player);   // se ven los dos
        yield return new WaitForSeconds(0.6f);
        Encarar();
        yield return new WaitForSeconds(0.35f);
        Poner(laugh, true);
        Sonidos.Play(laughSound, "jefe_risa");
        if (obby != null) obby.Susto();
        yield return new WaitForSeconds(laughTime);

        // La PRIMERA roca le tapa la entrada: en la puerta que marcaste o, si no marcaste ninguna,
        // atras de donde entro Obby.
        if (PuertaDeLaCinematica(out Vector2 puerta))
        {
            // levanta la roca despacio (con todo el peso) y la tira en un arco lento
            var levantaLento = new Anim { frames = lift.frames, fps = lift.fps * 0.5f };
            Poner(levantaLento, false);
            yield return new WaitForSeconds(DuracionDe(levantaLento) + 0.3f);
            RocaGrande tapon = Lanzar(puerta, TamanoTapon(puerta), doorRockFlightTime, true);
            taponCinematica = tapon;
            Poner(idle, true);
            // la camara se va con la roca hasta que se estrella
            if (cam != null && tapon != null) cam.Enfocar(tapon.transform, true);
            for (float t = 0f; t < 2.5f && tapon != null && !tapon.Asentada; t += Time.deltaTime) yield return null;
            if (cam != null) cam.Shake(0.45f, 0.3f);
            yield return new WaitForSeconds(0.8f);
            // otra risita (de nuevo los dos)
            if (cam != null) cam.EnfocarDos(transform, player);
            Poner(laugh, true);
            Sonidos.Play(laughSound, "jefe_risa");
            yield return new WaitForSeconds(0.9f);
        }

        if (cam != null) cam.SoltarFoco();
        yield return new WaitForSeconds(0.4f);
        if (obby != null) obby.ControlBloqueado = false;
        Poner(idle, true);
        cinematicaTermino = true;
    }

    // ---------------- saltear la cinematica ----------------
    // La primera vez se ve entera. Si moris y el nivel vuelve a empezar, abajo a la derecha aparece
    // "ESPACIO: SALTAR CINEMATICA" y con espacio (o el boton de saltar del joystick) se saltea.

    static bool s_cinematicaVista;   // sobrevive al reinicio del nivel (no al cerrar el juego)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void OlvidarCinematica() { s_cinematicaVista = false; }

    bool cinematicaArranco, cinematicaTermino;
    Vector2 entradaCinematica;
    RocaGrande taponCinematica;

    IEnumerator CinematicaSalteable()
    {
        if (!s_cinematicaVista)
        {
            yield return Cinematica();
            s_cinematicaVista = true;
            yield break;
        }

        cinematicaArranco = cinematicaTermino = false;
        Coroutine corriendo = StartCoroutine(Cinematica());
        TextMeshProUGUI cartel = null;
        while (!cinematicaTermino)
        {
            if (cinematicaArranco)
            {
                if (cartel == null) cartel = CrearCartelSaltar();
                var c = cartel.color; c.a = 0.65f + 0.35f * Mathf.Sin(Time.time * 5f); cartel.color = c;   // titila suave
                if (ApretoSaltar())
                {
                    StopCoroutine(corriendo);
                    yield return SaltarCinematica();
                    break;
                }
            }
            yield return null;
        }
        if (cartel != null) Destroy(cartel.canvas.gameObject);
    }

    static bool ApretoSaltar()
    {
        return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
    }

    // Deja todo como al final de la cinematica: Obby en la arena, la puerta tapada, a pelear.
    IEnumerator SaltarCinematica()
    {
        if (obby != null)
        {
            obby.MoverSolo = 0f;
            if (introWalkTo != null || introJumpAt != null)
            {
                float x = UltimoPuntoIntro();
                RaycastHit2D h = Physics2D.Raycast(new Vector2(x, PiesY + 6f), Vector2.down, 20f, groundLayer);
                if (h.collider != null)
                {
                    float pies = obbyCol != null ? player.position.y - obbyCol.bounds.min.y : 0.4f;
                    player.position = new Vector3(x, h.point.y + pies + 0.02f, player.position.z);
                    var rbObby = obby.GetComponent<Rigidbody2D>();
                    if (rbObby != null) { rbObby.position = player.position; rbObby.linearVelocity = Vector2.zero; }
                    Physics2D.SyncTransforms();
                }
            }
        }

        // la puerta queda tapada
        if (taponCinematica != null) taponCinematica.AterrizarYa();
        else if (PuertaDeLaCinematica(out Vector2 puerta))
        {
            var r = RocaGrande.TirarDelJefe(rockSprite, TamanoTapon(puerta), puerta + Vector2.up * 3f, puerta, 0.5f,
                                            groundLayer, sr.sortingOrder + 1, true, false);
            r.AterrizarYa();
        }

        Encarar();
        Poner(idle, true);
        sr.color = colorBase;
        if (cam != null) { cam.SoltarFoco(); cam.SnapToTarget(); }
        yield return null;   // un cuadro: que el mismo ESPACIO no lo haga saltar
        if (obby != null) obby.ControlBloqueado = false;
    }

    // La puerta que tapa la roca: la que marcaste o, si no, atras de donde entro Obby.
    bool PuertaDeLaCinematica(out Vector2 puerta)
    {
        if (door != null) { puerta = door.position; return true; }
        return EntradaDetras(entradaCinematica, out puerta);
    }

    TextMeshProUGUI CrearCartelSaltar()
    {
        var go = new GameObject("Saltar cinematica");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        var escala = go.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920, 1080);
        escala.matchWidthOrHeight = 0.5f;

        var txt = new GameObject("Texto");
        txt.transform.SetParent(go.transform, false);
        var t = txt.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) t.font = uiFont;
        t.text = "ESPACIO: SALTAR CINEMATICA";
        t.fontSize = 30;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.BottomRight;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-40f, 36f);
        rt.sizeDelta = new Vector2(1100f, 80f);
        return t;
    }

    // Obby llego: esta cerca del jefe o paso el primer punto de la intro (y Intro From, si hay).
    bool ObbyLlego()
    {
        float lejos = Mathf.Abs(player.position.x - transform.position.x);
        if (introFrom != null && (player.position.x - introFrom.position.x) * Mathf.Sign(transform.position.x - introFrom.position.x) < 0f)
            return false;
        if (lejos <= wakeDistance) return true;
        if (introWalkTo != null && lejos <= Mathf.Abs(introWalkTo.position.x - transform.position.x)) return true;
        if (introJumpAt != null && lejos <= Mathf.Abs(introJumpAt.position.x - transform.position.x)) return true;
        return false;
    }

    // De los puntos de la intro, el mas cerca del jefe: ahi para de caminar Obby.
    float UltimoPuntoIntro()
    {
        float x = player.position.x;
        float mejor = float.MaxValue;
        foreach (var p in new[] { introWalkTo, introJumpAt })
        {
            if (p == null) continue;
            float d = Mathf.Abs(p.position.x - transform.position.x);
            if (d < mejor) { mejor = d; x = p.position.x; }
        }
        return x;
    }

    // Adelante de Obby no hay piso (llego al borde): ahi salta.
    bool SeTerminaElPiso(float lado)
    {
        if (obbyCol == null) return false;
        Bounds b = obbyCol.bounds;
        Vector2 o = new Vector2(b.center.x + lado * (b.extents.x + 0.25f), b.min.y + 0.1f);
        return Physics2D.Raycast(o, Vector2.down, 0.8f, groundLayer).collider == null;
    }

    // El piso un poco atras de donde entro Obby (del lado contrario al jefe): ahi cae el tapon.
    bool EntradaDetras(Vector2 entrada, out Vector2 punto)
    {
        punto = default;
        float lado = Mathf.Sign(transform.position.x - entrada.x);   // hacia el jefe
        float x = entrada.x - lado * (doorRockSize * 0.5f + 1.2f);
        RaycastHit2D h = Physics2D.Raycast(new Vector2(x, entrada.y + 3f), Vector2.down, 20f, groundLayer);
        if (h.collider == null) return false;
        punto = new Vector2(x, h.point.y);
        return true;
    }
}
