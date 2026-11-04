using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Construtor de Malha Paramétrico do Machado de Sobrevivência Tradicional da Fraga.
/// Substitui os cilindros primitivos por um modelo anatômico de alta qualidade AAA (Padrão The Forest).
/// </summary>
public static class ProceduralSurvivalAxeGenerator
{
    public static GameObject BuildAxeGameObject(Material woodMaterial = null, Material steelMaterial = null, Material leatherMaterial = null)
    {
        if (woodMaterial == null) woodMaterial = Resources.Load<Material>("Materials/Wood_Bark");
        if (woodMaterial == null) woodMaterial = Resources.Load<Material>("Materials/Mat_DarkForest_Bark");

        if (steelMaterial == null) steelMaterial = Resources.Load<Material>("Materials/Mat_Ruins_Iron");
        if (leatherMaterial == null) leatherMaterial = Resources.Load<Material>("Materials/Celtic_Stone");

        GameObject axeRoot = new GameObject("Axe_Prefab");

        // 1. Cabo Ergonômico de Madeira de Freixo (Fawn-Foot Curvature)
        GameObject handleObj = new GameObject("Axe_Handle");
        handleObj.transform.SetParent(axeRoot.transform, false);
        MeshFilter handleMF = handleObj.AddComponent<MeshFilter>();
        handleMF.sharedMesh = GenerateHandleMesh();
        MeshRenderer handleMR = handleObj.AddComponent<MeshRenderer>();
        handleMR.sharedMaterial = woodMaterial;

        // 2. Cabeça de Ferro Forjado com Lâmina Biselada
        GameObject headObj = new GameObject("Axe_Head");
        headObj.transform.SetParent(axeRoot.transform, false);
        headObj.transform.localPosition = new Vector3(0, 0.42f, 0.045f);
        MeshFilter headMF = headObj.AddComponent<MeshFilter>();
        headMF.sharedMesh = GenerateAxeHeadMesh();
        MeshRenderer headMR = headObj.AddComponent<MeshRenderer>();
        headMR.sharedMaterial = steelMaterial;

        // 3. Amarras de Couro Cru Trançado na União
        GameObject wrapObj = new GameObject("Leather_Wraps");
        wrapObj.transform.SetParent(axeRoot.transform, false);
        wrapObj.transform.localPosition = new Vector3(0, 0.38f, 0.015f);
        MeshFilter wrapMF = wrapObj.AddComponent<MeshFilter>();
        wrapMF.sharedMesh = GenerateWrapsMesh();
        MeshRenderer wrapMR = wrapObj.AddComponent<MeshRenderer>();
        wrapMR.sharedMaterial = leatherMaterial;

        // Colisor para registro de golpes
        BoxCollider box = axeRoot.AddComponent<BoxCollider>();
        box.center = new Vector3(0, 0.28f, 0.05f);
        box.size = new Vector3(0.08f, 0.65f, 0.25f);
        box.isTrigger = true;

        return axeRoot;
    }

    private static Mesh GenerateHandleMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "AxeHandle_Ergonomic";

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        int heightSteps = 16;
        int radialSteps = 8;
        float totalLength = 0.58f;

