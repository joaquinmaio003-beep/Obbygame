using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// PERSECUCION ENTRE LOS ARBOLES. Cuando Obby entra a esta zona aparece el enemigo de las sierras
/// y sale corriendo por el piso, cortando uno tras otro los arboles sobre los que Obby va saltando:
/// hay que ir de copa en copa sin parar, porque el arbol donde estas parado se viene abajo.
///
/// - Arranca con una CINEMATICA: Obby queda quieto, la camara se va a mostrar la sierra saliendo
///   de su escondite (sube de a poco y avanza un poquito) y vuelve a Obby para que corra.
/// - Si Obby SE CAE al piso en ese tramo, la sierra se le viene encima y lo mata: muere y el
///   nivel ENTERO arranca de nuevo. Lo mismo si la sierra lo agarra de cualquier otra forma.
/// - Al cortar el ULTIMO arbol la sierra se sobrecarga y EXPLOTA.
/// - Si se cae a un pozo y vuelve al checkpoint, la zona se rearma (arboles, sierra y cinematica).
///
/// Setup:
/// 1. Este objeto: un Collider2D en Is Trigger donde Obby cae sobre el primer arbol (la zona que
///    dispara la escena) + este script.
/// 2. Sierra: un enemigo de las sierras de la escena, puesto EN SU ESCONDITE (atras del primer
///    arbol). El script lo apaga al empezar el nivel y lo prende ahi mismo cuando arranca. Si lo
///    dejas un poco hundido en el piso, en la cinematica se lo ve salir para arriba.
/// 3. End Point: un objeto vacio donde termina el tramo (marca hacia donde corre y hasta donde
///    cuenta caerse).
/// 4. Los arboles: el prefab Prefab/ArbolCortable (el arbol comun). Los encuentra solo: todos
///    los que esten entre la sierra y el final. Los arboles de manzanas NO se cortan.
///
/// Un piedrazo en la cabeza de la sierra la aturde igual que siempre: sirve para ganar tiempo.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PersecucionSierra : MonoBehaviour
{
    [Header("Quien persigue")]
    [Tooltip("El enemigo de las sierras de la escena, puesto en su escondite: sale de ahi.")]
    public EnemigoSierra sierra;
    [Tooltip("Opcional: si lo asignas, aparece ahi (apoyada en el piso) en vez de donde esta puesta.")]
    public Transform spawnPoint;

    [Header("Recorrido")]
    [Tooltip("Donde termina el tramo (un objeto vacio): marca hacia donde corre y hasta donde cuenta caerse.")]
    public Transform endPoint;
    [Tooltip("Arboles a cortar, en cualquier orden. Vacio = busca solo los arboles cortables que haya entre la sierra y el final.")]
    public Destructible[] trees;

    [Header("Cinematica del comienzo")]
    [Tooltip("Segundos que la camara muestra a la sierra saliendo (Obby queda quieto mientras tanto).")]
    public float introTime = 2f;
    [Tooltip("Cuanto avanza la sierra al salir de su escondite (unidades). Despues se queda ahi hasta que termina la cinematica.")]
    public float introAdvance = 1f;
    [Tooltip("Velocidad a la que avanza ese tramito.")]
    public float introSpeed = 1.5f;
    [Tooltip("Si esta escondida mas abajo que el piso: a que velocidad sube al salir (unidades por segundo). Bajo = sale despacio.")]
    public float emergeSpeed = 1.2f;
    [Tooltip("Segundos para que la camara vuelva a Obby antes de devolverle el control.")]
    public float cameraReturnTime = 0.5f;

    [Header("Final")]
    [Tooltip("Al cortar el ultimo arbol se sobrecarga: segundos que tiembla y titila antes de explotar.")]
    public float overloadTime = 0.9f;
    [Tooltip("Sonido de la explosion (opcional).")]
    public AudioClip explodeSound;

    [Header("Ritmo")]
    [Tooltip("Velocidad a la que corre. Obby camina a 9; saltando de arbol en arbol avanza bastante menos.")]
    public float speed = 4f;
    [Tooltip("Si Obby le saca mas ventaja que esta (en unidades), acelera para no quedar lejos.")]
    public float catchUpDistance = 7f;
    [Tooltip("Velocidad cuando acelera para alcanzarte.")]
    public float catchUpSpeed = 7.5f;

    [Header("Si Obby se cae")]
    [Tooltip("Obby cuenta como caido si pisa algo mas abajo que la copa del arbol mas bajo, por mas de esta distancia.")]
    public float fallMargin = 1f;
    [Tooltip("Velocidad con la que la sierra se le viene encima cuando se cae.")]
    public float catchSpeed = 18f;

    [Header("Corte de los arboles")]
    [Tooltip("A que distancia del arbol empieza a cortarlo.")]
    public float cutDistance = 0.8f;
    [Tooltip("Segundos que el arbol titila antes de caerse (el aviso para saltar al siguiente).")]
    public float cutTime = 0.35f;
    [Tooltip("Color con el que titila el arbol que esta por caerse.")]
    public Color cutWarnColor = new Color(1f, 0.4f, 0.3f, 1f);

    [Header("Efectos (opcional)")]
    [Tooltip("Sonido cuando aparece la sierra.")]
    public AudioClip appearSound;
    [Tooltip("Sonido cuando se cae un arbol.")]
    public AudioClip treeFallSound;
    [Tooltip("La camara tiembla cuando aparece y con cada arbol que cae.")]
    public bool cameraShake = true;
    [Tooltip("Algo extra que quieras que pase cuando arranca.")]
    public UnityEvent onStart;
    [Tooltip("Algo extra que quieras que pase cuando termina.")]
    public UnityEvent onEnd;

    static readonly List<PersecucionSierra> todas = new();

    readonly List<Destructible> arboles = new();
    readonly List<SpriteRenderer> titilando = new();   // arboles avisando (para devolverles el color)
    readonly List<Color> colorTitilando = new();
    PlayerController2D obby;
    Collider2D obbyCol;
    CameraFollow2D cam;
    int dir = 1;            // hacia donde corre
    int siguiente;          // proximo arbol a cortar
    bool disparada;         // ya arranco (no vuelve a dispararse hasta rearmarse)
    bool corriendo;
    bool atrapando;         // Obby se cayo: la sierra se le viene encima
    float tiempoAtrapando;
    bool bloqueeControl;    // este script dejo quieto a Obby (para devolverle el control pase lo que pase)
    float tramoIni;         // donde aparecio la sierra: ahi empieza el tramo
    float copaMasBaja;      // altura de la copa mas baja (NaN si no se pudo medir)

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Awake() { todas.Add(this); }
    void OnDestroy() { todas.Remove(this); }

    void Start()
    {
        if (Camera.main != null) cam = Camera.main.GetComponent<CameraFollow2D>();
        if (sierra == null)
        {
            Debug.LogWarning("PersecucionSierra: falta asignar la Sierra.", this);
            return;
        }
        sierra.gameObject.SetActive(false);   // escondida hasta que arranque
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (disparada || sierra == null) return;
        var pc = other.GetComponentInParent<PlayerController2D>();
        if (pc == null) return;

        obby = pc;
        obbyCol = pc.GetComponent<Collider2D>();
        disparada = true;
        StartCoroutine(Empezar());
    }

    IEnumerator Empezar()
    {
        // hacia donde corre: hacia el final; si no hay, hacia donde mira Obby
        dir = endPoint != null ? (endPoint.position.x >= transform.position.x ? 1 : -1) : obby.Facing;
        BuscarArboles();
        siguiente = 0;

        // aparece la sierra en su escondite (donde la pusiste), o en el Spawn Point si hay
        sierra.VolverAlInicio();
        if (spawnPoint != null && !sierra.ColocarEn(spawnPoint.position.x, spawnPoint.position.y))
            Debug.LogWarning("PersecucionSierra: debajo del Spawn Point no hay piso.", this);
        tramoIni = sierra.CuerpoX;
        sierra.AlAtrapar = Matar;   // si agarra a Obby, lo mata (no le saca una vida y sigue)
        if (appearSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(appearSound);
        if (cameraShake && cam != null) cam.Shake(0.35f, 0.2f);
        onStart?.Invoke();

        // CINEMATICA: Obby quieto y la camara va a mostrar la sierra SALIENDO de su escondite:
        // sube de a poco (si estaba mas abajo que el piso), avanza un poquito y se queda ahi.
        BloquearControl(true);
        sierra.SubidaSuave = emergeSpeed;
        if (cam != null) cam.Enfocar(sierra.transform);
        float desde = sierra.CuerpoX;
        for (float t = 0f; t < introTime; t += Time.deltaTime)
        {
            bool yaSalio = Mathf.Abs(sierra.CuerpoX - desde) >= introAdvance;
            sierra.Embestir(dir, yaSalio ? 0f : introSpeed);
            yield return null;
        }

        // la camara vuelve a Obby y arranca la persecucion
        if (cam != null) cam.SoltarFoco();
        yield return new WaitForSeconds(cameraReturnTime);
        sierra.SubidaSuave = 0f;   // corriendo sigue el piso al instante (cuestas)
        BloquearControl(false);

        corriendo = true;
    }

    void Update()
    {
        if (!corriendo) return;

        // la mataron o se cayo a un pozo: se termino la persecucion
        if (sierra == null || sierra.Muerto || !sierra.gameObject.activeInHierarchy) { Terminar(); return; }

        float x = sierra.CuerpoX;

        // Obby se cayo: la sierra se le viene encima (al tocarlo, lo mata)
        if (atrapando)
        {
            sierra.Embestir(obby.transform.position.x >= x ? 1 : -1, catchSpeed);
            tiempoAtrapando += Time.deltaTime;
            if (tiempoAtrapando > 1.5f) Matar();   // por si no llega (aturdida, algo en el medio)
            return;
        }
        if (ObbySeCayo(x))
        {
            atrapando = true;
            tiempoAtrapando = 0f;
            BloquearControl(true);   // quieto: ya no se salva
            return;
        }

        // ritmo: si Obby se escapo lejos, acelera
        float ventaja = (obby.transform.position.x - x) * dir;
        sierra.Embestir(dir, ventaja > catchUpDistance ? catchUpSpeed : speed);

        // corta los arboles que va alcanzando
        while (siguiente < arboles.Count)
        {
            var arbol = arboles[siguiente];
            if (arbol == null || !arbol.gameObject.activeInHierarchy) { siguiente++; continue; }
            if ((XDe(arbol) - x) * dir > cutDistance) break;   // todavia no llego
            StartCoroutine(Cortar(arbol));
            siguiente++;
        }

        // corto el ultimo arbol (o se paso del final del tramo): se sobrecarga y explota
        bool sinArboles = siguiente >= arboles.Count && titilando.Count == 0;
        bool pasoElFinal = endPoint != null && (endPoint.position.x - x) * dir <= 0f;
        if (sinArboles || pasoElFinal) StartCoroutine(Explotar());
    }

    // Final: la sierra se clava, tiembla y titila cada vez mas rapido, y explota.
    IEnumerator Explotar()
    {
        corriendo = false;
        atrapando = false;
        BloquearControl(false);
        sierra.AlAtrapar = null;
        sierra.Sobrecargar(overloadTime);

        yield return new WaitForSeconds(overloadTime);
        yield return null;   // un frame mas: ya exploto

        if (explodeSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(explodeSound);
        if (cameraShake && cam != null) cam.Shake(0.45f, 0.3f);
        onEnd?.Invoke();
    }

    // Obby esta parado en el piso (bien por debajo de las copas), dentro del tramo de la persecucion.
    bool ObbySeCayo(float sierraX)
    {
        if (obby == null || !obby.IsGrounded) return false;

        float ox = obby.transform.position.x;
        if ((ox - tramoIni) * dir < -1f) return false;                                   // antes del tramo
        if (endPoint != null && (endPoint.position.x - ox) * dir < 0f) return false;     // ya paso el final

        // Se mide contra las copas, no contra la sierra: asi vale en todo el tramo, este donde
        // este la sierra y aunque el piso suba o baje.
        float pies = obbyCol != null ? obbyCol.bounds.min.y : obby.transform.position.y;
        if (float.IsNaN(copaMasBaja)) return pies < sierra.PiesY + fallMargin;
        return pies < copaMasBaja - fallMargin;
    }

    // La sierra agarro a Obby: muere y el nivel ENTERO arranca de nuevo (tenga las vidas que tenga).
    void Matar()
    {
        if (obby == null) return;
        BloquearControl(false);
        var vida = obby.GetComponent<PlayerRespawn>();
        if (vida != null) vida.Morir();

        // mientras se funde a negro: se corta todo y la sierra se queda quieta al lado
        StopAllCoroutines();
        corriendo = false;
        atrapando = false;
        if (cam != null) cam.SoltarFoco();
        if (sierra != null) { sierra.AlAtrapar = null; sierra.Embestir(dir, 0f); }
    }

    // Deja quieto a Obby (cinematica, o cuando ya se cayo) o le devuelve el control.
    void BloquearControl(bool bloquear)
    {
        if (obby == null) return;
        if (bloquear) { obby.ControlBloqueado = true; bloqueeControl = true; }
        else if (bloqueeControl) { obby.ControlBloqueado = false; bloqueeControl = false; }
    }

    // El arbol titila (aviso para saltar) y se viene abajo.
    IEnumerator Cortar(Destructible arbol)
    {
        sierra.AnimarCorte(cutTime + 0.15f);

        var sr = arbol.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color original = sr.color;
            titilando.Add(sr);
            colorTitilando.Add(original);
            for (float t = 0f; t < cutTime; t += Time.deltaTime)
            {
                sr.color = Color.Lerp(original, cutWarnColor, Mathf.PingPong(t * 14f, 1f));
                yield return null;
            }
            sr.color = original;   // antes de romperlo: los pedacitos salen con su color de verdad
            int i = titilando.IndexOf(sr);
            if (i >= 0) { titilando.RemoveAt(i); colorTitilando.RemoveAt(i); }
        }
        else yield return new WaitForSeconds(cutTime);

        arbol.Break();
        if (treeFallSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(treeFallSound);
        if (cameraShake && cam != null) cam.Shake(0.15f, 0.1f);
    }

    // Se termino antes de tiempo (a la sierra la mataron o se cayo a un pozo): se corta la persecucion.
    void Terminar()
    {
        corriendo = false;
        atrapando = false;
        BloquearControl(false);
        if (sierra != null) { sierra.AlAtrapar = null; sierra.TerminarEmbestida(); }
        onEnd?.Invoke();
    }

    // Los arboles a cortar, ordenados desde donde arranca la sierra.
    void BuscarArboles()
    {
        arboles.Clear();
        if (trees != null && trees.Length > 0)
        {
            foreach (var t in trees) if (t != null) arboles.Add(t);
        }
        else
        {
            // solos: todo lo cortable entre la sierra y el final. Ni la roca grande ni los
            // arboles de manzanas cuentan.
            float ini = transform.position.x - dir * 3f;   // desde esta zona (con un margen)
            float fin = endPoint != null ? endPoint.position.x : ini + dir * 100000f;
            foreach (var d in FindObjectsByType<Destructible>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (d.GetComponent<PushableBox>() != null) continue;
                if (d.GetComponentInParent<ArbolManzanas>() != null) continue;
                float x = XDe(d);
                if ((x - ini) * dir < 0f || (fin - x) * dir < 0f) continue;
                arboles.Add(d);
            }
        }
        int sentido = dir;
        arboles.Sort((a, b) => (XDe(a) * sentido).CompareTo(XDe(b) * sentido));

        // la copa mas baja: de ahi para abajo, Obby se cayo
        copaMasBaja = float.NaN;
        foreach (var a in arboles)
        {
            var c = a.GetComponent<Collider2D>();
            if (c == null || !c.enabled || !a.gameObject.activeInHierarchy) continue;
            float copa = c.bounds.max.y;
            if (float.IsNaN(copaMasBaja) || copa < copaMasBaja) copaMasBaja = copa;
        }
    }

    static float XDe(Destructible d) { return d.transform.position.x; }

    // ---------------- volver al checkpoint ----------------

    /// <summary>Rearma todas las persecuciones (lo llama PlayerRespawn al volver a un checkpoint).</summary>
    public static void ResetAll()
    {
        for (int i = 0; i < todas.Count; i++)
            if (todas[i] != null) todas[i].Rearmar();
    }

    void Rearmar()
    {
        StopAllCoroutines();
        for (int i = 0; i < titilando.Count; i++)
            if (titilando[i] != null) titilando[i].color = colorTitilando[i];
        titilando.Clear();
        colorTitilando.Clear();

        corriendo = false;
        atrapando = false;
        disparada = false;
        siguiente = 0;
        BloquearControl(false);
        if (cam != null) cam.SoltarFoco();
        if (sierra != null)
        {
            sierra.AlAtrapar = null;
            sierra.VolverAlInicio();              // a su lugar y sin embestir
            sierra.gameObject.SetActive(false);   // escondida hasta que Obby vuelva a entrar
        }
    }

    void OnDrawGizmos()
    {
        // linea del recorrido: de esta zona (o del Spawn Point) al final
        if (endPoint == null) return;
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
        Vector3 a = spawnPoint != null ? spawnPoint.position : transform.position;
        Gizmos.DrawLine(a, new Vector3(endPoint.position.x, a.y, 0f));
        Gizmos.DrawWireSphere(endPoint.position, 0.25f);
        if (spawnPoint != null) Gizmos.DrawWireSphere(spawnPoint.position, 0.25f);
    }
}
