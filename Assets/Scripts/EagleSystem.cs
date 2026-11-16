using UnityEngine;

/// EagleSystem: Aguias raras que sobrevoam e cacam coelhos automaticamente.
/// Dropar Penas (item novo) quando passam perto do jogador.
public class EagleSystem : MonoBehaviour
{
    [Header("Config")]
    public int maxEagles = 4;
    public float swoopSpeed = 8f;
    public float cruiseHeight = 12f;
    public float huntCooldown = 30f;
    
    [Header("Dependencies")]
    public FaunaManager faunaManager;
    public Transform playerTransform;
    public InventoryAdvanced inventory;
    
    private Vector3[] eaglePositions;
    private float[] eagleHuntTimers;
    private GameObject[] eagleInstances;
    private Transform[] eagleWingL;
    private Transform[] eagleWingR;
    private bool[] isSwooping;
    private Vector3[] swoopTargets;
    private float maxMapW = FragaWorldBlueprint.DefaultWidth;
    private float maxMapH = FragaWorldBlueprint.DefaultHeight;
    
    private void Awake()
    {
        eaglePositions = new Vector3[maxEagles];
        eagleHuntTimers = new float[maxEagles];
        eagleInstances = new GameObject[maxEagles];
        eagleWingL = new Transform[maxEagles];
        eagleWingR = new Transform[maxEagles];
        isSwooping = new bool[maxEagles];
        swoopTargets = new Vector3[maxEagles];
        
        SpawnEagles();
    }
    
    private void SpawnEagles()
    {
        GameObject eagleParent = new GameObject("Eagle_Instances");
        eagleParent.transform.SetParent(transform);

        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
        {
            maxMapW = Mathf.Max(40f, gm.mapWidth);
            maxMapH = Mathf.Max(40f, gm.mapHeight);
        }
        GameObject eaglePrefab = Resources.Load<GameObject>("Prefabs/Eagle_Prefab");
        
        for (int i = 0; i < maxEagles; i++)
        {
            float ex = (i == 0) ? (maxMapW * 0.5f + Random.Range(-25f, 25f)) : Random.Range(20f, maxMapW - 20f);
            float ez = (i == 0) ? (maxMapH * 0.5f + Random.Range(-25f, 25f)) : Random.Range(20f, maxMapH - 20f);
            float ey = TerrainMeshGenerator.GetTerrainHeight(ex, ez) + cruiseHeight;
            eaglePositions[i] = new Vector3(ex, ey, ez);
            eagleHuntTimers[i] = Random.Range(10f, huntCooldown);
            
            eagleInstances[i] = CreateEagleModel(eagleParent.transform, eaglePrefab, out eagleWingL[i], out eagleWingR[i]);
        }
    }

