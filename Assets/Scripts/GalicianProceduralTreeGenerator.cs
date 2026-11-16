using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gerador Procedural de Árvores Atlânticas AAA (Padrão The Forest / Sons of the Forest para Fraga Game).
/// Gera Carvalhos Centenários, Pinheiros Bravos e Árvores Mortas com raízes tubulares ancoradas ao terreno,
/// ramos mestres bifurcados, tangentes calculadas para Normal Mapping PBR, e nuvens volumétricas
/// de folhagem com prateleiras horizontais, Oclusão de Ambiente nos vértices (AO) e normais esféricas híbridas.
/// </summary>
public class GalicianProceduralTreeGenerator : MonoBehaviour
{
    public enum TreeType { GalicianOak, MaritimePine, DeadTree }

    [Header("Configurações Botânicas")]
    public TreeType type = TreeType.GalicianOak;
    public float height = 14.5f;
    public float baseRadius = 0.60f;
    public float topRadius = 0.14f;
    public int radialSegments = 12;
    public int heightSegments = 16;

    [Header("Materiais")]
    public Material barkMaterial;
    public Material foliageMaterial;

    public static GameObject CreateTreePrefabObject(TreeType treeType, Material barkMat, Material foliageMat)
    {
        string prefabName = treeType == TreeType.MaritimePine ? "PineTree_Prefab" : (treeType == TreeType.DeadTree ? "DeadTree_Prefab" : "Tree_Prefab");
        GameObject treeRoot = new GameObject(prefabName);

        GalicianProceduralTreeGenerator gen = treeRoot.AddComponent<GalicianProceduralTreeGenerator>();
        gen.type = treeType;
        gen.barkMaterial = barkMat;
        gen.foliageMaterial = foliageMat;

        if (treeType == TreeType.MaritimePine)
        {
            gen.height = 18.0f;
            gen.baseRadius = 0.48f;
            gen.topRadius = 0.09f;
            gen.radialSegments = 12;
            gen.heightSegments = 16;
        }
        else if (treeType == TreeType.DeadTree)
        {
            gen.height = 11.5f;
            gen.baseRadius = 0.52f;
            gen.topRadius = 0.11f;
            gen.radialSegments = 10;
            gen.heightSegments = 14;
        }
        else // GalicianOak
        {
            gen.height = 14.2f;
            gen.baseRadius = 0.66f;
            gen.topRadius = 0.16f;
            gen.radialSegments = 12;
            gen.heightSegments = 16;
        }

        gen.BuildTreeHierarchy(treeRoot);
        DestroyImmediate(gen);
        return treeRoot;
    }

    public void BuildTreeHierarchy(GameObject root)
    {
        Mesh trunkMesh = GenerateTrunkMesh();
        GameObject trunkObj = new GameObject("Trunk");
        trunkObj.transform.SetParent(root.transform, false);
        MeshFilter trunkMF = trunkObj.AddComponent<MeshFilter>();
        trunkMF.sharedMesh = trunkMesh;
        MeshRenderer trunkMR = trunkObj.AddComponent<MeshRenderer>();
        trunkMR.sharedMaterial = barkMaterial != null ? barkMaterial : Resources.Load<Material>("Materials/Mat_DarkForest_Bark");

        if (type != TreeType.DeadTree)
        {
            Mesh foliageMesh = GenerateFoliageMesh();
            GameObject foliageObj = new GameObject("Foliage");
            foliageObj.transform.SetParent(root.transform, false);
            MeshFilter foliageMF = foliageObj.AddComponent<MeshFilter>();
            foliageMF.sharedMesh = foliageMesh;
            MeshRenderer foliageMR = foliageObj.AddComponent<MeshRenderer>();

            Material folMat = foliageMaterial;
            if (folMat == null)
            {
                folMat = (type == TreeType.MaritimePine)
                    ? Resources.Load<Material>("Materials/Mat_PineNeedle")
                    : Resources.Load<Material>("Materials/Mat_OakFoliage");
            }
            foliageMR.sharedMaterial = folMat;
        }

        CapsuleCollider col = root.AddComponent<CapsuleCollider>();
        col.radius = baseRadius * 0.72f;
        col.height = height * 0.85f;
        col.center = new Vector3(0, height * 0.42f, 0);
    }

