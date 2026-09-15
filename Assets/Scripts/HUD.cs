using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD en pantalla: contador de vidas + barra de recarga del dash.
///
/// Setup: un Canvas con un Text (para las vidas, ahi le ponés tu Font) y una
/// Image tipo Filled (para la barra del dash). Arrastrás todo a este script.
/// </summary>
public class HUD : MonoBehaviour
{
    [Header("Referencias del jugador")]
    public PlayerRespawn player;          // de donde saca las vidas
    public PlayerController2D controller;  // de donde saca la recarga del dash
    public RockThrower rockThrower;        // de donde saca las piedras

    [Header("Vidas")]
    public TMP_Text livesText;
    [Tooltip("Texto antes del numero de vidas (ej: 'x' -> x3).")]
    public string livesPrefix = "x";

    [Header("Piedras")]
    public TMP_Text rocksText;
    [Tooltip("Texto antes del numero de piedras (ej: 'x' -> x5).")]
    public string rocksPrefix = "x";

    [Header("Barra del dash")]
    [Tooltip("Image con Image Type = Filled (se llena mientras recarga).")]
    public Image dashBar;
    [Tooltip("Color de la barra mientras recarga.")]
    public Color chargingColor = new Color(1f, 0.8f, 0.2f);
    [Tooltip("Color de la barra cuando el dash esta listo.")]
    public Color readyColor = new Color(0.3f, 0.9f, 1f);

    [Header("Barra de agarre (wall grip)")]
    [Tooltip("Image con Image Type = Filled (muestra cuanto agarre de pared le queda).")]
    public Image gripBar;
    [Tooltip("Color cuando tiene agarre.")]
    public Color gripColor = new Color(0.6f, 0.9f, 0.4f);
    [Tooltip("Color cuando el agarre esta casi/vacio.")]
    public Color gripEmptyColor = new Color(0.9f, 0.4f, 0.3f);
    [Tooltip("Ocultar la barra cuando el agarre esta lleno (solo se ve al usarlo).")]
    public bool hideGripWhenFull = true;

    void Update()
    {
        if (livesText != null && player != null)
            livesText.text = livesPrefix + Mathf.Max(0, player.Lives);

        if (rocksText != null && rockThrower != null)
            rocksText.text = rocksPrefix + rockThrower.currentRocks;

        if (dashBar != null && controller != null)
        {
            dashBar.fillAmount = controller.DashChargeNormalized;
            dashBar.color = controller.DashReady ? readyColor : chargingColor;
        }

        if (gripBar != null && controller != null)
        {
            float g = controller.WallGripNormalized;
            gripBar.fillAmount = g;
            gripBar.color = Color.Lerp(gripEmptyColor, gripColor, g); // rojo cuando se acaba, verde cuando esta lleno
            // solo se muestra cuando la esta usando (no lleno), si esta activada la opcion
            bool show = !hideGripWhenFull || g < 0.999f;
            if (gripBar.enabled != show) gripBar.enabled = show;
        }
    }
}
