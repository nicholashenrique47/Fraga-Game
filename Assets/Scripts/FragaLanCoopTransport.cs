using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

[Serializable]
public struct DiscoveredLanRoom
{
    public string hostIp;
    public int tcpPort;
    public string joinCode;
    public string hostName;
    public int memberCount;
    public float lastSeenUnscaledTime;
}

[Serializable]
public struct LanPingPacket
{
    public string playerId;
    public float x;
    public float y;
    public float z;
    public string label;
}

/// <summary>
/// Transporte Co-op nativo em C# puro (System.Net.Sockets, Zero-SDK externo).
/// Suporta descoberta automatica em rede local/Wi-Fi (UDP 47777) e conexao TCP direta por IP:Porta (7777),
/// sincronizando sinais vitais, pings taticos, construcoes e avatares 3D remotos com fila thread-safe Zero-Block.
/// </summary>
public class FragaLanCoopTransport : MonoBehaviour
{
    public static FragaLanCoopTransport Instance { get; private set; }

    public const int DefaultTcpPort = 7777;
    public const int DiscoveryUdpPort = 47777;
    private const float StateBroadcastIntervalSec = 0.20f;
    private const float DiscoveryRoomTimeoutSec = 5.0f;

    [Header("Estado de Rede")]
    [SerializeField] private int activePort = DefaultTcpPort;
    [SerializeField] private string connectionStatus = "Desconectado (Solo)";
    [SerializeField] private string lastError = string.Empty;

    private volatile bool isHosting = false;
    private volatile bool isConnectedClient = false;
    private volatile bool isConnecting = false;
    private volatile bool stopRequested = false;
    private volatile bool discoveryRunning = false;

    private TcpListener tcpListener;
    private Thread acceptThread;
    private Thread beaconThread;

    private readonly object clientsLock = new object();
    private readonly List<TcpClient> connectedClients = new List<TcpClient>(4);
    private readonly List<StreamWriter> clientWriters = new List<StreamWriter>(4);

    private TcpClient clientSocket;
    private StreamWriter clientWriter;
    private Thread clientReadThread;

    private UdpClient discoveryListener;
    private Thread discoveryThread;

    private readonly ConcurrentQueue<string> incomingPacketQueue = new ConcurrentQueue<string>();
    private readonly ConcurrentQueue<string> incomingBeaconQueue = new ConcurrentQueue<string>();
    private readonly ConcurrentQueue<string> pendingStatusUpdates = new ConcurrentQueue<string>();
    private readonly ConcurrentQueue<string> pendingErrors = new ConcurrentQueue<string>();

    private readonly List<DiscoveredLanRoom> discoveredRooms = new List<DiscoveredLanRoom>(8);
    private readonly Dictionary<string, GameObject> remoteAvatars = new Dictionary<string, GameObject>(4);
    private readonly Dictionary<string, Vector3> remoteAvatarTargets = new Dictionary<string, Vector3>(4);
    private readonly Dictionary<string, GameObject> activePingBeacons = new Dictionary<string, GameObject>(4);
    private readonly Dictionary<string, float> activePingExpires = new Dictionary<string, float>(4);
    private readonly List<string> staleKeysBuffer = new List<string>(4);
    private readonly List<string> expiredPingKeysBuffer = new List<string>(4);

    private FragaCoopSession boundSession;
    private float nextStateSendTime = 0f;
    private bool isApplyingRemotePacket = false;
    private volatile int hostConnectedClientCount = 0;

    public bool IsHosting => isHosting;
    public bool IsConnectedClient => isConnectedClient;
    public bool IsConnecting => isConnecting;
    public bool IsNetworkActive => isHosting || isConnectedClient;
    public int ActivePort => activePort;
    public string ConnectionStatus => connectionStatus;
    public string LastError => lastError;
    public int RemotePeerCount => isHosting ? hostConnectedClientCount : (isConnectedClient ? 1 : 0);
    public IReadOnlyList<DiscoveredLanRoom> DiscoveredRooms => discoveredRooms;

    public event Action OnTransportStateChanged;

    public static FragaLanCoopTransport GetOrCreate()
    {
        if (Instance != null) return Instance;
        FragaLanCoopTransport existing = FindAnyObjectByType<FragaLanCoopTransport>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject host = FragaCoopSession.Instance != null
            ? FragaCoopSession.Instance.gameObject
            : new GameObject("FragaLanCoopTransport");
        return host.AddComponent<FragaLanCoopTransport>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        BindSessionEvents();
    }

    private void OnDestroy()
    {
        StopAll();
        UnbindSessionEvents();
        if (Instance == this) Instance = null;
    }

    private void OnApplicationQuit()
    {
        StopAll();
    }

    private void BindSessionEvents()
    {
        FragaCoopSession session = FragaCoopSession.GetOrCreate();
        if (boundSession == session) return;
        UnbindSessionEvents();
        boundSession = session;
        if (boundSession != null)
        {
            boundSession.OnBuildCommandBroadcast += HandleLocalBuildBroadcast;
            boundSession.OnInventoryShareBroadcast += HandleLocalInventoryShareBroadcast;
            boundSession.OnPingBroadcast += HandleLocalPingBroadcast;
        }
    }

