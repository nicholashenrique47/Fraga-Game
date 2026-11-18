using UnityEngine;
using UnityEngine.UI;
using System.Text;

/// PerformanceProfiler: Ferramenta de diagnostico leve para Mobile e PC (tecla [F3] alterna visibilidade).
/// Posicionada a esquerda do Relogio para zero sobreposicao com o HUD.
public class PerformanceProfiler : MonoBehaviour
{
    private float deltaTime = 0.0f;
    private float updateTimer = 0.5f;

    private float minFps = 999f;
    private float maxFps = 0f;
    private bool isVisible = true;

    private GameObject counterObj;
    private Text debugText;
    private readonly StringBuilder sb = new StringBuilder(100);

    private void Start()
    {
        Canvas canvas = HUDSurvivalOverlay.GetGameplayCanvas();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Debug_Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 55;
            CanvasScaler cs = canvasObj.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cs.matchWidthOrHeight = 0.5f;
        }

        counterObj = new GameObject("FPS_Counter");
        counterObj.transform.SetParent(canvas.transform, false);
        debugText = counterObj.AddComponent<Text>();
        debugText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        debugText.fontSize = 11;
        debugText.fontStyle = FontStyle.Bold;
        debugText.color = new Color(0.55f, 0.96f, 0.62f, 0.85f);
        debugText.alignment = TextAnchor.UpperRight;
        debugText.raycastTarget = false;

        Outline ol = counterObj.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.85f);
        ol.effectDistance = new Vector2(1f, -1f);

        RectTransform rt = counterObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        // Fica a esquerda do Relogio (-20..-216), sem qualquer sobreposicao
        rt.anchoredPosition = new Vector2(-232f, -14f);
        rt.sizeDelta = new Vector2(210f, 52f);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3) && counterObj != null)
        {
            isVisible = !isVisible;
            counterObj.SetActive(isVisible);
        }

        if (!isVisible) return;

        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        updateTimer -= Time.unscaledDeltaTime;
        if (updateTimer <= 0f)
        {
            float safeDt = Mathf.Max(0.0005f, deltaTime);
            float currentFps = 1.0f / safeDt;
            if (currentFps < minFps) minFps = currentFps;
            if (currentFps > maxFps) maxFps = currentFps;

            long memoryBytes = System.GC.GetTotalMemory(false);
            float memoryMB = memoryBytes / 1048576f;

            sb.Clear();
            sb.AppendFormat("FPS: {0:0.} (Min {1:0.} / Max {2:0.})\n", currentFps, minFps, maxFps);
            sb.AppendFormat("{0:0.0} ms  |  GC: {1:0.0} MB", deltaTime * 1000f, memoryMB);

            if (debugText != null)
            {
                debugText.text = sb.ToString();

                if (currentFps >= 55f) debugText.color = new Color(0.55f, 0.96f, 0.62f, 0.85f);
                else if (currentFps >= 30f) debugText.color = new Color(1.0f, 0.88f, 0.36f, 0.90f);
                else debugText.color = new Color(1.0f, 0.35f, 0.35f, 0.95f);
            }

            if (Time.unscaledTime % 10f < 0.5f)
            {
                minFps = currentFps;
                maxFps = 0f;
            }

            updateTimer = 0.5f;
        }
    }
}
