using UnityEngine;

/// AtmosphereManager: Cria elementos visuais procedurais como Corvos
/// para aumentar a imersao e a tensao do survival horror.
public class AtmosphereManager : MonoBehaviour
{
    [Header("Crows Settings")]
    public int maxCrows = 25;
    public float fleeDistance = 10f;
    
    [Header("Dependencies")]
    public Transform playerTransform;

    private GameObject[] crowInstances;
    private Vector3[] crowTargets;
    private bool[] isFlying;
    private float[] flapTimers;

    private void Start()
    {
        crowInstances = new GameObject[maxCrows];
        crowTargets = new Vector3[maxCrows];
        isFlying = new bool[maxCrows];
        flapTimers = new float[maxCrows];

        GameObject parentObj = new GameObject("VFX_Crows");
        parentObj.transform.SetParent(transform);

        for (int i = 0; i < maxCrows; i++)
        {
            // Substituição de Cone por Cube esticado (Low Poly Bird)
            crowInstances[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crowInstances[i].transform.SetParent(parentObj.transform);
            crowInstances[i].transform.localScale = new Vector3(0.2f, 0.2f, 0.5f);
            
            // Remover colisor para não interferir na navegação
            Destroy(crowInstances[i].GetComponent<Collider>());
            
            Renderer r = crowInstances[i].GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(0.05f, 0.05f, 0.05f);
            
            if (playerTransform != null) RepositionCrow(i, playerTransform.position);
            else RepositionCrow(i, Vector3.zero);
        }
    }

    private void Update()
    {
        if (playerTransform == null) return;

        float dt = Time.deltaTime;
        Vector3 pPos = playerTransform.position;

        for (int i = 0; i < maxCrows; i++)
        {
            if (isFlying[i])
            {
                flapTimers[i] += dt * 15f;
                // Animação low poly procedural: Bater asas (Scale Y oscila)
                crowInstances[i].transform.localScale = new Vector3(0.2f, 0.2f + Mathf.Sin(flapTimers[i]) * 0.1f, 0.5f);

                // Voar em direção ao target no céu
                Vector3 dir = (crowTargets[i] - crowInstances[i].transform.position).normalized;
                crowInstances[i].transform.position += dir * 9f * dt;
                
                if (dir.sqrMagnitude > 0.001f)
                {
                    crowInstances[i].transform.rotation = Quaternion.Lerp(
                        crowInstances[i].transform.rotation, 
                        Quaternion.LookRotation(dir), 
                        dt * 5f
                    );
                }
                
                // Se chegou ao target alto, faz respawn no chão
                if (crowInstances[i].transform.position.y > 20f)
                {
                    RepositionCrow(i, pPos);
                }
            }
            else
            {
                // Verifica distancia do jogador para assustar os corvos
                float dx = pPos.x - crowInstances[i].transform.position.x;
                float dz = pPos.z - crowInstances[i].transform.position.z;
                
                if (dx * dx + dz * dz < fleeDistance * fleeDistance)
                {
                    isFlying[i] = true;
                    // Alvo aleatorio no ceu
                    Vector3 randomSky = crowInstances[i].transform.position + new Vector3(Random.Range(-15f, 15f), 25f, Random.Range(-15f, 15f));
                    crowTargets[i] = randomSky;
                    
                    // Som opcional
                    if (Random.value < 0.2f) Debug.Log("[Atmosphere] *Grasnido de Corvo*");
                }
            }
        }
    }

    private void RepositionCrow(int index, Vector3 centerPos)
    {
        isFlying[index] = false;
        // Posicionar num raio de 15 a 45 metros do jogador (fora de vista)
        Vector2 randCircle = Random.insideUnitCircle.normalized * Random.Range(15f, 45f);
        Vector3 newPos = centerPos + new Vector3(randCircle.x, 0f, randCircle.y);
        
        // Colocar ligeiramente acima do chao ou em cima de algo
        newPos.y = Random.value > 0.5f ? 0.3f : 2.5f; 
        
        crowInstances[index].transform.position = newPos;
        crowInstances[index].transform.localScale = new Vector3(0.2f, 0.2f, 0.5f); // reset scale
        crowInstances[index].transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
    }
}
