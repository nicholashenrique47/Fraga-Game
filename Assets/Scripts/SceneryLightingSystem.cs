using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// SceneryLightingSystem: Adiciona e gerencia PointLights dinâmicos para cenários e chunks streamados.
/// - Cogumelos luminosos emanam brilho verde-azulado
/// - Fogueiras e totens emanam luz cálida com tremulação orgânica
/// - Cruzes celtas emanam aura lunar serena
/// Ativa apenas luzes num raio de 48m do jogador para 60 FPS no Mobile e respeito ao limite de luzes da URP.
/// </summary>
public class SceneryLightingSystem : MonoBehaviour
{
    [Header("Dependencies")]
    public MapRenderer mapRenderer;
    public DayNightCycle dayNightCycle;
    public Transform playerTransform;

    [Header("Light Config")]
    public float mushroomLightRange = 5f;
    public float campfireLightRange = 9f;
    public float crossLightRange = 6f;
    public float activeRadius = 48f;

    private enum LightKind : byte { Mushroom, Campfire, Cross }

    private struct TrackedLight
    {
        public Light light;
        public LightKind kind;
    }

    private readonly Dictionary<GameObject, TrackedLight> trackedLights = new Dictionary<GameObject, TrackedLight>();
    private readonly List<GameObject> deadKeys = new List<GameObject>(32);
    private float nextScanTime = 0f;

    private void Start()
    {
        if (mapRenderer == null) mapRenderer = FindAnyObjectByType<MapRenderer>();
        if (dayNightCycle == null) dayNightCycle = FindAnyObjectByType<DayNightCycle>();
        ResolvePlayer();
    }

    private void ResolvePlayer()
    {
        if (playerTransform != null) return;
        if (PlayerController.Instance != null)
        {
            playerTransform = PlayerController.Instance.transform;
            return;
        }
        if (Camera.main != null) playerTransform = Camera.main.transform;
    }

    private void Update()
    {
        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + 1.0f;
            SyncWithStreamedInstances();
        }

        UpdateLightEffects();
    }

    private void SyncWithStreamedInstances()
    {
        // 1. Limpa referências de chunks descarregados / árvores cortadas
        deadKeys.Clear();
        foreach (var kvp in trackedLights)
        {
            if (kvp.Key == null || kvp.Value.light == null)
                deadKeys.Add(kvp.Key);
        }
        for (int i = 0; i < deadKeys.Count; i++)
        {
            trackedLights.Remove(deadKeys[i]);
        }

        if (mapRenderer == null || mapRenderer.activeInstances == null) return;

        // 2. Anexa luzes em novos objetos carregados pelos chunks ativos
        foreach (var kvp in mapRenderer.activeInstances)
        {
            GameObject go = kvp.Value;
            if (go == null || trackedLights.ContainsKey(go)) continue;

            string lower = go.name.ToLower();
            if (lower.Contains("glowmushroom") || lower.Contains("mushroom"))
            {
                Light l = CreatePointLight(go.transform, new Color(0.12f, 0.92f, 0.72f), mushroomLightRange, 0.65f);
                trackedLights[go] = new TrackedLight { light = l, kind = LightKind.Mushroom };
            }
            else if (lower.Contains("campfire") || lower.Contains("fogueira") || lower.Contains("totem"))
            {
                Light existing = go.GetComponentInChildren<Light>();
                Light l = existing != null ? existing : CreatePointLight(go.transform, new Color(1f, 0.58f, 0.20f), campfireLightRange, 1.4f);
                trackedLights[go] = new TrackedLight { light = l, kind = LightKind.Campfire };
            }
            else if (lower.Contains("celticcross") || lower.Contains("cruz"))
            {
                Light l = CreatePointLight(go.transform, new Color(0.75f, 0.85f, 1f), crossLightRange, 0.45f);
                trackedLights[go] = new TrackedLight { light = l, kind = LightKind.Cross };
            }
        }
    }

    private Light CreatePointLight(Transform parent, Color color, float range, float intensity)
    {
        GameObject lightObj = new GameObject("SceneryLight");
        lightObj.transform.SetParent(parent, false);
        lightObj.transform.localPosition = new Vector3(0f, 0.55f, 0f);

        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.Auto;
        return light;
    }

    private void UpdateLightEffects()
    {
        ResolvePlayer();
        Vector3 pPos = playerTransform != null ? playerTransform.position : Vector3.zero;
        float radiusSq = activeRadius * activeRadius;
        float time = Time.time;
        bool isNight = dayNightCycle != null && (dayNightCycle.currentTime >= 19f || dayNightCycle.currentTime < 6f);

        foreach (var kvp in trackedLights)
        {
            Light l = kvp.Value.light;
            if (l == null) continue;

            float dSq = (l.transform.position - pPos).sqrMagnitude;
            bool inRange = (playerTransform == null) || dSq <= radiusSq;
            if (l.enabled != inRange) l.enabled = inRange;
            if (!inRange) continue;

            switch (kvp.Value.kind)
            {
                case LightKind.Mushroom:
                    l.intensity = 0.45f + Mathf.Sin(time * 1.6f) * 0.22f;
                    l.range = isNight ? mushroomLightRange * 1.45f : mushroomLightRange;
                    break;
                case LightKind.Campfire:
                    float flicker = 0.9f + Mathf.PerlinNoise(time * 7.5f, l.transform.position.x) * 0.55f;
                    l.intensity = Mathf.Lerp(l.intensity, flicker * 1.35f, Time.deltaTime * 10f);
                    l.range = campfireLightRange + Mathf.Sin(time * 3.1f) * 0.45f;
                    break;
                case LightKind.Cross:
                    l.intensity = 0.35f + Mathf.Sin(time * 0.5f) * 0.1f;
                    break;
            }
        }
    }
}
