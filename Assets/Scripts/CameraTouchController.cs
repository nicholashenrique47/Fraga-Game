using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Controlador de Toque de Camera Mobile.
/// Acumula os deltas de toque no lado direito da tela para rotacao suave em primeira pessoa
/// e limpa estado automaticamente ao desativar (evita trava de camera ao abrir menus).
/// </summary>
public class CameraTouchController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public static CameraTouchController Instance { get; private set; }

    private Vector2 touchDelta;
    private bool isDragging = false;
    private int pointerId = -1;

    public bool IsDragging => isDragging;
    public int ActivePointerId => pointerId;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnDisable()
    {
        ResetTouchState();
    }

    public void ResetTouchState()
    {
        isDragging = false;
        pointerId = -1;
        touchDelta = Vector2.zero;
    }

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
            if (eventData.delta.sqrMagnitude > 240f * 240f) return;
            touchDelta += eventData.delta;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == pointerId)
        {
            ResetTouchState();
        }
    }

    public Vector2 GetTouchDelta()
    {
        Vector2 delta = touchDelta;
        touchDelta = Vector2.zero; // Consumir o delta acumulado
        return delta;
    }
}
