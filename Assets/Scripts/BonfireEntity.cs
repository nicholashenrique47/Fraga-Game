using UnityEngine;
using System.Collections.Generic;

public class BonfireEntity : MonoBehaviour
{
    public static readonly List<BonfireEntity> AllBonfires = new List<BonfireEntity>();

    public const int MaxCookSlots = 3;

    public enum CookStage
    {
        Empty = 0,
        Cooking = 1,
        Ready = 2,
        Burnt = 3
    }

    [System.Serializable]
    public struct CookSlotState
    {
        public int rawItemId;
        public int resultItemId;
        public float timer;
        public float duration;
        public float overcookTimer;
        public CookStage stage;

        public float Progress01 => duration > 0.01f ? Mathf.Clamp01(timer / duration) : 0f;
        public bool IsOccupied => stage != CookStage.Empty && rawItemId > 0;
    }

    [Header("Fuel System")]
    public float fuelLevel = 100f; // 0 a 100
    public float burnRate = 0.85f; // Drena X por segundo (~2 min por carga completa)
    public Light fireLight;
    public ParticleSystem fireParticles;

    [Header("Cooking System (Legacy + Multi-Slot 3D)")]
    public bool isCooking = false;
    public float cookTimer = 0f;
    public float cookDuration = 8.5f;
    public float overcookGraceDuration = 18f;
    public int itemBeingCooked = -1; // -1 = Vazio, 104/113 = Carne, 111 = Peixe, 103 = Ervas, 123 = Agua Turva

    public bool isLit { get; private set; } = true;

    public event System.Action OnBonfireStateChanged;

    private readonly CookSlotState[] cookSlots = new CookSlotState[MaxCookSlots];
    private readonly MeshRenderer[] grillRenderers = new MeshRenderer[MaxCookSlots];
    private readonly Transform[] grillSlotTransforms = new Transform[MaxCookSlots];
    private bool visualsBuilt = false;
    private AudioSource fireAudioSource;

    private static Material s_rawMeatMat;
    private static Material s_cookedMeatMat;
    private static Material s_waterPotMat;
    private static Material s_boiledWaterMat;
    private static Material s_burntMat;

    private GalicianWeatherEngine weatherCache;
    private LootManager lootCache;
    private float nextNearWarningCheckTime = 0f;

    private static readonly Vector3[] SlotLocalOffsets =
    {
        new Vector3(-0.26f, 0.28f, 0.14f),
        new Vector3( 0.26f, 0.28f, 0.14f),
        new Vector3( 0.00f, 0.28f, -0.24f)
    };

    private static readonly int[] AutoCookPriorityIds = { 104, 113, 111, 123 };

