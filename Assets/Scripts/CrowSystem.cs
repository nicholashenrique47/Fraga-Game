using UnityEngine;

/// CrowSystem: Gere corvos que voam em circulos sobre areas perigosas e cadaveres.
/// Os corvos servem como indicador visual de perigo para o jogador.
public class CrowSystem : MonoBehaviour
{
    [Header("Config")]
    public int maxCrows = 12;
    public float circleRadius = 5f;
    public float circleSpeed = 1.5f;
    public float flyHeight = 8f;
    
    [Header("Dependencies")]
    public Transform playerTransform;
    
    // Crow state arrays
    private Vector3[] crowCenterPositions; // Onde o corvo circula
    private float[] crowAngles;
    private GameObject[] crowInstances;
    private Transform[] crowWingL;
    private Transform[] crowWingR;
    
    private void Awake()
    {
        crowCenterPositions = new Vector3[maxCrows];
        crowAngles = new float[maxCrows];
        crowInstances = new GameObject[maxCrows];
        crowWingL = new Transform[maxCrows];
        crowWingR = new Transform[maxCrows];
        
        SpawnCrows();
    }
    
    private void SpawnCrows()
    {
        GameObject crowParent = new GameObject("Crow_Instances");
        crowParent.transform.SetParent(transform);

        GameManager gm = FindAnyObjectByType<GameManager>();
        float maxW = (gm != null && gm.mapWidth > 0) ? gm.mapWidth - 40f : FragaWorldBlueprint.DefaultWidth - 40f;
        float maxH = (gm != null && gm.mapHeight > 0) ? gm.mapHeight - 40f : FragaWorldBlueprint.DefaultHeight - 40f;
        float centerX = (gm != null && gm.mapWidth > 0) ? gm.mapWidth * 0.5f : FragaWorldBlueprint.CrashCampPos.x;
        float centerZ = (gm != null && gm.mapHeight > 0) ? gm.mapHeight * 0.5f : FragaWorldBlueprint.CrashCampPos.y;
        GameObject crowPrefab = Resources.Load<GameObject>("Prefabs/Crow_Prefab");
        
        for (int i = 0; i < maxCrows; i++)
        {
            float cx = (i < 4) ? Mathf.Clamp(centerX + Random.Range(-35f, 35f), 10f, maxW) : Random.Range(10f, maxW);
            float cz = (i < 4) ? Mathf.Clamp(centerZ + Random.Range(-35f, 35f), 10f, maxH) : Random.Range(10f, maxH);
            crowCenterPositions[i] = new Vector3(cx, flyHeight, cz);
            crowAngles[i] = Random.Range(0f, Mathf.PI * 2f);
            
            crowInstances[i] = CreateCrowModel(crowParent.transform, crowPrefab, out crowWingL[i], out crowWingR[i]);
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
    
    private GameObject CreateCrowModel(Transform parent, GameObject prefab, out Transform wingLOut, out Transform wingROut)
    {
        if (prefab != null)
        {
            GameObject inst = Instantiate(prefab, parent);
            inst.name = "Crow";
            foreach (Collider c in inst.GetComponentsInChildren<Collider>(true)) Destroy(c);
            wingLOut = FindChildNamed(inst.transform, "Wing_L");
            wingROut = FindChildNamed(inst.transform, "Wing_R");
            return inst;
        }

        GameObject crow = new GameObject("Crow");
        crow.transform.SetParent(parent);
        
        // Body
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(crow.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(0.1f, 0.08f, 0.15f);
        body.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Renderer bodyR = body.GetComponent<Renderer>();
        bodyR.material.color = new Color(0.12f, 0.14f, 0.20f);
        
        // Left wing
        GameObject wingL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wingL.transform.SetParent(crow.transform);
        wingL.transform.localPosition = new Vector3(-0.15f, 0, 0);
        wingL.transform.localScale = new Vector3(0.2f, 0.01f, 0.08f);
        Renderer wlR = wingL.GetComponent<Renderer>();
        wlR.material.color = new Color(0.12f, 0.14f, 0.20f);
        
        // Right wing
        GameObject wingR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wingR.transform.SetParent(crow.transform);
        wingR.transform.localPosition = new Vector3(0.15f, 0, 0);
        wingR.transform.localScale = new Vector3(0.2f, 0.01f, 0.08f);
        Renderer wrR = wingR.GetComponent<Renderer>();
        wrR.material.color = new Color(0.12f, 0.14f, 0.20f);
        
        // Remover colliders para nao interferir
        foreach (Collider c in crow.GetComponentsInChildren<Collider>()) Destroy(c);

        wingLOut = wingL.transform;
        wingROut = wingR.transform;
        
        return crow;
    }
    
    private void Update()
    {
        float dt = Time.deltaTime;
        Vector3 pPos = playerTransform != null ? playerTransform.position : Vector3.zero;
        
        for (int i = 0; i < maxCrows; i++)
        {
            if (crowInstances[i] == null) continue;
            
            crowAngles[i] += circleSpeed * dt;
            
            float x = crowCenterPositions[i].x + Mathf.Cos(crowAngles[i]) * circleRadius;
            float z = crowCenterPositions[i].z + Mathf.Sin(crowAngles[i]) * circleRadius;

            if (playerTransform != null)
            {
                float dSq = (pPos.x - x) * (pPos.x - x) + (pPos.z - z) * (pPos.z - z);
                bool vis = dSq <= 115f * 115f;
                if (crowInstances[i].activeSelf != vis) crowInstances[i].SetActive(vis);
                if (!vis) continue;
            }
            
            // Animar asas batendo
            float wingFlap = Mathf.Sin(Time.time * 8f + i) * 24f;
            if (crowWingL[i] != null) crowWingL[i].localRotation = Quaternion.Euler(0, 0, wingFlap);
            if (crowWingR[i] != null) crowWingR[i].localRotation = Quaternion.Euler(0, 0, -wingFlap);
            
            float groundY = TerrainMeshGenerator.GetTerrainHeight(x, z);
            float y = groundY + flyHeight + Mathf.Sin(Time.time * 0.5f + i) * 0.5f;
            
            Vector3 newPos = new Vector3(x, y, z);
            Vector3 dir = newPos - crowInstances[i].transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0005f)
            {
                Quaternion lookRot = Quaternion.LookRotation(dir.normalized);
                Quaternion bankRot = Quaternion.Euler(0, 0, 22f);
                crowInstances[i].transform.rotation = Quaternion.Slerp(crowInstances[i].transform.rotation, lookRot * bankRot, Time.deltaTime * 5f);
            }
            crowInstances[i].transform.position = newPos;
        }
    }
    
    /// Move um corvo para circular sobre um ponto especifico (cadaver, area perigosa)
    public void SendCrowTo(int index, Vector3 worldPos)
    {
        if (index >= 0 && index < maxCrows)
        {
            crowCenterPositions[index] = new Vector3(worldPos.x, flyHeight, worldPos.z);
        }
    }
}