    public Mesh GenerateTrunkMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Trunk_" + type.ToString();

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        float seedShift = (type == TreeType.MaritimePine) ? 23.4f : (type == TreeType.DeadTree ? 57.1f : 10.0f);

        for (int y = 0; y <= heightSegments; y++)
        {
            float v = (float)y / heightSegments;
            // Começa ligeiramente abaixo de zero (-0.25m) para ancorar em encostas sem flutuar
            float currentHeight = Mathf.Lerp(-0.25f, height, v);

            float radius = Mathf.Lerp(baseRadius, topRadius, Mathf.Pow(v, 1.12f));
            if (v < 0.24f)
            {
                float rootFlare = Mathf.Exp(-v * 11f) * (baseRadius * 0.85f);
                radius += rootFlare;
            }

            float curveX = (type == TreeType.MaritimePine) ? Mathf.Sin(v * 1.6f) * 0.16f : Mathf.Sin(v * 2.3f) * 0.46f;
            float curveZ = (type == TreeType.MaritimePine) ? Mathf.Cos(v * 1.8f) * 0.14f : Mathf.Cos(v * 2.0f) * 0.38f;
            if (type == TreeType.DeadTree)
            {
                curveX = Mathf.Sin(v * 3.1f) * 0.58f;
                curveZ = Mathf.Cos(v * 2.7f) * 0.44f;
            }

            // Duplicar último vértice em u=1.0 para evitar costura de UV!
            for (int r = 0; r <= radialSegments; r++)
            {
                float u = (float)r / radialSegments;
                float angle = u * Mathf.PI * 2f;

                float flareFactor = 1f;
                if (v < 0.22f)
                {
                    flareFactor += Mathf.Cos(angle * 4f + 0.4f) * 0.24f * (1f - v / 0.22f);
                }
                float noise = (Mathf.PerlinNoise(Mathf.Cos(angle) * 2.2f + seedShift, v * 4.0f) - 0.5f) * 0.14f;
                float finalRadius = radius * (flareFactor + noise);

                Vector3 pos = new Vector3(
                    Mathf.Cos(angle) * finalRadius + curveX,
                    currentHeight,
                    Mathf.Sin(angle) * finalRadius + curveZ
                );

                vertices.Add(pos);
                normals.Add(new Vector3(Mathf.Cos(angle), 0.05f, Mathf.Sin(angle)).normalized);
                uvs.Add(new Vector2(u * 2.5f, v * (height * 0.42f)));
            }
        }

        int ringStride = radialSegments + 1;
        for (int y = 0; y < heightSegments; y++)
        {
            for (int r = 0; r < radialSegments; r++)
            {
                int current = y * ringStride + r;
                int next = current + ringStride;
                int currentRNext = current + 1;
                int nextRNext = next + 1;

                triangles.Add(current);
                triangles.Add(next);
                triangles.Add(currentRNext);

                triangles.Add(currentRNext);
                triangles.Add(next);
                triangles.Add(nextRNext);
            }
        }

        // Raízes tubulares expostas cravadas no solo
        AddButtressRoots(vertices, normals, uvs, triangles);

        if (type == TreeType.GalicianOak)
        {
            AddMainOakBranches(vertices, normals, uvs, triangles);
        }
        else if (type == TreeType.MaritimePine)
        {
            AddPineBranches(vertices, normals, uvs, triangles);
        }
        else if (type == TreeType.DeadTree)
        {
            AddDeadBranches(vertices, normals, uvs, triangles);
        }

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void AddButtressRoots(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris)
    {
        int rootCount = (type == TreeType.MaritimePine) ? 4 : 5;
        for (int i = 0; i < rootCount; i++)
        {
            float ang = (i * (360f / rootCount) + 18f) * Mathf.Deg2Rad;
            Vector3 p0 = new Vector3(Mathf.Cos(ang) * baseRadius * 0.55f, 1.05f, Mathf.Sin(ang) * baseRadius * 0.55f);
            Vector3 p1 = new Vector3(Mathf.Cos(ang) * baseRadius * 1.45f, 0.25f, Mathf.Sin(ang) * baseRadius * 1.45f);
            Vector3 p2 = new Vector3(Mathf.Cos(ang + 0.14f) * baseRadius * 2.35f, -0.35f, Mathf.Sin(ang + 0.14f) * baseRadius * 2.35f);
            AddCurvedTube(new Vector3[] { p0, p1, p2 }, new float[] { baseRadius * 0.36f, baseRadius * 0.22f, 0.06f }, 6, verts, norms, uvs, tris);
        }
    }