    private void UnbindSessionEvents()
    {
        if (boundSession != null)
        {
            boundSession.OnBuildCommandBroadcast -= HandleLocalBuildBroadcast;
            boundSession.OnInventoryShareBroadcast -= HandleLocalInventoryShareBroadcast;
            boundSession.OnPingBroadcast -= HandleLocalPingBroadcast;
            boundSession = null;
        }
    }

    private void Update()
    {
        DrainThreadQueues();

        if (IsNetworkActive)
        {
            float now = Time.unscaledTime;
            if (now >= nextStateSendTime)
            {
                nextStateSendTime = now + StateBroadcastIntervalSec;
                BroadcastLocalMemberState();
            }
        }

        UpdateRemoteAvatars(Time.deltaTime);
    }

    public static string GetLocalIPv4Address()
    {
        try
        {
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            string fallbackIp = null;
            for (int i = 0; i < interfaces.Length; i++)
            {
                NetworkInterface ni = interfaces[i];
                if (ni == null || ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    continue;

                IPInterfaceProperties props = ni.GetIPProperties();
                if (props == null) continue;
                UnicastIPAddressInformationCollection addrs = props.UnicastAddresses;
                for (int j = 0; j < addrs.Count; j++)
                {
                    IPAddress ip = addrs[j].Address;
                    if (ip == null || ip.AddressFamily != AddressFamily.InterNetwork) continue;
                    string s = ip.ToString();
                    if (s.StartsWith("169.254.", StringComparison.Ordinal)) continue;

                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    {
                        return s;
                    }
                    if (string.IsNullOrEmpty(fallbackIp)) fallbackIp = s;
                }
            }
            if (!string.IsNullOrEmpty(fallbackIp)) return fallbackIp;
        }
        catch
        {
            // Ignora ambientes restritos e cai no fallback DNS
        }

        try
        {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            for (int i = 0; i < host.AddressList.Length; i++)
            {
                if (host.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                    return host.AddressList[i].ToString();
            }
        }
        catch
        {
            // Fallback final
        }

        return "127.0.0.1";
    }

    public bool StartHost(int tcpPort = DefaultTcpPort)
    {
        StopNetworkOnly();
        BindSessionEvents();

        activePort = tcpPort > 0 ? tcpPort : DefaultTcpPort;
        stopRequested = false;
        lastError = string.Empty;

        try
        {
            tcpListener = new TcpListener(IPAddress.Any, activePort);
            tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            tcpListener.Start(4);

            isHosting = true;
            hostConnectedClientCount = 0;
            connectionStatus = $"Sala aberta em {GetLocalIPv4Address()}:{activePort}";

            acceptThread = new Thread(HostAcceptLoop) { IsBackground = true, Name = "FragaCoopHostAccept" };
            acceptThread.Start();

            beaconThread = new Thread(HostBeaconLoop) { IsBackground = true, Name = "FragaCoopLanBeacon" };
            beaconThread.Start();

            OnTransportStateChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            lastError = $"Falha ao abrir porta {activePort}: {ex.Message}";
            connectionStatus = "Erro ao abrir sala LAN";
            StopNetworkOnly();
            OnTransportStateChanged?.Invoke();
            return false;
        }
    }

    public bool ConnectToHost(string hostInput, int defaultPort = DefaultTcpPort)
    {
        if (!TryParseHostAndPort(hostInput, defaultPort, out string targetHost, out int targetPort))
        {
            lastError = "Endereco IP invalido. Ex: 192.168.1.15 ou 192.168.1.15:7777";
            connectionStatus = "IP invalido";
            OnTransportStateChanged?.Invoke();
            return false;
        }

        StopNetworkOnly();
        BindSessionEvents();

        activePort = targetPort;
        stopRequested = false;
        isConnecting = true;
        lastError = string.Empty;
        connectionStatus = $"Conectando em {targetHost}:{targetPort}...";
        OnTransportStateChanged?.Invoke();

        Thread connectThread = new Thread(() => ClientConnectAndReadLoop(targetHost, targetPort))
        {
            IsBackground = true,
            Name = "FragaCoopClientConnect"
        };
        connectThread.Start();
        return true;
    }

    public static bool TryParseHostAndPort(string raw, int defaultPort, out string host, out int port)
    {
        host = string.Empty;
        port = defaultPort > 0 ? defaultPort : DefaultTcpPort;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        string clean = raw.Trim();
        int colon = clean.LastIndexOf(':');
        if (colon > 0 && colon < clean.Length - 1)
        {
            string portPart = clean.Substring(colon + 1).Trim();
            if (int.TryParse(portPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPort) &&
                parsedPort > 0 && parsedPort <= 65535)
            {
                port = parsedPort;
                clean = clean.Substring(0, colon).Trim();
            }
        }

        if (string.IsNullOrEmpty(clean) || clean.Length > 64) return false;
        host = clean;
        return true;
    }

    public bool StartLanDiscovery()
    {
        if (discoveryRunning) return true;

        try
        {
            discoveryListener = new UdpClient();
            discoveryListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            discoveryListener.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryUdpPort));
            discoveryListener.Client.ReceiveTimeout = 500;
            discoveryRunning = true;

            discoveryThread = new Thread(DiscoveryListenLoop) { IsBackground = true, Name = "FragaCoopLanDiscovery" };
            discoveryThread.Start();
            return true;
        }
        catch (Exception ex)
        {
            lastError = $"Busca LAN indisponivel: {ex.Message}";
            StopLanDiscovery();
            return false;
        }
    }

