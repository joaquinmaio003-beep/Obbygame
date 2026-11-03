using System.Collections;
using UnityEngine;

// Parte de WarriorEnemy: lo que le piden la Emboscada y la fila: distancia, saltos, retirada, entradas. Las variables y el Update estan en WarriorEnemy.cs
public partial class WarriorEnemy
{
    /// <summary>
    /// No se encima con otros guerreros: guarda 'distancia' con el que tiene adelante. Entra a la
    /// fila como recien llegado: no frena a los que ya venian, se acomoda el (ver MirarFila).
    /// </summary>
    public void MantenerDistancia(float distancia)
    {
        separacion = distancia;
        turno = ++s_turnos;
        acomodado = false;
        acomodarAntesDe = Time.time + 6f;
    }

    /// <summary>
    /// Persiguiendo a Obby avanza a los saltitos de 'altura', uno cada 'cada' segundos mas o menos
    /// (cada uno a su tiempo). A menos de 'noCercaDe' de Obby no salta. Con cada en 0 no salta nunca.
    /// </summary>
    public void Saltarin(float altura, float cada, float noCercaDe)
    {
        saltoAltura = altura;
        saltoCada = cada;
        saltoLejosDe = noCercaDe;
    }

    // LA FILA. Para no encimarse, el que tiene a otro guerrero adelante a menos de 'separacion'
    // frena. Pero un RECIEN LLEGADO no frena a nadie: los que ya venian caminando lo ignoran hasta
    // que encuentre su lugar. Es el el que se acomoda: si tiene a uno pegado atras (o encima) se
    // apura hasta sacarle distancia, y si lo tiene adelante espera a que se aleje. Cuando queda
    // con lugar libre adelante y atras ya es uno mas de la fila. Entre dos recien llegados se
    // acomoda el mas nuevo.
    // (Antes cualquiera que tuviera adelante lo frenaba: cada vez que llegaba uno nuevo en el
    // medio se paraban todos los de atras.)
    void MirarFila(out bool frenar, out bool apurar)
    {
        frenar = apurar = false;
        if (separacion <= 0f) return;
        float x = CentroX(), y = transform.position.y;
        for (int i = 0; i < todos.Count; i++)
        {
            WarriorEnemy o = todos[i];
            if (o == this || o.muerto || !o.gameObject.activeInHierarchy) continue;
            if (!o.acomodado && (acomodado || o.turno > turno)) continue;   // recien llegado: se acomoda el, no yo
            if (Mathf.Abs(o.transform.position.y - y) > 1.2f) continue;     // en otro piso, o todavia cayendo: no estorba
            float dx = (o.CentroX() - x) * dir;
            if (dx > 0.01f && dx < separacion) frenar = true;
            else if (!acomodado && dx <= 0.01f && dx > -separacion) apurar = true;
        }
    }

    /// <summary>Se queda parado mirando a Obby estos segundos (caer y saltar puede; caminar y pegar no).</summary>
    public void Esperar(float segundos) { quietoHasta = Time.time + segundos; }

    /// <summary>
    /// SE RETIRA: deja de pelear, le da la espalda a Obby, se va corriendo a 'velocidad' y
    /// desaparece cuando sale de la pantalla. Si no puede seguir (una pared, un pozo) o tarda
    /// demasiado, se desvanece donde esta. No vuelve (salvo que el nivel lo reponga).
    /// </summary>
    public void Retirarse(float velocidad)
    {
        if (muerto || retirada) return;
        // todavia venia entrando y no se lo llego a ver: directamente no aparece
        if (enElAire && !ALaVista()) { EmpezarMuerte(); Desaparecer(); return; }

        retirada = true;
        retiradaVel = velocidad;
        retiradaTope = Time.time + 8f;
        trabadoDesde = -1f;
        // ya no amenaza, ni salta, ni espera, ni hace fila: se va
        amenaza = false;
        saltoCada = 0f;
        quietoHasta = 0f;
        separacion = 0f;
        if (isAttacking)   // corta el golpe que estaba por dar
        {
            StopAllCoroutines();
            isAttacking = false;
            sr.color = colorBase;
        }
        if (alertIcon != null) alertIcon.SetActive(false);
    }