    private void AddMainOakBranches(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris)
    {
        float[] branchAngles = { 24f, 88f, 148f, 208f, 272f, 332f };
        float[] branchHeights = { 0.44f, 0.52f, 0.48f, 0.58f, 0.54f, 0.64f };
        float[] branchSpreads = { 4.3f, 3.9f, 4.5f, 4.0f, 4.2f, 3.8f };

        for (int b = 0; b < branchAngles.Length; b++)
        {
            float angRad = branchAngles[b] * Mathf.Deg2Rad;
            float v = branchHeights[b];
            float trunkOffsetX = Mathf.Sin(v * 2.3f) * 0.46f;
            float trunkOffsetZ = Mathf.Cos(v * 2.0f) * 0.38f;
            Vector3 startPos = new Vector3(trunkOffsetX + Mathf.Cos(angRad) * 0.32f, height * v, trunkOffsetZ + Mathf.Sin(angRad) * 0.32f);

            float spread = branchSpreads[b];
            Vector3 midPos = startPos + new Vector3(Mathf.Cos(angRad) * spread * 0.52f, 1.35f + (b % 2) * 0.35f, Mathf.Sin(angRad) * spread * 0.52f);
            Vector3 endPos = startPos + new Vector3(Mathf.Cos(angRad) * spread, 2.25f + (b % 2) * 0.5f, Mathf.Sin(angRad) * spread);

            AddCurvedTube(new Vector3[] { startPos, midPos, endPos }, new float[] { 0.28f, 0.17f, 0.07f }, 7, verts, norms, uvs, tris);

            // Bifurcação secundária para silhueta natural de carballo
            for (int f = -1; f <= 1; f += 2)
            {
                float subAng = angRad + f * 0.55f;
                Vector3 subEnd = midPos + new Vector3(Mathf.Cos(subAng) * 2.1f, 1.15f, Mathf.Sin(subAng) * 2.1f);
                AddCurvedTube(new Vector3[] { midPos, (midPos + subEnd) * 0.5f + Vector3.up * 0.2f, subEnd }, new float[] { 0.14f, 0.09f, 0.04f }, 5, verts, norms, uvs, tris);
            }
        }
    }

    private void AddPineBranches(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris)
    {
        int whorls = 5;
        for (int w = 0; w < whorls; w++)
        {
            float tW = (float)w / (whorls - 1);
            float whorlH = height * Mathf.Lerp(0.45f, 0.88f, tW);
            int branchCount = 4;
            float baseAngle = w * 43f;

            for (int b = 0; b < branchCount; b++)
            {
                float ang = (baseAngle + b * 90f) * Mathf.Deg2Rad;
                Vector3 startPos = new Vector3(Mathf.Cos(ang) * 0.22f, whorlH, Mathf.Sin(ang) * 0.22f);
                float spread = Mathf.Lerp(3.5f, 1.6f, tW);
                Vector3 midPos = startPos + new Vector3(Mathf.Cos(ang) * spread * 0.55f, -0.15f, Mathf.Sin(ang) * spread * 0.55f);
                Vector3 endPos = startPos + new Vector3(Mathf.Cos(ang) * spread, 0.28f, Mathf.Sin(ang) * spread);

                AddCurvedTube(new Vector3[] { startPos, midPos, endPos }, new float[] { 0.12f, 0.07f, 0.03f }, 5, verts, norms, uvs, tris);
            }
        }
    }

    private void AddDeadBranches(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris)
    {
        float[] angles = { 18f, 92f, 168f, 245f, 312f };
        float[] heights = { 0.42f, 0.54f, 0.65f, 0.74f, 0.85f };
        for (int i = 0; i < angles.Length; i++)
        {
            float ang = angles[i] * Mathf.Deg2Rad;
            float v = heights[i];
            Vector3 p0 = new Vector3(Mathf.Sin(v * 3.1f) * 0.58f, height * v, Mathf.Cos(v * 2.7f) * 0.44f);
            Vector3 p1 = p0 + new Vector3(Mathf.Cos(ang) * 1.4f, 0.75f, Mathf.Sin(ang) * 1.4f);
            Vector3 p2 = p1 + new Vector3(Mathf.Cos(ang + 0.35f) * 1.1f, 0.35f - (i % 2) * 0.45f, Mathf.Sin(ang + 0.35f) * 1.1f);
            AddCurvedTube(new Vector3[] { p0, p1, p2 }, new float[] { 0.19f, 0.10f, 0.03f }, 6, verts, norms, uvs, tris);
        }
    }

