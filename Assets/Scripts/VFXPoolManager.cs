using UnityEngine;
using System.Collections.Generic;

/// VFXPoolManager: Object Pooling de alto desempenho para efeitos de particulas.
/// Evita Instantiate/Destroy em runtime (critico para mobile).
/// Gere pools separados por tipo de efeito com reciclagem automatica e fallbacks procedurais URP.
public class VFXPoolManager : MonoBehaviour
{
    public static VFXPoolManager Instance { get; private set; }

    [Header("Pool Config")]
    public int initialPoolSize = 8;

    [Header("VFX Prefabs (Atribuir no Inspector ou gerados em runtime)")]
    public GameObject celticBeaconVFX;    // Particulas de luz divina ao construir Farol Celta
    public GameObject oilTrapFireVFX;     // Chamas ao incendiar armadilha de oleo
    public GameObject trasnoAppearVFX;    // Fumaca negra ao Trasno aparecer
    public GameObject trasnoDisappearVFX; // Dissipacao ao Trasno fugir
    public GameObject buildPlaceVFX;      // Poeira ao construir qualquer estrutura
    public GameObject deathVFX;           // Particulas de escuridao ao morrer

    private GameObject woodChipVFX;
    private GameObject sparkVFX;
    private GameObject bloodSplashVFX;
    private GameObject shadowHitVFX;
    private GameObject bloodDecalVFX;
    private GameObject shadowDecalVFX;
    private Material cachedParticleMat;
    private Texture2D cachedSplatterTex;

    private readonly Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();

    private void Awake()
    {
        Instance = this;
        EnsureProceduralVFXTemplates();
        PrewarmAllPools();
    }

    private void EnsureProceduralVFXTemplates()
    {
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Standard");

        cachedParticleMat = new Material(particleShader);
        if (cachedParticleMat.HasProperty("_BaseColor"))
            cachedParticleMat.SetColor("_BaseColor", Color.white);
        cachedParticleMat.color = Color.white;

        if (buildPlaceVFX == null)
            buildPlaceVFX = CreateBurstTemplate("VFX_Template_Dust", new Color(0.68f, 0.58f, 0.44f, 0.85f), 0.14f, 3.2f, 14, 0.55f, 0.4f);

        if (deathVFX == null)
            deathVFX = CreateBurstTemplate("VFX_Template_Death", new Color(0.14f, 0.08f, 0.20f, 0.95f), 0.22f, 4.0f, 22, 0.95f, -0.35f);

        woodChipVFX = CreateBurstTemplate("VFX_Template_WoodChips", new Color(0.64f, 0.43f, 0.21f, 1f), 0.09f, 5.2f, 16, 0.55f, 1.4f);
        sparkVFX = CreateBurstTemplate("VFX_Template_Sparks", new Color(1.0f, 0.72f, 0.20f, 1f), 0.055f, 6.8f, 18, 0.35f, 1.1f);
        bloodSplashVFX = CreateBurstTemplate("VFX_Template_Blood", new Color(0.58f, 0.04f, 0.05f, 0.96f), 0.095f, 4.6f, 16, 0.48f, 1.6f);
        shadowHitVFX = CreateBurstTemplate("VFX_Template_ShadowHit", new Color(0.32f, 0.12f, 0.48f, 0.92f), 0.14f, 4.4f, 18, 0.55f, -0.2f);
        bloodDecalVFX = CreateDecalTemplate("VFX_Template_BloodDecal", new Color(0.38f, 0.02f, 0.03f, 0.86f));
        shadowDecalVFX = CreateDecalTemplate("VFX_Template_ShadowDecal", new Color(0.10f, 0.03f, 0.16f, 0.84f));
    }

    private Texture2D GetOrCreateSplatterTexture()
    {
        if (cachedSplatterTex != null) return cachedSplatterTex;
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                float wobble = 0.18f * Mathf.Sin(ang * 5f + 0.6f) + 0.11f * Mathf.Cos(ang * 9f - 1.1f);
                float edge = 0.72f + wobble;
                float alpha = Mathf.Clamp01((edge - r) * 5.5f);
                float drop = Mathf.Max(0f, 1f - Vector2.Distance(new Vector2(dx, dy), new Vector2(0.55f, -0.35f)) * 4.5f);
                alpha = Mathf.Clamp01(Mathf.Max(alpha, drop * 0.85f));
                px[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        cachedSplatterTex = tex;
        return cachedSplatterTex;
    }

    private GameObject CreateDecalTemplate(string templateName, Color tint)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        obj.name = templateName;
        obj.transform.SetParent(transform, false);
        Collider col = obj.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Shader decalShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Standard");
        Material mat = new Material(decalShader);
        Texture2D tex = GetOrCreateSplatterTexture();
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        mat.color = tint;
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        mat.renderQueue = 3000;

        Renderer r = obj.GetComponent<Renderer>();
        if (r != null)
        {
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        obj.SetActive(false);
        return obj;
    }

    private GameObject CreateBurstTemplate(string templateName, Color color, float size, float speed, int burstCount, float lifetime, float gravityMod)
    {
        GameObject obj = new GameObject(templateName);
        obj.transform.SetParent(transform, false);
        obj.SetActive(false);

        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = lifetime;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.55f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.55f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.65f, size * 1.25f);
        main.startColor = color;
        main.gravityModifier = gravityMod;
        main.maxParticles = burstCount + 4;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)burstCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 38f;
        shape.radius = 0.08f;

