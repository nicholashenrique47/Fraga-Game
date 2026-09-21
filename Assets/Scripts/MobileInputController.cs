using UnityEngine;
using UnityEngine.EventSystems;

public class MobileInputController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("Joystick Visuals")]
    public RectTransform joystickBackground;
    public RectTransform joystickHandle;

    private Vector2 inputVector;
    private bool isDragging = false;

    private void Start()
    {
        if (joystickHandle != null)
        {
            joystickHandle.anchoredPosition = Vector2.zero;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isDragging = true;
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        Vector2 position = RectTransformUtility.PixelAdjustPoint(eventData.position, joystickBackground, null);
        
        // Calcular o raio do fundo
        float radius = joystickBackground.sizeDelta.x / 2f;

        // Limitar o handle dentro do raio
        inputVector = Vector2.ClampMagnitude(position / radius, 1f);

        if (joystickHandle != null)
        {
            joystickHandle.anchoredPosition = inputVector * radius;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        inputVector = Vector2.zero;
        
        if (joystickHandle != null)
        {
            joystickHandle.anchoredPosition = Vector2.zero;
        }
    }

    // Função para o PlayerController aceder aos inputs (substitui Input.GetAxisRaw)
    public Vector2 GetInputVector()
    {
        return inputVector;
    }
}
