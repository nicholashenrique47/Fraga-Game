using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public class FragaSceneSetup : EditorWindow
{
    private static Sprite CreateCircleSprite(int radius)
    {
        Texture2D texture = new Texture2D(radius * 2, radius * 2, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[texture.width * texture.height];
        Vector2 center = new Vector2(radius, radius);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float dist = Vector2.Distance(center, new Vector2(x, y));
                float alpha = Mathf.Clamp01(radius - dist); // Soft edge AA
                pixels[y * texture.width + x] = new Color(1, 1, 1, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    [MenuItem("Fraga Native/1. Criar Cena Completa (Mobile)")]
    public static void CreateGameScene()
    {
        // 1. Limpar cena atual (opcional, vamos apenas instanciar num ambiente limpo)
        GameObject orchestrator = new GameObject("--- GAME ORCHESTRATOR ---");
        GameManager gm = orchestrator.AddComponent<GameManager>();
        
        // 2. Setup do TopÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³grafo (Iago)
        GameObject playerPrefab = Resources.Load<GameObject>("Prefabs/Player_Prefab");
        GameObject player;
        if (playerPrefab != null) {
            player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        } else {
            player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        }
        player.name = "Iago (Topografo)";
        player.transform.position = new Vector3(50f, 1f, 50f); // Centro do mapa 100x100
        
        Rigidbody rb = player.AddComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.useGravity = false; // IsomÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©trico 2.5D
        
        PlayerController pc = player.AddComponent<PlayerController>();
        SanitySystem sanity = player.AddComponent<SanitySystem>();
        BuildingSystem buildSys = player.AddComponent<BuildingSystem>();
        ResourceManager resManager = player.AddComponent<ResourceManager>();
        
        // LampiÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£o (URP Light)
        GameObject lamp = new GameObject("Lampiao");
        lamp.transform.SetParent(player.transform);
        lamp.transform.localPosition = new Vector3(0, 1f, 0);
        Light lightComp = lamp.AddComponent<Light>();
        lightComp.type = LightType.Point;
        lightComp.color = new Color(1.0f, 0.6f, 0.2f); // Fogo quente
        lightComp.range = 15f;
        lightComp.intensity = 2f;
        lightComp.shadows = LightShadows.Soft; // Sombras atmosfÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©ricas
        sanity.lampLight = lightComp;

        // Setup AtmosfÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©rico Dark Low-Poly
        RenderSettings.ambientLight = new Color(0.05f, 0.05f, 0.1f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.05f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.04f;

        GameObject sun = new GameObject("Sol_Direcional");
        Light sunLight = sun.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        sunLight.color = new Color(0.2f, 0.3f, 0.5f); // Luz do luar
        sunLight.intensity = 0.4f;
        sunLight.shadows = LightShadows.Hard;
        
        DayNightCycle timeCycle = orchestrator.AddComponent<DayNightCycle>();
        timeCycle.directionalLight = sunLight;
        
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            IsometricCamera isoCam = mainCam.gameObject.AddComponent<IsometricCamera>();
            isoCam.target = player.transform;
            mainCam.orthographic = true;
            mainCam.orthographicSize = 8f;
            mainCam.gameObject.name = "Camera Isometrica";
        }

        // 4. Setup do Sistema de Hordas (Vultos e Entidades)
        VultoManager vultoManager = orchestrator.AddComponent<VultoManager>();
        vultoManager.playerTransform = player.transform;
        vultoManager.playerSanity = sanity;
        GameObject tempVultoPrefab = Resources.Load<GameObject>("Prefabs/Vulto_Prefab");
        if(tempVultoPrefab != null) {
            vultoManager.vultoMesh = tempVultoPrefab.GetComponentInChildren<MeshFilter>().sharedMesh;
            vultoManager.vultoMaterial = tempVultoPrefab.GetComponentInChildren<Renderer>().sharedMaterial;
        } else {
            GameObject tempVulto = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            vultoManager.vultoMesh = tempVulto.GetComponent<MeshFilter>().sharedMesh;
            vultoManager.vultoMaterial = tempVulto.GetComponent<Renderer>().sharedMaterial;
            vultoManager.vultoMaterial.color = Color.black;
            vultoManager.vultoMaterial.enableInstancing = true;
            Object.DestroyImmediate(tempVulto);
        }

        EntityEventSystem entitySys = orchestrator.AddComponent<EntityEventSystem>();
        entitySys.sanitySystem = sanity;

        SantaCompanaSystem santaCompana = orchestrator.AddComponent<SantaCompanaSystem>();
        santaCompana.playerTransform = player.transform;
        santaCompana.sanitySystem = sanity;
        GameObject santaObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        santaObj.name = "A_Santa_Compana";
        santaObj.SetActive(false);
        santaCompana.processionTransform = santaObj.transform;

        // 4.5 Setup do MapRenderer (AGORA COM PREFABS)
        MapRenderer mapRenderer = orchestrator.AddComponent<MapRenderer>();
        
        mapRenderer.treePrefab = Resources.Load<GameObject>("Prefabs/Tree_Prefab");
            mapRenderer.barricadePrefab = Resources.Load<GameObject>("Prefabs/Barricade_Prefab");
        mapRenderer.rockPrefab = Resources.Load<GameObject>("Prefabs/Rock_Prefab");
        mapRenderer.farolCeltaPrefab = Resources.Load<GameObject>("Prefabs/FarolCelta_Prefab");
        
        // Ruina (Usa Cubo como base caso nao tenha prefab)
        mapRenderer.ruinPrefab = Resources.Load<GameObject>("Prefabs/Ruin_Prefab");
        if(mapRenderer.ruinPrefab == null)
        {
            GameObject tempRuin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tempRuin.name = "Ruin_Prefab_Temp";
            tempRuin.GetComponent<Renderer>().sharedMaterial.color = Color.gray;
            mapRenderer.ruinPrefab = tempRuin; // Placeholder
            tempRuin.SetActive(false);
        }

        // 4.6 Setup do ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âudio e Sanidade Visual
        AudioManager audioManager = orchestrator.AddComponent<AudioManager>();
        audioManager.dayNightCycle = timeCycle;
        audioManager.sanitySystem = sanity;
        audioManager.vultoManager = vultoManager;
        
        GameObject audioGroup = new GameObject("Audio_Sources");
        audioGroup.transform.SetParent(orchestrator.transform);
        audioManager.ambientSource = audioGroup.AddComponent<AudioSource>();
        audioManager.sanityWhisperSource = audioGroup.AddComponent<AudioSource>();
        audioManager.vultoSwarmSource = audioGroup.AddComponent<AudioSource>();

        HeartbeatSynth heartbeat = audioGroup.AddComponent<HeartbeatSynth>();
        heartbeat.sanitySystem = orchestrator.GetComponent<SanitySystem>();
        
        PositionalAudioSystem posAudio = audioGroup.AddComponent<PositionalAudioSystem>();
        posAudio.playerTransform = player.transform;
        posAudio.dayNightCycle = orchestrator.GetComponent<DayNightCycle>();
        posAudio.audioManager = audioManager;

        GameObject ppVolume = new GameObject("PostProcessing_Volume");
        Volume volume = ppVolume.AddComponent<Volume>();
        volume.isGlobal = true;
        timeCycle.globalFogVolume = volume;

        Shader GetShaderSafely(string shaderName)
        {
            Shader s = Shader.Find(shaderName);
            if (s == null)
            {
                Debug.LogWarning($"[FragaSceneSetup] Shader '{shaderName}' not found. Falling back to 'Universal Render Pipeline/Lit'.");
                s = Shader.Find("Universal Render Pipeline/Lit");
                if (s == null) s = Shader.Find("Standard");
            }
            return s;
        }

        SanityVisualsManager sanityVisuals = orchestrator.AddComponent<SanityVisualsManager>();
        sanityVisuals.sanitySystem = sanity;
        sanityVisuals.postProcessVolume = volume;
        
        GameObject fakeVulto = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sanityVisuals.fakeVultoMesh = fakeVulto.GetComponent<MeshFilter>().sharedMesh;
        sanityVisuals.fakeVultoMaterial = new Material(GetShaderSafely("Fraga/VultoShadow_Instanced"));
        sanityVisuals.fakeVultoMaterial.color = Color.red; // AlucinaÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â§ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âµes vermelhas
        sanityVisuals.fakeVultoMaterial.enableInstancing = true;
        Object.DestroyImmediate(fakeVulto);

        // 4.7 Setup do Clima e Shaders Especiais
        WeatherSystem weather = orchestrator.AddComponent<WeatherSystem>();
        weather.dayNightCycle = timeCycle;
        weather.audioManager = audioManager;
        weather.playerTransform = player.transform;

        // Vulto e Arvores agora sao baseados nos Prefabs, nao precisamos sobrescrever aqui
        // Adicionar malha para Cavernas/Rochas (Agora no MapRenderer via Prefab)

        // 4.8 Setup da Santa Compana (Horde)
        GameObject hordeObj = new GameObject("Santa_Compana_Horde");
        hordeObj.SetActive(false); // Inicia desativado
        // Opcional: Adicionar um sistema de particulas e AudioSource aqui para a procissao
        ParticleSystem hordePS = hordeObj.AddComponent<ParticleSystem>();
        var em = hordePS.emission; em.rateOverTime = 50f;
        var sh = hordePS.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(8f, 2f, 2f);
        var main = hordePS.main; main.startColor = new Color(0.2f, 1.0f, 0.3f, 0.8f); // Verde fantasma

        santaCompana.processionTransform = hordeObj.transform;
        santaCompana.resourceManager = resManager;

        // Ligar ao ciclo de tempo
        timeCycle.santaCompanaSystem = santaCompana;

        // 4.9 Setup do Terreno Procedural
        GameObject terrainObj = new GameObject("Procedural_Terrain");
        TerrainMeshGenerator terrainGen = terrainObj.AddComponent<TerrainMeshGenerator>();
        mapRenderer.terrainGenerator = terrainGen;

        // 5. Setup da UI Mobile e Meta-ProgressÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£o (MonetizaÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â§ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£o)
        GameObject canvasObj = new GameObject("UI_Mobile_Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var cs = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        cs.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        
        GameObject touchFieldObj = new GameObject("CameraTouchZone");
        touchFieldObj.transform.SetParent(canvasObj.transform);
        UnityEngine.UI.Image touchImg = touchFieldObj.AddComponent<UnityEngine.UI.Image>();
        touchImg.color = new Color(0,0,0,0); // invisible
        RectTransform touchRT = touchFieldObj.GetComponent<RectTransform>();
        touchRT.anchorMin = new Vector2(0.5f, 0);
        touchRT.anchorMax = new Vector2(1, 1);
        touchRT.offsetMin = Vector2.zero;
        touchRT.offsetMax = Vector2.zero;
        CameraTouchController touchCtrl = touchFieldObj.AddComponent<CameraTouchController>();
        if (mainCam != null) {
            FirstPersonCamera fpCam = mainCam.GetComponent<FirstPersonCamera>();
            if (fpCam != null) fpCam.touchController = touchCtrl;
        }

        UIManager uiManager = canvasObj.AddComponent<UIManager>();
        uiManager.sanitySystem = sanity;
        uiManager.resourceManager = resManager;
        uiManager.dayNightCycle = timeCycle;

        HubManager hubManager = orchestrator.AddComponent<HubManager>();
        MonetizationManager adsManager = orchestrator.AddComponent<MonetizationManager>();
        adsManager.hubManager = hubManager;
        adsManager.playerSanity = sanity;

        LeaderboardManager leaderboard = orchestrator.AddComponent<LeaderboardManager>();
        leaderboard.dayNightCycle = timeCycle;

        // 6. InventÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡rio e Defesa
        InventorySystem inventory = orchestrator.AddComponent<InventorySystem>();
        inventory.buildingSystem = buildSys;
        inventory.sanitySystem = sanity;

        // 7. IA AvanÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â§ada (Diretor e Haptics)
        HordeDirector hordeDirector = orchestrator.AddComponent<HordeDirector>();
        hordeDirector.dayNightCycle = timeCycle;
        hordeDirector.vultoManager = vultoManager;
        hordeDirector.playerSanity = sanity;

        HapticManager haptic = orchestrator.AddComponent<HapticManager>();
        haptic.sanitySystem = sanity;

        // 8. PostProcessing AAA (Morte Cinematica, Dano Visual, Aberracao)
        PostProcessingManager ppManager = orchestrator.AddComponent<PostProcessingManager>();
        ppManager.sanitySystem = sanity;
        ppManager.hapticManager = haptic;
        ppManager.postProcessVolume = volume;

        // Death Overlay (CanvasGroup para fade-to-black)
        GameObject deathOverlayObj = new GameObject("Death_Overlay");
        deathOverlayObj.transform.SetParent(canvasObj.transform);
        UnityEngine.UI.Image deathBg = deathOverlayObj.AddComponent<UnityEngine.UI.Image>();
        deathBg.color = Color.black;
        deathBg.raycastTarget = false;
        CanvasGroup deathCG = deathOverlayObj.AddComponent<CanvasGroup>();
        deathCG.alpha = 0f;
        deathCG.blocksRaycasts = false;
        RectTransform deathRect = deathOverlayObj.GetComponent<RectTransform>();
        deathRect.anchorMin = Vector2.zero;
        deathRect.anchorMax = Vector2.one;
        deathRect.offsetMin = Vector2.zero;
        deathRect.offsetMax = Vector2.zero;
        ppManager.deathOverlay = deathCG;

        // Death Panel (Botoes de Revive e Return)
        GameObject deathPanel = new GameObject("Death_Panel");
        deathPanel.transform.SetParent(canvasObj.transform);
        RectTransform dpRect = deathPanel.AddComponent<RectTransform>();
        dpRect.anchorMin = new Vector2(0.2f, 0.3f);
        dpRect.anchorMax = new Vector2(0.8f, 0.7f);
        dpRect.offsetMin = Vector2.zero;
        dpRect.offsetMax = Vector2.zero;
        deathPanel.SetActive(false);
        uiManager.deathPanel = deathPanel;

        // 9. VFX Pool Manager
        GameObject vfxPoolObj = new GameObject("VFX_Pool_Manager");
        VFXPoolManager vfxPool = vfxPoolObj.AddComponent<VFXPoolManager>();

        // 10. Missoes Diarias (Retencao)
        DailyQuestManager dailyQuests = orchestrator.AddComponent<DailyQuestManager>();
        dailyQuests.hubManager = hubManager;

        // 11. IAP Manager (Loja Premium)
        IAPManager iapManager = orchestrator.AddComponent<IAPManager>();
        iapManager.hubManager = hubManager;
        iapManager.monetizationManager = adsManager;

        // 12. PlayerStatus (Stamina, Fome, Temperatura)
        PlayerStatus playerStatusSys = player.AddComponent<PlayerStatus>();
        playerStatusSys.playerController = pc;
        playerStatusSys.sanitySystem = sanity;
        playerStatusSys.dayNightCycle = timeCycle;
        weather.playerStatus = playerStatusSys;

        // 13. Inventario Avancado com Slots
        InventoryAdvanced advInventory = player.AddComponent<InventoryAdvanced>();
        advInventory.playerStatus = playerStatusSys;
        advInventory.sanitySystem = sanity;
        advInventory.buildingSystem = buildSys;

        // 14. Sistema de Crafting
        CraftingSystem crafting = player.AddComponent<CraftingSystem>();
        crafting.inventory = advInventory;
        crafting.playerStatus = playerStatusSys;

        // 15. Fauna (Cervos e Lobos)
        FaunaManager fauna = orchestrator.AddComponent<FaunaManager>();
        fauna.playerTransform = player.transform;
        fauna.playerSanity = sanity;
        fauna.dayNightCycle = timeCycle;
        fauna.inventory = advInventory;
        fauna.resourceManager = resManager;

        // Prefabs de fauna
        fauna.deerPrefab = Resources.Load<GameObject>("Prefabs/Deer_Prefab");
        if (fauna.deerPrefab == null)
        {
            GameObject tempDeer = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            tempDeer.name = "Deer_Placeholder";
            fauna.deerPrefab = tempDeer;
            tempDeer.SetActive(false);
            Material deerMat = new Material(GetShaderSafely("Universal Render Pipeline/Lit"));
            Texture2D tex3 = Resources.Load<Texture2D>("Textures/wolf_fur");
            if (tex3 != null) deerMat.SetTexture("_BaseMap", tex3);
            deerMat.color = new Color(0.6f, 0.4f, 0.2f);
            tempDeer.GetComponent<MeshRenderer>().sharedMaterial = deerMat;
        }

        fauna.wolfPrefab = Resources.Load<GameObject>("Prefabs/Wolf_Prefab");
        if (fauna.wolfPrefab == null)
        {
            GameObject tempWolf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tempWolf.name = "Wolf_Placeholder";
            fauna.wolfPrefab = tempWolf;
            tempWolf.SetActive(false);
            Material wolfMat = new Material(GetShaderSafely("Universal Render Pipeline/Lit"));
            Texture2D tex4 = Resources.Load<Texture2D>("Textures/wolf_fur");
            if (tex4 != null) wolfMat.SetTexture("_BaseMap", tex4);
            wolfMat.color = Color.gray;
            tempWolf.GetComponent<MeshRenderer>().sharedMaterial = wolfMat;
        }

        // 16. Ambiente do Mundo (Ervas, Cogumelos, Bagas)
        WorldEnvironment worldEnv = orchestrator.AddComponent<WorldEnvironment>();
        worldEnv.playerTransform = player.transform;
        worldEnv.inventory = advInventory;
        worldEnv.dayNightCycle = timeCycle;
        worldEnv.playerStatus = playerStatusSys;

        // Meshes de itens do mundo (primitivos de placeholder)
        GameObject tempHerb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        worldEnv.herbMesh = tempHerb.GetComponent<MeshFilter>().sharedMesh;
        worldEnv.herbMaterial = new Material(GetShaderSafely("Universal Render Pipeline/Lit"));
        worldEnv.herbMaterial.color = new Color(0.2f, 0.7f, 0.2f); // Verde
        worldEnv.herbMaterial.enableInstancing = true;
        Object.DestroyImmediate(tempHerb);

        GameObject tempMush = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        worldEnv.mushroomMesh = tempMush.GetComponent<MeshFilter>().sharedMesh;
        worldEnv.mushroomMaterial = new Material(GetShaderSafely("Universal Render Pipeline/Lit"));
        worldEnv.mushroomMaterial.color = new Color(0.6f, 0.2f, 0.5f); // Roxo escuro
        worldEnv.mushroomMaterial.enableInstancing = true;
        Object.DestroyImmediate(tempMush);

        GameObject tempBerry = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        worldEnv.berryMesh = tempBerry.GetComponent<MeshFilter>().sharedMesh;
        worldEnv.berryMaterial = new Material(GetShaderSafely("Universal Render Pipeline/Lit"));
        worldEnv.berryMaterial.color = new Color(0.8f, 0.1f, 0.2f); // Vermelho
        worldEnv.berryMaterial.enableInstancing = true;
        Object.DestroyImmediate(tempBerry);

        Sprite circleSprite = CreateCircleSprite(100);

        // Joystick Mobile
        GameObject joystickBg = new GameObject("Joystick_BG");
        joystickBg.transform.SetParent(canvasObj.transform);
        UnityEngine.UI.Image bgImg = joystickBg.AddComponent<UnityEngine.UI.Image>();
        bgImg.sprite = circleSprite;
        bgImg.color = new Color(0, 0, 0, 0.3f);
        RectTransform bgRect = joystickBg.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(200, 200);
        bgRect.anchoredPosition = new Vector2(150, 150);
        bgRect.anchorMin = new Vector2(0, 0);
        bgRect.anchorMax = new Vector2(0, 0);

        GameObject joystickHandle = new GameObject("Joystick_Handle");
        joystickHandle.transform.SetParent(joystickBg.transform);
        UnityEngine.UI.Image handleImg = joystickHandle.AddComponent<UnityEngine.UI.Image>();
        handleImg.sprite = circleSprite;
        handleImg.color = new Color(1, 1, 1, 0.8f);
        RectTransform handleRect = joystickHandle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(80, 80);

        MobileInputController mobileInput = joystickBg.AddComponent<MobileInputController>();
        mobileInput.joystickBackground = bgRect;
        mobileInput.joystickHandle = handleRect;
        pc.mobileInput = mobileInput;

        // 17. Associar o Orchestrator
        gm.playerTransform = player.transform;
        gm.buildingSystem = buildSys;
        gm.resourceManager = resManager;
        gm.entityEventSystem = entitySys;
        gm.vultoManager = vultoManager;
        gm.mapRenderer = mapRenderer;

        // Building System Removido (Movido para a seccao de Gestao)
        buildSys.inventory = advInventory;

        // 18. SaveSystem
        SaveSystem saveSys = orchestrator.AddComponent<SaveSystem>();
        saveSys.playerTransform = player.transform;
        saveSys.dayNightCycle = timeCycle;
        saveSys.sanitySystem = sanity;
        saveSys.playerStatus = playerStatusSys;
        saveSys.inventory = advInventory;

        // 19. Surface Audio SistÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©mico
        SurfaceAudio surfaceAudio = player.AddComponent<SurfaceAudio>();
        surfaceAudio.playerTransform = player.transform;
        surfaceAudio.resourceManager = resManager;
        surfaceAudio.playerController = pc;

        // 20. Performance Profiler (Analise FPS/GC)
        orchestrator.AddComponent<PerformanceProfiler>();

        // 21. Main Menu Nativo AvanÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â§ado (Zero Loading Screens com Seed Input)
        GameObject menuCanvasObj = new GameObject("MainMenu_Canvas");
        Canvas menuCanvas = menuCanvasObj.AddComponent<Canvas>();
        menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        menuCanvas.sortingOrder = 100;
        var mcs = menuCanvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        mcs.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        mcs.referenceResolution = new Vector2(1920, 1080);
        mcs.matchWidthOrHeight = 0.5f;
        menucanvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        
        GameObject touchFieldObj = new GameObject("CameraTouchZone");
        touchFieldObj.transform.SetParent(canvasObj.transform);
        UnityEngine.UI.Image touchImg = touchFieldObj.AddComponent<UnityEngine.UI.Image>();
        touchImg.color = new Color(0,0,0,0); // invisible
        RectTransform touchRT = touchFieldObj.GetComponent<RectTransform>();
        touchRT.anchorMin = new Vector2(0.5f, 0);
        touchRT.anchorMax = new Vector2(1, 1);
        touchRT.offsetMin = Vector2.zero;
        touchRT.offsetMax = Vector2.zero;
        CameraTouchController touchCtrl = touchFieldObj.AddComponent<CameraTouchController>();
        if (mainCam != null) {
            FirstPersonCamera fpCam = mainCam.GetComponent<FirstPersonCamera>();
            if (fpCam != null) fpCam.touchController = touchCtrl;
        }
        CanvasGroup menuCG = menuCanvasObj.AddComponent<CanvasGroup>();

        // Fundo escuro
        GameObject menuBg = new GameObject("Menu_Background");
        menuBg.transform.SetParent(menuCanvasObj.transform);
        UnityEngine.UI.Image bgImage = menuBg.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);
        RectTransform bgRT = menuBg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;

        // --- MAIN PANEL ---
        GameObject mainPanel = new GameObject("Main_Panel");
        mainPanel.transform.SetParent(menuCanvasObj.transform);
        RectTransform mainRT = mainPanel.AddComponent<RectTransform>();
        mainRT.anchorMin = Vector2.zero; mainRT.anchorMax = Vector2.one;
        mainRT.offsetMin = Vector2.zero; mainRT.offsetMax = Vector2.zero;

        GameObject titleObj = new GameObject("Game_Title");
        titleObj.transform.SetParent(mainPanel.transform);
        UnityEngine.UI.Text titleText = titleObj.AddComponent<UnityEngine.UI.Text>();
        titleText.text = "FRAGA\n<size=40>A Noite Esquecida</size>";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 80;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(0.8f, 0.1f, 0.1f);
        
        // --- PREMIUM AAA: Outline de sangue no Titulo ---
        UnityEngine.UI.Outline titleOutline = titleObj.AddComponent<UnityEngine.UI.Outline>();
        titleOutline.effectColor = new Color(0.02f, 0.02f, 0.02f, 0.95f);
        titleOutline.effectDistance = new Vector2(3f, -3f);

        RectTransform titleRT = titleObj.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 0.6f); titleRT.anchorMax = new Vector2(1, 1);
        titleRT.offsetMin = Vector2.zero; titleRT.offsetMax = Vector2.zero;

        // --- BOTOES DO MAIN MENU (COM JUICE) ---
        GameObject playBtnObj = new GameObject("Btn_Continue");
        playBtnObj.transform.SetParent(mainPanel.transform);
        UnityEngine.UI.Button playBtn = playBtnObj.AddComponent<UnityEngine.UI.Button>();
        playBtnObj.AddComponent<UnityEngine.UI.Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        playBtnObj.AddComponent<UIJuiceModifier>();
        playBtnObj.AddComponent<UnityEngine.UI.Shadow>().effectDistance = new Vector2(2f, -2f);
        GameObject pTextObj = new GameObject("Text"); pTextObj.transform.SetParent(playBtnObj.transform);
        UnityEngine.UI.Text pText = pTextObj.AddComponent<UnityEngine.UI.Text>(); pText.text = "CONTINUAR"; pText.font = titleText.font; pText.color = Color.white; pText.alignment = TextAnchor.MiddleCenter;
        RectTransform ptRT = pTextObj.GetComponent<RectTransform>(); ptRT.anchorMin = Vector2.zero; ptRT.anchorMax = Vector2.one; ptRT.offsetMin = Vector2.zero; ptRT.offsetMax = Vector2.zero;

        GameObject newBtnObj = new GameObject("Btn_NewGame");
        newBtnObj.transform.SetParent(mainPanel.transform);
        UnityEngine.UI.Button newBtn = newBtnObj.AddComponent<UnityEngine.UI.Button>();
        newBtnObj.AddComponent<UnityEngine.UI.Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        newBtnObj.AddComponent<UIJuiceModifier>();
        newBtnObj.AddComponent<UnityEngine.UI.Shadow>().effectDistance = new Vector2(2f, -2f);
        GameObject nTextObj = new GameObject("Text"); nTextObj.transform.SetParent(newBtnObj.transform);
        UnityEngine.UI.Text nText = nTextObj.AddComponent<UnityEngine.UI.Text>(); nText.text = "NOVO JOGO"; nText.font = titleText.font; nText.color = Color.white; nText.alignment = TextAnchor.MiddleCenter;
        RectTransform ntRT = nTextObj.GetComponent<RectTransform>(); ntRT.anchorMin = Vector2.zero; ntRT.anchorMax = Vector2.one; ntRT.offsetMin = Vector2.zero; ntRT.offsetMax = Vector2.zero;

        GameObject setBtnObj = new GameObject("Btn_Settings");
        setBtnObj.transform.SetParent(mainPanel.transform);
        UnityEngine.UI.Button setBtn = setBtnObj.AddComponent<UnityEngine.UI.Button>();
        setBtnObj.AddComponent<UnityEngine.UI.Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        setBtnObj.AddComponent<UIJuiceModifier>();
        setBtnObj.AddComponent<UnityEngine.UI.Shadow>().effectDistance = new Vector2(2f, -2f);
        GameObject sTextObj = new GameObject("Text"); sTextObj.transform.SetParent(setBtnObj.transform);
        UnityEngine.UI.Text sText = sTextObj.AddComponent<UnityEngine.UI.Text>(); sText.text = "OPCOES"; sText.font = titleText.font; sText.color = Color.white; sText.alignment = TextAnchor.MiddleCenter;
        RectTransform st2RT = sTextObj.GetComponent<RectTransform>(); st2RT.anchorMin = Vector2.zero; st2RT.anchorMax = Vector2.one; st2RT.offsetMin = Vector2.zero; st2RT.offsetMax = Vector2.zero;

        // Arranjo basico dos botoes
        playBtnObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);
        playBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 50);
        newBtnObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -60);
        newBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 50);
        setBtnObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -120);
        setBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 50);

        // --- NEW GAME PANEL ---
        GameObject newGamePanel = new GameObject("NewGame_Panel");
        newGamePanel.transform.SetParent(menuCanvasObj.transform);
        RectTransform ngRT = newGamePanel.AddComponent<RectTransform>();
        ngRT.anchorMin = Vector2.zero; ngRT.anchorMax = Vector2.one;
        ngRT.offsetMin = Vector2.zero; ngRT.offsetMax = Vector2.zero;

        GameObject seedInputObj = new GameObject("Seed_Input");
        seedInputObj.transform.SetParent(newGamePanel.transform);
        UnityEngine.UI.Image seedImg = seedInputObj.AddComponent<UnityEngine.UI.Image>(); seedImg.color = new Color(0,0,0, 0.5f);
        UnityEngine.UI.InputField seedField = seedInputObj.AddComponent<UnityEngine.UI.InputField>();
        GameObject seedTextObj = new GameObject("Text"); seedTextObj.transform.SetParent(seedInputObj.transform);
        UnityEngine.UI.Text seedText = seedTextObj.AddComponent<UnityEngine.UI.Text>(); seedText.font = titleText.font; seedText.color = Color.white;
        seedField.textComponent = seedText;
        RectTransform stRT = seedTextObj.GetComponent<RectTransform>(); stRT.anchorMin = Vector2.zero; stRT.anchorMax = Vector2.one; stRT.offsetMin = Vector2.zero; stRT.offsetMax = Vector2.zero;
        seedInputObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 50);
        seedInputObj.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 60);

        GameObject createBtnObj = new GameObject("Btn_CreateWorld");
        createBtnObj.transform.SetParent(newGamePanel.transform);
        UnityEngine.UI.Button createBtn = createBtnObj.AddComponent<UnityEngine.UI.Button>();
        createBtnObj.AddComponent<UnityEngine.UI.Image>().color = new Color(0.1f, 0.5f, 0.1f, 0.9f); // Verde Premium
        createBtnObj.AddComponent<UIJuiceModifier>();
        createBtnObj.AddComponent<UnityEngine.UI.Shadow>().effectDistance = new Vector2(2f, -2f);
        GameObject cTextObj = new GameObject("Text"); cTextObj.transform.SetParent(createBtnObj.transform);
        UnityEngine.UI.Text cText = cTextObj.AddComponent<UnityEngine.UI.Text>(); cText.text = "GERAR MUNDO"; cText.font = titleText.font; cText.color = Color.white; cText.alignment = TextAnchor.MiddleCenter;
        RectTransform ctRT = cTextObj.GetComponent<RectTransform>(); ctRT.anchorMin = Vector2.zero; ctRT.anchorMax = Vector2.one; ctRT.offsetMin = Vector2.zero; ctRT.offsetMax = Vector2.zero;
        createBtnObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -30);
        createBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 60);

        GameObject backNgBtnObj = new GameObject("Btn_Back");
        backNgBtnObj.transform.SetParent(newGamePanel.transform);
        UnityEngine.UI.Button backNgBtn = backNgBtnObj.AddComponent<UnityEngine.UI.Button>();
        backNgBtnObj.AddComponent<UnityEngine.UI.Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        backNgBtnObj.AddComponent<UIJuiceModifier>();
        GameObject bTextObj = new GameObject("Text"); bTextObj.transform.SetParent(backNgBtnObj.transform);
        UnityEngine.UI.Text bText = bTextObj.AddComponent<UnityEngine.UI.Text>(); bText.text = "VOLTAR"; bText.font = titleText.font; bText.color = Color.white; bText.alignment = TextAnchor.MiddleCenter;
        RectTransform btRT = bTextObj.GetComponent<RectTransform>(); btRT.anchorMin = Vector2.zero; btRT.anchorMax = Vector2.one; btRT.offsetMin = Vector2.zero; btRT.offsetMax = Vector2.zero;
        backNgBtnObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -100);
        backNgBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 40);

        // --- SETTINGS PANEL ---
        GameObject settingsPanel = new GameObject("Settings_Panel");
        settingsPanel.transform.SetParent(menuCanvasObj.transform);
        
        GameObject sliderObj = new GameObject("Volume_Slider");
        sliderObj.transform.SetParent(settingsPanel.transform);
        UnityEngine.UI.Slider volSlider = sliderObj.AddComponent<UnityEngine.UI.Slider>();

        GameObject backSetBtnObj = new GameObject("Btn_Back");
        backSetBtnObj.transform.SetParent(settingsPanel.transform);
        UnityEngine.UI.Button backSetBtn = backSetBtnObj.AddComponent<UnityEngine.UI.Button>();

        // Ligar ao MainMenuManager
        MainMenuManager mmm = menuCanvasObj.AddComponent<MainMenuManager>();
        mmm.mainMenuCanvasGroup = menuCG;
        mmm.saveSystem = saveSys;
        mmm.uiManager = uiManager;
        mmm.audioManager = audioManager;
        mmm.mainCamera = mainCam;
        mmm.gameManager = gm; // Ligar novo GameManager
        
        mmm.mainPanel = mainPanel;
        mmm.newGamePanel = newGamePanel;
        mmm.settingsPanel = settingsPanel;
        
        mmm.titleText = titleText;
        mmm.playButton = playBtn;
        mmm.btnOpenNewGame = newBtn;
        mmm.btnOpenSettings = setBtn;
        
        mmm.seedInput = seedField;
        mmm.btnCreateWorld = createBtn;
        mmm.btnBackFromNewGame = backNgBtn;

        mmm.masterVolumeSlider = volSlider;
        mmm.btnBackFromSettings = backSetBtn;

        // 22. LootManager (GPU Instancing para drops no chao)
        LootManager lootManager = orchestrator.AddComponent<LootManager>();
        lootManager.playerTransform = player.transform;
        lootManager.inventory = advInventory;

        // 23. SanityVisuals (URP Post-Processing via Fome e Sanidade) -> Substituido pelo PostProcessingManager

        // Limpar o EventSystem que pode faltar
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        Debug.LogWarning("[FRAGA NATIVE] Cena Completa AAA (Fauna + Craft + Inventario + Menu + PostProc) construida! Pressione PLAY.");
    }
}
