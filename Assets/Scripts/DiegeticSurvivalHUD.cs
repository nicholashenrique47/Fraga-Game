using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controlador do HUD Diegético Sons of the Forest para Mobile.
/// Gerencia micro-arcos translúcidos concêntricos, bússola rúnica curva e relógio analógico de 24m.
/// </summary>
public class DiegeticSurvivalHUD : MonoBehaviour
{
    [Header("Arcos de Status Orgânicos")]
    public Image healthArc;
    public Image staminaArc;
    public Image hungerArc;
    public Image thirstArc;
    public Image sanityArc;
    public CanvasGroup statusCanvasGroup;

    [Header("Bússola Rúnica Superior")]
    public RectTransform compassRibbon;
    public Transform playerTransform;

    [Header("Relógio de Bolso Solar (24m Ciclo)")]
    public RectTransform hourHandTransform;
    public DayNightCycle dayNightCycle;

    private float targetAlpha = 0.95f;

    private void Start()
    {
        if (dayNightCycle == null) dayNightCycle = FindAnyObjectByType<DayNightCycle>();
        if (playerTransform == null)
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) playerTransform = pc.transform;
        }
    }

    private void Update()
    {
        UpdateCompass();
        UpdateClock();
        UpdateFadeDynamics();
    }

    private void UpdateCompass()
    {
        if (playerTransform == null || compassRibbon == null) return;
        float yaw = playerTransform.eulerAngles.y;
        compassRibbon.anchoredPosition = new Vector2(-yaw * 4.5f, 0);
    }

    private void UpdateClock()
    {
        if (dayNightCycle == null || hourHandTransform == null) return;
        float hourAngle = (dayNightCycle.currentTime / 24f) * 360f;
        hourHandTransform.localRotation = Quaternion.Euler(0, 0, -hourAngle);
    }

    private void UpdateFadeDynamics()
    {
        if (statusCanvasGroup == null) return;
        statusCanvasGroup.alpha = Mathf.Lerp(statusCanvasGroup.alpha, targetAlpha, Time.deltaTime * 3f);
    }

    public void SetStatusValues(float healthNorm, float staminaNorm, float hungerNorm, float thirstNorm, float sanityNorm)
    {
        if (healthArc != null) healthArc.fillAmount = healthNorm;
        if (staminaArc != null) staminaArc.fillAmount = staminaNorm;
        if (hungerArc != null) hungerArc.fillAmount = hungerNorm;
        if (thirstArc != null) thirstArc.fillAmount = thirstNorm;
        if (sanityArc != null) sanityArc.fillAmount = sanityNorm;

        bool isUnderStress = healthNorm < 0.99f || staminaNorm < 0.95f || hungerNorm < 0.8f || thirstNorm < 0.8f;
        targetAlpha = isUnderStress ? 0.95f : 0.25f;
    }
}
