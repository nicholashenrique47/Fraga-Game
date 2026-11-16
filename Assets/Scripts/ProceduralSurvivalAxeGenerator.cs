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
        if (woodMaterial == null) woodMaterial = Resources.Load<Material>("Materials/Mat_Wood_Carved");
        if (woodMaterial == null) woodMaterial = Resources.Load<Material>("Materials/Wood_Bark");
        if (woodMaterial == null) woodMaterial = Resources.Load<Material>("Materials/Mat_DarkForest_Bark");

        if (steelMaterial == null) steelMaterial = Resources.Load<Material>("Materials/Mat_Ruins_Iron");
        if (leatherMaterial == null) leatherMaterial = Resources.Load<Material>("Materials/Mat_Leather_Wrap");
        if (leatherMaterial == null) leatherMaterial = woodMaterial;

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

        // 3. Amarras de Couro Cru Trançado na União & Empunhadura
        GameObject wrapObj = new GameObject("Leather_Wraps");
        wrapObj.transform.SetParent(axeRoot.transform, false);
        wrapObj.transform.localPosition = new Vector3(0, 0.37f, 0.015f);
        MeshFilter wrapMF = wrapObj.AddComponent<MeshFilter>();
        wrapMF.sharedMesh = GenerateWrapsMesh();
        MeshRenderer wrapMR = wrapObj.AddComponent<MeshRenderer>();
        wrapMR.sharedMaterial = leatherMaterial;

        GameObject gripObj = new GameObject("Leather_Grip");
        gripObj.transform.SetParent(axeRoot.transform, false);
        gripObj.transform.localPosition = new Vector3(0, -0.02f, -0.005f);
        MeshFilter gripMF = gripObj.AddComponent<MeshFilter>();
        gripMF.sharedMesh = GenerateGripMesh();
        MeshRenderer gripMR = gripObj.AddComponent<MeshRenderer>();
        gripMR.sharedMaterial = leatherMaterial;

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

        int heightSteps = 20;
        int radialSteps = 12;
        float totalLength = 0.60f;

        for (int i = 0; i <= heightSteps; i++)
        {
            float t = (float)i / heightSteps;
            float y = (t - 0.25f) * totalLength;

            float offsetZ = Mathf.Sin(t * Mathf.PI * 0.95f) * 0.024f - (Mathf.Pow(1f - t, 2.2f) * 0.036f);
            float radiusX = Mathf.Lerp(0.014f, 0.019f, Mathf.Sin(t * Mathf.PI)) * 0.85f;
            float radiusZ = Mathf.Lerp(0.022f, 0.028f, Mathf.Sin(t * Mathf.PI));

            if (t < 0.16f)
            {
                float knob = (0.16f - t) * 0.08f;
                radiusX += knob * 0.5f;
                radiusZ += knob;
            }

            for (int r = 0; r <= radialSteps; r++)
            {
                float frac = (float)r / radialSteps;
                float angle = frac * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radiusX;
                float z = Mathf.Sin(angle) * radiusZ + offsetZ;

                verts.Add(new Vector3(x, y, z));
                norms.Add(new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)).normalized);
                uvs.Add(new Vector2(frac * 2f, t * 2.5f));
            }
        }

        int ringStride = radialSteps + 1;
        for (int i = 0; i < heightSteps; i++)
        {
            for (int r = 0; r < radialSteps; r++)
            {
                int curr = i * ringStride + r;
                int next = curr + ringStride;
                int currR = curr + 1;
                int nextR = next + 1;

                tris.Add(curr); tris.Add(next); tris.Add(currR);
                tris.Add(currR); tris.Add(next); tris.Add(nextR);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    private static Mesh GenerateAxeHeadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "AxeHead_ForgedSteel";

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

            // 8..11: Pescoço da lâmina chanfrada
            new Vector3(-0.015f, -0.062f, 0.080f),
            new Vector3( 0.015f, -0.062f, 0.080f),
            new Vector3( 0.015f,  0.064f, 0.080f),
            new Vector3(-0.015f,  0.064f, 0.080f),

            // 12..15: Corpo / Bochecha da lâmina
            new Vector3(-0.010f, -0.078f, 0.135f),
            new Vector3( 0.010f, -0.078f, 0.135f),
            new Vector3( 0.010f,  0.086f, 0.135f),
            new Vector3(-0.010f,  0.086f, 0.135f),

            // 16..19: Gume de corte fino
            new Vector3(-0.0015f, -0.098f, 0.188f),
            new Vector3( 0.0015f, -0.098f, 0.188f),
            new Vector3( 0.0015f,  0.106f, 0.188f),
            new Vector3(-0.0015f,  0.106f, 0.188f)
        };

        List<int> tris = new List<int>();
        void AddQuad(int a, int b, int c, int d)
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(a); tris.Add(c); tris.Add(d);
        }

        AddQuad(3, 2, 1, 0);
        for (int s = 0; s < 4; s++)
        {
            int o = s * 4;
            int n = (s + 1) * 4;
            AddQuad(o + 0, o + 1, n + 1, n + 0);
            AddQuad(o + 2, o + 3, n + 3, n + 2);
            AddQuad(o + 3, o + 0, n + 0, n + 3);
            AddQuad(o + 1, o + 2, n + 2, n + 1);
        }
        AddQuad(16, 17, 18, 19);

        Vector2[] uvs = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            uvs[i] = new Vector2(vertices[i].y * 4f + 0.5f, vertices[i].z * 3.5f + 0.25f);
        }

        mesh.vertices = vertices;
        mesh.triangles = tris.ToArray();
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    private static Mesh GenerateWrapsMesh()
    {
        return GenerateLeatherBandMesh("Axe_LeatherWraps", 0.085f, 0.026f, 0.036f, 8, 10);
    }

    private static Mesh GenerateGripMesh()
    {
        return GenerateLeatherBandMesh("Axe_LeatherGrip", 0.19f, 0.0185f, 0.0265f, 10, 10);
    }

    private static Mesh GenerateLeatherBandMesh(string meshName, float h, float rx, float rz, int turns, int radial)
    {
        Mesh mesh = new Mesh();
        mesh.name = meshName;

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        for (int i = 0; i <= turns; i++)
        {
            float t = (float)i / turns;
            float y = (t - 0.5f) * h;
            float ridge = 1f + 0.06f * Mathf.Sin(t * turns * Mathf.PI * 2f);
            for (int r = 0; r <= radial; r++)
            {
                float frac = (float)r / radial;
                float ang = frac * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(ang) * rx * ridge, y, Mathf.Sin(ang) * rz * ridge));
                uvs.Add(new Vector2(frac * 2f, t * 2f));
            }
        }

        int stride = radial + 1;
        for (int i = 0; i < turns; i++)
        {
            for (int r = 0; r < radial; r++)
            {
                int curr = i * stride + r;
                int next = curr + stride;
                tris.Add(curr); tris.Add(next); tris.Add(curr + 1);
                tris.Add(curr + 1); tris.Add(next); tris.Add(next + 1);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }
}