    private void AddCurvedTube(Vector3[] points, float[] radii, int sides,
        List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris)
    {
        int startIdx = verts.Count;
        int ringStride = sides + 1;

        for (int p = 0; p < points.Length; p++)
        {
            Vector3 dir = (p < points.Length - 1) ? (points[p + 1] - points[p]) : (points[p] - points[p - 1]);
            if (dir.sqrMagnitude < 1e-5f) dir = Vector3.up;
            Quaternion q = Quaternion.LookRotation(dir.normalized);
            float t = (float)p / (points.Length - 1);

            for (int s = 0; s <= sides; s++)
            {
                float u = (float)s / sides;
                float a = u * Mathf.PI * 2f;
                Vector3 localOffset = new Vector3(Mathf.Cos(a) * radii[p], Mathf.Sin(a) * radii[p], 0f);
                Vector3 worldOffset = q * localOffset;
                verts.Add(points[p] + worldOffset);
                norms.Add(worldOffset.normalized);
                uvs.Add(new Vector2(u * 1.5f, t * 2.2f));
            }
        }

        for (int p = 0; p < points.Length - 1; p++)
        {
            for (int s = 0; s < sides; s++)
            {
                int curr = startIdx + p * ringStride + s;
                int next = curr + ringStride;
                tris.Add(curr); tris.Add(next); tris.Add(curr + 1);
                tris.Add(curr + 1); tris.Add(next); tris.Add(next + 1);
            }
        }
    }

    public Mesh GenerateFoliageMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Foliage_" + type.ToString();

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Color> colors = new List<Color>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        Vector3 canopyCenter = new Vector3(0, height * 0.72f, 0);

        if (type == TreeType.GalicianOak)
        {
            GenerateOakCanopy(canopyCenter, vertices, normals, colors, uvs, triangles);
        }
        else
        {
            GeneratePineCanopy(canopyCenter, vertices, normals, colors, uvs, triangles);
        }

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void GenerateOakCanopy(Vector3 canopyCenter, List<Vector3> verts, List<Vector3> norms, List<Color> colors, List<Vector2> uvs, List<int> tris)
    {
        float[] branchAngles = { 24f, 88f, 148f, 208f, 272f, 332f };
        float[] branchHeights = { 0.44f, 0.52f, 0.48f, 0.58f, 0.54f, 0.64f };
        float[] branchSpreads = { 4.3f, 3.9f, 4.5f, 4.0f, 4.2f, 3.8f };

        for (int b = 0; b < branchAngles.Length; b++)
        {
            float angRad = branchAngles[b] * Mathf.Deg2Rad;
            float branchStartH = height * branchHeights[b];

            for (int step = 2; step <= 4; step++)
            {
                float t = (float)step / 4f;
                float dist = branchSpreads[b] * t;
                Vector3 clumpPos = new Vector3(
                    Mathf.Cos(angRad) * dist + Mathf.Sin(step * 3.7f + b) * 0.7f,
                    branchStartH + t * 2.5f + Mathf.Cos(step * 2.1f) * 0.45f,
                    Mathf.Sin(angRad) * dist + Mathf.Cos(step * 3.7f + b) * 0.7f
                );

                float cardSize = Mathf.Lerp(3.3f, 3.9f, t);
                AddVolumetricFoliageCluster(clumpPos, cardSize, canopyCenter, verts, norms, colors, uvs, tris);
            }
        }

        int domeCount = 16;
        float goldenAngle = 137.5077f * Mathf.Deg2Rad;
        for (int i = 0; i < domeCount; i++)
        {
            float t = (float)i / domeCount;
            float h = Mathf.Lerp(height * 0.68f, height * 1.05f, t);
            float rad = Mathf.Lerp(4.0f, 0.7f, Mathf.Pow(t, 0.75f));
            float a = i * goldenAngle;

            Vector3 clumpPos = new Vector3(Mathf.Cos(a) * rad, h, Mathf.Sin(a) * rad);
            float cardSize = Mathf.Lerp(3.8f, 2.9f, t);
            AddVolumetricFoliageCluster(clumpPos, cardSize, canopyCenter, verts, norms, colors, uvs, tris);
        }
    }

