using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ROCA GRANDE del jefe. Antes de cansarse el jefe tira tres en arco: si le pegan a Obby le sacan
/// una vida y al caer se hacen pedazos, MENOS UNA (al azar) que queda CLAVADA en el piso.
/// Esa la saca Obby parandose al lado y apretando E varias veces; ahi la lleva arriba de la cabeza
/// y la tira con el boton de ataque. Si le pega al jefe mientras esta cansado (parpadeando en
/// blanco) le saca una vida; si no, le rebota. La que tira Obby se rompe al caer (un solo tiro).
///
/// La que tira el jefe al empezar la pelea es un TAPON: vuela derecho a la puerta por la que entro
/// Obby (sin chocar con nada en el camino) y queda solida, bloqueandola. Esa no se puede agarrar.
///
/// No se pone a mano: la crea JefeCaballero.
/// </summary>
public class RocaGrande : MonoBehaviour
{
    enum Estado { Volando, EnElPiso, Cargada, Tapon }

    const float Gravedad = 25f;
    const int MaximoEnElPiso = 4;      // si hay mas clavadas, la mas vieja se rompe
    const int TironesParaSacar = 5;    // veces que hay que apretar E para sacarla del piso

    static readonly List<RocaGrande> enElPiso = new();

    /// <summary>Hay alguna clavada en el piso para que Obby saque.</summary>
    public static bool HayEnElPiso
    {
        get { foreach (var r in enElPiso) if (r != null) return true; return false; }
    }

    /// <summary>El jefe que la tiro (para el POW cuando le pega a Obby).</summary>
    public JefeCaballero Duenio { get; set; }
    bool yaPegoAObby;

    /// <summary>Ya cayo y quedo quieta (clavada o tapando la puerta).</summary>
    public bool Asentada => estado == Estado.Tapon || estado == Estado.EnElPiso;

    Estado estado;
    bool delJefe;         // la tiro el jefe (lastima a Obby) o la tiro Obby (lastima al jefe)
    bool esTapon;          // la de la puerta: al caer queda solida
    bool quedaClavada;     // de las del jefe, la unica que no se rompe al caer
    Vector2 vel;
    float radio;
    float sinAgarrarHasta;
    LayerMask piso;
    SpriteRenderer sr;
    Rigidbody2D rb;
    CircleCollider2D circulo;
    RockThrower quienLaLleva;
    Collider2D cuerpoObby;
    readonly HashSet<JefeCaballero> yaGolpeados = new();

    // tapon: vuela derecho a la puerta
    Vector2 destino;
    float vuelo, duracion;

    // clavada: Obby la saca a tirones
    RockThrower obbyCerca;
    float obbyCercaHasta;
    int tirones;
    float sacudida;
    Vector3 asentada;
    SpriteRenderer tecla;

    // en las manos de Obby: mas chica, y la tira rapido
    float tamano;
    float fraccionEnMano = 0.7f;   // en las manos: esta parte del tamano que tenia
    float levantadaEn;             // cuando la saco del piso (sube hasta las manos en LevantarEn seg)
    Vector3 desdeElPiso;
    const float LevantarEn = 0.3f;
    const float ManosArriba = 0.34f;   // alto de las manos levantadas de Obby sobre su centro
    int ordenOriginal;
    float velocidadTiro = 20f;
    Vector3 escalaEnMano;

    /// <summary>En las manos de Obby: que parte de su tamano conserva (0.7 = un poco mas chica) y
    /// velocidad con la que la tira.</summary>
    public void ParaObby(float fraccionEnLasManos, float velocidad)
    {
        fraccionEnMano = Mathf.Clamp(fraccionEnLasManos, 0.1f, 2f);
        velocidadTiro = Mathf.Max(1f, velocidad);
    }

    // ---------------- crear ----------------

