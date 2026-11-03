using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// Parte de JefeCaballero: cuando recibe roca, cuando pierde, la huida y el truco de la J. Las variables y el Update estan en JefeCaballero.cs
public partial class JefeCaballero
{
    // Truco de prueba (J): le saca todas las vidas de una y arranca la huida, como el ultimo golpe.
    void GanarYa()
    {
        cansado = false;
        pisoteando = false;
        vidas = 0;
        Sonidos.PlayEn(rockHitSound, "roca_impacto", transform.position, Sonidos.EnPantalla);
        if (barra != null) barra.Poner(0f);
        StopAllCoroutines();
        sr.color = colorBase;
        StartCoroutine(Morir());
    }

    /// <summary>
    /// Le pego una roca grande que tiro Obby. Cansado: pierde una vida. Si no: no le hace nada.
    /// </summary>
    public void RecibirRoca(Vector2 desde)
    {
        if (muerto || !despierto) return;
        if (!cansado)
        {
            Sonidos.PlayEn(bounceSound, "roca_grande_rebota", transform.position, Sonidos.EnPantalla);   // le rebota
            return;
        }
        cansado = false;
        vidas--;
        Sonidos.PlayEn(rockHitSound, "roca_impacto", transform.position, Sonidos.EnPantalla);   // se la estrella encima
        HitStop.Congelar(hitStopOnBoss);
        if (barra != null) barra.Poner((float)vidas / Mathf.Max(1, lives));
        StopAllCoroutines();
        sr.color = colorBase;
        StartCoroutine(vidas <= 0 ? Morir() : Herido());
    }

    IEnumerator Herido()
    {
        Sonidos.PlayEn(hurtSound, "jefe_herido", transform.position, Sonidos.EnPantalla);
        if (cam != null) cam.Shake(0.35f, 0.22f);
        for (int i = 0; i < 4; i++) { hitFX.Flash(); yield return new WaitForSeconds(0.12f); }
        yield return EsperarApoyado();
        // se rie de nuevo, enojado: desde ahora es mas rapido
        if (UltimaVida) { colorBase = lastLifeTint; sr.color = colorBase; }   // ultima vida: se pone rojo de bronca
        Encarar();
        Poner(laugh, true);
        Sonidos.PlayEn(laughSound, "jefe_risa", transform.position, Sonidos.EnPantalla);
        yield return new WaitForSeconds(1f);
        StartCoroutine(Pelear());
    }

    BarraJefe barra;
    bool UltimaVida => vidas == 1 && lives > 1;

    float entradaX = float.NaN;   // por donde entro Obby a la arena (la huida va para el otro lado)

