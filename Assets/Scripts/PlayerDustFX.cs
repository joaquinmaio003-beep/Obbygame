using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Polvito blanco de Obby: nubecitas que salen de sus pies al CORRER, al ATERRIZAR
/// y al DASHEAR, y una bocanada al TIRAR una piedra.
/// Cada nube crece, se desvanece y desaparece sola.
///
/// Setup: poner este componente en Obby (el mismo objeto que PlayerController2D)
/// y asignarle el sprite Art/FX/dustpuff en "Dust Sprite".
/// </summary>
public class PlayerDustFX : MonoBehaviour
{
    [Header("Sprite del polvo")]
    [Tooltip("Sprite de la nubecita (Art/FX/dustpuff).")]
    public Sprite dustSprite;

    [Header("Al correr")]
    public bool dustOnRun = true;
    [Tooltip("Velocidad minima para que largue polvo al correr.")]
    public float runSpeedThreshold = 3f;
    [Tooltip("Segundos entre nubecita y nubecita mientras corre.")]
    public float runPuffInterval = 0.16f;
    [Tooltip("Desde donde salen (respecto al centro de Obby). Y negativo = a los pies.")]
    public Vector2 footOffset = new Vector2(0f, -0.45f);

    [Header("Al aterrizar")]
    public bool dustOnLand = true;
    [Tooltip("Velocidad de caida minima para levantar polvo al tocar el piso.")]
    public float landSpeedThreshold = 6f;
    [Tooltip("Cuantas nubecitas salen en el golpe de aterrizaje.")]
    public int landPuffCount = 4;
    [Tooltip("Que tan grandes salen respecto a las normales.")]
    public float landSizeMultiplier = 1.4f;

    [Header("Al dashear")]
    public bool dustOnDash = true;
    [Tooltip("Cuantas nubecitas salen al arrancar el dash.")]
    public int dashPuffCount = 3;
    [Tooltip("Segundos entre nubecitas durante el dash (0 = solo al arrancar).")]
    public float dashTrailInterval = 0.04f;

    [Header("Al tirar una piedra")]
    public bool dustOnThrow = true;
    [Tooltip("Desde donde sale la bocanada del tiro (X se invierte segun a donde mira).")]
    public Vector2 throwOffset = new Vector2(0.35f, 0f);

    [Header("Aspecto")]
    public Color dustColor = new Color(1f, 1f, 1f, 0.75f);
    [Tooltip("Tamano al nacer y al morir (crece mientras se desvanece).")]
    public float startSize = 0.12f;
    public float endSize = 0.3f;
    [Tooltip("Cuanto dura cada nubecita.")]
    public float lifetime = 0.35f;
    [Tooltip("Cuanto se eleva y se aleja mientras se desvanece.")]
    public float driftSpeed = 0.7f;
    [Tooltip("Se dibuja detras de Obby (recomendado).")]
    public int sortingOrderOffset = -1;

    PlayerController2D player;
    SpriteRenderer playerSr;
    float runTimer;
    float dashTimer;
    bool wasGrounded;
    bool wasDashing;
    float fallSpeed;       // velocidad de caida acumulada antes de tocar el piso

    void Awake()
    {
        player = GetComponent<PlayerController2D>();
        playerSr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        AnimarNubes();   // las nubes que ya salieron se animan siempre, aunque abajo se corte antes

        if (player == null || dustSprite == null) return;

        bool grounded = player.IsGrounded;
        bool dashing = player.IsDashing;

        // --- guardar a que velocidad viene cayendo (para el golpe de aterrizaje) ---
        if (!grounded && player.Velocity.y < 0f)
            fallSpeed = -player.Velocity.y;

        // --- aterrizaje: justo cuando pasa de aire a piso ---
        if (dustOnLand && grounded && !wasGrounded && fallSpeed >= landSpeedThreshold)
            GolpeAterrizaje();
        if (grounded) fallSpeed = 0f;
        wasGrounded = grounded;

        // --- dash: bocanada al arrancar + estela mientras dura ---
        if (dustOnDash && dashing)
        {
            if (!wasDashing) EstelaDash();       // arranque
            if (dashTrailInterval > 0f)
            {
                dashTimer -= Time.deltaTime;
                if (dashTimer <= 0f)
                {
                    dashTimer = dashTrailInterval;
                    Lanzar(PiePos(), -player.Facing);
                }
            }
        }
        if (!dashing) dashTimer = 0f;
        wasDashing = dashing;

        // --- correr ---
        if (!dustOnRun) return;
        bool corriendo = grounded && !dashing && Mathf.Abs(player.Velocity.x) > runSpeedThreshold;
        if (!corriendo) { runTimer = 0f; return; }

        runTimer -= Time.deltaTime;
        if (runTimer > 0f) return;
        runTimer = runPuffInterval;
        Lanzar(PiePos(), -player.Facing);   // hacia atras del sentido en que corre
    }

