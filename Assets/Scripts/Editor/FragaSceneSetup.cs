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

    [MenuItem("Fraga Native/1. Criar Cena Completa (The Forest Mobile)")]
    public static void CreateGameScene()
    {
        // 0. Regenera materiais PBR (The Forest) e malhas 3D persistentes
        ProceduralMaterialGenerator.GenerateMaterials();
        FragaAssetGenerator.GenerateAssets();
        FragaModelBridge.SyncExternalModels();

        // Limpeza total da cena atual para garantir idempotencia e zero duplicatas
        var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var r in rootObjects)
        {
            Object.DestroyImmediate(r);
        }
        GameObject orchestrator = new GameObject("--- GAME ORCHESTRATOR ---");
        GameManager gm = orchestrator.AddComponent<GameManager>();
        
        // Setup de componentes
        GameObject playerPrefab = Resources.Load<GameObject>("Prefabs/Player_Prefab");
        GameObject player;
        if (playerPrefab != null) {
            player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        } else {
            player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        }
        player.name = "Iago (Topografo)";
        float startY = TerrainMeshGenerator.GetTerrainHeight(50f, 50f) + 1.0f;
        player.transform.position = new Vector3(50f, startY, 50f);

        // Remove colisores de membros que causavam forca de penetracao para o ceu
        foreach (var c in player.GetComponentsInChildren<Collider>())
        {
            Object.DestroyImmediate(c);
        }
        CapsuleCollider pCol = player.AddComponent<CapsuleCollider>();
        pCol.center = new Vector3(0, 1.0f, 0);
        pCol.radius = 0.35f;
        pCol.height = 1.9f;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb == null) rb = player.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.freezeRotation = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeAll;

        PlayerController pc = player.AddComponent<PlayerController>();
        SanitySystem sanity = player.AddComponent<SanitySystem>();
        BuildingSystem buildSys = player.AddComponent<BuildingSystem>();
        ResourceManager resManager = player.AddComponent<ResourceManager>();
        
        // Setup de componentes
        GameObject lamp = new GameObject("Lampiao");
        lamp.transform.SetParent(player.transform);
        lamp.transform.localPosition = new Vector3(0, 1f, 0);
        Light lightComp = lamp.AddComponent<Light>();
        lightComp.type = LightType.Point;
        lightComp.color = new Color(1.0f, 0.6f, 0.2f); // Fogo quente
        lightComp.range = 24f;
        lightComp.intensity = 3.5f;
        lightComp.shadows = LightShadows.Soft; // Setup de componentes
        sanity.lampLight = lightComp;

        // Setup de componentes
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.70f, 0.78f, 0.88f);
        RenderSettings.ambientEquatorColor = new Color(0.48f, 0.55f, 0.50f);
        RenderSettings.ambientGroundColor = new Color(0.32f, 0.35f, 0.28f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.55f, 0.65f, 0.72f);
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.0055f;

        GameObject sun = new GameObject("Sol_Direcional");
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        Light sunLight = sun.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        sunLight.color = new Color(0.85f, 0.90f, 0.95f);
        sunLight.intensity = 2.0f;
        sunLight.shadows = LightShadows.Soft;
        
        DayNightCycle timeCycle = orchestrator.AddComponent<DayNightCycle>();
        timeCycle.directionalLight = sunLight;
        
        AdvancedSkyboxSystem skySystem = orchestrator.AddComponent<AdvancedSkyboxSystem>();
        skySystem.dayNightCycle = timeCycle;
        skySystem.playerTransform = player.transform;
        
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            mainCam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
        }

        // 3. Setup de Camera Primeira Pessoa Imersiva (The Forest Mobile)
        mainCam.orthographic = false;
        mainCam.fieldOfView = 65f;
        mainCam.nearClipPlane = 0.08f;
        mainCam.farClipPlane = 350f;
        mainCam.gameObject.name = "Camera_PrimeiraPessoa";

        // Posicionar na altura dos olhos do personagem (1.75m)
        mainCam.transform.SetParent(player.transform);
        mainCam.transform.localPosition = new Vector3(0f, 1.75f, 0.15f);
        mainCam.transform.localRotation = Quaternion.identity;

        IsometricCamera oldIso = mainCam.GetComponent<IsometricCamera>();
        if (oldIso != null) Object.DestroyImmediate(oldIso);

        FirstPersonCamera fpCam = mainCam.GetComponent<FirstPersonCamera>();
        if (fpCam == null) fpCam = mainCam.gameObject.AddComponent<FirstPersonCamera>();
        fpCam.playerBody = player.transform;
        fpCam.baseFOV = 65f;
        fpCam.sprintFOV = 78f;

        // Viewmodel / Suporte para segurar machado/tocha estilo The Forest
        GameObject weaponSlot = new GameObject("Weapon_Holder_FP");
        weaponSlot.transform.SetParent(mainCam.transform);
        weaponSlot.transform.localPosition = new Vector3(0.32f, -0.28f, 0.52f);
        weaponSlot.transform.localRotation = Quaternion.Euler(5f, -10f, 0f);
        weaponSlot.AddComponent<WeaponMotionSway>();

        GameObject axePrefab = Resources.Load<GameObject>("Prefabs/Axe_Prefab");
        if (axePrefab != null)
        {
            GameObject heldAxe = (GameObject)PrefabUtility.InstantiatePrefab(axePrefab);
            heldAxe.name = "Held_Survival_Axe";
            heldAxe.transform.SetParent(weaponSlot.transform);
            heldAxe.transform.localPosition = Vector3.zero;
            heldAxe.transform.localRotation = Quaternion.Euler(15f, 185f, -20f);
            heldAxe.transform.localScale = Vector3.one * 0.75f;
            foreach (var c in heldAxe.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
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
        mapRenderer.pineTreePrefab = Resources.Load<GameObject>("Prefabs/PineTree_Prefab");
        mapRenderer.deadTreePrefab = Resources.Load<GameObject>("Prefabs/DeadTree_Prefab");
        mapRenderer.fentoPrefab = Resources.Load<GameObject>("Prefabs/Fento_Prefab");
        mapRenderer.barricadePrefab = Resources.Load<GameObject>("Prefabs/Barricade_Prefab");
        mapRenderer.rockPrefab = Resources.Load<GameObject>("Prefabs/Rock_Prefab");
        mapRenderer.berryPrefab = Resources.Load<GameObject>("Prefabs/Berry_Prefab");
        mapRenderer.mushroomPrefab = Resources.Load<GameObject>("Prefabs/Mushroom_Prefab");
        mapRenderer.pondPrefab = Resources.Load<GameObject>("Prefabs/Pond_Prefab");
        mapRenderer.waterCatcherPrefab = Resources.Load<GameObject>("Prefabs/WaterCatcher_Prefab");
        mapRenderer.workbenchPrefab = Resources.Load<GameObject>("Prefabs/Workbench_Prefab");
        mapRenderer.farmlandPrefab = Resources.Load<GameObject>("Prefabs/Farmland_Prefab");
        mapRenderer.cauldronPrefab = Resources.Load<GameObject>("Prefabs/Cauldron_Prefab");
        mapRenderer.snareTrapPrefab = Resources.Load<GameObject>("Prefabs/SnareTrap_Prefab");
        mapRenderer.runicStonePrefab = Resources.Load<GameObject>("Prefabs/RunicStone_Prefab");
        mapRenderer.fallenLogPrefab = Resources.Load<GameObject>("Prefabs/FallenLog_Prefab");
        mapRenderer.thornyBushPrefab = Resources.Load<GameObject>("Prefabs/ThornyBush_Prefab");
        mapRenderer.glowMushroomPrefab = Resources.Load<GameObject>("Prefabs/GlowMushroom_Prefab");
        mapRenderer.celticCrossPrefab = Resources.Load<GameObject>("Prefabs/CelticCross_Prefab");
        mapRenderer.ancientGravePrefab = Resources.Load<GameObject>("Prefabs/AncientGrave_Prefab");
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

        // Setup de componentes
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
        volume.profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/DefaultVolumeProfile.asset");
        if (volume.profile == null)
        {
            volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        }
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
        sanityVisuals.fakeVultoMaterial.color = Color.red; // Setup de componentes
        sanityVisuals.fakeVultoMaterial.enableInstancing = true;
        Object.DestroyImmediate(fakeVulto);

        // 4.7 Setup do Clima e Shaders Especiais
        WeatherSystem weather = orchestrator.AddComponent<WeatherSystem>();
        weather.dayNightCycle = timeCycle;
        weather.audioManager = audioManager;
        weather.playerTransform = player.transform;

        // Efeitos Atmosf?ricos estilo The Forest (Poeira, Vaga-lumes, N?voa da Madrugada e Folhas ao Vento)
        AtmosphericEffectsSystem atmos = orchestrator.AddComponent<AtmosphericEffectsSystem>();
        atmos.dayNightCycle = timeCycle;
        atmos.weatherSystem = weather;
        atmos.playerTransform = player.transform;

        // Relva Procedural densa estilo The Forest com GPU Instancing e vento
        ProceduralGrassSystem grassSys = orchestrator.AddComponent<ProceduralGrassSystem>();
        grassSys.playerTransform = player.transform;
        grassSys.weatherSystem = weather;

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

        // Setup de componentes
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
        if (fpCam != null) {
            fpCam.touchController = touchCtrl;
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

        // Setup de componentes
        InventorySystem inventory = orchestrator.AddComponent<InventorySystem>();
        inventory.buildingSystem = buildSys;
        inventory.sanitySystem = sanity;

        // Setup de componentes
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

        // Death Panel AAA (Painel Interativo de Morte e Ressurreição)
        GameObject deathPanel = new GameObject("Death_Panel");
        deathPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform dpRect = deathPanel.AddComponent<RectTransform>();
        dpRect.anchorMin = Vector2.zero;
        dpRect.anchorMax = Vector2.one;
        dpRect.offsetMin = Vector2.zero;
        dpRect.offsetMax = Vector2.zero;
        UnityEngine.UI.Image dpImg = deathPanel.AddComponent<UnityEngine.UI.Image>();
        dpImg.color = new Color(0.04f, 0.01f, 0.01f, 0.95f);
        dpImg.raycastTarget = true;

        GameObject deathModal = new GameObject("Death_Modal");
        deathModal.transform.SetParent(deathPanel.transform, false);
        RectTransform dmRect = deathModal.AddComponent<RectTransform>();
        dmRect.anchoredPosition = Vector2.zero;
        dmRect.sizeDelta = new Vector2(680, 360);
        UnityEngine.UI.Image dmImg = deathModal.AddComponent<UnityEngine.UI.Image>();
        dmImg.color = new Color(0.08f, 0.02f, 0.02f, 0.95f);
        var dmOutline = deathModal.AddComponent<UnityEngine.UI.Outline>();
        dmOutline.effectColor = new Color(0.75f, 0.15f, 0.15f, 0.85f);
        dmOutline.effectDistance = new Vector2(2, -2);

        GameObject deathTitle = new GameObject("Death_Title");
        deathTitle.transform.SetParent(deathModal.transform, false);
        RectTransform dtRect = deathTitle.AddComponent<RectTransform>();
        dtRect.anchoredPosition = new Vector2(0, 95);
        dtRect.sizeDelta = new Vector2(620, 60);
        UnityEngine.UI.Text dtText = deathTitle.AddComponent<UnityEngine.UI.Text>();
        dtText.text = "A FRAGA TE RECLAMOU";
        dtText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        dtText.fontSize = 32;
        dtText.fontStyle = FontStyle.Bold;
        dtText.alignment = TextAnchor.MiddleCenter;
        dtText.color = new Color(0.95f, 0.20f, 0.20f, 1f);
        var dtShadow = deathTitle.AddComponent<UnityEngine.UI.Shadow>();
        dtShadow.effectColor = Color.black;
        dtShadow.effectDistance = new Vector2(2f, -2f);

        GameObject deathSub = new GameObject("Death_Subtitle");
        deathSub.transform.SetParent(deathModal.transform, false);
        RectTransform dsRect = deathSub.AddComponent<RectTransform>();
        dsRect.anchoredPosition = new Vector2(0, 35);
        dsRect.sizeDelta = new Vector2(580, 45);
        UnityEngine.UI.Text dsText = deathSub.AddComponent<UnityEngine.UI.Text>();
        dsText.text = "A escuridão consumiu tuas forças vitais.\nA luz da alvorada é a tua única esperança.";
        dsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        dsText.fontSize = 17;
        dsText.alignment = TextAnchor.MiddleCenter;
        dsText.color = new Color(0.85f, 0.85f, 0.85f, 0.90f);

        GameObject reviveBtnObj = new GameObject("Btn_Death_Revive");
        reviveBtnObj.transform.SetParent(deathModal.transform, false);
        RectTransform rBtnRect = reviveBtnObj.AddComponent<RectTransform>();
        rBtnRect.anchoredPosition = new Vector2(0, -40);
        rBtnRect.sizeDelta = new Vector2(300, 52);
        UnityEngine.UI.Image rBtnImg = reviveBtnObj.AddComponent<UnityEngine.UI.Image>();
        rBtnImg.color = new Color(0.40f, 0.12f, 0.12f, 1f);
        var rOutline = reviveBtnObj.AddComponent<UnityEngine.UI.Outline>();
        rOutline.effectColor = new Color(1.0f, 0.35f, 0.35f, 0.9f);
        rOutline.effectDistance = new Vector2(1.5f, -1.5f);
        UnityEngine.UI.Button rBtn = reviveBtnObj.AddComponent<UnityEngine.UI.Button>();
        rBtn.onClick.AddListener(() =>
        {
            if (gm != null) gm.RevivePlayer();
            if (uiManager != null) uiManager.HideDeathScreen();
        });
        CreateButtonLabel(reviveBtnObj.transform, "☀ RENASCER NA ALVORADA", 16);

        GameObject menuBtnObj = new GameObject("Btn_Death_Menu");
        menuBtnObj.transform.SetParent(deathModal.transform, false);
        RectTransform mBtnRect = menuBtnObj.AddComponent<RectTransform>();
        mBtnRect.anchoredPosition = new Vector2(0, -100);
        mBtnRect.sizeDelta = new Vector2(240, 42);
        UnityEngine.UI.Image mBtnImg = menuBtnObj.AddComponent<UnityEngine.UI.Image>();
        mBtnImg.color = new Color(0.18f, 0.20f, 0.25f, 0.95f);
        var mOutline = menuBtnObj.AddComponent<UnityEngine.UI.Outline>();
        mOutline.effectColor = new Color(0.45f, 0.55f, 0.75f, 0.7f);
        mOutline.effectDistance = new Vector2(1.5f, -1.5f);
        UnityEngine.UI.Button mBtn = menuBtnObj.AddComponent<UnityEngine.UI.Button>();
        mBtn.onClick.AddListener(() =>
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        });
        CreateButtonLabel(menuBtnObj.transform, "MENU PRINCIPAL", 14);

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
        if (skySystem != null) skySystem.playerStatusRef = playerStatusSys;

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

        fauna.bearPrefab = Resources.Load<GameObject>("Prefabs/Bear_Prefab");
        fauna.boarPrefab = Resources.Load<GameObject>("Prefabs/Boar_Prefab");
        fauna.rabbitPrefab = Resources.Load<GameObject>("Prefabs/Rabbit_Prefab");
        fauna.maxDeer = 12;
        fauna.maxWolves = 6;
        fauna.maxBears = 3;
        fauna.maxBoars = 5;
        fauna.maxRabbits = 14;

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

        // 16.5 Joystick Mobile Responsivo e Touch Zone (The Forest AAA)
        GameObject joystickZone = new GameObject("Joystick_TouchZone");
        joystickZone.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image zoneImg = joystickZone.AddComponent<UnityEngine.UI.Image>();
        zoneImg.color = new Color(0, 0, 0, 0);
        zoneImg.raycastTarget = true;
        RectTransform zoneRect = joystickZone.GetComponent<RectTransform>();
        zoneRect.anchorMin = new Vector2(0, 0);
        zoneRect.anchorMax = new Vector2(0.48f, 0.65f);
        zoneRect.offsetMin = Vector2.zero;
        zoneRect.offsetMax = Vector2.zero;

        GameObject joystickBg = new GameObject("Joystick_BG");
        joystickBg.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image bgImg = joystickBg.AddComponent<UnityEngine.UI.Image>();
        bgImg.sprite = circleSprite;
        bgImg.color = new Color(0.08f, 0.1f, 0.12f, 0.45f);
        bgImg.raycastTarget = false;
        RectTransform bgRect = joystickBg.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(180, 180);
        bgRect.anchorMin = new Vector2(0, 0);
        bgRect.anchorMax = new Vector2(0, 0);
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.anchoredPosition = new Vector2(160, 160);

        GameObject joystickHandle = new GameObject("Joystick_Handle");
        joystickHandle.transform.SetParent(joystickBg.transform, false);
        UnityEngine.UI.Image handleImg = joystickHandle.AddComponent<UnityEngine.UI.Image>();
        handleImg.sprite = circleSprite;
        handleImg.color = new Color(1f, 1f, 1f, 0.85f);
        handleImg.raycastTarget = false;
        RectTransform handleRect = joystickHandle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(75, 75);
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero;

        MobileInputController mobileInput = joystickZone.AddComponent<MobileInputController>();
        mobileInput.joystickBackground = bgRect;
        mobileInput.joystickHandle = handleRect;
        mobileInput.joystickRadius = 80f;
        mobileInput.deadZone = 6f;
        mobileInput.joystickType = MobileInputController.JoystickType.DynamicFloatingFollow;
        pc.mobileInput = mobileInput;

        // Sistema de Combate e Corte de Madeira AAA
        ChoppingAndCombatSystem combatSys = player.AddComponent<ChoppingAndCombatSystem>();
        combatSys.playerCamera = Camera.main;
        combatSys.playerController = pc;
        combatSys.inventory = advInventory;

        // Interface de Inventário e Crafting (Mochila de Sobrevivência)
        SurvivalInventoryCraftUI craftUI = canvasObj.AddComponent<SurvivalInventoryCraftUI>();
        craftUI.inventory = advInventory;
        craftUI.craftingSystem = crafting;
        craftUI.playerStatus = playerStatusSys;
        craftUI.cameraTouchZone = touchCtrl;

        // --- CLUSTER DE BOTÕES DE AÇÃO MOBILE (The Forest AAA Layout) ---
        // 1. Botão de Golpe / Corte de Madeira (🪓)
        GameObject atkBtnObj = new GameObject("Btn_Mobile_Attack");
        atkBtnObj.transform.SetParent(canvasObj.transform, false);
        RectTransform atkRT = atkBtnObj.AddComponent<RectTransform>();
        atkRT.anchorMin = new Vector2(1, 0); atkRT.anchorMax = new Vector2(1, 0); atkRT.pivot = new Vector2(1, 0);
        atkRT.anchoredPosition = new Vector2(-65, 110);
        atkRT.sizeDelta = new Vector2(88, 88);
        UnityEngine.UI.Image atkImg = atkBtnObj.AddComponent<UnityEngine.UI.Image>();
        atkImg.sprite = circleSprite;
        atkImg.color = new Color(0.20f, 0.32f, 0.44f, 0.85f);
        var atkOutline = atkBtnObj.AddComponent<UnityEngine.UI.Outline>();
        atkOutline.effectColor = new Color(0.35f, 0.85f, 1.0f, 0.45f);
        atkOutline.effectDistance = new Vector2(2, -2);
        UnityEngine.UI.Button atkBtn = atkBtnObj.AddComponent<UnityEngine.UI.Button>();
        atkBtn.onClick.AddListener(() => { if (combatSys != null) combatSys.TriggerAttack(); });
        CreateButtonLabel(atkBtnObj.transform, "🪓\nGOLPE", 13);

        // 2. Botão de Pulo (▲)
        GameObject jumpBtnObj = new GameObject("Btn_Mobile_Jump");
        jumpBtnObj.transform.SetParent(canvasObj.transform, false);
        RectTransform jumpRT = jumpBtnObj.AddComponent<RectTransform>();
        jumpRT.anchorMin = new Vector2(1, 0); jumpRT.anchorMax = new Vector2(1, 0); jumpRT.pivot = new Vector2(1, 0);
        jumpRT.anchoredPosition = new Vector2(-65, 220);
        jumpRT.sizeDelta = new Vector2(68, 68);
        UnityEngine.UI.Image jumpImg = jumpBtnObj.AddComponent<UnityEngine.UI.Image>();
        jumpImg.sprite = circleSprite;
        jumpImg.color = new Color(0.14f, 0.20f, 0.28f, 0.75f);
        var jumpOutline = jumpBtnObj.AddComponent<UnityEngine.UI.Outline>();
        jumpOutline.effectColor = new Color(0.35f, 0.85f, 1.0f, 0.35f);
        jumpOutline.effectDistance = new Vector2(1.5f, -1.5f);
        UnityEngine.UI.Button jumpBtn = jumpBtnObj.AddComponent<UnityEngine.UI.Button>();
        jumpBtn.onClick.AddListener(() => { if (pc != null) pc.TryJump(); });
        CreateButtonLabel(jumpBtnObj.transform, "▲\nPULAR", 11);

        // 3. Botão de Coleta / Interação (✋)
        GameObject interactBtnObj = new GameObject("Btn_Mobile_Interact");
        interactBtnObj.transform.SetParent(canvasObj.transform, false);
        RectTransform intRT = interactBtnObj.AddComponent<RectTransform>();
        intRT.anchorMin = new Vector2(1, 0); intRT.anchorMax = new Vector2(1, 0); intRT.pivot = new Vector2(1, 0);
        intRT.anchoredPosition = new Vector2(-165, 110);
        intRT.sizeDelta = new Vector2(68, 68);
        UnityEngine.UI.Image intImg = interactBtnObj.AddComponent<UnityEngine.UI.Image>();
        intImg.sprite = circleSprite;
        intImg.color = new Color(0.14f, 0.20f, 0.28f, 0.75f);
        var intOutline = interactBtnObj.AddComponent<UnityEngine.UI.Outline>();
        intOutline.effectColor = new Color(0.35f, 0.85f, 1.0f, 0.35f);
        intOutline.effectDistance = new Vector2(1.5f, -1.5f);
        UnityEngine.UI.Button intBtn = interactBtnObj.AddComponent<UnityEngine.UI.Button>();
        intBtn.onClick.AddListener(() => { if (resManager != null) resManager.TryHarvest(); });
        CreateButtonLabel(interactBtnObj.transform, "✋\nCOLETAR", 11);

        // 4. Botão de Mochila / Crafting (🎒)
        GameObject bagBtnObj = new GameObject("Btn_Mobile_Inventory");
        bagBtnObj.transform.SetParent(canvasObj.transform, false);
        RectTransform bagRT = bagBtnObj.AddComponent<RectTransform>();
        bagRT.anchorMin = new Vector2(1, 0); bagRT.anchorMax = new Vector2(1, 0); bagRT.pivot = new Vector2(1, 0);
        bagRT.anchoredPosition = new Vector2(-165, 220);
        bagRT.sizeDelta = new Vector2(68, 68);
        UnityEngine.UI.Image bagImg = bagBtnObj.AddComponent<UnityEngine.UI.Image>();
        bagImg.sprite = circleSprite;
        bagImg.color = new Color(0.14f, 0.26f, 0.36f, 0.85f);
        var bagOutline = bagBtnObj.AddComponent<UnityEngine.UI.Outline>();
        bagOutline.effectColor = new Color(0.4f, 0.9f, 1.0f, 0.45f);
        bagOutline.effectDistance = new Vector2(1.5f, -1.5f);
        UnityEngine.UI.Button bagBtn = bagBtnObj.AddComponent<UnityEngine.UI.Button>();
        bagBtn.onClick.AddListener(() => { if (craftUI != null) craftUI.ToggleMenu(); });
        CreateButtonLabel(bagBtnObj.transform, "🎒\nMOCHILA", 11);

        // 5. Botão de Agachamento (▼)
        GameObject crouchBtnObj = new GameObject("Btn_Mobile_Crouch");
        crouchBtnObj.transform.SetParent(canvasObj.transform, false);
        RectTransform crRT = crouchBtnObj.AddComponent<RectTransform>();
        crRT.anchorMin = new Vector2(1, 0); crRT.anchorMax = new Vector2(1, 0); crRT.pivot = new Vector2(1, 0);
        crRT.anchoredPosition = new Vector2(-255, 110);
        crRT.sizeDelta = new Vector2(60, 60);
        UnityEngine.UI.Image crImg = crouchBtnObj.AddComponent<UnityEngine.UI.Image>();
        crImg.sprite = circleSprite;
        crImg.color = new Color(0.12f, 0.16f, 0.22f, 0.70f);
        var crOutline = crouchBtnObj.AddComponent<UnityEngine.UI.Outline>();
        crOutline.effectColor = new Color(0.35f, 0.85f, 1.0f, 0.30f);
        crOutline.effectDistance = new Vector2(1.5f, -1.5f);
        UnityEngine.UI.Button crBtn = crouchBtnObj.AddComponent<UnityEngine.UI.Button>();
        crBtn.onClick.AddListener(() => { if (pc != null) pc.ToggleCrouch(); });
        CreateButtonLabel(crouchBtnObj.transform, "▼\nAGACHO", 10);

        // 6. Botão de Lanterna / Luz (🏮)
        GameObject lanternBtnObj = new GameObject("Btn_Mobile_Lantern");
        lanternBtnObj.transform.SetParent(canvasObj.transform, false);
        RectTransform lanRT = lanternBtnObj.AddComponent<RectTransform>();
        lanRT.anchorMin = new Vector2(1, 0); lanRT.anchorMax = new Vector2(1, 0); lanRT.pivot = new Vector2(1, 0);
        lanRT.anchoredPosition = new Vector2(-165, 330);
        lanRT.sizeDelta = new Vector2(68, 68);
        UnityEngine.UI.Image lanImg = lanternBtnObj.AddComponent<UnityEngine.UI.Image>();
        lanImg.sprite = circleSprite;
        lanImg.color = new Color(0.35f, 0.26f, 0.10f, 0.85f);
        var lanOutline = lanternBtnObj.AddComponent<UnityEngine.UI.Outline>();
        lanOutline.effectColor = new Color(1.0f, 0.82f, 0.25f, 0.55f);
        lanOutline.effectDistance = new Vector2(1.5f, -1.5f);
        UnityEngine.UI.Button lanBtn = lanternBtnObj.AddComponent<UnityEngine.UI.Button>();
        lanBtn.onClick.AddListener(() => { if (sanity != null) sanity.ToggleLamp(); });
        CreateButtonLabel(lanternBtnObj.transform, "🏮\nLUZ", 11);


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

        // Setup de componentes
        SurfaceAudio surfaceAudio = player.AddComponent<SurfaceAudio>();
        surfaceAudio.playerTransform = player.transform;
        surfaceAudio.resourceManager = resManager;
        surfaceAudio.playerController = pc;

        // 20. Performance Profiler (Analise FPS/GC)
        orchestrator.AddComponent<PerformanceProfiler>();

        // Setup de componentes
        GameObject menuCanvasObj = new GameObject("MainMenu_Canvas");
        Canvas menuCanvas = menuCanvasObj.AddComponent<Canvas>();
        menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        menuCanvas.sortingOrder = 100;
        var mcs = menuCanvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        mcs.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        mcs.referenceResolution = new Vector2(1920, 1080);
        mcs.matchWidthOrHeight = 0.5f;
        menuCanvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
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

        if (ppManager != null)
        {
            ppManager.playerStatus = playerStatusSys;
        }

        // Limpar o EventSystem que pode faltar
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Scenes/GameScene.unity");
        Debug.LogWarning("[FRAGA NATIVE] Cena Completa AAA construida e salva no disco! Pressione PLAY.");
    }
    private static void CreateButtonLabel(Transform parent, string text, int fontSize)
    {
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(parent, false);
        UnityEngine.UI.Text t = textObj.AddComponent<UnityEngine.UI.Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(0.95f, 0.95f, 0.95f, 0.95f);
        t.raycastTarget = false;
        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
