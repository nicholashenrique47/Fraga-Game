using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// LoreSystem: Gere o progresso da historia (Paginas do Diario encontradas nos POIs).
/// </summary>
public class LoreSystem : MonoBehaviour
{
    public static LoreSystem Instance { get; private set; }

    private int currentLoreIndex = 0;
    private readonly HashSet<int> readIndices = new HashSet<int>();

    private readonly string[] lorePages = new string[]
    {
        "Dia 12. Os vultos continuam a observar-me. Sinto o frio deles a noite.",
        "Dia 23. Encontrei as velhas ruinas celtas. O ar aqui e pesado, mas sinto-me protegido.",
        "Dia 34. A Santa Companha cruzou a floresta ontem a noite. Escondi-me, nao respirei.",
        "Dia 40. O Vulto Anciao... Ele e a fonte de tudo isto. Preciso de encontrar o altar.",
        "Dia 41. Se alguem ler isto... Ofereca o Coracao Sombrio ao Altar de Purificacao. Salve-nos.",
        "Diario do Faroleiro: A luz do farol afasta a nevoa, mas as sombras sobem do oceano a cada lua cheia."
    };

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public int DiscoveredCount => readIndices.Count;
    public int TotalPages => lorePages.Length;

    public void ShowNextPage()
    {
        int idx = Mathf.Clamp(currentLoreIndex, 0, lorePages.Length - 1);
        if (currentLoreIndex < lorePages.Length) currentLoreIndex++;
        ReadPoiNote(idx, $"Diario Antigo ({idx + 1}/{lorePages.Length})");
    }

    public void ReadPoiNote(int pageIndex, string title)
    {
        if (lorePages == null || lorePages.Length == 0) return;
        int idx = Mathf.Clamp(pageIndex, 0, lorePages.Length - 1);
        bool firstTime = readIndices.Add(idx);

        if (firstTime)
        {
            SanitySystem sanity = FindAnyObjectByType<SanitySystem>();
            if (sanity != null)
            {
                sanity.sanity = Mathf.Min(100f, sanity.sanity + 12f);
            }
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayPickupSound();
            }
        }

        string header = string.IsNullOrEmpty(title) ? $"Diario ({idx + 1}/{lorePages.Length})" : title;
        string bonus = firstTime ? " [+12 Sanidade]" : "";
        string msg = $"[{header}]{bonus}\n{lorePages[idx]}";

        if (HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.ShowLootTooltip(msg);
        }
        else
        {
            UIManager ui = FindAnyObjectByType<UIManager>();
            if (ui != null) ui.SetLootTooltip(msg);
        }

        Debug.Log($"[Lore] ({readIndices.Count}/{lorePages.Length}) {lorePages[idx]}");
    }
}

public class LoreNoteInteractable : MonoBehaviour
{
    public int loreIndex = 0;
    public string noteTitle = "Nota Antiga";

    public void Interact()
    {
        LoreSystem lore = LoreSystem.Instance ?? FindAnyObjectByType<LoreSystem>();
        if (lore == null)
        {
            GameObject go = new GameObject("LoreSystem");
            lore = go.AddComponent<LoreSystem>();
        }
        lore.ReadPoiNote(loreIndex, noteTitle);
    }
}