        for (int i = 0; i <= heightSteps; i++)
        {
            float t = (float)i / heightSteps;
            float y = (t - 0.25f) * totalLength; // Centralizado no ponto de apoio

            // Curvatura fawn-foot sutil
            float offsetZ = Mathf.Sin(t * Mathf.PI * 0.95f) * 0.024f - (Mathf.Pow(1f - t, 2.2f) * 0.035f);
            float radiusX = Mathf.Lerp(0.014f, 0.018f, Mathf.Sin(t * Mathf.PI)) * 0.85f;
            float radiusZ = Mathf.Lerp(0.022f, 0.028f, Mathf.Sin(t * Mathf.PI));

            // Alargamento da ponta da base para empunhadura firme
            if (t < 0.15f)
            {
                float knob = (0.15f - t) * 0.08f;
                radiusX += knob * 0.5f;
                radiusZ += knob;
            }

            for (int r = 0; r < radialSteps; r++)
            {
                float angle = (float)r / radialSteps * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radiusX;
                float z = Mathf.Sin(angle) * radiusZ + offsetZ;

                verts.Add(new Vector3(x, y, z));
                norms.Add(new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)).normalized);
                uvs.Add(new Vector2((float)r / radialSteps, t * 2.5f));
            }
        }

        for (int i = 0; i < heightSteps; i++)
        {
            for (int r = 0; r < radialSteps; r++)
            {
                int curr = i * radialSteps + r;
                int next = curr + radialSteps;
                int rNext = (r + 1) % radialSteps;
                int currR = i * radialSteps + rNext;
                int nextR = (i + 1) * radialSteps + rNext;

                tris.Add(curr); tris.Add(next); tris.Add(currR);
                tris.Add(currR); tris.Add(next); tris.Add(nextR);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh GenerateAxeHeadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "AxeHead_ForgedSteel";

        // Vértices da cabeça do machado (Olho traseiro -> Corpo -> Fio de corte curvo)
        Vector3[] vertices = new Vector3[]
        {
            // 0..3: Fundo do martelo (Poll traseiro)
            new Vector3(-0.022f, -0.038f, -0.045f),
            new Vector3( 0.022f, -0.038f, -0.045f),
            new Vector3( 0.022f,  0.038f, -0.045f),
            new Vector3(-0.022f,  0.038f, -0.045f),

            // 4..7: Olho central do cabo
            new Vector3(-0.025f, -0.045f, 0.015f),
            new Vector3( 0.025f, -0.045f, 0.015f),
            new Vector3( 0.025f,  0.045f, 0.015f),
            new Vector3(-0.025f,  0.045f, 0.015f),

            // 8..11: Corpo da lâmina chanfrada
            new Vector3(-0.012f, -0.065f, 0.115f),
            new Vector3( 0.012f, -0.065f, 0.115f),
            new Vector3( 0.012f,  0.075f, 0.115f),
            new Vector3(-0.012f,  0.075f, 0.115f),

            // 12..15: Gume de corte fino (Curvado de machado clássico)
            new Vector3(-0.0015f, -0.095f, 0.185f),
            new Vector3( 0.0015f, -0.095f, 0.185f),
            new Vector3( 0.0015f,  0.105f, 0.185f),
            new Vector3(-0.0015f,  0.105f, 0.185f)
        };

        List<int> tris = new List<int>();

        void AddQuad(int a, int b, int c, int d)
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(a); tris.Add(c); tris.Add(d);
        }

        // Tampa traseira
        AddQuad(3, 2, 1, 0);

        // Segmento Traseiro -> Olho
        AddQuad(0, 1, 5, 4); // Baixo
        AddQuad(2, 3, 7, 6); // Cima
        AddQuad(3, 0, 4, 7); // Esquerda
        AddQuad(1, 2, 6, 5); // Direita

        // Segmento Olho -> Corpo
        AddQuad(4, 5, 9, 8);
        AddQuad(6, 7, 11, 10);
        AddQuad(7, 4, 8, 11);
        AddQuad(5, 6, 10, 9);

        // Segmento Corpo -> Fio de Corte
        AddQuad(8, 9, 13, 12);
        AddQuad(10, 11, 15, 14);
        AddQuad(11, 8, 12, 15);
        AddQuad(9, 10, 14, 13);

        // Face frontal do fio
        AddQuad(12, 13, 14, 15);

        Vector2[] uvs = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            uvs[i] = new Vector2(vertices[i].x * 5f + 0.5f, vertices[i].z * 3f);
        }

        mesh.vertices = vertices;
        mesh.triangles = tris.ToArray();
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh GenerateWrapsMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Axe_LeatherWraps";

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        int turns = 6;
        int radial = 6;
        float h = 0.08f;
        float rx = 0.026f;
        float rz = 0.036f;

        for (int i = 0; i <= turns; i++)
        {
            float t = (float)i / turns;
            float y = (t - 0.5f) * h;
            for (int r = 0; r < radial; r++)
            {
                float ang = (float)r / radial * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(ang) * rx, y, Mathf.Sin(ang) * rz));
            }
        }

        for (int i = 0; i < turns; i++)
        {
            for (int r = 0; r < radial; r++)
            {
                int curr = i * radial + r;
                int next = curr + radial;
                int rNext = (r + 1) % radial;
                int currR = i * radial + rNext;
                int nextR = (i + 1) * radial + rNext;

                tris.Add(curr); tris.Add(next); tris.Add(currR);
                tris.Add(currR); tris.Add(next); tris.Add(nextR);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
