using UnityEngine;

public class ResourcePickup : MonoBehaviour
{
    public int itemID;
    public int amount;
    private bool canPickup = false;
    private float spawnTime;

    private void Start()
    {
        spawnTime = Time.time;
        // Adiciona forca aleatoria para espalhar
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = new Vector3(Random.Range(-3f, 3f), Random.Range(2f, 5f), Random.Range(-3f, 3f));
            rb.angularVelocity = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), Random.Range(-5f, 5f));
        }
    }

    private void Update()
    {
        if (!canPickup && Time.time - spawnTime > 1.5f)
        {
            canPickup = true;
        }

        if (canPickup)
        {
            GameObject player = GameObject.Find("Player_CameraParent");
            if (player == null) player = GameObject.Find("Player");

            if (player != null)
            {
                float dist = Vector3.Distance(transform.position, player.transform.position);
                
                // Magnetismo quando proximo
                if (dist < 3f)
                {
                    transform.position = Vector3.Lerp(transform.position, player.transform.position, Time.deltaTime * 5f);
                }
                
                // Coleta
                if (dist < 1.0f)
                {
                    InventoryAdvanced inv = FindAnyObjectByType<InventoryAdvanced>();
                    if (inv != null)
                    {
                        inv.AddItem(itemID, amount);
                        UIManager ui = FindAnyObjectByType<UIManager>();
                        if (ui != null) ui.SetLootTooltip($"+{amount} ID: {itemID}");
                        
                        AudioManager am = FindAnyObjectByType<AudioManager>();
                        if (am != null) am.PlayHorrorSting(); // Efeito sonoro
                        
                        Destroy(gameObject);
                    }
                }
            }
        }
    }
}
