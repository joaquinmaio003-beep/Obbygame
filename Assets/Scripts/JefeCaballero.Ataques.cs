using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// Parte de JefeCaballero: como pelea: eleccion de ataque, espadazos, pina, pisoton, rocas y cansancio. Las variables y el Update estan en JefeCaballero.cs
public partial class JefeCaballero
{
    IEnumerator Pelear()
    {
        if (!despierto)
        {
            yield return CinematicaSalteable();
            despierto = true;
            if (showHealthBar && barra == null) barra = BarraJefe.Crear(bossName, uiFont, Mathf.Max(1, lives));
        }

        while (!muerto)
        {
            int ataques = Mathf.Max(2, attacksBeforeTired + Random.Range(-1, 2));
            for (int i = 0; i < ataques && !muerto; i++)
            {
                yield return EsperarApoyado();
                Encarar();
                float dx = Mathf.Abs(player.position.x - transform.position.x);
                bool aMiAltura = Mathf.Abs(ObbyPies() - PiesY) < 1.3f;

                float cerca = col.bounds.extents.x + punchRange;
                float r = Random.value;
                bool lejos = dx > cerca + 4f;   // lejos: ahi si se larga con el dash
                if (aMiAltura && dx <= cerca) yield return GolpeDeCerca();
                else if (aMiAltura && lejos && r < 0.7f) yield return UltimaVida ? DashIdaYVuelta() : EspadazoDash();
                else if (dx > cerca + 1f && dx <= stompDistance && r < 0.5f) yield return UltimaVida ? PisotonYRoca() : Pisoton();
                else yield return Seguir(2.2f);   // va hasta vos y te pega (camina un rato corto y vuelve a elegir)

                Poner(idle, true);
                yield return new WaitForSeconds(restTime / Furia);
            }
            // antes de cansarse tira sus rocas: una queda clavada para que Obby la saque y se la tire
            if (!muerto) yield return LluviaDeRocas();
            if (!muerto) yield return Cansarse();
        }
    }

    IEnumerator EsperarApoyado()
    {
        for (float t = 0f; enElAire && t < 3f; t += Time.deltaTime) yield return null;
    }

    // Camina hacia Obby; si esta mas arriba (o hay una pared en el medio) salta adonde esta.
    IEnumerator Seguir(float maximo)
    {
        Poner(walk, true);
        float proximoPaso = 0f, proximoDash = 0.4f;
        for (float t = 0f; t < maximo && !muerto; t += Time.deltaTime)
        {
            if (enElAire) { yield return null; continue; }   // se tiro de una plataforma: cae y sigue
            // pasos pesados mientras camina
            if (t >= proximoPaso)
            {
                proximoPaso = t + 0.38f / Furia;
                Sonidos.PlayEn(stepSound, "jefe_paso", transform.position, Sonidos.EnPantalla);
            }
            Encarar();
            float dx = player.position.x - transform.position.x;
            float dy = ObbyPies() - PiesY;
            bool obbyQuieto = obby == null || obby.IsGrounded;

            // Obby arriba de una plataforma: salta adonde esta
            if (dy > 1.3f && obbyQuieto && Mathf.Abs(dx) < 12f) { yield return SaltarHacia(ObbyPiesPunto()); yield break; }
            if (Mathf.Abs(dx) <= (col.bounds.extents.x + punchRange) * 0.9f && Mathf.Abs(dy) < 1.3f) break;
            // caminando, si quedo a media distancia, muchas veces se larga con el espadazo dash
            if (t >= proximoDash)
            {
                proximoDash = t + 0.6f;
                float lejos = Mathf.Abs(dx), cerca = col.bounds.extents.x + punchRange;
                if (Mathf.Abs(dy) < 1.3f && lejos > cerca + 4f && Random.value < 0.5f)   // solo si estas lejos
                { yield return EspadazoDash(); yield break; }
            }
            if (anim != walk) Poner(walk, true);
            if (!Mover(dir * walkSpeed * Furia * Time.deltaTime))
            {
                // una pared o un escalon en el medio: lo salta
                yield return SaltarHacia(ObbyPiesPunto());
                yield break;
            }
            yield return null;
        }
        if (!muerto && !enElAire && Mathf.Abs(player.position.x - transform.position.x) <= col.bounds.extents.x + punchRange
            && Mathf.Abs(ObbyPies() - PiesY) < 1.3f)
            yield return GolpeDeCerca();
    }