    /// <summary>La tira el jefe desde 'desde' para que caiga en 'hasta' en 'tiempo' segundos.</summary>
    public static RocaGrande TirarDelJefe(Sprite sprite, float tamano, Vector2 desde, Vector2 hasta, float tiempo,
                                          LayerMask piso, int orden, bool tapon, bool clavada)
    {
        var r = Crear(sprite, tamano, desde, piso, orden);
        float T = Mathf.Max(0.3f, tiempo);
        if (tapon)
        {
            // apoyada en el piso de la puerta (si hay piso abajo del punto)
            RaycastHit2D h = Physics2D.Raycast(hasta + Vector2.up * 0.5f, Vector2.down, 6f, piso);
            r.destino = h.collider != null ? h.point + Vector2.up * r.radio : hasta;
            r.duracion = T;
            hasta = r.destino;
        }
        r.vel = new Vector2((hasta.x - desde.x) / T, (hasta.y - desde.y) / T + 0.5f * Gravedad * T);
        r.delJefe = true;
        r.esTapon = tapon;
        r.quedaClavada = clavada;
        return r;
    }

    static RocaGrande Crear(Sprite sprite, float tamano, Vector2 desde, LayerMask piso, int orden)
    {
        var go = new GameObject("Roca grande");
        go.transform.position = desde;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = orden;
        float lado = sprite != null ? Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y) : 1f;
        go.transform.localScale = Vector3.one * (tamano / Mathf.Max(0.0001f, lado));

        // dinamico sin gravedad y con collider trigger: asi detecta al jefe y a Obby (que son
        // kinematicos o dinamicos) pero no empuja nada. La gravedad la pone este script.
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        var c = go.AddComponent<CircleCollider2D>();
        c.isTrigger = true;
        c.radius = lado * 0.42f;

