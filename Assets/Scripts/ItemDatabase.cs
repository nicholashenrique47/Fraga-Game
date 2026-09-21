using UnityEngine;
using System.Collections.Generic;

/// ItemDatabase: Base de dados central de todos os itens do jogo.
/// Cada item tem ID unico, tipo, stats e stackability.
/// Acedido estaticamente por qualquer sistema.

public enum ItemType
{
    Resource,       // Madeira, Pedra, Oleo
    Consumable,     // Comida, Pocoes
    Tool,           // Machado, Picareta
    Deployable,     // Armadilha, Fogueira
    Quest,          // Itens de quest
    Cosmetic        // Lampioes cosmeticos
}

[System.Serializable]
public class ItemData
{
    public int id;
    public string itemName;
    public string description;
    public ItemType type;
    public int maxStack;
    public float hungerRestore;     // Se consumivel
    public float thirstRestore;     // Se consumivel
    public float sanityRestore;     // Se consumivel
    public float healthRestore;     // Se consumivel
    public float poisonDuration;    // Se venenoso (negativo = cura)
    public int craftOutputAmount;   // Quantidade produzida no craft
    public bool isPerishable;       // Apodrece ao longo do tempo
    public float maxDurability;     // Segundos ate apodrecer
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
        Register(new ItemData { id = 100, itemName = "Madeira", description = "Troncos cortados. Essencial para construcao.", type = ItemType.Resource, maxStack = 99 });
        Register(new ItemData { id = 101, itemName = "Pedra", description = "Pedra bruta das ruinas celtas.", type = ItemType.Resource, maxStack = 99 });
        Register(new ItemData { id = 102, itemName = "Oleo de Lampiao", description = "Combustivel para o lampiao.", type = ItemType.Resource, maxStack = 20 });
        Register(new ItemData { id = 103, itemName = "Ervas", description = "Plantas medicinais da floresta.", type = ItemType.Consumable, maxStack = 30, hungerRestore = 2f, sanityRestore = 10f, poisonDuration = -1f });
        Register(new ItemData { id = 105, itemName = "Pele de Animal", description = "Pele para protecao contra o frio.", type = ItemType.Resource, maxStack = 10 });
        Register(new ItemData { id = 106, itemName = "Sal", description = "Mineral sagrado. Repele os espiritos.", type = ItemType.Resource, maxStack = 15 });
        Register(new ItemData { id = 107, itemName = "Ferro Velho", description = "Fragmentos de metal das ruinas.", type = ItemType.Resource, maxStack = 20 });
        Register(new ItemData { id = 110, itemName = "Flechas de Pedra", description = "Municao para o arco.", type = ItemType.Resource, maxStack = 30 });
        Register(new ItemData { id = 111, itemName = "Peixe Cru", description = "Acabado de pescar. Nao coma cru.", type = ItemType.Consumable, maxStack = 10, hungerRestore = 10f, poisonDuration = 15f, isPerishable = true, maxDurability = 200f });
        Register(new ItemData { id = 112, itemName = "Sementes Silvestres", description = "Planta-as em terra fertil para crescer bagas.", type = ItemType.Resource, maxStack = 20 });
        Register(new ItemData { id = 113, itemName = "Carne Leve", description = "Carne de coelho crua.", type = ItemType.Consumable, maxStack = 10, hungerRestore = 5f, poisonDuration = 5f, isPerishable = true, maxDurability = 250f });
        Register(new ItemData { id = 114, itemName = "Pena de Aguia", description = "Pena rara. Usada em flechas premium e decoracao.", type = ItemType.Resource, maxStack = 20 });
        Register(new ItemData { id = 115, itemName = "Coracao Sombrio", description = "O nucleo de poder do Vulto Anciao. Ofereca no Altar de Purificacao para vencer.", type = ItemType.Quest, maxStack = 1 });
        Register(new ItemData { id = 116, itemName = "Pagina do Diario", description = "Memorias de um cacador. Ler restaura 20 de sanidade.", type = ItemType.Consumable, maxStack = 10, sanityRestore = 20f });

        // === CONSUMIVEIS ===
        Register(new ItemData { id = 104, itemName = "Carne Crua", description = "Carne de cervideo. Perigosa se nao cozinhada.", type = ItemType.Consumable, maxStack = 10, hungerRestore = 15f, poisonDuration = 10f, isPerishable = true, maxDurability = 300f }); // 5 min para apodrecer
        Register(new ItemData { id = 108, itemName = "Comida Podre", description = "Cheira a morte. Toxico.", type = ItemType.Consumable, maxStack = 10, hungerRestore = 2f, sanityRestore = -20f, poisonDuration = 60f });
        Register(new ItemData { id = 109, itemName = "Agua Limpa", description = "Agua fresca de uma nascente.", type = ItemType.Consumable, maxStack = 5, thirstRestore = 40f });

