using UnityEngine;
using System.Collections.Generic;

/// VFXPoolManager: Object Pooling de alto desempenho para efeitos de particulas.
/// Evita Instantiate/Destroy em runtime (critico para mobile).
/// Gere pools separados por tipo de efeito com reciclagem automatica.
public class VFXPoolManager : MonoBehaviour
{
    // Singleton leve (sem DontDestroyOnLoad para respeitar o ciclo de vida da cena)
    public static VFXPoolManager Instance { get; private set; }

    [Header("Pool Config")]
    public int initialPoolSize = 8;

    [Header("VFX Prefabs (Atribuir no Inspector ou via FragaSceneSetup)")]
    public GameObject celticBeaconVFX;    // Particulas de luz divina ao construir Farol Celta
    public GameObject oilTrapFireVFX;     // Chamas ao incendiar armadilha de oleo
    public GameObject trasnoAppearVFX;    // Fumaca negra ao Trasno aparecer
    public GameObject trasnoDisappearVFX; // Dissipacao ao Trasno fugir
    public GameObject buildPlaceVFX;      // Poeira ao construir qualquer estrutura
    public GameObject deathVFX;           // Particulas de escuridao ao morrer

    // Pools internos indexados por prefab
    private Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();

    private void Awake()
    {
        Instance = this;
        PrewarmAllPools();
    }

    private void PrewarmAllPools()
    {
        PrewarmPool(celticBeaconVFX);
        PrewarmPool(oilTrapFireVFX);
        PrewarmPool(trasnoAppearVFX);
        PrewarmPool(trasnoDisappearVFX);
        PrewarmPool(buildPlaceVFX);
        PrewarmPool(deathVFX);
    }

    private void PrewarmPool(GameObject prefab)
    {
        if (prefab == null) return;

        Queue<GameObject> pool = new Queue<GameObject>(initialPoolSize);
        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
        pools[prefab] = pool;
    }

    /// Spawna um efeito VFX do pool na posicao dada.
    /// Retorna a instancia para controlo externo se necessario.
    public GameObject SpawnVFX(GameObject prefab, Vector3 position)
    {
        return SpawnVFX(prefab, position, Quaternion.identity, 3f);
    }

    public GameObject SpawnVFX(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime)
    {
        if (prefab == null) return null;

        if (!pools.ContainsKey(prefab))
        {
            PrewarmPool(prefab);
        }

        Queue<GameObject> pool = pools[prefab];
        GameObject instance;

        if (pool.Count > 0)
        {
            instance = pool.Dequeue();
        }
        else
        {
            // Pool esgotada: expandir dinamicamente (raro se o initialPoolSize for adequado)
            instance = Instantiate(prefab, transform);
        }

        instance.transform.position = position;
        instance.transform.rotation = rotation;
        instance.SetActive(true);

        // Resetar o ParticleSystem se existir
        ParticleSystem ps = instance.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Clear(true);
            ps.Play(true);
        }

        // Agendar retorno ao pool
        StartCoroutine(ReturnToPoolAfter(instance, prefab, lifetime));

        return instance;
    }

    private System.Collections.IEnumerator ReturnToPoolAfter(GameObject obj, GameObject prefab, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToPool(obj, prefab);
    }

    public void ReturnToPool(GameObject obj, GameObject prefab)
    {
        if (obj == null) return;

        obj.SetActive(false);

        ParticleSystem ps = obj.GetComponent<ParticleSystem>();
        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (pools.ContainsKey(prefab))
        {
            pools[prefab].Enqueue(obj);
        }
    }

    // Metodos de conveniencia para os sistemas do jogo
    public void PlayCelticBeaconVFX(Vector3 position)
    {
        SpawnVFX(celticBeaconVFX, position + Vector3.up * 0.5f, Quaternion.identity, 4f);
    }

    public void PlayOilTrapFireVFX(Vector3 position)
    {
        SpawnVFX(oilTrapFireVFX, position, Quaternion.identity, 8f);
    }

    public void PlayTrasnoAppearVFX(Vector3 position)
    {
        SpawnVFX(trasnoAppearVFX, position + Vector3.up * 0.3f, Quaternion.identity, 2f);
    }

    public void PlayTrasnoDisappearVFX(Vector3 position)
    {
        SpawnVFX(trasnoDisappearVFX, position + Vector3.up * 0.3f, Quaternion.identity, 2.5f);
    }

    public void PlayBuildPlaceVFX(Vector3 position)
    {
        SpawnVFX(buildPlaceVFX, position, Quaternion.identity, 1.5f);
    }

    public void PlayDeathVFX(Vector3 position)
    {
        SpawnVFX(deathVFX, position + Vector3.up * 1f, Quaternion.identity, 5f);
    }
}
