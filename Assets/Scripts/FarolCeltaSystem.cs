using UnityEngine;

public class FarolCeltaSystem : MonoBehaviour
{
    [Header("Win Condition Settings")]
    public int requiredRunes = 5;
    public int requiredOil = 3;
    
    private int currentRunes = 0;
    private int currentOil = 0;
    private bool isLit = false;
    
    [Header("Effects")]
    public Light farolLight;
    public ParticleSystem firePillarVFX;
    private AudioSource hornAudio;
    private AudioClip hornClip;
    private float sweepAngle = 0f;
    
    private void Start()
    {
        if (farolLight != null) farolLight.enabled = false;
        if (firePillarVFX != null) firePillarVFX.Stop();
        
        hornAudio = gameObject.AddComponent<AudioSource>();
        hornAudio.spatialBlend = 1f;
        hornAudio.minDistance = 20f;
        hornAudio.maxDistance = 200f;
        hornClip = GenerateFoghornClip();
    }
    
    private void Update()
    {
        if (isLit && farolLight != null)
        {
            sweepAngle += Time.deltaTime * 60f; // 60 graus por segundo
            farolLight.transform.localRotation = Quaternion.Euler(15f, sweepAngle, 0f);
            
            // Fazer a luz piscar ligeiramente como fogo intenso
            farolLight.intensity = 50f + Mathf.PerlinNoise(Time.time * 5f, 0f) * 10f;
        }
    }
    
    private AudioClip GenerateFoghornClip()
    {
        int sampleRate = 44100;
        float duration = 6f; // Longo braaam
        float[] samples = new float[(int)(sampleRate * duration)];
        
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / samples.Length;
            float timeSec = (float)i / sampleRate;
            
            // Envelope: ataque rápido, sustain longo, fade out lento
            float env = 1f;
            if (t < 0.1f) env = t / 0.1f;
            else if (t > 0.7f) env = 1f - ((t - 0.7f) / 0.3f);
            
            // Som massivo de farol (mistura de saw e square grave)
            float freq1 = 65f; // Grave principal
            float freq2 = 66.5f; // Dissonância para criar batimento (beating)
            
            float s1 = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq1 * timeSec)); // Square
            float s2 = (timeSec * freq2) % 1f * 2f - 1f; // Saw
            
            float sample = (s1 * 0.6f + s2 * 0.4f) * env;
            samples[i] = sample;
        }

        AudioClip clip = AudioClip.Create("Synth_Foghorn", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
    
    public void InteractWithFarol(InventoryAdvanced inventory)
    {
        if (isLit) return;
        
        bool addedSomething = false;
        
        // ID 400 = Rune, ID 401 = Oil (exemplo)
        if (inventory.HasItem(400) && currentRunes < requiredRunes)
        {
            inventory.RemoveItem(400, 1);
            currentRunes++;
            addedSomething = true;
            Debug.Log($"[Farol] Runa adicionada! ({currentRunes}/{requiredRunes})");
        }
        
        if (inventory.HasItem(401) && currentOil < requiredOil)
        {
            inventory.RemoveItem(401, 1);
            currentOil++;
            addedSomething = true;
            Debug.Log($"[Farol] Óleo adicionado! ({currentOil}/{requiredOil})");
        }
        
        if (!addedSomething)
        {
            Debug.Log("[Farol] Faltam Pedras Rúnicas ou Óleo Bêntico para acender!");
            return;
        }
        
        if (currentRunes >= requiredRunes && currentOil >= requiredOil)
        {
            LightTheBeacon();
        }
    }
    
    private void LightTheBeacon()
    {
        isLit = true;
        
        if (farolLight != null)
        {
            farolLight.enabled = true;
            farolLight.intensity = 50f;
            farolLight.range = 650f; // Ilumina o cabo e o mar atlântico inteiro
            farolLight.color = new Color(1f, 0.4f, 0.1f); // Laranja Sagrado Intenso
            farolLight.type = LightType.Spot;
            farolLight.spotAngle = 60f;
            farolLight.shadows = LightShadows.Soft;
        }
        
        if (firePillarVFX != null) firePillarVFX.Play();
        
        if (hornAudio != null && hornClip != null)
        {
            hornAudio.PlayOneShot(hornClip, 1f);
        }
        
        // Destrói os inimigos todos (Purificação)
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach(var enemy in enemies)
        {
            Destroy(enemy);
            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(enemy.transform.position);
        }
        
        // UI de Vitória
        UIManager ui = FindAnyObjectByType<UIManager>();
        if (ui != null)
        {
            ui.SetLootTooltip("FAROL CELTA ACESO! A LUZ PURIFICOU A FRAGA!");
            Debug.Log("=====================================");
            Debug.Log("VITÓRIA! A LUZ DO FAROL PURIFICOU A FRAGA!");
            Debug.Log("=====================================");
        }
        
        HapticManager haptic = FindAnyObjectByType<HapticManager>();
        if (haptic != null) haptic.TriggerHeavyVibration();
    }
}
