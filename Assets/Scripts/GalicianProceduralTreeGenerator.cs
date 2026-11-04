using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gerador Procedural de Árvores Atlânticas AAA (Padrão The Forest / Sons of the Forest para Fraga Game).
/// Gera Carvalhos Centenários e Pinheiros Bravos com contrafortes radiculares profundos,
/// bacias de ramos mestres horizontais, e nuvens volumétricas de folhagem com prateleiras horizontais
/// e normais esféricas para iluminação suave e copas frondosas sob o sol.
/// </summary>
public class GalicianProceduralTreeGenerator : MonoBehaviour
{
    public enum TreeType { GalicianOak, MaritimePine, DeadTree }

    [Header("Configurações Botânicas")]
    public TreeType type = TreeType.GalicianOak;
    public float height = 14.5f;
    public float baseRadius = 0.60f;
    public float topRadius = 0.14f;
    public int radialSegments = 10;
    public int heightSegments = 15;

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
            gen.height = 17.5f;
            gen.baseRadius = 0.50f;
            gen.topRadius = 0.10f;
        }
        else if (treeType == TreeType.DeadTree)
        {
            gen.height = 11.0f;
            gen.baseRadius = 0.55f;
            gen.topRadius = 0.12f;
        }
        else // GalicianOak
        {
            gen.height = 14.0f;
            gen.baseRadius = 0.65f;
            gen.topRadius = 0.16f;
        }

        gen.BuildTreeHierarchy(treeRoot);
        DestroyImmediate(gen);
        return treeRoot;
    }

    public void BuildTreeHierarchy(GameObject root)
    {
        // 1. Gerar Tronco com Contrafortes Radiculares e Galhos Mestres
        Mesh trunkMesh = GenerateTrunkMesh();
        GameObject trunkObj = new GameObject("Trunk");
        trunkObj.transform.SetParent(root.transform, false);
        MeshFilter trunkMF = trunkObj.AddComponent<MeshFilter>();
        trunkMF.sharedMesh = trunkMesh;
        MeshRenderer trunkMR = trunkObj.AddComponent<MeshRenderer>();
        trunkMR.sharedMaterial = barkMaterial != null ? barkMaterial : Resources.Load<Material>("Materials/Mat_DarkForest_Bark");

        // 2. Gerar Folhagem Volumétrica Exuberante (se não for árvore morta)
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
                folMat = (type == TreeType.MaritimePine) ?
                    Resources.Load<Material>("Materials/Mat_PineNeedle") :
                    Resources.Load<Material>("Materials/Mat_OakFoliage");
            }
            foliageMR.sharedMaterial = folMat;
        }

        // 3. Colisor Exclusivo no Tronco (Zero sobrecarga de colisão nas folhas para 60 FPS mobile)
        CapsuleCollider col = root.AddComponent<CapsuleCollider>();
        col.radius = baseRadius * 0.75f;
        col.height = height * 0.88f;
        col.center = new Vector3(0, height * 0.44f, 0);
    }

    public Mesh GenerateTrunkMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Trunk_" + type.ToString();

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        for (int y = 0; y <= heightSegments; y++)
        {
            float v = (float)y / heightSegments;
            float currentHeight = v * height;
            
            // Afunilamento suave estrutural
            float radius = Mathf.Lerp(baseRadius, topRadius, Mathf.Pow(v, 1.15f));

            // Contrafortes radiculares na base (Buttress roots para abraçar o solo)
            if (v < 0.25f)
            {
                float rootFlare = Mathf.Exp(-v * 12f) * 0.65f;
                radius += rootFlare;
            }

            // Curvatura orgânica sinuosa
            float curveX = (type == TreeType.MaritimePine) ? Mathf.Sin(v * 1.6f) * 0.18f : Mathf.Sin(v * 2.2f) * 0.42f;
            float curveZ = (type == TreeType.MaritimePine) ? Mathf.Cos(v * 1.8f) * 0.15f : Mathf.Cos(v * 1.9f) * 0.36f;

            for (int r = 0; r < radialSegments; r++)
            {
                float u = (float)r / radialSegments;
                float angle = u * Mathf.PI * 2f;

                // Ondulação de casca e sapopemas
                float flareFactor = 1f;
                if (v < 0.20f)
                {
                    // 4 raízes proeminentes nas diagonais
                    flareFactor += Mathf.Cos(angle * 4f) * 0.22f * (1f - v / 0.20f);
                }
                float noise = Mathf.PerlinNoise(Mathf.Cos(angle) * 2f + 10f, v * 3.5f) * 0.08f;
                float finalRadius = radius * (flareFactor + noise);

                Vector3 pos = new Vector3(
                    Mathf.Cos(angle) * finalRadius + curveX,
                    currentHeight,
                    Mathf.Sin(angle) * finalRadius + curveZ
                );

                vertices.Add(pos);
                normals.Add(new Vector3(Mathf.Cos(angle), 0.04f, Mathf.Sin(angle)).normalized);
                uvs.Add(new Vector2(u * 3f, v * (height * 0.45f)));
            }
        }

        // Triângulos do tronco
        for (int y = 0; y < heightSegments; y++)
        {
            for (int r = 0; r < radialSegments; r++)
            {
                int current = y * radialSegments + r;
                int next = current + radialSegments;
                int rNext = (r + 1) % radialSegments;
                int currentRNext = y * radialSegments + rNext;
                int nextRNext = (y + 1) * radialSegments + rNext;

                triangles.Add(current);
                triangles.Add(next);
                triangles.Add(currentRNext);

                triangles.Add(currentRNext);
                triangles.Add(next);
                triangles.Add(nextRNext);
            }
        }

        // Ramos mestres estruturais para suporte visual da copa
        if (type == TreeType.GalicianOak)
        {
            AddMainOakBranches(vertices, normals, uvs, triangles);
        }
        else if (type == TreeType.MaritimePine)
        {
            AddPineBranches(vertices, normals, uvs, triangles);
        }

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private void AddMainOakBranches(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris)
    {
        // 6 grandes ramos mestres espalhados horizontalmente criando a coroa larga
        float[] branchAngles = { 30f, 95f, 155f, 215f, 275f, 335f };
        float[] branchHeights = { 0.48f, 0.55f, 0.50f, 0.62f, 0.58f, 0.65f };
        float[] branchSpreads = { 4.2f, 3.8f, 4.4f, 3.9f, 4.1f, 3.7f };

        for (int b = 0; b < branchAngles.Length; b++)
        {
            float angRad = branchAngles[b] * Mathf.Deg2Rad;
            float branchStartH = height * branchHeights[b];
            Vector3 startPos = new Vector3(Mathf.Cos(angRad) * 0.45f, branchStartH, Mathf.Sin(angRad) * 0.45f);
            
            // Vetor diretor estendendo-se para fora e para cima
            Vector3 dir = new Vector3(
                Mathf.Cos(angRad) * branchSpreads[b],
                2.2f + (b % 2) * 0.6f,
                Mathf.Sin(angRad) * branchSpreads[b]
            );

            int startIdx = verts.Count;
            int branchSegments = 6;
            int steps = 4;

            for (int step = 0; step <= steps; step++)
            {
                float t = (float)step / steps;
                // Curvatura com leve caimento na ponta
                Vector3 center = startPos + dir * t + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.5f - t * t * 0.4f);
                float bRad = Mathf.Lerp(0.26f, 0.08f, t);

                for (int s = 0; s < branchSegments; s++)
                {
                    float u = (float)s / branchSegments;
                    float a = u * Mathf.PI * 2f;
                    Vector3 offset = new Vector3(Mathf.Cos(a) * bRad, Mathf.Sin(a) * bRad * 0.7f, 0);
                    Quaternion q = Quaternion.LookRotation(dir);
                    Vector3 pos = center + q * offset;

                    verts.Add(pos);
                    norms.Add((q * offset).normalized);
                    uvs.Add(new Vector2(u * 1.5f, t * 2f));
                }
            }

            for (int step = 0; step < steps; step++)
            {
                for (int s = 0; s < branchSegments; s++)
                {
                    int curr = startIdx + step * branchSegments + s;
                    int next = curr + branchSegments;
                    int sNext = (s + 1) % branchSegments;
                    int currNext = startIdx + step * branchSegments + sNext;
                    int nextNext = startIdx + (step + 1) * branchSegments + sNext;

                    tris.Add(curr); tris.Add(next); tris.Add(currNext);
                    tris.Add(currNext); tris.Add(next); tris.Add(nextNext);
                }
            }
        }
    }

    private void AddPineBranches(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris)
    {
        // Ramos horizontais típicos de Pinheiro-Bravo
        int whorls = 4;
        for (int w = 0; w < whorls; w++)
        {
            float whorlH = height * Mathf.Lerp(0.50f, 0.85f, (float)w / whorls);
            int branchCount = 4;
            float baseAngle = w * 45f;

            for (int b = 0; b < branchCount; b++)
            {
                float ang = (baseAngle + b * 90f) * Mathf.Deg2Rad;
                Vector3 startPos = new Vector3(Mathf.Cos(ang) * 0.28f, whorlH, Mathf.Sin(ang) * 0.28f);
                float spread = Mathf.Lerp(3.2f, 1.8f, (float)w / whorls);
                Vector3 dir = new Vector3(Mathf.Cos(ang) * spread, 0.4f, Mathf.Sin(ang) * spread);

                int startIdx = verts.Count;
                int branchSegments = 5;
                for (int step = 0; step <= 2; step++)
                {
                    float t = (float)step / 2f;
                    Vector3 center = startPos + dir * t;
                    float bRad = Mathf.Lerp(0.12f, 0.04f, t);

                    for (int s = 0; s < branchSegments; s++)
                    {
                        float u = (float)s / branchSegments;
                        float a = u * Mathf.PI * 2f;
                        Vector3 offset = new Vector3(Mathf.Cos(a) * bRad, Mathf.Sin(a) * bRad, 0);
                        Quaternion q = Quaternion.LookRotation(dir);
                        Vector3 pos = center + q * offset;

                        verts.Add(pos);
                        norms.Add((q * offset).normalized);
                        uvs.Add(new Vector2(u, t));
                    }
                }

                for (int s = 0; s < branchSegments; s++)
                {
                    int curr = startIdx + s;
                    int next = curr + branchSegments;
                    int sNext = (s + 1) % branchSegments;
                    int currNext = startIdx + sNext;
                    int nextNext = startIdx + branchSegments + sNext;

                    tris.Add(curr); tris.Add(next); tris.Add(currNext);
                    tris.Add(currNext); tris.Add(next); tris.Add(nextNext);
                }
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
        else // MaritimePine
        {
            GeneratePineCanopy(canopyCenter, vertices, normals, colors, uvs, triangles);
        }

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Gera a copa exuberante, volumétrica e ampla de Carvalho Centenário Galego (Quercus robur / Carballo).
    /// Copa em cúpula com largura de 7.0 a 9.5 metros, cobrindo o céu com folhas douradas/verdes.
    /// </summary>
    private void GenerateOakCanopy(Vector3 canopyCenter, List<Vector3> verts, List<Vector3> norms, List<Color> colors, List<Vector2> uvs, List<int> tris)
    {
        // Nuvens de folhagem distribuídas nos 6 ramos mestres + coroa central superior
        float[] branchAngles = { 30f, 95f, 155f, 215f, 275f, 335f };
        float[] branchHeights = { 0.48f, 0.55f, 0.50f, 0.62f, 0.58f, 0.65f };
        float[] branchSpreads = { 4.2f, 3.8f, 4.4f, 3.9f, 4.1f, 3.7f };

        // 1. Tufos ao longo e nas pontas dos ramos
        for (int b = 0; b < branchAngles.Length; b++)
        {
            float angRad = branchAngles[b] * Mathf.Deg2Rad;
            float branchStartH = height * branchHeights[b];
            Vector3 bDir = new Vector3(Mathf.Cos(angRad), 0.35f, Mathf.Sin(angRad)).normalized;

            // 4 a 5 nuvens de folhas por ramo
            for (int step = 1; step <= 4; step++)
            {
                float t = (float)step / 4f;
                float dist = branchSpreads[b] * t;
                Vector3 clumpPos = new Vector3(
                    Mathf.Cos(angRad) * dist + Mathf.Sin(step * 3.7f) * 0.8f,
                    branchStartH + t * 2.8f + Mathf.Cos(step * 2.1f) * 0.5f,
                    Mathf.Sin(angRad) * dist + Mathf.Cos(step * 3.7f) * 0.8f
                );

                float cardSize = Mathf.Lerp(3.2f, 3.8f, t);
                AddVolumetricFoliageCluster(clumpPos, cardSize, canopyCenter, verts, norms, colors, uvs, tris, false);
            }
        }

        // 2. Coroa central e cúpula superior fechando o teto verde
        int domeCount = 14;
        float goldenAngle = 137.5077f * Mathf.Deg2Rad;
        for (int i = 0; i < domeCount; i++)
        {
            float t = (float)i / domeCount;
            float h = Mathf.Lerp(height * 0.70f, height * 1.04f, t);
            float rad = Mathf.Lerp(3.8f, 0.8f, Mathf.Pow(t, 0.75f));
            float a = i * goldenAngle;

            Vector3 clumpPos = new Vector3(Mathf.Cos(a) * rad, h, Mathf.Sin(a) * rad);
            float cardSize = Mathf.Lerp(3.6f, 2.8f, t);
            AddVolumetricFoliageCluster(clumpPos, cardSize, canopyCenter, verts, norms, colors, uvs, tris, false);
        }
    }

    /// <summary>
    /// Gera a copa cônica escalonada de Pinheiro-Bravo com prateleiras horizontais densas de agulhas.
    /// </summary>
    private void GeneratePineCanopy(Vector3 canopyCenter, List<Vector3> verts, List<Vector3> norms, List<Color> colors, List<Vector2> uvs, List<int> tris)
    {
        int tiers = 6;
        for (int tier = 0; tier < tiers; tier++)
        {
            float t = (float)tier / tiers;
            float tierH = Mathf.Lerp(height * 0.46f, height * 0.94f, t);
            float spread = Mathf.Lerp(4.0f, 1.2f, Mathf.Pow(t, 0.85f));
            int clustersInTier = Mathf.RoundToInt(Mathf.Lerp(6, 3, t));

            for (int c = 0; c < clustersInTier; c++)
            {
                float ang = ((float)c / clustersInTier * 360f + tier * 32f) * Mathf.Deg2Rad;
                Vector3 clumpPos = new Vector3(
                    Mathf.Cos(ang) * spread,
                    tierH + ((c % 2) - 0.5f) * 0.35f,
                    Mathf.Sin(ang) * spread
                );

                float cardSize = Mathf.Lerp(3.2f, 2.2f, t);
                AddVolumetricFoliageCluster(clumpPos, cardSize, canopyCenter, verts, norms, colors, uvs, tris, true);
            }
        }

        // Ponteiro / topo cônico
        Vector3 apexPos = new Vector3(0, height * 0.98f, 0);
        AddVolumetricFoliageCluster(apexPos, 2.2f, canopyCenter, verts, norms, colors, uvs, tris, true);
    }

    /// <summary>
    /// Adiciona um tufo volumétrico AAA (The Forest Standard):
    /// - 2 Prateleiras Horizontais (com inclinação sutil de 12° a 18°) para criar o teto denso de folhas visto de baixo
    /// - 2 Quads Diagonais (inclinados a 45°) para dar espessura na visão horizontal
    /// - 1 Quad Cruzado Íngreme (75°) para volume interior
    /// - Normais Esféricas Híbridas (suavidade contínua e SSS iluminado pelo sol)
    /// - Double-Sided com faces invertidas
    /// </summary>
    private void AddVolumetricFoliageCluster(Vector3 center, float size, Vector3 canopyCenter,
        List<Vector3> verts, List<Vector3> norms, List<Color> colors, List<Vector2> uvs, List<int> tris, bool isPine)
    {
        float halfW = size * 0.50f;
        float halfL = size * 0.52f;

        // Configuração de 5 cartões volumétricos cobrindo todos os ângulos visuais
        // (Yaw, Pitch, Roll)
        Vector3[] cardRotations = new Vector3[]
        {
            new Vector3(12f, 0f, 8f),       // Prateleira horizontal primária (captura sol do zênite)
            new Vector3(-14f, 65f, -10f),   // Prateleira horizontal secundária cruzada
            new Vector3(42f, 30f, 0f),      // Quad diagonal frontal
            new Vector3(45f, 120f, 0f),     // Quad diagonal lateral
            new Vector3(72f, 80f, 0f)       // Quad vertical com leve inclinação
        };

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

            // NORMAIS ESFÉRICAS HÍBRIDAS: 50% centro da copa (canopy) + 50% centro do tufo (clump)
            // Isso garante que cada folha pareça parte de um volume denso tridimensional com iluminação contínua
            Vector3 n0 = Vector3.Normalize((v0 - canopyCenter) * 0.55f + (v0 - center) * 0.45f);
            Vector3 n1 = Vector3.Normalize((v1 - canopyCenter) * 0.55f + (v1 - center) * 0.45f);
            Vector3 n2 = Vector3.Normalize((v2 - canopyCenter) * 0.55f + (v2 - center) * 0.45f);
            Vector3 n3 = Vector3.Normalize((v3 - canopyCenter) * 0.55f + (v3 - center) * 0.45f);

            norms.Add(n0);
            norms.Add(n1);
            norms.Add(n2);
            norms.Add(n3);

            // Cores de vértice:
            // R = intensidade do vento na ponta
            // G = fase local de flutter
            // B = oclusão de ambiente (mais escuro no interior, mais claro na periferia)
            float distFromCenter = (center - canopyCenter).magnitude;
            float windWeight = Mathf.Clamp01(distFromCenter / (height * 0.45f));
            float ao = Mathf.Clamp01(0.55f + 0.45f * (distFromCenter / (height * 0.4f)));
            Color vColor = new Color(windWeight, Random.value, ao, 1.0f);

            colors.Add(vColor);
            colors.Add(vColor);
            colors.Add(vColor);
            colors.Add(vColor);

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 1f));

            // Face frontal (Lado de cima)
            tris.Add(startIndex + 0);
            tris.Add(startIndex + 2);
            tris.Add(startIndex + 1);

            tris.Add(startIndex + 0);
            tris.Add(startIndex + 3);
            tris.Add(startIndex + 2);

            // Face traseira (Double-sided visto de baixo)
            tris.Add(startIndex + 0);
            tris.Add(startIndex + 1);
            tris.Add(startIndex + 2);

            tris.Add(startIndex + 0);
            tris.Add(startIndex + 2);
            tris.Add(startIndex + 3);
        }
    }
}
