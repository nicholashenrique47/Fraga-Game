using UnityEngine;

/// <summary>
/// Câmera Isométrica / Terceira Pessoa Tática AAA (The Forest Standard).
/// Suporta:
/// - Seguimento suave do jogador com offset elevado (ângulo de visão limpo e amplo)
/// - Detecção anti-clipping de terreno (a câmera nunca penetra montanhas nem fica abaixo do solo)
/// - Projeção em Perspectiva nítida com FOV calibrado (evita achatamento ortográfico)
/// - Auto-recuperação de alvo caso o jogador dê spawn ou mude de cena
/// </summary>
public class IsometricCamera : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;
    
    [Header("Camera Settings")]
    public Vector3 offset = new Vector3(-9f, 14f, -9f);
    public float smoothSpeed = 8f;
    public float fieldOfView = 60f;

    private Camera cam;
    private bool isInitialized = false;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.orthographic = false; // Garante modo perspectiva
            cam.fieldOfView = fieldOfView;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.15f;
            if (cam.farClipPlane < 300f) cam.farClipPlane = 350f;
        }
    }

    private void Start()
    {
        FindPlayerTarget();
        if (target != null)
        {
            SnapToTarget();
        }
    }

    public void FindPlayerTarget()
    {
        if (target == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null)
            {
                target = pc.transform;
            }
            else
            {
                var playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null) target = playerObj.transform;
            }
        }
    }

    public void SnapToTarget()
    {
        if (target == null) return;
        Vector3 desiredPosition = target.position + offset;
        float minCamY = TerrainMeshGenerator.GetTerrainHeight(desiredPosition.x, desiredPosition.z) + 3.0f;
        desiredPosition.y = Mathf.Max(desiredPosition.y, minCamY);
        transform.position = desiredPosition;
        transform.LookAt(target.position + Vector3.up * 1.2f);
        isInitialized = true;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            FindPlayerTarget();
            if (target == null) return;
        }

        // Posição desejada mantendo o offset diagonal
        Vector3 desiredPosition = target.position + offset;

        // Anti-Clipping de Relevo: Impede que a câmera fique presa dentro de uma colina
        float minCamY = TerrainMeshGenerator.GetTerrainHeight(desiredPosition.x, desiredPosition.z) + 2.5f;
        desiredPosition.y = Mathf.Max(desiredPosition.y, minCamY);

        if (!isInitialized || Vector3.Distance(transform.position, desiredPosition) > 35f)
        {
            SnapToTarget();
            return;
        }

        // Interpolação suave
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        
        // Ponto de foco no tronco/peito do jogador
        Vector3 lookPoint = target.position + Vector3.up * 1.2f;
        transform.LookAt(lookPoint);
    }
}
