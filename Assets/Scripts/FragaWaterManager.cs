using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// FragaWaterManager: Gera e gerencia os corpos d'água autoritativos da ilha:
/// 1. Oceano Atlântico (saia oceânica ao redor da ilha, y = SeaLevel)
/// 2. Lago da Fraga (lago de altitude na serra, y = LakeLevel)
/// 3. Rio da Fraga (fitas contínuas da nascente serrana -> Lago -> Enseada Sul)
/// Utiliza o shader GalicianWater_Lit com ondas, Fresnel, reflexo de luzes e espuma na orla.
/// </summary>
public class FragaWaterManager : MonoBehaviour
{
    public static FragaWaterManager Instance { get; private set; }

    [Header("Water Visuals")]
    public Material oceanMaterial;
    public Material freshwaterMaterial;

    private GameObject oceanObj;
    private GameObject lakeObj;
    private GameObject riverObj;
    private bool isBuilt = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (!isBuilt)
        {
            FragaWorldBlueprint.EnsureInitialized();
            BuildWater(FragaWorldBlueprint.Width, FragaWorldBlueprint.Height);
        }
    }

    private Material GetWaterMaterial(bool isOcean)
    {
        if (isOcean && oceanMaterial != null) return oceanMaterial;
        if (!isOcean && freshwaterMaterial != null) return freshwaterMaterial;

        Material baseMat = Resources.Load<Material>("Materials/Water_Surface");
        if (baseMat != null)
        {
            Material mat = new Material(baseMat);
            if (isOcean)
            {
                mat.SetColor("_DeepWaterColor", new Color(0.015f, 0.065f, 0.11f, 0.96f));
                mat.SetColor("_ShallowWaterColor", new Color(0.06f, 0.21f, 0.27f, 0.78f));
                mat.SetFloat("_WaveHeight", 0.065f);
                mat.SetFloat("_WaveScale", 0.95f);
                mat.SetFloat("_WaveSpeed", 0.95f);
            }
            else
            {
                mat.SetColor("_DeepWaterColor", new Color(0.03f, 0.11f, 0.14f, 0.90f));
                mat.SetColor("_ShallowWaterColor", new Color(0.10f, 0.28f, 0.28f, 0.68f));
                mat.SetFloat("_WaveHeight", 0.022f);
                mat.SetFloat("_WaveScale", 2.4f);
                mat.SetFloat("_WaveSpeed", 1.15f);
            }
            return mat;
        }

        Shader waterShader = Shader.Find("Fraga/GalicianWater_Lit");
        if (waterShader == null) waterShader = Shader.Find("Universal Render Pipeline/Lit");
        Material fallback = new Material(waterShader);
        fallback.color = isOcean ? new Color(0.04f, 0.16f, 0.24f, 0.88f) : new Color(0.08f, 0.24f, 0.26f, 0.78f);
        return fallback;
    }

    public void BuildWater(int worldWidth, int worldHeight)
    {
        FragaWorldBlueprint.EnsureInitialized();

        if (oceanObj != null) Destroy(oceanObj);
        if (lakeObj != null) Destroy(lakeObj);
        if (riverObj != null) Destroy(riverObj);

        oceanMaterial = GetWaterMaterial(true);
        freshwaterMaterial = GetWaterMaterial(false);

        BuildOceanMesh(worldWidth, worldHeight);
        BuildLakeMesh();
        BuildRiverMesh();

        isBuilt = true;
    }

    private void BuildOceanMesh(int worldWidth, int worldHeight)
    {
        oceanObj = new GameObject("Ocean_Atlantic_Skirt");
        oceanObj.transform.SetParent(transform, false);

        MeshFilter mf = oceanObj.AddComponent<MeshFilter>();
        MeshRenderer mr = oceanObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = oceanMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = true;

        float margin = 460f;
        float minX = -margin;
        float minZ = -margin;
        float totalSpanX = worldWidth + margin * 2f;
        float totalSpanZ = worldHeight + margin * 2f;

        int gridRes = Application.isMobilePlatform ? 96 : 120;
        int vertCount = (gridRes + 1) * (gridRes + 1);
        Vector3[] verts = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        float[] coastVals = new float[vertCount];

        int idx = 0;
        for (int z = 0; z <= gridRes; z++)
        {
            float wz = minZ + (z / (float)gridRes) * totalSpanZ;
            for (int x = 0; x <= gridRes; x++)
            {
                float wx = minX + (x / (float)gridRes) * totalSpanX;
                verts[idx] = new Vector3(wx, FragaWorldBlueprint.SeaLevel, wz);
                normals[idx] = Vector3.up;

                float coast = FragaWorldBlueprint.GetIslandCoastMetric(wx, wz);
                coastVals[idx] = coast;

                // Ativa espuma costeira sutil somente perto da linha de praia
                if (coast >= 0.90f && coast <= 1.02f)
                    uvs[idx] = new Vector2(0.045f, 0.5f);
                else
                    uvs[idx] = new Vector2(0.5f, 0.5f);

                idx++;
            }
        }

        var tris = new List<int>(gridRes * gridRes * 6);
        int rowStride = gridRes + 1;

        for (int z = 0; z < gridRes; z++)
        {
            for (int x = 0; x < gridRes; x++)
            {
                int i0 = z * rowStride + x;
                int i1 = i0 + 1;
                int i2 = i0 + rowStride;
                int i3 = i2 + 1;

                // Descarta apenas quadrados estritamente secos no interior da ilha
                bool anyCoastalOrSea =
                    coastVals[i0] >= 0.85f ||
                    coastVals[i1] >= 0.85f ||
                    coastVals[i2] >= 0.85f ||
                    coastVals[i3] >= 0.85f;

                if (!anyCoastalOrSea) continue;

                tris.Add(i0); tris.Add(i2); tris.Add(i1);
                tris.Add(i1); tris.Add(i2); tris.Add(i3);
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "Atlantic_Ocean_Mesh";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;
    }

    private void BuildLakeMesh()
    {
        lakeObj = new GameObject("Lago_da_Fraga_Surface");
        lakeObj.transform.SetParent(transform, false);

        MeshFilter mf = lakeObj.AddComponent<MeshFilter>();
        MeshRenderer mr = lakeObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = freshwaterMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = true;

        Vector2 center = FragaWorldBlueprint.HighlandLakePos;
        float waterY = FragaWorldBlueprint.LakeLevel;
        float outerRadius = 43.5f;
        int rings = 7;
        int segs = 44;

        var verts = new List<Vector3>(1 + rings * segs);
        var normals = new List<Vector3>(1 + rings * segs);
        var uvs = new List<Vector2>(1 + rings * segs);
        var tris = new List<int>(segs * rings * 6);

        verts.Add(new Vector3(center.x, waterY, center.y));
        normals.Add(Vector3.up);
        uvs.Add(new Vector2(0.5f, 0.5f));

        for (int r = 1; r <= rings; r++)
        {
            float frac = r / (float)rings;
            float rad = outerRadius * frac;
            float edgeUv = (frac >= 0.84f) ? Mathf.Lerp(0.18f, 0.025f, (frac - 0.84f) / 0.16f) : 0.5f;

            for (int s = 0; s < segs; s++)
            {
                float ang = (s / (float)segs) * Mathf.PI * 2f;
                float vx = center.x + Mathf.Cos(ang) * rad;
                float vz = center.y + Mathf.Sin(ang) * rad;
                verts.Add(new Vector3(vx, waterY, vz));
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(edgeUv, 0.5f));
            }
        }

        // Anel central (triangle fan)
        for (int s = 0; s < segs; s++)
        {
            int next = (s + 1) % segs;
            tris.Add(0);
            tris.Add(1 + next);
            tris.Add(1 + s);
        }

        // Anéis externos
        for (int r = 1; r < rings; r++)
        {
            int ringStart = 1 + (r - 1) * segs;
            int nextRingStart = 1 + r * segs;
            for (int s = 0; s < segs; s++)
            {
                int nextS = (s + 1) % segs;
                int a = ringStart + s;
                int b = ringStart + nextS;
                int c = nextRingStart + s;
                int d = nextRingStart + nextS;

                tris.Add(a); tris.Add(b); tris.Add(d);
                tris.Add(a); tris.Add(d); tris.Add(c);
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "Highland_Lake_Mesh";
        mesh.vertices = verts.ToArray();
        mesh.normals = normals.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;
    }

    private void BuildRiverMesh()
    {
        int count = FragaWorldBlueprint.GetRiverSampleCount();
        if (count < 2) return;

        riverObj = new GameObject("Rio_da_Fraga_Ribbon");
        riverObj.transform.SetParent(transform, false);

        MeshFilter mf = riverObj.AddComponent<MeshFilter>();
        MeshRenderer mr = riverObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = freshwaterMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = true;

        var verts = new List<Vector3>(count * 3);
        var normals = new List<Vector3>(count * 3);
        var uvs = new List<Vector2>(count * 3);
        var tris = new List<int>(count * 12);

        Vector2 prevPos = Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            FragaWorldBlueprint.GetRiverSample(i, out Vector2 pos, out float halfWidth, out float waterY);
            Vector2 nextPos = pos;
            if (i < count - 1)
                FragaWorldBlueprint.GetRiverSample(i + 1, out nextPos, out _, out _);

            Vector2 tangent = (i == 0) ? (nextPos - pos) : (i == count - 1 ? (pos - prevPos) : (nextPos - prevPos));
            if (tangent.sqrMagnitude < 0.0001f) tangent = Vector2.down;
            tangent.Normalize();
            Vector2 perp = new Vector2(-tangent.y, tangent.x);

            float visualHalfW = halfWidth * 1.28f;
            float clampedY = Mathf.Max(FragaWorldBlueprint.SeaLevel - 0.02f, waterY - 0.02f);

            // Se estiver bem dentro do Lago ou no oceano profundo, empurra levemente para baixo evitando Z-fighting
            float dLake = Vector2.Distance(pos, FragaWorldBlueprint.HighlandLakePos);
            if (dLake < 34f) clampedY = FragaWorldBlueprint.LakeLevel - 0.06f;
            if (FragaWorldBlueprint.GetIslandCoastMetric(pos.x, pos.y) > 1.005f) clampedY = FragaWorldBlueprint.SeaLevel - 0.08f;

            Vector2 left = pos - perp * visualHalfW;
            Vector2 right = pos + perp * visualHalfW;
            float vCoord = (i / (float)(count - 1)) * 8f;

            verts.Add(new Vector3(left.x, clampedY, left.y));
            verts.Add(new Vector3(pos.x, clampedY, pos.y));
            verts.Add(new Vector3(right.x, clampedY, right.y));

            normals.Add(Vector3.up);
            normals.Add(Vector3.up);
            normals.Add(Vector3.up);

            // UV.x próximo das bordas [0.025 .. 0.975] cria espuma sutil nas margens do rio
            uvs.Add(new Vector2(0.025f, vCoord));
            uvs.Add(new Vector2(0.500f, vCoord));
            uvs.Add(new Vector2(0.975f, vCoord));

            if (i > 0)
            {
                int prevRow = (i - 1) * 3;
                int curRow = i * 3;

                tris.Add(prevRow + 0); tris.Add(curRow + 0); tris.Add(curRow + 1);
                tris.Add(prevRow + 0); tris.Add(curRow + 1); tris.Add(prevRow + 1);

                tris.Add(prevRow + 1); tris.Add(curRow + 1); tris.Add(curRow + 2);
                tris.Add(prevRow + 1); tris.Add(curRow + 2); tris.Add(prevRow + 2);
            }

            prevPos = pos;
        }

        Mesh mesh = new Mesh();
        mesh.name = "River_Ribbon_Mesh";
        mesh.vertices = verts.ToArray();
        mesh.normals = normals.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;
    }
}