    private void GeneratePineCanopy(Vector3 canopyCenter, List<Vector3> verts, List<Vector3> norms, List<Color> colors, List<Vector2> uvs, List<int> tris)
    {
        int tiers = 7;
        for (int tier = 0; tier < tiers; tier++)
        {
            float t = (float)tier / (tiers - 1);
            float tierH = Mathf.Lerp(height * 0.44f, height * 0.95f, t);
            float spread = Mathf.Lerp(4.1f, 1.0f, Mathf.Pow(t, 0.85f));
            int clustersInTier = Mathf.RoundToInt(Mathf.Lerp(7, 3, t));

            for (int c = 0; c < clustersInTier; c++)
            {
                float ang = ((float)c / clustersInTier * 360f + tier * 34f) * Mathf.Deg2Rad;
                Vector3 clumpPos = new Vector3(
                    Mathf.Cos(ang) * spread,
                    tierH + ((c % 2) - 0.5f) * 0.30f,
                    Mathf.Sin(ang) * spread
                );

                float cardSize = Mathf.Lerp(3.3f, 2.1f, t);
                AddVolumetricFoliageCluster(clumpPos, cardSize, canopyCenter, verts, norms, colors, uvs, tris);
            }
        }

        Vector3 apexPos = new Vector3(0, height * 0.99f, 0);
        AddVolumetricFoliageCluster(apexPos, 2.2f, canopyCenter, verts, norms, colors, uvs, tris);
    }

    private void AddVolumetricFoliageCluster(Vector3 center, float size, Vector3 canopyCenter,
        List<Vector3> verts, List<Vector3> norms, List<Color> colors, List<Vector2> uvs, List<int> tris)
    {
        float halfW = size * 0.50f;
        float halfL = size * 0.52f;

        Vector3[] cardRotations = new Vector3[]
        {
            new Vector3(12f, 0f, 8f),
            new Vector3(-15f, 62f, -9f),
            new Vector3(40f, 28f, 0f),
            new Vector3(44f, 118f, 0f),
            new Vector3(72f, 75f, 0f),
            new Vector3(-38f, 165f, 6f)
        };

        float distFromCenter = (center - canopyCenter).magnitude;
        float windWeight = Mathf.Clamp01(distFromCenter / (height * 0.42f));
        float ao = Mathf.Clamp01(0.48f + 0.52f * (distFromCenter / (height * 0.38f)));
        float clusterPhase = Random.value;

        for (int c = 0; c < cardRotations.Length; c++)
        {
            Quaternion rot = Quaternion.Euler(cardRotations[c]);
            Vector3 right = rot * Vector3.right * halfW;
            Vector3 forward = rot * Vector3.forward * halfL;

            int startIndex = verts.Count;

            Vector3 v0 = center - right - forward;
            Vector3 v1 = center + right - forward;
            Vector3 v2 = center + right + forward;
            Vector3 v3 = center - right + forward;

            verts.Add(v0);
            verts.Add(v1);
            verts.Add(v2);
            verts.Add(v3);

            norms.Add(Vector3.Normalize((v0 - canopyCenter) * 0.55f + (v0 - center) * 0.45f));
            norms.Add(Vector3.Normalize((v1 - canopyCenter) * 0.55f + (v1 - center) * 0.45f));
            norms.Add(Vector3.Normalize((v2 - canopyCenter) * 0.55f + (v2 - center) * 0.45f));
            norms.Add(Vector3.Normalize((v3 - canopyCenter) * 0.55f + (v3 - center) * 0.45f));

            Color vColor = new Color(windWeight, clusterPhase, ao, 1.0f);
            colors.Add(vColor);
            colors.Add(vColor);
            colors.Add(vColor);
            colors.Add(vColor);

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 1f));

            tris.Add(startIndex + 0); tris.Add(startIndex + 2); tris.Add(startIndex + 1);
            tris.Add(startIndex + 0); tris.Add(startIndex + 3); tris.Add(startIndex + 2);

            tris.Add(startIndex + 0); tris.Add(startIndex + 1); tris.Add(startIndex + 2);
            tris.Add(startIndex + 0); tris.Add(startIndex + 2); tris.Add(startIndex + 3);
        }
    }
}
