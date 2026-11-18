using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Biblioteca central de icones vetoriais procedurais de alta definicao (Zero-Emoji, Cacheada).
/// Renderiza icones SDF anti-serrilhados uma unica vez por chave para manter 60 FPS no Mobile e 120 FPS no PC.
/// </summary>
public static class FragaProIconLibrary
{
    public enum ControlIcon
    {
        AttackAxe,
        AttackBow,
        AttackSpear,
        AttackKnife,
        AttackPickaxe,
        AttackRod,
        AttackHoe,
        AttackFist,
        Jump,
        Crouch,
        Interact,
        ShieldBlock,
        Lantern,
        Backpack,
        RadialWheel,
        BuildHammer,
        MapCompass,
        DropItem,
        SplitStack,
        CookFire,
        Workbench,
        Cauldron,
        PauseMenu,
        DodgeStep,
        RotateLeft,
        RotateRight,
        ConfirmCheck,
        CancelCross,
        HeartPulse,
        HungerFork,
        WaterDrop,
        SanityEye,
        Thermometer,
        BookmarkPin,
        UsersSquad,
        SettingsGear,
        PlayArrow
    }

    private static readonly Dictionary<ControlIcon, Sprite> s_controlCache = new Dictionary<ControlIcon, Sprite>();
    private static readonly Dictionary<int, Sprite> s_itemCache = new Dictionary<int, Sprite>();
    private static Sprite s_slotFrameSprite;
    private static Sprite s_circleBgSprite;
    private static Sprite s_pillBgSprite;
    private static Sprite s_glassPanelSprite;
    private static Sprite s_verticalGradSprite;
    private static Sprite s_blueprintGridSprite;
    private static Sprite s_anatomySprite;
    private static Sprite s_barFillSprite;

    private const int ICON_SIZE = 96;