        // === CONSUMIVEIS AVANCADOS ===
        Register(new ItemData { id = 200, itemName = "Carne Assada", description = "Carne cozinhada. Restaura fome e vida.", type = ItemType.Consumable, maxStack = 10, hungerRestore = 40f, healthRestore = 10f, sanityRestore = 5f, isPerishable = true, maxDurability = 600f }); // 10 min para apodrecer
        Register(new ItemData { id = 201, itemName = "Sopa de Ervas", description = "Cura venenos e restaura sanidade.", type = ItemType.Consumable, maxStack = 5, hungerRestore = 15f, sanityRestore = 25f, thirstRestore = 20f, poisonDuration = -1f, isPerishable = true, maxDurability = 900f });
        Register(new ItemData { id = 202, itemName = "Bagas Silvestres", description = "Frutos acidos. Pouco nutritivos.", type = ItemType.Consumable, maxStack = 20, hungerRestore = 10f, thirstRestore = 5f, sanityRestore = 0f });
        Register(new ItemData { id = 203, itemName = "Cogumelo Estranho", description = "Aspeto suspeito. Comer por conta e risco.", type = ItemType.Consumable, maxStack = 5, hungerRestore = 5f, sanityRestore = -15f, poisonDuration = 30f });
        Register(new ItemData { id = 204, itemName = "Agua Benta", description = "Abencoa e restaura sanidade profunda.", type = ItemType.Consumable, maxStack = 3, thirstRestore = 20f, sanityRestore = 50f });
        Register(new ItemData { id = 205, itemName = "Fosforos", description = "Acende fogueiras instantaneamente.", type = ItemType.Consumable, maxStack = 10 });
        Register(new ItemData { id = 206, itemName = "Ligaduras", description = "Pensos medicos. Curam sangramento.", type = ItemType.Consumable, maxStack = 5, healthRestore = 20f, curesBleeding = true });
        Register(new ItemData { id = 209, itemName = "Tala de Madeira", description = "Imobiliza ossos partidos. Cura fraturas.", type = ItemType.Consumable, maxStack = 2, curesFracture = true });
        Register(new ItemData { id = 207, itemName = "Peixe Assado", description = "Excelente fonte de omegas. Cura muita fome e sede.", type = ItemType.Consumable, maxStack = 10, hungerRestore = 35f, thirstRestore = 25f, sanityRestore = 10f, isPerishable = true, maxDurability = 500f });
        Register(new ItemData { id = 208, itemName = "Ensopado Druida", description = "Milagre em caldo. Cura Fome, Sede, Vida e Sanidade ao maximo.", type = ItemType.Consumable, maxStack = 3, hungerRestore = 100f, thirstRestore = 100f, sanityRestore = 100f, healthRestore = 100f, isPerishable = true, maxDurability = 1200f });

        // === FERRAMENTAS ===
        Register(new ItemData { id = 300, itemName = "Machado Rudimentar", description = "Dobra a coleta de madeira.", type = ItemType.Tool, maxStack = 1 });
        Register(new ItemData { id = 301, itemName = "Picareta de Pedra", description = "Quebra rochas e minerais.", type = ItemType.Tool, maxStack = 1 });
        Register(new ItemData { id = 302, itemName = "Faca de Caca", description = "Permite esfolar animais.", type = ItemType.Tool, maxStack = 1 });
        Register(new ItemData { id = 303, itemName = "Tocha", description = "Luz portatil temporaria.", type = ItemType.Tool, maxStack = 1 });
        Register(new ItemData { id = 304, itemName = "Lampiao Reforcado", description = "Luz intensa. Protege da chuva.", type = ItemType.Tool, maxStack = 1 });
        Register(new ItemData { id = 305, itemName = "Arco de Sobrevivencia", description = "Arma de caca a longa distancia.", type = ItemType.Tool, maxStack = 1 });
        Register(new ItemData { id = 306, itemName = "Vara de Pesca", description = "Para apanhar peixe nos lagos.", type = ItemType.Tool, maxStack = 1 });
        Register(new ItemData { id = 307, itemName = "Enxada Rustica", description = "Permite lavrar a terra para agricultura.", type = ItemType.Tool, maxStack = 1 });

        // === DEPLOYABLES ===
        Register(new ItemData { id = 400, itemName = "Kit de Fogueira", description = "Materiais prontos para montar uma fogueira.", type = ItemType.Deployable, maxStack = 3 });
        Register(new ItemData { id = 401, itemName = "Armadilha de Caca pesada", description = "Causa dano massivo.", type = ItemType.Deployable, maxStack = 5 });
        Register(new ItemData { id = 402, itemName = "Circulo de Sal", description = "Repele entidades por 60 segundos.", type = ItemType.Deployable, maxStack = 3 });
        Register(new ItemData { id = 403, itemName = "Coletor de Agua", description = "Passivamente gera agua limpa ao longo do tempo.", type = ItemType.Deployable, maxStack = 1 });
        Register(new ItemData { id = 404, itemName = "Mesa de Trabalho", description = "Bancada para crafting avancado.", type = ItemType.Deployable, maxStack = 1 });
        Register(new ItemData { id = 405, itemName = "Caldeirao", description = "Culinaria masterclass.", type = ItemType.Deployable, maxStack = 1 });
        Register(new ItemData { id = 406, itemName = "Laco para Coelhos", description = "Apanha presas rapidas sem dano.", type = ItemType.Deployable, maxStack = 5 });
    }

    private static void Register(ItemData item)
    {
        items[item.id] = item;
    }

    public static ItemData GetItem(int id)
    {
        Initialize();
        if (items.ContainsKey(id)) return items[id];
        Debug.LogWarning($"[ItemDatabase] Item ID {id} nao encontrado.");
        return null;
    }

    public static List<ItemData> GetAllItems()
    {
        Initialize();
        return new List<ItemData>(items.Values);
    }
}
