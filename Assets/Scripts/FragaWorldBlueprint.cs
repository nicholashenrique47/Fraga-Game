using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// FragaWorldBlueprint: Arquitetura Geográfica Autoritativa do Mundo Aberto (768x768m ~50% de The Forest).
/// Define de forma determinística (seed-driven):
/// - Costa orgânica da Ilha Atlântica (enseadas, cabos, praias arenosas e penhascos)
/// - Cadeia montanhosa norte (Pico da Serra a +32.5m com trilha em zigue-zague caminhável)
/// - Lago da Serra (+5.35m) + Rio sinuoso que desce até a Enseada Sul
/// - 8 Pontos de Interesse (POIs) e Biomas distintos conectados por rede de trilhas limpas
/// - Cache espacial pré-assado O(1) para carregamento instantâneo e zero NaNs
/// </summary>
public static class FragaWorldBlueprint
{
    public const int DefaultWidth = 768;
    public const int DefaultHeight = 768;
    public const float SeaLevel = 1.60f;
    public const float LakeLevel = 5.35f;

    public enum WorldRegion
    {
        CrashCamp,
        OakForest,
        PineRidge,
        SwampBasin,
        CelticCastro,
        FarolBluff,
        HighlandLake,
        SacredCave,
        MountainSummit,
        AtlanticCoast,
        DeepOcean
    }

    public struct PoiInfo
    {
        public string Id;
        public string DisplayName;
        public Vector2 Pos;
        public float DiscoverRadius;
        public Color MarkerColor;
    }

    public struct BridgePlacement
    {
        public Vector2 Pos;
        public float YawDegrees;
    }

    public static bool IsInitialized { get; private set; }
    public static int Seed { get; private set; } = 1913;
    public static int Width { get; private set; } = DefaultWidth;
    public static int Height { get; private set; } = DefaultHeight;

    private static float s_seedOx;
    private static float s_seedOz;
    private static float s_coastPhase1;
    private static float s_coastPhase2;

    // POIs autoritativos em espaço do mundo (metros)
    public static Vector2 CrashCampPos { get; private set; }
    public static Vector2 HighlandLakePos { get; private set; }
    public static Vector2 MountainSummitPos { get; private set; }
    public static Vector2 SacredCavePos { get; private set; }
    public static Vector2 CelticCastroPos { get; private set; }
    public static Vector2 FarolBluffPos { get; private set; }
    public static Vector2 SwampBasinPos { get; private set; }
    public static Vector2 SouthCovePos { get; private set; }

    public static PoiInfo[] AllPois { get; private set; } = Array.Empty<PoiInfo>();
    public static BridgePlacement[] RiverBridges { get; private set; } = Array.Empty<BridgePlacement>();

    private static Vector2[] s_riverPoints;
    private static float[] s_riverWidths;
    private static float[] s_riverWaterY;

    private struct TrailSegment
    {
        public Vector2 A;
        public Vector2 B;
        public float HalfWidth;
    }
    private static TrailSegment[] s_trailSegments = Array.Empty<TrailSegment>();
    private static Texture2D s_cachedMapTexture;
    private static int s_cachedTexSeed = -1;

    // Cache espacial O(1) para consultas em tempo real sem travamentos no Play
    private static bool s_fieldsBaked = false;
    private static bool s_heightsBaked = false;
    private static float[] s_trailField;
    private static float[] s_riverMaskField;
    private static float[] s_riverWaterYField;
    private static float[] s_coastGrid;
    private static float[] s_heightGrid;

    /// <summary>
    /// Smoothstep estilo GLSL: retorna [0..1] conforme x varia entre edge0 e edge1.
    /// (Ao contrário de Mathf.SmoothStep do Unity, cujo terceiro parâmetro é t em [0..1]).
    /// </summary>
    public static float SmoothRange(float edge0, float edge1, float x)
    {
        float span = edge1 - edge0;
        if (Mathf.Abs(span) < 1e-6f) return x >= edge0 ? 1f : 0f;
        float t = Mathf.Clamp01((x - edge0) / span);
        return t * t * (3f - 2f * t);
    }

    public static void EnsureInitialized()
    {
        if (!IsInitialized)
        {
            Initialize(1913, DefaultWidth, DefaultHeight);
        }
    }