        var r = go.AddComponent<RocaGrande>();
        r.sr = sr; r.rb = rb; r.circulo = c;
        r.piso = piso;
        r.radio = tamano * 0.42f;
        r.tamano = tamano;
        r.estado = Estado.Volando;
        return r;
    }

    // ---------------- volar ----------------

    void FixedUpdate()
    {
        if (estado != Estado.Volando) return;
        float dt = Time.fixedDeltaTime;
        vel.y -= Gravedad * dt;
        Vector2 p = rb.position;
        Vector2 paso = vel * dt;

        if (esTapon)
        {
            // el tapon no choca con nada en el camino: llega justo a la puerta
            vuelo += dt;
            if (vuelo >= duracion) { Apoyar(destino - Vector2.up * radio); return; }
        }
        else
        {
            // choca contra el piso (cayendo) o una pared
            RaycastHit2D h = Physics2D.Raycast(p, paso.normalized, paso.magnitude + radio, piso);
            if (h.collider != null && !h.collider.isTrigger && h.distance > 0f)
            {
                if (h.normal.y > 0.5f && vel.y <= 0f) { Apoyar(h.point); return; }   // cayo al piso
                if (Mathf.Abs(h.normal.x) > 0.5f) vel.x = -vel.x * 0.25f;            // pared: rebota un poco
            }
        }
        rb.MovePosition(p + paso);
        // gira apenas en el aire (como mucho un cuarto de vuelta por segundo). Con el rigidbody:
        // tocando el transform se perdia el movimiento.
        rb.MoveRotation(rb.rotation - Mathf.Clamp(vel.x * 6f, -90f, 90f) * dt);
        if (p.y < -60f) Destroy(gameObject);
    }

    void Apoyar(Vector2 suelo)
    {
        vel = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        transform.rotation = Quaternion.identity;
        transform.position = suelo + Vector2.up * radio;
        Sonidos.PlayEn(null, "roca_grande_cae", transform.position, Sonidos.EnPantalla);
        EnemigoFX.Polvo(sr.bounds, sr, 4);

        if (esTapon)
        {
            // queda solida, tapando la puerta
            estado = Estado.Tapon;
            circulo.isTrigger = false;
            int capa = LayerMask.NameToLayer("Grounded");
            if (capa >= 0) gameObject.layer = capa;
            return;
        }

        // las demas se hacen pedazos (y la que tiro Obby tambien: era su tiro)
        if (!quedaClavada) { Romper(); return; }

        // la elegida queda clavada: medio enterrada y un poco torcida
        estado = Estado.EnElPiso;
        transform.position = suelo + Vector2.up * (radio * 0.45f);
        transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-14f, 14f));
        asentada = transform.position;
        tirones = 0;
        enElPiso.Add(this);
        // demasiadas clavadas: la mas vieja se rompe en pixeles
        while (enElPiso.Count > MaximoEnElPiso)
        {
            var vieja = enElPiso[0];
            enElPiso.RemoveAt(0);
            if (vieja != null) vieja.Romper();
        }
    }

    // ---------------- tocar cosas ----------------

    void OnTriggerEnter2D(Collider2D other) { Tocar(other); }
    void OnTriggerStay2D(Collider2D other)  { Tocar(other); }

    void Tocar(Collider2D other)
    {
        if (estado == Estado.Volando)
        {
            if (delJefe)
            {
                if (esTapon) return;   // la de la puerta es parte de la cinematica: no lastima
                // la del jefe lastima a Obby
                var obby = other.GetComponentInParent<PlayerRespawn>();
                if (obby != null && !yaPegoAObby)
                {
                    yaPegoAObby = true;   // una sola vez por roca (el trigger avisa en cada cuadro)
                    obby.Hurt(transform.position);
                    if (Duenio != null) Duenio.Texto("POW");
                }
            }
            else
            {
                // la de Obby golpea al jefe (cansado: le saca una vida; si no, le rebota)
                var jefe = other.GetComponentInParent<JefeCaballero>();
                if (jefe != null && !yaGolpeados.Contains(jefe))
                {
                    yaGolpeados.Add(jefe);
                    jefe.RecibirRoca(transform.position);
                    vel = new Vector2(-vel.x * 0.3f, 5f);   // rebota para atras y cae
                }
            }
            return;
        }

        // clavada: Obby esta al lado (la saca con E)
        if (estado == Estado.EnElPiso)
        {
            var tirador = other.GetComponentInParent<RockThrower>();
            if (tirador != null)
            {
                obbyCerca = tirador; obbyCercaHasta = Time.time + 0.15f;
                PonerAtrasDe(tirador);   // Obby se ve adelante de la roca (si no, quedaba tapado al sacarla)
            }
        }
    }

    // Mientras Obby tironea se queda quieto (si no, se iba moviendo y la soltaba). Si deja de
    // apretar E un ratito, vuelve a poder moverse.
    const float QuietoTrasTiron = 0.6f;
    PlayerController2D obbyQuieto;
    float quietoHasta;

    void FrenarObby(RockThrower quien)
    {
        var ctrl = quien != null ? quien.GetComponent<PlayerController2D>() : null;
        if (ctrl == null) return;
        if (obbyQuieto == null && ctrl.ControlBloqueado) return;   // ya lo tiene frenado otra cosa (cinematica)
        obbyQuieto = ctrl;
        obbyQuieto.ControlBloqueado = true;
        quietoHasta = Time.time + QuietoTrasTiron;
        // mira a la roca y hace fuerza con la animacion de levantar
        obbyQuieto.MirarHacia(transform.position.x >= obbyQuieto.transform.position.x ? 1 : -1);
        animTironea = quien.Animador;
        if (animTironea != null) animTironea.Pulling = true;
    }
    PlayerAnimator animTironea;

    bool SeTocan(RockThrower quien)
    {
        var c = quien.GetComponent<Collider2D>();
        if (c == null) return false;
        Bounds b = circulo.bounds;
        b.Expand(0.3f);
        return b.Intersects(c.bounds);
    }

    void SoltarObby()
    {
        if (animTironea != null) { animTironea.Pulling = false; animTironea = null; }
        if (obbyQuieto == null) return;
        obbyQuieto.ControlBloqueado = false;
        obbyQuieto = null;
    }

    void Update()
    {
        if (obbyQuieto != null && (Time.time > quietoHasta || estado != Estado.EnElPiso)) SoltarObby();

        // al lado: lo avisa el trigger; y si esta quieto tironeando (su cuerpo puede "dormirse" y
        // el trigger deja de avisar), alcanza con que se toquen
        bool tocando = obbyCerca != null && (Time.time < obbyCercaHasta || (obbyQuieto != null && SeTocan(obbyCerca)));
        bool obbyAca = estado == Estado.EnElPiso && tocando && !obbyCerca.LlevaRocaGrande;
        MostrarTecla(obbyAca);
        if (!obbyAca || Time.time < sinAgarrarHasta) return;
        if (ApretoE()) Tiron();
    }

    static bool ApretoE()
    {
        return (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);
    }

    // Un tiron: se sacude, sale un poco del piso y larga polvo. Al ultimo, Obby la levanta.
    void Tiron()
    {
        tirones++;
        sacudida = 0.14f;
        FrenarObby(obbyCerca);
        Sonidos.Play(null, "obby_fuerza");   // Obby haciendo fuerza
        asentada += Vector3.up * (radio * 0.55f / TironesParaSacar);
        EnemigoFX.Polvo(sr.bounds, sr, 2);
        if (tirones < TironesParaSacar)
        {
            Sonidos.PlayEn(null, "roca_tiron", transform.position, Sonidos.EnPantalla);
            return;
        }
        if (!obbyCerca.AgarrarRocaGrande(this)) return;
        SoltarObby();   // ya la saco: se puede mover con ella
        estado = Estado.Cargada;
        enElPiso.Remove(this);
        quienLaLleva = obbyCerca;
        cuerpoObby = obbyCerca.GetComponent<Collider2D>();
        circulo.enabled = false;
        MostrarTecla(false);
        PonerAtrasDe(obbyCerca);   // las manos de Obby por delante de la roca
        Sonidos.Play(null, "roca_grande_agarra");
        // sube del piso a las manos y se achica un poco
        levantadaEn = Time.time;
        desdeElPiso = transform.position;
        escalaEnMano = transform.localScale * fraccionEnMano;
        tamano *= fraccionEnMano;
        radio = tamano * 0.42f;
    }

    // La roca se dibuja justo atras de Obby.
    void PonerAtrasDe(RockThrower quien)
    {
        var dibujo = quien.DibujoDeObby;
        if (dibujo == null || sr.sortingLayerID != dibujo.sortingLayerID || sr.sortingOrder < dibujo.sortingOrder) return;
        ordenOriginal = sr.sortingOrder;
        sr.sortingOrder = dibujo.sortingOrder - 1;
    }

    void LateUpdate()
    {
        // clavada: tiembla un toque con cada tiron
        if (estado == Estado.EnElPiso)
        {
            Vector3 temblor = Vector3.zero;
            if (sacudida > 0f)
            {
                sacudida -= Time.deltaTime;
                temblor = new Vector3(Random.Range(-1f, 1f), Random.Range(-0.5f, 0.5f), 0f) * 0.08f;
            }
            transform.position = asentada + temblor;
        }

        // la tecla E flotando arriba de la roca
        if (tecla != null && tecla.enabled)
        {
            float arriba = sr.bounds.max.y + 0.45f + Mathf.Sin(Time.time * 6f) * 0.08f;
            tecla.transform.position = new Vector3(transform.position.x, arriba, transform.position.z);
            tecla.transform.localScale = Vector3.one * (sacudida > 0f ? 0.85f : 1f);   // se "aprieta"
        }

        // cargada: va arriba de la cabeza de Obby
        if (estado != Estado.Cargada || quienLaLleva == null) return;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.MoveTowards(transform.localScale, escalaEnMano, 8f * Time.deltaTime * Mathf.Max(1f, transform.localScale.x));
        // apoyada en las manos levantadas (en el dibujo de Obby estan 0.34 arriba de su centro)
        Vector3 obby = quienLaLleva.transform.position;
        float medioAlto = sr.bounds.extents.y;
        Vector3 manos = new Vector3(obby.x, obby.y + ManosArriba + medioAlto, transform.position.z);
        float k = Mathf.Clamp01((Time.time - levantadaEn) / LevantarEn);   // recien sacada: sube desde el piso
        transform.position = Vector3.Lerp(desdeElPiso, manos, k * k * (3f - 2f * k));
    }

    /// <summary>El tapon cae ya mismo en la puerta (al saltear la cinematica).</summary>
    public void AterrizarYa()
    {
        if (estado != Estado.Volando || !esTapon) return;
        Apoyar(destino - Vector2.up * radio);
    }

    /// <summary>Obby la tira hacia donde mira (lo llama RockThrower con el boton de ataque).</summary>
    public void TirarDeObby(int direccion)
    {
        if (estado != Estado.Cargada) return;
        estado = Estado.Volando;
        delJefe = false;
        quedaClavada = false;   // la de Obby se rompe al caer: un solo tiro
        yaGolpeados.Clear();
        quienLaLleva = null;
        circulo.enabled = true;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        transform.localScale = escalaEnMano;   // por si la tira antes de terminar de achicarse
        if (ordenOriginal != 0) sr.sortingOrder = ordenOriginal;   // vuela por delante de todo otra vez
        vel = new Vector2(direccion * velocidadTiro, 3.5f);
        sinAgarrarHasta = Time.time + 0.5f;
        Sonidos.Play(null, "roca_grande_tira");
    }

    /// <summary>Se rompe en pixeles.</summary>
    public void Romper()
    {
        if (quienLaLleva != null) quienLaLleva.SoltarRocaGrande(this);
        enElPiso.Remove(this);
        Sonidos.PlayEn(null, "roca_rompe", transform.position, Sonidos.EnPantalla);
        if (sr != null && sr.enabled) EnemigoFX.Pixelar(sr, null, 0.8f);
        Destroy(gameObject);
    }

    /// <summary>Rompe todas las que quedaron por el mapa (menos el tapon de la puerta).</summary>
    public static void RomperTodas()
    {
        foreach (var r in FindObjectsByType<RocaGrande>(FindObjectsSortMode.None))
            if (r != null && r.estado != Estado.Tapon) r.Romper();
    }

    void OnDestroy()
    {
        SoltarObby();
        enElPiso.Remove(this);
        if (tecla != null) Destroy(tecla.gameObject);
    }

    // ---------------- la tecla E ----------------

    void MostrarTecla(bool ver)
    {
        if (!ver) { if (tecla != null) tecla.enabled = false; return; }
        if (tecla == null)
        {
            var go = new GameObject("Tecla E");
            tecla = go.AddComponent<SpriteRenderer>();
            tecla.sprite = SpriteTecla();
            tecla.sortingOrder = sr.sortingOrder + 5;
        }
        tecla.enabled = true;
    }

    static Sprite s_tecla;

    // Tecla de 11x11 pixeles dibujada por codigo: borde blanco, fondo oscuro y una E.
    static Sprite SpriteTecla()
    {
        if (s_tecla != null) return s_tecla;
        string[] dibujo =
        {
            ".#########.",
            "#.........#",
            "#..#####..#",
            "#..#......#",
            "#..#......#",
            "#..####...#",
            "#..#......#",
            "#..#......#",
            "#..#####..#",
            "#.........#",
            ".#########.",
        };
        int n = dibujo.Length;
        var tex = new Texture2D(n, n) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Color blanco = Color.white, fondo = new Color(0.12f, 0.12f, 0.16f, 0.9f), nada = new Color(0, 0, 0, 0);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                char c = dibujo[n - 1 - y][x];
                bool esquina = (x == 0 || x == n - 1) && (y == 0 || y == n - 1);
                tex.SetPixel(x, y, c == '#' ? blanco : esquina ? nada : fondo);
            }
        tex.Apply();
        s_tecla = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 16f);
        return s_tecla;
    }
}
