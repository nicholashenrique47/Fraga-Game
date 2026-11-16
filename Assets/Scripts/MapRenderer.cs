using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>
/// MapRenderer: Streaming de Mundo Aberto 768x768m (Mobile 60 FPS + PC High-Fidelity).
/// Arquitetura hibrida:
/// 1. Marcos Globais Permanentes (Acampamento, Farol Celta, Castro Celta, Gruta, Pico, Pontes, Pier)
/// 2. Streaming por Chunks 64x64m ao redor do jogador (5x5 Mobile / 7x7 PC) com orcamento por frame
/// 3. Instanciacao GPU (Graphics.DrawMeshInstanced) de arvores distantes nas serras e vales
/// 4. Espacamento caminhavel garantido em trilhas, clareiras, rochedos e pedras soltas coletaveis
/// </summary>
public class MapRenderer : MonoBehaviour
{
    [Header("Map Visuals (Prefabs)")]
    public GameObject treePrefab;
    public GameObject pineTreePrefab;
    public GameObject deadTreePrefab;
    public GameObject fentoPrefab;
    public GameObject ruinPrefab;
    public GameObject rockPrefab;
    public GameObject barricadePrefab;
    public GameObject campfirePrefab;
    public GameObject berryPrefab;
    public GameObject mushroomPrefab;
    public GameObject pondPrefab;
    public GameObject waterCatcherPrefab;
    public GameObject workbenchPrefab;
    public GameObject farmlandPrefab;
    public GameObject cauldronPrefab;
    public GameObject snareTrapPrefab;

    // Loop 8: Environmental
    public GameObject runicStonePrefab;
    public GameObject fallenLogPrefab;
    public GameObject thornyBushPrefab;
    public GameObject glowMushroomPrefab;
    public GameObject celticCrossPrefab;
    public GameObject ancientGravePrefab;
    public GameObject farolCeltaPrefab;

    // World Landmarks & Cliffs
    [Header("World Landmarks (768x768 Open World)")]
    public GameObject crashCampShelterPrefab;
    public GameObject woodenDockBridgePrefab;
    public GameObject swampTotemPrefab;
    public GameObject castroRoundhousePrefab;
    public GameObject stoneCircleHengePrefab;
    public GameObject caveArchGrottoPrefab;
    public GameObject cliffBoulderPrefab;

    [Header("Rendering & Streaming Settings")]
    public float tileSize = 1f;
    public int chunkSize = 64;

    [Header("Terrain Generator")]
    public TerrainMeshGenerator terrainGenerator;

    private System.IntPtr mapDataPtr;
    private int mapWidth;
    private int mapHeight;

    // Referencia instancias ativas proximas + marcos globais para coleta, combate e iluminacao dinamica
    [System.NonSerialized] public Dictionary<Vector2Int, GameObject> activeInstances = new Dictionary<Vector2Int, GameObject>();
    [System.NonSerialized] private readonly Dictionary<Vector2Int, PropKind> activePropKinds = new Dictionary<Vector2Int, PropKind>();

    private enum PropKind : byte
    {
        OakTree = 0,
        PineTree = 1,
        DeadTree = 2,
        Fern = 3,
        ThornyBush = 4,
        FallenLog = 5,
        Rock = 6,
        CliffBoulder = 7,
        Berry = 8,
        Mushroom = 9,
        GlowMushroom = 10,
        RunicStone = 11,
        CelticCross = 12,
        AncientGrave = 13,
        Ruin = 14,
        Pond = 15,
        WaterCatcher = 16,
        Workbench = 17,
        Farmland = 18,
        Cauldron = 19,
        SnareTrap = 20,
        Barricade = 21,
        Campfire = 22,
        LooseStone = 23,
        LooseBranch = 24
    }

    private struct PlannedProp
    {
        public Vector2Int gridKey;
        public int tileID;
        public PropKind kind;
        public Vector3 pos;
        public Quaternion rot;
        public Vector3 scale;
    }

    private class ChunkData
    {
        public int cx;
        public int cz;
        public Vector3 center;
        public GameObject rootObj;
        public bool isLoaded;
        public bool matricesDirty;
        public int spawnCursor;
        public List<PlannedProp> props = new List<PlannedProp>(64);
        public List<Matrix4x4> oakMats = new List<Matrix4x4>(32);
        public List<Matrix4x4> pineMats = new List<Matrix4x4>(32);
        public List<Matrix4x4> deadMats = new List<Matrix4x4>(8);
    }

    private ChunkData[,] chunkGrid;
    private int chunksX;
    private int chunksZ;
    private GameObject environmentParent;
    private GameObject landmarksParent;
    private GameObject poolParent;
    private Transform playerTransformCache;
    private Vector2Int lastPlayerChunk = new Vector2Int(-999, -999);
    private bool worldPlanned = false;

    // Persistencia de coleta e construcao do jogador atraves de streaming de chunks
    private readonly HashSet<Vector2Int> harvestedKeys = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, int> playerBuiltTiles = new Dictionary<Vector2Int, int>();
    private readonly Dictionary<Vector2Int, int> treeHitCount = new Dictionary<Vector2Int, int>();
    private readonly Dictionary<Vector2Int, int> rockHitCount = new Dictionary<Vector2Int, int>();
    private readonly Dictionary<PropKind, Stack<GameObject>> propPool = new Dictionary<PropKind, Stack<GameObject>>();
    private const int MaxPooledPerKind = 128;
    private System.Action<GameObject> recycleLooseStoneAction;
    private System.Action<GameObject> recycleLooseBranchAction;

    // GPU Instancing de copa distante (Zero-GC per frame)
    private Mesh oakTrunkMesh, oakFoliageMesh;
    private Material oakTrunkMat, oakFoliageMat;
    private Mesh pineTrunkMesh, pineFoliageMesh;
    private Material pineTrunkMat, pineFoliageMat;
    private Mesh deadTrunkMesh;
    private Material deadTrunkMat;
    private readonly Matrix4x4[] instancingBatch = new Matrix4x4[1023];

    private static Material s_barkFallback;
    private static Material s_foliageFallback;
    private static Material s_pineFallback;
    private static Material s_rockFallback;
    private static Material s_mossFallback;
    private static Mesh s_looseStoneMesh;
    private static Material s_looseStoneMat;
    private static Mesh s_looseBranchMesh;
    private static Material s_looseBranchMat;
    private static Material s_poiIronMat;
    private static Material s_poiParchmentMat;
    private static Material s_poiWaxMat;

    public void Initialize(System.IntPtr ptr, int width, int height)
    {
        mapDataPtr = ptr;
        mapWidth = width;
        mapHeight = height;
        if (recycleLooseStoneAction == null)
            recycleLooseStoneAction = go => RecyclePickupInstance(PropKind.LooseStone, go);
        if (recycleLooseBranchAction == null)
            recycleLooseBranchAction = go => RecyclePickupInstance(PropKind.LooseBranch, go);
        LoadFallbackPrefabs();
        UpgradeToAAATrees();
        CacheInstancingAssets();
    }

    private void LoadFallbackPrefabs()
    {
        if (fentoPrefab == null) fentoPrefab = Resources.Load<GameObject>("Prefabs/Fento_Prefab");
        if (rockPrefab == null) rockPrefab = Resources.Load<GameObject>("Prefabs/Rock_Prefab");
        if (ruinPrefab == null) ruinPrefab = Resources.Load<GameObject>("Prefabs/Ruin_Prefab");
        if (barricadePrefab == null) barricadePrefab = Resources.Load<GameObject>("Prefabs/Barricade_Prefab");
        if (campfirePrefab == null) campfirePrefab = Resources.Load<GameObject>("Prefabs/Campfire_Prefab");
        if (berryPrefab == null) berryPrefab = Resources.Load<GameObject>("Prefabs/Berry_Prefab");
        if (mushroomPrefab == null) mushroomPrefab = Resources.Load<GameObject>("Prefabs/Mushroom_Prefab");
        if (pondPrefab == null) pondPrefab = Resources.Load<GameObject>("Prefabs/Pond_Prefab");
        if (waterCatcherPrefab == null) waterCatcherPrefab = Resources.Load<GameObject>("Prefabs/WaterCatcher_Prefab");
        if (workbenchPrefab == null) workbenchPrefab = Resources.Load<GameObject>("Prefabs/Workbench_Prefab");
        if (farmlandPrefab == null) farmlandPrefab = Resources.Load<GameObject>("Prefabs/Farmland_Prefab");
        if (cauldronPrefab == null) cauldronPrefab = Resources.Load<GameObject>("Prefabs/Cauldron_Prefab");
        if (snareTrapPrefab == null) snareTrapPrefab = Resources.Load<GameObject>("Prefabs/SnareTrap_Prefab");
        if (runicStonePrefab == null) runicStonePrefab = Resources.Load<GameObject>("Prefabs/RunicStone_Prefab");
        if (fallenLogPrefab == null) fallenLogPrefab = Resources.Load<GameObject>("Prefabs/FallenLog_Prefab");
        if (thornyBushPrefab == null) thornyBushPrefab = Resources.Load<GameObject>("Prefabs/ThornyBush_Prefab");
        if (glowMushroomPrefab == null) glowMushroomPrefab = Resources.Load<GameObject>("Prefabs/GlowMushroom_Prefab");
        if (celticCrossPrefab == null) celticCrossPrefab = Resources.Load<GameObject>("Prefabs/CelticCross_Prefab");
        if (ancientGravePrefab == null) ancientGravePrefab = Resources.Load<GameObject>("Prefabs/AncientGrave_Prefab");
        if (farolCeltaPrefab == null) farolCeltaPrefab = Resources.Load<GameObject>("Prefabs/FarolCelta_Prefab");

        if (crashCampShelterPrefab == null) crashCampShelterPrefab = Resources.Load<GameObject>("Prefabs/CrashCampShelter_Prefab");
        if (woodenDockBridgePrefab == null) woodenDockBridgePrefab = Resources.Load<GameObject>("Prefabs/WoodenDockBridge_Prefab");
        if (swampTotemPrefab == null) swampTotemPrefab = Resources.Load<GameObject>("Prefabs/SwampTotem_Prefab");
        if (castroRoundhousePrefab == null) castroRoundhousePrefab = Resources.Load<GameObject>("Prefabs/CastroRoundhouse_Prefab");
        if (stoneCircleHengePrefab == null) stoneCircleHengePrefab = Resources.Load<GameObject>("Prefabs/StoneCircleHenge_Prefab");
        if (caveArchGrottoPrefab == null) caveArchGrottoPrefab = Resources.Load<GameObject>("Prefabs/CaveArchGrotto_Prefab");
        if (cliffBoulderPrefab == null) cliffBoulderPrefab = Resources.Load<GameObject>("Prefabs/CliffBoulder_Prefab");
    }

