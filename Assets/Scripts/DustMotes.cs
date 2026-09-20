using UnityEngine;

/// <summary>
/// Polvo flotando en el aire (queda genial dentro de un rayo de sol).
/// Crea motas chiquitas que suben lento, se mecen de costado y laten de brillo.
/// Cuando una sale de la zona, reaparece del otro lado (flujo continuo).
/// Usa mezcla aditiva, asi brillan dentro del haz de luz.
///
/// Setup: ponelo como HIJO del rayo de luz (o suelto donde quieras polvo).
/// Asignale el sprite Art/FX/dustmote y ajusta el Area al tamano del rayo.
/// </summary>
public class DustMotes : MonoBehaviour
{
    [Header("Motas")]
    [Tooltip("Sprite de la mota (Art/FX/dustmote).")]
    public Sprite moteSprite;
    [Tooltip("Cuantas motas flotan en la zona.")]
    public int count = 25;
    [Tooltip("Zona donde flotan (ancho x alto), centrada en este objeto.")]
    public Vector2 area = new Vector2(3f, 6f);
    [Tooltip("Color del polvo. Calido queda natural dentro del rayo.")]
    public Color color = new Color(1f, 0.97f, 0.85f, 1f);

    [Header("Tamano y brillo")]
    public float minSize = 0.03f;
    public float maxSize = 0.08f;
    [Range(0f, 1f)] public float minAlpha = 0.10f;
    [Range(0f, 1f)] public float maxAlpha = 0.55f;
    [Tooltip("Que tan rapido titilan.")]
    public float twinkleSpeed = 0.5f;

    [Header("Movimiento")]
    [Tooltip("Direccion general de la deriva (sube y va apenas al costado).")]
    public Vector2 driftDir = new Vector2(0.15f, 1f);
    [Tooltip("Velocidad de la deriva (unidades/seg). Muy baja = flota.")]
    public float driftSpeed = 0.18f;
    [Tooltip("Cuanto se mecen de costado.")]
    public float swayAmount = 0.12f;
    public float swaySpeed = 0.4f;

    [Header("Orden de dibujado")]
    [Tooltip("Si esta vacio, copia el Sorting Layer del padre (el rayo).")]
    public string sortingLayerName = "";
    [Tooltip("Se suma al Order in Layer del padre.")]
    public int sortingOrderOffset = 1;

    static Material sharedAdditive;

    SpriteRenderer[] motes;
    Vector2[] pos;      // posicion local dentro de la zona
    float[] speed;      // velocidad propia de cada mota
    float[] phase;      // desfase para titileo y vaiven
    float[] sway;       // amplitud propia del vaiven

    void Start()
    {
        if (moteSprite == null) { enabled = false; return; }

        if (sharedAdditive == null)
        {
            var sh = Shader.Find("Obby/SpriteAdditive");
            if (sh != null) sharedAdditive = new Material(sh);
        }

        // de donde saco el orden de dibujado
        var refSr = GetComponentInParent<SpriteRenderer>();
        int baseOrder = refSr != null ? refSr.sortingOrder : 0;
        int layerId = refSr != null ? refSr.sortingLayerID : 0;

        count = Mathf.Max(0, count);
        motes = new SpriteRenderer[count];
        pos = new Vector2[count];
        speed = new float[count];
        phase = new float[count];
        sway = new float[count];

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Mota");
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = moteSprite;
            if (sharedAdditive != null) sr.sharedMaterial = sharedAdditive;
            if (!string.IsNullOrEmpty(sortingLayerName)) sr.sortingLayerName = sortingLayerName;
            else sr.sortingLayerID = layerId;
            sr.sortingOrder = baseOrder + sortingOrderOffset;

            float size = Random.Range(minSize, maxSize);
            go.transform.localScale = Vector3.one * size;

            pos[i] = new Vector2(Random.Range(-area.x, area.x) * 0.5f,
                                 Random.Range(-area.y, area.y) * 0.5f);
            speed[i] = Random.Range(0.6f, 1.4f);
            phase[i] = Random.value * 10f;
            sway[i] = Random.Range(0.5f, 1.5f);
            motes[i] = sr;
        }
    }

    void Update()
    {
        if (motes == null) return;

        Vector2 dir = driftDir.sqrMagnitude > 0.0001f ? driftDir.normalized : Vector2.up;
        float halfY = area.y * 0.5f;
        float halfX = area.x * 0.5f;

        for (int i = 0; i < motes.Length; i++)
        {
            var sr = motes[i];
            if (sr == null) continue;

            // deriva lenta
            pos[i] += dir * (driftSpeed * speed[i] * Time.deltaTime);

            // si se fue por arriba, vuelve por abajo (y al reves)
            if (pos[i].y > halfY) { pos[i].y = -halfY; pos[i].x = Random.Range(-halfX, halfX); }
            else if (pos[i].y < -halfY) { pos[i].y = halfY; pos[i].x = Random.Range(-halfX, halfX); }
            if (pos[i].x > halfX) pos[i].x = -halfX;
            else if (pos[i].x < -halfX) pos[i].x = halfX;

            // vaiven de costado (no cambia pos, es solo visual)
            float t = (Time.time + phase[i]);
            float offsetX = Mathf.Sin(t * swaySpeed * Mathf.PI * 2f) * swayAmount * sway[i];
            sr.transform.localPosition = new Vector3(pos[i].x + offsetX, pos[i].y, 0f);

            // titileo
            float tw = (Mathf.Sin(t * twinkleSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            Color c = color;
            c.a = Mathf.Lerp(minAlpha, maxAlpha, tw);
            sr.color = c;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0.6f, 0.6f);
        Gizmos.DrawWireCube(transform.position, new Vector3(area.x, area.y, 0f));
    }
}
