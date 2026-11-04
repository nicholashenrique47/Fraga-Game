using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Controlador de Toque de Câmera Mobile.
/// Acumula os deltas de toque no lado direito da tela para rotação suave em primeira pessoa.
/// </summary>
public class CameraTouchController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    private Vector2 touchDelta;
    private bool isDragging = false;
    private int pointerId = -1;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isDragging)
        {
            isDragging = true;
            pointerId = eventData.pointerId;
            touchDelta = Vector2.zero;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isDragging && eventData.pointerId == pointerId)
        {
            touchDelta += eventData.delta;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == pointerId)
        {
            isDragging = false;
            pointerId = -1;
            touchDelta = Vector2.zero;
        }
    }

    public Vector2 GetTouchDelta()
    {
        Vector2 delta = touchDelta;
        touchDelta = Vector2.zero; // Consumir o delta acumulado
        return delta;
    }
}