    private void UpgradeToAAATrees()
    {
        InitFallbackMaterials();
        try
        {
            GameObject oakTemplate = GalicianProceduralTreeGenerator.CreateTreePrefabObject(
                GalicianProceduralTreeGenerator.TreeType.GalicianOak, s_barkFallback, s_foliageFallback);
            oakTemplate.transform.position = new Vector3(0, -9999f, 0);
            oakTemplate.hideFlags = HideFlags.HideAndDontSave;
            treePrefab = oakTemplate;

            GameObject pineTemplate = GalicianProceduralTreeGenerator.CreateTreePrefabObject(
                GalicianProceduralTreeGenerator.TreeType.MaritimePine, s_barkFallback, s_pineFallback);
            pineTemplate.transform.position = new Vector3(0, -9999f, 0);
            pineTemplate.hideFlags = HideFlags.HideAndDontSave;
            pineTreePrefab = pineTemplate;

            GameObject deadTemplate = GalicianProceduralTreeGenerator.CreateTreePrefabObject(
                GalicianProceduralTreeGenerator.TreeType.DeadTree, s_barkFallback, null);
            deadTemplate.transform.position = new Vector3(0, -9999f, 0);
            deadTemplate.hideFlags = HideFlags.HideAndDontSave;
            deadTreePrefab = deadTemplate;
            Debug.Log("[MapRenderer] Modelos de Arvores AAA + GPU Instancing prontos para 768x768.");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[MapRenderer] Falha ao instanciar gerador procedural de arvores: " + ex.Message);
        }
    }

    private void CacheInstancingAssets()
    {
        ExtractTreeMeshPair(treePrefab, out oakTrunkMesh, out oakTrunkMat, out oakFoliageMesh, out oakFoliageMat);
        ExtractTreeMeshPair(pineTreePrefab, out pineTrunkMesh, out pineTrunkMat, out pineFoliageMesh, out pineFoliageMat);
        ExtractTreeMeshPair(deadTreePrefab, out deadTrunkMesh, out deadTrunkMat, out _, out _);
    }

    private void ExtractTreeMeshPair(
        GameObject prefab,
        out Mesh trunkMesh, out Material trunkMat,
        out Mesh foliageMesh, out Material foliageMat)
    {
        trunkMesh = null; trunkMat = null;
        foliageMesh = null; foliageMat = null;
        if (prefab == null) return;

        Transform tChild = prefab.transform.Find("Trunk");
        if (tChild != null)
        {
            MeshFilter mf = tChild.GetComponent<MeshFilter>();
            MeshRenderer mr = tChild.GetComponent<MeshRenderer>();
            if (mf != null) trunkMesh = mf.sharedMesh;
            if (mr != null)
            {
                trunkMat = mr.sharedMaterial != null ? mr.sharedMaterial : s_barkFallback;
                if (trunkMat != null) trunkMat.enableInstancing = true;
            }
        }

        Transform fChild = prefab.transform.Find("Foliage");
        if (fChild != null)
        {
            MeshFilter mf = fChild.GetComponent<MeshFilter>();
            MeshRenderer mr = fChild.GetComponent<MeshRenderer>();
            if (mf != null) foliageMesh = mf.sharedMesh;
            if (mr != null)
            {
                foliageMat = mr.sharedMaterial != null ? mr.sharedMaterial : s_foliageFallback;
                if (foliageMat != null) foliageMat.enableInstancing = true;
            }
        }
    }

    private static void InitFallbackMaterials()
    {
        if (s_barkFallback == null) s_barkFallback = Resources.Load<Material>("Materials/Mat_DarkForest_Bark");
        if (s_foliageFallback == null) s_foliageFallback = Resources.Load<Material>("Materials/Mat_OakFoliage");
        if (s_pineFallback == null) s_pineFallback = Resources.Load<Material>("Materials/Mat_PineNeedle");
        if (s_rockFallback == null) s_rockFallback = Resources.Load<Material>("Materials/Celtic_Stone");
        if (s_mossFallback == null) s_mossFallback = Resources.Load<Material>("Materials/Mat_DarkForest_Moss");
    }