    // Se ve en pantalla (el dibujo entero, con un poquito de margen)?
    bool ALaVista()
    {
        Bounds d = sr.bounds;
        return Pantalla.Ve(d.center, d.extents, 0.5f);
    }

    // Un paso de la retirada (ver Retirarse).
    void Retirada()
    {
        if (!ALaVista()) { EmpezarMuerte(); Desaparecer(); return; }   // ya nadie lo ve: se fue

        // de espaldas a Obby
        if (player != null)
        {
            int lejos = CentroX() >= player.position.x ? 1 : -1;
            if (lejos != dir) { dir = lejos; ApplyFacing(); }
        }

        bool puede = CanAdvance();
        rb.linearVelocity = puede ? new Vector2(dir * retiradaVel, 0f) : Vector2.zero;
        if (puede) trabadoDesde = -1f;
        else if (trabadoDesde < 0f) trabadoDesde = Time.time;

        // no tiene por donde irse, o ya tardo mucho: se desvanece ahi
        if ((trabadoDesde >= 0f && Time.time - trabadoDesde > 0.5f) || Time.time >= retiradaTope)
        {
            EmpezarMuerte();   // deja de hacer todo y ya no lo toca nadie
            StartCoroutine(Desvanecer());
        }
    }

    IEnumerator Desvanecer()
    {
        Color c = sr.color;
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            sr.color = new Color(c.r, c.g, c.b, c.a * (1f - t / 0.35f));
            yield return null;
        }
        Desaparecer();
    }

    /// <summary>
    /// Entra en escena CAYENDO desde arriba de la pantalla. Si en el camino hay un techo lo
    /// atraviesa, dibujado por delante para que se lo vea venir, y recien busca piso cuando todo
    /// el cuerpo quedo por debajo de 'techoY'. A cielo abierto: float.PositiveInfinity.
    /// </summary>
    public void EntrarCayendo(float techoY)
    {
        enElAire = true;
        fallVel = 0f;
        if (float.IsPositiveInfinity(techoY)) return;   // cielo abierto: cae como cualquiera
        atravesarHastaY = techoY;
        PonerDelante(true);
    }

    /// <summary>
    /// Entra en escena SALTANDO desde abajo de la pantalla: sube atravesando el piso, dibujado por
    /// delante, hasta que los pies llegan a 'piesHasta'; de ahi cae y se apoya como siempre.
    /// </summary>
    public void EntrarSaltando(float piesHasta)
    {
        float sube = Mathf.Max(0.1f, piesHasta - col.bounds.min.y);
        fallVel = -Mathf.Sqrt(2f * fallGravity * sube);
        aireVelX = 0f;
        enElAire = true;
        PonerDelante(true);
    }

    // Se dibuja por delante de todo mientras atraviesa el techo o el piso al entrar.
    void PonerDelante(bool delante)
    {
        porDelante = delante;
        sr.sortingOrder = delante ? ordenNormal + 10 : ordenNormal;
    }

    /// <summary>
    /// Saltito hacia Obby: sube 'altura' y avanza 'avance' unidades. Si adelante no hay piso salta
    /// en el lugar. En el aire, aturdido o muerto no hace nada.
    /// </summary>
    public void Saltito(float altura, float avance)
    {
        if (altura <= 0f) return;
        float tiempo = 2f * Mathf.Sqrt(2f * altura / fallGravity);   // lo que tarda en subir y volver a bajar
        Saltar(altura, avance / tiempo);
    }

    // Salta 'altura' hacia Obby avanzando a 'velX' (unidades por segundo) mientras dura.
    void Saltar(float altura, float velX)
    {
        if (muerto || isStunned || isRecovering || isAttacking || enElAire || altura <= 0f) return;
        FacePlayer();
        float subida = Mathf.Sqrt(2f * fallGravity * altura);
        float tiempo = 2f * subida / fallGravity;
        Bounds b = col.bounds;
        var adelante = new Vector2(b.center.x + dir * velX * tiempo, b.min.y + maxStepHeight);
        bool hayPiso = Physics2D.Raycast(adelante, Vector2.down, maxStepHeight + maxDropHeight, groundLayer);
        aireVelX = hayPiso ? dir * velX : 0f;
        fallVel = -subida;
        enElAire = true;
    }
}
