<div align="center">
  <h1>🌲 Fraga: A Noite Esquecida</h1>
  <p><b>A experiência definitiva de Sobrevivência Sombria AAA para Dispositivos Móveis.</b></p>
</div>

<br/>

> [!CAUTION]
> **Sobrevivência Extrema:** Este projeto inclui mecânicas realistas altamente punitivas. Dano grave resulta em hemorragias fatais e fraturas expostas. A chuva e a noite trazem a hipotermia silenciosa. Fica o aviso.

## 📖 Sobre o Projeto

*Fraga: A Noite Esquecida* é um simulador de sobrevivência visceral, desenhado para elevar o padrão gráfico e mecânico dos dispositivos móveis. Fortemente inspirado pela tensão e arquitetura de clássicos do género como *The Forest*, o jogo utiliza o poder de **Bibliotecas Nativas (C++)** interligadas com a **Unity (URP)** para garantir performance máxima, mesmo lidando com centenas de árvores ao vento, simulações atmosféricas complexas e I.A. predatória inteligente.

---

## ⚙️ Funcionalidades e Mecânicas Core

### 1. Sistema Atmosférico AAA
- **Shaders Nativos e Vento Dinâmico (FoliageSway.shader):** Todas as árvores, arbustos e flora reagem matematicamente à força, direção e rajadas do vento geradas pelo WeatherSystem.
- **Hipotermia e Clima:** Estar debaixo de chuva não é apenas visual; acumulas uma barra invisível de Wetness (Encharcado) que destrói a temperatura do teu corpo, obrigando-te a construir abrigos ou fogueiras.

### 2. Anatomia e Traumas Realistas
Esquece as "barras de vida" genéricas. O teu corpo sofre as consequências da floresta:
- **[SANGRAMENTO]:** Ataques brutais rebentam as tuas veias. Perdes vida por segundo até morreres ou usares *Ligaduras*.
- **[FRATURA]:** Golpes pesados partem os teus ossos. A tua velocidade é esmagada e deixas de poder correr até aplicares uma *Tala de Madeira*.

### 3. Geração Procedural e Design de Nível
- **Injeções em Tempo Real:** Elementos monumentais como o majestoso *Farol Celta* e estruturas ancestrais são renderizados em runtime via script de gestão do Mapa, garantindo que o mundo é moldável e expandível dinamicamente.

---

## 🛠 Stack Tecnológica

| Tecnologia | Função no Projeto |
| :--- | :--- |
| **Unity (URP)** | Motor gráfico configurado para *Mobile-High End*. |
| **C++ Nativo** | Módulos intensivos de IA, Pathfinding e Gestão de Memória bruta exportados em .so / .dll. |
| **C#** | Pontes de Interoperabilidade, UI/UX Profissional e Lógica de Gameplay em High-Level. |
| **Git LFS** | Alojamento do UIManager gigantesco e futuros assets de topo de gama. |

---

## 🗂 Estrutura do Repositório

O nosso código encontra-se dividido em dois pólos para facilitar a gestão:

* **Branch main:** Contém todo o ambiente Unity. As cenas (.unity), prefabs, definições do URP, e as UI. Para abrires o jogo para testar/editar graficamente, faz clone deste branch.
* **Branch 
ative-source:** A forja. Contém o código C++ original (FragaNative.slnx), o Visual Studio Solution e as pontes de memória pura que alimentam a inteligência do jogo.

---

## 🚀 Como Iniciar

1. Certifica-te de que tens o Git LFS instalado na tua máquina (git lfs install).
2. Clona o projeto: 
   `ash
   git clone https://github.com/nicholashenrique47/Fraga-Game.git
   `
3. Abre o diretório clonado no **Unity Hub** (versão 2026+ com Universal Render Pipeline instalado).
4. *Bem-vindo à Fraga.*

> [!TIP]
> **Performance Profiler:** Ao testares no Editor da Unity, tem atenção à ferramenta de Performance Profiling nativa, ativada por defeito, para garantir que as tuas modificações não afetam a fluidez mobile (60FPS).