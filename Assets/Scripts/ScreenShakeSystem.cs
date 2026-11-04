using UnityEngine;

/// ScreenShakeSystem: Sistema de screen shake reutilizável com perfis de trauma.
/// Suporta múltiplos impactos simultâneos com decaimento independente.
/// Usa Perlin Noise para movimentação orgânica (não aleatória).
public class ScreenShakeSystem : MonoBehaviour
{
    public static ScreenShakeSystem Instance { get; private set; }

    [Header("Config")]
    public float maxTrauma = 1f;
    public float traumaDecay = 1.3f; // Velocidade de recuperação
    public float maxOffset = 0.5f;   // Deslocamento máximo da câmera
    public float maxAngle = 3f;      // Rotação máxima
    public float frequency = 25f;    // Velocidade do noise

    private float currentTrauma = 0f;
    private Camera mainCamera;
    private Vector3 originalPos;
    private Quaternion originalRot;
    private float seed;

    private void Awake()
    {
        Instance = this;
        mainCamera = Camera.main;
        seed = Random.value * 1000f;
    }

    private void LateUpdate()
    {
        if (mainCamera == null) return;

        if (currentTrauma > 0f)
        {
            currentTrauma = Mathf.Max(0f, currentTrauma - traumaDecay * Time.deltaTime);

            // Shake² para curva quadrática (mais suave no fim)
            float shake = currentTrauma * currentTrauma;

            float time = Time.time * frequency;

            // Deslocamento via Perlin Noise para movimento orgânico
            float offsetX = maxOffset * shake * (Mathf.PerlinNoise(seed, time) * 2f - 1f);
            float offsetY = maxOffset * shake * (Mathf.PerlinNoise(seed + 1f, time) * 2f - 1f);
            float angle = maxAngle * shake * (Mathf.PerlinNoise(seed + 2f, time) * 2f - 1f);

            // Aplicar offset à câmera
            mainCamera.transform.localPosition += new Vector3(offsetX, offsetY, 0f);
            mainCamera.transform.localRotation *= Quaternion.Euler(0, 0, angle);
        }
    }

    // =================== API PÚBLICA ===================

    /// Adiciona trauma (0.0 a 1.0). Múltiplas chamadas acumulam.
    public void AddTrauma(float amount)
    {
        currentTrauma = Mathf.Min(maxTrauma, currentTrauma + amount);
    }

    /// Shake leve (coleta de item, passo)
    public void ShakeLight()
    {
        AddTrauma(0.1f);
    }

    /// Shake médio (dano recebido, explosão próxima)
    public void ShakeMedium()
    {
        AddTrauma(0.35f);
    }

    /// Shake pesado (ataque de boss, AoE, morte)
    public void ShakeHeavy()
    {
        AddTrauma(0.7f);
    }

    /// Shake contínuo (terremoto, evento cinemático)
    public void ShakeContinuous(float intensity)
    {
        currentTrauma = Mathf.Max(currentTrauma, intensity);
    }
}