    public int ReadySlotCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < MaxCookSlots; i++)
            {
                if (cookSlots[i].stage == CookStage.Ready || cookSlots[i].stage == CookStage.Burnt) count++;
            }
            return count;
        }
    }

    public int CookingSlotCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < MaxCookSlots; i++)
            {
                if (cookSlots[i].stage == CookStage.Cooking) count++;
            }
            return count;
        }
    }

    public int EmptySlotCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < MaxCookSlots; i++)
            {
                if (cookSlots[i].stage == CookStage.Empty) count++;
            }
            return count;
        }
    }

    public CookSlotState GetSlotState(int index)
    {
        if (index < 0 || index >= MaxCookSlots) return default;
        return cookSlots[index];
    }

    public void RegisterInGlobalList()
    {
        if (!AllBonfires.Contains(this)) AllBonfires.Add(this);
    }

    public void UnregisterFromGlobalList()
    {
        AllBonfires.Remove(this);
    }

    private void Awake()
    {
        RegisterInGlobalList();
    }

    private void OnEnable()
    {
        RegisterInGlobalList();
    }

    private void OnDisable()
    {
        UnregisterFromGlobalList();
    }

    private void OnDestroy()
    {
        UnregisterFromGlobalList();
    }

    private void Start()
    {
        weatherCache = FindAnyObjectByType<GalicianWeatherEngine>();
        lootCache = LootManager.Instance ?? FindAnyObjectByType<LootManager>();
        EnsureVisuals();
        RefreshGrillVisuals();
    }

    private void Update()
    {
        if (!isLit) return;

        bool isRaining = weatherCache != null && weatherCache.isRaining;
        AdvanceSimulation(Time.deltaTime, isRaining, allowOvercookBurn: true);
        UpdateVisualAnimations();
    }

    public void AdvanceSimulation(float deltaTime, bool isRaining, bool allowOvercookBurn = true)
    {
        if (!isLit || deltaTime <= 0f) return;

        // Drena combustivel (chuva consome lenha mais depressa)
        float weatherMult = isRaining ? 1.45f : 1f;
        fuelLevel -= burnRate * weatherMult * deltaTime;
        if (fuelLevel <= 0f)
        {
            fuelLevel = 0f;
            Extinguish();
            return;
        }

        // Visuais de Fogo ligados ao combustivel
        if (fireLight != null)
        {
            float flicker = 0.90f + Mathf.Sin(Time.time * 11f + transform.position.x) * 0.10f;
            fireLight.intensity = Mathf.Lerp(0.75f, 4.0f, fuelLevel / 100f) * flicker;
            fireLight.range = Mathf.Lerp(3.0f, 10.5f, fuelLevel / 100f);
        }
        UpdateFireAudioState();

        bool anyStageChanged = false;
        for (int i = 0; i < MaxCookSlots; i++)
        {
            CookSlotState slot = cookSlots[i];
            if (slot.stage == CookStage.Cooking)
            {
                slot.timer += deltaTime;
                if (slot.timer >= slot.duration)
                {
                    slot.timer = slot.duration;
                    slot.stage = CookStage.Ready;
                    slot.overcookTimer = 0f;
                    anyStageChanged = true;
                    NotifyNearbyPlayerCookReady(slot.resultItemId);
                }
                cookSlots[i] = slot;
            }
            else if (slot.stage == CookStage.Ready && allowOvercookBurn && CanItemBurn(slot.rawItemId))
            {
                slot.overcookTimer += deltaTime;
                if (slot.overcookTimer >= overcookGraceDuration)
                {
                    slot.stage = CookStage.Burnt;
                    slot.resultItemId = 122; // Carvao Vegetal
                    anyStageChanged = true;
                    NotifyNearbyPlayerBurnt();
                }
                cookSlots[i] = slot;
            }
        }

        SyncLegacyFields();
        if (anyStageChanged)
        {
            RefreshGrillVisuals();
            OnBonfireStateChanged?.Invoke();
        }
    }

    private void NotifyNearbyPlayerCookReady(int resultItemId)
    {
        if (HUDSurvivalOverlay.Instance == null || PlayerController.Instance == null) return;
        if ((PlayerController.Instance.transform.position - transform.position).sqrMagnitude > 144f) return;

        ItemData d = ItemDatabase.GetItem(resultItemId);
        string itemName = d != null ? d.itemName : "Alimento";
        HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#FFDD66>Fogueira: {itemName} pronto para retirar [E]!</color>");
    }

    private void NotifyNearbyPlayerBurnt()
    {
        if (HUDSurvivalOverlay.Instance == null || PlayerController.Instance == null) return;
        if ((PlayerController.Instance.transform.position - transform.position).sqrMagnitude > 144f) return;

        HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#FF7755>Fogueira: alimento passou do ponto e virou Carvao Vegetal!</color>");
    }

    public static bool CanItemBurn(int rawItemId)
    {
        return rawItemId != 123; // Agua fervida nao vira carvao
    }

    public static int ResolveCookOutput(int rawItemId, out float cookTime, out bool canBurn)
    {
        switch (rawItemId)
        {
            case 104: cookTime = 8.5f; canBurn = true; return 200; // Carne Crua -> Carne Assada
            case 113: cookTime = 7.0f; canBurn = true; return 200; // Carne Leve -> Carne Assada
            case 111: cookTime = 7.5f; canBurn = true; return 207; // Peixe Cru -> Peixe Assado
            case 103: cookTime = 9.0f; canBurn = true; return 201; // Ervas -> Sopa de Ervas
            case 123: cookTime = 5.5f; canBurn = false; return 109; // Agua Turva -> Agua Limpa
            default: cookTime = 0f; canBurn = false; return -1;
        }
    }

    public void AddFuel(float amount)
    {
        fuelLevel = Mathf.Clamp(fuelLevel + amount, 0f, 100f);
        if (!isLit && fuelLevel > 0f) Ignite();
        OnBonfireStateChanged?.Invoke();
    }

    public bool TryAddWoodFuel(InventoryAdvanced inv, out string feedback)
    {
        if (fuelLevel >= 95f)
        {
            feedback = "Fogueira ja esta abastecida de lenha.";
            return false;
        }
        if (inv == null || !inv.HasItem(100))
        {
            feedback = "Sem Madeira (100) na mochila para alimentar o fogo.";
            return false;
        }

        inv.RemoveItem(100, 1);
        float before = fuelLevel;
        fuelLevel = Mathf.Clamp(fuelLevel + 45f, 0f, 100f);
        if (isLit && AudioManager.Instance != null) AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.PickupWood, transform.position, 0.72f);
        feedback = $"+Lenha na fogueira ({Mathf.RoundToInt(before)}% -> {Mathf.RoundToInt(fuelLevel)}%).";
        OnBonfireStateChanged?.Invoke();
        return true;
    }

    public bool TryStartCooking(int rawItemID)
    {
        if (!isLit) return false;
        int outId = ResolveCookOutput(rawItemID, out float dur, out _);
        if (outId <= 0) return false;

        int emptyIdx = -1;
        for (int i = 0; i < MaxCookSlots; i++)
        {
            if (cookSlots[i].stage == CookStage.Empty)
            {
                emptyIdx = i;
                break;
            }
        }
        if (emptyIdx < 0) return false;

        cookSlots[emptyIdx] = new CookSlotState
        {
            rawItemId = rawItemID,
            resultItemId = outId,
            timer = 0f,
            duration = dur,
            overcookTimer = 0f,
            stage = CookStage.Cooking
        };

        SyncLegacyFields();
        EnsureVisuals();
        RefreshGrillVisuals();
        OnBonfireStateChanged?.Invoke();
        return true;
    }

    public bool TryPlaceCookItemFromInventory(InventoryAdvanced inv, int preferredItemId, out string feedback)
    {
        if (!isLit)
        {
            feedback = "Fogueira apagada! Acenda antes de cozinhar.";
            return false;
        }
        if (inv == null)
        {
            feedback = "Inventario indisponivel.";
            return false;
        }
        if (EmptySlotCount <= 0)
        {
            feedback = "Grelha da fogueira cheia (3/3).";
            return false;
        }

        int chosenId = -1;
        if (preferredItemId > 0 && ResolveCookOutput(preferredItemId, out _, out _) > 0 && inv.HasItem(preferredItemId))
        {
            chosenId = preferredItemId;
        }
        else if (preferredItemId <= 0)
        {
            for (int i = 0; i < AutoCookPriorityIds.Length; i++)
            {
                if (inv.HasItem(AutoCookPriorityIds[i]))
                {
                    chosenId = AutoCookPriorityIds[i];
                    break;
                }
            }
        }

        if (chosenId <= 0)
        {
            feedback = "Sem carne, peixe cru ou agua turva na mochila.";
            return false;
        }

        if (!TryStartCooking(chosenId))
        {
            feedback = "Nao foi possivel colocar item na fogueira.";
            return false;
        }

        inv.RemoveItem(chosenId, 1);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.CookingSizzle, transform.position, 0.72f);

        ItemData d = ItemDatabase.GetItem(chosenId);
        string itemName = d != null ? d.itemName : "Item";
        feedback = $"Na grelha: {itemName} (preparo em andamento).";
        return true;
    }

    public int TryCollectReadyItems(InventoryAdvanced inv, out string summary)
    {
        int collected = 0;
        int burntCount = 0;
        string lastItemName = string.Empty;

        for (int i = 0; i < MaxCookSlots; i++)
        {
            CookSlotState slot = cookSlots[i];
            if (slot.stage != CookStage.Ready && slot.stage != CookStage.Burnt) continue;

            int outId = slot.resultItemId;
            if (slot.stage == CookStage.Burnt)
            {
                outId = 122;
                burntCount++;
            }

            if (outId > 0)
            {
                int leftover = inv != null ? inv.AddItem(outId, 1) : 1;
                if (leftover > 0)
                {
                    if (lootCache == null) lootCache = LootManager.Instance ?? FindAnyObjectByType<LootManager>();
                    if (lootCache != null)
                        lootCache.DropItem(outId, leftover, transform.position + Vector3.up * 0.9f);
                }

                ItemData d = ItemDatabase.GetItem(outId);
                if (d != null) lastItemName = d.itemName;
                collected++;
            }

            cookSlots[i] = default;
        }

        if (collected > 0)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.PickupGeneric, transform.position, 0.65f);
            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayBuildPlaceVFX(transform.position + Vector3.up * 0.35f);
            SyncLegacyFields();
            RefreshGrillVisuals();
            OnBonfireStateChanged?.Invoke();
            summary = collected == 1
                ? $"Retirou da fogueira: {lastItemName}!"
                : $"Retirou {collected} itens da fogueira{(burntCount > 0 ? $" ({burntCount} virou carvao)" : "")}!";
        }
        else
        {
            summary = "Nenhum item pronto na fogueira ainda.";
        }

        return collected;
    }

    public bool HasAnyAutoCookableInInventory(InventoryAdvanced inv)
    {
        if (inv == null) return false;
        for (int i = 0; i < AutoCookPriorityIds.Length; i++)
        {
            if (inv.HasItem(AutoCookPriorityIds[i])) return true;
        }
        return false;
    }

    public string GetPromptLabel(InventoryAdvanced inv)
    {
        if (!isLit)
        {
            bool hasIgniteSource = (inv != null && (inv.equippedOffHandId == 303 || inv.HasItem(205))) ||
                                   (PlayerController.Instance != null && PlayerController.Instance.IsLighterOn);
            return hasIgniteSource
                ? "Acender Fogueira [E]"
                : "Fogueira Apagada (Requer Fosforo ou Lampiao [L])";
        }

        int ready = ReadySlotCount;
        if (ready > 0)
        {
            return ready == 1
                ? "Retirar Alimento Pronto da Fogueira [E]"
                : $"Retirar {ready} Itens Prontos da Fogueira [E]";
        }

        if (EmptySlotCount > 0 && HasAnyAutoCookableInInventory(inv))
        {
            return "Colocar na Grelha (Assar / Ferver) [E] | Receitas [F]";
        }

        if (fuelLevel < 40f && inv != null && inv.HasItem(100))
        {
            return $"Adicionar Lenha ({Mathf.RoundToInt(fuelLevel)}%) [E] | Receitas [F]";
        }

        int cooking = CookingSlotCount;
        if (cooking > 0)
        {
            return $"Fogueira ({Mathf.RoundToInt(fuelLevel)}% | {cooking} na grelha) — Receitas [E]";
        }

        return $"Fogueira ({Mathf.RoundToInt(fuelLevel)}% Lenha) — Receitas [E]";
    }

    public void Ignite()
    {
        isLit = true;
        if (fuelLevel <= 0f) fuelLevel = 100f;
        EnsureVisuals();
        if (fireLight != null) fireLight.enabled = true;
        if (fireParticles != null) fireParticles.Play();
        if (AudioManager.Instance != null) AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.FireIgnite, transform.position, 0.76f);
        UpdateFireAudioState();
        OnBonfireStateChanged?.Invoke();
    }

    public void Extinguish()
    {
        isLit = false;
        if (fireLight != null) fireLight.enabled = false;
        if (fireParticles != null) fireParticles.Stop();
        UpdateFireAudioState();
        // Preserva itens e progresso parcial na grelha ao apagar por falta de lenha
        SyncLegacyFields();
        OnBonfireStateChanged?.Invoke();
    }

    public void ExportState(out int[] rawIds, out float[] timers, out float[] overcookTimers, out int[] stages)
    {
        rawIds = new int[MaxCookSlots];
        timers = new float[MaxCookSlots];
        overcookTimers = new float[MaxCookSlots];
        stages = new int[MaxCookSlots];
        for (int i = 0; i < MaxCookSlots; i++)
        {
            rawIds[i] = cookSlots[i].rawItemId;
            timers[i] = cookSlots[i].timer;
            overcookTimers[i] = cookSlots[i].overcookTimer;
            stages[i] = (int)cookSlots[i].stage;
        }
    }

    public void ImportState(float savedFuel, bool savedLit, int[] rawIds, float[] timers, float[] overcookTimers, int[] stages)
    {
        fuelLevel = Mathf.Clamp(savedFuel, 0f, 100f);
        if (savedLit && fuelLevel > 0f) Ignite();
        else Extinguish();

        for (int i = 0; i < MaxCookSlots; i++)
        {
            int rawId = (rawIds != null && i < rawIds.Length) ? rawIds[i] : 0;
            int stInt = (stages != null && i < stages.Length) ? stages[i] : 0;
            CookStage st = (CookStage)Mathf.Clamp(stInt, 0, 3);
            int resId = ResolveCookOutput(rawId, out float dur, out _);

            if (rawId <= 0 || resId <= 0 || st == CookStage.Empty)
            {
                cookSlots[i] = default;
            }
            else
            {
                cookSlots[i] = new CookSlotState
                {
                    rawItemId = rawId,
                    resultItemId = st == CookStage.Burnt ? 122 : resId,
                    timer = (timers != null && i < timers.Length) ? Mathf.Max(0f, timers[i]) : 0f,
                    duration = dur > 0f ? dur : 8.5f,
                    overcookTimer = (overcookTimers != null && i < overcookTimers.Length) ? Mathf.Max(0f, overcookTimers[i]) : 0f,
                    stage = st
                };
            }
        }

        SyncLegacyFields();
        EnsureVisuals();
        RefreshGrillVisuals();
        OnBonfireStateChanged?.Invoke();
    }

    private void SyncLegacyFields()
    {
        bool foundCooking = false;
        for (int i = 0; i < MaxCookSlots; i++)
        {
            if (cookSlots[i].stage == CookStage.Cooking)
            {
                isCooking = true;
                cookTimer = cookSlots[i].timer;
                cookDuration = cookSlots[i].duration;
                itemBeingCooked = cookSlots[i].rawItemId;
                foundCooking = true;
                break;
            }
        }
        if (!foundCooking)
        {
            isCooking = false;
            cookTimer = 0f;
            itemBeingCooked = -1;
        }
    }

    private static void EnsureSharedMaterials()
    {
        if (s_rawMeatMat != null) return;
        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (lit == null) return;

        s_rawMeatMat = new Material(lit) { color = new Color(0.76f, 0.20f, 0.18f) };
        s_cookedMeatMat = new Material(lit) { color = new Color(0.62f, 0.34f, 0.14f) };
        s_waterPotMat = new Material(lit) { color = new Color(0.28f, 0.44f, 0.50f) };
        s_boiledWaterMat = new Material(lit) { color = new Color(0.22f, 0.72f, 0.92f) };
        s_burntMat = new Material(lit) { color = new Color(0.12f, 0.11f, 0.11f) };
    }

    private void EnsureVisuals()
    {
        if (visualsBuilt) return;
        visualsBuilt = true;
        EnsureSharedMaterials();

        if (fireLight == null)
        {
            fireLight = GetComponentInChildren<Light>(true);
            if (fireLight == null)
            {
                GameObject lightObj = new GameObject("BonfirePointLight");
                lightObj.transform.SetParent(transform, false);
                lightObj.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                fireLight = lightObj.AddComponent<Light>();
                fireLight.type = LightType.Point;
                fireLight.color = new Color(1.0f, 0.58f, 0.20f);
                fireLight.intensity = 3.2f;
                fireLight.range = 9.5f;
                fireLight.shadows = LightShadows.None;
            }
            fireLight.enabled = isLit;
        }

        if (fireAudioSource == null)
        {
            fireAudioSource = GetComponent<AudioSource>();
            if (fireAudioSource == null)
            {
                fireAudioSource = gameObject.AddComponent<AudioSource>();
            }
            fireAudioSource.playOnAwake = false;
            fireAudioSource.loop = true;
            fireAudioSource.spatialBlend = 1f;
            fireAudioSource.rolloffMode = AudioRolloffMode.Linear;
            fireAudioSource.minDistance = 2.5f;
            fireAudioSource.maxDistance = 18f;
            fireAudioSource.volume = 0f;
        }

        for (int i = 0; i < MaxCookSlots; i++)
        {
            GameObject slotObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slotObj.name = $"BonfireGrillSlot_{i}";
            Collider col = slotObj.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
            slotObj.transform.SetParent(transform, false);
            slotObj.transform.localPosition = SlotLocalOffsets[i];
            slotObj.transform.localScale = new Vector3(0.20f, 0.08f, 0.15f);
            slotObj.transform.localRotation = Quaternion.Euler(0f, i * 42f, 0f);

            MeshRenderer mr = slotObj.GetComponent<MeshRenderer>();
            if (mr != null && s_rawMeatMat != null) mr.sharedMaterial = s_rawMeatMat;
            slotObj.SetActive(false);

            grillSlotTransforms[i] = slotObj.transform;
            grillRenderers[i] = mr;
        }
        UpdateFireAudioState();
    }

    private void UpdateFireAudioState()
    {
        if (fireAudioSource == null) return;
        if (!isLit || fuelLevel <= 0f)
        {
            if (fireAudioSource.isPlaying) fireAudioSource.Stop();
            fireAudioSource.volume = 0f;
            return;
        }

        if (fireAudioSource.clip == null && AudioManager.Instance != null)
        {
            fireAudioSource.clip = AudioManager.Instance.GetFoleyClip(AudioManager.FoleyEventType.FireCrackleLoop);
        }

        float targetVol = Mathf.Lerp(0.14f, 0.38f, fuelLevel / 100f) + (CookingSlotCount > 0 ? 0.06f : 0f);
        fireAudioSource.volume = targetVol;
        if (fireAudioSource.clip != null && !fireAudioSource.isPlaying && Application.isPlaying)
        {
            fireAudioSource.Play();
        }
    }

    private void RefreshGrillVisuals()
    {
        if (!visualsBuilt) return;
        EnsureSharedMaterials();

        for (int i = 0; i < MaxCookSlots; i++)
        {
            Transform tr = grillSlotTransforms[i];
            MeshRenderer mr = grillRenderers[i];
            if (tr == null) continue;

            CookSlotState slot = cookSlots[i];
            if (slot.stage == CookStage.Empty || slot.rawItemId <= 0)
            {
                tr.gameObject.SetActive(false);
                continue;
            }

            tr.gameObject.SetActive(true);
            bool isLiquid = slot.rawItemId == 123;
            tr.localScale = isLiquid ? new Vector3(0.15f, 0.18f, 0.15f) : new Vector3(0.22f, 0.08f, 0.15f);

            if (mr != null)
            {
                if (slot.stage == CookStage.Burnt)
                    mr.sharedMaterial = s_burntMat ?? mr.sharedMaterial;
                else if (slot.stage == CookStage.Ready)
                    mr.sharedMaterial = (isLiquid ? s_boiledWaterMat : s_cookedMeatMat) ?? mr.sharedMaterial;
                else
                    mr.sharedMaterial = (isLiquid ? s_waterPotMat : s_rawMeatMat) ?? mr.sharedMaterial;
            }
        }
    }

    private void UpdateVisualAnimations()
    {
        if (!visualsBuilt) return;

        float now = Time.time;
        for (int i = 0; i < MaxCookSlots; i++)
        {
            Transform tr = grillSlotTransforms[i];
            if (tr == null || !tr.gameObject.activeSelf) continue;

            CookSlotState slot = cookSlots[i];
            Vector3 basePos = SlotLocalOffsets[i];
            if (slot.stage == CookStage.Ready)
            {
                bool urgentBurn = CanItemBurn(slot.rawItemId) && slot.overcookTimer >= overcookGraceDuration * 0.65f;
                float speed = urgentBurn ? 14f : 5f;
                float amp = urgentBurn ? 0.045f : 0.025f;
                tr.localPosition = basePos + new Vector3(0f, Mathf.Sin(now * speed + i) * amp, 0f);
            }
            else
            {
                tr.localPosition = basePos;
            }
        }

        // Alerta proximo se algum alimento estiver perto de queimar
        if (now >= nextNearWarningCheckTime)
        {
            nextNearWarningCheckTime = now + 4.0f;
            for (int i = 0; i < MaxCookSlots; i++)
            {
                CookSlotState s = cookSlots[i];
                if (s.stage == CookStage.Ready && CanItemBurn(s.rawItemId) && s.overcookTimer >= overcookGraceDuration * 0.72f)
                {
                    if (HUDSurvivalOverlay.Instance != null && PlayerController.Instance != null &&
                        (PlayerController.Instance.transform.position - transform.position).sqrMagnitude <= 144f)
                    {
                        HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#FF9944>Fogueira: comida quase queimando! Retire com [E]!</color>");
                    }
                    break;
                }
            }
        }
    }
}
