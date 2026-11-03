using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Parte de Emboscada: como entran los guerreros y donde caen. Las variables y el Update estan en Emboscada.cs
public partial class Emboscada
{
    // Lo saca de la pantalla y lo hace entrar hasta 'pie' (el piso donde va a quedar parado).
    // Los guerreros: unos caen desde arriba de la pantalla y otros saltan desde abajo; el techo y
    // el piso que haya en el medio los atraviesan dibujados por delante, asi se los ve venir (antes
    // salian del techo: aparecian de la nada). Los demas enemigos caen desde arriba o, si hay techo,
    // aparecen justo debajo.
    void Entrar(GameObject go, WarriorEnemy guerrero, Vector2 pie, float techoY)
    {
        Camera c = Camera.main;
        bool hayCamara = c != null && c.orthographic;
        float arriba = (hayCamara ? c.transform.position.y + c.orthographicSize : pie.y + 8f) + FueraDePantalla;
        float abajo = (hayCamara ? c.transform.position.y - c.orthographicSize : pie.y - 8f) - FueraDePantalla;
        var dibujo = go.GetComponent<SpriteRenderer>();
        Bounds d = dibujo != null ? dibujo.bounds : new Bounds(go.transform.position, new Vector3(2f, 2f, 0f));

        if (guerrero == null)
        {
            var cuerpo = go.GetComponent<Collider2D>();
            if (float.IsPositiveInfinity(techoY)) go.transform.position += Vector3.up * (arriba - d.min.y);
            else if (cuerpo != null) go.transform.position += Vector3.up * (techoY - 0.05f - cuerpo.bounds.max.y);
            return;
        }

        // reparte: con fromBelow en 0.5 va uno de arriba, uno de abajo, uno de arriba...
        cuentaAbajo += fromBelow;
        bool desdeAbajo = cuentaAbajo >= 0.999f;
        if (desdeAbajo) cuentaAbajo -= 1f;

        if (desdeAbajo)
        {
            go.transform.position += Vector3.up * (abajo - d.max.y);   // todo el dibujo por debajo de la pantalla
            Physics2D.SyncTransforms();                                // que el cuerpo ya este ahi: el salto se mide desde sus pies
            guerrero.EntrarSaltando(pie.y + SaltoDeEntrada);
        }
        else
        {
            go.transform.position += Vector3.up * (arriba - d.min.y);  // todo el dibujo por encima de la pantalla
            guerrero.EntrarCayendo(techoY - 0.02f);
        }
    }

    // Cuando el que llego toca el piso (venia en el aire y se apoyo): golpe seco, polvo y, si es
    // un guerrero, el saltito hacia Obby.
    IEnumerator AvisarCaida(GameObject go, WarriorEnemy guerrero)
    {
        float y = go.transform.position.y;
        bool cayo = false;
        for (float t = 0f; t < 4f; t += Time.fixedDeltaTime)
        {
            yield return s_pasoFisica;
            if (go == null || !go.activeInHierarchy) yield break;
            float ahora = go.transform.position.y;
            // el guerrero avisa el mismo si esta en el aire; a los demas se les mira si siguen bajando
            bool cayendo = guerrero != null ? guerrero.EnElAire : ahora < y - 0.001f;
            y = ahora;
            if (cayendo) cayo = true;
            else if (cayo || t > 0.2f) break;   // toco el piso (o aparecio ya parado: no cayo nunca)
        }
        if (cayo)
        {
            Sonidos.PlayEn(landSound, "enemigo_cae", go.transform.position);
            var cuerpo = go.GetComponent<Collider2D>();
            if (cuerpo != null) EnemigoFX.Polvo(cuerpo.bounds, go.GetComponent<SpriteRenderer>(), 4);
        }
        if (guerrero != null && hopHeight > 0f && obby != null)
        {
            // los que llegan cerca de Obby no saltan; a los demas el saltito no los mete en esa zona
            float lejos = Mathf.Abs(obby.transform.position.x - go.transform.position.x);
            if (lejos > hopMinDistance) guerrero.Saltito(hopHeight, Mathf.Min(lejos - hopMinDistance, hopDistance));
        }
    }

    // Con Spawn Points: donde va a quedar parado el proximo, cerca de uno que se VEA en pantalla y
    // repartidos a sus costados (todos en el mismo punto quedaban encimados). Si ninguno se ve
    // devuelve false y llega atras de Obby: un punto fijo queda fuera de la pantalla apenas Obby
    // avanza, y ahi no se los veria llegar.
    bool LugarEnPunto(out Vector2 pie, out float techoY)
    {
        pie = default;
        techoY = float.PositiveInfinity;
        if (spawnPoints == null || spawnPoints.Length == 0) return false;

        Camera c = Camera.main;
        bool hayCamara = c != null && c.orthographic;
        float obbyX = obby.transform.position.x;
        int desde = Random.Range(0, spawnPoints.Length);
        for (int k = 0; k < spawnPoints.Length; k++)
        {
            Transform p = spawnPoints[(desde + k) % spawnPoints.Length];
            if (p == null) continue;
            Vector2 q = p.position;
            if (hayCamara && Mathf.Abs(q.x - c.transform.position.x) > c.orthographicSize * c.aspect - 1f) continue;   // no se ve
            // si quedo metido en el piso, sube hasta el aire
            for (int i = 0; i < 24 && Physics2D.OverlapPoint(q, groundLayer) != null; i++) q.y += 0.25f;
            float cielo = hayCamara ? c.transform.position.y + c.orthographicSize + 1.5f : q.y + 8f;

            bool hayReserva = false;
            Vector2 reserva = default;
            float techoReserva = float.PositiveInfinity;
            for (int intento = 0; intento < 10; intento++)
            {
                float x = q.x + (intento == 0 ? 0f : Random.Range(-spawnSpread, spawnSpread));
                float lejos = Mathf.Abs(x - obbyX);
                if (lejos < MinAtras || lejos > behindDistance + 1.5f) continue;         // ni encima de Obby ni demasiado lejos
                if (!Probar(new Vector2(x, q.y), cielo, out Vector2 lugar, out float techo)) continue;
                if (!Apretado(x)) { pie = lugar; techoY = techo; return true; }
                if (!hayReserva) { hayReserva = true; reserva = lugar; techoReserva = techo; }
            }
            if (hayReserva) { pie = reserva; techoY = techoReserva; return true; }
        }
        return false;
    }

