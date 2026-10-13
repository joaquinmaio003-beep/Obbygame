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
public class Emboscada : MonoBehaviour
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
