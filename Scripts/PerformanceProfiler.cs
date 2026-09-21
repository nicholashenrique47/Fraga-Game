using UnityEngine;
using UnityEngine.UI;
using System.Text;

/// PerformanceProfiler: Ferramenta de diagnostico leve para Mobile
/// Calcula FPS Medios, Minimos, picos de frame-time e reporta Memoria GC.
public class PerformanceProfiler : MonoBehaviour
{
    private float deltaTime = 0.0f;
    private float updateTimer = 0.5f;

    private float minFps = 999f;
    private float maxFps = 0f;

    private Text debugText;
    private StringBuilder sb = new StringBuilder(100);

    private void Start()
    {
        // Criar UI Debugger no Canvas Overlay automatico se possivel
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Debug_Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        GameObject textObj = new GameObject("FPS_Counter");
        textObj.transform.SetParent(canvas.transform);
        debugText = textObj.AddComponent<Text>();
        debugText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        debugText.fontSize = 14;
        debugText.color = Color.green;
        debugText.alignment = TextAnchor.UpperRight;

        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-10, -10);
        rt.sizeDelta = new Vector2(250, 100);
    }

    private void Update()
    {
        // Smooth delta time
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        updateTimer -= Time.unscaledDeltaTime;
        if (updateTimer <= 0)
        {
            float currentFps = 1.0f / deltaTime;
            if (currentFps < minFps) minFps = currentFps;
            if (currentFps > maxFps) maxFps = currentFps;

            long memoryBytes = System.GC.GetTotalMemory(false);
            float memoryMB = memoryBytes / 1048576f; // 1024 * 1024

            sb.Clear();
            sb.AppendFormat("FPS: {0:0.} (Min: {1:0.} / Max: {2:0.})\n", currentFps, minFps, maxFps);
            sb.AppendFormat("Frame: {0:0.0} ms\n", deltaTime * 1000f);
            sb.AppendFormat("Mem: {0:0.00} MB (GC)", memoryMB);

            if (debugText != null)
            {
                debugText.text = sb.ToString();

                // Feedback visual de performance
                if (currentFps >= 55f) debugText.color = Color.green;
                else if (currentFps >= 30f) debugText.color = Color.yellow;
                else debugText.color = Color.red;
            }

            // Reseta mins e maxes a cada 10s para manter valores recentes
            if (Time.time % 10f < 0.5f)
            {
                minFps = currentFps;
                maxFps = 0f;
            }

            updateTimer = 0.5f;
        }
    }
}