    // Le ganaste: titila, sale corriendo para un costado y ROMPE la pared de ese lado escapandose.
    // Por el agujero que deja pasa Obby a la salida del nivel.
    IEnumerator Morir()
    {
        muerto = true;
        // el cuerpo (antes de apagar el collider: apagado ya no tiene tamano)
        Vector3 centroLocal = col.bounds.center - transform.position;
        Vector3 medio = col.bounds.extents;
        foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
        RocaGrande.RomperTodas();

        Poner(laugh, false);
        Sonidos.PlayEn(hurtSound, "jefe_herido", transform.position, Sonidos.EnPantalla);
        if (cam != null) { cam.Enfocar(transform); cam.Shake(0.8f, 0.3f); }
        for (int i = 0; i < 8; i++)
        {
            hitFX.Flash();
            yield return new WaitForSeconds(0.1f);
        }


        // para que lado se va
        int lado = escapeSide != 0 ? (int)Mathf.Sign(escapeSide)
                 : door != null ? (transform.position.x >= door.position.x ? 1 : -1)
                 : !float.IsNaN(entradaX) ? (transform.position.x >= entradaX ? 1 : -1)
                 : dir;
        Mirar(lado);
        var corrida = new Anim { frames = walk.frames, fps = walk.fps * 2.2f };
        Poner(corrida, true);
        if (cam != null) cam.Enfocar(transform, true);

        // los pisos que puede romper (los tilemaps de la capa del piso)
        var mapas = new System.Collections.Generic.List<Tilemap>();
        foreach (var tm in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            if ((groundLayer.value & (1 << tm.gameObject.layer)) != 0) mapas.Add(tm);

        float pies = PiesY;
        float velCaida = 0f;
        float ultimoGolpe = -1f, proximoPaso = 0f;
        bool rompio = false, soltoCamara = false;
        float xRotura = transform.position.x;
        for (float t = 0f; t < 7f; t += Time.deltaTime)
        {
            transform.position += Vector3.right * (lado * escapeSpeed * Time.deltaTime);

            // piso: si se termina, cae
            Vector2 piesPunto = new Vector2(transform.position.x + centroLocal.x, pies + 0.3f);
            RaycastHit2D h = Physics2D.Raycast(piesPunto, Vector2.down, 0.3f + Mathf.Max(0.05f, -velCaida * Time.deltaTime), groundLayer);
            if (h.collider != null && velCaida <= 0f) { velCaida = 0f; pies = h.point.y; }
            else { velCaida -= gravity * Time.deltaTime; pies += velCaida * Time.deltaTime; }
            transform.position = new Vector3(transform.position.x, pies - (centroLocal.y - medio.y), transform.position.z);

            // pasos pesados
            if (t >= proximoPaso && velCaida == 0f)
            {
                proximoPaso = t + 0.2f;
                Sonidos.PlayEn(stepSound, "jefe_paso", transform.position, Sonidos.EnPantalla);
            }

            // rompe todo lo que tenga adelante, un poco mas alto que el
            Vector2 c = (Vector2)(transform.position + centroLocal) + Vector2.right * (lado * 0.4f);
            Vector2 min = new Vector2(c.x - medio.x - 0.4f, pies + 0.15f);
            Vector2 max = new Vector2(c.x + medio.x + 0.4f, pies + medio.y * 2f + holeExtraHeight);
            int rotas = 0;
            foreach (var tm in mapas) rotas += RomperParedes(tm, min, max);
            rotas += AbrirParedes(min, max, pies);
            if (rotas > 0)
            {
                if (!rompio) Sonidos.PlayEn(deathSound, "jefe_muere", transform.position, Sonidos.EnPantalla);   // primer golpe contra la pared
                rompio = true;
                xRotura = transform.position.x;
                if (Time.time - ultimoGolpe > 0.1f)
                {
                    ultimoGolpe = Time.time;
                    Sonidos.PlayEn(null, "roca_rompe", transform.position, Sonidos.EnPantalla);
                    if (cam != null) cam.Shake(0.25f, 0.28f);
                }
            }

            // ya paso la pared: la camara vuelve con Obby y el se pierde a lo lejos
            float pasado = (transform.position.x - xRotura) * lado;
            if (rompio && !soltoCamara && pasado > 3f) { soltoCamara = true; if (cam != null) cam.SoltarFoco(); }
            if ((rompio && pasado > 25f) || pies < -40f) break;
            yield return null;
        }

        if (cam != null) cam.SoltarFoco();
        sr.enabled = false;
        if (barra != null) barra.Romper();   // ya se escapo: recien ahora la barra de vida se hace pedazos
        if (enableOnDefeat != null) enableOnDefeat.SetActive(true);
        onDefeated?.Invoke();
    }

    // Rompe los cuadros del tilemap que caen dentro de la caja (en pixeles). Devuelve cuantos rompio.
    int RomperParedes(Tilemap tm, Vector2 min, Vector2 max)
    {
        Vector3Int a = tm.WorldToCell(min), b = tm.WorldToCell(max);
        var tr = tm.GetComponent<TilemapRenderer>();
        int rotas = 0;
        for (int y = a.y; y <= b.y; y++)
            for (int x = a.x; x <= b.x; x++)
            {
                var celda = new Vector3Int(x, y, 0);
                if (!tm.HasTile(celda)) continue;
                Vector3 centro = tm.GetCellCenterWorld(celda);
                if (centro.y < min.y) continue;   // el piso donde pisa no se rompe
                Sprite s = tm.GetSprite(celda);
                Color color = tm.GetColor(celda);
                tm.SetTile(celda, null);
                rotas++;
                if (s == null) continue;

                // el cuadro se deshace en pixeles (uno si, uno no: son muchos de golpe)
                var go = new GameObject("Pared rota");
                go.transform.position = centro;
                go.transform.localScale = tm.transform.lossyScale;
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = s;
                r.color = color * tm.color;
                if (tr != null) { r.sortingLayerID = tr.sortingLayerID; r.sortingOrder = tr.sortingOrder + 1; r.sharedMaterial = tr.sharedMaterial; }
                bool pixeles = ((x + y) & 1) == 0 && EnemigoFX.Pixelar(r, null, 1.3f);
                if (!pixeles) EnemigoFX.Polvo(r.bounds, r, 2);
                Destroy(go);
            }
        return rotas;
    }

    // Los colliders de pared que tenga adelante: a las cajas (como la de la pared del tilemap) les
    // abre el agujero (queda lo de abajo y lo de arriba); otras paredes invisibles se apagan.
    static readonly Collider2D[] s_paredes = new Collider2D[16];
    int AbrirParedes(Vector2 min, Vector2 max, float pies)
    {
        var filtro = new ContactFilter2D { useTriggers = false };
        int n = Physics2D.OverlapArea(min, max, filtro, s_paredes);
        int abiertas = 0;
        for (int i = 0; i < n; i++)
        {
            Collider2D c = s_paredes[i];
            if (c == null || c is TilemapCollider2D || c is CompositeCollider2D) continue;
            if (c.attachedRigidbody != null && c.attachedRigidbody.bodyType != RigidbodyType2D.Static) continue;   // cosas que se mueven (rocas, Obby)
            Bounds b = c.bounds;
            if (b.max.y <= min.y + 0.05f) continue;   // es el piso
            if (b.min.y >= max.y - 0.05f) continue;   // ya es lo que quedo arriba del agujero
            if (c is BoxCollider2D caja)
            {
                caja.enabled = false;
                if (pies - b.min.y > 0.05f) PedazoDePared(caja, b, b.min.y, pies);   // lo de abajo (el piso)
                if (b.max.y - max.y > 0.05f) PedazoDePared(caja, b, max.y, b.max.y);  // lo de arriba del agujero
            }
            else if (c.GetComponent<Renderer>() == null) c.enabled = false;
            else continue;
            abiertas++;
        }
        return abiertas;
    }

    static void PedazoDePared(BoxCollider2D molde, Bounds b, float y0, float y1)
    {
        var n = molde.gameObject.AddComponent<BoxCollider2D>();
        n.sharedMaterial = molde.sharedMaterial;
        Transform tr = molde.transform;
        Vector3 escala = tr.lossyScale;
        n.offset = tr.InverseTransformPoint(new Vector3(b.center.x, (y0 + y1) * 0.5f, tr.position.z));
        n.size = new Vector2(b.size.x / Mathf.Abs(escala.x), (y1 - y0) / Mathf.Abs(escala.y));
    }
}