    public static void Initialize(int seed, int width = DefaultWidth, int height = DefaultHeight)
    {
        if (IsInitialized && Seed == seed && Width == width && Height == height && s_heightsBaked)
            return;

        Seed = seed;
        Width = Mathf.Max(128, width);
        Height = Mathf.Max(128, height);
        s_fieldsBaked = false;
        s_heightsBaked = false;

        float sx = Width / 768f;
        float sz = Height / 768f;

        // Offsets determinísticos suaves derivados da seed (preservando a geografia coerente)
        int h = Mathf.Abs(seed * 73856093);
        s_seedOx = (h % 997) * 0.13f;
        s_seedOz = ((h / 997) % 991) * 0.17f;
        s_coastPhase1 = ((h % 360) * 0.01745f) * 0.15f;
        s_coastPhase2 = (((h / 31) % 360) * 0.01745f) * 0.15f;

        CrashCampPos = new Vector2(384f * sx, 384f * sz);
        HighlandLakePos = new Vector2(508f * sx, 432f * sz);
        MountainSummitPos = new Vector2(215f * sx, 585f * sz);
        SacredCavePos = new Vector2(312f * sx, 502f * sz);
        CelticCastroPos = new Vector2(568f * sx, 258f * sz);
        FarolBluffPos = new Vector2(642f * sx, 152f * sz);
        SwampBasinPos = new Vector2(188f * sx, 215f * sz);
        SouthCovePos = new Vector2(455f * sx, 92f * sz);

        AllPois = new PoiInfo[]
        {
            new PoiInfo { Id = "camp",   DisplayName = "Acampamento",         Pos = CrashCampPos,      DiscoverRadius = 35f, MarkerColor = new Color(0.95f, 0.78f, 0.25f) },
            new PoiInfo { Id = "lake",   DisplayName = "Lago da Fraga",       Pos = HighlandLakePos,   DiscoverRadius = 56f, MarkerColor = new Color(0.25f, 0.78f, 0.98f) },
            new PoiInfo { Id = "cave",   DisplayName = "Gruta do Ancião",     Pos = SacredCavePos,     DiscoverRadius = 32f, MarkerColor = new Color(0.62f, 0.72f, 0.85f) },
            new PoiInfo { Id = "summit", DisplayName = "Pico da Serra",       Pos = MountainSummitPos, DiscoverRadius = 36f, MarkerColor = new Color(0.92f, 0.94f, 0.98f) },
            new PoiInfo { Id = "castro", DisplayName = "Castro Celta",        Pos = CelticCastroPos,   DiscoverRadius = 42f, MarkerColor = new Color(0.85f, 0.55f, 0.28f) },
            new PoiInfo { Id = "farol",  DisplayName = "Farol Celta",         Pos = FarolBluffPos,     DiscoverRadius = 38f, MarkerColor = new Color(1.00f, 0.42f, 0.20f) },
            new PoiInfo { Id = "swamp",  DisplayName = "Pântano das Meigas",  Pos = SwampBasinPos,     DiscoverRadius = 58f, MarkerColor = new Color(0.32f, 0.85f, 0.48f) },
            new PoiInfo { Id = "cove",   DisplayName = "Enseada Atlântica",   Pos = SouthCovePos,      DiscoverRadius = 42f, MarkerColor = new Color(0.90f, 0.84f, 0.58f) },
        };

        BuildRiverSpline(sx, sz);
        BuildTrailNetwork(sx, sz);

        IsInitialized = true;
        BakeSpatialCache();

        if (s_cachedMapTexture != null && s_cachedTexSeed != seed)
        {
            UnityEngine.Object.Destroy(s_cachedMapTexture);
            s_cachedMapTexture = null;
        }
    }

