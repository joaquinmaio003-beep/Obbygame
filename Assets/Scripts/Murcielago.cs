using UnityEngine;

/// <summary>
/// Murcielago de la cueva (decoracion). Se queda quieto donde lo pongas, pegado a una estructura.
/// Cuando pasa un enemigo (u Obby) cerca sale volando por los alrededores un rato y despues se va
/// a descansar a otra estructura cercana (si no encuentra, vuelve a la suya).
/// Nunca atraviesa paredes, pisos ni techos: solo vuela a lugares que se ven libres desde su lugar.
/// No lastima ni choca con nada.
///
/// Setup: arrastra el prefab Prefab/Murcielago al nivel, apoyado donde quieras que descanse
/// (el punto de apoyo esta abajo al medio, en las patitas). Se pueden poner varios juntos.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Murcielago : MonoBehaviour
{
    [Header("Dibujos")]
    [Tooltip("Quieto, con las alas cerradas (murcielago_00).")]
    public Sprite quieto;
    [Tooltip("Volando, en orden (murcielago_00 y 01).")]
    public Sprite[] vuelo;
    public float vueloFps = 12f;

    [Header("Comportamiento")]
    [Tooltip("Si un enemigo (u Obby) pasa a menos de esta distancia, sale volando.")]
    public float radioAlerta = 2.5f;
    [Tooltip("Obby tambien lo asusta, no solo los enemigos.")]
    public bool tambienObby = true;
    [Tooltip("Hasta que distancia de su lugar se aleja volando.")]
    public float radioVuelo = 2.5f;
    [Tooltip("Velocidad de vuelo.")]
    public float velocidad = 4.5f;
    [Tooltip("Segundos que vuela antes de volver (minimo y maximo, al azar).")]
    public float vueloMin = 2.5f;
    public float vueloMax = 4f;
    [Tooltip("Segundos quieto despues de volver, antes de poder asustarse otra vez.")]
    public float calma = 2f;

    [Header("Mudarse")]
    [Tooltip("Despues de volar se va a descansar a OTRA estructura cercana (si no encuentra, vuelve a la suya).")]
    public bool cambiaDeLugar = true;
    [Tooltip("Hasta que distancia busca otro lugar donde pararse.")]
    public float radioMudanza = 6f;
    [Tooltip("El lugar nuevo tiene que estar al menos a esta distancia del anterior.")]
    public float distanciaMinima = 2f;

    float cuerpo = 0.12f;        // radio del cuerpo para no meterse en las paredes (sale del dibujo)
    float alturaCentro = 0.22f;  // del punto de apoyo (patitas) al centro del cuerpo

    enum Estado { Quieto, Volando, Volviendo }
    Estado estado = Estado.Quieto;
    SpriteRenderer sr;
    Vector2 casa, objetivo, puntoSeguro, destino;
    bool hayObjetivo;
    float finVuelo, proximoChequeo, calmaHasta, animT;
    // grande: cerca de las paredes hay muchos colliders del piso y antes llenaban la lista
    // antes de llegar al enemigo (por eso no se asustaba)
    static readonly Collider2D[] s_cerca = new Collider2D[64];
    static readonly RaycastHit2D[] s_hits = new RaycastHit2D[8];

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        casa = transform.position;
        if (quieto != null) sr.sprite = quieto;

        // tamano del cuerpo segun el dibujo (asi anda a cualquier escala)
        Bounds b = sr.bounds;
        alturaCentro = b.center.y - transform.position.y;
        cuerpo = Mathf.Max(0.05f, Mathf.Min(b.extents.x, b.extents.y) * 0.7f);
    }

    void Update()
    {
        if (estado == Estado.Quieto) { Quieto(); return; }

        // aletea
        animT += Time.deltaTime;
        if (vuelo != null && vuelo.Length > 0) sr.sprite = vuelo[(int)(animT * vueloFps) % vuelo.Length];

        if (estado == Estado.Volando && Time.time >= finVuelo)
        {
            // a descansar: a otra estructura si encuentra una, si no a la suya
            estado = Estado.Volviendo;
            destino = cambiaDeLugar && BuscarPercha(out Vector2 nueva) ? nueva : casa;
            objetivo = destino; hayObjetivo = true;
        }

        if (!hayObjetivo) ElegirObjetivo();
        Volar();
    }

    // ---- quieto: mira cada tanto si pasa un enemigo ----
    void Quieto()
    {
        if (Time.time < proximoChequeo || Time.time < calmaHasta) return;
        proximoChequeo = Time.time + 0.1f;

        var filtro = new ContactFilter2D(); filtro.NoFilter();   // los enemigos tienen el collider en trigger
        int n = Physics2D.OverlapCircle(casa, radioAlerta, filtro, s_cerca);
        for (int i = 0; i < n; i++)
        {
            var c = s_cerca[i];
            if (c == null) continue;
            bool asusta = c.GetComponentInParent<IStunnable>() != null
                       || (tambienObby && c.GetComponentInParent<PlayerRespawn>() != null);
            if (!asusta) continue;
            SalirVolando();
            return;
        }
    }

    void SalirVolando()
    {
        estado = Estado.Volando;
        finVuelo = Time.time + Random.Range(vueloMin, vueloMax);
        puntoSeguro = casa;
        hayObjetivo = false;
        animT = Random.value;   // si hay varios juntos, no aletean igual
    }

    // ---- volando ----
    void Volar()
    {
        Vector2 pos = transform.position;
        Vector2 hacia = objetivo - pos;
        float dist = hacia.magnitude;
        float paso = velocidad * Time.deltaTime;

        if (dist <= paso)
        {
            transform.position = new Vector3(objetivo.x, objetivo.y, transform.position.z);
            puntoSeguro = objetivo;   // desde aca se ve su lugar: siempre puede volver
            if (estado == Estado.Volviendo)
            {
                if (objetivo == destino) { casa = destino; Aterrizar(); }   // su lugar nuevo
                else objetivo = destino;   // volvia por un desvio: ahora derecho
            }
            else hayObjetivo = false;   // llego: elige otro lugar
            return;
        }

        Vector2 dir = hacia / dist;
        if (Bloqueado(pos, dir, paso + 0.05f))
        {
            // algo en el camino: volando busca otro lugar; volviendo, vuelve a su lugar de antes
            // pasando primero por un punto seguro (desde ahi se ve)
            if (estado == Estado.Volviendo)
            {
                destino = casa;
                objetivo = puntoSeguro != objetivo ? puntoSeguro : casa;
            }
            else hayObjetivo = false;
            return;
        }

        Vector2 nueva = pos + dir * paso;
        transform.position = new Vector3(nueva.x, nueva.y, transform.position.z);
        if (Mathf.Abs(dir.x) > 0.1f) sr.flipX = dir.x < 0f;
    }

    void Aterrizar()
    {
        estado = Estado.Quieto;
        hayObjetivo = false;
        calmaHasta = Time.time + calma;
        if (quieto != null) sr.sprite = quieto;
    }

    // Otro lugar donde pararse: arriba de un piso o una plataforma quieta, con lugar para el,
    // lejos del lugar de antes y que se llegue volando derecho sin atravesar nada.
    static readonly Collider2D[] s_dentro = new Collider2D[8];
    bool BuscarPercha(out Vector2 percha)
    {
        percha = default;
        Vector2 pos = transform.position;
        var filtro = new ContactFilter2D { useTriggers = false };
        for (int intento = 0; intento < 24; intento++)
        {
            Vector2 p = pos + Random.insideUnitCircle * radioMudanza;
            if (Solido(Physics2D.OverlapPoint(p, filtro, s_dentro))) continue;   // adentro de una pared

            // el piso de abajo
            int n = Physics2D.Raycast(p, Vector2.down, filtro, s_hits, 5f);
            RaycastHit2D piso = default; bool hay = false;
            for (int i = 0; i < n; i++)
            {
                var h = s_hits[i];
                if (h.collider == null || h.distance <= 0f || !EsFijo(h.collider)) continue;
                piso = h; hay = true; break;
            }
            if (!hay || piso.normal.y < 0.7f) continue;   // nada abajo, o es una pared inclinada

            Vector2 lugar = piso.point;
            if ((lugar - casa).sqrMagnitude < distanciaMinima * distanciaMinima) continue;
            // lugar para el cuerpo (que no quede metido debajo de otra cosa)
            Vector2 centro = lugar + Vector2.up * (alturaCentro + 0.03f);
            if (Solido(Physics2D.OverlapCircle(centro, cuerpo, filtro, s_dentro))) continue;
            if (!Libre(pos, lugar + Vector2.up * 0.03f)) continue;

            percha = lugar + Vector2.up * 0.01f;
            return true;
        }
        return false;
    }

    // Algo de lo encontrado es una estructura (no Obby ni enemigos)?
    bool Solido(int n)
    {
        for (int i = 0; i < n; i++)
            if (s_dentro[i] != null && EsFijo(s_dentro[i])) return true;
        return false;
    }

    // Estructura que no se mueve: sin Rigidbody o con uno estatico (no Obby, enemigos, rocas ni taladros)
    static bool EsFijo(Collider2D c)
    {
        var rb = c.attachedRigidbody;
        return rb == null || rb.bodyType == RigidbodyType2D.Static;
    }

    // Un punto al azar alrededor de su lugar que se vea libre desde su lugar Y desde donde esta.
    void ElegirObjetivo()
    {
        Vector2 pos = transform.position;
        for (int intento = 0; intento < 12; intento++)
        {
            Vector2 p = casa + Random.insideUnitCircle * radioVuelo;
            if (p.y < casa.y - radioVuelo * 0.3f) p.y = casa.y - radioVuelo * 0.3f;   // no se tira para abajo de mas
            if (Libre(casa, p) && Libre(pos, p)) { objetivo = p; hayObjetivo = true; return; }
        }
        // no encontro: vuelve
        objetivo = puntoSeguro; hayObjetivo = true;
    }

    bool Libre(Vector2 desde, Vector2 hasta)
    {
        Vector2 d = hasta - desde;
        float largo = d.magnitude;
        if (largo < 0.01f) return true;
        return !Bloqueado(desde, d / largo, largo);
    }

    // Hay una pared, piso o techo en el camino? (Obby, los enemigos y los triggers no cuentan)
    bool Bloqueado(Vector2 desde, Vector2 dir, float largo)
    {
        // se mide desde el centro del cuerpo (el punto de apoyo esta en las patitas, tocando el piso)
        var filtro = new ContactFilter2D { useTriggers = false };
        int n = Physics2D.CircleCast(desde + Vector2.up * alturaCentro, cuerpo, dir, filtro, s_hits, largo);
        for (int i = 0; i < n; i++)
        {
            var c = s_hits[i].collider;
            if (c == null) continue;
            if (s_hits[i].distance <= 0f) continue;   // ya estaba tocandolo al arrancar
            if (c.GetComponentInParent<PlayerRespawn>() != null) continue;
            if (c.GetComponentInParent<IStunnable>() != null) continue;
            return true;
        }
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 c = Application.isPlaying ? (Vector3)casa : transform.position;
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
        Gizmos.DrawWireSphere(c, radioAlerta);
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.6f);
        Gizmos.DrawWireSphere(c, radioVuelo);
        if (cambiaDeLugar)
        {
            Gizmos.color = new Color(0.6f, 1f, 0.5f, 0.35f);
            Gizmos.DrawWireSphere(c, radioMudanza);
        }
    }
}
