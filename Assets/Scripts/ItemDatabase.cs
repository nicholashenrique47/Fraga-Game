using UnityEngine;
using System.Collections.Generic;

/// ItemDatabase: Base de dados central de todos os itens do jogo.
/// Cada item tem ID unico, tipo, categoria, slot de equipamento, stats de combate/sobrevivencia e stackability.

public enum ItemType
{
    Resource,       // Madeira, Pedra, Oleo, Resina, Osso, Tecido, Carvao
    Consumable,     // Comida, Bebida, Pocoes, Cha, Cantil
    Tool,           // Machado, Picareta, Armas, Escudo, Vestuario
    Deployable,     // Armadilha, Fogueira, Bancada
    Quest,          // Itens de quest
    Cosmetic        // Lampioes cosmeticos
}

public enum EquipSlotType
{
    None,
    MainHand,
    OffHand,
    BodyArmor
}

public enum ItemCategory
{
    Resource,
    ToolAndWeapon,
    Consumable,
    ArmorAndLight,
    Deployable,
    Quest
}

[System.Serializable]
public class ItemData
{
    public int id;
    public string itemName;
    public string description;
    public ItemType type;
    public ItemCategory category = ItemCategory.Resource;
    public EquipSlotType equipSlot = EquipSlotType.None;
    public int maxStack = 1;

    // Consumiveis / Fisiologia
    public float hungerRestore;
    public float thirstRestore;
    public float sanityRestore;
    public float healthRestore;
    public float staminaRestore;
    public float warmthBonus;       // Aquece temperatura corporal (+C)
    public float poisonDuration;    // negativo = cura veneno
    public int craftOutputAmount = 1;
    public bool isPerishable;
    public float maxDurability;     // Segundos ate apodrecer (se isPerishable), cargas (Cantil) ou durabilidade de ferramenta
    public bool curesBleeding;
    public bool curesFracture;

    // Combate, Ferramentas & Protecao
    public float attackDamage = 8f;
    public float attackRange = 2.4f;
    public float attackCooldown = 0.45f;
    public float staminaCost = 10f;
    public float harvestTreeMultiplier = 1f;
    public float harvestRockMultiplier = 1f;
    public float armorDefense = 0f;     // Reducao percentual de dano (0..0.6)
    public float coldInsulation = 0f;   // Reducao percentual de perda termica (0..0.8)
}

public static class ItemDatabase
{
    private static Dictionary<int, ItemData> items;
    private static bool initialized = false;

    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;

        items = new Dictionary<int, ItemData>();

