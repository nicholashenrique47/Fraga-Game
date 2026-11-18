using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Modo da sessao multiplayer-ready (transport-agnostic ate a acoplagem de rede).
/// </summary>
public enum CoopSessionMode
{
    Solo = 0,
    CoopHost = 1,
    CoopClient = 2
}

/// <summary>
/// Controle de acesso compartilhado em estruturas e baus cooperativos.
/// </summary>
public enum CoopAccessMode
{
    SharedCamp = 0,
    OwnerOnly = 1
}

/// <summary>
/// Snapshot serializavel dos sinais vitais e posicao de um membro do grupo Co-op.
/// </summary>
[Serializable]
public struct CoopMemberState
{
    public string playerId;
    public string displayName;
    public string colorHex;
    public float health;
    public float maxHealth;
    public float stamina;
    public float hunger;
    public float thirst;
    public float sanity;
    public float bodyTemp;
    public bool isExhausted;
    public bool isBleeding;
    public bool isFractured;
    public bool isSick;
    public bool isPoisoned;
    public bool isHypothermic;
    public bool isDowned;
    public bool isConnected;
    public bool isLocalPlayer;
    public Vector3 worldPosition;
    public int pingMs;
    public int equippedMainHandId;
    public bool hasActivePing;
    public Vector3 activePingPosition;
    public string activePingLabel;
    public float activePingExpiresAt;
}

/// <summary>
/// Evento serializavel de sincronizacao de construcao (colocacao, deposito, conclusao, reparo, cancelamento).
/// </summary>
[Serializable]
public struct CoopBuildSyncEvent
{
    public string structureNetId;
    public string actorPlayerId;
    public string ownerPlayerId;
    public string ownerName;
    public string structureName;
    public string actionType;
    public Vector3 position;
    public float rotationY;
    public int currentLogs;
    public int neededLogs;
    public int currentStones;
    public int neededStones;
    public bool isComplete;
    public bool isSharedWithGroup;
}

/// <summary>
/// Evento serializavel de compartilhamento ou descarte cooperativo de itens.
/// </summary>
[Serializable]
public struct CoopInventoryShareEvent
{
    public string fromPlayerId;
    public string fromPlayerName;
    public string toPlayerId;
    public int itemId;
    public int amount;
    public Vector3 worldPosition;
}

/// <summary>
/// Resumo de ameaca do mundo para sincronizacao Co-op e telemetria do personagem.
/// </summary>
[Serializable]
public struct CoopWorldThreatSnapshot
{
    public int activeVultoCount;
    public bool bossActive;
    public bool bossEnraged;
    public float bossHealthRatio;
    public float threatLevel01;
}

/// <summary>
/// FragaCoopSession: Camada central Multiplayer-Ready (Zero-SDK externo, 100% C# serializavel).
/// Gerencia identidade local, sala Co-op, permissao de base compartilhada, snapshots vitais (4 Hz, Zero-GC),
/// ping tatico e eventos prontos para Mirror / Netcode for GameObjects / Photon Fusion.
/// </summary>
public class FragaCoopSession : MonoBehaviour
{
    public static FragaCoopSession Instance { get; private set; }

    public const int MaxPartySize = 4;
    private const string PrefPlayerNameKey = "Fraga_Coop_PlayerName";
    private const string PrefSharedDefaultKey = "Fraga_Coop_SharedDefault";

    private static readonly string[] DefaultCallsigns =
    {
        "Sobrevivente",
        "Batedor da Fraga",
        "Lenhador Celta",
        "Curandeiro"
    };

    private static readonly string[] MemberColorPalette =
    {
        "#4EE6FF",
        "#66FF99",
        "#FFD166",
        "#FF8FA3"
    };

    [Header("Sessao & Identidade")]
    public CoopSessionMode sessionMode = CoopSessionMode.Solo;
    public CoopAccessMode defaultBuildAccess = CoopAccessMode.SharedCamp;
    public bool friendlyFireEnabled = false;
    public string localPlayerId = "P1-LOCAL";
    public string localPlayerName = "Sobrevivente";
    public string sessionJoinCode = "FRAGA-1913";

