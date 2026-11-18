<div align="center">
  <h1>Fraga: A Noite Esquecida</h1>
  <p><b>Simulador de Sobrevivência Sombria e Terror Cooperativo para Android e PC (Unity URP + C++ Nativo).</b></p>
</div>

<br/>

> [!CAUTION]
> **Sobrevivência Extrema:** Este projeto inclui mecânicas realistas altamente punitivas. Dano grave resulta em hemorragias fatais e fraturas expostas. A chuva e a noite trazem a hipotermia silenciosa. Fica o aviso.

## Sobre o Projeto

*Fraga: A Noite Esquecida* é um simulador de sobrevivência em primeira pessoa na Ilha da Fraga (`768x768m`), desenhado para unir ambientação densa, combate visceral e alta taxa de quadros no mobile e no desktop. Inspirado pela tensão de clássicos como *The Forest*, o jogo combina **Bibliotecas Nativas (C++)** para cálculo de *FlowField* com **Unity (URP)**, renderização GPU Instanced de enxames e interface reativa *Zero-GC*.

Para consultar os diagramas detalhados de boot, fisiologia, combate, construção, IA e rede Co-op, acesse [SYSTEM_FLOWCHARTS.md](file:///C:/Users/Windows%2010/Fraga-Game/SYSTEM_FLOWCHARTS.md).

---

## Funcionalidades e Mecânicas Principais

### 1. Sistema Atmosférico e Termorregulação
- **Shaders Nativos e Vento Dinâmico (`FoliageSway.shader`):** Árvores, arbustos e vegetação reagem matematicamente à força, direção e rajadas do vento geradas pelo `WeatherSystem`.
- **Hipotermia e Clima (`BodyTemperatureSystem`):** Tomar chuva acumula umidade corporal (`Wetness`), derrubando a temperatura térmica até causar calafrios na câmera e hipotermia, exigindo abrigos, fogueiras, tochas ou casacos de pele.

### 2. Anatomia e Traumas Realistas (`PlayerStatus`)
Além da barra de vida, o corpo sofre condições clínicas distintas:
- **[SANGRAMENTO]:** Golpes profundos causam perda contínua de vida até o uso de *Bandagem* (`ID 206`) ou repouso em cabana.
- **[FRATURA]:** Impactos severos bloqueiam corrida e esquiva até a aplicação de uma *Tala de Madeira* (`ID 209`).
- **[VENENO / DOENÇA / EXAUSTÃO]:** Exigem antídotos/ervas e gestão de estamina (a exaustão trava ações intensas abaixo de 18 de estamina).

### 3. Mundo Aberto Procedural (`768x768m`) e Chefes
- **Marcos Exploráveis (`FragaWorldBlueprint`):** Regiões determinísticas com o *Acampamento da Queda*, *Farol Celta*, *Caverna Sagrada*, *Ruínas Ancestrais* e mapa topográfico completo em `[M]`.
- **Hordas GPU Instanced e Boss (`VultoManager` + `BossSystem`):** Vultos reagem a fogueiras, barreiras de sal e estruturas; o *Vulto Ancião da Fraga* desperta na Noite 5 ou na Caverna Sagrada com invocação de enxame e ataques em área telegrafados.

### 4. UI Responsiva, Construção Modular e Co-op LAN/IP
- **Mochila, Fabricação & Status Clínico (`[I]` / `[K]`):** Hub de 4 abas (`MOCHILA`, `FABRICACAO`, `PERSONAGEM [K]`, `GRUPO CO-OP`) com cache *Zero-GC*, compactação de pilhas, cálculo de peso (`kg`), 7 indicadores fisiológicos e tratamento de traumas em 1 clique.
- **Livro de Construção (`[B]`) & Fluxograma de Sobrevivência:** Guia passo a passo integrado ao catálogo de estruturas, validação de terreno/água e *snapping* modular, além de baú com controle de acesso (`GRUPO CO-OP` / `PRIVADO`).
- **Multiplayer Cooperativo LAN / VPN (`FragaCoopSession` + `FragaLanCoopTransport`):** Descoberta automática em rede local (`UDP 47777`), conexão direta por IP (`TCP 7777`), sincronização de estado a 5 Hz, réplica de construções (`BUILD|`), sinais táticos 3D e marcadores ao vivo no mapa (`PING|`).
- **Testes Automatizados (`EditMode`):** Cobertura de regressão em [FragaCoreEditModeTests.cs](file:///C:/Users/Windows%2010/Fraga-Game/Assets/Scripts/Editor/FragaCoreEditModeTests.cs).

---

## Stack Tecnológica

| Tecnologia | Função no Projeto |
| :--- | :--- |
| **Unity (URP)** | Renderização otimizada para *Mobile High-End* e Desktop. |
| **C++ Nativo (`FragaNative`)** | Geração de grade do mapa e *FlowField* em memória fixada (`GCHandle`). |
| **C# (`Assets/Scripts/`)** | Sistemas de gameplay, IA, fisiologia, UI *Zero-GC* e transporte LAN/IP. |
| **Git LFS** | Versionamento de prefabs, materiais, texturas e cenas pesadas. |

---

## Estrutura do Repositório

- **Branch `main`:** Ambiente completo da Unity (cenas, prefabs, materiais URP, shaders, scripts C# e testes EditMode).
- **Branch `native-source`:** Código-fonte C++ nativo (`FragaNative.slnx`) e pontes de memória de alto desempenho.
- **Documentação Técnica de Fluxo:** [SYSTEM_FLOWCHARTS.md](file:///C:/Users/Windows%2010/Fraga-Game/SYSTEM_FLOWCHARTS.md).

---

## Como Iniciar

1. Certifique-se de ter o **Git LFS** instalado (`git lfs install`).
2. Clone o repositório:
   ```bash
   git clone https://github.com/nicholashenrique47/Fraga-Game.git
   ```
3. Abra o diretório clonado no **Unity Hub** (Unity 6 / 2026+ com módulo **Universal Render Pipeline** e **Android Build Support**).
4. Execute os testes **EditMode** pelo *Unity Test Runner* para validar todos os sistemas antes de gerar builds.

> [!TIP]
> **Performance Mobile (60 FPS):** Evite alocações no `Update()`, mantenha o uso de `MaterialPropertyBlock` para feedbacks de hit e prefira o `Mobile` / `Auto` do `FragaInputRouter` para validar controles táteis no Editor.