        // === RECURSOS ===
        Register(new ItemData { id = 100, itemName = "Madeira", description = "Galhos e troncos. Essencial para ferramentas, fogueiras e construcoes.", type = ItemType.Resource, category = ItemCategory.Resource, maxStack = 99 });
        Register(new ItemData { id = 101, itemName = "Pedra", description = "Pedra bruta da serra e ruinas. Usada em ferramentas e construcoes.", type = ItemType.Resource, category = ItemCategory.Resource, maxStack = 99 });
        Register(new ItemData { id = 102, itemName = "Oleo de Lampiao", description = "Combustivel inflamavel refinado para tochas e lampiao.", type = ItemType.Resource, category = ItemCategory.Resource, maxStack = 20 });
        Register(new ItemData { id = 103, itemName = "Ervas", description = "Plantas medicinais e fibrosas. Base para cordas, ataduras e chas.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 30, hungerRestore = 2f, sanityRestore = 10f, poisonDuration = -1f });
        Register(new ItemData { id = 105, itemName = "Pele de Animal", description = "Couro espesso para fabricar casacos termicos, cantis e armaduras.", type = ItemType.Resource, category = ItemCategory.Resource, maxStack = 15 });
        Register(new ItemData { id = 106, itemName = "Sal", description = "Mineral sagrado e conservante. Repele vultos e cura charque.", type = ItemType.Resource, category = ItemCategory.Resource, maxStack = 25 });
        Register(new ItemData { id = 107, itemName = "Ferro Velho", description = "Fragmentos de metal forjado das ruinas. Base para ferramentas fortes.", type = ItemType.Resource, category = ItemCategory.Resource, maxStack = 30 });
        Register(new ItemData { id = 110, itemName = "Flechas de Pedra", description = "Municao padrao para o Arco de Sobrevivencia.", type = ItemType.Resource, category = ItemCategory.Resource, maxStack = 40 });
        Register(new ItemData { id = 111, itemName = "Peixe Cru", description = "Recem-pescado. Asse na fogueira antes de comer.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 10, hungerRestore = 10f, poisonDuration = 15f, isPerishable = true, maxDurability = 220f });
        Register(new ItemData { id = 112, itemName = "Sementes Silvestres", description = "Plante em solo lavrado com a Enxada para cultivar bagas.", type = ItemType.Resource, category = ItemCategory.Resource, maxStack = 25 });
        Register(new ItemData { id = 113, itemName = "Carne Leve", description = "Carne de coelho crua. Melhor se assada ou salgada.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 10, hungerRestore = 6f, poisonDuration = 5f, isPerishable = true, maxDurability = 260f });
        Register(new ItemData { id = 114, itemName = "Pena de Aguia", description = "Pena rara de precisao. Usada em flechas penadas de longo alcance.", type = ItemType.Resource, category = ItemCategory.Resource, maxStack = 25 });
        Register(new ItemData { id = 115, itemName = "Coracao Sombrio", description = "O nucleo de poder do Vulto Anciao. Ofereca no Altar de Purificacao.", type = ItemType.Quest, category = ItemCategory.Quest, maxStack = 1 });
        Register(new ItemData { id = 116, itemName = "Pagina do Diario", description = "Memorias de um cacador da serra. Ler restaura 20 de sanidade.", type = ItemType.Consumable, category = ItemCategory.Quest, maxStack = 10, sanityRestore = 20f });
        Register(new ItemData { id = 117, itemName = "Flecha Penada", description = "Flecha equilibrada com penas de aguia. +45% de dano e velocidade.", type = ItemType.Resource, category = ItemCategory.Resource, maxStack = 40 });
        Register(new ItemData { id = 118, itemName = "Corda de Fibra", description = "Corda trancada de fibras vegetais para machados, arcos e armaduras.", type = ItemType.Resource, category = ItemCategory.Resource, maxStack = 25 });
        Register(new ItemData { id = 119, itemName = "Resina de Pinheiro", description = "Seiva inflamavel e cicatrizante extraida das arvores. Otimo para pomadas e fogo.", type = ItemType.Resource, category = ItemCategory.Resource, maxStack = 25 });
        Register(new ItemData { id = 120, itemName = "Osso", description = "Ossos resistentes de caca ou sepulturas. Base para clavas e armadura.", type = ItemType.Resource, category = ItemCategory.Resource, maxStack = 25 });
        Register(new ItemData { id = 121, itemName = "Tecido Rustico", description = "Pano resistente tecido de fibras. Usado em cantis e curativos.", type = ItemType.Resource, category = ItemCategory.Resource, maxStack = 20 });
        Register(new ItemData { id = 122, itemName = "Carvao Vegetal", description = "Madeira carbonizada na fogueira. Filtra agua turva e reforca forja.", type = ItemType.Resource, category = ItemCategory.Resource, maxStack = 30 });

        // === CONSUMIVEIS ===
        Register(new ItemData { id = 104, itemName = "Carne Crua", description = "Carne de cervo. Perigosa se nao for assada ou salgada.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 10, hungerRestore = 15f, poisonDuration = 10f, isPerishable = true, maxDurability = 300f });
        Register(new ItemData { id = 108, itemName = "Comida Podre", description = "Estragada e toxica. Pode virar adubo de Sementes Silvestres.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 10, hungerRestore = 2f, sanityRestore = -20f, poisonDuration = 60f });
        Register(new ItemData { id = 109, itemName = "Agua Limpa", description = "Agua fresca e purificada. Restaura muita sede.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 6, thirstRestore = 45f });
        Register(new ItemData { id = 123, itemName = "Agua Turva", description = "Agua coletada de rios e lagos. Ferva na fogueira ou filtre com carvao.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 6, thirstRestore = 24f, poisonDuration = 12f });

        // === CONSUMIVEIS AVANCADOS ===
        Register(new ItemData { id = 200, itemName = "Carne Assada", description = "Carne assada na brasa. Restaura fome e vida.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 10, hungerRestore = 40f, healthRestore = 12f, sanityRestore = 6f, isPerishable = true, maxDurability = 650f });
        Register(new ItemData { id = 201, itemName = "Sopa de Ervas", description = "Cura venenos, hidrata e acalma a mente.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 5, hungerRestore = 18f, sanityRestore = 25f, thirstRestore = 25f, warmthBonus = 1.2f, poisonDuration = -1f, isPerishable = true, maxDurability = 900f });
        Register(new ItemData { id = 202, itemName = "Bagas Silvestres", description = "Frutos acidos da floresta. Hidratam levemente e nao apodrecem rapido.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 20, hungerRestore = 10f, thirstRestore = 7f, staminaRestore = 8f });
        Register(new ItemData { id = 203, itemName = "Cogumelo Estranho", description = "Aparencia suspeita. Coma por conta e risco.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 10, hungerRestore = 5f, sanityRestore = -15f, poisonDuration = 30f });
        Register(new ItemData { id = 204, itemName = "Agua Benta", description = "Abencoa e restaura sanidade profunda.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 4, thirstRestore = 25f, sanityRestore = 55f });
        Register(new ItemData { id = 205, itemName = "Fosforos", description = "Acende fogueiras apagadas e reativa o lampiao.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 15 });
        Register(new ItemData { id = 206, itemName = "Ataduras", description = "Curativos medicos. Estancam sangramento e curam.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 6, healthRestore = 28f, curesBleeding = true });
        Register(new ItemData { id = 207, itemName = "Peixe Assado", description = "Excelente fonte de energia. Restaura fome e sede.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 10, hungerRestore = 35f, thirstRestore = 25f, sanityRestore = 10f, isPerishable = true, maxDurability = 550f });
        Register(new ItemData { id = 208, itemName = "Ensopado Druida", description = "Caldo ancestral. Restaura Fome, Sede, Vida, Folego e Sanidade ao maximo.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 3, hungerRestore = 100f, thirstRestore = 100f, sanityRestore = 100f, healthRestore = 100f, staminaRestore = 100f, warmthBonus = 3.5f, isPerishable = true, maxDurability = 1200f });
        Register(new ItemData { id = 209, itemName = "Tala de Madeira", description = "Imobiliza ossos quebrados. Cura fraturas.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 3, curesFracture = true });
        Register(new ItemData { id = 210, itemName = "Cantil de Couro", description = "Cantil reutilizavel com 3 goles. Encha no Coletor de Chuva ou com Agua Limpa.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 1, thirstRestore = 50f, staminaRestore = 15f, isPerishable = false, maxDurability = 3f });
        Register(new ItemData { id = 211, itemName = "Cha de Ervas Quente", description = "Infusao quente que aquece o corpo contra o frio da noite e acalma a mente.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 5, thirstRestore = 32f, sanityRestore = 18f, healthRestore = 10f, staminaRestore = 15f, warmthBonus = 2.6f, poisonDuration = -1f, isPerishable = true, maxDurability = 720f });
        Register(new ItemData { id = 212, itemName = "Charque Salgado", description = "Carne curada com sal ou secador. Nao apodrece jamais e sustenta longas viagens.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 12, hungerRestore = 38f, healthRestore = 6f, sanityRestore = 4f, isPerishable = false });
        Register(new ItemData { id = 213, itemName = "Pomada de Resina", description = "Balsamo forte de resina e ervas. Cura +50 Vida, veneno e sangramento.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 5, healthRestore = 50f, curesBleeding = true, poisonDuration = -1f });
        Register(new ItemData { id = 214, itemName = "Tonico da Fraga", description = "Elixir de bagas e ervas. Recupera folego instantaneo, sede e sanidade.", type = ItemType.Consumable, category = ItemCategory.Consumable, maxStack = 5, staminaRestore = 55f, thirstRestore = 22f, sanityRestore = 15f, healthRestore = 12f });

        // === FERRAMENTAS, ARMAS & EQUIPAMENTOS ===
        Register(new ItemData
        {
            id = 300, itemName = "Machado de Pedra",
            description = "Primeiro machado amarrado com corda. Dobra a coleta de madeira. Conserte na Bancada.",
            type = ItemType.Tool, category = ItemCategory.ToolAndWeapon, equipSlot = EquipSlotType.MainHand, maxStack = 1,
            attackDamage = 26f, attackRange = 2.7f, attackCooldown = 0.52f, staminaCost = 11f, harvestTreeMultiplier = 2.0f, maxDurability = 45f
        });
        Register(new ItemData
        {
            id = 301, itemName = "Picareta de Pedra",
            description = "Quebra rochas e extrai o dobro de ferro e sal nas ruinas. Conserte na Bancada.",
            type = ItemType.Tool, category = ItemCategory.ToolAndWeapon, equipSlot = EquipSlotType.MainHand, maxStack = 1,
            attackDamage = 22f, attackRange = 2.6f, attackCooldown = 0.60f, staminaCost = 13f, harvestRockMultiplier = 2.5f, maxDurability = 55f
        });
        Register(new ItemData
        {
            id = 302, itemName = "Faca de Caca",
            description = "Combo rapido, causa sangramento e aumenta rendimento ao esfolar.",
            type = ItemType.Tool, category = ItemCategory.ToolAndWeapon, equipSlot = EquipSlotType.MainHand, maxStack = 1,
            attackDamage = 28f, attackRange = 2.15f, attackCooldown = 0.30f, staminaCost = 7f, maxDurability = 60f
        });
        Register(new ItemData
        {
            id = 303, itemName = "Tocha",
            description = "Luz portatil na mao esquerda. Espanta vultos e aquece.",
            type = ItemType.Tool, category = ItemCategory.ArmorAndLight, equipSlot = EquipSlotType.OffHand, maxStack = 1,
            attackDamage = 15f, attackRange = 2.2f, attackCooldown = 0.45f, staminaCost = 9f, coldInsulation = 0.15f
        });
        Register(new ItemData
        {
            id = 304, itemName = "Lampiao Reforcado",
            description = "Luz intensa imune a chuva na mao secundaria.",
            type = ItemType.Tool, category = ItemCategory.ArmorAndLight, equipSlot = EquipSlotType.OffHand, maxStack = 1,
            coldInsulation = 0.10f
        });
        Register(new ItemData
        {
            id = 305, itemName = "Arco de Sobrevivencia",
            description = "Arma silenciosa de longa distancia. Segure Botao Direito/Mirar para retesar.",
            type = ItemType.Tool, category = ItemCategory.ToolAndWeapon, equipSlot = EquipSlotType.MainHand, maxStack = 1,
            attackDamage = 55f, attackRange = 45f, attackCooldown = 0.35f, staminaCost = 12f, maxDurability = 80f
        });
        Register(new ItemData
        {
            id = 306, itemName = "Vara de Pesca",
            description = "Para pescar em lagos, rios e na costa.",
            type = ItemType.Tool, category = ItemCategory.ToolAndWeapon, equipSlot = EquipSlotType.MainHand, maxStack = 1,
            attackDamage = 8f, attackRange = 6.0f, attackCooldown = 0.7f, staminaCost = 6f, maxDurability = 50f
        });
        Register(new ItemData
        {
            id = 307, itemName = "Enxada Rustica",
            description = "Lavra a terra para cultivar Sementes Silvestres.",
            type = ItemType.Tool, category = ItemCategory.ToolAndWeapon, equipSlot = EquipSlotType.MainHand, maxStack = 1,
            attackDamage = 14f, attackRange = 2.5f, attackCooldown = 0.50f, staminaCost = 10f, maxDurability = 65f
        });
        Register(new ItemData
        {
            id = 308, itemName = "Lanca Celta",
            description = "Longo alcance em estocadas. Segure Botao Direito para mirar e arremessar.",
            type = ItemType.Tool, category = ItemCategory.ToolAndWeapon, equipSlot = EquipSlotType.MainHand, maxStack = 1,
            attackDamage = 36f, attackRange = 3.25f, attackCooldown = 0.54f, staminaCost = 12f, maxDurability = 70f
        });
        Register(new ItemData
        {
            id = 309, itemName = "Escudo de Madeira",
            description = "Equipado na mao esquerda. Bloqueia 85% do dano e facilita Parry.",
            type = ItemType.Tool, category = ItemCategory.ArmorAndLight, equipSlot = EquipSlotType.OffHand, maxStack = 1,
            armorDefense = 0.12f, maxDurability = 80f
        });
        Register(new ItemData
        {
            id = 310, itemName = "Casaco de Pele",
            description = "Roupa termica de pele curtida. Protege do frio da serra e reduz dano.",
            type = ItemType.Tool, category = ItemCategory.ArmorAndLight, equipSlot = EquipSlotType.BodyArmor, maxStack = 1,
            armorDefense = 0.22f, coldInsulation = 0.65f, maxDurability = 100f
        });
        Register(new ItemData
        {
            id = 311, itemName = "Clava de Osso",
            description = "Porrete pesado reforcado com ossos. Alto impacto e tambem quebra pedras.",
            type = ItemType.Tool, category = ItemCategory.ToolAndWeapon, equipSlot = EquipSlotType.MainHand, maxStack = 1,
            attackDamage = 34f, attackRange = 2.55f, attackCooldown = 0.46f, staminaCost = 10f, harvestRockMultiplier = 1.4f, maxDurability = 65f
        });
        Register(new ItemData
        {
            id = 312, itemName = "Machado de Ferro",
            description = "Lamina forjada na bancada. Derruba arvores 3x mais rapido e causa dano letal.",
            type = ItemType.Tool, category = ItemCategory.ToolAndWeapon, equipSlot = EquipSlotType.MainHand, maxStack = 1,
            attackDamage = 38f, attackRange = 2.85f, attackCooldown = 0.45f, staminaCost = 10f, harvestTreeMultiplier = 3.0f, harvestRockMultiplier = 1.6f, maxDurability = 110f
        });
        Register(new ItemData
        {
            id = 313, itemName = "Armadura de Ossos",
            description = "Placas de ossos amarradas sobre couro. Maxima protecao fisica em combate.",
            type = ItemType.Tool, category = ItemCategory.ArmorAndLight, equipSlot = EquipSlotType.BodyArmor, maxStack = 1,
            armorDefense = 0.42f, coldInsulation = 0.30f, maxDurability = 120f
        });
        Register(new ItemData
        {
            id = 314, itemName = "Funda Celta",
            description = "Atiradeira de couro que usa Pedras (101) como municao silenciosa de longo alcance. Segure e solte para lancar.",
            type = ItemType.Tool, category = ItemCategory.ToolAndWeapon, equipSlot = EquipSlotType.MainHand, maxStack = 1,
            attackDamage = 30f, attackRange = 32f, attackCooldown = 0.45f, staminaCost = 8f, maxDurability = 75f
        });

        // === DEPLOYABLES / CONSTRUCOES ===
        Register(new ItemData { id = 400, itemName = "Kit de Fogueira", description = "Posiciona uma fogueira completa para cozinhar e aquecer.", type = ItemType.Deployable, category = ItemCategory.Deployable, maxStack = 3 });
        Register(new ItemData { id = 401, itemName = "Armadilha de Caca Pesada", description = "Causa dano pesado a predadores e vultos.", type = ItemType.Deployable, category = ItemCategory.Deployable, maxStack = 5 });
        Register(new ItemData { id = 402, itemName = "Circulo de Sal", description = "Repele entidades sombrias na area.", type = ItemType.Deployable, category = ItemCategory.Deployable, maxStack = 3 });
        Register(new ItemData { id = 403, itemName = "Coletor de Agua", description = "Passivamente coleta agua da chuva e orvalho.", type = ItemType.Deployable, category = ItemCategory.Deployable, maxStack = 2 });
        Register(new ItemData { id = 404, itemName = "Mesa de Trabalho", description = "Bancada para fabricacao avancada (Tier 2).", type = ItemType.Deployable, category = ItemCategory.Deployable, maxStack = 1 });
        Register(new ItemData { id = 405, itemName = "Caldeirao", description = "Culinaria druida e alquimia de sobrevivencia (Tier 3).", type = ItemType.Deployable, category = ItemCategory.Deployable, maxStack = 1 });
        Register(new ItemData { id = 406, itemName = "Laco para Coelhos", description = "Captura presas pequenas automaticamente.", type = ItemType.Deployable, category = ItemCategory.Deployable, maxStack = 5 });
    }

    private static void Register(ItemData item)
    {
        items[item.id] = item;
    }

    public static ItemData GetItem(int id)
    {
        Initialize();
        if (items.TryGetValue(id, out ItemData data)) return data;
        return null;
    }

    public static List<ItemData> GetAllItems()
    {
        Initialize();
        return new List<ItemData>(items.Values);
    }
}