    private readonly List<CoopMemberState> members = new List<CoopMemberState>(MaxPartySize);
    private float nextLocalSyncTime = 0f;
    private bool squadPreviewEnabled = false;

    public IReadOnlyList<CoopMemberState> Members => members;
    public bool IsCoopSessionActive => sessionMode != CoopSessionMode.Solo;
    public bool IsSquadPreviewEnabled => squadPreviewEnabled;
    public bool ShouldShowPartyHUD => IsCoopSessionActive || squadPreviewEnabled || members.Count > 1;
    public bool DefaultStructuresShared => defaultBuildAccess == CoopAccessMode.SharedCamp;
    public string LocalLanAddress => FragaLanCoopTransport.GetLocalIPv4Address();
    public int ActiveLanPort => FragaLanCoopTransport.Instance != null ? FragaLanCoopTransport.Instance.ActivePort : FragaLanCoopTransport.DefaultTcpPort;

    public event Action OnPartyUpdated;
    public event Action<CoopBuildSyncEvent> OnBuildCommandBroadcast;
    public event Action<CoopInventoryShareEvent> OnInventoryShareBroadcast;
    public event Action<string, Vector3, string> OnPingBroadcast;

    public static FragaCoopSession GetOrCreate()
    {
        if (Instance != null) return Instance;
        FragaCoopSession existing = FindAnyObjectByType<FragaCoopSession>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject host = new GameObject("FragaCoopSession");
        return host.AddComponent<FragaCoopSession>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Application.isPlaying) Destroy(this);
            else DestroyImmediate(this);
            return;
        }

        Instance = this;
        LoadSavedPreferences();
        EnsureLocalMemberInitialized();
        SyncLocalVitalsNow();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        if (now >= nextLocalSyncTime)
        {
            nextLocalSyncTime = now + 0.25f;
            SyncLocalVitalsNow();
        }
    }

    private void LoadSavedPreferences()
    {
        string savedName = PlayerPrefs.GetString(PrefPlayerNameKey, "Sobrevivente");
        localPlayerName = string.IsNullOrWhiteSpace(savedName) ? "Sobrevivente" : savedName.Trim();
        int sharedPref = PlayerPrefs.GetInt(PrefSharedDefaultKey, 0);
        defaultBuildAccess = sharedPref == 1 ? CoopAccessMode.OwnerOnly : CoopAccessMode.SharedCamp;
        if (string.IsNullOrEmpty(localPlayerId) || localPlayerId == "P1-LOCAL")
        {
            int idHash = Mathf.Abs(SystemInfo.deviceUniqueIdentifier.GetHashCode()) % 90000 + 10000;
            localPlayerId = $"P-{idHash}";
        }
        if (string.IsNullOrEmpty(sessionJoinCode))
            sessionJoinCode = GenerateDeterministicJoinCode();
    }

    private static string GenerateDeterministicJoinCode()
    {
        int code = Mathf.Abs(SystemInfo.deviceUniqueIdentifier.GetHashCode()) % 9000 + 1000;
        return $"FRAGA-{code}";
    }

    private void EnsureLocalMemberInitialized()
    {
        if (members.Count == 0)
        {
            members.Add(new CoopMemberState
            {
                playerId = localPlayerId,
                displayName = localPlayerName,
                colorHex = MemberColorPalette[0],
                health = 100f,
                maxHealth = 100f,
                stamina = 100f,
                hunger = 100f,
                thirst = 100f,
                sanity = 100f,
                bodyTemp = 37.0f,
                isConnected = true,
                isLocalPlayer = true,
                pingMs = 0
            });
        }
    }

    public void SetLocalPlayerName(string newName)
    {
        string clean = string.IsNullOrWhiteSpace(newName) ? "Sobrevivente" : newName.Trim();
        if (clean.Length > 20) clean = clean.Substring(0, 20);
        localPlayerName = clean;
        PlayerPrefs.SetString(PrefPlayerNameKey, localPlayerName);
        PlayerPrefs.Save();

        EnsureLocalMemberInitialized();
        CoopMemberState local = members[0];
        local.displayName = localPlayerName;
        members[0] = local;
        OnPartyUpdated?.Invoke();
    }

    public void CycleDefaultCallsign()
    {
        int nextIdx = 0;
        for (int i = 0; i < DefaultCallsigns.Length; i++)
        {
            if (string.Equals(DefaultCallsigns[i], localPlayerName, StringComparison.OrdinalIgnoreCase))
            {
                nextIdx = (i + 1) % DefaultCallsigns.Length;
                break;
            }
        }
        SetLocalPlayerName(DefaultCallsigns[nextIdx]);
    }

    public void SetDefaultBuildAccess(CoopAccessMode mode)
    {
        defaultBuildAccess = mode;
        PlayerPrefs.SetInt(PrefSharedDefaultKey, mode == CoopAccessMode.OwnerOnly ? 1 : 0);
        PlayerPrefs.Save();
        OnPartyUpdated?.Invoke();
    }

    public void ToggleDefaultBuildAccess()
    {
        SetDefaultBuildAccess(defaultBuildAccess == CoopAccessMode.SharedCamp
            ? CoopAccessMode.OwnerOnly
            : CoopAccessMode.SharedCamp);
    }

    public void StartCoopHost(string customJoinCode = null)
    {
        sessionMode = CoopSessionMode.CoopHost;
        sessionJoinCode = string.IsNullOrWhiteSpace(customJoinCode) ? GenerateDeterministicJoinCode() : customJoinCode.Trim().ToUpper();
        SyncLocalVitalsNow();
        OnPartyUpdated?.Invoke();
    }

    public void JoinCoopByCode(string joinCode)
    {
        if (string.IsNullOrWhiteSpace(joinCode)) return;
        sessionMode = CoopSessionMode.CoopClient;
        sessionJoinCode = joinCode.Trim().ToUpper();
        SyncLocalVitalsNow();
        OnPartyUpdated?.Invoke();
    }

    public bool StartLanHost(int tcpPort = FragaLanCoopTransport.DefaultTcpPort, string customJoinCode = null)
    {
        StartCoopHost(customJoinCode);
        FragaLanCoopTransport transport = FragaLanCoopTransport.GetOrCreate();
        bool ok = transport.StartHost(tcpPort);
        OnPartyUpdated?.Invoke();
        return ok;
    }

    public bool JoinLanHost(string ipOrHostPort)
    {
        if (string.IsNullOrWhiteSpace(ipOrHostPort)) return false;
        sessionMode = CoopSessionMode.CoopClient;
        squadPreviewEnabled = false;
        RemoveRemoteMembers();
        FragaLanCoopTransport transport = FragaLanCoopTransport.GetOrCreate();
        bool started = transport.ConnectToHost(ipOrHostPort, FragaLanCoopTransport.DefaultTcpPort);
        SyncLocalVitalsNow();
        OnPartyUpdated?.Invoke();
        return started;
    }

    public void StopLanSession()
    {
        SetSoloMode();
    }

    public void SetSoloMode()
    {
        sessionMode = CoopSessionMode.Solo;
        squadPreviewEnabled = false;
        RemoveRemoteMembers();
        if (FragaLanCoopTransport.Instance != null)
        {
            FragaLanCoopTransport.Instance.StopAll();
        }
        SyncLocalVitalsNow();
        OnPartyUpdated?.Invoke();
    }

    public void ToggleSquadPreview()
    {
        squadPreviewEnabled = !squadPreviewEnabled;
        if (squadPreviewEnabled)
        {
            if (sessionMode == CoopSessionMode.Solo)
                sessionMode = CoopSessionMode.CoopHost;

            Vector3 basePos = PlayerController.Instance != null ? PlayerController.Instance.transform.position : Vector3.zero;
            UpsertRemoteMemberState(new CoopMemberState
            {
                playerId = "P2-SCOUT",
                displayName = "Iago (Batedor)",
                colorHex = MemberColorPalette[1],
                health = 84f,
                maxHealth = 100f,
                stamina = 72f,
                hunger = 68f,
                thirst = 62f,
                sanity = 88f,
                bodyTemp = 36.4f,
                isConnected = true,
                isLocalPlayer = false,
                worldPosition = basePos + new Vector3(14f, 0f, 9f),
                pingMs = 28,
                equippedMainHandId = 305
            });
            UpsertRemoteMemberState(new CoopMemberState
            {
                playerId = "P3-MEDIC",
                displayName = "Clara (Herborista)",
                colorHex = MemberColorPalette[2],
                health = 58f,
                maxHealth = 100f,
                stamina = 46f,
                hunger = 75f,
                thirst = 41f,
                sanity = 79f,
                bodyTemp = 34.7f,
                isHypothermic = true,
                isBleeding = true,
                isConnected = true,
                isLocalPlayer = false,
                worldPosition = basePos + new Vector3(-22f, 0f, 18f),
                pingMs = 42,
                equippedMainHandId = 303
            });
        }
        else
        {
            RemoveRemoteMembers();
        }
        OnPartyUpdated?.Invoke();
    }

    public void RemoveRemoteMembers()
    {
        for (int i = members.Count - 1; i >= 1; i--)
        {
            members.RemoveAt(i);
        }
    }

    public void UpsertRemoteMemberState(CoopMemberState remote)
    {
        if (string.IsNullOrEmpty(remote.playerId) || remote.playerId == localPlayerId) return;
        EnsureLocalMemberInitialized();

        remote.isLocalPlayer = false;
        for (int i = 1; i < members.Count; i++)
        {
            if (members[i].playerId == remote.playerId)
            {
                members[i] = remote;
                OnPartyUpdated?.Invoke();
                return;
            }
        }

        if (members.Count < MaxPartySize)
        {
            if (string.IsNullOrEmpty(remote.colorHex))
                remote.colorHex = MemberColorPalette[Mathf.Clamp(members.Count, 0, MemberColorPalette.Length - 1)];
            members.Add(remote);
            OnPartyUpdated?.Invoke();
        }
    }

    public void SyncLocalVitalsNow()
    {
        EnsureLocalMemberInitialized();
        CoopMemberState local = members[0];
        local.playerId = localPlayerId;
        local.displayName = localPlayerName;
        local.isLocalPlayer = true;
        local.isConnected = true;

        PlayerStatus ps = PlayerStatus.Instance ?? FindAnyObjectByType<PlayerStatus>();
        if (ps != null)
        {
            local.health = ps.health;
            local.maxHealth = 100f;
            local.stamina = ps.stamina;
            local.hunger = ps.hunger;
            local.thirst = ps.thirst;
            local.bodyTemp = BodyTemperatureSystem.Instance != null ? BodyTemperatureSystem.Instance.currentTemperature : ps.bodyTemperature;
            local.isExhausted = ps.isExhausted;
            local.isBleeding = ps.isBleeding;
            local.isFractured = ps.isFractured;
            local.isSick = ps.isSick;
            local.isPoisoned = ps.isPoisoned;
            local.isHypothermic = local.bodyTemp <= BodyTemperatureSystem.TIER1_SHIVER_TEMP;
            local.isDowned = ps.health <= 0f;
        }

        SanitySystem sanity = FindAnyObjectByType<SanitySystem>();
        if (sanity != null)
            local.sanity = sanity.sanity;

        InventoryAdvanced inv = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (inv != null)
            local.equippedMainHandId = inv.equippedMainHandId;

        if (PlayerController.Instance != null)
            local.worldPosition = PlayerController.Instance.transform.position;

        if (local.hasActivePing && Time.unscaledTime > local.activePingExpiresAt)
            local.hasActivePing = false;

        members[0] = local;
    }

    public bool CanLocalPlayerInteract(string ownerPlayerId, bool isSharedWithGroup)
    {
        if (isSharedWithGroup) return true;
        if (string.IsNullOrEmpty(ownerPlayerId)) return true;
        return string.Equals(ownerPlayerId, localPlayerId, StringComparison.Ordinal);
    }

    public void SendQuickPing(Vector3 worldPos, string label)
    {
        EnsureLocalMemberInitialized();
        CoopMemberState local = members[0];
        local.hasActivePing = true;
        local.activePingPosition = worldPos;
        local.activePingLabel = string.IsNullOrEmpty(label) ? "Ponto de Encontro" : label;
        local.activePingExpiresAt = Time.unscaledTime + 18f;
        members[0] = local;

        OnPingBroadcast?.Invoke(localPlayerId, worldPos, local.activePingLabel);
        OnPartyUpdated?.Invoke();

        if (HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.ShowLootTooltip(
                $"<color=#55EEFF>Sinal Co-op enviado: {local.activePingLabel} ({worldPos.x:F0}, {worldPos.z:F0})</color>");
        }
    }

    public void SetMemberPing(string playerId, Vector3 worldPos, string label, float durationSec = 18f)
    {
        if (string.IsNullOrEmpty(playerId)) return;
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].playerId == playerId)
            {
                CoopMemberState m = members[i];
                m.hasActivePing = true;
                m.activePingPosition = worldPos;
                m.activePingLabel = string.IsNullOrEmpty(label) ? "Ponto Tatico" : label;
                m.activePingExpiresAt = Time.unscaledTime + durationSec;
                members[i] = m;
                OnPingBroadcast?.Invoke(playerId, worldPos, m.activePingLabel);
                OnPartyUpdated?.Invoke();
                return;
            }
        }
    }

    public void BroadcastBuildEvent(CoopBuildSyncEvent evt)
    {
        if (string.IsNullOrEmpty(evt.actorPlayerId)) evt.actorPlayerId = localPlayerId;
        if (string.IsNullOrEmpty(evt.ownerPlayerId)) evt.ownerPlayerId = localPlayerId;
        if (string.IsNullOrEmpty(evt.ownerName)) evt.ownerName = localPlayerName;
        OnBuildCommandBroadcast?.Invoke(evt);
    }

    public bool TryShareOrDropItem(InventoryAdvanced inv, int slotIndex, int amount, string targetPlayerId = null)
    {
        if (inv == null || inv.slots == null || slotIndex < 0 || slotIndex >= inv.slots.Length)
            return false;

        InventorySlot slot = inv.slots[slotIndex];
        if (slot.IsEmpty()) return false;

        int itemId = slot.itemId;
        int qty = Mathf.Clamp(amount, 1, slot.quantity);
        Vector3 pos = PlayerController.Instance != null ? PlayerController.Instance.transform.position : Vector3.zero;

        inv.DropItemToWorld(slotIndex, qty);

        CoopInventoryShareEvent evt = new CoopInventoryShareEvent
        {
            fromPlayerId = localPlayerId,
            fromPlayerName = localPlayerName,
            toPlayerId = targetPlayerId ?? string.Empty,
            itemId = itemId,
            amount = qty,
            worldPosition = pos
        };
        OnInventoryShareBroadcast?.Invoke(evt);

        ItemData data = ItemDatabase.GetItem(itemId);
        string itemName = data != null ? data.itemName : $"Item {itemId}";
        if (HUDSurvivalOverlay.Instance != null)
        {
            string msg = IsCoopSessionActive
                ? $"<color=#66FFBB>Compartilhado no chao (Co-op): {qty}x {itemName}</color>"
                : $"Soltou no chao: {qty}x {itemName}";
            HUDSurvivalOverlay.Instance.ShowLootTooltip(msg);
        }

        return true;
    }

    public CoopWorldThreatSnapshot GetThreatSnapshot()
    {
        int vultos = VultoManager.Instance != null ? VultoManager.Instance.ActiveVultoCount : 0;
        bool bossActive = BossSystem.Instance != null && BossSystem.Instance.IsActive;
        bool bossEnraged = BossSystem.Instance != null && BossSystem.Instance.IsEnraged;
        float bossHp = BossSystem.Instance != null ? BossSystem.Instance.HealthPercentage : 0f;

        float threat = Mathf.Clamp01(vultos / 10f);
        if (bossActive) threat = Mathf.Max(threat, bossEnraged ? 1.0f : 0.8f);

        return new CoopWorldThreatSnapshot
        {
            activeVultoCount = vultos,
            bossActive = bossActive,
            bossEnraged = bossEnraged,
            bossHealthRatio = bossHp,
            threatLevel01 = threat
        };
    }

    public int CountConnectedMembers()
    {
        int count = 0;
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].isConnected) count++;
        }
        return count;
    }

    public float GetDistanceToMember(in CoopMemberState member)
    {
        if (member.isLocalPlayer) return 0f;
        EnsureLocalMemberInitialized();
        return Vector3.Distance(members[0].worldPosition, member.worldPosition);
    }
}
