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

    [Header("Vidas")]
    public TMP_Text livesText;
    [Tooltip("Texto antes del numero de vidas (ej: 'x' -> x3).")]
    public string livesPrefix = "x";

    [Header("Barra del dash")]
    [Tooltip("Image con Image Type = Filled (se llena mientras recarga).")]
    public Image dashBar;
    [Tooltip("Color de la barra mientras recarga.")]
    public Color chargingColor = new Color(1f, 0.8f, 0.2f);
    [Tooltip("Color de la barra cuando el dash esta listo.")]
    public Color readyColor = new Color(0.3f, 0.9f, 1f);

    void Update()
    {
        if (livesText != null && player != null)
            livesText.text = livesPrefix + Mathf.Max(0, player.Lives);

        if (dashBar != null && controller != null)
        {
            dashBar.fillAmount = controller.DashChargeNormalized;
            dashBar.color = controller.DashReady ? readyColor : chargingColor;
        }
    }
}
