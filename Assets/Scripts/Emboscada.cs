using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// EMBOSCADA. Cuando Obby entra a esta zona le empiezan a llegar enemigos por atras, uno tras otro,
/// mientras tiene que empujar la roca hasta su hueco para poder seguir. Termina cuando la roca
/// encaja (o cuando Obby pasa el punto final): ahi dejan de venir.
///
/// - Arranca con una CINEMATICA corta: Obby queda quieto, la camara va a mostrar a los primeros
///   llegando atras suyo y vuelve. Mientras dura, los que llegaron esperan parados.
/// - Entran desde AFUERA de la pantalla, atras de Obby: unos caen desde arriba y otros saltan desde
///   abajo. Si en el medio hay techo o piso lo atraviesan dibujados por delante, asi se los ve
///   venir en vez de aparecer de la nada. Si pusiste Spawn Points, aparecen ahi.
/// - Los guerreros avanzan a los saltitos (uno al tocar el piso y despues cada tanto), todos
///   revoleando la espada, y en fila: ninguno se le encima al que tiene adelante.
/// - Cuando termina, los que quedan se RETIRAN: se van corriendo y desaparecen al salir de pantalla.
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
public partial class Emboscada : MonoBehaviour
{
    public enum Final { SeRetiran, SeDeshacen, SeQuedan }

    [Header("Quienes atacan")]
    [Tooltip("Prefabs de los enemigos que llegan (elige uno al azar cada vez). De fabrica: el guerrero.")]
    public GameObject[] enemyPrefabs;
    [Tooltip("Cuantos llegan en total. 0 = siguen llegando hasta que termine la emboscada.")]
    public int totalEnemies = 10;
    [Tooltip("Cuantos llegan de entrada, uno atras del otro, mientras la camara los muestra.")]
    public int firstWave = 3;
    [Tooltip("Cuantos puede haber vivos a la vez. Si esta lleno, espera a que caiga uno.")]
    public int maxAlive = 10;
    [Tooltip("Segundos entre un enemigo y el siguiente (despues de los primeros).")]
    public float interval = 1f;
    [Tooltip("Desde que distancia te detectan (mas que un enemigo comun: vienen por vos).")]
    public float aggroRange = 18f;
    [Tooltip("Que tan rapido caminan los de la emboscada, comparado con uno comun. 1 = igual; 0.6 = bastante mas lentos. " +
             "Referencia: Obby empuja la roca a 2.5 y el guerrero comun persigue a 2.8.")]
    [Range(0.2f, 1.5f)] public float enemySpeed = 0.72f;

    [Header("Como entran (solo los guerreros)")]
    [Tooltip("Que parte entra SALTANDO DESDE ABAJO de la pantalla; el resto CAE DESDE ARRIBA. " +
             "0 = todos de arriba, 1 = todos de abajo, 0.5 = uno y uno.")]
    [Range(0f, 1f)] public float fromBelow = 0.5f;
    [Tooltip("Altura de los saltitos. 0 = no saltan. Referencia: Obby salta 3.2.")]
    public float hopHeight = 0.9f;
    [Tooltip("Cuanto avanza hacia Obby en el primer saltito, el que da al tocar el piso.")]
    public float hopDistance = 1.2f;
    [Tooltip("Mientras lo persiguen dan un saltito cada tantos segundos (mas o menos). 0 = solo el del comienzo.")]
    public float hopEvery = 1.4f;
    [Tooltip("Los que estan a menos de esto de Obby NO saltan: solo revolean la espada.")]
    public float hopMinDistance = 4f;
    [Tooltip("Todos revolean la espada mientras ven a Obby (solo se ve: pegan recien al llegar).")]
    public bool swingSwords = true;
    [Tooltip("Las piedras de Obby los aturden. Destildado (de fabrica): les rebotan sin hacerles nada, " +
             "asi Obby no puede darse vuelta y frenarlos: tiene que empujar la roca.")]
    public bool rocksStun = false;
    [Tooltip("Distancia que se guardan entre ellos: hacen fila en vez de encimarse. 0 = se enciman.")]
    public float separation = 1.3f;

