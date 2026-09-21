using UnityEngine;

/// LoreSystem: Gere o progresso da historia (Páginas do Diário).
public class LoreSystem : MonoBehaviour
{
    private int currentLoreIndex = 0;
    
    private string[] lorePages = new string[] {
        "Dia 12. Os vultos continuam a observar-me. Sinto o frio deles à noite.",
        "Dia 23. Encontrei as velhas ruínas celtas. O ar aqui é pesado, mas sinto-me protegido.",
        "Dia 34. A Santa Compaña cruzou a floresta ontem à noite. Escondi-me, não respirei.",
        "Dia 40. O Vulto Ancião... Ele é a fonte de tudo isto. Preciso de encontrar o altar.",
        "Dia 41. Se alguém ler isto... Ofereça o Coração Sombrio ao Altar de Purificação. Salve-nos."
    };
    
    public void ShowNextPage()
    {
        string text = "";
        if (currentLoreIndex < lorePages.Length)
        {
            text = lorePages[currentLoreIndex];
            currentLoreIndex++;
        }
        else
        {
            text = lorePages[lorePages.Length - 1];
        }
        
        UIManager ui = FindAnyObjectByType<UIManager>();
        if (ui != null)
        {
            ui.SetLootTooltip($"[Diário Antigo]\n{text}");
        }
        
        Debug.Log($"[Lore] {text}");
    }
}
