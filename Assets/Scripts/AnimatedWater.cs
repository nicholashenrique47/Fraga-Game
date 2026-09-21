using UnityEngine;

/// AnimatedWater: Anima o offset da textura do material para simular agua fluindo.
public class AnimatedWater : MonoBehaviour
{
    public Vector2 scrollSpeed = new Vector2(0.1f, 0.1f);
    public float waveSpeed = 1f;
    public float waveHeight = 0.05f;
    
    private Material waterMat;
    private Vector2 offset;
    private Vector3 initialPos;
    
    private void Start()
    {
        Renderer r = GetComponent<Renderer>();
        if (r != null)
        {
            waterMat = r.material;
        }
        initialPos = transform.localPosition;
    }
    
    private void Update()
    {
        if (waterMat != null)
        {
            offset += scrollSpeed * Time.deltaTime;
            waterMat.SetTextureOffset("_MainTex", offset);
            if (waterMat.HasProperty("_BumpMap"))
            {
                waterMat.SetTextureOffset("_BumpMap", offset * 0.8f);
            }
        }
        
        // Animacao de onda vertice simples
        transform.localPosition = initialPos + new Vector3(0, Mathf.Sin(Time.time * waveSpeed) * waveHeight, 0);
    }
}
