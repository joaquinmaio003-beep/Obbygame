using System.Collections;
using UnityEngine;

// Parte de PlayerAnimator: estela del dash, flash de dano y estirar/aplastar. Las variables y el Update estan en PlayerAnimator.cs
public partial class PlayerAnimator
{
    // Las copias de la estela: siluetas de color (el mismo shader del flash), sueltas en el mundo.
    void CrearEstela(Shader silueta)
    {
        raizEstela = new GameObject("Estela de Obby").transform;
        matEstela = new Material(silueta);   // blanco: el color lo pone cada copia
        copias = new SpriteRenderer[CopiasEstela];
        edadCopia = new float[CopiasEstela];
        for (int i = 0; i < CopiasEstela; i++)
        {
            var go = new GameObject("Copia");
            go.transform.SetParent(raizEstela, false);
            var c = go.AddComponent<SpriteRenderer>();
            c.sharedMaterial = matEstela;
            c.enabled = false;
            copias[i] = c;
        }
    }

    // Mientras dashea suelta copias; las que ya estan se van desvaneciendo.
    void ActualizarEstela()
    {
        if (copias == null) return;

        if (player != null && player.IsDashing && !isDead)
        {
            timerEstela -= Time.deltaTime;
            if (timerEstela <= 0f) { timerEstela = trailInterval; SoltarCopia(); }
        }
        else timerEstela = 0f;   // asi la primera copia sale apenas arranca el dash

        for (int i = 0; i < copias.Length; i++)
        {
            var c = copias[i];
            if (!c.enabled) continue;
            edadCopia[i] += Time.deltaTime;
            float k = edadCopia[i] / Mathf.Max(0.01f, trailLife);
            if (k >= 1f) { c.enabled = false; continue; }
            Color col = trailColor; col.a *= 1f - k;
            c.color = col;
        }
    }

    void SoltarCopia()
    {
        var c = copias[proximaCopia];
        edadCopia[proximaCopia] = 0f;
        proximaCopia = (proximaCopia + 1) % copias.Length;

        c.sprite = sr.sprite;
        c.flipX = sr.flipX;
        c.flipY = sr.flipY;
        c.sortingLayerID = sr.sortingLayerID;
        c.sortingOrder = sr.sortingOrder - 1;   // detras de Obby
        c.color = trailColor;
        c.transform.SetPositionAndRotation(visual.position, visual.rotation);
        c.transform.localScale = visual.lossyScale;   // con el signo: mira para el mismo lado
        c.enabled = true;
    }

    // pinta a Obby de blanco mientras dashea y lo restaura al terminar
    void UpdateDashFlash()
    {
        if (flashMat == null || player == null || damageFlashing) return;
        bool wantFlash = player.IsDashing;
        if (wantFlash == flashing) return;
        flashing = wantFlash;
        sr.sharedMaterial = wantFlash ? flashMat : normalMat;
    }

    /// <summary>Parpadeo rojo al recibir dano (lo llama PlayerRespawn).</summary>
    public void DamageFlash()
    {
        if (damageMat == null || !gameObject.activeInHierarchy) return;
        StopCoroutine(nameof(DamageFlashRoutine));
        StartCoroutine(nameof(DamageFlashRoutine));
    }

    IEnumerator DamageFlashRoutine()
    {
        damageFlashing = true;
        for (int i = 0; i < 3; i++)
        {
            sr.sharedMaterial = damageMat;
            yield return new WaitForSeconds(0.09f);
            sr.sharedMaterial = normalMat;
            yield return new WaitForSeconds(0.09f);
        }
        damageFlashing = false;
        flashing = false; // que el dash reaplique su flash si corresponde
    }

    // Crea el hijo que dibuja a Obby, copiando todo del SpriteRenderer original (que se apaga).
    Transform CrearVisual(SpriteRenderer raiz)
    {
        var go = new GameObject("Visual");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);

        var v = go.AddComponent<SpriteRenderer>();
        v.sprite = raiz.sprite;
        v.color = raiz.color;
        v.sharedMaterial = raiz.sharedMaterial;
        v.flipX = raiz.flipX;
        v.flipY = raiz.flipY;
        v.sortingLayerID = raiz.sortingLayerID;
        v.sortingOrder = raiz.sortingOrder;
        v.maskInteraction = raiz.maskInteraction;
        v.spriteSortPoint = raiz.spriteSortPoint;

        raiz.enabled = false;   // el original ya no se dibuja (queda para quien lea su orden de dibujo)
        return go.transform;
    }

    // Se estira al despegar y se aplasta al aterrizar, y vuelve solo a su forma.
    void ActualizarEstiramiento()
    {
        if (visual == null) return;

        if (!squashStretch || player == null || isDead)
        {
            escalaVisual = Vector2.one;
        }
        else
        {
            bool enPiso = player.IsGrounded;
            float vy = player.Velocity.y;
            if (!enPiso && vy < 0f) velCaida = -vy;   // a que velocidad viene cayendo

            if (enPiso && !pisoAntes && velCaida >= landSquashMinSpeed) escalaVisual = landSquash;   // aterrizo
            else if (!enPiso && pisoAntes && vy > 1f) escalaVisual = jumpStretch;                    // salto

            if (enPiso) velCaida = 0f;
            pisoAntes = enPiso;
            escalaVisual = Vector2.Lerp(escalaVisual, Vector2.one, squashRecover * Time.deltaTime);
        }

        visual.localScale = new Vector3(escalaVisual.x, escalaVisual.y, 1f);
        // los pies quedan en el mismo lugar (si no, al aplastarse quedaria flotando)
        float pie = sr.sprite != null ? sr.sprite.bounds.min.y : 0f;
        visual.localPosition = new Vector3(0f, pie * (1f - escalaVisual.y), 0f);
    }
}