    // Titila avisando que va a atacar (con el crujido de la armadura).
    IEnumerator Avisar(float duracion)
    {
        Sonidos.PlayEn(warnSound, "jefe_aviso", transform.position, Sonidos.EnPantalla);
        yield return EnemigoFX.Amague(sr, colorBase, warnColor, duracion);
    }

    /// <summary>Palabra de historieta arriba de Obby (PUM, SLASH, POW). La usa tambien la roca.</summary>
    public void Texto(string palabra)
    {
        if (showHitWords && player != null) TextoGolpe.Mostrar(palabra, player.position + Vector3.up * 1.4f, hitWordSize);
    }

    // ULTIMA VIDA: dash, y apenas termina se da vuelta y vuelve con otro dash.
    IEnumerator DashIdaYVuelta()
    {
        yield return EspadazoDash();
        if (muerto) yield break;
        yield return new WaitForSeconds(0.12f);
        yield return EspadazoDash();   // Encarar() adentro: vuelve para donde esta Obby
    }

    // ULTIMA VIDA: pisoton y, apenas cae, una roca adonde este Obby.
    IEnumerator PisotonYRoca()
    {
        yield return Pisoton();
        if (muerto) yield break;
        yield return TirarRoca(ApuntarAObby(), false);
    }

    // De cerca: mitad espadazo, mitad pina.
    IEnumerator GolpeDeCerca()
    {
        if (Random.value < 0.5f) yield return Espadazo();
        else yield return Pina();
    }

    // PISOTON: se agacha avisando, salta alto adonde esta Obby y cae con todo: la camara tiembla
    // y si Obby esta abajo o al lado, le saca una vida.
    IEnumerator Pisoton()
    {
        yield return EsperarApoyado();
        Encarar();
        // se agacha y CARGA el salto (titilando y crujiendo)
        Anim agachado = lift != null && lift.frames != null && lift.frames.Length > 0
            ? new Anim { frames = new[] { lift.frames[0] }, fps = 1f } : jump;
        Poner(agachado, true);
        yield return Avisar(stompCharge / Furia);

        // calcula donde va a estar Obby cuando caiga (si corre, adelante suyo) y salta RAPIDO
        float T = Mathf.Max(0.25f, stompTime / Mathf.Sqrt(Furia));
        Vector2 destino = ObbyPiesPunto();
        // se adelanta un poco a donde corre Obby (si se adelantaba todo, Obby frenaba y erraba lejisimo)
        if (obby != null) destino.x += Mathf.Clamp(obby.Velocity.x * T * stompLead, -stompMaxLead, stompMaxLead);
        destino = PisoDeLaArena(destino.x, destino.y);
        if (showLandingMarks) MarcaDeCaida.Crear(destino, col.bounds.size.x + stompReach * 2f, T);   // donde va a caer
        Mirar(destino.x >= transform.position.x ? 1 : -1);
        float dy = destino.y - PiesY;
        velX = (destino.x - transform.position.x) / T;
        velY = (dy + 0.5f * stompGravity * T * T) / T;   // llega justo en T segundos
        enElAire = true;
        pisoteando = true;
        aplastoEsteSalto = false;
        Poner(jump, true);
        Sonidos.PlayEn(jumpSound, "jefe_salta", transform.position, Sonidos.EnPantalla);
        for (float t = 0f; enElAire && t < 3f; t += Time.deltaTime) yield return null;
        pisoteando = false;
        Poner(idle, true);
        yield return new WaitForSeconds(0.25f);
    }

