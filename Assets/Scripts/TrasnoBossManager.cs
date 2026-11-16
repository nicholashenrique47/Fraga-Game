using UnityEngine;

public class TrasnoBossManager : MonoBehaviour
{
    public EntityEventSystem eventSystem;
    public GameManager gameManager;
    public DayNightCycle dayNightCycle;
    
    public GameObject trasnoPrefab;
    private GameObject activeTrasno;
    
    private bool trasnoSpawned = false;

    private void Update()
    {
        if (dayNightCycle == null || eventSystem == null) return;

        // O Trasno caça o jogador e barricadas à meia-noite (entre 0:00 e 1:00)
        if (!trasnoSpawned && dayNightCycle.currentTime >= 0f && dayNightCycle.currentTime < 1f)
        {
            SpawnTrasno();
        }
        
        // O Trasno foge com o nascer do Sol (6:00)
        if (trasnoSpawned && dayNightCycle.currentTime >= 6f && dayNightCycle.currentTime < 7f)
        {
            DespawnTrasno();
        }
        
        // Ataques e Destruição
        if (trasnoSpawned && activeTrasno != null)
        {
            CheckAttack();
        }
    }

    private void SpawnTrasno()
    {
        trasnoSpawned = true;
        
        // Nasce longe do centro (geralmente onde o player não está)
        int mw = (gameManager != null && gameManager.mapWidth > 0) ? gameManager.mapWidth : FragaWorldBlueprint.DefaultWidth;
        int mh = (gameManager != null && gameManager.mapHeight > 0) ? gameManager.mapHeight : FragaWorldBlueprint.DefaultHeight;
        float sx = Random.Range(120f, Mathf.Max(140f, mw - 120f));
        float sz = Random.Range(120f, Mathf.Max(140f, mh - 120f));
        Vector3 spawnPos = new Vector3(sx, TerrainMeshGenerator.GetTerrainHeight(sx, sz), sz);
        
        if (trasnoPrefab != null)
        {
            activeTrasno = Instantiate(trasnoPrefab, spawnPos, Quaternion.identity);
            eventSystem.trasnoTransform = activeTrasno.transform;
        }
        else
        {
            // Fallback (GameObject vazio)
            activeTrasno = new GameObject("Trasno_Fallback");
            activeTrasno.transform.position = spawnPos;
            eventSystem.trasnoTransform = activeTrasno.transform;
        }
        
        eventSystem.SpawnTrasno(spawnPos);
        
        if (VFXPoolManager.Instance != null)
        {
            VFXPoolManager.Instance.PlayTrasnoAppearVFX(spawnPos);
        }
        
        AudioManager am = FindAnyObjectByType<AudioManager>();
        if (am != null) am.PlayHorrorSting();
        
        Debug.LogWarning("[TrasnoBoss] O TRASNO SURGIU DAS SOMBRAS!");
    }

    private void DespawnTrasno()
    {
        trasnoSpawned = false;
        if (activeTrasno != null)
        {
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.PlayTrasnoDisappearVFX(activeTrasno.transform.position);
            }
            Destroy(activeTrasno);
        }
        Debug.Log("[TrasnoBoss] O Trasno fugiu com a luz da manha.");
    }

    private float attackTimer = 0f;
    private void CheckAttack()
    {
        if (gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero) return;

        attackTimer += Time.deltaTime;
        if (attackTimer < 1f) return; // Checa 1x por segundo
        
        attackTimer = 0f;
        
        int gridX = Mathf.RoundToInt(activeTrasno.transform.position.x);
        int gridY = Mathf.RoundToInt(activeTrasno.transform.position.z);
        
        // Verifica cross-pattern por Barricadas adjacentes
        int[] dx = {1, -1, 0, 0};
        int[] dy = {0, 0, 1, -1};
        
        int mw = Mathf.Max(1, gameManager.mapWidth);
        int mh = Mathf.Max(1, gameManager.mapHeight);
        for (int i = 0; i < 4; i++)
        {
            int checkX = gridX + dx[i];
            int checkY = gridY + dy[i];
            
            int cellID = ResourceManager.GetMapCell(gameManager.mapDataPtr, mw, mh, checkX, checkY);
            if (cellID == 3) // 3 = Barricada
            {
                // DESTRUIR
                gameManager.UpdateMapCellNative(checkX, checkY, 0); 
                
                if (VFXPoolManager.Instance != null)
                {
                    float vy = TerrainMeshGenerator.GetTerrainHeight(checkX, checkY);
                    VFXPoolManager.Instance.PlayBuildPlaceVFX(new Vector3(checkX, vy, checkY));
                }
                
                Debug.LogWarning($"[TrasnoBoss] BARRICADA DESTRUIDA em ({checkX}, {checkY}) pelo Trasno!");
                break;
            }
        }
    }
}
