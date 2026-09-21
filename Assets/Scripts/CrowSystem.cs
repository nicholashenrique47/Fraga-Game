using UnityEngine;

/// CrowSystem: Gere corvos que voam em circulos sobre areas perigosas e cadaveres.
/// Os corvos servem como indicador visual de perigo para o jogador.
public class CrowSystem : MonoBehaviour
{
    [Header("Config")]
    public int maxCrows = 8;
    public float circleRadius = 5f;
    public float circleSpeed = 1.5f;
    public float flyHeight = 8f;
    
    [Header("Dependencies")]
    public Transform playerTransform;
    
    // Crow state arrays
    private Vector3[] crowCenterPositions; // Onde o corvo circula
    private float[] crowAngles;
    private GameObject[] crowInstances;
    
    private void Awake()
    {
        crowCenterPositions = new Vector3[maxCrows];
        crowAngles = new float[maxCrows];
        crowInstances = new GameObject[maxCrows];
        
        SpawnCrows();
    }
    
    private void SpawnCrows()
    {
        GameObject crowParent = new GameObject("Crow_Instances");
        crowParent.transform.SetParent(transform);
        
        for(int i=0; i<maxCrows; i++)
        {
            // Posicoes aleatorias pelo mapa
            crowCenterPositions[i] = new Vector3(
                Random.Range(10f, 90f),
                flyHeight,
                Random.Range(10f, 90f)
            );
            crowAngles[i] = Random.Range(0f, Mathf.PI * 2f);
            
            // Criar modelo basico de corvo (corpo escuro)
            crowInstances[i] = CreateCrowModel(crowParent.transform);
        }
    }
    
    private GameObject CreateCrowModel(Transform parent)
    {
        GameObject crow = new GameObject("Crow");
        crow.transform.SetParent(parent);
        
        // Body
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(crow.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(0.1f, 0.08f, 0.15f);
        body.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Renderer bodyR = body.GetComponent<Renderer>();
        bodyR.material.color = new Color(0.05f, 0.05f, 0.08f); // Preto quase puro
        
        // Left wing
        GameObject wingL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wingL.transform.SetParent(crow.transform);
        wingL.transform.localPosition = new Vector3(-0.15f, 0, 0);
        wingL.transform.localScale = new Vector3(0.2f, 0.01f, 0.08f);
        Renderer wlR = wingL.GetComponent<Renderer>();
        wlR.material.color = new Color(0.05f, 0.05f, 0.08f);
        
        // Right wing
        GameObject wingR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wingR.transform.SetParent(crow.transform);
        wingR.transform.localPosition = new Vector3(0.15f, 0, 0);
        wingR.transform.localScale = new Vector3(0.2f, 0.01f, 0.08f);
        Renderer wrR = wingR.GetComponent<Renderer>();
        wrR.material.color = new Color(0.05f, 0.05f, 0.08f);
        
        // Remover colliders para nao interferir
        foreach(Collider c in crow.GetComponentsInChildren<Collider>()) Destroy(c);
        
        return crow;
    }
    
    private void Update()
    {
        float dt = Time.deltaTime;
        
        for(int i=0; i<maxCrows; i++)
        {
            if (crowInstances[i] == null) continue;
            
            crowAngles[i] += circleSpeed * dt;
            
            // Animar asas batendo
            float wingFlap = Mathf.Sin(Time.time * 8f + i) * 15f;
            Transform wingL = crowInstances[i].transform.GetChild(1); // Left wing
            Transform wingR = crowInstances[i].transform.GetChild(2); // Right wing
            if (wingL != null) wingL.localRotation = Quaternion.Euler(0, 0, wingFlap);
            if (wingR != null) wingR.localRotation = Quaternion.Euler(0, 0, -wingFlap);
            
            // Circular motion
            float x = crowCenterPositions[i].x + Mathf.Cos(crowAngles[i]) * circleRadius;
            float z = crowCenterPositions[i].z + Mathf.Sin(crowAngles[i]) * circleRadius;
            float y = flyHeight + Mathf.Sin(Time.time * 0.5f + i) * 0.5f; // Leve flutuacao
            
            Vector3 newPos = new Vector3(x, y, z);
            Vector3 dir = (newPos - crowInstances[i].transform.position).normalized;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(dir);
                // Banking: corvos circulam no sentido trigonometrico ou inverso, inclinar para dentro do circulo
                Quaternion bankRot = Quaternion.Euler(0, 0, 30f); 
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