    // El piso de la arena en esa x (sin pasarse de las paredes): adonde cae el pisoton.
    Vector2 PisoDeLaArena(float x, float yObby)
    {
        Bounds b = col.bounds;
        Vector2 ojo = new Vector2(b.center.x, b.min.y + 1f);
        RaycastHit2D izq = Physics2D.Raycast(ojo, Vector2.left, 25f, groundLayer);
        RaycastHit2D der = Physics2D.Raycast(ojo, Vector2.right, 25f, groundLayer);
        float minX = (izq.collider != null ? izq.point.x : ojo.x - 25f) + b.extents.x + 0.2f;
        float maxX = (der.collider != null ? der.point.x : ojo.x + 25f) - b.extents.x - 0.2f;
        if (minX < maxX) x = Mathf.Clamp(x, minX, maxX);
        RaycastHit2D h = Physics2D.Raycast(new Vector2(x, Mathf.Max(yObby, PiesY) + 3f), Vector2.down, 20f, groundLayer);
        return new Vector2(x, h.collider != null ? h.point.y : PiesY);
    }

    // Cae el pisoton.
    void Pisada()
    {
        Sonidos.PlayEn(stompSound, "jefe_pisoton", transform.position, Sonidos.EnPantalla);
        if (cam != null) cam.Shake(0.45f, 0.4f);
        EnemigoFX.Polvo(sr.bounds, sr, 10);
        if (player == null || obbyVidas == null) return;
        // la onda del golpe: el cuerpo de Obby (no su centro) al lado o abajo del jefe
        Bounds b = col.bounds;
        Bounds o = obbyCol != null ? obbyCol.bounds : new Bounds(player.position, Vector3.one * 0.5f);
        bool deCostado = o.max.x >= b.min.x - stompReach && o.min.x <= b.max.x + stompReach;
        bool deAlto = o.min.y <= b.min.y + 1.5f && o.max.y >= b.min.y - 0.5f;
        if (((deCostado && deAlto) || b.Intersects(o)) && !aplastoEsteSalto)
        {
            aplastoEsteSalto = true;
            obbyVidas.Hurt(transform.position);
            HitStop.Congelar(hitStopOnObby);
        }
    }

    // Cayendo con el pisoton: si le cae encima a Obby, lo aplasta (antes solo contaba al tocar el piso).
    void AplastarSiToca()
    {
        if (obbyVidas == null || obbyCol == null || aplastoEsteSalto) return;
        if (col.bounds.Intersects(obbyCol.bounds)) { aplastoEsteSalto = true; obbyVidas.Hurt(transform.position); HitStop.Congelar(hitStopOnObby); }
    }
    bool aplastoEsteSalto;   // un solo aplastamiento (y un solo freno) por pisoton

    IEnumerator Pina()
    {
        Encarar();
        Poner(punch, false);
        yield return Avisar(0.3f / Furia);   // aviso
        animT = 10f;   // brazo estirado (ultimo cuadro)
        Sonidos.PlayEn(punchSound, "jefe_pina", transform.position, Sonidos.EnPantalla);
        if (cam != null) cam.Shake(0.12f, 0.08f);
        // el brazo queda estirado un ratito: pega en cualquier momento de ese rato (no en un solo cuadro)
        bool pego = false;
        for (float t = 0f; t < 0.15f; t += Time.deltaTime)
        {
            if (!pego)
            {
                pego = Golpear(punchRange + 0.3f);
                // la pina lo manda volando lejos
                if (pego)
                {
                    Sonidos.PlayEn(punchHitSound, "pina_impacto", player.position, Sonidos.EnPantalla);   // PUM y sale volando
                    Texto("PUM");
                    HitStop.Congelar(hitStopOnObby);
                    if (cam != null) cam.Shake(0.2f, 0.18f);
                    if (obby != null) obby.Empujar(new Vector2(dir * punchKnockback, punchKnockUp), 0.45f);
                }
            }
            yield return null;
        }
        yield return new WaitForSeconds(0.2f);
    }

    // Tira varias rocas, cada una adonde esta Obby. Una sola (al azar) queda clavada en el
    // piso; las demas se rompen al caer. Si Obby ya tiene una (en la mano o clavada), no clava otra.
    IEnumerator LluviaDeRocas()
    {
        yield return EsperarApoyado();
        int cuantas = Mathf.Max(1, rocksBeforeTired);
        bool yaTiene = RocaGrande.HayEnElPiso || (obbyTira != null && obbyTira.LlevaRocaGrande);
        int clavada = yaTiene ? -1 : Random.Range(0, cuantas);
        for (int i = 0; i < cuantas && !muerto; i++)
            yield return TirarRoca(ApuntarAObby(), i == clavada);
    }