    public void StopLanDiscovery()
    {
        discoveryRunning = false;
        if (discoveryListener != null)
        {
            try { discoveryListener.Close(); } catch { }
            discoveryListener = null;
        }
    }

    public void StopAll()
    {
        StopNetworkOnly();
        StopLanDiscovery();
        ClearRemoteAvatars();
        discoveredRooms.Clear();
        connectionStatus = "Desconectado (Solo)";
        OnTransportStateChanged?.Invoke();
    }

    private void StopNetworkOnly()
    {
        stopRequested = true;
        isHosting = false;
        isConnectedClient = false;
        isConnecting = false;
        hostConnectedClientCount = 0;

        if (tcpListener != null)
        {
            try { tcpListener.Stop(); } catch { }
            tcpListener = null;
        }

        lock (clientsLock)
        {
            for (int i = 0; i < clientWriters.Count; i++)
            {
                try { clientWriters[i].Dispose(); } catch { }
            }
            for (int i = 0; i < connectedClients.Count; i++)
            {
                try { connectedClients[i].Close(); } catch { }
            }
            clientWriters.Clear();
            connectedClients.Clear();
        }

        if (clientWriter != null)
        {
            try { clientWriter.Dispose(); } catch { }
            clientWriter = null;
        }
        if (clientSocket != null)
        {
            try { clientSocket.Close(); } catch { }
            clientSocket = null;
        }

        ClearRemoteAvatars();
    }

    private void HostAcceptLoop()
    {
        while (!stopRequested && isHosting && tcpListener != null)
        {
            try
            {
                TcpClient incoming = tcpListener.AcceptTcpClient();
                incoming.NoDelay = true;

                StreamWriter writer = new StreamWriter(incoming.GetStream(), Encoding.UTF8) { AutoFlush = true };
                lock (clientsLock)
                {
                    if (stopRequested || connectedClients.Count >= FragaCoopSession.MaxPartySize - 1)
                    {
                        try { writer.Dispose(); } catch { }
                        try { incoming.Close(); } catch { }
                        continue;
                    }
                    connectedClients.Add(incoming);
                    clientWriters.Add(writer);
                    hostConnectedClientCount = connectedClients.Count;
                }

                pendingStatusUpdates.Enqueue($"Sala ativa ({GetLocalIPv4Address()}:{activePort}) - {hostConnectedClientCount} amigo(s)");

                Thread clientReader = new Thread(() => HostReadClientLoop(incoming, writer))
                {
                    IsBackground = true,
                    Name = "FragaCoopHostClientRead"
                };
                clientReader.Start();
            }
            catch
            {
                if (stopRequested || !isHosting) break;
                Thread.Sleep(50);
            }
        }
    }

    private void HostReadClientLoop(TcpClient client, StreamWriter ownWriter)
    {
        try
        {
            using (StreamReader reader = new StreamReader(client.GetStream(), Encoding.UTF8))
            {
                while (!stopRequested && isHosting && client.Connected)
                {
                    string line = reader.ReadLine();
                    if (line == null) break;
                    if (line.Length == 0) continue;

                    incomingPacketQueue.Enqueue(line);
                    RelayToOtherClients(line, ownWriter);
                }
            }
        }
        catch
        {
            // Cliente desconectou
        }
        finally
        {
            lock (clientsLock)
            {
                int idx = connectedClients.IndexOf(client);
                if (idx >= 0)
                {
                    connectedClients.RemoveAt(idx);
                    if (idx < clientWriters.Count) clientWriters.RemoveAt(idx);
                }
                hostConnectedClientCount = connectedClients.Count;
            }
            try { ownWriter.Dispose(); } catch { }
            try { client.Close(); } catch { }
            pendingStatusUpdates.Enqueue($"Sala ativa ({GetLocalIPv4Address()}:{activePort}) - {hostConnectedClientCount} amigo(s)");
        }
    }