    [Header("Donde llegan")]
    [Tooltip("Opcional: lugares donde llegan. Llegan repartidos alrededor de uno que se vea en pantalla " +
             "(entrando igual desde arriba o desde abajo); si ninguno se ve, o esta vacio, llegan atras de Obby.")]
    public Transform[] spawnPoints;
    [Tooltip("Con Spawn Points: cuanto se reparten a los costados del punto, para no caer todos en el mismo lugar.")]
    public float spawnSpread = 2.5f;
    [Tooltip("A cuantas unidades atras de Obby llegan, mas o menos (nunca mas atras que el borde de la pantalla). " +
             "Un Spawn Point que quede mas lejos que esto no se usa.")]
    public float behindDistance = 5.5f;
    [Tooltip("Capas del piso (para comprobar que llegan sobre algo). Vacio = la layer Grounded.")]
    public LayerMask groundLayer;

    [Header("Cuando termina")]
    [Tooltip("La roca que hay que empujar: cuando encaja en su hueco, la emboscada termina.")]
    public PushableBox rock;
    [Tooltip("Opcional: tambien termina cuando Obby pasa este punto.")]
    public Transform endPoint;
    [Tooltip("Que hacen al terminar los que quedan vivos. Se Retiran: se dan vuelta, se van corriendo y desaparecen " +
             "al salir de pantalla. Se Deshacen: mueren todos en pixeles. Se Quedan: siguen como enemigos comunes.")]
    public Final whenDone = Final.SeRetiran;
    [Tooltip("Con Se Retiran: a que velocidad se van (el guerrero comun persigue a 2.8; Obby corre a 9).")]
    public float retreatSpeed = 4.5f;

    [Header("Cinematica del comienzo")]
    [Tooltip("Segundos que la camara muestra a los primeros llegando (Obby queda quieto). 0 = sin cinematica.")]
    public float introTime = 2f;
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
    const float EntreLosPrimeros = 0.35f;   // segundos entre uno y otro de los que llegan de entrada
    const float MinAtras = 3f;              // nunca llegan mas cerca de Obby que esto
    const float FueraDePantalla = 0.3f;     // cuanto mas alla del borde de la pantalla arrancan
    const float SaltoDeEntrada = 1.3f;      // los que saltan desde abajo pasan el piso por esto

    readonly List<GameObject> enemigos = new();        // los que hizo aparecer esta emboscada
    PlayerController2D obby;
    Collider2D obbyCol;
    CameraFollow2D cam;
    Transform foco;       // adonde mira la camara en la cinematica (la zona donde llegan)
    int dir = 1;          // hacia donde avanza Obby (hacia la roca)
    int caidos;           // cuantos ya hizo aparecer
    bool disparada;       // ya arranco (no vuelve a dispararse hasta rearmarse)
    bool activa;          // siguen llegando enemigos
    bool bloqueeControl;
    float esperarHasta;   // los que llegan antes de este momento esperan parados (dura la cinematica)
    float cuentaAbajo;    // reparte quienes entran desde abajo (ver fromBelow)
    Vector2 ultimoPie;    // donde quedo (o va a quedar) parado el ultimo que llego

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
        cuentaAbajo = 0f;
        activa = true;

        Sonidos.Play(startSound, "emboscada");   // vacio = el de fabrica
        if (cameraShake && cam != null) cam.Shake(0.3f, 0.18f);
        obby.Susto();   // Obby pega un saltito de susto
        onStart?.Invoke();