    private static void EnsureLooseStoneAssets()
    {
        InitFallbackMaterials();
        if (s_looseStoneMesh == null)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh src = temp.GetComponent<MeshFilter>().sharedMesh;
            s_looseStoneMesh = Instantiate(src);
            s_looseStoneMesh.name = "Procedural_LooseStone_Mesh";
            Vector3[] verts = s_looseStoneMesh.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i];
                float wobble = 0.84f + 0.24f * Mathf.Sin(v.x * 7.3f + v.z * 5.1f) * Mathf.Cos(v.y * 6.7f);
                verts[i] = new Vector3(v.x * 0.36f * wobble, v.y * 0.22f * wobble, v.z * 0.32f * wobble);
            }
            s_looseStoneMesh.vertices = verts;
            s_looseStoneMesh.RecalculateNormals();
            s_looseStoneMesh.RecalculateBounds();
            Destroy(temp);
        }

        if (s_looseStoneMat == null)
        {
            if (s_rockFallback != null)
            {
                s_looseStoneMat = s_rockFallback;
            }
            else
            {
                s_looseStoneMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                s_looseStoneMat.color = new Color(0.50f, 0.50f, 0.52f);
                s_looseStoneMat.SetFloat("_Smoothness", 0.18f);
            }
        }
    }

    private GameObject CreateLooseStonePickup(Vector3 pos, Quaternion rot, Vector3 scale, int amount, Transform parent, Vector2Int? chunkKey)
    {
        EnsureLooseStoneAssets();

        GameObject go = new GameObject("Loose_Stone_Pickup");
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = s_looseStoneMesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = s_looseStoneMat;

        SphereCollider col = go.AddComponent<SphereCollider>();
        col.radius = 0.44f;
        col.center = new Vector3(0f, 0.08f, 0f);
        col.isTrigger = true;

        ResourcePickup pickup = go.AddComponent<ResourcePickup>();
        pickup.ResetPickupState(101, amount, chunkKey.HasValue, chunkKey ?? Vector2Int.zero, launch: false, rotate: false, lifetime: 0f, magnet: 1.35f);
        if (chunkKey.HasValue) pickup.onRecycle = recycleLooseStoneAction;

        return go;
    }

    private static void EnsureLooseBranchAssets()
    {
        InitFallbackMaterials();
        if (s_looseBranchMesh == null)
        {
            GameObject main = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            GameObject twig = GameObject.CreatePrimitive(PrimitiveType.Cylinder);

            CombineInstance[] combine = new CombineInstance[2];
            combine[0].mesh = main.GetComponent<MeshFilter>().sharedMesh;
            combine[0].transform = Matrix4x4.TRS(
                new Vector3(0f, 0.045f, 0f),
                Quaternion.Euler(88f, 0f, 0f),
                new Vector3(0.065f, 0.34f, 0.065f));

            combine[1].mesh = twig.GetComponent<MeshFilter>().sharedMesh;
            combine[1].transform = Matrix4x4.TRS(
                new Vector3(0.08f, 0.06f, 0.08f),
                Quaternion.Euler(78f, 38f, 12f),
                new Vector3(0.042f, 0.16f, 0.042f));

            s_looseBranchMesh = new Mesh { name = "Procedural_LooseBranch_Mesh" };
            s_looseBranchMesh.CombineMeshes(combine, true, true);
            s_looseBranchMesh.RecalculateNormals();
            s_looseBranchMesh.RecalculateBounds();

            Destroy(main);
            Destroy(twig);
        }

        if (s_looseBranchMat == null)
        {
            if (s_barkFallback != null)
            {
                s_looseBranchMat = s_barkFallback;
            }
            else
            {
                s_looseBranchMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                s_looseBranchMat.color = new Color(0.36f, 0.24f, 0.14f);
                s_looseBranchMat.SetFloat("_Smoothness", 0.12f);
            }
        }
    }

    private GameObject CreateLooseBranchPickup(Vector3 pos, Quaternion rot, Vector3 scale, int amount, Transform parent, Vector2Int? chunkKey)
    {
        EnsureLooseBranchAssets();

        GameObject go = new GameObject("Loose_Branch_Pickup");
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = s_looseBranchMesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = s_looseBranchMat;

        SphereCollider col = go.AddComponent<SphereCollider>();
        col.radius = 0.48f;
        col.center = new Vector3(0f, 0.08f, 0f);
        col.isTrigger = true;

        ResourcePickup pickup = go.AddComponent<ResourcePickup>();
        pickup.ResetPickupState(100, amount, chunkKey.HasValue, chunkKey ?? Vector2Int.zero, launch: false, rotate: false, lifetime: 0f, magnet: 1.35f);
        if (chunkKey.HasValue) pickup.onRecycle = recycleLooseBranchAction;

        return go;
    }

    private static void AttachRockNode(GameObject obj, Vector2Int gridKey, int tileID, bool isPermanent)
    {
        if (obj == null) return;
        MineableRockNode node = obj.GetComponent<MineableRockNode>();
        if (node == null) node = obj.AddComponent<MineableRockNode>();
        node.ResetNode(gridKey, tileID, isPermanent);
    }

    public static void EnsureValidMaterials(GameObject go, Material primary = null, Material secondary = null)
    {
        if (go == null) return;
        InitFallbackMaterials();

        if (primary == null)
        {
            string n = go.name.ToLower();
            if (n.Contains("fern") || n.Contains("fento") || n.Contains("bush") || n.Contains("foliage") || n.Contains("leaf") || n.Contains("berry"))
                primary = s_foliageFallback;
            else if (n.Contains("rock") || n.Contains("stone") || n.Contains("ruin") || n.Contains("grave") || n.Contains("cross") || n.Contains("cliff") || n.Contains("castro") || n.Contains("cave") || n.Contains("henge"))
                primary = s_rockFallback;
            else if (n.Contains("moss") || n.Contains("mush"))
                primary = s_mossFallback;
            else
                primary = s_barkFallback;
        }

        Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends)
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            int subCount = (mf != null && mf.sharedMesh != null) ? Mathf.Max(1, mf.sharedMesh.subMeshCount) : 1;
            Material[] mats = r.sharedMaterials;
            bool needsFix = (mats == null || mats.Length < subCount);
            if (!needsFix)
            {
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] == null || mats[m].shader == null || mats[m].shader.name.Contains("Error") || mats[m].shader.name == "Hidden/InternalErrorShader")
                    {
                        needsFix = true;
                        break;
                    }
                }
            }
            if (needsFix)
            {
                Material[] fixedMats = new Material[subCount];
                for (int s = 0; s < subCount; s++)
                {
                    bool validSlot = (mats != null && s < mats.Length && mats[s] != null && mats[s].shader != null && !mats[s].shader.name.Contains("Error") && mats[s].shader.name != "Hidden/InternalErrorShader");
                    if (validSlot)
                    {
                        fixedMats[s] = mats[s];
                    }
                    else
                    {
                        fixedMats[s] = (s == 0 || secondary == null) ? primary : secondary;
                        if (fixedMats[s] == null) fixedMats[s] = s_barkFallback;
                    }
                }
                r.sharedMaterials = fixedMats;
            }
        }
    }

    private static uint HashCoords(int x, int z, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + z * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }

    private static float Hash01(uint h, int salt = 0)
    {
        unchecked
        {
            uint v = h ^ (uint)(salt * 747796405 + 2891336453u);
            v = ((v >> ((int)(v >> 28) + 4)) ^ v) * 277803737u;
            v ^= v >> 22;
            return (v & 0x00FFFFFFu) / 16777216f;
        }
    }

    private void EnsurePoolParent()
    {
        if (poolParent == null)
        {
            poolParent = new GameObject("MapProp_Pool");
            poolParent.SetActive(false);
        }
    }

    private void ClearPool()
    {
        foreach (var kvp in propPool)
        {
            kvp.Value.Clear();
        }
        propPool.Clear();
        if (poolParent != null)
        {
            Destroy(poolParent);
            poolParent = null;
        }
    }

    private bool TryPopFromPool(PropKind kind, Transform newParent, out GameObject inst)
    {
        if (propPool.TryGetValue(kind, out Stack<GameObject> stack))
        {
            while (stack.Count > 0)
            {
                inst = stack.Pop();
                if (inst != null)
                {
                    if (newParent != null) inst.transform.SetParent(newParent, false);
                    return true;
                }
            }
        }
        inst = null;
        return false;
    }

    private void ReturnToPool(PropKind kind, GameObject inst)
    {
        if (inst == null) return;
        EnsurePoolParent();

        if (!propPool.TryGetValue(kind, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>(32);
            propPool[kind] = stack;
        }

        if (stack.Count >= MaxPooledPerKind)
        {
            Destroy(inst);
            return;
        }

        inst.SetActive(false);
        inst.transform.SetParent(poolParent.transform, false);
        stack.Push(inst);
    }

    private void RecyclePickupInstance(PropKind kind, GameObject go)
    {
        if (go == null) return;
        ResourcePickup p = go.GetComponent<ResourcePickup>();
        if (p != null) activePropKinds.Remove(p.mapGridKey);
        ReturnToPool(kind, go);
    }

    public void ResetWorldState()
    {
        harvestedKeys.Clear();
        playerBuiltTiles.Clear();
        treeHitCount.Clear();
        rockHitCount.Clear();
    }

    public void ExportWorldState(List<int> outHarvestedXY, List<int> outBuiltXYZ)
    {
        if (outHarvestedXY != null)
        {
            outHarvestedXY.Clear();
            foreach (Vector2Int key in harvestedKeys)
            {
                outHarvestedXY.Add(key.x);
                outHarvestedXY.Add(key.y);
            }
        }

        if (outBuiltXYZ != null)
        {
            outBuiltXYZ.Clear();
            foreach (var kvp in playerBuiltTiles)
            {
                outBuiltXYZ.Add(kvp.Key.x);
                outBuiltXYZ.Add(kvp.Key.y);
                outBuiltXYZ.Add(kvp.Value);
            }
        }
    }

    public void ImportWorldState(List<int> harvestedXY, List<int> builtXYZ)
    {
        harvestedKeys.Clear();
        playerBuiltTiles.Clear();

        if (harvestedXY != null)
        {
            for (int i = 0; i + 1 < harvestedXY.Count; i += 2)
            {
                harvestedKeys.Add(new Vector2Int(harvestedXY[i], harvestedXY[i + 1]));
            }
        }

        if (builtXYZ != null)
        {
            for (int i = 0; i + 2 < builtXYZ.Count; i += 3)
            {
                playerBuiltTiles[new Vector2Int(builtXYZ[i], builtXYZ[i + 1])] = builtXYZ[i + 2];
            }
        }
    }

    public void RenderInitialMap(bool preserveRestoredState = false)
    {
        if (mapDataPtr == System.IntPtr.Zero || treePrefab == null)
        {
            Debug.LogError("[MapRenderer] Prefabs de mapa nao configurados.");
            return;
        }

        FragaWorldBlueprint.EnsureInitialized();
        LoadFallbackPrefabs();

        // Limpa instancias e chunks antigos (suporte a New Game / Load)
        foreach (var kvp in activeInstances)
        {
            if (kvp.Value != null) Destroy(kvp.Value);
        }
        activeInstances.Clear();
        activePropKinds.Clear();
        if (!preserveRestoredState)
        {
            harvestedKeys.Clear();
            playerBuiltTiles.Clear();
        }
        treeHitCount.Clear();
        rockHitCount.Clear();
        ClearPool();

        if (environmentParent != null) Destroy(environmentParent);
        if (landmarksParent != null) Destroy(landmarksParent);

        environmentParent = new GameObject("Environment_Instances");
        landmarksParent = new GameObject("Landmarks_Global");
        EnsurePoolParent();

        // 1. Terreno chunked + Aguas Atlanticas
        if (terrainGenerator != null)
        {
            terrainGenerator.GenerateTerrain(mapDataPtr, mapWidth, mapHeight);
        }

        // 2. Planejamento espacial deterministico por Chunks 64x64m
        int cSize = Mathf.Max(32, chunkSize);
        chunksX = Mathf.CeilToInt(mapWidth / (float)cSize);
        chunksZ = Mathf.CeilToInt(mapHeight / (float)cSize);
        chunkGrid = new ChunkData[chunksX, chunksZ];

        for (int cz = 0; cz < chunksZ; cz++)
        {
            for (int cx = 0; cx < chunksX; cx++)
            {
                ChunkData cd = new ChunkData
                {
                    cx = cx,
                    cz = cz,
                    center = new Vector3((cx + 0.5f) * cSize * tileSize, 8f, (cz + 0.5f) * cSize * tileSize),
                    isLoaded = false,
                    matricesDirty = false,
                    spawnCursor = 0
                };
                chunkGrid[cx, cz] = cd;
            }
        }

        BuildChunkPropPlan(cSize);

        // 3. Marcos Globais Permanentes (nunca sofrem culling de chunk)
        SpawnGlobalLandmarks();

        // Restaura construcoes criadas pelo jogador em sessoes salvas
        if (preserveRestoredState && playerBuiltTiles.Count > 0)
        {
            var savedBuilt = new List<KeyValuePair<Vector2Int, int>>(playerBuiltTiles);
            for (int i = 0; i < savedBuilt.Count; i++)
            {
                InstantiateTile(savedBuilt[i].Key.x, savedBuilt[i].Key.y, savedBuilt[i].Value);
            }
        }

        // 4. Carrega imediatamente os 3x3 chunks ao redor do jogador/spawn para zero pop-in inicial
        ResolvePlayerTransform();
        Vector3 focusPos = playerTransformCache != null
            ? playerTransformCache.position
            : new Vector3(FragaWorldBlueprint.CrashCampPos.x, 0f, FragaWorldBlueprint.CrashCampPos.y);

        int pChunkX = Mathf.Clamp(Mathf.FloorToInt(focusPos.x / (cSize * tileSize)), 0, chunksX - 1);
        int pChunkZ = Mathf.Clamp(Mathf.FloorToInt(focusPos.z / (cSize * tileSize)), 0, chunksZ - 1);
        lastPlayerChunk = new Vector2Int(pChunkX, pChunkZ);

        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = pChunkX + dx;
                int nz = pChunkZ + dz;
                if (nx >= 0 && nx < chunksX && nz >= 0 && nz < chunksZ)
                {
                    SpawnAllPropsInChunk(chunkGrid[nx, nz]);
                }
            }
        }

        worldPlanned = true;
        Debug.Log($"[MapRenderer] Mundo 768x768 planejado em {chunksX}x{chunksZ} chunks. Instancias iniciais ativas: {activeInstances.Count}.");
    }

    private void BuildChunkPropPlan(int cSize)
    {
        // Grade espacial de pedras (16m minimos entre rochedos - caminho sempre livre e natural!)
        int rockCellSize = 16;
        int rockGridW = Mathf.CeilToInt(mapWidth / (float)rockCellSize) + 1;
        int rockGridH = Mathf.CeilToInt(mapHeight / (float)rockCellSize) + 1;
        bool[,] rockOccupied = new bool[rockGridW, rockGridH];

        bool TryReserveRockSlot(int wx, int wz)
        {
            int gx = Mathf.Clamp(wx / rockCellSize, 0, rockGridW - 1);
            int gz = Mathf.Clamp(wz / rockCellSize, 0, rockGridH - 1);
            for (int dz = -1; dz <= 1; dz++)
            {
                int tz = gz + dz;
                if (tz < 0 || tz >= rockGridH) continue;
                for (int dx = -1; dx <= 1; dx++)
                {
                    int tx = gx + dx;
                    if (tx < 0 || tx >= rockGridW) continue;
                    if (rockOccupied[tx, tz]) return false;
                }
            }
            rockOccupied[gx, gz] = true;
            return true;
        }

        int seed = FragaWorldBlueprint.Seed;

        for (int z = 2; z < mapHeight - 2; z++)
        {
            for (int x = 2; x < mapWidth - 2; x++)
            {
                // Filtro ultrarrapido antes de consultar altura/agua/normais
                bool isTreeGridSlot = (x % 5 == 0) && ((z + (x / 5) % 2 * 2) % 5 == 0);
                bool isDetailSlot = ((x + z) % 2 == 0);
                int tileID = ResourceManager.GetMapCell(mapDataPtr, mapWidth, mapHeight, x, z);
                if (!isTreeGridSlot && !isDetailSlot && tileID != 2 && tileID != 8)
                    continue;

                float wx = x * tileSize;
                float wz = z * tileSize;

                float coast = FragaWorldBlueprint.GetIslandCoastMetric(wx, wz);
                if (coast >= 0.96f) continue; // Oceano ou linha de mare

                float waterDepth = FragaWorldBlueprint.GetWaterDepth(wx, wz);
                if (waterDepth > 0.08f) continue; // Dentro do Lago ou leito do Rio

                // Mantem POIs e pontes amplamente desobstruidos ao redor de suas estruturas principais
                float dx = wx - FragaWorldBlueprint.CrashCampPos.x;
                float dz = wz - FragaWorldBlueprint.CrashCampPos.y;
                if (dx * dx + dz * dz < 15.5f * 15.5f) continue;
                dx = wx - FragaWorldBlueprint.CelticCastroPos.x; dz = wz - FragaWorldBlueprint.CelticCastroPos.y;
                if (dx * dx + dz * dz < 22f * 22f) continue;
                dx = wx - FragaWorldBlueprint.FarolBluffPos.x; dz = wz - FragaWorldBlueprint.FarolBluffPos.y;
                if (dx * dx + dz * dz < 15f * 15f) continue;
                dx = wx - FragaWorldBlueprint.MountainSummitPos.x; dz = wz - FragaWorldBlueprint.MountainSummitPos.y;
                if (dx * dx + dz * dz < 11f * 11f) continue;
                dx = wx - FragaWorldBlueprint.SacredCavePos.x; dz = wz - FragaWorldBlueprint.SacredCavePos.y;
                if (dx * dx + dz * dz < 13f * 13f) continue;

                bool nearBridge = false;
                for (int b = 0; b < FragaWorldBlueprint.RiverBridges.Length; b++)
                {
                    dx = wx - FragaWorldBlueprint.RiverBridges[b].Pos.x;
                    dz = wz - FragaWorldBlueprint.RiverBridges[b].Pos.y;
                    if (dx * dx + dz * dz < 9.5f * 9.5f)
                    {
                        nearBridge = true;
                        break;
                    }
                }
                if (nearBridge) continue;

                float trailW = FragaWorldBlueprint.GetTrailWeight(wx, wz);
                float groundY = TerrainMeshGenerator.GetTerrainHeight(wx, wz);

                // Trilhas principais ficam limpas (apenas rara samambaia na borda externa da trilha)
                if (trailW > 0.16f)
                {
                    if (trailW < 0.32f && fentoPrefab != null && (x + z) % 7 == 0)
                    {
                        uint fh = HashCoords(x, z, seed + 17);
                        if (Hash01(fh, 1) < 0.14f)
                        {
                            AddPropToChunk(x, z, cSize, 0, PropKind.Fern,
                                new Vector3(wx, groundY, wz),
                                Quaternion.Euler(0f, Hash01(fh, 2) * 360f, 0f),
                                Vector3.one * Mathf.Lerp(0.65f, 0.95f, Hash01(fh, 3)));
                        }
                    }
                    continue;
                }

                // Evita arvores dentro de agua rasa ou praia arenosa
                bool isSandyBeach = (coast > 0.88f && groundY < FragaWorldBlueprint.SeaLevel + 1.3f);
                FragaWorldBlueprint.WorldRegion reg = FragaWorldBlueprint.GetRegion(wx, wz);
                uint h = HashCoords(x, z, seed);
                float r0 = Hash01(h, 1);
                float r1 = Hash01(h, 2);
                float r2 = Hash01(h, 3);
                float r3 = Hash01(h, 4);

                Vector3 normal = TerrainMeshGenerator.GetTerrainNormal(wx, wz);
                float slope = 1f - normal.y;

                if (tileID == 2 && ruinPrefab != null)
                {
                    AddPropToChunk(x, z, cSize, tileID, PropKind.Ruin,
                        new Vector3(wx, groundY, wz),
                        Quaternion.Euler(0f, r0 * 360f, 0f),
                        Vector3.one * Mathf.Lerp(0.9f, 1.15f, r1));
                    continue;
                }

                if (tileID == 8 && slope < 0.45f)
                {
                    if (TryReserveRockSlot(x, z))
                    {
                        PropKind rk = (reg == FragaWorldBlueprint.WorldRegion.PineRidge || reg == FragaWorldBlueprint.WorldRegion.FarolBluff || groundY > 12f) && cliffBoulderPrefab != null
                            ? PropKind.CliffBoulder
                            : PropKind.Rock;
                        float sXZ = Mathf.Lerp(0.85f, 1.35f, r1);
                        float sY = Mathf.Lerp(0.70f, 1.15f, r2);
                        AddPropToChunk(x, z, cSize, 8, rk,
                            new Vector3(wx, groundY, wz),
                            Quaternion.Euler(r2 * 6f - 3f, r0 * 360f, r3 * 6f - 3f),
                            new Vector3(sXZ, sY, sXZ));
                    }
                    continue;
                }

                // Arvores (nao nascem na neve > 26.5m, nem na praia arenosa, nem em paredao vertical)
                if (isTreeGridSlot && !isSandyBeach && slope < 0.34f && groundY < 26.0f)
                {
                    float canopyNoise = Mathf.PerlinNoise(wx * 0.032f + 13.5f, wz * 0.032f + 71.2f);
                    float densityThresh = (reg == FragaWorldBlueprint.WorldRegion.SwampBasin) ? 0.44f :
                                          (reg == FragaWorldBlueprint.WorldRegion.HighlandLake) ? 0.46f : 0.32f;

                    bool spawnTree = (canopyNoise > densityThresh) || (r0 < 0.14f);
                    if (spawnTree)
                    {
                        float jx = wx + (r1 - 0.5f) * 1.1f;
                        float jz = wz + (r2 - 0.5f) * 1.1f;
                        float ty = TerrainMeshGenerator.GetTerrainHeight(jx, jz);
                        if (FragaWorldBlueprint.GetWaterDepth(jx, jz) <= 0.05f && FragaWorldBlueprint.GetTrailWeight(jx, jz) < 0.16f)
                        {
                            PropKind tk;
                            if (reg == FragaWorldBlueprint.WorldRegion.SwampBasin)
                            {
                                tk = (r3 < 0.62f && deadTreePrefab != null) ? PropKind.DeadTree : PropKind.OakTree;
                            }
                            else if (reg == FragaWorldBlueprint.WorldRegion.PineRidge || ty > 11.5f)
                            {
                                tk = (r3 < 0.78f && pineTreePrefab != null) ? PropKind.PineTree :
                                     (r3 > 0.94f && deadTreePrefab != null) ? PropKind.DeadTree : PropKind.OakTree;
                            }
                            else
                            {
                                tk = (r3 < 0.24f && pineTreePrefab != null) ? PropKind.PineTree :
                                     (r3 > 0.93f && deadTreePrefab != null) ? PropKind.DeadTree : PropKind.OakTree;
                            }

                            float overallScale = Mathf.Lerp(0.86f, 1.32f, r0);
                            float widthVar = Mathf.Lerp(0.92f, 1.14f, r1);
                            Quaternion trot = Quaternion.Euler((r1 - 0.5f) * 5f, r2 * 360f, (r3 - 0.5f) * 5f);
                            AddPropToChunk(x, z, cSize, 1, tk,
                                new Vector3(jx, ty, jz), trot,
                                new Vector3(overallScale * widthVar, overallScale, overallScale * widthVar));
                            continue;
                        }
                    }
                }

                // Vegetacao rasteira esparsa, coletas, totens, pedras soltas e raras pedras em encostas (passo x2)
                if (!isDetailSlot || slope > 0.42f) continue;

                if (isSandyBeach)
                {
                    if (r0 < 0.012f && fallenLogPrefab != null)
                    {
                        AddPropToChunk(x, z, cSize, 18, PropKind.FallenLog,
                            new Vector3(wx, groundY, wz), Quaternion.Euler(0f, r1 * 360f, 0f),
                            Vector3.one * Mathf.Lerp(0.85f, 1.25f, r2));
                    }
                    else if (r0 < 0.020f && cliffBoulderPrefab != null && TryReserveRockSlot(x, z))
                    {
                        AddPropToChunk(x, z, cSize, 8, PropKind.CliffBoulder,
                            new Vector3(wx, groundY, wz), Quaternion.Euler(0f, r1 * 360f, 0f),
                            Vector3.one * Mathf.Lerp(0.8f, 1.25f, r2));
                    }
                    else if (r0 < 0.044f)
                    {
                        float s = Mathf.Lerp(0.85f, 1.28f, r2);
                        AddPropToChunk(x, z, cSize, 0, PropKind.LooseStone,
                            new Vector3(wx, groundY + 0.05f, wz),
                            Quaternion.Euler(r2 * 14f - 7f, r1 * 360f, r3 * 14f - 7f),
                            new Vector3(s, s * 0.78f, s));
                    }
                    else if (r0 < 0.060f)
                    {
                        AddPropToChunk(x, z, cSize, 0, PropKind.LooseBranch,
                            new Vector3(wx, groundY + 0.03f, wz),
                            Quaternion.Euler(r2 * 8f - 4f, r1 * 360f, r3 * 8f - 4f),
                            Vector3.one * Mathf.Lerp(0.85f, 1.20f, r2));
                    }
                    continue;
                }

                if (reg == FragaWorldBlueprint.WorldRegion.SwampBasin)
                {
                    if (glowMushroomPrefab != null && r0 < 0.045f)
                        AddPropToChunk(x, z, cSize, 20, PropKind.GlowMushroom, new Vector3(wx, groundY, wz), Quaternion.Euler(0f, r1 * 360f, 0f), Vector3.one * Mathf.Lerp(0.9f, 1.4f, r2));
                    else if (pondPrefab != null && r0 < 0.048f && TryReserveRockSlot(x, z))
                        AddPropToChunk(x, z, cSize, 11, PropKind.Pond, new Vector3(wx, groundY - 0.05f, wz), Quaternion.Euler(0f, r1 * 360f, 0f), Vector3.one * Mathf.Lerp(1.2f, 1.8f, r2));
                    else if (fentoPrefab != null && r0 < 0.09f)
                        AddPropToChunk(x, z, cSize, 0, PropKind.Fern, new Vector3(wx, groundY, wz), Quaternion.Euler(0f, r1 * 360f, 0f), Vector3.one * Mathf.Lerp(0.85f, 1.3f, r2));
                    else if (ancientGravePrefab != null && r0 < 0.095f)
                        AddPropToChunk(x, z, cSize, 22, PropKind.AncientGrave, new Vector3(wx, groundY, wz), Quaternion.Euler(0f, r1 * 360f, 0f), Vector3.one);
                    else if (r0 < 0.108f)
                    {
                        float s = Mathf.Lerp(0.82f, 1.22f, r2);
                        AddPropToChunk(x, z, cSize, 0, PropKind.LooseStone,
                            new Vector3(wx, groundY + 0.05f, wz),
                            Quaternion.Euler(r2 * 12f - 6f, r1 * 360f, r3 * 12f - 6f),
                            new Vector3(s, s * 0.8f, s));
                    }
                    else if (r0 < 0.122f)
                    {
                        AddPropToChunk(x, z, cSize, 0, PropKind.LooseBranch,
                            new Vector3(wx, groundY + 0.03f, wz),
                            Quaternion.Euler(r2 * 8f - 4f, r1 * 360f, r3 * 8f - 4f),
                            Vector3.one * Mathf.Lerp(0.85f, 1.25f, r2));
                    }
                }
                else
                {
                    if (fentoPrefab != null && r0 < 0.055f)
                        AddPropToChunk(x, z, cSize, 0, PropKind.Fern, new Vector3(wx, groundY, wz), Quaternion.Euler(0f, r1 * 360f, 0f), Vector3.one * Mathf.Lerp(0.8f, 1.3f, r2));
                    else if (thornyBushPrefab != null && r0 < 0.072f)
                        AddPropToChunk(x, z, cSize, 19, PropKind.ThornyBush, new Vector3(wx, groundY, wz), Quaternion.Euler(0f, r1 * 360f, 0f), Vector3.one * Mathf.Lerp(0.8f, 1.15f, r2));
                    else if (fallenLogPrefab != null && r0 < 0.082f)
                        AddPropToChunk(x, z, cSize, 18, PropKind.FallenLog, new Vector3(wx, groundY, wz), Quaternion.Euler(0f, r1 * 360f, 0f), Vector3.one * Mathf.Lerp(0.9f, 1.3f, r2));
                    else if (berryPrefab != null && r0 < 0.096f)
                        AddPropToChunk(x, z, cSize, 9, PropKind.Berry, new Vector3(wx, groundY, wz), Quaternion.Euler(0f, r1 * 360f, 0f), Vector3.one * Mathf.Lerp(0.85f, 1.2f, r2));
                    else if (mushroomPrefab != null && r0 < 0.108f)
                        AddPropToChunk(x, z, cSize, 10, PropKind.Mushroom, new Vector3(wx, groundY, wz), Quaternion.Euler(0f, r1 * 360f, 0f), Vector3.one * Mathf.Lerp(0.85f, 1.25f, r2));
                    else if (glowMushroomPrefab != null && r0 < 0.114f)
                        AddPropToChunk(x, z, cSize, 20, PropKind.GlowMushroom, new Vector3(wx, groundY, wz), Quaternion.Euler(0f, r1 * 360f, 0f), Vector3.one * Mathf.Lerp(0.9f, 1.3f, r2));
                    else if (runicStonePrefab != null && r0 < 0.117f)
                        AddPropToChunk(x, z, cSize, 17, PropKind.RunicStone, new Vector3(wx, groundY, wz), Quaternion.Euler(0f, r1 * 360f, 0f), Vector3.one);
                    else if (celticCrossPrefab != null && r0 < 0.119f)
                        AddPropToChunk(x, z, cSize, 21, PropKind.CelticCross, new Vector3(wx, groundY, wz), Quaternion.Euler(0f, r1 * 360f, 0f), Vector3.one);
                    else if (r0 < 0.128f && TryReserveRockSlot(x, z))
                    {
                        PropKind rk = (groundY > 12f || slope > 0.25f) && cliffBoulderPrefab != null ? PropKind.CliffBoulder : PropKind.Rock;
                        AddPropToChunk(x, z, cSize, 8, rk,
                            new Vector3(wx, groundY, wz), Quaternion.Euler(0f, r1 * 360f, 0f),
                            new Vector3(Mathf.Lerp(0.8f, 1.2f, r1), Mathf.Lerp(0.6f, 1.0f, r2), Mathf.Lerp(0.8f, 1.2f, r3)));
                    }
                    else if (r0 < 0.148f)
                    {
                        float s = Mathf.Lerp(0.82f, 1.26f, r2);
                        AddPropToChunk(x, z, cSize, 0, PropKind.LooseStone,
                            new Vector3(wx, groundY + 0.05f, wz),
                            Quaternion.Euler(r2 * 14f - 7f, r1 * 360f, r3 * 14f - 7f),
                            new Vector3(s, s * 0.78f, s));
                    }
                    else if (r0 < 0.168f)
                    {
                        AddPropToChunk(x, z, cSize, 0, PropKind.LooseBranch,
                            new Vector3(wx, groundY + 0.03f, wz),
                            Quaternion.Euler(r2 * 8f - 4f, r1 * 360f, r3 * 8f - 4f),
                            Vector3.one * Mathf.Lerp(0.85f, 1.25f, r2));
                    }
                }
            }
        }
    }

    private void AddPropToChunk(int gridX, int gridZ, int cSize, int tileID, PropKind kind, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        int cx = Mathf.Clamp(gridX / cSize, 0, chunksX - 1);
        int cz = Mathf.Clamp(gridZ / cSize, 0, chunksZ - 1);
        ChunkData chunk = chunkGrid[cx, cz];

        PlannedProp prop = new PlannedProp
        {
            gridKey = new Vector2Int(gridX, gridZ),
            tileID = tileID,
            kind = kind,
            pos = pos,
            rot = rot,
            scale = scale
        };
        chunk.props.Add(prop);

        if (kind == PropKind.OakTree)
            chunk.oakMats.Add(Matrix4x4.TRS(pos, rot, scale));
        else if (kind == PropKind.PineTree)
            chunk.pineMats.Add(Matrix4x4.TRS(pos, rot, scale));
        else if (kind == PropKind.DeadTree)
            chunk.deadMats.Add(Matrix4x4.TRS(pos, rot, scale));
    }

    private void RebuildChunkTreeMatrices(ChunkData chunk)
    {
        chunk.oakMats.Clear();
        chunk.pineMats.Clear();
        chunk.deadMats.Clear();
        for (int i = 0; i < chunk.props.Count; i++)
        {
            PlannedProp p = chunk.props[i];
            if (harvestedKeys.Contains(p.gridKey)) continue;
            if (p.kind == PropKind.OakTree) chunk.oakMats.Add(Matrix4x4.TRS(p.pos, p.rot, p.scale));
            else if (p.kind == PropKind.PineTree) chunk.pineMats.Add(Matrix4x4.TRS(p.pos, p.rot, p.scale));
            else if (p.kind == PropKind.DeadTree) chunk.deadMats.Add(Matrix4x4.TRS(p.pos, p.rot, p.scale));
        }
        chunk.matricesDirty = false;
    }

    private GameObject SpawnLandmark(GameObject prefab, float wx, float wz, float yawDeg, float scale = 1f, float yOffset = 0f, bool registerActive = true)
    {
        if (prefab == null) return null;
        float gy = TerrainMeshGenerator.GetTerrainHeight(wx, wz) + yOffset;
        GameObject obj = Instantiate(prefab, new Vector3(wx, gy, wz), Quaternion.Euler(0f, yawDeg, 0f), landmarksParent.transform);
        obj.SetActive(true);
        obj.transform.localScale = Vector3.one * scale;
        EnsureValidMaterials(obj);
        if (registerActive)
        {
            activeInstances[new Vector2Int(Mathf.RoundToInt(wx), Mathf.RoundToInt(wz))] = obj;
        }
        return obj;
    }

    private void SpawnLooseStoneCluster(float centerX, float centerZ, (float dx, float dz, int amt)[] offsets)
    {
        for (int i = 0; i < offsets.Length; i++)
        {
            float sx = centerX + offsets[i].dx;
            float sz = centerZ + offsets[i].dz;
            if (FragaWorldBlueprint.GetWaterDepth(sx, sz) > 0.05f) continue;
            float sy = TerrainMeshGenerator.GetTerrainHeight(sx, sz) + 0.06f;
            float s = offsets[i].amt > 1 ? 1.25f : Random.Range(0.88f, 1.12f);
            CreateLooseStonePickup(
                new Vector3(sx, sy, sz),
                Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f)),
                new Vector3(s, s * 0.78f, s),
                offsets[i].amt,
                landmarksParent.transform,
                null);
        }
    }

    private void SpawnLooseBranchCluster(float centerX, float centerZ, (float dx, float dz, int amt)[] offsets)
    {
        for (int i = 0; i < offsets.Length; i++)
        {
            float bx = centerX + offsets[i].dx;
            float bz = centerZ + offsets[i].dz;
            if (FragaWorldBlueprint.GetWaterDepth(bx, bz) > 0.05f) continue;
            float by = TerrainMeshGenerator.GetTerrainHeight(bx, bz) + 0.03f;
            CreateLooseBranchPickup(
                new Vector3(bx, by, bz),
                Quaternion.Euler(Random.Range(-4f, 4f), Random.Range(0f, 360f), Random.Range(-4f, 4f)),
                Vector3.one * (offsets[i].amt > 1 ? 1.18f : Random.Range(0.92f, 1.10f)),
                offsets[i].amt,
                landmarksParent.transform,
                null);
        }
    }

    private void SpawnGlobalLandmarks()
    {
        // 1. Acampamento Inicial (Spawn do Iago)
        Vector2 camp = FragaWorldBlueprint.CrashCampPos;
        SpawnLandmark(crashCampShelterPrefab, camp.x - 4.5f, camp.y + 3.2f, 138f, 1.05f);
        SpawnLandmark(campfirePrefab, camp.x, camp.y + 2.8f, 0f, 1.0f);
        SpawnLandmark(workbenchPrefab, camp.x + 4.2f, camp.y + 1.8f, -35f, 1.0f);
        SpawnLandmark(waterCatcherPrefab, camp.x - 3.8f, camp.y - 3.5f, 25f, 1.0f);
        SpawnLandmark(fallenLogPrefab, camp.x + 2.2f, camp.y + 4.4f, 15f, 1.0f);

        // Pedras soltas visiveis ao redor do acampamento inicial (estilo The Forest - coleta sem ferramenta)
        SpawnLooseStoneCluster(camp.x, camp.y, new (float, float, int)[]
        {
            ( 2.6f, -2.4f, 1),
            ( 3.9f, -3.6f, 2),
            (-2.2f, -4.8f, 1),
            (-5.4f,  0.4f, 1),
            ( 5.8f,  4.2f, 2),
            (-1.6f,  5.6f, 1),
            ( 1.2f, -6.2f, 1),
            ( 6.6f, -1.8f, 1)
        });

        // Galhos e gravetos soltos ao redor do acampamento (coleta sem ferramenta -> Madeira inicial)
        SpawnLooseBranchCluster(camp.x, camp.y, new (float, float, int)[]
        {
            (-2.8f,  2.2f, 1),
            ( 3.4f,  0.8f, 2),
            (-4.2f, -1.6f, 1),
            ( 0.8f, -3.4f, 2),
            ( 5.2f, -4.4f, 1),
            (-1.2f,  4.8f, 1),
            ( 4.0f,  5.6f, 1),
            (-5.8f,  4.2f, 1)
        });

        // Arbustos de bagas e cogumelos proximos ao acampamento para fibra/alimento inicial
        SpawnLandmark(berryPrefab, Mathf.Round(camp.x + 6f), Mathf.Round(camp.y + 3f), 25f, 1.0f);
        SpawnLandmark(berryPrefab, Mathf.Round(camp.x - 7f), Mathf.Round(camp.y - 2f), 140f, 1.0f);
        SpawnLandmark(mushroomPrefab, Mathf.Round(camp.x + 5f), Mathf.Round(camp.y - 5f), 60f, 1.05f);
        SpawnLandmark(mushroomPrefab, Mathf.Round(camp.x - 6f), Mathf.Round(camp.y + 6f), 210f, 1.05f);

        // 2. Farol Celta (Win Condition no Penhasco Sudeste)
        Vector2 farol = FragaWorldBlueprint.FarolBluffPos;
        SpawnLandmark(farolCeltaPrefab, farol.x, farol.y, -40f, 1.15f);
        SpawnLandmark(runicStonePrefab, farol.x - 7f, farol.y + 5f, 30f, 1.0f);
        SpawnLandmark(runicStonePrefab, farol.x + 5f, farol.y + 7f, -20f, 1.0f);

        // 3. Castro Celta (Henge central + 3 Cabanas Castrejas + Cruzes + Fogueira ritual)
        Vector2 castro = FragaWorldBlueprint.CelticCastroPos;
        GameObject castroHenge = SpawnLandmark(stoneCircleHengePrefab, castro.x, castro.y, 12f, 1.05f);
        if (castroHenge != null) AttachRockNode(castroHenge, new Vector2Int(Mathf.RoundToInt(castro.x), Mathf.RoundToInt(castro.y)), 2, true);
        SpawnLandmark(castroRoundhousePrefab, castro.x - 14f, castro.y + 10f, 128f, 1.0f);
        SpawnLandmark(castroRoundhousePrefab, castro.x + 15f, castro.y + 8f, 225f, 1.0f);
        SpawnLandmark(castroRoundhousePrefab, castro.x - 2f, castro.y - 16f, 10f, 1.0f);
        SpawnLandmark(campfirePrefab, castro.x, castro.y, 0f, 1.15f);
        SpawnLandmark(celticCrossPrefab, castro.x - 10f, castro.y - 6f, 45f, 1.1f);
        SpawnLandmark(celticCrossPrefab, castro.x + 10f, castro.y - 7f, -45f, 1.1f);
        SpawnLandmark(ancientGravePrefab, castro.x + 17f, castro.y - 3f, 80f, 1.0f);
        SpawnLandmark(ancientGravePrefab, castro.x - 17f, castro.y - 4f, -80f, 1.0f);

        // 4. Gruta do Anciao (arco rochoso + runas + cogumelos luminosos + pedras soltas)
        Vector2 cave = FragaWorldBlueprint.SacredCavePos;
        GameObject caveArch = SpawnLandmark(caveArchGrottoPrefab, cave.x, cave.y, 145f, 1.05f);
        if (caveArch != null) AttachRockNode(caveArch, new Vector2Int(Mathf.RoundToInt(cave.x), Mathf.RoundToInt(cave.y)), 8, true);
        SpawnLandmark(runicStonePrefab, cave.x + 6f, cave.y - 5f, -35f, 1.05f);
        SpawnLandmark(glowMushroomPrefab, cave.x - 3f, cave.y - 3f, 20f, 1.25f);
        SpawnLandmark(glowMushroomPrefab, cave.x + 3f, cave.y - 4f, 110f, 1.15f);
        SpawnLooseStoneCluster(cave.x, cave.y, new (float, float, int)[]
        {
            (-4.5f, -5.2f, 2),
            ( 2.8f, -6.4f, 1),
            ( 6.4f,  1.2f, 2)
        });
        SpawnLooseBranchCluster(cave.x, cave.y, new (float, float, int)[]
        {
            (-3.2f, -4.6f, 1),
            ( 4.2f, -5.0f, 2),
            (-5.5f,  0.8f, 1)
        });

        // 5. Pico da Serra (+31.8m com altar megalitico e fogueira de vigia)
        Vector2 summit = FragaWorldBlueprint.MountainSummitPos;
        GameObject summitHenge = SpawnLandmark(stoneCircleHengePrefab, summit.x, summit.y, 0f, 0.78f);
        if (summitHenge != null) AttachRockNode(summitHenge, new Vector2Int(Mathf.RoundToInt(summit.x), Mathf.RoundToInt(summit.y)), 2, true);
        SpawnLandmark(campfirePrefab, summit.x, summit.y, 0f, 1.1f);
        SpawnLandmark(celticCrossPrefab, summit.x + 6f, summit.y - 4f, -30f, 1.1f);

        // 6. Lago da Fraga (Pier Oeste de Pesca + Fogueira + pedras na margem)
        Vector2 lake = FragaWorldBlueprint.HighlandLakePos;
        if (woodenDockBridgePrefab != null)
        {
            GameObject pier = Instantiate(woodenDockBridgePrefab,
                new Vector3(lake.x - 38f, FragaWorldBlueprint.LakeLevel - 0.12f, lake.y),
                Quaternion.Euler(0f, 90f, 0f),
                landmarksParent.transform);
            pier.SetActive(true);
            EnsureValidMaterials(pier);
            activeInstances[new Vector2Int(Mathf.RoundToInt(lake.x - 38f), Mathf.RoundToInt(lake.y))] = pier;
        }
        SpawnLandmark(campfirePrefab, lake.x - 46f, lake.y - 4f, 0f, 1.0f);
        SpawnLandmark(fallenLogPrefab, lake.x - 45f, lake.y - 7f, 22f, 1.0f);
        SpawnLooseStoneCluster(lake.x - 46f, lake.y - 4f, new (float, float, int)[]
        {
            ( 2.4f,  2.1f, 1),
            (-2.8f,  3.2f, 2),
            ( 1.8f, -4.5f, 1)
        });
        SpawnLooseBranchCluster(lake.x - 46f, lake.y - 4f, new (float, float, int)[]
        {
            ( 3.4f, -1.6f, 2),
            (-3.6f,  1.4f, 1),
            ( 0.8f,  4.0f, 1)
        });

        // 7. Pontes de Madeira sobre o Rio da Fraga
        for (int b = 0; b < FragaWorldBlueprint.RiverBridges.Length; b++)
        {
            FragaWorldBlueprint.BridgePlacement br = FragaWorldBlueprint.RiverBridges[b];
            FragaWorldBlueprint.SampleRiverInfo(br.Pos.x, br.Pos.y, out _, out float rWaterY);
            if (woodenDockBridgePrefab != null)
            {
                GameObject bridge = Instantiate(woodenDockBridgePrefab,
                    new Vector3(br.Pos.x, rWaterY - 0.05f, br.Pos.y),
                    Quaternion.Euler(0f, br.YawDegrees, 0f),
                    landmarksParent.transform);
                bridge.SetActive(true);
                bridge.transform.localScale = new Vector3(1.18f, 1.05f, 1.35f);
                EnsureValidMaterials(bridge);
                activeInstances[new Vector2Int(Mathf.RoundToInt(br.Pos.x), Mathf.RoundToInt(br.Pos.y))] = bridge;
            }
        }

        // 8. Pantano das Meigas (Totens rituais e luzes espectrais)
        Vector2 swamp = FragaWorldBlueprint.SwampBasinPos;
        SpawnLandmark(swampTotemPrefab, swamp.x + 14f, swamp.y + 10f, 220f, 1.1f);
        SpawnLandmark(swampTotemPrefab, swamp.x - 16f, swamp.y + 6f, 140f, 1.05f);
        SpawnLandmark(swampTotemPrefab, swamp.x + 2f, swamp.y - 18f, 15f, 1.15f);
        SpawnLandmark(glowMushroomPrefab, swamp.x, swamp.y, 0f, 1.3f);

        // 9. Baus de Suprimentos e Notas de Historia (Lore) nos POIs principais
        SpawnPoiCrateAndNote(camp.x + 1.8f, camp.y - 1.6f, "Caixa do Acampamento", new[] { 205, 206, 118, 102, 109 }, 0, "Diario: Dia 12");
        SpawnPoiCrateAndNote(castro.x + 6f, castro.y + 5f, "Arca Celta do Castro", new[] { 107, 106, 120, 206, 116 }, 1, "Diario: Dia 23 (Castro)");
        SpawnPoiCrateAndNote(swamp.x - 4f, swamp.y + 3f, "Relicario do Pantano", new[] { 103, 119, 204, 102, 116 }, 2, "Diario: Dia 34 (Santa Compana)");
        SpawnPoiCrateAndNote(cave.x - 1.5f, cave.y + 2.0f, "Bau da Gruta do Anciao", new[] { 107, 102, 117, 206, 120 }, 3, "Diario: Dia 40 (Gruta)");
        SpawnPoiCrateAndNote(summit.x - 3.5f, summit.y + 2.5f, "Oferenda do Pico", new[] { 107, 106, 204, 213, 116 }, 4, "Diario: Dia 41 (Altar)");
        SpawnPoiCrateAndNote(farol.x + 3.5f, farol.y - 4.0f, "Caixa do Faroleiro", new[] { 102, 205, 107, 206, 109 }, 5, "Notas do Faroleiro");
    }

    private void SpawnPoiCrateAndNote(float wx, float wz, string chestName, int[] items, int loreIdx, string noteTitle)
    {
        if (landmarksParent == null) return;
        float gy = TerrainMeshGenerator.GetTerrainHeight(wx, wz);
        InitFallbackMaterials();
        EnsurePoiMaterials();

        GameObject crate = new GameObject($"LootCrate_{loreIdx}");
        crate.name = $"LootCrate_{loreIdx}";
        crate.transform.SetParent(landmarksParent.transform, false);
        crate.transform.position = new Vector3(wx, gy, wz);
        crate.transform.rotation = Quaternion.Euler(0f, (loreIdx * 37f) % 360f, 0f);

        BoxCollider crateCol = crate.AddComponent<BoxCollider>();
        crateCol.center = new Vector3(0f, 0.32f, 0f);
        crateCol.size = new Vector3(0.98f, 0.64f, 0.70f);

        CreatePoiSubPart("Crate_Body", crate.transform, PrimitiveType.Cube,
            new Vector3(0f, 0.27f, 0f), Vector3.zero, new Vector3(0.92f, 0.52f, 0.64f), s_barkFallback);
        CreatePoiSubPart("Crate_Lid", crate.transform, PrimitiveType.Cube,
            new Vector3(0f, 0.56f, 0f), new Vector3(0f, 0f, -2.5f), new Vector3(0.97f, 0.11f, 0.69f), s_barkFallback);
        CreatePoiSubPart("Crate_IronBandL", crate.transform, PrimitiveType.Cube,
            new Vector3(-0.34f, 0.32f, 0f), Vector3.zero, new Vector3(0.055f, 0.61f, 0.705f), s_poiIronMat);
        CreatePoiSubPart("Crate_IronBandR", crate.transform, PrimitiveType.Cube,
            new Vector3(0.34f, 0.32f, 0f), Vector3.zero, new Vector3(0.055f, 0.61f, 0.705f), s_poiIronMat);
        CreatePoiSubPart("Crate_LockPlate", crate.transform, PrimitiveType.Cube,
            new Vector3(0f, 0.44f, 0.34f), Vector3.zero, new Vector3(0.14f, 0.16f, 0.04f), s_poiIronMat);

        LootContainer lc = crate.AddComponent<LootContainer>();
        lc.containerName = chestName;
        lc.possibleItems = new List<int>(items);
        lc.minItems = 2;
        lc.maxItems = 3;

        float nx = wx + 1.1f;
        float nz = wz + 0.6f;
        float ny = TerrainMeshGenerator.GetTerrainHeight(nx, nz);
        GameObject noteObj = new GameObject($"LoreNote_{loreIdx}");
        noteObj.name = $"LoreNote_{loreIdx}";
        noteObj.transform.SetParent(landmarksParent.transform, false);
        noteObj.transform.position = new Vector3(nx, ny + 0.05f, nz);
        noteObj.transform.rotation = Quaternion.Euler(0f, (loreIdx * 53f) % 360f, 0f);

        BoxCollider noteCol = noteObj.AddComponent<BoxCollider>();
        noteCol.center = new Vector3(0f, 0.06f, 0f);
        noteCol.size = new Vector3(0.48f, 0.16f, 0.62f);

        CreatePoiSubPart("Parchment_Sheet", noteObj.transform, PrimitiveType.Cube,
            new Vector3(0f, 0.03f, 0f), Vector3.zero, new Vector3(0.40f, 0.025f, 0.52f), s_poiParchmentMat);
        CreatePoiSubPart("Parchment_RollTop", noteObj.transform, PrimitiveType.Cylinder,
            new Vector3(0f, 0.055f, 0.25f), new Vector3(0f, 0f, 90f), new Vector3(0.055f, 0.22f, 0.055f), s_poiParchmentMat);
        CreatePoiSubPart("Parchment_RollBottom", noteObj.transform, PrimitiveType.Cylinder,
            new Vector3(0f, 0.055f, -0.25f), new Vector3(0f, 0f, 90f), new Vector3(0.05f, 0.22f, 0.05f), s_poiParchmentMat);
        CreatePoiSubPart("Parchment_WaxSeal", noteObj.transform, PrimitiveType.Cylinder,
            new Vector3(0.08f, 0.048f, -0.10f), Vector3.zero, new Vector3(0.085f, 0.015f, 0.085f), s_poiWaxMat);

        LoreNoteInteractable ln = noteObj.AddComponent<LoreNoteInteractable>();
        ln.loreIndex = loreIdx;
        ln.noteTitle = noteTitle;
    }

    private static void EnsurePoiMaterials()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (s_poiIronMat == null)
        {
            s_poiIronMat = new Material(litShader) { color = new Color(0.20f, 0.21f, 0.24f) };
            s_poiIronMat.SetFloat("_Metallic", 0.72f);
            s_poiIronMat.SetFloat("_Smoothness", 0.45f);
        }
        if (s_poiParchmentMat == null)
        {
            s_poiParchmentMat = new Material(litShader) { color = new Color(0.86f, 0.79f, 0.62f) };
            s_poiParchmentMat.SetFloat("_Smoothness", 0.15f);
        }
        if (s_poiWaxMat == null)
        {
            s_poiWaxMat = new Material(litShader) { color = new Color(0.62f, 0.10f, 0.10f) };
            s_poiWaxMat.SetFloat("_Smoothness", 0.55f);
        }
    }

    private static void CreatePoiSubPart(string partName, Transform parent, PrimitiveType type, Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPos;
        part.transform.localEulerAngles = localEuler;
        part.transform.localScale = localScale;
        Collider c = part.GetComponent<Collider>();
        if (c != null) Destroy(c);
        Renderer r = part.GetComponent<Renderer>();
        if (r != null && mat != null) r.sharedMaterial = mat;
    }

    private void ResolvePlayerTransform()
    {
        if (playerTransformCache != null) return;
        if (PlayerController.Instance != null)
        {
            playerTransformCache = PlayerController.Instance.transform;
            return;
        }
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null && gm.playerTransform != null)
        {
            playerTransformCache = gm.playerTransform;
        }
    }

    private void Update()
    {
        if (!worldPlanned || chunkGrid == null) return;

        ResolvePlayerTransform();
        Vector3 playerPos = playerTransformCache != null
            ? playerTransformCache.position
            : new Vector3(FragaWorldBlueprint.CrashCampPos.x, 0f, FragaWorldBlueprint.CrashCampPos.y);

        int cSize = Mathf.Max(32, chunkSize);
        int pChunkX = Mathf.Clamp(Mathf.FloorToInt(playerPos.x / (cSize * tileSize)), 0, chunksX - 1);
        int pChunkZ = Mathf.Clamp(Mathf.FloorToInt(playerPos.z / (cSize * tileSize)), 0, chunksZ - 1);

        int streamRadius = Application.isMobilePlatform ? 2 : 3;
        int unloadRadius = streamRadius + 1;

        // 1. Descarrega chunks distantes para manter orcamento de memoria/colliders enxuto no mobile
        if (pChunkX != lastPlayerChunk.x || pChunkZ != lastPlayerChunk.y)
        {
            lastPlayerChunk = new Vector2Int(pChunkX, pChunkZ);
            for (int cz = 0; cz < chunksZ; cz++)
            {
                for (int cx = 0; cx < chunksX; cx++)
                {
                    ChunkData cd = chunkGrid[cx, cz];
                    int cheb = Mathf.Max(Mathf.Abs(cx - pChunkX), Mathf.Abs(cz - pChunkZ));
                    if (cd.isLoaded && cheb > unloadRadius)
                    {
                        UnloadChunk(cd);
                    }
                }
            }
        }

        // 2. Carrega chunks proximos de forma progressiva (zero engasgos de frame)
        int spawnBudget = Application.isMobilePlatform ? 18 : 30;
        int spawnedThisFrame = 0;

        for (int ring = 0; ring <= streamRadius && spawnedThisFrame < spawnBudget; ring++)
        {
            for (int dz = -ring; dz <= ring && spawnedThisFrame < spawnBudget; dz++)
            {
                for (int dx = -ring; dx <= ring && spawnedThisFrame < spawnBudget; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring) continue;
                    int nx = pChunkX + dx;
                    int nz = pChunkZ + dz;
                    if (nx < 0 || nx >= chunksX || nz < 0 || nz >= chunksZ) continue;

                    ChunkData cd = chunkGrid[nx, nz];
                    if (!cd.isLoaded || cd.spawnCursor < cd.props.Count)
                    {
                        spawnedThisFrame += StepSpawnChunk(cd, spawnBudget - spawnedThisFrame);
                    }
                }
            }
        }

        // 3. Renderiza arvores de chunks distantes com GPU Instancing (visao ampla ate montanha e farol)
        RenderDistantTreeCanopy(playerPos, pChunkX, pChunkZ);
    }

    private void EnsureChunkRoot(ChunkData cd)
    {
        if (cd.rootObj == null)
        {
            cd.rootObj = new GameObject($"Chunk_{cd.cx}_{cd.cz}");
            cd.rootObj.transform.SetParent(environmentParent.transform, false);
        }
        cd.isLoaded = true;
    }

    private void SpawnAllPropsInChunk(ChunkData cd)
    {
        EnsureChunkRoot(cd);
        while (cd.spawnCursor < cd.props.Count)
        {
            SpawnPlannedProp(cd, cd.props[cd.spawnCursor]);
            cd.spawnCursor++;
        }
    }

    private int StepSpawnChunk(ChunkData cd, int budget)
    {
        EnsureChunkRoot(cd);
        int count = 0;
        while (cd.spawnCursor < cd.props.Count && count < budget)
        {
            SpawnPlannedProp(cd, cd.props[cd.spawnCursor]);
            cd.spawnCursor++;
            count++;
        }
        return count;
    }

    private void SpawnPlannedProp(ChunkData cd, PlannedProp p)
    {
        if (harvestedKeys.Contains(p.gridKey)) return;

        if (p.kind == PropKind.LooseStone)
        {
            int amt = p.scale.x > 1.12f ? 2 : 1;
            if (!TryPopFromPool(PropKind.LooseStone, cd.rootObj.transform, out GameObject stoneInst))
            {
                stoneInst = CreateLooseStonePickup(p.pos, p.rot, p.scale, amt, cd.rootObj.transform, p.gridKey);
            }
            stoneInst.transform.SetPositionAndRotation(p.pos, p.rot);
            stoneInst.transform.localScale = p.scale;
            ResourcePickup sp = stoneInst.GetComponent<ResourcePickup>();
            if (sp != null)
            {
                sp.ResetPickupState(101, amt, true, p.gridKey, launch: false, rotate: false, lifetime: 0f, magnet: 1.35f);
                sp.onRecycle = recycleLooseStoneAction;
            }
            stoneInst.SetActive(true);
            activeInstances[p.gridKey] = stoneInst;
            activePropKinds[p.gridKey] = PropKind.LooseStone;
            return;
        }

        if (p.kind == PropKind.LooseBranch)
        {
            int amt = p.scale.x > 1.12f ? 2 : 1;
            if (!TryPopFromPool(PropKind.LooseBranch, cd.rootObj.transform, out GameObject branchInst))
            {
                branchInst = CreateLooseBranchPickup(p.pos, p.rot, p.scale, amt, cd.rootObj.transform, p.gridKey);
            }
            branchInst.transform.SetPositionAndRotation(p.pos, p.rot);
            branchInst.transform.localScale = p.scale;
            ResourcePickup bp = branchInst.GetComponent<ResourcePickup>();
            if (bp != null)
            {
                bp.ResetPickupState(100, amt, true, p.gridKey, launch: false, rotate: false, lifetime: 0f, magnet: 1.35f);
                bp.onRecycle = recycleLooseBranchAction;
            }
            branchInst.SetActive(true);
            activeInstances[p.gridKey] = branchInst;
            activePropKinds[p.gridKey] = PropKind.LooseBranch;
            return;
        }

        GameObject prefab = ResolvePrefab(p.kind);
        if (prefab == null) return;

        bool fromPool = TryPopFromPool(p.kind, cd.rootObj.transform, out GameObject inst);
        if (!fromPool)
        {
            inst = Instantiate(prefab, p.pos, p.rot, cd.rootObj.transform);
            if (p.kind == PropKind.OakTree || p.kind == PropKind.PineTree || p.kind == PropKind.DeadTree)
            {
                EnsureValidMaterials(inst, s_barkFallback, (p.kind == PropKind.PineTree) ? s_pineFallback : s_foliageFallback);
            }
            else
            {
                EnsureValidMaterials(inst);
            }
        }
        else
        {
            inst.transform.SetPositionAndRotation(p.pos, p.rot);
        }

        inst.transform.localScale = p.scale;
        inst.SetActive(true);

        if (p.kind == PropKind.Rock || p.kind == PropKind.CliffBoulder || p.kind == PropKind.Ruin)
        {
            AttachRockNode(inst, p.gridKey, p.tileID > 0 ? p.tileID : 8, isPermanent: false);
        }

        activeInstances[p.gridKey] = inst;
        activePropKinds[p.gridKey] = p.kind;
    }

    private void UnloadChunk(ChunkData cd)
    {
        for (int i = 0; i < cd.props.Count; i++)
        {
            PlannedProp prop = cd.props[i];
            Vector2Int key = prop.gridKey;
            if (!playerBuiltTiles.ContainsKey(key))
            {
                if (activeInstances.TryGetValue(key, out GameObject inst) && inst != null)
                {
                    ReturnToPool(prop.kind, inst);
                }
                activeInstances.Remove(key);
                activePropKinds.Remove(key);
            }
        }
        if (cd.rootObj != null)
        {
            Destroy(cd.rootObj);
            cd.rootObj = null;
        }
        cd.isLoaded = false;
        cd.spawnCursor = 0;
    }

    private GameObject ResolvePrefab(PropKind kind)
    {
        switch (kind)
        {
            case PropKind.OakTree:      return treePrefab != null ? treePrefab : pineTreePrefab;
            case PropKind.PineTree:     return pineTreePrefab != null ? pineTreePrefab : treePrefab;
            case PropKind.DeadTree:     return deadTreePrefab != null ? deadTreePrefab : treePrefab;
            case PropKind.Fern:         return fentoPrefab;
            case PropKind.ThornyBush:   return thornyBushPrefab;
            case PropKind.FallenLog:    return fallenLogPrefab;
            case PropKind.Rock:         return rockPrefab;
            case PropKind.CliffBoulder: return cliffBoulderPrefab != null ? cliffBoulderPrefab : rockPrefab;
            case PropKind.Berry:        return berryPrefab;
            case PropKind.Mushroom:     return mushroomPrefab;
            case PropKind.GlowMushroom: return glowMushroomPrefab;
            case PropKind.RunicStone:   return runicStonePrefab;
            case PropKind.CelticCross:  return celticCrossPrefab;
            case PropKind.AncientGrave: return ancientGravePrefab;
            case PropKind.Ruin:         return ruinPrefab;
            case PropKind.Pond:         return pondPrefab;
            case PropKind.WaterCatcher: return waterCatcherPrefab;
            case PropKind.Workbench:    return workbenchPrefab;
            case PropKind.Farmland:     return farmlandPrefab;
            case PropKind.Cauldron:     return cauldronPrefab;
            case PropKind.SnareTrap:    return snareTrapPrefab;
            case PropKind.Barricade:    return barricadePrefab;
            case PropKind.Campfire:     return campfirePrefab;
            default:                    return null;
        }
    }

    private void RenderDistantTreeCanopy(Vector3 playerPos, int pChunkX, int pChunkZ)
    {
        float maxDist = Application.isMobilePlatform ? 290f : 460f;
        float maxDistSq = maxDist * maxDist;

        for (int cz = 0; cz < chunksZ; cz++)
        {
            for (int cx = 0; cx < chunksX; cx++)
            {
                ChunkData cd = chunkGrid[cx, cz];
                // Se o chunk esta totalmente instanciado como GameObjects reais, nao duplica no GPU instancing
                if (cd.isLoaded && cd.spawnCursor >= cd.props.Count) continue;

                float dx = cd.center.x - playerPos.x;
                float dz = cd.center.z - playerPos.z;
                if (dx * dx + dz * dz > maxDistSq) continue;

                if (cd.matricesDirty) RebuildChunkTreeMatrices(cd);

                DrawMatrixListBatched(cd.oakMats, oakTrunkMesh, oakTrunkMat);
                DrawMatrixListBatched(cd.oakMats, oakFoliageMesh, oakFoliageMat);
                DrawMatrixListBatched(cd.pineMats, pineTrunkMesh, pineTrunkMat);
                DrawMatrixListBatched(cd.pineMats, pineFoliageMesh, pineFoliageMat);
                DrawMatrixListBatched(cd.deadMats, deadTrunkMesh, deadTrunkMat);
            }
        }
    }

    private void DrawMatrixListBatched(List<Matrix4x4> matrices, Mesh mesh, Material mat)
    {
        if (matrices == null || matrices.Count == 0 || mesh == null || mat == null) return;
        int total = matrices.Count;
        int offset = 0;
        while (offset < total)
        {
            int count = Mathf.Min(1023, total - offset);
            for (int i = 0; i < count; i++)
            {
                instancingBatch[i] = matrices[offset + i];
            }
            Graphics.DrawMeshInstanced(mesh, 0, mat, instancingBatch, count, null, ShadowCastingMode.Off, true);
            offset += count;
        }
    }

    private void MarkChunkMatricesDirtyForKey(Vector2Int key)
    {
        if (chunkGrid == null) return;
        int cSize = Mathf.Max(32, chunkSize);
        int cx = Mathf.Clamp(key.x / cSize, 0, chunksX - 1);
        int cz = Mathf.Clamp(key.y / cSize, 0, chunksZ - 1);
        chunkGrid[cx, cz].matricesDirty = true;
    }

    public void MarkPropHarvested(Vector2Int key)
    {
        harvestedKeys.Add(key);
        activeInstances.Remove(key);
        activePropKinds.Remove(key);
    }

    public void RemoveInstanceAt(int gridX, int gridY, int tileID)
    {
        Vector2Int key = new Vector2Int(gridX, gridY);
        harvestedKeys.Add(key);
        playerBuiltTiles.Remove(key);
        MarkChunkMatricesDirtyForKey(key);

        if (activeInstances.TryGetValue(key, out GameObject obj))
        {
            if (obj != null) Destroy(obj);
            activeInstances.Remove(key);
        }
        activePropKinds.Remove(key);
    }

    public void AddInstanceAt(int gridX, int gridY, int tileID)
    {
        if (tileID <= 0) return;
        InstantiateTile(gridX, gridY, tileID);
    }

    public void InstantiateTile(int gridX, int gridY, int tileID)
    {
        Vector2Int key = new Vector2Int(gridX, gridY);
        harvestedKeys.Remove(key);
        playerBuiltTiles[key] = tileID;

        if (activeInstances.TryGetValue(key, out GameObject existing) && existing != null)
        {
            Destroy(existing);
            activeInstances.Remove(key);
        }
        activePropKinds.Remove(key);

        Vector3 pos = new Vector3(gridX * tileSize, TerrainMeshGenerator.GetTerrainHeight(gridX * tileSize, gridY * tileSize), gridY * tileSize);
        Quaternion rot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        Transform parent = environmentParent != null ? environmentParent.transform : transform;
        GameObject inst = null;

        if (tileID == 1 && treePrefab != null)
        {
            GameObject chosenTree = treePrefab;
            float roll = Random.value;
            if (roll > 0.90f && deadTreePrefab != null) chosenTree = deadTreePrefab;
            else if (roll > 0.70f && pineTreePrefab != null) chosenTree = pineTreePrefab;

            if (chosenTree != null)
            {
                inst = Instantiate(chosenTree, pos, rot, parent);
                inst.SetActive(true);
                EnsureValidMaterials(inst, s_barkFallback, (chosenTree == pineTreePrefab) ? s_pineFallback : s_foliageFallback);
            }
        }
        else if (tileID == 2 && ruinPrefab != null) inst = Instantiate(ruinPrefab, pos, rot, parent);
        else if (tileID == 8 && rockPrefab != null) inst = Instantiate(rockPrefab, pos, rot, parent);
        else if (tileID == 3 && barricadePrefab != null) inst = Instantiate(barricadePrefab, pos, rot, parent);
        else if (tileID == 4 && campfirePrefab != null) inst = Instantiate(campfirePrefab, pos, rot, parent);
        else if (tileID == 9 && berryPrefab != null) inst = Instantiate(berryPrefab, pos, rot, parent);
        else if (tileID == 10 && mushroomPrefab != null) inst = Instantiate(mushroomPrefab, pos, rot, parent);
        else if (tileID == 11 && pondPrefab != null) inst = Instantiate(pondPrefab, pos, rot, parent);
        else if (tileID == 12 && waterCatcherPrefab != null) inst = Instantiate(waterCatcherPrefab, pos, rot, parent);
        else if (tileID == 13 && workbenchPrefab != null) inst = Instantiate(workbenchPrefab, pos, rot, parent);
        else if (tileID == 14 && farmlandPrefab != null) inst = Instantiate(farmlandPrefab, pos, rot, parent);
        else if (tileID == 15 && cauldronPrefab != null) inst = Instantiate(cauldronPrefab, pos, rot, parent);
        else if (tileID == 16 && snareTrapPrefab != null) inst = Instantiate(snareTrapPrefab, pos, rot, parent);
        else if (tileID == 17 && runicStonePrefab != null) inst = Instantiate(runicStonePrefab, pos, rot, parent);
        else if (tileID == 18 && fallenLogPrefab != null) inst = Instantiate(fallenLogPrefab, pos, rot, parent);
        else if (tileID == 19 && thornyBushPrefab != null) inst = Instantiate(thornyBushPrefab, pos, rot, parent);
        else if (tileID == 20 && glowMushroomPrefab != null) inst = Instantiate(glowMushroomPrefab, pos, rot, parent);
        else if (tileID == 21 && celticCrossPrefab != null) inst = Instantiate(celticCrossPrefab, pos, rot, parent);
        else if (tileID == 22 && ancientGravePrefab != null) inst = Instantiate(ancientGravePrefab, pos, rot, parent);

        if (inst != null)
        {
            inst.SetActive(true);
            EnsureValidMaterials(inst);
            if (tileID == 2 || tileID == 8)
            {
                AttachRockNode(inst, key, tileID, isPermanent: false);
            }
            activeInstances[key] = inst;
        }
    }

    public bool TryHarvestTreeAt(int gridX, int gridY)
    {
        Vector2Int key = new Vector2Int(gridX, gridY);
        GameObject obj = null;

        if (!activeInstances.TryGetValue(key, out obj) || obj == null)
        {
            float closestDist = float.MaxValue;
            Vector2Int bestKey = key;
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    Vector2Int testKey = new Vector2Int(gridX + dx, gridY + dy);
                    if (activeInstances.TryGetValue(testKey, out GameObject candidate) && candidate != null)
                    {
                        float d = Vector2.Distance(new Vector2(gridX, gridY), new Vector2(testKey.x, testKey.y));
                        if (d <= 2.5f && d < closestDist)
                        {
                            string lower = candidate.name.ToLower();
                            if (lower.Contains("tree") || lower.Contains("pine"))
                            {
                                closestDist = d;
                                bestKey = testKey;
                                obj = candidate;
                            }
                        }
                    }
                }
            }
            if (obj != null) key = bestKey;
        }

        if (obj != null)
        {
            if (!treeHitCount.ContainsKey(key)) treeHitCount[key] = 0;
            treeHitCount[key]++;

            if (ScreenShakeSystem.Instance != null)
            {
                ScreenShakeSystem.Instance.AddTrauma(0.12f);
            }

            obj.transform.localScale = obj.transform.localScale * Random.Range(0.92f, 0.98f);

            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.PlayBuildPlaceVFX(obj.transform.position + Vector3.up * 1.2f);
            }

            if (treeHitCount[key] >= 3)
            {
                GameManager gm = FindAnyObjectByType<GameManager>();
                if (gm != null) gm.UpdateMapCellNative(key.x, key.y, 0);

                if (VFXPoolManager.Instance != null)
                    VFXPoolManager.Instance.PlayDeathVFX(new Vector3(key.x, TerrainMeshGenerator.GetTerrainHeight(key.x, key.y) + 0.5f, key.y));
                if (ScreenShakeSystem.Instance != null)
                    ScreenShakeSystem.Instance.AddTrauma(0.38f);

                GameObject woodDrop = Resources.Load<GameObject>("Prefabs/DroppedWood_Prefab");
                if (woodDrop == null) woodDrop = Resources.Load<GameObject>("Prefabs/FallenLog_Prefab");
                if (woodDrop != null)
                {
                    Vector3 dropPos = new Vector3(key.x, TerrainMeshGenerator.GetTerrainHeight(key.x, key.y) + 0.15f, key.y);
                    Instantiate(woodDrop, dropPos, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
                }

                ProceduralGrassSystem grass = FindAnyObjectByType<ProceduralGrassSystem>();
                if (grass != null) grass.RefreshGrassAt(key.x, key.y, 2.5f);

                harvestedKeys.Add(key);
                MarkChunkMatricesDirtyForKey(key);
                if (obj != null) Destroy(obj);
                activeInstances.Remove(key);
                activePropKinds.Remove(key);
                treeHitCount.Remove(key);
                return true;
            }
        }
        return false;
    }

    public bool TryHarvestRockAt(int gridX, int gridY)
    {
        Vector2Int key = new Vector2Int(gridX, gridY);
        GameObject obj = null;

        if (!activeInstances.TryGetValue(key, out obj) || obj == null)
        {
            float closestDist = float.MaxValue;
            Vector2Int bestKey = key;
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    Vector2Int testKey = new Vector2Int(gridX + dx, gridY + dy);
                    if (activeInstances.TryGetValue(testKey, out GameObject candidate) && candidate != null)
                    {
                        float d = Vector2.Distance(new Vector2(gridX, gridY), new Vector2(testKey.x, testKey.y));
                        if (d <= 2.5f && d < closestDist)
                        {
                            string lower = candidate.name.ToLower();
                            if (lower.Contains("rock") || lower.Contains("boulder"))
                            {
                                closestDist = d;
                                bestKey = testKey;
                                obj = candidate;
                            }
                        }
                    }
                }
            }
            if (obj != null) key = bestKey;
        }

        if (obj != null)
        {
            if (!rockHitCount.ContainsKey(key)) rockHitCount[key] = 0;
            rockHitCount[key]++;

            if (ScreenShakeSystem.Instance != null) ScreenShakeSystem.Instance.AddTrauma(0.15f);
            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayBuildPlaceVFX(obj.transform.position + Vector3.up * 0.8f);

            obj.transform.localScale = obj.transform.localScale * Random.Range(0.95f, 1.04f);

            if (rockHitCount[key] >= 5)
            {
                GameManager gm = FindAnyObjectByType<GameManager>();
                if (gm != null) gm.UpdateMapCellNative(key.x, key.y, 0);

                if (VFXPoolManager.Instance != null)
                    VFXPoolManager.Instance.PlayDeathVFX(new Vector3(key.x, TerrainMeshGenerator.GetTerrainHeight(key.x, key.y) + 0.5f, key.y));
                if (ScreenShakeSystem.Instance != null)
                    ScreenShakeSystem.Instance.AddTrauma(0.45f);

                harvestedKeys.Add(key);
                if (obj != null) Destroy(obj);
                activeInstances.Remove(key);
                activePropKinds.Remove(key);
                rockHitCount.Remove(key);
                return true;
            }
        }
        return false;
    }

    public int TryHarvestPlantAt(int gridX, int gridY)
    {
        Vector2Int key = new Vector2Int(gridX, gridY);

        if (activeInstances.TryGetValue(key, out GameObject obj))
        {
            int tileID = ResourceManager.GetMapCell(mapDataPtr, mapWidth, mapHeight, gridX, gridY);
            if (tileID != 9 && tileID != 10 && obj != null)
            {
                string lower = obj.name.ToLower();
                if (lower.Contains("berry")) tileID = 9;
                else if (lower.Contains("mushroom") || lower.Contains("fento") || lower.Contains("fern")) tileID = 10;
            }
            if (tileID == 9 || tileID == 10)
            {
                GameManager gm = FindAnyObjectByType<GameManager>();
                if (gm != null) gm.UpdateMapCellNative(gridX, gridY, 0);

                if (VFXPoolManager.Instance != null)
                    VFXPoolManager.Instance.PlayDeathVFX(new Vector3(gridX, TerrainMeshGenerator.GetTerrainHeight(gridX, gridY) + 0.5f, gridY));

                harvestedKeys.Add(key);
                if (obj != null) Destroy(obj);
                activeInstances.Remove(key);
                activePropKinds.Remove(key);
                return tileID;
            }
        }
        return 0;
    }
}