    private static Transform FindChildNamed(Transform root, string exactName)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == exactName) return t;
        }
        return null;
    }
    
    private GameObject CreateEagleModel(Transform parent, GameObject prefab, out Transform wingLOut, out Transform wingROut)
    {
        if (prefab != null)
        {
            GameObject inst = Instantiate(prefab, parent);
            inst.name = "Eagle";
            foreach (Collider c in inst.GetComponentsInChildren<Collider>(true)) Destroy(c);
            wingLOut = FindChildNamed(inst.transform, "Wing_L");
            wingROut = FindChildNamed(inst.transform, "Wing_R");
            return inst;
        }

        GameObject eagle = new GameObject("Eagle");
        eagle.transform.SetParent(parent);
        
        // Body
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(eagle.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(0.15f, 0.1f, 0.25f);
        body.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Renderer bodyR = body.GetComponent<Renderer>();
        bodyR.material.color = new Color(0.45f, 0.28f, 0.14f);
        
        // Left wing (grande)
        GameObject wingL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wingL.transform.SetParent(eagle.transform);
        wingL.transform.localPosition = new Vector3(-0.3f, 0, 0);
        wingL.transform.localScale = new Vector3(0.4f, 0.015f, 0.12f);
        Renderer wlR = wingL.GetComponent<Renderer>();
        wlR.material.color = new Color(0.38f, 0.24f, 0.12f);
        
        // Right wing
        GameObject wingR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wingR.transform.SetParent(eagle.transform);
        wingR.transform.localPosition = new Vector3(0.3f, 0, 0);
        wingR.transform.localScale = new Vector3(0.4f, 0.015f, 0.12f);
        Renderer wrR = wingR.GetComponent<Renderer>();
        wrR.material.color = new Color(0.38f, 0.24f, 0.12f);
        
        foreach (Collider c in eagle.GetComponentsInChildren<Collider>()) Destroy(c);

        wingLOut = wingL.transform;
        wingROut = wingR.transform;
        
        return eagle;
    }
    
    private void Update()
    {
        float dt = Time.deltaTime;
        
        for (int i = 0; i < maxEagles; i++)
        {
            if (eagleInstances[i] == null) continue;
            
            eagleHuntTimers[i] -= dt;
            
            if (isSwooping[i])
            {
                // Mergulhar em direcao ao alvo
                Vector3 dir = (swoopTargets[i] - eaglePositions[i]).normalized;
                eaglePositions[i] += dir * swoopSpeed * dt;
                
                float dist = Vector3.Distance(eaglePositions[i], swoopTargets[i]);
                if (dist < 1.5f)
                {
                    // Tentativa de apanhar coelho
                    if (faunaManager != null)
                    {
                        float[] rx = faunaManager.GetRabbitPosX();
                        float[] ry = faunaManager.GetRabbitPosY();
                        for (int r = 0; r < faunaManager.maxRabbits; r++)
                        {
                            float dx = rx[r] - swoopTargets[i].x;
                            float dz = ry[r] - swoopTargets[i].z;
                            if (dx * dx + dz * dz < 4f)
                            {
                                faunaManager.CatchRabbit(r);
                                Debug.Log("[Eagle] Aguia cacou um coelho!");
                                
                                // Dropar pena perto do jogador
                                if (LootManager.Instance != null && playerTransform != null)
                                {
                                    float pdist = Vector3.Distance(playerTransform.position, swoopTargets[i]);
                                    if (pdist < 20f)
                                    {
                                        LootManager.Instance.DropItem(114, 1, swoopTargets[i]); // Pena de Aguia
                                    }
                                }
                                break;
                            }
                        }
                    }
                    
                    isSwooping[i] = false;
                    eagleHuntTimers[i] = huntCooldown;
                }
            }
            else
            {
                // Sobrevoar em circulos largos
                float angle = Time.time * 0.3f + i * Mathf.PI;
                eaglePositions[i].x += Mathf.Cos(angle) * 2f * dt;
                eaglePositions[i].z += Mathf.Sin(angle) * 2f * dt;
                
                // Manter dentro dos limites
                eaglePositions[i].x = Mathf.Clamp(eaglePositions[i].x, 5f, maxMapW - 5f);
                eaglePositions[i].z = Mathf.Clamp(eaglePositions[i].z, 5f, maxMapH - 5f);
                float groundY = TerrainMeshGenerator.GetTerrainHeight(eaglePositions[i].x, eaglePositions[i].z);
                eaglePositions[i].y = groundY + cruiseHeight + Mathf.Sin(Time.time * 0.2f + i) * 1.5f;
                
                // Tentar iniciar caca
                if (eagleHuntTimers[i] <= 0 && faunaManager != null)
                {
                    float[] rx = faunaManager.GetRabbitPosX();
                    float[] ry = faunaManager.GetRabbitPosY();
                    float closest = float.MaxValue;
                    int target = -1;
                    
                    for (int r = 0; r < faunaManager.maxRabbits; r++)
                    {
                        float dx = rx[r] - eaglePositions[i].x;
                        float dz = ry[r] - eaglePositions[i].z;
                        float d = dx * dx + dz * dz;
                        if (d < closest && rx[r] > 0)
                        {
                            closest = d;
                            target = r;
                        }
                    }
                    
                    if (target >= 0 && closest < 25f * 25f)
                    {
                        float ryGround = TerrainMeshGenerator.GetTerrainHeight(rx[target], ry[target]);
                        swoopTargets[i] = new Vector3(rx[target], ryGround + 0.4f, ry[target]);
                        isSwooping[i] = true;
                    }
                }
            }
            
            // Wing flap animation
            float wingFlap = Mathf.Sin(Time.time * 4f + i) * 18f;
            if (eagleWingL[i] != null) eagleWingL[i].localRotation = Quaternion.Euler(0, 0, wingFlap);
            if (eagleWingR[i] != null) eagleWingR[i].localRotation = Quaternion.Euler(0, 0, -wingFlap);
            
            // Update visual position
            Vector3 moveDir = eaglePositions[i] - eagleInstances[i].transform.position;
            if (moveDir.sqrMagnitude > 0.0005f)
            {
                Quaternion lookRot = Quaternion.LookRotation(moveDir.normalized);
                float banking = isSwooping[i] ? 15f : 28f;
                Quaternion bankRot = Quaternion.Euler(0, 0, banking);
                eagleInstances[i].transform.rotation = Quaternion.Slerp(eagleInstances[i].transform.rotation, lookRot * bankRot, Time.deltaTime * 4f);
            }
            eagleInstances[i].transform.position = eaglePositions[i];
        }
    }
}
