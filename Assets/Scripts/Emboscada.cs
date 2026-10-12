using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// EMBOSCADA. Cuando Obby entra a esta zona le empiezan a caer enemigos por atras, uno tras otro,
/// mientras tiene que empujar la roca hasta su hueco para poder seguir. Termina cuando la roca
/// encaja (o cuando Obby pasa el punto final): ahi dejan de venir.
///
/// - Arranca con una CINEMATICA corta: Obby queda quieto, la camara va a mostrar a los primeros
///   cayendo atras suyo y vuelve. Mientras dura, los que cayeron esperan parados.
/// - Caen DESDE ARRIBA, atras de Obby y siempre a la vista: a cielo abierto desde arriba de la
///   pantalla; si hay techo (un tunel, una cueva) salen del techo. Si pusiste Spawn Points,
///   aparecen ahi.
/// - Los guerreros, al tocar el piso, dan un saltito hacia Obby. Los que van mas adelante caminan
///   revoleando la espada.
/// - Si Obby se cae a un pozo y vuelve al checkpoint, se rearma todo: los enemigos de la emboscada
///   desaparecen y la zona vuelve a dispararse cuando entre.
///
/// Setup:
/// 1. Arrastra el prefab Prefab/Emboscada al nivel, un poco antes de la roca (el recuadro rojo es
///    la zona que la dispara).
/// 2. Rock: arrastra la roca que hay que empujar (la misma del Hueco Para Roca). Cuando encaja
///    en su hueco, la emboscada termina.
/// 3. Listo. Lo demas (cuantos enemigos, cada cuanto, cuales) viene con valores de fabrica.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Emboscada : MonoBehaviour
{
    [Header("Quienes atacan")]
    [Tooltip("Prefabs de los enemigos que caen (elige uno al azar cada vez). De fabrica: el guerrero.")]
    public GameObject[] enemyPrefabs;
    [Tooltip("Cuantos caen en total. 0 = siguen cayendo hasta que termine la emboscada.")]
    public int totalEnemies = 10;
    [Tooltip("Cuantos caen de entrada, uno atras del otro, mientras la camara los muestra.")]
    public int firstWave = 4;
    [Tooltip("Cuantos puede haber vivos a la vez. Si esta lleno, espera a que caiga uno.")]
    public int maxAlive = 10;
    [Tooltip("Segundos entre un enemigo y el siguiente (despues de los primeros).")]
    public float interval = 1f;
    [Tooltip("Desde que distancia te detectan los que caen (mas que un enemigo comun: vienen por vos).")]
    public float aggroRange = 18f;
    [Tooltip("Que tan rapido caminan los de la emboscada, comparado con uno comun. 1 = igual; 0.6 = bastante mas lentos. " +
             "Referencia: Obby empuja la roca a 2.5 y el guerrero comun persigue a 2.8.")]
    [Range(0.2f, 1.5f)] public float enemySpeed = 0.6f;

    [Header("Como entran (solo los guerreros)")]
    [Tooltip("Altura del saltito que da cada uno al tocar el piso. 0 = sin saltito.")]
    public float hopHeight = 0.5f;
    [Tooltip("Cuanto avanza hacia Obby en ese saltito.")]
    public float hopDistance = 1.2f;
    [Tooltip("Cuantos de los que van mas adelante caminan revoleando la espada (solo se ve: pegan recien al llegar). 0 = ninguno.")]
    public int swordsInFront = 3;

    [Header("Donde caen")]
    [Tooltip("Opcional: lugares exactos donde aparecen (elige uno al azar). Vacio = caen desde arriba, atras de Obby.")]
    public Transform[] spawnPoints;
    [Tooltip("Sin Spawn Points: a cuantas unidades atras de Obby caen (nunca mas atras que el borde de la pantalla).")]
    public float behindDistance = 8f;
    [Tooltip("Capas del piso (para comprobar que caen sobre algo). Vacio = la layer Grounded.")]
    public LayerMask groundLayer;

    [Header("Cuando termina")]
    [Tooltip("La roca que hay que empujar: cuando encaja en su hueco, la emboscada termina.")]
    public PushableBox rock;
    [Tooltip("Opcional: tambien termina cuando Obby pasa este punto.")]
    public Transform endPoint;
    [Tooltip("Al terminar, los enemigos de la emboscada que queden vivos se deshacen en pixeles.")]
    public bool killRemaining = false;

    [Header("Cinematica del comienzo")]
    [Tooltip("Segundos que la camara muestra a los primeros cayendo (Obby queda quieto). 0 = sin cinematica.")]
    public float introTime = 1.6f;
    [Tooltip("Segundos para que la camara vuelva a Obby antes de devolverle el control.")]
    public float cameraReturnTime = 0.4f;

    [Header("Efectos (opcional)")]
    [Tooltip("Sonido cuando arranca. Vacio = el de fabrica.")]
    public AudioClip startSound;
    [Tooltip("Golpe de cada enemigo al caer al piso. Vacio = el de fabrica.")]
    public AudioClip landSound;
    [Tooltip("La camara tiembla un poco cuando arranca.")]
    public bool cameraShake = true;
    [Tooltip("Algo extra que quieras que pase cuando arranca.")]
    public UnityEvent onStart;
    [Tooltip("Algo extra que quieras que pase cuando termina.")]
    public UnityEvent onEnd;

    static readonly List<Emboscada> todas = new();
    static readonly WaitForFixedUpdate s_pasoFisica = new();
    const float EntreLosPrimeros = 0.35f;   // segundos entre uno y otro de los que caen de entrada
    const float MinAtras = 3f;              // nunca caen mas cerca de Obby que esto
    const float BuenaCaida = 3.5f;          // bajo techo: alto libre para que se los vea caer de arriba

    readonly List<GameObject> enemigos = new();        // los que hizo aparecer esta emboscada
    readonly List<WarriorEnemy> guerreros = new();     // de esos, los guerreros (saltito y espada)
    readonly List<Vector2> recientes = new();          // donde cayeron los ultimos (x = lugar, y = cuando)
    PlayerController2D obby;
    Collider2D obbyCol;
    CameraFollow2D cam;
    int dir = 1;          // hacia donde avanza Obby (hacia la roca)
    int caidos;           // cuantos ya hizo aparecer
    bool disparada;       // ya arranco (no vuelve a dispararse hasta rearmarse)
    bool activa;          // siguen cayendo enemigos
    bool bloqueeControl;
    float esperarHasta;   // los que caen antes de este momento esperan parados (dura la cinematica)
    float proximaMarca;   // cuando se vuelve a revisar quienes van adelante

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Awake()
    {
        todas.Add(this);
        if (groundLayer.value == 0) groundLayer = LayerMask.GetMask("Grounded");
    }

    void OnDestroy() { todas.Remove(this); }

    void Start()
    {
        if (Camera.main != null) cam = Camera.main.GetComponent<CameraFollow2D>();
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
            Debug.LogWarning("Emboscada: falta poner al menos un prefab de enemigo en Enemy Prefabs.", this);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (disparada || enemyPrefabs == null || enemyPrefabs.Length == 0) return;
        var pc = other.GetComponentInParent<PlayerController2D>();
        if (pc == null) return;
        if (rock != null && rock.Encajada) return;   // la roca ya esta en su hueco: no hay emboscada

        obby = pc;
        obbyCol = pc.GetComponent<Collider2D>();
        disparada = true;
        StartCoroutine(Emboscar());
    }

    IEnumerator Emboscar()
    {
        // Obby avanza hacia la roca (si no hay roca, hacia donde mira): "atras" es lo contrario
        dir = rock != null ? (rock.transform.position.x >= obby.transform.position.x ? 1 : -1) : obby.Facing;
        caidos = 0;
        activa = true;

        Sonidos.Play(startSound, "emboscada");   // vacio = el de fabrica
        if (cameraShake && cam != null) cam.Shake(0.3f, 0.18f);
        onStart?.Invoke();

        // CINEMATICA: caen los primeros, uno atras del otro; la camara los muestra y vuelve a Obby.
        // Los que caen mientras tanto esperan parados: Obby esta quieto y no puede defenderse.
        esperarHasta = introTime > 0f ? Time.time + introTime + cameraReturnTime + 0.3f : 0f;
        GameObject primero = HacerCaer();
        bool cinematica = primero != null && introTime > 0f;
        if (cinematica)
        {
            BloquearControl(true);
            if (cam != null) cam.Enfocar(primero.transform);
        }
        else esperarHasta = 0f;   // sin cinematica nadie espera
        float mostrado = 0f;
        for (int i = 1; i < firstWave && PuedeCaerOtro(); i++)
        {
            yield return new WaitForSeconds(EntreLosPrimeros);
            mostrado += EntreLosPrimeros;
            HacerCaer();
        }
        if (cinematica)
        {
            if (mostrado < introTime) yield return new WaitForSeconds(introTime - mostrado);
            if (cam != null) cam.SoltarFoco();
            yield return new WaitForSeconds(cameraReturnTime);
            BloquearControl(false);
        }

        // siguen cayendo, de a uno, hasta completar el total (o hasta que termine)
        while (activa && (totalEnemies <= 0 || caidos < totalEnemies))
        {
            yield return new WaitForSeconds(interval);
            // si justo no habia lugar (una pared, un pozo) prueba de nuevo enseguida
            while (PuedeCaerOtro() && HacerCaer() == null) yield return new WaitForSeconds(0.3f);
        }
    }

    // Queda alguno por caer y hay lugar para uno mas?
    bool PuedeCaerOtro()
    {
        return activa && (totalEnemies <= 0 || caidos < totalEnemies) && Vivos() < maxAlive;
    }

    void Update()
    {
        // los que van mas adelante revolean la espada (se revisa cada tanto, no en cada frame)
        if (guerreros.Count > 0 && obby != null && Time.time >= proximaMarca)
        {
            proximaMarca = Time.time + 0.25f;
            MarcarLosDeAdelante();
        }

        if (!activa) return;

        // la roca encajo en su hueco, u Obby paso el punto final: se termino
        bool rocaLista = rock != null && rock.Encajada;
        bool paso = endPoint != null && obby != null && (obby.transform.position.x - endPoint.position.x) * dir >= 0f;
        if (rocaLista || paso) Terminar();
    }

    // Los 'swordsInFront' guerreros vivos mas cercanos a Obby caminan revoleando la espada.
    void MarcarLosDeAdelante()
    {
        float ox = obby.transform.position.x;
        guerreros.Sort((a, b) => Lejos(a, ox).CompareTo(Lejos(b, ox)));
        for (int i = 0; i < guerreros.Count; i++)
            if (guerreros[i] != null) guerreros[i].Amenaza = i < swordsInFront;
    }

    // Distancia a Obby; los muertos quedan al fondo de la fila.
    static float Lejos(WarriorEnemy g, float ox)
    {
        return g != null && g.gameObject.activeInHierarchy ? Mathf.Abs(g.transform.position.x - ox) : float.MaxValue;
    }

    // Deja de caer gente. Los que quedan siguen siendo enemigos comunes (o se deshacen, si lo elegiste).
    void Terminar()
    {
        activa = false;
        StopAllCoroutines();
        BloquearControl(false);
        if (cam != null) cam.SoltarFoco();

        if (killRemaining)
            foreach (var e in enemigos)
                if (e != null && e.activeInHierarchy) e.GetComponent<IStunnable>()?.Defeat();
        onEnd?.Invoke();
    }

    int Vivos()
    {
        int n = 0;
        foreach (var e in enemigos) if (e != null && e.activeInHierarchy) n++;
        return n;
    }

    // Hace aparecer un enemigo: en un Spawn Point, o cayendo desde arriba atras de Obby.
    GameObject HacerCaer()
    {
        if (!Lugar(out Vector2 donde, out bool bajoTecho)) return null;

        GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        if (prefab == null) return null;
        var go = Instantiate(prefab, donde, Quaternion.identity);
        enemigos.Add(go);
        caidos++;
        recientes.Add(new Vector2(donde.x, Time.time));

        var guerrero = go.GetComponent<WarriorEnemy>();
        var dibujo = go.GetComponent<SpriteRenderer>();

        // Bajo techo: 'donde' es el techo mismo.
        if (bajoTecho)
        {
            var cuerpo = go.GetComponent<Collider2D>();
            if (guerrero != null && dibujo != null)
            {
                // Arranca metido ADENTRO del techo (el piso se dibuja delante y lo tapa) y cae
                // atravesandolo: se lo ve salir del techo, no aparecer de la nada. Si el techo es
                // finito y no alcanza a taparlo, asoma por abajo lo que sobre.
                float alto = dibujo.bounds.size.y;
                float asoma = Mathf.Max(0f, alto + 0.1f - GrosorDelTecho(donde, alto + 0.2f));
                go.transform.position += Vector3.up * (donde.y + 0.05f - asoma - dibujo.bounds.min.y);
                guerrero.SalirDelTecho(donde.y - 0.02f);
            }
            else if (cuerpo != null)
            {
                // los demas aparecen con todo el cuerpo justo por debajo
                float sobra = cuerpo.bounds.max.y - (donde.y - 0.05f);
                if (sobra > 0f) go.transform.position += Vector3.down * sobra;
            }
            EnemigoFX.Polvo(new Bounds(donde + Vector2.down * 0.4f, new Vector3(1.2f, 0.8f, 0f)), dibujo, 5);
        }
        StartCoroutine(AvisarCaida(go, guerrero));

        // vienen por Obby desde lejos, no se duermen aunque quede lejos, y caminan a la velocidad
        // de la emboscada (mas lentos que uno comun, para dar tiempo a empujar la roca)
        if (guerrero != null)
        {
            guerrero.detectRange = Mathf.Max(guerrero.detectRange, aggroRange);
            guerrero.sleepDistance = 0f;
            guerrero.chaseSpeed *= enemySpeed;
            guerrero.patrolSpeed *= enemySpeed;
            if (Time.time < esperarHasta) guerrero.Esperar(esperarHasta - Time.time);
            guerreros.Add(guerrero);
        }
        var tirador = go.GetComponent<Enemy>();
        if (tirador != null)
        {
            tirador.shootRange = Mathf.Max(tirador.shootRange, aggroRange);
            tirador.sleepDistance = 0f;
            tirador.moveSpeed *= enemySpeed;
        }
        var sierra = go.GetComponent<EnemigoSierra>();
        if (sierra != null)
        {
            sierra.detectRange = Mathf.Max(sierra.detectRange, aggroRange);
            sierra.sleepDistance = 0f;
            sierra.chaseSpeed *= enemySpeed;
            sierra.patrolSpeed *= enemySpeed;
        }
        return go;
    }

    // Cuanto techo hay encima de ese punto para tapar al que sale (mide hasta 'max').
    float GrosorDelTecho(Vector2 techo, float max)
    {
        float g = 0f;
        while (g < max && Physics2D.OverlapPoint(techo + Vector2.up * (g + 0.2f), groundLayer) != null) g += 0.2f;
        return g;
    }

    // Cuando el que cayo toca el piso (venia bajando y freno): golpe seco, polvo y, si es un
    // guerrero, el saltito hacia Obby.
    IEnumerator AvisarCaida(GameObject go, WarriorEnemy guerrero)
    {
        float y = go.transform.position.y;
        bool cayo = false;
        for (float t = 0f; t < 4f; t += Time.fixedDeltaTime)
        {
            yield return s_pasoFisica;
            if (go == null || !go.activeInHierarchy) yield break;
            float ahora = go.transform.position.y;
            if (ahora < y - 0.001f) cayo = true;
            else if (cayo || t > 0.2f) break;   // toco el piso (o aparecio ya parado: no cayo nunca)
            y = ahora;
        }
        if (cayo)
        {
            Sonidos.PlayEn(landSound, "enemigo_cae", go.transform.position);
            var cuerpo = go.GetComponent<Collider2D>();
            if (cuerpo != null) EnemigoFX.Polvo(cuerpo.bounds, go.GetComponent<SpriteRenderer>(), 4);
        }
        if (guerrero != null && hopHeight > 0f && obby != null)
        {
            // el saltito no lo deja pegado a Obby: frena a unas unidades
            float lejos = Mathf.Abs(obby.transform.position.x - go.transform.position.x);
            guerrero.Saltito(hopHeight, Mathf.Clamp(lejos - MinAtras, 0f, hopDistance));
        }
    }

    // Donde aparece el proximo. Sin Spawn Points: atras de Obby, a la vista y en el MISMO lugar
    // donde esta el. A cielo abierto cae desde arriba de la pantalla; si hay techo (un tunel, una
    // cueva) sale del techo, y ahi elige donde el techo esta alto para que se lo vea caer.
    // Antes caian siempre del cielo: adentro de un tunel quedaban parados arriba del techo.
    bool Lugar(out Vector2 donde, out bool bajoTecho)
    {
        bajoTecho = false;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform p = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (p != null) { donde = p.position; return true; }
        }

        Camera c = Camera.main;
        bool hayCamara = c != null && c.orthographic;
        Vector2 o = obbyCol != null ? (Vector2)obbyCol.bounds.center : (Vector2)obby.transform.position;
        float cielo = hayCamara ? c.transform.position.y + c.orthographicSize + 1.5f : o.y + 8f;

        // A LA VISTA: nunca mas atras que el borde de la pantalla. Si no, caian donde no se los ve
        // y, como son mas lentos que Obby, no llegaba a verse ninguno.
        float maxAtras = behindDistance + 1.5f;
        if (hayCamara)
        {
            float borde = c.transform.position.x - dir * (c.orthographicSize * c.aspect - 2f);   // 2 unidades adentro
            maxAtras = Mathf.Min(maxAtras, (o.x - borde) * dir);
        }
        maxAtras = Mathf.Max(maxAtras, MinAtras);

        for (int i = recientes.Count - 1; i >= 0; i--)
            if (Time.time - recientes[i].y > 1.5f) recientes.RemoveAt(i);

        // A la altura de Obby: aire ahi, piso debajo y, si hay, el techo de encima. El primero
        // que sirva del todo (buena altura y sin otro recien caido encima) queda; si ninguno
        // sirve del todo, el mejor de los que hubo.
        bool hayReserva = false, reservaBajoTecho = false;
        Vector2 reserva = default;
        float puntosReserva = 0f;
        for (int intento = 0; intento < 10; intento++)
        {
            float atras = Mathf.Clamp(behindDistance + Random.Range(-1.5f, 1.5f) - intento * 0.7f, MinAtras, maxAtras);
            float x = o.x - dir * atras;
            var medio = new Vector2(x, o.y);
            if (Physics2D.OverlapPoint(medio, groundLayer) != null) continue;            // ahi hay una pared
            RaycastHit2D piso = Physics2D.Raycast(medio, Vector2.down, 40f, groundLayer);
            if (piso.collider == null) continue;                                         // abajo hay un pozo

            bool techo = TechoSobre(medio, cielo - o.y, out float techoY);
            float alto = techo ? techoY - piso.point.y : BuenaCaida;
            var lugar = new Vector2(x, techo ? techoY : cielo);
            bool apretado = Apretado(x);
            if (alto >= BuenaCaida && !apretado) { donde = lugar; bajoTecho = techo; return true; }

            float puntos = (alto >= BuenaCaida ? 10f : alto) + (apretado ? 0f : 1f);     // primero la altura
            if (!hayReserva || puntos > puntosReserva)
            {
                hayReserva = true; puntosReserva = puntos; reserva = lugar; reservaBajoTecho = techo;
            }
        }
        if (hayReserva) { donde = reserva; bajoTecho = reservaBajoTecho; return true; }

        // atras el terreno esta mas alto que Obby (viene bajando una cuesta): del cielo, como siempre
        for (int intento = 0; intento < 6; intento++)
        {
            float atras = Mathf.Clamp(behindDistance + Random.Range(-1.5f, 1.5f) - intento * 0.8f, MinAtras, maxAtras);
            var punto = new Vector2(o.x - dir * atras, cielo);
            if (Physics2D.OverlapPoint(punto, groundLayer) != null) continue;            // adentro de una pared
            if (!Physics2D.Raycast(punto, Vector2.down, 40f, groundLayer)) continue;     // abajo hay un pozo
            donde = punto;
            return true;
        }
        donde = default;
        return false;
    }

    // El techo mas bajo sobre ese punto. Mira tambien a los costados, por el ancho del cuerpo:
    // asi no sale pegado a un escalon del techo ni a una pared.
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

    // Ahi mismo acaba de caer otro? (para que no salgan todos encimados)
    bool Apretado(float x)
    {
        for (int i = 0; i < recientes.Count; i++)
            if (Mathf.Abs(recientes[i].x - x) < 0.9f) return true;
        return false;
    }

    // Deja quieto a Obby durante la cinematica, o le devuelve el control.
    void BloquearControl(bool bloquear)
    {
        if (obby == null) return;
        if (bloquear) { obby.ControlBloqueado = true; bloqueeControl = true; }
        else if (bloqueeControl) { obby.ControlBloqueado = false; bloqueeControl = false; }
    }

    // ---------------- volver al checkpoint ----------------

    /// <summary>Rearma todas las emboscadas (lo llama PlayerRespawn al volver a un checkpoint).</summary>
    public static void ResetAll()
    {
        for (int i = 0; i < todas.Count; i++)
            if (todas[i] != null) todas[i].Rearmar();
    }

    void Rearmar()
    {
        StopAllCoroutines();
        activa = false;
        disparada = false;
        caidos = 0;
        esperarHasta = 0f;
        BloquearControl(false);
        if (cam != null) cam.SoltarFoco();
        foreach (var e in enemigos) if (e != null) Destroy(e);   // los de la emboscada no vuelven: caen de nuevo
        enemigos.Clear();
        guerreros.Clear();
        recientes.Clear();
    }

    void OnDrawGizmos()
    {
        var c = GetComponent<Collider2D>();
        if (c == null) return;
        Bounds b = c.bounds;
        Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.2f);
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = new Color(1f, 0.25f, 0.2f, 1f);
        Gizmos.DrawWireCube(b.center, b.size);
        if (rock != null) Gizmos.DrawLine(b.center, rock.transform.position);   // hacia la roca que hay que empujar
    }
}