    // Sirve ese lugar? Tiene que haber aire ahi y piso debajo. Devuelve el piso y el techo mas
    // bajo que tenga encima (infinito = cielo abierto).
    bool Probar(Vector2 medio, float cielo, out Vector2 lugar, out float techo)
    {
        lugar = default;
        techo = float.PositiveInfinity;
        if (Physics2D.OverlapPoint(medio, groundLayer) != null) return false;            // ahi hay una pared
        RaycastHit2D piso = Physics2D.Raycast(medio, Vector2.down, 40f, groundLayer);
        if (piso.collider == null) return false;                                         // abajo hay un pozo
        TechoSobre(medio, cielo - medio.y, out techo);
        lugar = new Vector2(medio.x, piso.point.y);
        return true;
    }

    // Sin Spawn Points a la vista: donde va a quedar parado el proximo, atras de Obby, a la vista
    // y en el MISMO lugar donde esta el (no arriba del techo de un tunel, que fue lo que paso al
    // principio).
    bool Lugar(out Vector2 pie, out float techoY)
    {
        Camera c = Camera.main;
        bool hayCamara = c != null && c.orthographic;
        Vector2 o = obbyCol != null ? (Vector2)obbyCol.bounds.center : (Vector2)obby.transform.position;
        float cielo = hayCamara ? c.transform.position.y + c.orthographicSize + 1.5f : o.y + 8f;

        // A LA VISTA: nunca mas atras que el borde de la pantalla. Si no, llegaban donde no se los
        // ve y, como son mas lentos que Obby, no llegaba a verse ninguno.
        float maxAtras = behindDistance + 1.5f;
        if (hayCamara)
        {
            float borde = c.transform.position.x - dir * (c.orthographicSize * c.aspect - 2f);   // 2 unidades adentro
            maxAtras = Mathf.Min(maxAtras, (o.x - borde) * dir);
        }
        maxAtras = Mathf.Max(maxAtras, MinAtras);

        // A la altura de Obby: aire ahi y piso debajo. El primero que no tenga a otro encima queda;
        // si todos estan apretados, el primero que hubo.
        bool hayReserva = false;
        Vector2 reserva = default;
        float techoReserva = float.PositiveInfinity;
        for (int intento = 0; intento < 10; intento++)
        {
            float atras = Mathf.Clamp(behindDistance + Random.Range(-1.5f, 1.5f) - intento * 0.7f, MinAtras, maxAtras);
            float x = o.x - dir * atras;
            if (!Probar(new Vector2(x, o.y), cielo, out Vector2 lugar, out float techo)) continue;
            if (!Apretado(x)) { pie = lugar; techoY = techo; return true; }
            if (!hayReserva) { hayReserva = true; reserva = lugar; techoReserva = techo; }
        }
        if (hayReserva) { pie = reserva; techoY = techoReserva; return true; }

        // atras el terreno esta mas alto que Obby (viene bajando una cuesta): sobre lo primero que
        // haya mirando desde arriba de la pantalla
        techoY = float.PositiveInfinity;
        for (int intento = 0; intento < 6; intento++)
        {
            float atras = Mathf.Clamp(behindDistance + Random.Range(-1.5f, 1.5f) - intento * 0.8f, MinAtras, maxAtras);
            var punto = new Vector2(o.x - dir * atras, cielo);
            if (Physics2D.OverlapPoint(punto, groundLayer) != null) continue;            // adentro de una pared
            RaycastHit2D piso = Physics2D.Raycast(punto, Vector2.down, 40f, groundLayer);
            if (piso.collider == null) continue;                                         // abajo hay un pozo
            pie = new Vector2(punto.x, piso.point.y);
            return true;
        }
        pie = default;
        return false;
    }

    // El techo mas bajo sobre ese punto (infinito si no hay). Mira tambien a los costados, por el
    // ancho del cuerpo.
    bool TechoSobre(Vector2 desde, float hasta, out float techoY)
    {
        techoY = float.PositiveInfinity;
        for (int i = -1; i <= 1; i++)
        {
            RaycastHit2D h = Physics2D.Raycast(desde + Vector2.right * (i * 0.6f), Vector2.up, Mathf.Max(hasta, 1f), groundLayer);
            if (h.collider != null && h.point.y < techoY) techoY = h.point.y;
        }
        return !float.IsPositiveInfinity(techoY);
    }

    // Ahi ya hay otro de la emboscada (parado, o llegando)? Para que no queden encimados.
    bool Apretado(float x)
    {
        float cerca = Mathf.Max(separation, 0.9f);
        foreach (var e in enemigos)
            if (e != null && e.activeInHierarchy && Mathf.Abs(e.transform.position.x - x) < cerca) return true;
        return false;
    }
}
