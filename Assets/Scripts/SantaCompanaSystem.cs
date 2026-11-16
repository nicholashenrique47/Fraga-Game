using UnityEngine;

public class SantaCompanaSystem : MonoBehaviour
{
    [Header("Procissao (The Horde)")]
    public Transform processionTransform;
    public float speed = 1.5f;
    public float lethalRadius = 3f;

    [Header("Dependencias")]
    public Transform playerTransform;
    public SanitySystem sanitySystem;
    public ResourceManager resourceManager;

    [Header("Visuais e Audio")]
    public GameObject meigaPrefab; // O prefab do espirito
    public int numberOfMeigas = 5;
    public float spacing = 1.5f; // Espaco entre os espiritos na procissao
    public AudioClip deathBellSound;
    private AudioSource audioSource;
    private GameObject[] meigaInstances;

    private bool isEventActive = false;

    // P/Invoke
    [System.Runtime.InteropServices.DllImport("FragaNative", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private static extern float CalculateMeigaDebuff(System.IntPtr mapData, int width, int height, float[] meigasX, float[] meigasY, int meigaCount, float playerX, float playerY);

    private float[] meigasX = new float[1];
    private float[] meigasY = new float[1];

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f; // 3D sound
        audioSource.maxDistance = 30f;

        if (meigaPrefab == null)
        {
            meigaPrefab = Resources.Load<GameObject>("Prefabs/Vulto_Prefab");
        }
    }

    // Invocado pelo DayNightCycle nas horas de maior terror (ex: 03:00 AM)
    public void TriggerEvent(Vector3 startPoint, Vector3 endPoint)
    {
        if (processionTransform == null) return;

        startPoint.y = TerrainMeshGenerator.GetTerrainHeight(startPoint.x, startPoint.z);
        endPoint.y = TerrainMeshGenerator.GetTerrainHeight(endPoint.x, endPoint.z);

        processionTransform.position = startPoint;
        Vector3 movementDirection = endPoint - startPoint;
        movementDirection.y = 0f;
        if (movementDirection.sqrMagnitude < 0.001f) movementDirection = Vector3.forward;
        movementDirection.Normalize();
        
        // Alinha os espiritos com o destino
        processionTransform.rotation = Quaternion.LookRotation(movementDirection);
        
        processionTransform.gameObject.SetActive(true);
        isEventActive = true;
        
        SpawnMeigas();

        if (deathBellSound != null)
        {
            audioSource.clip = deathBellSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        Debug.LogWarning("[EVENTO CRITICO] A Santa Compana entrou na Fraga. A luz nao o salvara. Evite a rota.");
    }

    private void SpawnMeigas()
    {
        if (meigaPrefab == null) return;

        if (meigaInstances != null)
        {
            foreach (var inst in meigaInstances) if (inst != null) Destroy(inst);
        }

        meigaInstances = new GameObject[numberOfMeigas];
        for (int i = 0; i < numberOfMeigas; i++)
        {
            Vector3 offset = -processionTransform.forward * (i * spacing);
            Vector3 spawnPos = processionTransform.position + offset;
            spawnPos.y = TerrainMeshGenerator.GetTerrainHeight(spawnPos.x, spawnPos.z);
            meigaInstances[i] = Instantiate(meigaPrefab, spawnPos, processionTransform.rotation, processionTransform);
            foreach (Collider c in meigaInstances[i].GetComponentsInChildren<Collider>(true)) Destroy(c);
        }
    }

    private void Update()
    {
        if (!isEventActive || processionTransform == null) return;

        // Avanco continuo e irrefreavel ignorando obstaculos fisicos (fantasmas)
        processionTransform.Translate(Vector3.forward * (speed * Time.deltaTime), Space.Self);
        Vector3 p = processionTransform.position;
        p.y = TerrainMeshGenerator.GetTerrainHeight(p.x, p.z);
        processionTransform.position = p;

        if (meigaInstances != null)
        {
            for (int i = 0; i < meigaInstances.Length; i++)
            {
                if (meigaInstances[i] == null) continue;
                Vector3 mp = meigaInstances[i].transform.position;
                mp.y = TerrainMeshGenerator.GetTerrainHeight(mp.x, mp.z);
                meigaInstances[i].transform.position = mp;
            }
        }

        CheckPlayerProximityNative();
    }

    private void CheckPlayerProximityNative()
    {
        if (playerTransform == null || sanitySystem == null || resourceManager == null) return;

        float distance = Vector3.Distance(playerTransform.position, processionTransform.position);
        
        if (distance <= lethalRadius)
        {
            // Fim de jogo fulminante
            sanitySystem.sanity = 0f;
            Debug.LogError("[GAME OVER] O Topografo juntou-se a procissao dos mortos. Morte Instantanea.");
            isEventActive = false;
            if (audioSource != null) audioSource.Stop();
        }
        else if (distance <= 20f)
        {
            // Uso da IA nativa C++ para verificar debuff de linha de visao
            System.IntPtr mapData = resourceManager.MapDataPtr;
            if (mapData != System.IntPtr.Zero)
            {
                meigasX[0] = processionTransform.position.x;
                meigasY[0] = processionTransform.position.z;

                float debuff = CalculateMeigaDebuff(
                    mapData, resourceManager.MapWidth, resourceManager.MapHeight, 
                    meigasX, meigasY, 1, 
                    playerTransform.position.x, playerTransform.position.z);

                // Aplica o dreno extremo se houver LOS e estiver perto
                if (debuff > 0)
                {
                    sanitySystem.sanity -= debuff * Time.deltaTime;
                    
                    // Vibra o comando mobile de acordo com a proximidade
                    HapticManager haptic = Object.FindAnyObjectByType<HapticManager>();
                    if (haptic != null && Time.frameCount % 30 == 0)
                    {
                        if (debuff > 10f) haptic.TriggerHeavyVibration();
                        else haptic.TriggerLightVibration();
                    }
                }
            }
        }
    }
}
