using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// HUECO PARA LA ROCA ESPECIAL. Un lugar sin piso (un hueco, un pozo) donde hay que empujar UNA
/// roca en particular: cuando esa roca llega al hueco cae adentro (centrandose mientras cae) y queda
/// FIJA como plataforma, para poder cruzar. Las demas rocas empujables no encajan: pasan de largo
/// o se caen como siempre.
///
/// Setup:
/// 1. Un objeto vacio con un BoxCollider2D en Is Trigger + este script, tapando el hueco:
///    - el ANCHO de la caja = el ancho del hueco (cuando el centro de la roca entra ahi, encaja);
///    - el BORDE DE ARRIBA de la caja = hasta donde queda la parte de arriba de la roca
///      (a la altura del piso si queres que quede pareja con el piso).
/// 2. Rock: arrastra aca la roca especial (una roca empujable de la escena).
///
/// La roca queda centrada en la caja. No hace falta que el hueco tenga fondo: la roca queda
/// sostenida sola. Al volver a un checkpoint la roca vuelve a su lugar y el hueco queda libre.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class HuecoParaRoca : MonoBehaviour
{
    [Tooltip("La roca especial que encaja aca (una roca empujable de la escena). Las demas no encajan.")]
    public PushableBox rock;
    [Tooltip("Velocidad minima con la que se corre de costado hacia el centro del hueco mientras cae.")]
    public float slideSpeed = 2.5f;
    [Tooltip("Gravedad con la que cae adentro del hueco. Mas bajo = cae mas lento.")]
    public float fallGravity = 25f;
    [Tooltip("Cuanto mas arriba del borde de la caja puede estar la base de la roca y todavia encajar (viene rodando por el piso).")]
    public float reachAbove = 1.5f;

    [Header("Efectos (opcional)")]
    [Tooltip("Sonido cuando encaja. Vacio = el de fabrica.")]
    public AudioClip lockSound;
    [Tooltip("La camara tiembla un poco cuando encaja.")]
    public bool cameraShake = true;
    [Tooltip("Algo extra que quieras que pase cuando encaja (abrir una puerta, etc).")]
    public UnityEvent onLocked;

    BoxCollider2D zona;
    Collider2D rocaCol;
    Rigidbody2D rocaRb;
    bool ocupado;

    void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void Awake()
    {
        zona = GetComponent<BoxCollider2D>();
        zona.isTrigger = true;   // es solo una marca: nunca tiene que ser piso
        if (rock != null) { rocaCol = rock.GetComponent<Collider2D>(); rocaRb = rock.GetComponent<Rigidbody2D>(); }
        else Debug.LogWarning("HuecoParaRoca: falta asignar la roca especial (Rock).", this);
    }

    void FixedUpdate()
    {
        if (rock == null || rocaCol == null) return;

        // la roca volvio a su lugar (checkpoint): el hueco queda libre otra vez
        if (ocupado) { if (!rock.Encajada) ocupado = false; return; }
        if (!rock.gameObject.activeInHierarchy || rock.Encajada) return;

        // la roca especial esta ARRIBA del hueco? su centro entro en el ancho de la caja y su base
        // esta a la altura del borde de arriba (rodando por el piso) o ya metida adentro
        Bounds z = zona.bounds;
        Bounds r = rocaCol.bounds;
        if (r.center.x < z.min.x || r.center.x > z.max.x) return;
        if (r.min.y > z.max.y + reachAbove || r.max.y < z.min.y) return;

        // cae de una al hueco, centrandose mientras cae; el golpe suena cuando se asienta
        ocupado = true;
        float venia = rocaRb != null ? Mathf.Abs(rocaRb.linearVelocity.x) : 0f;
        rock.Encajar(z.center.x, z.max.y, Mathf.Max(slideSpeed, venia), fallGravity, AlAsentarse);
    }

    // La roca toco fondo: sonido, temblor y lo que hayas enganchado.
    void AlAsentarse()
    {
        Sonidos.Play(lockSound, "roca_encaja");
        if (cameraShake && Camera.main != null)
        {
            var cam = Camera.main.GetComponent<CameraFollow2D>();
            if (cam != null) cam.Shake(0.25f, 0.2f);
        }
        onLocked?.Invoke();
    }

    void OnDrawGizmos()
    {
        var c = GetComponent<BoxCollider2D>();
        if (c == null) return;
        Bounds b = c.bounds;
        // la caja del hueco, y en amarillo el borde de arriba: ahi queda el techo de la roca
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireCube(b.center, b.size);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(b.min.x, b.max.y, 0f), new Vector3(b.max.x, b.max.y, 0f));
    }
}