    // Adonde tirarle la roca a Obby: donde va a estar cuando la roca llegue (si corre, adelante
    // suyo), sobre el piso que tenga abajo y sin pasarse de las paredes de la arena.
    Vector2 ApuntarAObby()
    {
        Vector2 p = ObbyPiesPunto();
        if (obby != null) p.x += obby.Velocity.x * rockFlightTime / Mathf.Sqrt(Furia);
        return PisoDeLaArena(p.x, p.y);
    }
    // La roca de la puerta: bastante mas alta que el hueco, para que lo tape bien.
    float TamanoTapon(Vector2 puerta)
    {
        RaycastHit2D abajo = Physics2D.Raycast(puerta, Vector2.down, 10f, groundLayer);
        RaycastHit2D arriba = Physics2D.Raycast(puerta, Vector2.up, 10f, groundLayer);
        if (abajo.collider == null || arriba.collider == null) return doorRockSize * 1.4f;
        return Mathf.Max(doorRockSize, (arriba.point.y - abajo.point.y) * 1.8f);
    }

    IEnumerator TirarRoca(Vector2 hasta, bool clavada)
    {
        Encarar();
        Poner(lift, false);   // agarra la roca y la levanta
        yield return new WaitForSeconds(DuracionDe(lift) / Furia);
        yield return Avisar(0.2f / Furia);
        Lanzar(hasta, TamanoRoca(), rockFlightTime / Mathf.Sqrt(Furia), false, clavada);
        Poner(idle, true);
        yield return new WaitForSeconds(0.25f);
    }

    static bool Vacia(PlayerAnimator.SpriteAnim a) { return a == null || a.frames == null || a.frames.Length == 0; }

    float TamanoRoca() { return Mathf.Max(0.5f, col.bounds.size.y * rockScale); }

    RocaGrande Lanzar(Vector2 hasta, float tamano, float tiempo, bool tapon, bool clavada = false)
    {
        Bounds b = col.bounds;
        Vector2 desde = new Vector2(b.center.x, b.max.y + tamano * 0.6f);
        Sonidos.PlayEn(throwSound, "jefe_tira", transform.position, Sonidos.EnPantalla);
        var roca = RocaGrande.TirarDelJefe(rockSprite, tamano, desde, hasta, tiempo, groundLayer, sr.sortingOrder + 1, tapon, clavada);
        if (showLandingMarks && !tapon) MarcaDeCaida.Crear(hasta, tamano * 1.1f, tiempo);   // donde va a caer la roca
        roca.ParaObby(carriedRockScale, obbyThrowSpeed);
        roca.Duenio = this;
        return roca;
    }

