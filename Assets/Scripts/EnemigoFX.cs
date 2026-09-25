using System.Collections;
using UnityEngine;

/// <summary>
/// Efectos compartidos por los tres enemigos (tirador, guerrero y sierra), asi se sienten igual:
/// aviso antes del golpe, muerte aplastado con polvo, y caida cuando se rompe la plataforma.
/// No va en ningun objeto: los enemigos lo usan desde su codigo.
/// </summary>
public static class EnemigoFX
{
    /// <summary>Titila de color durante el amague del golpe, para que el jugador pueda esquivarlo.</summary>
    public static IEnumerator Amague(SpriteRenderer sr, Color colorBase, Color aviso, float duracion)
    {
        for (float t = 0f; t < duracion; t += Time.deltaTime)
        {
            if (sr != null) sr.color = Color.Lerp(colorBase, aviso, Mathf.PingPong(t * 14f, 1f));
            yield return null;
        }
        if (sr != null) sr.color = colorBase;
    }

    /// <summary>
    /// Se aplasta contra el piso (se achata y se ensancha) sin despegar los pies.
    /// pieAlPivote = distancia del pivote a la base del collider.
    /// </summary>
    public static IEnumerator Aplastar(Transform t, float pieAlPivote, float duracion)
    {
        Vector3 s0 = t.localScale;
        Vector3 p0 = t.position;
        for (float k = 0f; k < 1f; k += Time.deltaTime / duracion)
        {
            float sy = Mathf.Lerp(1f, 0.15f, k);
            t.localScale = new Vector3(s0.x * Mathf.Lerp(1f, 1.4f, k), s0.y * sy, s0.z);
            t.position = p0 + Vector3.down * (pieAlPivote * (1f - sy));   // los pies quedan en el piso
            yield return null;
        }
    }

    /// <summary>Polvo a los pies, abriendose a los dos lados.</summary>
    public static void Polvo(Bounds b, SpriteRenderer sr, int cantidad)
    {
        int capa = sr != null ? sr.sortingLayerID : 0;
        int orden = sr != null ? sr.sortingOrder + 1 : 0;
        float alto = Mathf.Max(0.3f, b.size.y);
        for (int i = 0; i < cantidad; i++)
        {
            float lado = (i % 2 == 0) ? -1f : 1f;
            Vector2 pos = new Vector2(b.center.x + lado * Random.Range(0f, b.extents.x), b.min.y + alto * 0.15f);
            Vector2 deriva = new Vector2(lado * Random.Range(0.6f, 1f), Random.Range(0.3f, 0.8f)).normalized
                           * Random.Range(0.8f, 1.5f);
            NubePolvo.Lanzar(null, pos, deriva, 0.2f * alto, 0.55f * alto, 0.5f, 0.9f, capa, orden);
        }
    }

    /// <summary>Cae atravesando todo (se rompio la plataforma) durante 'tiempo' segundos.</summary>
    public static IEnumerator CaerAtravesando(Transform t, float gravedad, float velMax, float tiempo)
    {
        float vel = 0f;
        for (float k = 0f; k < tiempo; k += Time.deltaTime)
        {
            vel = Mathf.Min(vel + gravedad * Time.deltaTime, velMax);
            t.position += Vector3.down * (vel * Time.deltaTime);
            yield return null;
        }
    }
}