    private void HostBeaconLoop()
    {
        UdpClient udp = null;
        try
        {
            udp = new UdpClient();
            udp.EnableBroadcast = true;
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryUdpPort);

            while (!stopRequested && isHosting)
            {
                string joinCode = boundSession != null ? boundSession.sessionJoinCode : "FRAGA-1913";
                string hostName = boundSession != null ? boundSession.localPlayerName : "Host";
                int members = boundSession != null ? boundSession.CountConnectedMembers() : 1;
                string safeName = string.IsNullOrEmpty(hostName) ? "Host" : hostName.Replace('|', ' ');
                string payload = $"FRAGA_LAN|{joinCode}|{safeName}|{activePort}|{members}";
                byte[] bytes = Encoding.UTF8.GetBytes(payload);
                try { udp.Send(bytes, bytes.Length, endpoint); } catch { }

                for (int s = 0; s < 12 && !stopRequested && isHosting; s++)
                    Thread.Sleep(100);
            }
        }
        catch
        {
            // Ambiente sem broadcast permitido continua funcionando via IP direto
        }
        finally
        {
            if (udp != null)
            {
                try { udp.Close(); } catch { }
            }
        }
    }

    private void ClientConnectAndReadLoop(string targetHost, int targetPort)
    {
        TcpClient tcp = null;
        try
        {
            tcp = new TcpClient();
            IAsyncResult ar = tcp.BeginConnect(targetHost, targetPort, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(4000))
            {
                try { tcp.Close(); } catch { }
                pendingErrors.Enqueue($"Tempo esgotado tentando conectar em {targetHost}:{targetPort}");
                pendingStatusUpdates.Enqueue("Falha na conexao");
                isConnecting = false;
                return;
            }
            tcp.EndConnect(ar);
            if (stopRequested)
            {
                try { tcp.Close(); } catch { }
                return;
            }

            tcp.NoDelay = true;
            clientSocket = tcp;
            clientWriter = new StreamWriter(tcp.GetStream(), Encoding.UTF8) { AutoFlush = true };
            isConnecting = false;
            isConnectedClient = true;
            pendingStatusUpdates.Enqueue($"Conectado a {targetHost}:{targetPort}");

            using (StreamReader reader = new StreamReader(tcp.GetStream(), Encoding.UTF8))
            {
                while (!stopRequested && isConnectedClient && tcp.Connected)
                {
                    string line = reader.ReadLine();
                    if (line == null) break;
                    if (line.Length > 0)
                        incomingPacketQueue.Enqueue(line);
                }
            }
        }
        catch (Exception ex)
        {
            if (!stopRequested)
            {
                pendingErrors.Enqueue($"Conexao falhou ({targetHost}:{targetPort}): {ex.Message}");
                pendingStatusUpdates.Enqueue("Desconectado");
            }
        }
        finally
        {
            isConnecting = false;
            isConnectedClient = false;
            if (clientWriter != null) { try { clientWriter.Dispose(); } catch { } clientWriter = null; }
            if (clientSocket != null) { try { clientSocket.Close(); } catch { } clientSocket = null; }
            if (tcp != null) { try { tcp.Close(); } catch { } }
        }
    }

    private void DiscoveryListenLoop()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        while (discoveryRunning && discoveryListener != null)
        {
            try
            {
                byte[] data = discoveryListener.Receive(ref remoteEP);
                if (data == null || data.Length == 0) continue;
                string msg = Encoding.UTF8.GetString(data);
                if (msg.StartsWith("FRAGA_LAN|", StringComparison.Ordinal))
                {
                    incomingBeaconQueue.Enqueue($"{remoteEP.Address}|{msg}");
                }
            }
            catch (SocketException)
            {
                // Timeout esperado para checar discoveryRunning
            }
            catch
            {
                if (!discoveryRunning) break;
            }
        }
    }

    private void RelayToOtherClients(string line, StreamWriter excludeWriter)
    {
        lock (clientsLock)
        {
            for (int i = clientWriters.Count - 1; i >= 0; i--)
            {
                StreamWriter w = clientWriters[i];
                if (w == null || w == excludeWriter) continue;
                try { w.WriteLine(line); }
                catch { /* Sera limpo pelo loop de leitura */ }
            }
        }
    }

    private void SendPacketLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        if (isHosting)
        {
            RelayToOtherClients(line, null);
        }
        else if (isConnectedClient && clientWriter != null)
        {
            try { clientWriter.WriteLine(line); }
            catch { /* Desconexao sera tratada na thread de leitura */ }
        }
    }

    private void BroadcastLocalMemberState()
    {
        FragaCoopSession coop = FragaCoopSession.Instance;
        if (coop == null) return;
        coop.SyncLocalVitalsNow();
        if (coop.Members.Count == 0) return;

        CoopMemberState local = coop.Members[0];
        SendPacketLine("STATE|" + JsonUtility.ToJson(local));
    }

    private void HandleLocalBuildBroadcast(CoopBuildSyncEvent evt)
    {
        if (isApplyingRemotePacket || !IsNetworkActive) return;
        FragaCoopSession coop = FragaCoopSession.Instance;
        string senderId = !string.IsNullOrEmpty(evt.actorPlayerId) ? evt.actorPlayerId : evt.ownerPlayerId;
        if (coop != null && !string.IsNullOrEmpty(senderId) && senderId != coop.localPlayerId)
            return;

        SendPacketLine("BUILD|" + JsonUtility.ToJson(evt));
    }

    private void HandleLocalInventoryShareBroadcast(CoopInventoryShareEvent evt)
    {
        if (isApplyingRemotePacket || !IsNetworkActive) return;
        FragaCoopSession coop = FragaCoopSession.Instance;
        if (coop != null && !string.IsNullOrEmpty(evt.fromPlayerId) && evt.fromPlayerId != coop.localPlayerId)
            return;

        SendPacketLine("SHARE|" + JsonUtility.ToJson(evt));
    }

    private void HandleLocalPingBroadcast(string playerId, Vector3 pos, string label)
    {
        if (isApplyingRemotePacket || !IsNetworkActive) return;
        FragaCoopSession coop = FragaCoopSession.Instance;
        if (coop != null && playerId != coop.localPlayerId)
            return;

        LanPingPacket pkt = new LanPingPacket
        {
            playerId = playerId,
            x = pos.x,
            y = pos.y,
            z = pos.z,
            label = label
        };
        SendPacketLine("PING|" + JsonUtility.ToJson(pkt));
    }

    private void DrainThreadQueues()
    {
        bool changed = false;

        while (pendingErrors.TryDequeue(out string err))
        {
            lastError = err;
            changed = true;
        }

        while (pendingStatusUpdates.TryDequeue(out string st))
        {
            connectionStatus = st;
            if (st.StartsWith("Conectado", StringComparison.Ordinal) && FragaCoopSession.Instance != null)
            {
                FragaCoopSession.Instance.sessionMode = CoopSessionMode.CoopClient;
                BroadcastLocalMemberState();
            }
            changed = true;
        }

        while (incomingBeaconQueue.TryDequeue(out string beacon))
        {
            ProcessDiscoveryBeacon(beacon);
            changed = true;
        }

        PruneExpiredDiscoveryRooms();

        int processed = 0;
        while (processed < 32 && incomingPacketQueue.TryDequeue(out string pkt))
        {
            ProcessIncomingPacket(pkt);
            processed++;
        }

        if (changed)
        {
            OnTransportStateChanged?.Invoke();
        }
    }

    public void ProcessDiscoveryBeacon(string rawBeacon)
    {
        if (string.IsNullOrEmpty(rawBeacon)) return;
        // Formato: <ip>|FRAGA_LAN|<joinCode>|<hostName>|<port>|<members>
        string[] parts = rawBeacon.Split('|');
        if (parts.Length < 6 || parts[1] != "FRAGA_LAN") return;

        string ip = parts[0];
        string joinCode = parts[2];
        string hostName = parts[3];
        if (!int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int port))
            port = DefaultTcpPort;
        if (!int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int memberCount))
            memberCount = 1;

        float now = Time.unscaledTime;
        for (int i = 0; i < discoveredRooms.Count; i++)
        {
            if (discoveredRooms[i].hostIp == ip && discoveredRooms[i].tcpPort == port)
            {
                discoveredRooms[i] = new DiscoveredLanRoom
                {
                    hostIp = ip,
                    tcpPort = port,
                    joinCode = joinCode,
                    hostName = hostName,
                    memberCount = memberCount,
                    lastSeenUnscaledTime = now
                };
                return;
            }
        }

        discoveredRooms.Add(new DiscoveredLanRoom
        {
            hostIp = ip,
            tcpPort = port,
            joinCode = joinCode,
            hostName = hostName,
            memberCount = memberCount,
            lastSeenUnscaledTime = now
        });
    }

    private void PruneExpiredDiscoveryRooms()
    {
        float now = Time.unscaledTime;
        for (int i = discoveredRooms.Count - 1; i >= 0; i--)
        {
            if (now - discoveredRooms[i].lastSeenUnscaledTime > DiscoveryRoomTimeoutSec)
            {
                discoveredRooms.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Decodifica um pacote recebido ('STATE|...', 'PING|...', 'BUILD|...') e aplica no estado local.
    /// Tambem pode ser chamado diretamente em testes EditMode sem abrir sockets reais.
    /// </summary>
    public bool ProcessIncomingPacket(string rawPacket)
    {
        if (string.IsNullOrEmpty(rawPacket)) return false;
        int sep = rawPacket.IndexOf('|');
        if (sep <= 0 || sep >= rawPacket.Length - 1) return false;

        string type = rawPacket.Substring(0, sep);
        string json = rawPacket.Substring(sep + 1);
        FragaCoopSession coop = FragaCoopSession.GetOrCreate();

        isApplyingRemotePacket = true;
        try
        {
            if (type == "STATE")
            {
                CoopMemberState state = JsonUtility.FromJson<CoopMemberState>(json);
                if (string.IsNullOrEmpty(state.playerId) || state.playerId == coop.localPlayerId)
                    return false;

                state.isConnected = true;
                state.isLocalPlayer = false;
                coop.UpsertRemoteMemberState(state);
                EnsureOrUpdateRemoteAvatar(state);
                return true;
            }
            if (type == "PING")
            {
                LanPingPacket pkt = JsonUtility.FromJson<LanPingPacket>(json);
                if (string.IsNullOrEmpty(pkt.playerId) || pkt.playerId == coop.localPlayerId)
                    return false;

                Vector3 pos = new Vector3(pkt.x, pkt.y, pkt.z);
                coop.SetMemberPing(pkt.playerId, pos, pkt.label, 18f);
                SpawnOrUpdatePingBeacon(pkt.playerId, pos, pkt.label, coop);
                if (HUDSurvivalOverlay.Instance != null)
                {
                    HUDSurvivalOverlay.Instance.ShowLootTooltip(
                        $"<color=#55EEFF>Sinal de aliado: {pkt.label} ({pos.x:F0}, {pos.z:F0})</color>");
                }
                return true;
            }
            if (type == "BUILD")
            {
                CoopBuildSyncEvent evt = JsonUtility.FromJson<CoopBuildSyncEvent>(json);
                string senderId = !string.IsNullOrEmpty(evt.actorPlayerId) ? evt.actorPlayerId : evt.ownerPlayerId;
                if (string.IsNullOrEmpty(evt.structureNetId) || senderId == coop.localPlayerId)
                    return false;

                ApplyRemoteBuildEvent(evt);
                coop.BroadcastBuildEvent(evt);
                return true;
            }
            if (type == "SHARE")
            {
                CoopInventoryShareEvent evt = JsonUtility.FromJson<CoopInventoryShareEvent>(json);
                if (string.IsNullOrEmpty(evt.fromPlayerId) || evt.fromPlayerId == coop.localPlayerId || evt.amount <= 0)
                    return false;

                if (LootManager.Instance != null)
                {
                    LootManager.Instance.DropItem(evt.itemId, evt.amount, evt.worldPosition + Vector3.up * 0.25f);
                }
                if (HUDSurvivalOverlay.Instance != null)
                {
                    ItemData data = ItemDatabase.GetItem(evt.itemId);
                    string itemName = data != null ? data.itemName : $"Item {evt.itemId}";
                    HUDSurvivalOverlay.Instance.ShowLootTooltip(
                        $"<color=#66FFBB>{evt.fromPlayerName} compartilhou no chao: {evt.amount}x {itemName}</color>");
                }
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FragaLanCoopTransport] Pacote ignorado: {ex.Message}");
        }
        finally
        {
            isApplyingRemotePacket = false;
        }

        return false;
    }

    private static WorldBlueprintSite FindSiteByNetId(string netId)
    {
        if (string.IsNullOrEmpty(netId)) return null;
        var sites = WorldBlueprintSite.AllSites;
        for (int i = 0; i < sites.Count; i++)
        {
            if (sites[i] != null && sites[i].structureId == netId)
                return sites[i];
        }
        return null;
    }

    private static StructureHealth FindStructureByNetId(string netId)
    {
        if (string.IsNullOrEmpty(netId)) return null;
        var structures = StructureHealth.AllStructures;
        for (int i = 0; i < structures.Count; i++)
        {
            if (structures[i] != null && structures[i].structureId == netId)
                return structures[i];
        }
        return null;
    }

    private void ApplyRemoteBuildEvent(CoopBuildSyncEvent evt)
    {
        WorldBlueprintSite existingSite = FindSiteByNetId(evt.structureNetId);
        StructureHealth existingStruct = FindStructureByNetId(evt.structureNetId);

        if (evt.actionType == "Cancel")
        {
            if (existingSite != null) BlueprintPlacementManager.SafeDestroy(existingSite.gameObject);
            return;
        }

        if (evt.actionType == "Destroy")
        {
            if (existingStruct != null) BlueprintPlacementManager.SafeDestroy(existingStruct.gameObject);
            return;
        }

        if (evt.actionType == "Repair")
        {
            if (existingStruct != null)
            {
                existingStruct.currentHealth = Mathf.Min(existingStruct.maxHealth, existingStruct.currentHealth + 65f);
                if (BlueprintPlacementManager.Instance != null)
                    BlueprintPlacementManager.Instance.PlayHammerSound(existingStruct.transform.position);
            }
            return;
        }

        if (evt.actionType == "Deposit" || evt.actionType == "Complete")
        {
            if (existingSite != null)
            {
                existingSite.currentLogs = Mathf.Clamp(evt.currentLogs, existingSite.currentLogs, existingSite.neededLogs);
                existingSite.currentStones = Mathf.Clamp(evt.currentStones, existingSite.currentStones, existingSite.neededStones);
                if (evt.isComplete || evt.actionType == "Complete" || existingSite.IsComplete)
                {
                    existingSite.ForceCompleteFromSave(-1f);
                }
                else
                {
                    existingSite.UpdateBlueprintProgressVisuals();
                }
                return;
            }
        }

        if (evt.actionType == "Place" || (evt.actionType == "Complete" && existingStruct == null))
        {
            if (existingSite != null || existingStruct != null) return;

            BlueprintPlacementManager bpMgr = BlueprintPlacementManager.Instance != null
                ? BlueprintPlacementManager.Instance
                : FindAnyObjectByType<BlueprintPlacementManager>();
            if (bpMgr == null) return;

            GameObject obj = bpMgr.SpawnStructureObject(evt.structureName, evt.position, evt.rotationY, evt.neededLogs, evt.neededStones);
            if (obj == null) return;

            WorldBlueprintSite site = obj.GetComponent<WorldBlueprintSite>();
            if (site != null)
            {
                site.structureId = evt.structureNetId;
                site.ownerPlayerId = evt.ownerPlayerId;
                site.ownerPlayerName = evt.ownerName;
                site.isSharedWithGroup = evt.isSharedWithGroup;
                site.currentLogs = evt.currentLogs;
                site.currentStones = evt.currentStones;
                if (evt.isComplete || evt.actionType == "Complete")
                    site.ForceCompleteFromSave(-1f);
                else
                    site.UpdateBlueprintProgressVisuals();
            }
        }
    }

    private void EnsureOrUpdateRemoteAvatar(in CoopMemberState state)
    {
        remoteAvatarTargets[state.playerId] = state.worldPosition;
        if (remoteAvatars.TryGetValue(state.playerId, out GameObject existing) && existing != null)
        {
            TextMesh tm = existing.GetComponentInChildren<TextMesh>();
            if (tm != null) tm.text = $"{state.displayName} ({state.health:F0} HP)";
            return;
        }

        GameObject root = new GameObject($"CoopRemoteAvatar_{state.playerId}");
        root.transform.position = state.worldPosition;

        Color playerCol = new Color(0.30f, 0.90f, 1.0f);
        ColorUtility.TryParseHtmlString(state.colorHex, out playerCol);

        GameObject playerPrefab = Resources.Load<GameObject>("Prefabs/Player_Prefab");
        bool builtFromPrefab = false;
        if (playerPrefab != null)
        {
            GameObject model = Instantiate(playerPrefab, root.transform, false);
            model.name = "RemoteModel";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            foreach (Collider col in model.GetComponentsInChildren<Collider>(true)) DestroySafe(col);
            foreach (Camera camComp in model.GetComponentsInChildren<Camera>(true)) DestroySafe(camComp);
            foreach (AudioListener al in model.GetComponentsInChildren<AudioListener>(true)) DestroySafe(al);
            foreach (MonoBehaviour mb in model.GetComponentsInChildren<MonoBehaviour>(true)) DestroySafe(mb);
            if (model.GetComponentInChildren<Renderer>(true) != null)
            {
                builtFromPrefab = true;
            }
            else
            {
                DestroySafe(model);
            }
        }

        if (!builtFromPrefab)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material cloakMat = new Material(lit);
            cloakMat.color = Color.Lerp(new Color(0.16f, 0.20f, 0.24f), playerCol, 0.45f);
            Material gearMat = Resources.Load<Material>("Materials/Mat_DarkForest_Bark") ?? cloakMat;

            CreateAvatarPart(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.98f, 0f), new Vector3(0.56f, 0.88f, 0.42f), cloakMat);
            CreateAvatarPart(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, 1.84f, 0f), new Vector3(0.36f, 0.40f, 0.36f), cloakMat);
            CreateAvatarPart(PrimitiveType.Cube, "Backpack", root.transform, new Vector3(0f, 1.15f, -0.26f), new Vector3(0.42f, 0.56f, 0.24f), gearMat);
        }

        // Anel tatico no chao + indicador dorsal da cor do aliado
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        Material teamMat = new Material(unlit);
        teamMat.color = new Color(playerCol.r, playerCol.g, playerCol.b, 0.92f);
        CreateAvatarPart(PrimitiveType.Cylinder, "TeamGroundRing", root.transform, new Vector3(0f, 0.04f, 0f), new Vector3(0.92f, 0.02f, 0.92f), teamMat);
        CreateAvatarPart(PrimitiveType.Sphere, "TeamBeaconOrb", root.transform, new Vector3(0f, 2.08f, 0f), new Vector3(0.16f, 0.16f, 0.16f), teamMat);

        GameObject labelObj = new GameObject("NameTag");
        labelObj.transform.SetParent(root.transform, false);
        labelObj.transform.localPosition = new Vector3(0f, 2.38f, 0f);
        TextMesh tag = labelObj.AddComponent<TextMesh>();
        tag.text = $"{state.displayName} ({state.health:F0} HP)";
        tag.characterSize = 0.08f;
        tag.fontSize = 36;
        tag.anchor = TextAnchor.MiddleCenter;
        tag.alignment = TextAlignment.Center;
        tag.color = playerCol;

        remoteAvatars[state.playerId] = root;
    }

    private static void CreateAvatarPart(PrimitiveType type, string partName, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = partName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        DestroySafe(go.GetComponent<Collider>());
        Renderer r = go.GetComponent<Renderer>();
        if (r != null && mat != null) r.sharedMaterial = mat;
    }

    private static void DestroySafe(UnityEngine.Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    private void SpawnOrUpdatePingBeacon(string playerId, Vector3 pos, string label, FragaCoopSession coop)
    {
        if (activePingBeacons.TryGetValue(playerId, out GameObject oldBeacon) && oldBeacon != null)
        {
            DestroySafe(oldBeacon);
        }

        Color pingCol = new Color(0.30f, 0.92f, 1.0f);
        if (coop != null)
        {
            for (int i = 0; i < coop.Members.Count; i++)
            {
                if (coop.Members[i].playerId == playerId)
                {
                    ColorUtility.TryParseHtmlString(coop.Members[i].colorHex, out pingCol);
                    break;
                }
            }
        }

        GameObject beacon = new GameObject($"CoopPingBeacon_{playerId}");
        beacon.transform.position = pos;

        Material glowMat = Resources.Load<Material>("Materials/Mat_Emissive_BeaconGlow");
        if (glowMat == null)
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            glowMat = new Material(unlit);
            glowMat.color = pingCol;
        }

        CreateAvatarPart(PrimitiveType.Cylinder, "PingRing", beacon.transform, new Vector3(0f, 0.08f, 0f), new Vector3(1.5f, 0.03f, 1.5f), glowMat);
        CreateAvatarPart(PrimitiveType.Cylinder, "PingBeam", beacon.transform, new Vector3(0f, 4.5f, 0f), new Vector3(0.14f, 4.5f, 0.14f), glowMat);

        GameObject lblObj = new GameObject("PingLabel");
        lblObj.transform.SetParent(beacon.transform, false);
        lblObj.transform.localPosition = new Vector3(0f, 2.4f, 0f);
        TextMesh tm = lblObj.AddComponent<TextMesh>();
        tm.text = string.IsNullOrEmpty(label) ? "SINAL" : label.ToUpperInvariant();
        tm.characterSize = 0.09f;
        tm.fontSize = 32;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = pingCol;

        activePingBeacons[playerId] = beacon;
        activePingExpires[playerId] = Time.unscaledTime + 18f;
    }

    private void UpdateRemoteAvatars(float dt)
    {
        float lerpT = Mathf.Clamp01(dt * 10f);
        Camera cam = Camera.main;

        if (remoteAvatars.Count > 0)
        {
            staleKeysBuffer.Clear();
            foreach (var kv in remoteAvatars)
            {
                if (kv.Value == null)
                {
                    staleKeysBuffer.Add(kv.Key);
                    continue;
                }
                if (remoteAvatarTargets.TryGetValue(kv.Key, out Vector3 targetPos))
                {
                    kv.Value.transform.position = Vector3.Lerp(kv.Value.transform.position, targetPos, lerpT);
                }
                if (cam != null)
                {
                    Transform tagT = kv.Value.transform.Find("NameTag");
                    if (tagT != null)
                    {
                        tagT.rotation = Quaternion.LookRotation(tagT.position - cam.transform.position);
                    }
                }
            }
            for (int i = 0; i < staleKeysBuffer.Count; i++)
            {
                remoteAvatars.Remove(staleKeysBuffer[i]);
            }
        }

        if (activePingBeacons.Count > 0)
        {
            float now = Time.unscaledTime;
            expiredPingKeysBuffer.Clear();
            foreach (var kv in activePingBeacons)
            {
                if (kv.Value == null || !activePingExpires.TryGetValue(kv.Key, out float exp) || now >= exp)
                {
                    if (kv.Value != null) DestroySafe(kv.Value);
                    expiredPingKeysBuffer.Add(kv.Key);
                    continue;
                }
                Transform beam = kv.Value.transform.Find("PingBeam");
                if (beam != null)
                {
                    float pulse = 0.12f + Mathf.Sin(now * 5f) * 0.04f;
                    beam.localScale = new Vector3(pulse, 4.5f, pulse);
                }
                if (cam != null)
                {
                    Transform lbl = kv.Value.transform.Find("PingLabel");
                    if (lbl != null)
                    {
                        lbl.rotation = Quaternion.LookRotation(lbl.position - cam.transform.position);
                    }
                }
            }
            for (int i = 0; i < expiredPingKeysBuffer.Count; i++)
            {
                activePingBeacons.Remove(expiredPingKeysBuffer[i]);
                activePingExpires.Remove(expiredPingKeysBuffer[i]);
            }
        }
    }

    private void ClearRemoteAvatars()
    {
        foreach (var kv in remoteAvatars)
        {
            if (kv.Value != null) DestroySafe(kv.Value);
        }
        remoteAvatars.Clear();
        remoteAvatarTargets.Clear();

        foreach (var kv in activePingBeacons)
        {
            if (kv.Value != null) DestroySafe(kv.Value);
        }
        activePingBeacons.Clear();
        activePingExpires.Clear();
    }
}