    private static void BakeSpatialCache()
    {
        int w = Width;
        int h = Height;
        int cellCount = w * h;

        if (s_trailField == null || s_trailField.Length != cellCount)
            s_trailField = new float[cellCount];
        else
            Array.Clear(s_trailField, 0, cellCount);

        if (s_riverMaskField == null || s_riverMaskField.Length != cellCount)
            s_riverMaskField = new float[cellCount];
        else
            Array.Clear(s_riverMaskField, 0, cellCount);

        if (s_riverWaterYField == null || s_riverWaterYField.Length != cellCount)
            s_riverWaterYField = new float[cellCount];

        float[] riverMinDist = new float[cellCount];
        for (int i = 0; i < cellCount; i++)
        {
            riverMinDist[i] = 999f;
            s_riverWaterYField[i] = SeaLevel;
        }

        // 1. Rasteriza trilhas apenas nos AABBs locais dos segmentos (~25k iterações, < 1ms)
        for (int s = 0; s < s_trailSegments.Length; s++)
        {
            TrailSegment seg = s_trailSegments[s];
            Vector2 ab = seg.B - seg.A;
            float abSq = ab.sqrMagnitude;
            if (abSq < 0.001f) continue;

            float reach = seg.HalfWidth + 1.65f;
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(seg.A.x, seg.B.x) - reach));
            int maxX = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(seg.A.x, seg.B.x) + reach));
            int minZ = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(seg.A.y, seg.B.y) - reach));
            int maxZ = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(seg.A.y, seg.B.y) + reach));

            float inner = seg.HalfWidth * 0.55f;
            float outer = seg.HalfWidth + 1.6f;

            for (int iz = minZ; iz <= maxZ; iz++)
            {
                int rowOff = iz * w;
                float pz = iz + 0.5f;
                for (int ix = minX; ix <= maxX; ix++)
                {
                    float px = ix + 0.5f;
                    float t = Mathf.Clamp01(((px - seg.A.x) * ab.x + (pz - seg.A.y) * ab.y) / abSq);
                    float dx = px - (seg.A.x + ab.x * t);
                    float dz = pz - (seg.A.y + ab.y * t);
                    float dSq = dx * dx + dz * dz;
                    if (dSq >= outer * outer) continue;

                    float wVal = 1f - SmoothRange(inner, outer, Mathf.Sqrt(dSq));
                    int idx = rowOff + ix;
                    if (wVal > s_trailField[idx]) s_trailField[idx] = wVal;
                }
            }
        }

        // 2. Rasteriza o rio apenas nos AABBs locais (~22k iterações, < 1ms)
        if (s_riverPoints != null && s_riverPoints.Length >= 2)
        {
            for (int s = 0; s < s_riverPoints.Length - 1; s++)
            {
                Vector2 a = s_riverPoints[s];
                Vector2 b = s_riverPoints[s + 1];
                Vector2 ab = b - a;
                float abSq = ab.sqrMagnitude;
                if (abSq < 0.001f) continue;

                float maxW = Mathf.Max(s_riverWidths[s], s_riverWidths[s + 1]);
                float reach = maxW * 2.15f;
                int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - reach));
                int maxX = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + reach));
                int minZ = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - reach));
                int maxZ = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + reach));

                for (int iz = minZ; iz <= maxZ; iz++)
                {
                    int rowOff = iz * w;
                    float pz = iz + 0.5f;
                    for (int ix = minX; ix <= maxX; ix++)
                    {
                        float px = ix + 0.5f;
                        float t = Mathf.Clamp01(((px - a.x) * ab.x + (pz - a.y) * ab.y) / abSq);
                        float dx = px - (a.x + ab.x * t);
                        float dz = pz - (a.y + ab.y * t);
                        float d = Mathf.Sqrt(dx * dx + dz * dz);

                        int idx = rowOff + ix;
                        if (d < riverMinDist[idx])
                        {
                            float bw = Mathf.Lerp(s_riverWidths[s], s_riverWidths[s + 1], t);
                            if (d < bw * 2.1f)
                            {
                                riverMinDist[idx] = d;
                                s_riverMaskField[idx] = 1f - SmoothRange(bw * 0.55f, bw * 2.1f, d);
                                s_riverWaterYField[idx] = Mathf.Lerp(s_riverWaterY[s], s_riverWaterY[s + 1], t);
                            }
                        }
                    }
                }
            }
        }

        s_fieldsBaked = true;

        // 3. Assa costa e elevação em grade (Width+1)x(Height+1) para leitura O(1)
        int gw = w + 1;
        int gh = h + 1;
        int gCount = gw * gh;
        if (s_coastGrid == null || s_coastGrid.Length != gCount)
            s_coastGrid = new float[gCount];
        if (s_heightGrid == null || s_heightGrid.Length != gCount)
            s_heightGrid = new float[gCount];

        for (int z = 0; z < gh; z++)
        {
            int rowOff = z * gw;
            for (int x = 0; x < gw; x++)
            {
                float coast = ComputeIslandCoastMetricAnalytic(x, z);
                s_coastGrid[rowOff + x] = coast;
                s_heightGrid[rowOff + x] = SampleHeightAnalytic(x, z, coast);
            }
        }

        s_heightsBaked = true;
    }

    private static void BuildRiverSpline(float sx, float sz)
    {
        // Nascente na Serra -> Cachoeira/Vale -> Lago da Fraga -> Vau do Castro -> Enseada Sul
        Vector2[] ctrl = new Vector2[]
        {
            new Vector2(252f * sx, 542f * sz), // Nascente serrana
            new Vector2(318f * sx, 512f * sz),
            new Vector2(388f * sx, 474f * sz), // Ponte Norte
            new Vector2(452f * sx, 452f * sz),
            new Vector2(508f * sx, 432f * sz), // Centro Lago da Fraga
            new Vector2(502f * sx, 362f * sz),
            new Vector2(486f * sx, 304f * sz), // Ponte Sul (caminho p/ Castro)
            new Vector2(468f * sx, 232f * sz),
            new Vector2(458f * sx, 158f * sz),
            new Vector2(455f * sx, 92f * sz),  // Desembocadura oceânica
            new Vector2(452f * sx, 52f * sz),
        };

        int samples = 56;
        s_riverPoints = new Vector2[samples];
        s_riverWidths = new float[samples];
        s_riverWaterY = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / (samples - 1);
            Vector2 p = EvaluateSpline(ctrl, t);
            s_riverPoints[i] = p;
            s_riverWidths[i] = Mathf.Lerp(2.8f, 6.4f, Mathf.Pow(t, 1.15f));

            // Nível da água desce suavemente da serra até o Lago (5.35m) e depois ao Mar (1.60m)
            if (t <= 0.40f)
            {
                float u = t / 0.40f;
                s_riverWaterY[i] = Mathf.Lerp(10.8f, LakeLevel, SmoothRange(0f, 1f, u));
            }
            else
            {
                float u = (t - 0.40f) / 0.60f;
                s_riverWaterY[i] = Mathf.Lerp(LakeLevel, SeaLevel, SmoothRange(0f, 1f, u));
            }
        }

        // Pontes de madeira sobre o rio nos cruzamentos de trilhas
        Vector2 b1 = new Vector2(388f * sx, 474f * sz);
        Vector2 b2 = new Vector2(486f * sx, 304f * sz);
        RiverBridges = new BridgePlacement[]
        {
            new BridgePlacement { Pos = b1, YawDegrees = 74f },
            new BridgePlacement { Pos = b2, YawDegrees = 98f },
        };
    }

    public static int GetRiverSampleCount()
    {
        EnsureInitialized();
        return s_riverPoints != null ? s_riverPoints.Length : 0;
    }

    public static void GetRiverSample(int index, out Vector2 pos, out float halfWidth, out float waterY)
    {
        EnsureInitialized();
        int idx = Mathf.Clamp(index, 0, s_riverPoints.Length - 1);
        pos = s_riverPoints[idx];
        halfWidth = s_riverWidths[idx];
        waterY = s_riverWaterY[idx];
    }

    private static void BuildTrailNetwork(float sx, float sz)
    {
        var segs = new List<TrailSegment>(96);

        void AddCurvedTrail(Vector2[] waypoints, float halfWidth = 2.4f, int stepsPerLeg = 5)
        {
            for (int w = 0; w < waypoints.Length - 1; w++)
            {
                Vector2 p0 = waypoints[w];
                Vector2 p1 = waypoints[w + 1];
                Vector2 dir = (p1 - p0);
                float len = dir.magnitude;
                if (len < 0.1f) continue;
                Vector2 perp = new Vector2(-dir.y, dir.x) / len;
                float curveAmp = Mathf.Min(12f, len * 0.12f) * (((w % 2) == 0) ? 1f : -1f);

                Vector2 prev = p0;
                for (int s = 1; s <= stepsPerLeg; s++)
                {
                    float u = (float)s / stepsPerLeg;
                    Vector2 next = Vector2.Lerp(p0, p1, u) + perp * (Mathf.Sin(u * Mathf.PI) * curveAmp);
                    segs.Add(new TrailSegment { A = prev, B = next, HalfWidth = halfWidth });
                    prev = next;
                }
            }
        }

        // 1. Acampamento -> Lago da Fraga (Pier Oeste)
        AddCurvedTrail(new[]
        {
            CrashCampPos,
            new Vector2(424f * sx, 405f * sz),
            new Vector2(462f * sx, 428f * sz),
        }, 2.4f);

        // 2. Acampamento -> Ponte Sul -> Castro Celta -> Farol Celta
        AddCurvedTrail(new[]
        {
            CrashCampPos,
            new Vector2(436f * sx, 344f * sz),
            new Vector2(486f * sx, 304f * sz),
            new Vector2(532f * sx, 282f * sz),
            CelticCastroPos,
            new Vector2(606f * sx, 206f * sz),
            FarolBluffPos + new Vector2(-10f, 10f),
        }, 2.5f);

        // 3. Acampamento -> Ponte Norte -> Gruta do Ancião
        AddCurvedTrail(new[]
        {
            CrashCampPos,
            new Vector2(362f * sx, 442f * sz),
            new Vector2(388f * sx, 474f * sz),
            SacredCavePos + new Vector2(4f, -8f),
        }, 2.3f);

        // 4. Trilha em Zigue-Zague (Serpentina) da Gruta até o Pico da Serra (+32.5m)
        AddCurvedTrail(new[]
        {
            SacredCavePos + new Vector2(4f, -8f),
            new Vector2(276f * sx, 514f * sz),
            new Vector2(246f * sx, 548f * sz),
            new Vector2(268f * sx, 578f * sz),
            new Vector2(236f * sx, 602f * sz),
            MountainSummitPos,
        }, 2.2f);

        // 5. Acampamento -> Pântano das Meigas
        AddCurvedTrail(new[]
        {
            CrashCampPos,
            new Vector2(315f * sx, 326f * sz),
            new Vector2(252f * sx, 268f * sz),
            SwampBasinPos + new Vector2(18f, 16f),
        }, 2.3f);

        // 6. Acampamento -> Enseada Sul
        AddCurvedTrail(new[]
        {
            CrashCampPos,
            new Vector2(396f * sx, 286f * sz),
            new Vector2(416f * sx, 185f * sz),
            new Vector2(436f * sx, 118f * sz),
        }, 2.3f);

        s_trailSegments = segs.ToArray();
    }

    private static Vector2 EvaluateSpline(Vector2[] pts, float t)
    {
        if (pts.Length == 0) return Vector2.zero;
        if (pts.Length == 1) return pts[0];
        float scaled = Mathf.Clamp01(t) * (pts.Length - 1);
        int idx = Mathf.Min(pts.Length - 2, Mathf.FloorToInt(scaled));
        float u = scaled - idx;

        Vector2 p0 = pts[Mathf.Max(0, idx - 1)];
        Vector2 p1 = pts[idx];
        Vector2 p2 = pts[idx + 1];
        Vector2 p3 = pts[Mathf.Min(pts.Length - 1, idx + 2)];

        float u2 = u * u;
        float u3 = u2 * u;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * u +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * u3
        );
    }

    private static float ComputeIslandCoastMetricAnalytic(float x, float z)
    {
        float cx = Width * 0.5f;
        float cz = Height * 0.5f;
        float dx = (x - cx) / (Width * 0.445f);
        float dz = (z - cz) / (Height * 0.435f);
        float baseR = Mathf.Sqrt(dx * dx + dz * dz);

        float ang = Mathf.Atan2(dz, dx);
        float warp =
            0.095f * Mathf.Sin(ang * 3f + 0.5f + s_coastPhase1) +
            0.065f * Mathf.Cos(ang * 5f - 1.1f + s_coastPhase2) +
            0.040f * Mathf.Sin(ang * 8f + 2.3f);

        // Enseada Sul aberta perto da foz do rio (ang ~ -1.37 rad)
        float tCove = (ang + 1.37f) / 0.24f;
        float southCove = Mathf.Exp(-(tCove * tCove)) * 0.14f;
        // Cabo Sudeste para o Farol Celta (ang ~ -0.72 rad)
        float tFarol = (ang + 0.72f) / 0.20f;
        float farolPeninsula = Mathf.Exp(-(tFarol * tFarol)) * 0.16f;

        return baseR - warp - farolPeninsula + southCove;
    }

    /// <summary>
    /// Retorna fator normalizado da ilha: &lt; 1.0 = terra firme, ~1.0 = praia, &gt; 1.05 = oceano profundo.
    /// </summary>
    public static float GetIslandCoastMetric(float x, float z)
    {
        EnsureInitialized();
        if (s_heightsBaked && x >= 0f && x <= Width && z >= 0f && z <= Height)
        {
            int gw = Width + 1;
            int ix = Mathf.Min(Width - 1, Mathf.FloorToInt(x));
            int iz = Mathf.Min(Height - 1, Mathf.FloorToInt(z));
            float fx = x - ix;
            float fz = z - iz;
            if (fx == 0f && fz == 0f) return s_coastGrid[iz * gw + ix];
            int i00 = iz * gw + ix;
            float c0 = Mathf.Lerp(s_coastGrid[i00], s_coastGrid[i00 + 1], fx);
            float c1 = Mathf.Lerp(s_coastGrid[i00 + gw], s_coastGrid[i00 + gw + 1], fx);
            return Mathf.Lerp(c0, c1, fz);
        }
        return ComputeIslandCoastMetricAnalytic(x, z);
    }

    public static bool IsInsideIsland(float x, float z)
    {
        return GetIslandCoastMetric(x, z) < 0.99f;
    }

    public static float GetTrailWeight(float x, float z)
    {
        EnsureInitialized();
        if (s_fieldsBaked && x >= 0f && x < Width && z >= 0f && z < Height)
        {
            int ix = Mathf.Clamp(Mathf.FloorToInt(x), 0, Width - 1);
            int iz = Mathf.Clamp(Mathf.FloorToInt(z), 0, Height - 1);
            return s_trailField[iz * Width + ix];
        }

        float minDist = 999f;
        float activeHW = 2.4f;
        for (int i = 0; i < s_trailSegments.Length; i++)
        {
            TrailSegment seg = s_trailSegments[i];
            float abx = seg.B.x - seg.A.x;
            float abz = seg.B.y - seg.A.y;
            float abSq = abx * abx + abz * abz;
            if (abSq < 0.001f) continue;
            float t = Mathf.Clamp01(((x - seg.A.x) * abx + (z - seg.A.y) * abz) / abSq);
            float dx = x - (seg.A.x + abx * t);
            float dz = z - (seg.A.y + abz * t);
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d < minDist)
            {
                minDist = d;
                activeHW = seg.HalfWidth;
            }
        }

        if (minDist >= activeHW + 1.6f) return 0f;
        return 1f - SmoothRange(activeHW * 0.55f, activeHW + 1.6f, minDist);
    }

    public static void SampleRiverInfo(float x, float z, out float riverMask, out float riverWaterY)
    {
        EnsureInitialized();
        if (s_fieldsBaked && x >= 0f && x < Width && z >= 0f && z < Height)
        {
            int idx = Mathf.Clamp(Mathf.FloorToInt(z), 0, Height - 1) * Width + Mathf.Clamp(Mathf.FloorToInt(x), 0, Width - 1);
            riverMask = s_riverMaskField[idx];
            riverWaterY = s_riverWaterYField[idx];
            return;
        }

        riverMask = 0f;
        riverWaterY = SeaLevel;
        if (s_riverPoints == null || s_riverPoints.Length < 2) return;

        float minDist = 999f;
        float bestWidth = 3f;
        float bestWaterY = SeaLevel;

        for (int i = 0; i < s_riverPoints.Length - 1; i++)
        {
            Vector2 a = s_riverPoints[i];
            Vector2 b = s_riverPoints[i + 1];
            float abx = b.x - a.x;
            float abz = b.y - a.y;
            float abSq = abx * abx + abz * abz;
            if (abSq < 0.001f) continue;

            float t = Mathf.Clamp01(((x - a.x) * abx + (z - a.y) * abz) / abSq);
            float dx = x - (a.x + abx * t);
            float dz = z - (a.y + abz * t);
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d < minDist)
            {
                minDist = d;
                bestWidth = Mathf.Lerp(s_riverWidths[i], s_riverWidths[i + 1], t);
                bestWaterY = Mathf.Lerp(s_riverWaterY[i], s_riverWaterY[i + 1], t);
            }
        }

        if (minDist < bestWidth * 2.1f)
        {
            riverMask = 1f - SmoothRange(bestWidth * 0.55f, bestWidth * 2.1f, minDist);
            riverWaterY = bestWaterY;
        }
    }

    private static float SampleHeightAnalytic(float x, float z, float coast)
    {
        // 1. Base continental suavemente ondulada (colinas galegas)
        float n1 = Mathf.PerlinNoise((x + s_seedOx) * 0.0085f, (z + s_seedOz) * 0.0085f);
        float n2 = Mathf.PerlinNoise((x - s_seedOx) * 0.022f + 37f, (z + s_seedOz) * 0.022f + 91f);
        float ridge = 1f - Mathf.Abs(Mathf.PerlinNoise((x + 17f) * 0.015f, (z + 53f) * 0.015f) * 2f - 1f);
        float micro = Mathf.PerlinNoise(x * 0.11f + 12f, z * 0.11f + 83f) * 0.16f;

        float inlandH = 3.2f + n1 * 5.8f + n2 * 2.2f + (ridge * ridge) * 2.1f + micro;

        // 2. Cadeia montanhosa norte (Pico da Serra a +32.5m)
        float dxS = x - MountainSummitPos.x;
        float dzS = z - MountainSummitPos.y;
        float dSummitSq = dxS * dxS + dzS * dzS;
        if (dSummitSq < 155f * 155f)
        {
            float dSummit = Mathf.Sqrt(dSummitSq);
            float mT = Mathf.Clamp01(1f - SmoothRange(8f, 155f, dSummit));
            float ridgeVar = 0.88f + 0.24f * Mathf.PerlinNoise(x * 0.03f, z * 0.03f);
            float peakBoost = 25.5f * Mathf.Pow(mT, 1.35f) * ridgeVar;
            inlandH += peakBoost;

            if (dSummit < 14f)
            {
                float flatT = 1f - SmoothRange(6f, 14f, dSummit);
                inlandH = Mathf.Lerp(inlandH, 31.8f, flatT);
            }
        }

        // Terraço da Gruta do Ancião
        float dxC = x - SacredCavePos.x;
        float dzC = z - SacredCavePos.y;
        float dCaveSq = dxC * dxC + dzC * dzC;
        if (dCaveSq < 30f * 30f)
        {
            float caveT = 1f - SmoothRange(10f, 30f, Mathf.Sqrt(dCaveSq));
            inlandH = Mathf.Lerp(inlandH, 13.6f, caveT * 0.85f);
        }

        // 3. Penhasco dramático do Farol Celta (+15.8m sobre o Atlântico)
        float dxF = x - FarolBluffPos.x;
        float dzF = z - FarolBluffPos.y;
        float dFarolSq = dxF * dxF + dzF * dzF;
        float dFarol = 999f;
        if (dFarolSq < 52f * 52f)
        {
            dFarol = Mathf.Sqrt(dFarolSq);
            float fT = Mathf.Clamp01(1f - SmoothRange(12f, 52f, dFarol));
            inlandH = Mathf.Max(inlandH, Mathf.Lerp(4.2f, 15.8f, Mathf.Pow(fT, 1.15f)));
            if (dFarol < 14f)
            {
                float fFlat = 1f - SmoothRange(8f, 14f, dFarol);
                inlandH = Mathf.Lerp(inlandH, 15.8f, fFlat);
            }
        }

        // 4. Platô do Castro Celta
        float dxK = x - CelticCastroPos.x;
        float dzK = z - CelticCastroPos.y;
        float dCastroSq = dxK * dxK + dzK * dzK;
        if (dCastroSq < 44f * 44f)
        {
            float cT = 1f - SmoothRange(22f, 44f, Mathf.Sqrt(dCastroSq));
            inlandH = Mathf.Lerp(inlandH, 8.6f + micro * 0.3f, cT);
        }

        // 5. Clareira do Acampamento Inicial
        float dxA = x - CrashCampPos.x;
        float dzA = z - CrashCampPos.y;
        float dCampSq = dxA * dxA + dzA * dzA;
        if (dCampSq < 34f * 34f)
        {
            float sT = 1f - SmoothRange(16f, 34f, Mathf.Sqrt(dCampSq));
            inlandH = Mathf.Lerp(inlandH, 6.8f + micro * 0.2f, sT);
        }

        // 6. Bacia do Pântano das Meigas
        float dxP = x - SwampBasinPos.x;
        float dzP = z - SwampBasinPos.y;
        float dSwampSq = dxP * dxP + dzP * dzP;
        if (dSwampSq < 92f * 92f)
        {
            float swT = 1f - SmoothRange(38f, 92f, Mathf.Sqrt(dSwampSq));
            float hummock = Mathf.PerlinNoise(x * 0.08f + 5f, z * 0.08f + 5f) * 1.15f;
            float swampTarget = 1.65f + hummock;
            inlandH = Mathf.Lerp(inlandH, swampTarget, swT * 0.88f);
        }

        // 7. Bacia do Lago da Fraga
        float dxL = x - HighlandLakePos.x;
        float dzL = z - HighlandLakePos.y;
        float dLakeSq = dxL * dxL + dzL * dzL;
        if (dLakeSq < 62f * 62f)
        {
            float dLake = Mathf.Sqrt(dLakeSq);
            float shoreRimH = LakeLevel + 0.95f;
            if (dLake < 38f)
            {
                float bowl = 1f - SmoothRange(10f, 38f, dLake);
                float bedH = Mathf.Lerp(LakeLevel - 0.15f, 3.10f, bowl);
                inlandH = Mathf.Min(inlandH, bedH);
            }
            else
            {
                float rimT = 1f - SmoothRange(40f, 62f, dLake);
                inlandH = Mathf.Lerp(inlandH, shoreRimH + micro * 0.3f, rimT * 0.78f);
            }
        }

        // 8. Escavação suave do Rio da Fraga
        SampleRiverInfo(x, z, out float riverMask, out float riverWaterY);
        if (riverMask > 0.001f)
        {
            float riverBedTarget = riverWaterY - 0.78f;
            inlandH = Mathf.Lerp(inlandH, Mathf.Min(inlandH, riverBedTarget), riverMask * 0.92f);
        }

        // 9. Suavização sutil em trilhas
        float trailW = GetTrailWeight(x, z);
        if (trailW > 0.01f)
        {
            inlandH -= micro * 0.85f * trailW;
        }

        // 10. Máscara costeira e leito marinho ao redor da ilha
        if (coast > 0.84f)
        {
            if (dFarol > 34f)
            {
                float beachT = SmoothRange(0.84f, 1.00f, coast);
                float deepT = SmoothRange(0.98f, 1.24f, coast);
                float beachH = Mathf.Lerp(inlandH, SeaLevel + 0.25f, beachT);
                inlandH = Mathf.Lerp(beachH, -3.8f, deepT);
            }
            else
            {
                float cliffDrop = SmoothRange(0.96f, 1.15f, coast);
                inlandH = Mathf.Lerp(inlandH, -3.5f, cliffDrop);
            }
        }

        if (float.IsNaN(inlandH) || float.IsInfinity(inlandH))
            inlandH = SeaLevel;

        return inlandH;
    }

    /// <summary>
    /// Retorna a altura do terreno em qualquer coordenada (x, z) em metros em tempo O(1).
    /// </summary>
    public static float SampleHeight(float x, float z)
    {
        EnsureInitialized();

        if (s_heightsBaked && x >= 0f && x <= Width && z >= 0f && z <= Height)
        {
            int gw = Width + 1;
            int ix = Mathf.Min(Width - 1, Mathf.FloorToInt(x));
            int iz = Mathf.Min(Height - 1, Mathf.FloorToInt(z));
            float fx = x - ix;
            float fz = z - iz;
            if (fx == 0f && fz == 0f) return s_heightGrid[iz * gw + ix];
            int i00 = iz * gw + ix;
            float h0 = Mathf.Lerp(s_heightGrid[i00], s_heightGrid[i00 + 1], fx);
            float h1 = Mathf.Lerp(s_heightGrid[i00 + gw], s_heightGrid[i00 + gw + 1], fx);
            return Mathf.Lerp(h0, h1, fz);
        }

        return SampleHeightAnalytic(x, z, ComputeIslandCoastMetricAnalytic(x, z));
    }

    /// <summary>
    /// Retorna a altura da superfície da água caso (x,z) esteja no Oceano, Lago ou Rio.
    /// </summary>
    public static float GetWaterSurfaceY(float x, float z)
    {
        EnsureInitialized();

        float dxL = x - HighlandLakePos.x;
        float dzL = z - HighlandLakePos.y;
        if (dxL * dxL + dzL * dzL < 42f * 42f) return LakeLevel;

        SampleRiverInfo(x, z, out float rMask, out float rWaterY);
        if (rMask > 0.25f) return Mathf.Max(SeaLevel, rWaterY);

        return SeaLevel;
    }

    public static float GetWaterDepth(float x, float z)
    {
        float groundY = SampleHeight(x, z);
        float waterY = GetWaterSurfaceY(x, z);
        return Mathf.Max(0f, waterY - groundY);
    }

    public static WorldRegion GetRegion(float x, float z)
    {
        EnsureInitialized();

        float coast = GetIslandCoastMetric(x, z);
        float dxF = x - FarolBluffPos.x;
        float dzF = z - FarolBluffPos.y;
        float dFarolSq = dxF * dxF + dzF * dzF;
        if (coast >= 1.01f) return WorldRegion.DeepOcean;
        if (coast >= 0.93f && dFarolSq > 32f * 32f)
            return WorldRegion.AtlanticCoast;

        if ((x - CrashCampPos.x) * (x - CrashCampPos.x) + (z - CrashCampPos.y) * (z - CrashCampPos.y) < 28f * 28f) return WorldRegion.CrashCamp;
        if (dFarolSq < 34f * 34f) return WorldRegion.FarolBluff;
        if ((x - CelticCastroPos.x) * (x - CelticCastroPos.x) + (z - CelticCastroPos.y) * (z - CelticCastroPos.y) < 42f * 42f) return WorldRegion.CelticCastro;
        if ((x - SacredCavePos.x) * (x - SacredCavePos.x) + (z - SacredCavePos.y) * (z - SacredCavePos.y) < 28f * 28f) return WorldRegion.SacredCave;
        if ((x - HighlandLakePos.x) * (x - HighlandLakePos.x) + (z - HighlandLakePos.y) * (z - HighlandLakePos.y) < 52f * 52f) return WorldRegion.HighlandLake;
        if ((x - MountainSummitPos.x) * (x - MountainSummitPos.x) + (z - MountainSummitPos.y) * (z - MountainSummitPos.y) < 45f * 45f) return WorldRegion.MountainSummit;
        if ((x - SwampBasinPos.x) * (x - SwampBasinPos.x) + (z - SwampBasinPos.y) * (z - SwampBasinPos.y) < 82f * 82f) return WorldRegion.SwampBasin;

        float h = SampleHeight(x, z);
        if (h > 13.5f || z > Height * 0.62f) return WorldRegion.PineRidge;
        return WorldRegion.OakForest;
    }

    public static string GetRegionDisplayName(WorldRegion region)
    {
        switch (region)
        {
            case WorldRegion.CrashCamp:      return "Acampamento da Queda";
            case WorldRegion.OakForest:      return "Fraga dos Carvalhos";
            case WorldRegion.PineRidge:      return "Serra dos Pinheiros";
            case WorldRegion.SwampBasin:     return "Pântano das Meigas";
            case WorldRegion.CelticCastro:   return "Castro Celta Antigo";
            case WorldRegion.FarolBluff:     return "Penhasco do Farol";
            case WorldRegion.HighlandLake:   return "Lago da Fraga";
            case WorldRegion.SacredCave:     return "Gruta do Ancião";
            case WorldRegion.MountainSummit: return "Pico da Serra";
            case WorldRegion.AtlanticCoast:  return "Costa Atlântica";
            case WorldRegion.DeepOcean:      return "Mar Atlântico";
            default:                         return "Terras da Fraga";
        }
    }

    /// <summary>
    /// Grava a geografia coerente diretamente na grade da DLL nativa (FragaNative.dll).
    /// Com o cache espacial pré-assado executa em poucos milissegundos.
    /// </summary>
    public static void StampNativeMapGrid(IntPtr mapDataPtr, int width, int height)
    {
        if (mapDataPtr == IntPtr.Zero) return;
        EnsureInitialized();

        int[] buffer = new int[width * height];
        for (int y = 0; y < height; y++)
        {
            float wz = y + 0.5f;
            int rowOff = y * width;
            for (int x = 0; x < width; x++)
            {
                float wx = x + 0.5f;
                float coast = GetIslandCoastMetric(wx, wz);
                float waterDepth = GetWaterDepth(wx, wz);

                int cellID;
                if (coast >= 0.99f || waterDepth > 0.35f)
                {
                    cellID = 7;
                }
                else if (GetTrailWeight(wx, wz) > 0.25f)
                {
                    cellID = 0;
                }
                else
                {
                    WorldRegion reg = GetRegion(wx, wz);
                    if (reg == WorldRegion.CrashCamp || reg == WorldRegion.AtlanticCoast || reg == WorldRegion.MountainSummit)
                    {
                        cellID = 0;
                    }
                    else if (reg == WorldRegion.CelticCastro)
                    {
                        float dx = wx - CelticCastroPos.x;
                        float dz = wz - CelticCastroPos.y;
                        float dSq = dx * dx + dz * dz;
                        cellID = (dSq > 18f * 18f && dSq < 30f * 30f && ((x * 13 + y * 29) % 19 == 0)) ? 2 : 0;
                    }
                    else if (reg == WorldRegion.FarolBluff)
                    {
                        float dx = wx - FarolBluffPos.x;
                        float dz = wz - FarolBluffPos.y;
                        cellID = ((dx * dx + dz * dz) > 14f * 14f && ((x * 7 + y * 11) % 23 == 0)) ? 8 : 0;
                    }
                    else if (reg == WorldRegion.SacredCave)
                    {
                        cellID = ((x * 17 + y * 7) % 13 == 0) ? 8 : 0;
                    }
                    else
                    {
                        float meadowNoise = Mathf.PerlinNoise(wx * 0.018f + 19f, wz * 0.018f + 43f);
                        if (meadowNoise < 0.32f)
                            cellID = 0;
                        else if (meadowNoise > 0.84f && ((x + y) % 11 == 0))
                            cellID = 8;
                        else
                            cellID = 1;
                    }
                }

                buffer[rowOff + x] = cellID;
            }
        }

        // Cópia direta de bloco para memória nativa em vez de 589k chamadas P/Invoke
        System.Runtime.InteropServices.Marshal.Copy(buffer, 0, mapDataPtr, buffer.Length);
    }

    /// <summary>
    /// Gera textura topográfica estilo carta de sobrevivência (256x256) para o Minimapa e Mapa Completo (M).
    /// </summary>
    public static Texture2D GetOrCreateWorldMapTexture(int size = 256)
    {
        EnsureInitialized();
        if (s_cachedMapTexture != null && s_cachedTexSeed == Seed && s_cachedMapTexture.width == size)
            return s_cachedMapTexture;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        Color oceanDeep = new Color(0.05f, 0.14f, 0.22f, 1f);
        Color oceanShallow = new Color(0.11f, 0.28f, 0.34f, 1f);
        Color sandCol = new Color(0.68f, 0.61f, 0.45f, 1f);
        Color grassLow = new Color(0.24f, 0.38f, 0.20f, 1f);
        Color pineHigh = new Color(0.16f, 0.27f, 0.17f, 1f);
        Color rockCol = new Color(0.45f, 0.44f, 0.43f, 1f);
        Color snowCol = new Color(0.84f, 0.86f, 0.89f, 1f);
        Color swampCol = new Color(0.21f, 0.30f, 0.19f, 1f);
        Color trailCol = new Color(0.58f, 0.46f, 0.30f, 1f);
        Color riverCol = new Color(0.16f, 0.44f, 0.56f, 1f);

        for (int py = 0; py < size; py++)
        {
            float wz = ((py + 0.5f) / size) * Height;
            for (int px = 0; px < size; px++)
            {
                float wx = ((px + 0.5f) / size) * Width;
                float h = SampleHeight(wx, wz);
                float waterD = GetWaterDepth(wx, wz);
                float coast = GetIslandCoastMetric(wx, wz);

                Color c;
                float dxF = wx - FarolBluffPos.x;
                float dzF = wz - FarolBluffPos.y;
                if (coast >= 0.97f && (dxF * dxF + dzF * dzF) > 22f * 22f)
                {
                    float shallowT = Mathf.Clamp01(1f - (coast - 0.97f) * 6f);
                    c = Color.Lerp(oceanDeep, oceanShallow, shallowT);
                }
                else if (waterD > 0.12f)
                {
                    c = Color.Lerp(oceanShallow, riverCol, Mathf.Clamp01(waterD));
                }
                else
                {
                    WorldRegion reg = GetRegion(wx, wz);
                    if (h < SeaLevel + 0.9f && coast > 0.86f)
                        c = sandCol;
                    else if (reg == WorldRegion.SwampBasin)
                        c = swampCol;
                    else if (h > 25f)
                        c = Color.Lerp(rockCol, snowCol, Mathf.Clamp01((h - 25f) / 6f));
                    else if (h > 14f)
                        c = Color.Lerp(pineHigh, rockCol, Mathf.Clamp01((h - 14f) / 11f));
                    else
                        c = Color.Lerp(grassLow, pineHigh, Mathf.Clamp01((h - 3f) / 11f));

                    float trail = GetTrailWeight(wx, wz);
                    if (trail > 0.2f)
                        c = Color.Lerp(c, trailCol, trail * 0.72f);

                    float modH = Mathf.Abs(Mathf.Repeat(h, 4.0f) - 2.0f);
                    if (modH > 1.84f)
                        c *= 0.86f;
                }

                if (Mathf.Abs(coast - 0.985f) < 0.012f)
                    c = Color.Lerp(c, new Color(0.10f, 0.08f, 0.06f, 1f), 0.55f);

                pixels[py * size + px] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        s_cachedMapTexture = tex;
        s_cachedTexSeed = Seed;
        return tex;
    }
}