    IEnumerator Espadazo()
    {
        Encarar();
        Poner(sword, false);
        // saca la espada y la levanta (todo menos los dos ultimos cuadros), titilando de aviso
        int preparar = sword.frames != null ? Mathf.Max(0, sword.frames.Length - 2) : 0;
        yield return Avisar(preparar / Mathf.Max(1f, sword.fps) / Furia);
        // estocada: avanza de golpe pegando lo que tenga adelante
        animT = preparar / Mathf.Max(1f, sword.fps);
        Sonidos.PlayEn(swordSound, "jefe_espadazo", transform.position, Sonidos.EnPantalla);
        bool pego = false;
        float dur = 0.22f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            Mover(dir * swordLunge / dur * Time.deltaTime);
            if (!pego && (pego = Golpear(swordReach))) Texto("SLASH");
            yield return null;
        }
        if (cam != null) cam.Shake(0.15f, 0.12f);
        yield return new WaitForSeconds(0.4f);
    }

    // ESPADAZO DASH: prepara la espada titilando y sale disparado para adelante con la estocada,
    // dejando una estela, hasta recorrer Dash Distance (o chocar una pared). Pega a lo que cruce.
    IEnumerator EspadazoDash()
    {
        Encarar();
        Poner(sword, false);
        int preparar = sword.frames != null ? Mathf.Max(0, sword.frames.Length - 2) : 0;
        yield return Avisar(Mathf.Max(0.35f, preparar / Mathf.Max(1f, sword.fps)) / Furia);
        animT = preparar / Mathf.Max(1f, sword.fps);   // estocada
        Sonidos.PlayEn(swordSound, "jefe_espadazo", transform.position, Sonidos.EnPantalla);
        Sonidos.PlayEn(dashSound, "jefe_dash", transform.position, Sonidos.EnPantalla);   // el zumbido de la carrera
        bool pego = false;
        float recorrido = 0f, proximaEstela = 0f;
        while (recorrido < dashDistance && !muerto)
        {
            float paso = dashSpeed * Furia * Time.deltaTime;
            if (!Mover(dir * paso)) break;   // choco una pared
            recorrido += paso;
            if (!pego && (pego = Golpear(swordReach * 0.6f))) Texto("SLASH");
            if (recorrido >= proximaEstela) { proximaEstela = recorrido + 0.6f; Estela(); }
            yield return null;
        }
        animT = 10f;   // termina con la espada abajo (ultimo cuadro)
        if (cam != null) cam.Shake(0.2f, 0.15f);
        yield return new WaitForSeconds(0.5f);
    }

    // Copia del dibujo que se desvanece (la estela del dash).
    void Estela()
    {
        var go = new GameObject("Estela jefe");
        go.transform.SetPositionAndRotation(transform.position, transform.rotation);
        go.transform.localScale = transform.lossyScale;
        var s = go.AddComponent<SpriteRenderer>();
        s.sprite = sr.sprite;
        s.sortingLayerID = sr.sortingLayerID;
        s.sortingOrder = sr.sortingOrder - 1;
        s.color = new Color(1f, 0.45f, 0.45f, 0.55f);
        go.AddComponent<DesvanecerEstela>();
    }

    // SE CANSA: queda quieto parpadeando en blanco. Es el momento de pegarle con una roca grande.
    IEnumerator Cansarse()
    {
        yield return EsperarApoyado();
        cansado = true;
        var jadeo = new Anim { frames = idle.frames, fps = 2f };
        Poner(jadeo, true);
        float dura = Mathf.Max(1.8f, tiredTime - 0.6f * (Fase - 1));
        float proximoJadeo = 0f;
        for (float t = 0f; t < dura && !muerto; t += 0.22f)
        {
            hitFX.Flash();   // parpadeo blanco
            if (t >= proximoJadeo)   // jadea
            {
                proximoJadeo = t + 1.2f;
                Sonidos.PlayEn(tiredSound, "jefe_cansado", transform.position, Sonidos.EnPantalla);
            }
            yield return new WaitForSeconds(0.22f);
        }
        cansado = false;
    }

    // Le pega a Obby si esta adelante, al alcance y a la altura del cuerpo.
    // Llega hasta donde llega el DIBUJO del cuadro actual (la estocada de la espada llega mucho mas
    // lejos que el cuerpo): si se ve que te pega, te pega. Mide contra el cuerpo de Obby, no su centro.
    bool Golpear(float alcance)
    {
        if (player == null || obbyVidas == null) return false;
        Bounds b = col.bounds;
        float frente = Mathf.Max(alcance + b.extents.x, AlcanceDelDibujo() + 0.15f);
        Bounds o = obbyCol != null ? obbyCol.bounds : new Bounds(player.position, Vector3.one * 0.5f);
        float bordeCerca = dir > 0 ? o.min.x - b.center.x : b.center.x - o.max.x;   // lo mas cerca de Obby, hacia adelante
        float bordeLejos = dir > 0 ? o.max.x - b.center.x : b.center.x - o.min.x;
        if (bordeLejos < -0.4f || bordeCerca > frente) return false;
        float arriba = Mathf.Max(b.max.y, sr.bounds.max.y);
        if (o.min.y > arriba || o.max.y < b.min.y - 0.3f) return false;
        obbyVidas.Hurt(transform.position);
        return true;
    }

    // Hasta donde llega para adelante el dibujo del cuadro actual (desde el centro del cuerpo).
    float AlcanceDelDibujo()
    {
        if (sr == null || sr.sprite == null) return 0f;
        float max = 0f;
        foreach (var v in sr.sprite.vertices) max = Mathf.Max(max, spriteFacesRight ? v.x : -v.x);
        return max * Mathf.Abs(transform.lossyScale.x);
    }
}