    public static Sprite GetCircleBackground()
    {
        if (s_circleBgSprite != null) return s_circleBgSprite;

        int s = 128;
        Color[] px = new Color[s * s];
        Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
        float rOuter = s * 0.47f;
        float rInner = s * 0.43f;

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float alpha = 1f - Smooth(rOuter - 1.8f, rOuter, d);
                if (alpha <= 0.001f)
                {
                    px[y * s + x] = Color.clear;
                    continue;
                }
                float ring = Smooth(rInner - 1.5f, rInner, d) * (1f - Smooth(rOuter - 2.0f, rOuter, d));
                float shade = Mathf.Lerp(0.88f, 1.0f, 1f - (d / rOuter) * 0.35f) + ring * 0.18f;
                px[y * s + x] = new Color(shade, shade, shade, alpha);
            }
        }

        s_circleBgSprite = BakeSprite("Pro_CircleBG", s, s, px);
        return s_circleBgSprite;
    }

    public static Sprite GetSlotFrameSprite()
    {
        if (s_slotFrameSprite != null) return s_slotFrameSprite;

        int s = 96;
        Color[] px = new Color[s * s];
        Vector2 half = new Vector2(s * 0.5f, s * 0.5f);
        Vector2 box = new Vector2(s * 0.44f, s * 0.44f);

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - half;
                float d = SdRoundBox(p, box, 10f);
                float fill = 1f - Smooth(-1.2f, 0.8f, d);
                float border = Smooth(-3.2f, -1.5f, d) * (1f - Smooth(-0.5f, 1.0f, d));
                float col = Mathf.Lerp(0.82f, 1.0f, border);
                px[y * s + x] = new Color(col, col, col, fill);
            }
        }

        s_slotFrameSprite = BakeSprite("Pro_SlotFrame", s, s, px, new Vector4(16, 16, 16, 16));
        return s_slotFrameSprite;
    }

    public static Sprite GetPillSprite()
    {
        if (s_pillBgSprite != null) return s_pillBgSprite;

        int w = 128, h = 48;
        Color[] px = new Color[w * h];
        Vector2 half = new Vector2(w * 0.5f, h * 0.5f);
        Vector2 box = new Vector2(w * 0.46f, h * 0.42f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - half;
                float d = SdRoundBox(p, box, 18f);
                float a = 1f - Smooth(-1.2f, 0.8f, d);
                px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
        }

        s_pillBgSprite = BakeSprite("Pro_PillBG", w, h, px, new Vector4(20, 14, 20, 14));
        return s_pillBgSprite;
    }

    public static Sprite GetGlassPanelSprite()
    {
        if (s_glassPanelSprite != null) return s_glassPanelSprite;

        int s = 96;
        Color[] px = new Color[s * s];
        Vector2 half = new Vector2(s * 0.5f, s * 0.5f);
        Vector2 box = new Vector2(s * 0.46f, s * 0.46f);

        for (int y = 0; y < s; y++)
        {
            float yNorm = y / (float)(s - 1);
            for (int x = 0; x < s; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - half;
                float d = SdRoundBox(p, box, 12f);
                float fill = 1f - Smooth(-1.2f, 0.8f, d);
                float border = Smooth(-2.8f, -1.2f, d) * (1f - Smooth(-0.4f, 0.9f, d));
                float topSheen = Mathf.Lerp(0.76f, 0.96f, yNorm);
                float col = Mathf.Clamp01(topSheen + border * 0.24f);
                px[y * s + x] = new Color(col, col, col, fill);
            }
        }

        s_glassPanelSprite = BakeSprite("Pro_GlassPanel", s, s, px, new Vector4(18, 18, 18, 18));
        return s_glassPanelSprite;
    }

    public static Sprite GetVerticalGradientSprite()
    {
        if (s_verticalGradSprite != null) return s_verticalGradSprite;

        int w = 4, h = 128;
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = y / (float)(h - 1);
            float a = Mathf.SmoothStep(0f, 1f, t);
            for (int x = 0; x < w; x++)
            {
                px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
        }

        s_verticalGradSprite = BakeSprite("Pro_VertGrad", w, h, px);
        return s_verticalGradSprite;
    }

    public static Sprite GetBlueprintGridSprite()
    {
        if (s_blueprintGridSprite != null) return s_blueprintGridSprite;

        int s = 64;
        Color[] px = new Color[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                bool major = (x == 0 || y == 0);
                bool minor = (x % 16 == 0 || y % 16 == 0);
                float a = major ? 0.24f : (minor ? 0.10f : 0.02f);
                px[y * s + x] = new Color(1f, 1f, 1f, a);
            }
        }

        s_blueprintGridSprite = BakeSprite("Pro_BlueprintGrid", s, s, px);
        return s_blueprintGridSprite;
    }

    public static Sprite GetAnatomySilhouetteSprite()
    {
        if (s_anatomySprite != null) return s_anatomySprite;

        int w = 128, h = 192;
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float py = ((y + 0.5f) / h) * 2f - 1f;
            for (int x = 0; x < w; x++)
            {
                float pxn = (((x + 0.5f) / w) * 2f - 1f) * (w / (float)h);
                Vector2 p = new Vector2(pxn, py);

                float head = SdCircle(p - new Vector2(0f, 0.72f), 0.15f);
                float neck = SdRoundBox(p - new Vector2(0f, 0.54f), new Vector2(0.065f, 0.08f), 0.02f);
                float chest = SdRoundBox(p - new Vector2(0f, 0.24f), new Vector2(0.22f, 0.26f), 0.09f);
                float waist = SdRoundBox(p - new Vector2(0f, -0.06f), new Vector2(0.17f, 0.14f), 0.06f);
                float armL = SdSegment(p, new Vector2(-0.22f, 0.40f), new Vector2(-0.34f, -0.14f), 0.062f);
                float armR = SdSegment(p, new Vector2(0.22f, 0.40f), new Vector2(0.34f, -0.14f), 0.062f);
                float legL = SdSegment(p, new Vector2(-0.10f, -0.14f), new Vector2(-0.14f, -0.86f), 0.078f);
                float legR = SdSegment(p, new Vector2(0.10f, -0.14f), new Vector2(0.14f, -0.86f), 0.078f);

                float body = Mathf.Min(head, neck);
                body = Mathf.Min(body, Mathf.Min(chest, waist));
                body = Mathf.Min(body, Mathf.Min(armL, armR));
                body = Mathf.Min(body, Mathf.Min(legL, legR));

                float fill = 1f - Smooth(-0.018f, 0.018f, body);
                float rim = Smooth(-0.045f, -0.015f, body) * (1f - Smooth(-0.005f, 0.018f, body));
                float shade = Mathf.Lerp(0.72f, 1.0f, rim);
                px[y * w + x] = new Color(shade, shade, shade, fill);
            }
        }

        s_anatomySprite = BakeSprite("Pro_AnatomyBody", w, h, px);
        return s_anatomySprite;
    }

    public static Sprite GetBarFillSprite()
    {
        if (s_barFillSprite != null) return s_barFillSprite;

        int w = 32, h = 16;
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float yn = y / (float)(h - 1);
            // Brilho sutil no topo da barra para visual AAA polido
            float sheen = yn >= 0.62f ? Mathf.Lerp(0.92f, 1.0f, (yn - 0.62f) / 0.38f) : Mathf.Lerp(0.78f, 0.92f, yn / 0.62f);
            float alphaEdge = (y == 0 || y == h - 1) ? 0.72f : 1f;
            for (int x = 0; x < w; x++)
            {
                px[y * w + x] = new Color(sheen, sheen, sheen, alphaEdge);
            }
        }

        s_barFillSprite = BakeSprite("Pro_BarFill", w, h, px);
        return s_barFillSprite;
    }

    public static Sprite GetControlIcon(ControlIcon icon)
    {
        if (s_controlCache.TryGetValue(icon, out Sprite cached) && cached != null)
            return cached;

        Color[] px = new Color[ICON_SIZE * ICON_SIZE];
        RenderControlIcon(icon, px);
        Sprite sp = BakeSprite($"ProCtrl_{icon}", ICON_SIZE, ICON_SIZE, px);
        s_controlCache[icon] = sp;
        return sp;
    }

    public static Sprite GetItemIcon(int itemId)
    {
        if (s_itemCache.TryGetValue(itemId, out Sprite cached) && cached != null)
            return cached;

        Color[] px = new Color[ICON_SIZE * ICON_SIZE];
        RenderItemIcon(itemId, px);
        Sprite sp = BakeSprite($"ProItem_{itemId}", ICON_SIZE, ICON_SIZE, px);
        s_itemCache[itemId] = sp;
        return sp;
    }

    // =================== RENDERIZACAO VETORIAL SDF DOS CONTROLES ===================

    private static void RenderControlIcon(ControlIcon icon, Color[] px)
    {
        Color main = new Color(0.95f, 0.97f, 1.0f, 1f);
        Color accent = new Color(0.38f, 0.88f, 0.98f, 1f);
        Color warm = new Color(1.0f, 0.72f, 0.28f, 1f);
        Color steel = new Color(0.78f, 0.84f, 0.90f, 1f);
        Color wood = new Color(0.76f, 0.54f, 0.34f, 1f);

        for (int y = 0; y < ICON_SIZE; y++)
        {
            for (int x = 0; x < ICON_SIZE; x++)
            {
                Vector2 p = ToNorm(x, y);
                Color outCol = Color.clear;

                switch (icon)
                {
                    case ControlIcon.AttackAxe:
                    {
                        float handle = SdSegment(p, new Vector2(-0.55f, -0.58f), new Vector2(0.32f, 0.42f), 0.065f);
                        float head = SdBox(Rot(p - new Vector2(0.22f, 0.28f), -0.68f), new Vector2(0.24f, 0.16f));
                        float edge = SdCircle(p - new Vector2(0.42f, 0.10f), 0.22f);
                        float blade = Mathf.Max(head, -edge + 0.08f);
                        outCol = Blend(outCol, wood, Fill(handle));
                        outCol = Blend(outCol, steel, Fill(Mathf.Min(head, blade)));
                        break;
                    }
                    case ControlIcon.AttackPickaxe:
                    {
                        float handle = SdSegment(p, new Vector2(-0.52f, -0.55f), new Vector2(0.36f, 0.38f), 0.06f);
                        float arc = Mathf.Abs(SdCircle(p - new Vector2(0.05f, -0.02f), 0.52f)) - 0.055f;
                        float clip = SdSegment(p, new Vector2(-0.18f, 0.52f), new Vector2(0.55f, -0.18f), 0.22f);
                        float pickHead = Mathf.Max(arc, clip);
                        outCol = Blend(outCol, wood, Fill(handle));
                        outCol = Blend(outCol, steel, Fill(pickHead));
                        break;
                    }
                    case ControlIcon.AttackKnife:
                    {
                        float grip = SdSegment(p, new Vector2(-0.50f, -0.50f), new Vector2(-0.18f, -0.18f), 0.065f);
                        float guard = SdSegment(p, new Vector2(-0.30f, -0.06f), new Vector2(-0.06f, -0.30f), 0.045f);
                        float blade = SdSegment(p, new Vector2(-0.12f, -0.12f), new Vector2(0.52f, 0.52f), 0.085f);
                        float bevel = SdBox(Rot(p - new Vector2(0.22f, 0.22f), -0.785f), new Vector2(0.38f, 0.07f));
                        outCol = Blend(outCol, wood, Fill(grip));
                        outCol = Blend(outCol, warm, Fill(guard));
                        outCol = Blend(outCol, steel, Fill(Mathf.Min(blade, bevel)));
                        break;
                    }
                    case ControlIcon.AttackSpear:
                    {
                        float shaft = SdSegment(p, new Vector2(-0.62f, -0.62f), new Vector2(0.34f, 0.34f), 0.045f);
                        float tip = SdTriangle(Rot(p - new Vector2(0.46f, 0.46f), 0.785f), 0.20f, 0.28f);
                        outCol = Blend(outCol, wood, Fill(shaft));
                        outCol = Blend(outCol, steel, Fill(tip));
                        break;
                    }
                    case ControlIcon.AttackBow:
                    {
                        float bowRing = Mathf.Abs(SdCircle(p - new Vector2(-0.32f, 0f), 0.62f)) - 0.048f;
                        float bowClip = Mathf.Max(bowRing, -p.x - 0.08f);
                        float cord = SdSegment(p, new Vector2(-0.06f, 0.55f), new Vector2(-0.06f, -0.55f), 0.018f);
                        float arrow = SdSegment(p, new Vector2(-0.36f, 0f), new Vector2(0.56f, 0f), 0.032f);
                        float tip = SdTriangle(Rot(p - new Vector2(0.54f, 0f), 1.57f), 0.12f, 0.16f);
                        outCol = Blend(outCol, main, Fill(cord));
                        outCol = Blend(outCol, wood, Fill(bowClip));
                        outCol = Blend(outCol, steel, Fill(Mathf.Min(arrow, tip)));
                        break;
                    }
                    case ControlIcon.AttackRod:
                    {
                        float rod = SdSegment(p, new Vector2(-0.52f, -0.52f), new Vector2(0.35f, 0.46f), 0.042f);
                        float line = SdSegment(p, new Vector2(0.35f, 0.46f), new Vector2(0.35f, -0.25f), 0.018f);
                        float hook = Mathf.Max(Mathf.Abs(SdCircle(p - new Vector2(0.24f, -0.32f), 0.11f)) - 0.028f, p.y + 0.28f);
                        outCol = Blend(outCol, wood, Fill(rod));
                        outCol = Blend(outCol, accent, Fill(Mathf.Min(line, hook)));
                        break;
                    }
                    case ControlIcon.AttackHoe:
                    {
                        float handle = SdSegment(p, new Vector2(-0.54f, -0.54f), new Vector2(0.36f, 0.36f), 0.052f);
                        float blade = SdSegment(p, new Vector2(0.28f, 0.38f), new Vector2(0.52f, -0.02f), 0.07f);
                        outCol = Blend(outCol, wood, Fill(handle));
                        outCol = Blend(outCol, steel, Fill(blade));
                        break;
                    }
                    case ControlIcon.AttackFist:
                    {
                        float palm = SdRoundBox(p - new Vector2(0f, -0.05f), new Vector2(0.30f, 0.28f), 0.12f);
                        float thumb = SdRoundBox(Rot(p - new Vector2(-0.28f, -0.08f), 0.3f), new Vector2(0.10f, 0.16f), 0.05f);
                        float knuckle = SdRoundBox(p - new Vector2(0f, 0.24f), new Vector2(0.28f, 0.08f), 0.04f);
                        outCol = Blend(outCol, main, Fill(Mathf.Min(palm, Mathf.Min(thumb, knuckle))));
                        break;
                    }
                    case ControlIcon.Jump:
                    {
                        float c1 = SdSegment(p, new Vector2(-0.40f, -0.05f), new Vector2(0f, 0.35f), 0.08f);
                        float c2 = SdSegment(p, new Vector2(0f, 0.35f), new Vector2(0.40f, -0.05f), 0.08f);
                        float c3 = SdSegment(p, new Vector2(-0.28f, -0.36f), new Vector2(0f, -0.08f), 0.065f);
                        float c4 = SdSegment(p, new Vector2(0f, -0.08f), new Vector2(0.28f, -0.36f), 0.065f);
                        outCol = Blend(outCol, main, Fill(Mathf.Min(c1, c2)));
                        outCol = Blend(outCol, accent, Fill(Mathf.Min(c3, c4)));
                        break;
                    }
                    case ControlIcon.Crouch:
                    {
                        float c1 = SdSegment(p, new Vector2(-0.38f, 0.24f), new Vector2(0f, -0.14f), 0.075f);
                        float c2 = SdSegment(p, new Vector2(0f, -0.14f), new Vector2(0.38f, 0.24f), 0.075f);
                        float baseBar = SdSegment(p, new Vector2(-0.38f, -0.36f), new Vector2(0.38f, -0.36f), 0.065f);
                        outCol = Blend(outCol, main, Fill(Mathf.Min(c1, c2)));
                        outCol = Blend(outCol, accent, Fill(baseBar));
                        break;
                    }
                    case ControlIcon.Interact:
                    {
                        float ring = Mathf.Abs(SdRoundBox(Rot(p, 0.785f), new Vector2(0.36f, 0.36f), 0.06f)) - 0.055f;
                        float dot = SdCircle(p, 0.14f);
                        outCol = Blend(outCol, main, Fill(ring));
                        outCol = Blend(outCol, accent, Fill(dot));
                        break;
                    }
                    case ControlIcon.ShieldBlock:
                    {
                        float top = SdRoundBox(p - new Vector2(0f, 0.14f), new Vector2(0.36f, 0.30f), 0.06f);
                        float tip = SdTriangle(Rot(p - new Vector2(0f, -0.24f), Mathf.PI), 0.36f, 0.38f);
                        float shield = Mathf.Min(top, tip);
                        float inner = shield + 0.09f;
                        float boss = SdCircle(p - new Vector2(0f, 0.05f), 0.11f);
                        outCol = Blend(outCol, main, Fill(shield));
                        outCol = Blend(outCol, new Color(0.18f, 0.42f, 0.56f, 1f), Fill(inner));
                        outCol = Blend(outCol, accent, Fill(boss));
                        break;
                    }
                    case ControlIcon.Lantern:
                    {
                        float topCap = SdRoundBox(p - new Vector2(0f, 0.36f), new Vector2(0.24f, 0.06f), 0.02f);
                        float botCap = SdRoundBox(p - new Vector2(0f, -0.40f), new Vector2(0.28f, 0.07f), 0.02f);
                        float cage = Mathf.Abs(SdRoundBox(p - new Vector2(0f, -0.02f), new Vector2(0.24f, 0.32f), 0.04f)) - 0.038f;
                        float handle = Mathf.Max(Mathf.Abs(SdCircle(p - new Vector2(0f, 0.44f), 0.16f)) - 0.03f, -p.y + 0.42f);
                        float flame = SdCircle(new Vector2(p.x * 1.4f, p.y + 0.03f), 0.14f);
                        outCol = Blend(outCol, steel, Fill(Mathf.Min(Mathf.Min(topCap, botCap), Mathf.Min(cage, handle))));
                        outCol = Blend(outCol, warm, Fill(flame));
                        break;
                    }
                    case ControlIcon.Backpack:
                    {
                        float body = SdRoundBox(p - new Vector2(0f, -0.06f), new Vector2(0.36f, 0.42f), 0.12f);
                        float flap = SdRoundBox(p - new Vector2(0f, 0.16f), new Vector2(0.34f, 0.18f), 0.08f);
                        float pocket = SdRoundBox(p - new Vector2(0f, -0.22f), new Vector2(0.24f, 0.14f), 0.05f);
                        float strap = Mathf.Max(Mathf.Abs(SdCircle(p - new Vector2(0f, 0.38f), 0.16f)) - 0.04f, -p.y + 0.36f);
                        outCol = Blend(outCol, wood, Fill(Mathf.Min(body, strap)));
                        outCol = Blend(outCol, main, Fill(flap) * 0.92f);
                        outCol = Blend(outCol, accent, Fill(pocket));
                        break;
                    }
                    case ControlIcon.RadialWheel:
                    {
                        float outer = Mathf.Abs(SdCircle(p, 0.42f)) - 0.07f;
                        float inner = SdCircle(p, 0.12f);
                        float s1 = SdSegment(p, Vector2.zero, new Vector2(0f, 0.48f), 0.025f);
                        float s2 = SdSegment(p, Vector2.zero, new Vector2(0.42f, -0.24f), 0.025f);
                        float s3 = SdSegment(p, Vector2.zero, new Vector2(-0.42f, -0.24f), 0.025f);
                        float cuts = Mathf.Min(s1, Mathf.Min(s2, s3));
                        outCol = Blend(outCol, main, Fill(Mathf.Max(outer, -cuts)));
                        outCol = Blend(outCol, accent, Fill(inner));
                        break;
                    }
                    case ControlIcon.BuildHammer:
                    {
                        float handle = SdSegment(p, new Vector2(-0.48f, -0.48f), new Vector2(0.26f, 0.26f), 0.06f);
                        float head = SdRoundBox(Rot(p - new Vector2(0.28f, 0.28f), -0.785f), new Vector2(0.14f, 0.32f), 0.04f);
                        outCol = Blend(outCol, wood, Fill(handle));
                        outCol = Blend(outCol, accent, Fill(head));
                        break;
                    }
                    case ControlIcon.MapCompass:
                    {
                        float ring = Mathf.Abs(SdCircle(p, 0.46f)) - 0.05f;
                        float nTop = SdTriangle(p - new Vector2(0f, 0.14f), 0.14f, 0.30f);
                        float nBot = SdTriangle(Rot(p - new Vector2(0f, -0.14f), Mathf.PI), 0.14f, 0.30f);
                        outCol = Blend(outCol, main, Fill(ring));
                        outCol = Blend(outCol, warm, Fill(nTop));
                        outCol = Blend(outCol, accent, Fill(nBot));
                        break;
                    }
                    case ControlIcon.DodgeStep:
                    {
                        float a1 = SdSegment(p, new Vector2(-0.42f, 0f), new Vector2(0.18f, 0f), 0.06f);
                        float a2 = SdTriangle(Rot(p - new Vector2(0.28f, 0f), 1.57f), 0.18f, 0.22f);
                        float trail = SdSegment(p, new Vector2(-0.55f, 0.22f), new Vector2(-0.15f, 0.22f), 0.035f);
                        float trail2 = SdSegment(p, new Vector2(-0.55f, -0.22f), new Vector2(-0.15f, -0.22f), 0.035f);
                        outCol = Blend(outCol, main, Fill(Mathf.Min(a1, a2)));
                        outCol = Blend(outCol, accent, Fill(Mathf.Min(trail, trail2)));
                        break;
                    }
                    case ControlIcon.CookFire:
                    {
                        float fOuter = SdCircle(new Vector2(p.x * 1.25f, p.y + 0.08f), 0.34f);
                        float fTip = SdTriangle(p - new Vector2(0f, 0.26f), 0.26f, 0.38f);
                        float fCore = SdCircle(new Vector2(p.x * 1.35f, p.y + 0.14f), 0.17f);
                        float logs = SdSegment(p, new Vector2(-0.42f, -0.44f), new Vector2(0.42f, -0.44f), 0.06f);
                        outCol = Blend(outCol, wood, Fill(logs));
                        outCol = Blend(outCol, warm, Fill(Mathf.Min(fOuter, fTip)));
                        outCol = Blend(outCol, main, Fill(fCore));
                        break;
                    }
                    case ControlIcon.Workbench:
                    {
                        float top = SdRoundBox(p - new Vector2(0f, 0.14f), new Vector2(0.52f, 0.11f), 0.03f);
                        float legL = SdSegment(p, new Vector2(-0.36f, 0.10f), new Vector2(-0.42f, -0.46f), 0.065f);
                        float legR = SdSegment(p, new Vector2(0.36f, 0.10f), new Vector2(0.42f, -0.46f), 0.065f);
                        float cross = SdSegment(p, new Vector2(-0.36f, -0.18f), new Vector2(0.36f, -0.18f), 0.045f);
                        outCol = Blend(outCol, wood, Fill(Mathf.Min(Mathf.Min(top, cross), Mathf.Min(legL, legR))));
                        break;
                    }
                    case ControlIcon.Cauldron:
                    {
                        float pot = SdCircle(p - new Vector2(0f, -0.06f), 0.40f);
                        float rim = SdRoundBox(p - new Vector2(0f, 0.26f), new Vector2(0.42f, 0.07f), 0.03f);
                        float cutTop = -p.y + 0.28f;
                        float body = Mathf.Min(Mathf.Max(pot, cutTop), rim);
                        float brew = SdCircle(p - new Vector2(0.10f, 0.42f), 0.08f);
                        float brew2 = SdCircle(p - new Vector2(-0.14f, 0.50f), 0.06f);
                        outCol = Blend(outCol, steel, Fill(body));
                        outCol = Blend(outCol, accent, Fill(Mathf.Min(brew, brew2)));
                        break;
                    }
                    case ControlIcon.DropItem:
                    {
                        float shaft = SdSegment(p, new Vector2(0f, 0.42f), new Vector2(0f, -0.12f), 0.07f);
                        float tip = SdTriangle(Rot(p - new Vector2(0f, -0.18f), Mathf.PI), 0.24f, 0.24f);
                        float tray = SdSegment(p, new Vector2(-0.44f, -0.44f), new Vector2(0.44f, -0.44f), 0.055f);
                        outCol = Blend(outCol, main, Fill(Mathf.Min(shaft, tip)));
                        outCol = Blend(outCol, warm, Fill(tray));
                        break;
                    }
                    case ControlIcon.RotateLeft:
                    case ControlIcon.RotateRight:
                    {
                        float sx = (icon == ControlIcon.RotateLeft) ? -1f : 1f;
                        Vector2 q = new Vector2(p.x * sx, p.y);
                        float arc = Mathf.Abs(SdCircle(q, 0.36f)) - 0.06f;
                        float clip = Mathf.Max(arc, -(q.y < -0.12f && q.x > 0f ? -1f : 1f));
                        float tip = SdTriangle(Rot(q - new Vector2(0.36f, 0.02f), Mathf.PI), 0.16f, 0.18f);
                        outCol = Blend(outCol, main, Fill(Mathf.Min(clip, tip)));
                        break;
                    }
                    case ControlIcon.PauseMenu:
                    {
                        float b1 = SdRoundBox(p - new Vector2(-0.18f, 0f), new Vector2(0.09f, 0.36f), 0.03f);
                        float b2 = SdRoundBox(p - new Vector2(0.18f, 0f), new Vector2(0.09f, 0.36f), 0.03f);
                        outCol = Blend(outCol, main, Fill(Mathf.Min(b1, b2)));
                        break;
                    }
                    case ControlIcon.ConfirmCheck:
                    {
                        float s1 = SdSegment(p, new Vector2(-0.38f, 0.02f), new Vector2(-0.10f, -0.28f), 0.08f);
                        float s2 = SdSegment(p, new Vector2(-0.10f, -0.28f), new Vector2(0.40f, 0.30f), 0.08f);
                        outCol = Blend(outCol, new Color(0.35f, 0.95f, 0.55f, 1f), Fill(Mathf.Min(s1, s2)));
                        break;
                    }
                    case ControlIcon.CancelCross:
                    {
                        float s1 = SdSegment(p, new Vector2(-0.34f, -0.34f), new Vector2(0.34f, 0.34f), 0.08f);
                        float s2 = SdSegment(p, new Vector2(-0.34f, 0.34f), new Vector2(0.34f, -0.34f), 0.08f);
                        outCol = Blend(outCol, new Color(1.0f, 0.38f, 0.38f, 1f), Fill(Mathf.Min(s1, s2)));
                        break;
                    }
                    case ControlIcon.HeartPulse:
                    {
                        float cL = SdCircle(p - new Vector2(-0.20f, 0.16f), 0.25f);
                        float cR = SdCircle(p - new Vector2(0.20f, 0.16f), 0.25f);
                        float bot = SdTriangle(Rot(p - new Vector2(0f, -0.12f), Mathf.PI), 0.44f, 0.48f);
                        float heart = Mathf.Min(Mathf.Min(cL, cR), bot);
                        float e1 = SdSegment(p, new Vector2(-0.34f, 0.0f), new Vector2(-0.14f, 0.0f), 0.032f);
                        float e2 = SdSegment(p, new Vector2(-0.14f, 0.0f), new Vector2(-0.04f, 0.22f), 0.032f);
                        float e3 = SdSegment(p, new Vector2(-0.04f, 0.22f), new Vector2(0.08f, -0.16f), 0.032f);
                        float e4 = SdSegment(p, new Vector2(0.08f, -0.16f), new Vector2(0.18f, 0.0f), 0.032f);
                        float e5 = SdSegment(p, new Vector2(0.18f, 0.0f), new Vector2(0.34f, 0.0f), 0.032f);
                        float ecg = Mathf.Min(Mathf.Min(e1, e2), Mathf.Min(e3, Mathf.Min(e4, e5)));
                        outCol = Blend(outCol, new Color(0.98f, 0.28f, 0.32f, 1f), Fill(heart));
                        outCol = Blend(outCol, main, Fill(ecg));
                        break;
                    }
                    case ControlIcon.HungerFork:
                    {
                        float handle = SdSegment(p, new Vector2(-0.42f, -0.46f), new Vector2(0.16f, 0.12f), 0.055f);
                        float meat = SdCircle(p - new Vector2(0.22f, 0.20f), 0.28f);
                        float boneTip = SdCircle(p - new Vector2(-0.46f, -0.48f), 0.09f);
                        outCol = Blend(outCol, main, Fill(Mathf.Min(handle, boneTip)));
                        outCol = Blend(outCol, warm, Fill(meat));
                        break;
                    }
                    case ControlIcon.WaterDrop:
                    {
                        float bulb = SdCircle(p - new Vector2(0f, -0.14f), 0.34f);
                        float tip = SdTriangle(p - new Vector2(0f, 0.22f), 0.30f, 0.44f);
                        float shine = SdCircle(p - new Vector2(-0.10f, -0.10f), 0.09f);
                        outCol = Blend(outCol, new Color(0.24f, 0.78f, 1.0f, 1f), Fill(Mathf.Min(bulb, tip)));
                        outCol = Blend(outCol, main, Fill(shine) * 0.85f);
                        break;
                    }
                    case ControlIcon.SanityEye:
                    {
                        float topArc = SdCircle(p - new Vector2(0f, -0.44f), 0.68f);
                        float botArc = SdCircle(p - new Vector2(0f, 0.44f), 0.68f);
                        float eyeShape = Mathf.Max(topArc, botArc);
                        float iris = SdCircle(p, 0.22f);
                        float pupil = SdCircle(p, 0.10f);
                        outCol = Blend(outCol, main, Fill(eyeShape));
                        outCol = Blend(outCol, new Color(0.68f, 0.42f, 0.96f, 1f), Fill(iris));
                        outCol = Blend(outCol, new Color(0.06f, 0.05f, 0.10f, 1f), Fill(pupil));
                        break;
                    }
                    case ControlIcon.Thermometer:
                    {
                        float stem = SdSegment(p, new Vector2(0f, -0.22f), new Vector2(0f, 0.44f), 0.12f);
                        float bulb = SdCircle(p - new Vector2(0f, -0.30f), 0.22f);
                        float mercStem = SdSegment(p, new Vector2(0f, -0.28f), new Vector2(0f, 0.24f), 0.055f);
                        float mercBulb = SdCircle(p - new Vector2(0f, -0.30f), 0.13f);
                        outCol = Blend(outCol, main, Fill(Mathf.Min(stem, bulb)));
                        outCol = Blend(outCol, new Color(1.0f, 0.46f, 0.24f, 1f), Fill(Mathf.Min(mercStem, mercBulb)));
                        break;
                    }
                    case ControlIcon.BookmarkPin:
                    {
                        float body = SdRoundBox(p - new Vector2(0f, 0.08f), new Vector2(0.28f, 0.44f), 0.05f);
                        float notch = SdTriangle(p - new Vector2(0f, -0.38f), 0.28f, 0.24f);
                        float tag = Mathf.Max(body, -notch);
                        float star = SdCircle(p - new Vector2(0f, 0.14f), 0.10f);
                        outCol = Blend(outCol, warm, Fill(tag));
                        outCol = Blend(outCol, main, Fill(star));
                        break;
                    }
                    case ControlIcon.UsersSquad:
                    {
                        float h1 = SdCircle(p - new Vector2(-0.18f, 0.24f), 0.16f);
                        float b1 = SdRoundBox(p - new Vector2(-0.18f, -0.18f), new Vector2(0.24f, 0.20f), 0.10f);
                        float h2 = SdCircle(p - new Vector2(0.24f, 0.16f), 0.13f);
                        float b2 = SdRoundBox(p - new Vector2(0.24f, -0.22f), new Vector2(0.20f, 0.16f), 0.08f);
                        outCol = Blend(outCol, accent, Fill(Mathf.Min(h2, b2)));
                        outCol = Blend(outCol, main, Fill(Mathf.Min(h1, b1)));
                        break;
                    }
                    case ControlIcon.SettingsGear:
                    {
                        float ring = Mathf.Abs(SdCircle(p, 0.30f)) - 0.11f;
                        float t1 = SdRoundBox(p, new Vector2(0.52f, 0.10f), 0.03f);
                        float t2 = SdRoundBox(p, new Vector2(0.10f, 0.52f), 0.03f);
                        float t3 = SdRoundBox(Rot(p, 0.785f), new Vector2(0.50f, 0.10f), 0.03f);
                        float t4 = SdRoundBox(Rot(p, 0.785f), new Vector2(0.10f, 0.50f), 0.03f);
                        float gear = Mathf.Max(Mathf.Min(ring, Mathf.Min(Mathf.Min(t1, t2), Mathf.Min(t3, t4))), -SdCircle(p, 0.15f));
                        outCol = Blend(outCol, steel, Fill(gear));
                        break;
                    }
                    case ControlIcon.PlayArrow:
                    {
                        float tri = SdTriangle(Rot(p - new Vector2(0.06f, 0f), 1.5708f), 0.42f, 0.54f);
                        outCol = Blend(outCol, main, Fill(tri));
                        break;
                    }
                }

                px[y * ICON_SIZE + x] = outCol;
            }
        }
    }

    // =================== RENDERIZACAO VETORIAL SDF DOS ITENS ===================

    private static void RenderItemIcon(int itemId, Color[] px)
    {
        switch (itemId)
        {
            case 300: RenderControlIcon(ControlIcon.AttackAxe, px); return;
            case 301: RenderControlIcon(ControlIcon.AttackPickaxe, px); return;
            case 302: RenderControlIcon(ControlIcon.AttackKnife, px); return;
            case 304: RenderControlIcon(ControlIcon.Lantern, px); return;
            case 305: RenderControlIcon(ControlIcon.AttackBow, px); return;
            case 306: RenderControlIcon(ControlIcon.AttackRod, px); return;
            case 307: RenderControlIcon(ControlIcon.AttackHoe, px); return;
            case 308: RenderControlIcon(ControlIcon.AttackSpear, px); return;
            case 309: RenderControlIcon(ControlIcon.ShieldBlock, px); return;
            case 400: RenderControlIcon(ControlIcon.CookFire, px); return;
            case 404: RenderControlIcon(ControlIcon.Workbench, px); return;
            case 405: RenderControlIcon(ControlIcon.Cauldron, px); return;
        }

        Color wood = new Color(0.72f, 0.48f, 0.28f, 1f);
        Color stone = new Color(0.68f, 0.72f, 0.76f, 1f);
        Color leaf = new Color(0.32f, 0.85f, 0.46f, 1f);
        Color meat = new Color(0.86f, 0.32f, 0.28f, 1f);
        Color cooked = new Color(0.78f, 0.46f, 0.22f, 1f);
        Color water = new Color(0.28f, 0.80f, 1.0f, 1f);
        Color gold = new Color(1.0f, 0.78f, 0.28f, 1f);
        Color white = new Color(0.95f, 0.97f, 1.0f, 1f);
        Color purple = new Color(0.76f, 0.42f, 0.95f, 1f);
        Color boneCol = new Color(0.92f, 0.88f, 0.76f, 1f);
        Color cyanSteel = new Color(0.54f, 0.88f, 0.98f, 1f);

        for (int y = 0; y < ICON_SIZE; y++)
        {
            for (int x = 0; x < ICON_SIZE; x++)
            {
                Vector2 p = ToNorm(x, y);
                Color outCol = Color.clear;

                switch (itemId)
                {
                    case 100: // Madeira
                    {
                        float log = SdRoundBox(Rot(p, 0.4f), new Vector2(0.46f, 0.18f), 0.08f);
                        float ring = SdCircle(p - new Vector2(0.36f, 0.16f), 0.14f);
                        outCol = Blend(outCol, wood, Fill(log));
                        outCol = Blend(outCol, new Color(0.92f, 0.76f, 0.52f, 1f), Fill(ring));
                        break;
                    }
                    case 101: // Pedra
                    {
                        float r1 = SdRoundBox(Rot(p - new Vector2(-0.04f, -0.02f), 0.25f), new Vector2(0.38f, 0.30f), 0.14f);
                        float r2 = SdCircle(p - new Vector2(0.18f, 0.12f), 0.22f);
                        outCol = Blend(outCol, stone, Fill(Mathf.Min(r1, r2)));
                        break;
                    }
                    case 102: // Oleo de Lampiao
                    case 109: // Agua Limpa
                    case 123: // Agua Turva
                    case 204: // Agua Benta
                    case 214: // Tonico da Fraga
                    {
                        Color liq = itemId == 102 ? gold :
                            (itemId == 123 ? new Color(0.46f, 0.52f, 0.32f, 1f) :
                            (itemId == 204 ? new Color(0.60f, 0.96f, 1.0f, 1f) :
                            (itemId == 214 ? new Color(0.38f, 0.95f, 0.72f, 1f) : water)));
                        float flask = SdRoundBox(p - new Vector2(0f, -0.10f), new Vector2(0.30f, 0.34f), 0.14f);
                        float neck = SdRoundBox(p - new Vector2(0f, 0.34f), new Vector2(0.11f, 0.16f), 0.03f);
                        outCol = Blend(outCol, white * 0.9f, Fill(Mathf.Min(flask, neck)));
                        outCol = Blend(outCol, liq, Fill(flask + 0.06f));
                        break;
                    }
                    case 103: // Ervas
                    case 118: // Corda / Fibra
                    {
                        float l1 = SdCircle(new Vector2((p.x + 0.10f) * 1.4f, p.y - 0.04f), 0.32f);
                        float l2 = SdCircle(new Vector2((p.x - 0.14f) * 1.4f, p.y + 0.06f), 0.26f);
                        float stem = SdSegment(p, new Vector2(-0.22f, -0.44f), new Vector2(0.14f, 0.28f), 0.03f);
                        Color c = itemId == 118 ? gold : leaf;
                        outCol = Blend(outCol, c, Fill(Mathf.Min(l1, l2)));
                        outCol = Blend(outCol, white, Fill(stem) * 0.7f);
                        break;
                    }
                    case 104: // Carne Crua
                    case 113: // Carne Leve
                    case 200: // Carne Assada
                    case 212: // Charque Salgado
                    {
                        Color mc = itemId == 200 ? cooked : (itemId == 212 ? new Color(0.56f, 0.22f, 0.16f, 1f) : meat);
                        float cut = SdRoundBox(Rot(p, -0.35f), new Vector2(0.40f, 0.26f), 0.16f);
                        outCol = Blend(outCol, mc, Fill(cut));
                        if (itemId == 212)
                        {
                            float s1 = SdCircle(p - new Vector2(-0.14f, 0.04f), 0.055f);
                            float s2 = SdCircle(p - new Vector2(0.10f, -0.05f), 0.055f);
                            float s3 = SdCircle(p - new Vector2(0.02f, 0.10f), 0.045f);
                            outCol = Blend(outCol, white, Fill(Mathf.Min(s1, Mathf.Min(s2, s3))));
                        }
                        else
                        {
                            float bone = SdCircle(p - new Vector2(0.12f, 0.02f), 0.09f);
                            outCol = Blend(outCol, white, Fill(bone));
                        }
                        break;
                    }
                    case 105: // Pele de Animal
                    case 310: // Casaco de Pele
                    {
                        float body = SdRoundBox(p - new Vector2(0f, -0.02f), new Vector2(0.32f, 0.42f), 0.10f);
                        float arms = SdRoundBox(p - new Vector2(0f, 0.18f), new Vector2(0.50f, 0.16f), 0.07f);
                        Color fur = itemId == 310 ? new Color(0.58f, 0.42f, 0.28f, 1f) : new Color(0.76f, 0.56f, 0.38f, 1f);
                        outCol = Blend(outCol, fur, Fill(Mathf.Min(body, arms)));
                        if (itemId == 310)
                            outCol = Blend(outCol, gold, Fill(SdSegment(p, new Vector2(0f, 0.36f), new Vector2(0f, -0.40f), 0.035f)));
                        break;
                    }
                    case 106: // Sal
                    case 402: // Circulo de Sal
                    {
                        if (itemId == 402)
                        {
                            float ring = Mathf.Abs(SdCircle(p, 0.42f)) - 0.07f;
                            outCol = Blend(outCol, white, Fill(ring));
                        }
                        else
                        {
                            float crystal = SdRoundBox(Rot(p, 0.785f), new Vector2(0.30f, 0.30f), 0.04f);
                            outCol = Blend(outCol, white, Fill(crystal));
                        }
                        break;
                    }
                    case 107: // Ferro Velho
                    {
                        float bar = SdRoundBox(Rot(p, 0.35f), new Vector2(0.44f, 0.16f), 0.04f);
                        outCol = Blend(outCol, new Color(0.62f, 0.68f, 0.78f, 1f), Fill(bar));
                        break;
                    }
                    case 108: // Comida Podre
                    {
                        float blob = SdCircle(p, 0.36f);
                        float s1 = SdCircle(p - new Vector2(-0.14f, 0.08f), 0.08f);
                        float s2 = SdCircle(p - new Vector2(0.14f, 0.08f), 0.08f);
                        outCol = Blend(outCol, new Color(0.46f, 0.56f, 0.24f, 1f), Fill(blob));
                        outCol = Blend(outCol, new Color(0.12f, 0.14f, 0.08f, 1f), Fill(Mathf.Min(s1, s2)));
                        break;
                    }
                    case 110: // Flechas de Pedra
                    case 117: // Flecha Penada
                    {
                        float shaft = SdSegment(p, new Vector2(-0.50f, -0.50f), new Vector2(0.38f, 0.38f), 0.038f);
                        float tip = SdTriangle(Rot(p - new Vector2(0.44f, 0.44f), 0.785f), 0.14f, 0.20f);
                        float fletch = SdRoundBox(Rot(p - new Vector2(-0.42f, -0.42f), 0.785f), new Vector2(0.14f, 0.12f), 0.02f);
                        outCol = Blend(outCol, wood, Fill(shaft));
                        outCol = Blend(outCol, itemId == 117 ? gold : stone, Fill(tip));
                        outCol = Blend(outCol, itemId == 117 ? water : white, Fill(fletch));
                        break;
                    }
                    case 111: // Peixe Cru
                    case 207: // Peixe Assado
                    {
                        Color fc = itemId == 207 ? cooked : new Color(0.55f, 0.78f, 0.92f, 1f);
                        float fish = SdCircle(new Vector2((p.x - 0.06f) * 0.8f, p.y * 1.4f), 0.32f);
                        float tail = SdTriangle(Rot(p - new Vector2(-0.42f, 0f), 1.57f), 0.18f, 0.20f);
                        outCol = Blend(outCol, fc, Fill(Mathf.Min(fish, tail)));
                        break;
                    }
                    case 112: // Sementes
                    {
                        float s1 = SdCircle(p - new Vector2(-0.18f, 0.12f), 0.11f);
                        float s2 = SdCircle(p - new Vector2(0.18f, 0.16f), 0.10f);
                        float s3 = SdCircle(p - new Vector2(0f, -0.18f), 0.12f);
                        outCol = Blend(outCol, gold, Fill(Mathf.Min(s1, Mathf.Min(s2, s3))));
                        break;
                    }
                    case 114: // Pena de Aguia
                    {
                        float quill = SdSegment(p, new Vector2(-0.48f, -0.48f), new Vector2(0.45f, 0.45f), 0.03f);
                        float plume = SdRoundBox(Rot(p - new Vector2(0.06f, 0.06f), -0.785f), new Vector2(0.42f, 0.14f), 0.12f);
                        outCol = Blend(outCol, white, Fill(plume));
                        outCol = Blend(outCol, gold, Fill(quill));
                        break;
                    }
                    case 115: // Coracao Sombrio
                    {
                        float core = SdCircle(p, 0.36f);
                        float aura = Mathf.Abs(SdCircle(p, 0.48f)) - 0.04f;
                        outCol = Blend(outCol, purple, Fill(aura));
                        outCol = Blend(outCol, new Color(0.15f, 0.04f, 0.24f, 1f), Fill(core));
                        break;
                    }
                    case 116: // Pagina do Diario
                    {
                        float page = SdRoundBox(p, new Vector2(0.32f, 0.42f), 0.03f);
                        float l1 = SdSegment(p, new Vector2(-0.20f, 0.18f), new Vector2(0.20f, 0.18f), 0.025f);
                        float l2 = SdSegment(p, new Vector2(-0.20f, 0f), new Vector2(0.20f, 0f), 0.025f);
                        float l3 = SdSegment(p, new Vector2(-0.20f, -0.18f), new Vector2(0.12f, -0.18f), 0.025f);
                        outCol = Blend(outCol, new Color(0.92f, 0.86f, 0.72f, 1f), Fill(page));
                        outCol = Blend(outCol, new Color(0.22f, 0.18f, 0.14f, 1f), Fill(Mathf.Min(l1, Mathf.Min(l2, l3))));
                        break;
                    }
                    case 119: // Resina de Pinheiro (gota ambar)
                    {
                        float drop = SdCircle(p - new Vector2(0f, -0.12f), 0.30f);
                        float tip = SdTriangle(p - new Vector2(0f, 0.18f), 0.24f, 0.34f);
                        float shine = SdCircle(p - new Vector2(-0.08f, -0.06f), 0.08f);
                        outCol = Blend(outCol, new Color(0.96f, 0.56f, 0.12f, 1f), Fill(Mathf.Min(drop, tip)));
                        outCol = Blend(outCol, gold, Fill(shine));
                        break;
                    }
                    case 120: // Osso
                    {
                        float shaft = SdSegment(p, new Vector2(-0.34f, -0.34f), new Vector2(0.34f, 0.34f), 0.085f);
                        float k1 = SdCircle(p - new Vector2(-0.40f, -0.26f), 0.11f);
                        float k2 = SdCircle(p - new Vector2(-0.26f, -0.40f), 0.11f);
                        float k3 = SdCircle(p - new Vector2(0.40f, 0.26f), 0.11f);
                        float k4 = SdCircle(p - new Vector2(0.26f, 0.40f), 0.11f);
                        float bone = Mathf.Min(shaft, Mathf.Min(Mathf.Min(k1, k2), Mathf.Min(k3, k4)));
                        outCol = Blend(outCol, boneCol, Fill(bone));
                        break;
                    }
                    case 121: // Tecido Rustico
                    {
                        float cloth = SdRoundBox(Rot(p, 0.12f), new Vector2(0.38f, 0.32f), 0.06f);
                        float weave1 = SdSegment(p, new Vector2(-0.26f, 0.10f), new Vector2(0.26f, 0.10f), 0.025f);
                        float weave2 = SdSegment(p, new Vector2(-0.26f, -0.10f), new Vector2(0.26f, -0.10f), 0.025f);
                        outCol = Blend(outCol, new Color(0.84f, 0.78f, 0.66f, 1f), Fill(cloth));
                        outCol = Blend(outCol, new Color(0.56f, 0.48f, 0.36f, 1f), Fill(Mathf.Min(weave1, weave2)));
                        break;
                    }
                    case 122: // Carvao Vegetal
                    {
                        float c1 = SdRoundBox(Rot(p - new Vector2(-0.05f, 0f), 0.35f), new Vector2(0.36f, 0.24f), 0.10f);
                        float c2 = SdCircle(p - new Vector2(0.18f, 0.10f), 0.18f);
                        float ember = SdCircle(p - new Vector2(-0.06f, -0.05f), 0.08f);
                        outCol = Blend(outCol, new Color(0.18f, 0.19f, 0.22f, 1f), Fill(Mathf.Min(c1, c2)));
                        outCol = Blend(outCol, new Color(1.0f, 0.44f, 0.12f, 1f), Fill(ember));
                        break;
                    }
                    case 201: // Sopa
                    case 208: // Ensopado Druida
                    case 211: // Cha de Ervas Quente
                    {
                        float bowl = Mathf.Max(SdCircle(p - new Vector2(0f, 0.02f), 0.42f), p.y - 0.05f);
                        float soup = SdRoundBox(p - new Vector2(0f, 0.02f), new Vector2(0.36f, 0.07f), 0.03f);
                        outCol = Blend(outCol, wood, Fill(bowl));
                        Color liqCol = itemId == 208 ? gold : (itemId == 211 ? new Color(0.95f, 0.56f, 0.20f, 1f) : leaf);
                        outCol = Blend(outCol, liqCol, Fill(soup));
                        if (itemId == 211)
                        {
                            float s1 = SdSegment(p, new Vector2(-0.12f, 0.18f), new Vector2(-0.08f, 0.42f), 0.028f);
                            float s2 = SdSegment(p, new Vector2(0.10f, 0.20f), new Vector2(0.14f, 0.44f), 0.028f);
                            outCol = Blend(outCol, white, Fill(Mathf.Min(s1, s2)) * 0.8f);
                        }
                        break;
                    }
                    case 202: // Bagas
                    {
                        float b1 = SdCircle(p - new Vector2(-0.16f, -0.06f), 0.18f);
                        float b2 = SdCircle(p - new Vector2(0.18f, -0.08f), 0.17f);
                        float b3 = SdCircle(p - new Vector2(0f, 0.18f), 0.18f);
                        outCol = Blend(outCol, new Color(0.36f, 0.48f, 0.96f, 1f), Fill(Mathf.Min(b1, Mathf.Min(b2, b3))));
                        break;
                    }
                    case 203: // Cogumelo
                    {
                        float cap = Mathf.Max(SdCircle(p - new Vector2(0f, 0.04f), 0.40f), -p.y + 0.02f);
                        float stem = SdRoundBox(p - new Vector2(0f, -0.22f), new Vector2(0.12f, 0.24f), 0.05f);
                        outCol = Blend(outCol, white, Fill(stem));
                        outCol = Blend(outCol, purple, Fill(cap));
                        break;
                    }
                    case 205: // Fosforos
                    {
                        float stick = SdSegment(p, new Vector2(-0.34f, -0.44f), new Vector2(0.24f, 0.24f), 0.05f);
                        float tip = SdCircle(p - new Vector2(0.26f, 0.28f), 0.13f);
                        outCol = Blend(outCol, wood, Fill(stick));
                        outCol = Blend(outCol, meat, Fill(tip));
                        break;
                    }
                    case 206: // Ligaduras
                    {
                        float pad = SdRoundBox(p, new Vector2(0.38f, 0.34f), 0.08f);
                        float hBar = SdRoundBox(p, new Vector2(0.22f, 0.07f), 0.02f);
                        float vBar = SdRoundBox(p, new Vector2(0.07f, 0.22f), 0.02f);
                        outCol = Blend(outCol, white, Fill(pad));
                        outCol = Blend(outCol, meat, Fill(Mathf.Min(hBar, vBar)));
                        break;
                    }
                    case 209: // Tala de Madeira
                    {
                        float p1 = SdRoundBox(p - new Vector2(-0.14f, 0f), new Vector2(0.10f, 0.46f), 0.03f);
                        float p2 = SdRoundBox(p - new Vector2(0.14f, 0f), new Vector2(0.10f, 0.46f), 0.03f);
                        float wrap = SdRoundBox(p, new Vector2(0.28f, 0.12f), 0.03f);
                        outCol = Blend(outCol, wood, Fill(Mathf.Min(p1, p2)));
                        outCol = Blend(outCol, white, Fill(wrap));
                        break;
                    }
                    case 210: // Cantil de Couro
                    {
                        float pouch = SdCircle(p - new Vector2(0f, -0.08f), 0.36f);
                        float neck = SdRoundBox(p - new Vector2(0f, 0.30f), new Vector2(0.10f, 0.12f), 0.03f);
                        float strap = Mathf.Abs(SdCircle(p - new Vector2(0f, 0.02f), 0.44f)) - 0.035f;
                        float drop = SdCircle(p - new Vector2(0f, -0.08f), 0.14f);
                        outCol = Blend(outCol, new Color(0.58f, 0.36f, 0.18f, 1f), Fill(Mathf.Min(pouch, neck)));
                        outCol = Blend(outCol, gold, Fill(strap) * 0.8f);
                        outCol = Blend(outCol, water, Fill(drop));
                        break;
                    }
                    case 213: // Pomada de Resina
                    {
                        float jar = SdRoundBox(p - new Vector2(0f, -0.08f), new Vector2(0.32f, 0.28f), 0.08f);
                        float lid = SdRoundBox(p - new Vector2(0f, 0.24f), new Vector2(0.26f, 0.07f), 0.03f);
                        float crossH = SdRoundBox(p - new Vector2(0f, -0.08f), new Vector2(0.16f, 0.05f), 0.01f);
                        float crossV = SdRoundBox(p - new Vector2(0f, -0.08f), new Vector2(0.05f, 0.16f), 0.01f);
                        outCol = Blend(outCol, new Color(0.92f, 0.60f, 0.16f, 1f), Fill(jar));
                        outCol = Blend(outCol, wood, Fill(lid));
                        outCol = Blend(outCol, leaf, Fill(Mathf.Min(crossH, crossV)));
                        break;
                    }
                    case 303: // Tocha
                    {
                        float stick = SdSegment(p, new Vector2(-0.38f, -0.48f), new Vector2(0.16f, 0.16f), 0.06f);
                        float flame = SdCircle(p - new Vector2(0.24f, 0.28f), 0.20f);
                        outCol = Blend(outCol, wood, Fill(stick));
                        outCol = Blend(outCol, gold, Fill(flame));
                        break;
                    }
                    case 311: // Clava de Osso
                    {
                        float shaft = SdSegment(p, new Vector2(-0.52f, -0.52f), new Vector2(0.28f, 0.28f), 0.085f);
                        float clubHead = SdCircle(p - new Vector2(0.26f, 0.26f), 0.22f);
                        float knob = SdCircle(p - new Vector2(0.42f, 0.12f), 0.09f);
                        outCol = Blend(outCol, boneCol, Fill(Mathf.Min(shaft, Mathf.Min(clubHead, knob))));
                        outCol = Blend(outCol, wood, Fill(SdSegment(p, new Vector2(-0.52f, -0.52f), new Vector2(-0.22f, -0.22f), 0.09f)));
                        break;
                    }
                    case 312: // Machado de Ferro Forjado
                    {
                        float handle = SdSegment(p, new Vector2(-0.55f, -0.58f), new Vector2(0.32f, 0.42f), 0.065f);
                        float head = SdBox(Rot(p - new Vector2(0.22f, 0.28f), -0.68f), new Vector2(0.26f, 0.19f));
                        float edge = SdCircle(p - new Vector2(0.45f, 0.10f), 0.22f);
                        float blade = Mathf.Max(head, -edge + 0.08f);
                        outCol = Blend(outCol, new Color(0.55f, 0.34f, 0.18f, 1f), Fill(handle));
                        outCol = Blend(outCol, cyanSteel, Fill(Mathf.Min(head, blade)));
                        break;
                    }
                    case 313: // Armadura de Ossos
                    {
                        float plate = SdRoundBox(p - new Vector2(0f, -0.02f), new Vector2(0.34f, 0.40f), 0.10f);
                        float rib1 = SdSegment(p, new Vector2(-0.24f, 0.16f), new Vector2(0.24f, 0.16f), 0.045f);
                        float rib2 = SdSegment(p, new Vector2(-0.24f, 0.00f), new Vector2(0.24f, 0.00f), 0.045f);
                        float rib3 = SdSegment(p, new Vector2(-0.20f, -0.16f), new Vector2(0.20f, -0.16f), 0.045f);
                        outCol = Blend(outCol, new Color(0.46f, 0.30f, 0.18f, 1f), Fill(plate));
                        outCol = Blend(outCol, boneCol, Fill(Mathf.Min(rib1, Mathf.Min(rib2, rib3))));
                        break;
                    }
                    case 314: // Funda Celta (couro + cordoes + pedra)
                    {
                        float cordL = SdSegment(p, new Vector2(-0.36f, -0.44f), new Vector2(-0.14f, 0.04f), 0.045f);
                        float cordR = SdSegment(p, new Vector2(0.36f, -0.44f), new Vector2(0.14f, 0.04f), 0.045f);
                        float pouch = SdRoundBox(p - new Vector2(0f, 0.16f), new Vector2(0.24f, 0.15f), 0.10f);
                        float pebble = SdCircle(p - new Vector2(0f, 0.22f), 0.14f);
                        outCol = Blend(outCol, wood, Fill(Mathf.Min(cordL, cordR)));
                        outCol = Blend(outCol, new Color(0.54f, 0.34f, 0.18f, 1f), Fill(pouch));
                        outCol = Blend(outCol, stone, Fill(pebble));
                        break;
                    }
                    case 401: // Armadilha
                    case 406: // Laco
                    {
                        float ring = Mathf.Abs(SdCircle(p, 0.40f)) - 0.065f;
                        float teeth = SdTriangle(p - new Vector2(0f, 0.18f), 0.12f, 0.14f);
                        outCol = Blend(outCol, itemId == 406 ? gold : stone, Fill(Mathf.Min(ring, teeth)));
                        break;
                    }
                    case 403: // Coletor de Agua
                    {
                        float cone = SdTriangle(Rot(p - new Vector2(0f, -0.05f), Mathf.PI), 0.42f, 0.42f);
                        float drop = SdCircle(p - new Vector2(0f, 0.34f), 0.11f);
                        outCol = Blend(outCol, wood, Fill(cone));
                        outCol = Blend(outCol, water, Fill(drop));
                        break;
                    }
                    default:
                    {
                        float box = SdRoundBox(p, new Vector2(0.36f, 0.36f), 0.08f);
                        outCol = Blend(outCol, white, Fill(box));
                        break;
                    }
                }

                px[y * ICON_SIZE + x] = outCol;
            }
        }
    }

    // =================== PRIMITIVAS SDF & BLEND ===================

    private static Vector2 ToNorm(int x, int y)
    {
        return new Vector2(((x + 0.5f) / ICON_SIZE) * 2f - 1f, ((y + 0.5f) / ICON_SIZE) * 2f - 1f);
    }

    private static Vector2 Rot(Vector2 p, float rad)
    {
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
        return new Vector2(c * p.x - s * p.y, s * p.x + c * p.y);
    }

    private static float SdCircle(Vector2 p, float r) => p.magnitude - r;

    private static float SdBox(Vector2 p, Vector2 b)
    {
        Vector2 d = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - b;
        return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
    }

    private static float SdRoundBox(Vector2 p, Vector2 b, float r)
    {
        return SdBox(p, b - Vector2.one * r) - r;
    }

    private static float SdSegment(Vector2 p, Vector2 a, Vector2 b, float r)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(0.0001f, Vector2.Dot(ba, ba)));
        return (pa - ba * h).magnitude - r;
    }

    private static float SdTriangle(Vector2 p, float halfBase, float height)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x), p.y);
        float d1 = q.y + height * 0.5f;
        float d2 = (q.x * height + (q.y - height * 0.5f) * halfBase) / Mathf.Sqrt(height * height + halfBase * halfBase);
        return Mathf.Max(-d1, d2);
    }

    private static float Fill(float sdf) => 1f - Smooth(-0.025f, 0.025f, sdf);

    private static float Smooth(float a, float b, float x)
    {
        if (Mathf.Approximately(a, b)) return x < a ? 0f : 1f;
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    private static Color Blend(Color dst, Color src, float alpha)
    {
        alpha = Mathf.Clamp01(alpha * src.a);
        if (alpha <= 0.001f) return dst;
        float outA = alpha + dst.a * (1f - alpha);
        if (outA <= 0.001f) return Color.clear;
        Vector3 rgb = (new Vector3(src.r, src.g, src.b) * alpha + new Vector3(dst.r, dst.g, dst.b) * dst.a * (1f - alpha)) / outA;
        return new Color(rgb.x, rgb.y, rgb.z, outA);
    }

    private static Sprite BakeSprite(string name, int w, int h, Color[] pixels, Vector4? border = null)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = name;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels(pixels);
        tex.Apply(false, false);

        if (border.HasValue)
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border.Value);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }
}
