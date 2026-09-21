using UnityEngine;

public class IsometricCamera : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;
    
    [Header("Camera Settings")]
    public Vector3 offset = new Vector3(-10f, 15f, -10f);
    public float smoothSpeed = 5f;

    private void LateUpdate()
    {
        if (target == null) return;

        // Posição desejada mantendo o offset (45 graus dependendo dos valores X/Y/Z)
        Vector3 desiredPosition = target.position + offset;
        
        // Interpolação suave (Lerp)
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        
        // Manter o foco da câmera no jogador
        transform.LookAt(target.position);
    }
}