        var colLife = ps.colorOverLifetime;
        colLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(color, 0f), new GradientColorKey(color * 0.6f, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colLife.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = obj.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && cachedParticleMat != null)
        {
            renderer.sharedMaterial = cachedParticleMat;
        }

        return obj;
    }

    private void PrewarmAllPools()
    {
        PrewarmPool(celticBeaconVFX);
        PrewarmPool(oilTrapFireVFX);
        PrewarmPool(trasnoAppearVFX);
        PrewarmPool(trasnoDisappearVFX);
        PrewarmPool(buildPlaceVFX);
        PrewarmPool(deathVFX);
        PrewarmPool(woodChipVFX);
        PrewarmPool(sparkVFX);
        PrewarmPool(bloodSplashVFX);
        PrewarmPool(shadowHitVFX);
        PrewarmPool(bloodDecalVFX);
        PrewarmPool(shadowDecalVFX);
    }

    private void PrewarmPool(GameObject prefab)
    {
        if (prefab == null || pools.ContainsKey(prefab)) return;

        Queue<GameObject> pool = new Queue<GameObject>(initialPoolSize);
        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
        pools[prefab] = pool;
    }

    public GameObject SpawnVFX(GameObject prefab, Vector3 position)
    {
        return SpawnVFX(prefab, position, Quaternion.identity, 3f);
    }

    public GameObject SpawnVFX(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime)
    {
        if (prefab == null) return null;

        if (!pools.ContainsKey(prefab))
        {
            PrewarmPool(prefab);
        }

        Queue<GameObject> pool = pools[prefab];
        GameObject instance;

        if (pool.Count > 0)
        {
            instance = pool.Dequeue();
        }
        else
        {
            instance = Instantiate(prefab, transform);
        }

        instance.transform.position = position;
        instance.transform.rotation = rotation;
        instance.SetActive(true);

        ParticleSystem ps = instance.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Clear(true);
            ps.Play(true);
        }

        StartCoroutine(ReturnToPoolAfter(instance, prefab, lifetime));
        return instance;
    }

    private System.Collections.IEnumerator ReturnToPoolAfter(GameObject obj, GameObject prefab, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToPool(obj, prefab);
    }

    public void ReturnToPool(GameObject obj, GameObject prefab)
    {
        if (obj == null) return;

        obj.SetActive(false);

        ParticleSystem ps = obj.GetComponent<ParticleSystem>();
        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (pools.ContainsKey(prefab))
        {
            pools[prefab].Enqueue(obj);
        }
    }

    public void PlayCelticBeaconVFX(Vector3 position)
    {
        SpawnVFX(celticBeaconVFX, position + Vector3.up * 0.5f, Quaternion.identity, 4f);
    }

    public void PlayOilTrapFireVFX(Vector3 position)
    {
        SpawnVFX(oilTrapFireVFX, position, Quaternion.identity, 8f);
    }

    public void PlayTrasnoAppearVFX(Vector3 position)
    {
        SpawnVFX(trasnoAppearVFX, position + Vector3.up * 0.3f, Quaternion.identity, 2f);
    }

    public void PlayTrasnoDisappearVFX(Vector3 position)
    {
        SpawnVFX(trasnoDisappearVFX, position + Vector3.up * 0.3f, Quaternion.identity, 2.5f);
    }

    public void PlayBuildPlaceVFX(Vector3 position)
    {
        SpawnVFX(buildPlaceVFX, position, Quaternion.Euler(-90f, 0f, 0f), 1.2f);
    }

    public void PlayDeathVFX(Vector3 position)
    {
        SpawnVFX(deathVFX, position + Vector3.up * 0.8f, Quaternion.Euler(-90f, 0f, 0f), 1.6f);
    }

    public void PlayWoodChipVFX(Vector3 position, Vector3 normal)
    {
        Quaternion rot = normal.sqrMagnitude > 0.01f ? Quaternion.LookRotation(normal) : Quaternion.Euler(-90f, 0f, 0f);
        SpawnVFX(woodChipVFX, position, rot, 0.9f);
    }

    public void PlaySparkVFX(Vector3 position, Vector3 normal)
    {
        Quaternion rot = normal.sqrMagnitude > 0.01f ? Quaternion.LookRotation(normal) : Quaternion.Euler(-90f, 0f, 0f);
        SpawnVFX(sparkVFX, position, rot, 0.7f);
    }

    public void PlayBloodSplashVFX(Vector3 position, Vector3 normal)
    {
        Quaternion rot = normal.sqrMagnitude > 0.01f ? Quaternion.LookRotation(normal) : Quaternion.Euler(-90f, 0f, 0f);
        SpawnVFX(bloodSplashVFX, position, rot, 0.8f);
    }

    public void PlayShadowHitVFX(Vector3 position)
    {
        SpawnVFX(shadowHitVFX, position, Quaternion.Euler(-90f, 0f, 0f), 0.9f);
    }

    public void PlayBloodDecalVFX(Vector3 position, bool isShadow = false)
    {
        GameObject template = isShadow ? shadowDecalVFX : bloodDecalVFX;
        if (template == null) return;

        float groundY = TerrainMeshGenerator.GetTerrainHeight(position.x, position.z) + 0.035f;
        Vector3 decalPos = new Vector3(position.x, groundY, position.z);
        Quaternion rot = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        GameObject decal = SpawnVFX(template, decalPos, rot, 18f);
        if (decal != null)
        {
            float s = Random.Range(0.75f, 1.35f);
            decal.transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
