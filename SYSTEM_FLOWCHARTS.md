# Fluxogramas e Arquitetura de Sistemas — Fraga: A Noite Esquecida

Este documento apresenta os fluxogramas técnicos oficiais de funcionamento dos sistemas centrais do projeto, alinhados diretamente com os scripts C# em `Assets/Scripts/` e a ponte nativa C++ (`FragaNative`).

---

## 1. Boot do Mundo e Ponte Nativa C++ (FlowField 768x768m)

O [GameManager.cs](file:///C:/Users/Windows%2010/Fraga-Game/Assets/Scripts/GameManager.cs) fixa buffers gerenciados em memória (`GCHandleType.Pinned`) para permitir que a DLL/SO nativa `FragaNative` escreva o mapa e calcule o *FlowField* em C++ puro sem alocações no Garbage Collector do C#.

```mermaid
flowchart TD
    Boot["MainMenuManager.Start()"] --> Choice{"Save local existe e jogador escolheu Continuar?"}
    Choice -- "Sim" --> Load["GameManager.ContinueGame()"]
    Choice -- "Nao / Novo Mundo" --> NewGame["GameManager.StartNewGame(seed)"]

    Load --> InitGrid["FragaWorldBlueprint.Initialize(seed, 768, 768)"]
    NewGame --> InitGrid
    InitGrid --> PinMem["GameManager.Awake(): GCHandle.Alloc (Pinned) mapData / flowFieldX / flowFieldY"]
    PinMem --> Stamp["FragaWorldBlueprint.StampNativeMapGrid(mapDataPtr)"]
    Stamp --> SpawnEnv["MapRenderer + ResourceManager + EntityEventSystem + TerrainMeshGenerator"]

    SpawnEnv --> Coroutine["FlowFieldLoop() a cada 0.25s (4 Hz)"]
    Coroutine --> NativeCall["DllImport FragaNative.CalculateFlowField(mapDataPtr, outX, outY, targetX, targetY)"]
    NativeCall --> Vultos["VultoManager le flowFieldX/Y e avanca Vultos via GPU Instancing"]
```

---

## 2. Fisiologia, Clima e Tratamento de Traumas

A sobrevivência integra clima ([WeatherSystem.cs](file:///C:/Users/Windows%2010/Fraga-Game/Assets/Scripts/WeatherSystem.cs)), ciclo dia/noite ([DayNightCycle.cs](file:///C:/Users/Windows%2010/Fraga-Game/Assets/Scripts/DayNightCycle.cs)), termorregulação ([BodyTemperatureSystem.cs](file:///C:/Users/Windows%2010/Fraga-Game/Assets/Scripts/BodyTemperatureSystem.cs)) e diagnóstico clínico em [PlayerStatus.cs](file:///C:/Users/Windows%2010/Fraga-Game/Assets/Scripts/PlayerStatus.cs) + [SurvivalInventoryCraftUI.cs](file:///C:/Users/Windows%2010/Fraga-Game/Assets/Scripts/SurvivalInventoryCraftUI.cs).

```mermaid
flowchart LR
    Env["Clima (Chuva / Vento) + Hora (Dia/Noite)"] --> Temp["BodyTemperatureSystem"]
    Heat["Fogueira (BonfireEntity) / Abrigo / Tocha / Casaco"] --> Temp
    Temp -- "Umidade alta & frio" --> Hypo["Hipotermia + Calafrio na camera e braços"]
    Temp -- "Temperatura estavel" --> Status["PlayerStatus"]
    Hypo --> Status

    Combat["Dano de Vulto / Lobo / Queda / Fome / Sede"] --> Status
    Status --> Bleed["Sangramento (drena Vida/s)"]
    Status --> Frac["Fratura (bloqueia Corrida e Esquiva)"]
    Status --> Sick["Doenca / Veneno"]
    Status --> Exhaust["Estamina <= 0 -> Exaustao travada ate >= 18"]

    Bleed --> Treat1["Bandagem (ID 206) -> Cura Sangramento"]
    Frac --> Treat2["Tala de Madeira (ID 209) -> Cura Fratura"]
    Sick --> Treat3["Antidoto / Cha / Ervas (IDs 208/213)"]
    Status --> Sanity["SanitySystem (Noite, Boss e Fome drenam Sanidade)"]
```

---

## 3. Roteamento de Input Híbrido (PC/Mobile) e Combate

O [FragaInputRouter.cs](file:///C:/Users/Windows%2010/Fraga-Game/Assets/Scripts/FragaInputRouter.cs) centraliza entradas de teclado/mouse e controles táteis ([MobileInputController.cs](file:///C:/Users/Windows%2010/Fraga-Game/Assets/Scripts/MobileInputController.cs) e [CameraTouchController.cs](file:///C:/Users/Windows%2010/Fraga-Game/Assets/Scripts/CameraTouchController.cs)), respeitando modais abertos (`IsGameplayInputBlocked`).

```mermaid
flowchart TD
    Router["FragaInputRouter.Update() (Modo: Auto / Mobile / Desktop)"] --> ModalCheck{"Algum modal aberto? (Mochila, Livro, Mapa, Bau, Opcoes)"}
    ModalCheck -- "Sim" --> Block["Bloqueia ataque, olhar e coleta no mundo"]
    ModalCheck -- "Nao" --> Sources["Le Teclado/Mouse + Botoes Mobile + CameraTouchZone"]

    Sources --> Move["MoveInput + Sprint / Crouch / Jump"]
    Sources --> Dodge["TryDodge() -> Consome 18 Estamina, bloqueado se Fraturado/Exausto, concede i-frames"]
    Sources --> Equip{"Arma equipada na Mao Direita"}

    Equip -- "Machado / Picareta / Faca / Lanca / Clava" --> Melee["ChoppingAndCombatSystem: Combo 1-2-3, Golpe Pesado, Parry/Guarda"]
    Equip -- "Arco (ID 305) / Funda (ID 314)" --> Ranged["BowCombatSystem: Tensionar -> Mirar -> Disparar projetil + Arco Viewmodel"]
    Equip -- "Desarmado" --> Gather["Coleta manual [E] de pedras, galhos, ervas e interacao com estruturas"]

    Melee --> Viewmodel["FirstPersonViewmodelController (bracos, ferramentas 3D e recoil)"]
    Ranged --> Viewmodel
    Dodge --> Controller["PlayerController"]
```

---

## 4. Ciclo de Inventário, Fabricação e Construção

O ciclo econômico conecta coleta de campo ([InventoryAdvanced.cs](file:///C:/Users/Windows%2010/Fraga-Game/Assets/Scripts/InventoryAdvanced.cs)), transformação de itens ([CraftingSystem.cs](file:///C:/Users/Windows%2010/Fraga-Game/Assets/Scripts/CraftingSystem.cs)) e canteiros modulares em mundo aberto ([BlueprintPlacementManager.cs](file:///C:/Users/Windows%2010/Fraga-Game/Assets/Scripts/BlueprintPlacementManager.cs)).

```mermaid
stateDiagram-v2
    [*] --> ColetaCampo: "Coleta [E] no chao ou corte com ferramenta"
    ColetaCampo --> Mochila: "InventoryAdvanced (24 slots, peso kg, durabilidade)"
    Mochila --> Fabricacao: "[I] CraftingSystem (Tiers 0-3: Maos, Fogueira, Bancada, Forja)"
    Fabricacao --> Mochila: "Gera Ferramentas, Remedios, Flechas e Armaduras"
    Mochila --> LivroProjetos: "[B] SurvivalConstructionBookModal"

    LivroProjetos --> FantasmaAzul: "Seleciona projeto -> CreateGhostVisual() + Snapping"
    FantasmaAzul --> ObraNoMundo: "Valida terreno/agua -> Spawna WorldBlueprintSite"
    ObraNoMundo --> EmConstrucao: "Jogador ou aliado Co-op deposita Toras e Pedras [E]"
    ObraNoMundo --> Cancelada: "Segurar [X] reembolsa materiais depositados"
    EmConstrucao --> EstruturaPronta: "Materiais 100% -> CompleteStructure()"

    EstruturaPronta --> Cabana: "ShelterInteractable (Dormir & Salvar Jogo)"
    EstruturaPronta --> Bau: "StorageChestInteractable (Scroll + Acesso Co-op/Privado)"
    EstruturaPronta --> Utilidade: "Secador de Carne / Coletor de Chuva / Palicada / Porta"
```

---

## 5. Ecossistema de IA, Diretor de Horda e Chefe Final

A ameaça da ilha escala dinamicamente entre fauna ([WildlifeAIEngine.cs](file:///C:/Users/Windows%2010/Fraga-Game/Assets/Scripts/WildlifeAIEngine.cs)), enxames sombrios instanciados na GPU ([VultoManager.cs](file:///C:/Users/Windows%2010/Fraga-Game/Assets/Scripts/VultoManager.cs)), o diretor de tensão ([HordeDirector.cs](file:///C:/Users/Windows%2010/Fraga-Game/Assets/Scripts/HordeDirector.cs)) e o chefe ([BossSystem.cs](file:///C:/Users/Windows%2010/Fraga-Game/Assets/Scripts/BossSystem.cs)).

```mermaid
stateDiagram-v2
    [*] --> Dormant: "Aguarda Dia 5 (>= 20h) OU jogador perto da Caverna Sagrada a noite"
    Dormant --> Chasing: "SpawnBoss() (500 HP, BossHealthBar ativa no HUD)"

    Chasing --> Summoning: "summonTimer <= 0 -> Invoca 3 Vultos via VultoManager"
    Summoning --> Chasing: "Fim do channeling"

    Chasing --> AreaAttack: "aoeTimer <= 0 -> Cria AoE Indicator pulsante no chao"
    AreaAttack --> Chasing: "Detonacao AoE (esquiva com i-frames evita dano e sangramento)"

    Chasing --> Fase2Enfurecido: "currentHealth <= 50% HP (IsEnraged = true, velocidade e frequencia aumentadas)"
    Fase2Enfurecido --> Summoning
    Fase2Enfurecido --> AreaAttack

    Chasing --> Dead: "currentHealth <= 0 -> Vitoria, drops e fim da BossHealthBar"
    Fase2Enfurecido --> Dead
```

---

## 6. Rede Co-op LAN/IP e Persistência (`SaveSystem`)

O multiplayer cooperativo roda de forma nativa em C# sem SDK externo pago via [FragaCoopSession.cs](file:///C:/Users/Windows%2010/Fraga-Game/Assets/Scripts/FragaCoopSession.cs) e [FragaLanCoopTransport.cs](file:///C:/Users/Windows%2010/Fraga-Game/Assets/Scripts/FragaLanCoopTransport.cs), enquanto a persistência usa [SaveSystem.cs](file:///C:/Users/Windows%2010/Fraga-Game/Assets/Scripts/SaveSystem.cs) com cópia de segurança `.bak`.

```mermaid
sequenceDiagram
    participant Host as "Anfitriao (CoopHost)"
    participant UDP as "Discovery UDP :47777"
    participant TCP as "Relay TCP :7777"
    participant Client as "Amigo (CoopClient)"
    participant World as "Mapa 3D + SurvivalMapSystem + SaveSystem"

    Host->>UDP: "Broadcast beacon <ip>|FRAGA_LAN|<joinCode>|<hostName>|7777|<membros>"
    Client->>UDP: "Escuta beacons locais (ou insere IP manualmente)"
    Client->>TCP: "Conecta em <hostIp>:7777"

    loop "A cada 0.20s (5 Hz)"
        Host->>TCP: "STATE|<CoopMemberState JSON>"
        Client->>TCP: "STATE|<CoopMemberState JSON>"
        TCP-->>Host: "Atualiza Avatar 3D, CoopPartyHUD e Pips no Mapa"
        TCP-->>Client: "Atualiza Avatar 3D, CoopPartyHUD e Pips no Mapa"
    end

    Client->>TCP: "PING|<LanPingPacket JSON>"
    TCP-->>World: "Spawna Beacon 3D brilhante + marcador no Minimapa/Mapa [M]"

    Host->>TCP: "BUILD|<CoopBuildSyncEvent JSON>"
    TCP-->>World: "Replica WorldBlueprintSite + Permissao (Grupo vs Privado)"
    World->>World: "SaveSystem grava fraga_save_v2.dat (SaveData v4) + .bak ao dormir/pausar"
```