    /// <summary>Bocanada al tirar una piedra. La llama RockThrower.</summary>
    public void ThrowPuff()
    {
        if (!dustOnThrow || dustSprite == null || player == null) return;
        Vector2 pos = (Vector2)transform.position + new Vector2(throwOffset.x * player.Facing, throwOffset.y);
        Lanzar(pos, player.Facing);
    }

    Vector2 PiePos()
    {
        return (Vector2)transform.position + new Vector2(footOffset.x * player.Facing, footOffset.y);
    }

    // Golpe al caer: varias nubes que se abren hacia AMBOS lados, mas grandes.
    void GolpeAterrizaje()
    {
        Vector2 pie = PiePos();
        for (int i = 0; i < landPuffCount; i++)
        {
            int lado = (i % 2 == 0) ? -1 : 1;                       // alterna izquierda/derecha
            float sep = Random.Range(0.02f, 0.16f) * lado;
            Lanzar(pie + new Vector2(sep, 0f), lado, 1.6f, landSizeMultiplier); // bien horizontal
        }
    }

    // Arranque del dash: nubes hacia atras del sentido del dash.
    void EstelaDash()
    {
        Vector2 pie = PiePos();
        for (int i = 0; i < dashPuffCount; i++)
        {
            float sep = Random.Range(-0.1f, 0.1f);
            Lanzar(pie + new Vector2(sep, Random.Range(0f, 0.2f)), -player.Facing, 1.2f);
        }
    }

    // ---- pool de nubecitas ----
    // Antes cada nube era un GameObject NUEVO con su propia corrutina, que se destruia al
    // desvanecerse: corriendo o dasheando eran decenas de objetos creados y destruidos por
    // segundo (basura para el GC = tirones). Ahora se reciclan siempre las mismas.
    class Nube
    {
        public GameObject go;
        public Transform tr;
        public SpriteRenderer sr;
        public Vector3 desde, deriva;
        public float t, sizeMul;
        public bool viva;
    }
    readonly List<Nube> nubes = new List<Nube>();
    Transform poolRoot;

    // horizBias: cuanto se abre de costado (mas alto = mas horizontal).
    void Lanzar(Vector2 pos, int dir, float horizBias = 0.35f, float sizeMul = 1f)
    {
        Nube n = NubeLibre();
        n.desde = pos;
        n.deriva = new Vector3(dir * horizBias, 1f, 0f).normalized * driftSpeed;
        n.sizeMul = sizeMul;
        n.t = 0f;
        n.viva = true;

        n.tr.position = pos;
        n.tr.localScale = Vector3.one * (startSize * sizeMul);
        n.sr.sprite = dustSprite;
        n.sr.color = dustColor;
        if (playerSr != null)
        {
            n.sr.sortingLayerID = playerSr.sortingLayerID;
            n.sr.sortingOrder = playerSr.sortingOrder + sortingOrderOffset;
        }
        n.go.SetActive(true);
    }

    // Devuelve una nube apagada para reusar; si estan todas en uso, crea una mas.
    Nube NubeLibre()
    {
        for (int i = 0; i < nubes.Count; i++)
            if (!nubes[i].viva) return nubes[i];

        if (poolRoot == null) poolRoot = new GameObject("Polvo (pool)").transform;
        var go = new GameObject("Polvo");
        go.transform.SetParent(poolRoot, false);
        var n = new Nube { go = go, tr = go.transform, sr = go.AddComponent<SpriteRenderer>() };
        if (NubePolvo.MaterialPolvo != null) n.sr.sharedMaterial = NubePolvo.MaterialPolvo;   // sin luz: blanco de verdad
        nubes.Add(n);
        return n;
    }

    // Crece, se eleva/aleja y se desvanece. Al terminar se apaga y queda libre para reusar.
    void AnimarNubes()
    {
        float alpha0 = dustColor.a;
        for (int i = 0; i < nubes.Count; i++)
        {
            var n = nubes[i];
            if (!n.viva) continue;

            n.t += Time.deltaTime;
            float k = Mathf.Clamp01(n.t / lifetime);

            n.tr.position = n.desde + n.deriva * (k * lifetime);
            n.tr.localScale = Vector3.one * (Mathf.Lerp(startSize, endSize, k) * n.sizeMul);

            Color c = dustColor;
            c.a = Mathf.Lerp(alpha0, 0f, k);   // se apaga
            n.sr.color = c;

            if (n.t >= lifetime) { n.viva = false; n.go.SetActive(false); }
        }
    }

    void OnDestroy()
    {
        if (poolRoot != null) Destroy(poolRoot.gameObject);
    }
}
