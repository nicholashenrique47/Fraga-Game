using UnityEngine;
using UnityEngine.EventSystems;

/// Componente para dar "Juice" (Vida/Micro-animacoes) aos botoes da UI.
/// Produz um efeito de mola quando o jogador toca no ecra ou passa com o rato.
public class UIJuiceModifier : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    private RectTransform rectTransform;
    private Vector3 originalScale;
    
    [Header("Juice Settings")]
    public float pressedScale = 0.9f;
    public float hoverScale = 1.05f;
    public float lerpSpeed = 20f;
    
    private Vector3 targetScale;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            originalScale = rectTransform.localScale;
            targetScale = originalScale;
        }
    }

    private void Update()
    {
        if (rectTransform != null)
        {
            // Interpolacao suave (Lerp) para gamefeel AAA
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.unscaledDeltaTime * lerpSpeed);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        targetScale = originalScale * pressedScale; // Encolhe ao toque
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        targetScale = originalScale; // Volta ao normal ao largar
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Efeito ligeiro de hover (mais focado para Desktop, mas utl em gamepads Mobile se suportado)
        targetScale = originalScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
    }
}