        // CINEMATICA: llegan los primeros, uno atras del otro; la camara los muestra y vuelve a Obby.
        // Los que llegan mientras tanto esperan parados: Obby esta quieto y no puede defenderse.
        esperarHasta = introTime > 0f ? Time.time + introTime + cameraReturnTime + 0.3f : 0f;
        GameObject primero = HacerCaer();
        bool cinematica = primero != null && introTime > 0f;
        if (cinematica)
        {
            BloquearControl(true);
            if (cam != null) cam.Enfocar(DondeMirar());
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

        // siguen llegando, de a uno, hasta completar el total (o hasta que termine)
        while (activa && (totalEnemies <= 0 || caidos < totalEnemies))
        {
            yield return new WaitForSeconds(interval);
            // si justo no habia lugar (una pared, un pozo) prueba de nuevo enseguida
            while (PuedeCaerOtro() && HacerCaer() == null) yield return new WaitForSeconds(0.3f);
        }
    }

    // Adonde mira la camara en la cinematica: un punto quieto donde llega el primero, a la altura
    // de Obby. (Seguirlo a el la hacia subir y bajar: arranca fuera de la pantalla.)
    Transform DondeMirar()
    {
        if (foco == null)
        {
            foco = new GameObject("FocoEmboscada").transform;
            foco.SetParent(transform, true);
        }
        Vector3 o = obby.transform.position;
        foco.position = new Vector3(ultimoPie.x, o.y, o.z);
        return foco;
    }

    // Queda alguno por llegar y hay lugar para uno mas?
    bool PuedeCaerOtro()
    {
        return activa && (totalEnemies <= 0 || caidos < totalEnemies) && Vivos() < maxAlive;
    }

    void Update()
    {
        if (!activa) return;

        // la roca encajo en su hueco, u Obby paso el punto final: se termino
        bool rocaLista = rock != null && rock.Encajada;
        bool paso = endPoint != null && obby != null && (obby.transform.position.x - endPoint.position.x) * dir >= 0f;
        if (rocaLista || paso) Terminar();
    }

    // Deja de llegar gente. Los que quedan se retiran (o se deshacen, o siguen como enemigos comunes: ver whenDone).
    void Terminar()
    {
        activa = false;
        StopAllCoroutines();
        BloquearControl(false);
        if (cam != null) cam.SoltarFoco();

        foreach (var e in enemigos)
        {
            if (e == null || !e.activeInHierarchy) continue;
            if (whenDone == Final.SeDeshacen) e.GetComponent<IStunnable>()?.Defeat();
            else if (whenDone == Final.SeRetiran)
            {
                // solo los guerreros saben retirarse; otros enemigos quedan como uno comun
                var guerrero = e.GetComponent<WarriorEnemy>();
                if (guerrero != null) guerrero.Retirarse(retreatSpeed);
            }
        }
        onEnd?.Invoke();
    }

    int Vivos()
    {
        int n = 0;
        foreach (var e in enemigos) if (e != null && e.activeInHierarchy) n++;
        return n;
    }

    // Hace aparecer un enemigo: en un Spawn Point, o entrando desde afuera de la pantalla atras de Obby.
    GameObject HacerCaer()
    {
        GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        if (prefab == null) return null;

        // donde va a quedar parado: cerca de un Spawn Point que se vea; si no hay, atras de Obby
        if (!LugarEnPunto(out Vector2 pie, out float techoY) && !Lugar(out pie, out techoY)) return null;
        ultimoPie = pie;

        var go = Instantiate(prefab, pie, Quaternion.identity);
        enemigos.Add(go);
        caidos++;

        var guerrero = go.GetComponent<WarriorEnemy>();
        Entrar(go, guerrero, pie, techoY);
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
            guerrero.Amenaza = swingSwords;
            guerrero.InmuneAPiedras = !rocksStun;
            // cada uno con su distancia, para que la fila no quede pareja como una regla
            if (separation > 0f) guerrero.MantenerDistancia(separation * Random.Range(0.85f, 1.15f));
            if (hopHeight > 0f) guerrero.Saltarin(hopHeight, hopEvery, hopMinDistance);
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
        cuentaAbajo = 0f;
        esperarHasta = 0f;
        BloquearControl(false);
        if (cam != null) cam.SoltarFoco();
        foreach (var e in enemigos) if (e != null) Destroy(e);   // los de la emboscada no vuelven: llegan de nuevo
        enemigos.Clear();
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
