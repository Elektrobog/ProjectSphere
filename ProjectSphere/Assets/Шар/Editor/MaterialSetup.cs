#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public static class MaterialSetup
{
    private const string TEXTURES_DIR = "Assets/Textures";
    private const string MATERIALS_DIR = "Assets/Materials";
    private const int TEX_SIZE = 256;

    // ============================================================
    //  ЦВЕТА
    // ============================================================
    private static Color plateColor = new Color(0.82f, 0.82f, 0.85f, 1f);
    private static Color neckBaseColor = plateColor;
    private static Color outlineColor = new Color(0.25f, 0.55f, 1.00f, 1f);
    private static Color neckStripeCol = new Color(0.30f, 0.65f, 1.00f, 1f);

    // ============================================================
    //  ПОЛОСА НА КОЛОННЕ
    // ============================================================
    private static float stripeWidth = 0.22f;
    private static float stripeSoftness = 0.10f;
    private static float stripePeak = 0.55f;
    private static float stripeSpread = 0.45f;
    private static float stripeVerticalSoftness = 0.15f;

    // ============================================================
    //  СВЕЧЕНИЕ ПЛИТЫ (новый блок)
    // ============================================================
    [Tooltip("Цвет свечения в центре плиты. Обычно = цвет полосы на колонне.")]
    private static Color plateGlowColor = new Color(0.30f, 0.65f, 1.00f, 1f);

    [Tooltip("Интенсивность свечения плиты. 0 = выключено, 0.5 = слабо, 1.5 = заметно.")]
    private static float plateEmissionIntensity = 0.8f;

    [Tooltip("Радиус свечения от центра плиты (доля UV).\n" +
             "0.15 = маленькая точка, 0.5 = половина плиты, 1.0 = вся плита.")]
    private static float plateGlowRadius = 0.35f;

    [Tooltip("Мягкость края свечения. Больше = плавнее переход к серому.")]
    private static float plateGlowSoftness = 0.5f;

    // ============================================================
    //  ИНТЕНСИВНОСТЬ СВЕЧЕНИЯ
    // ============================================================
    private static float neckEmissionIntensity = 2.5f;
    private static float outlineEmissionIntensity = 1.2f;

    // ============================================================

    [MenuItem("Tools/Ball Plates/Setup Materials")]
    public static void Setup()
    {
        EnsureFolder("Assets", "Textures");
        EnsureFolder("Assets", "Materials");

        Texture2D plateTex = CreateSolidTexture("PlateColor", plateColor);
        Texture2D outlineTex = CreateSolidTexture("OutlineColor", outlineColor);
        Texture2D neckStripeTex = CreateNeckStripeMask();
        Texture2D plateGlowTex = CreatePlateGlowMask();

        AssetDatabase.Refresh();

        // Плита: base = серый, emission = лёгкое свечение в центре
        Material plateMat = CreateURPMaterial("PlateMaterial", plateTex, plateColor, plateGlowTex, plateGlowColor, plateEmissionIntensity);
        Material outlineMat = CreateURPMaterial("OutlineMaterial", outlineTex, outlineColor, null, outlineColor, outlineEmissionIntensity);
        Material neckMat = CreateURPMaterial("NeckMaterial", null, neckBaseColor, neckStripeTex, neckStripeCol, neckEmissionIntensity);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green>[MaterialSetup]</color> Материалы обновлены.");
        Selection.objects = new Object[] { plateMat, neckMat, outlineMat };
    }

    // ============================================================
    //  ТЕКСТУРЫ
    // ============================================================

    private static Texture2D CreateSolidTexture(string name, Color col)
    {
        string path = $"{TEXTURES_DIR}/{name}.png";
        Texture2D tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
        Color[] px = new Color[TEX_SIZE * TEX_SIZE];
        for (int i = 0; i < px.Length; i++) px[i] = col;
        tex.SetPixels(px);
        tex.Apply();
        SavePNG(tex, path);
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>Маска полосы на колонне (как раньше).</summary>
    private static Texture2D CreateNeckStripeMask()
    {
        string path = $"{TEXTURES_DIR}/NeckMask.png";
        Texture2D tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
        Color[] px = new Color[TEX_SIZE * TEX_SIZE];

        float stripeMin = 0.5f - stripeWidth * 0.5f;
        float stripeMax = 0.5f + stripeWidth * 0.5f;
        float hSoft = Mathf.Max(0.0001f, stripeSoftness);
        float vSoft = Mathf.Max(0.0001f, stripeVerticalSoftness);

        for (int y = 0; y < TEX_SIZE; y++)
        {
            float v = y / (TEX_SIZE - 1f);
            float distToPeak = Mathf.Abs(v - stripePeak);

            float vFactor;
            if (distToPeak <= stripeSpread)
                vFactor = 1f;
            else
                vFactor = Mathf.Clamp01(1f - (distToPeak - stripeSpread) / vSoft);

            float edgeFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / 0.05f))
                           * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - v) / 0.05f));
            vFactor *= edgeFade;

            for (int x = 0; x < TEX_SIZE; x++)
            {
                float u = x / (TEX_SIZE - 1f);
                float uFactor;
                if (u < stripeMin)
                    uFactor = Mathf.Clamp01(1f - (stripeMin - u) / hSoft);
                else if (u > stripeMax)
                    uFactor = Mathf.Clamp01(1f - (u - stripeMax) / hSoft);
                else
                    uFactor = 1f;

                float val = uFactor * vFactor;
                px[y * TEX_SIZE + x] = new Color(val, val, val, 1f);
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        SavePNG(tex, path);
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>
    /// Маска свечения плиты: радиальный градиент от центра UV.
    /// Белый в центре (плита светится там, где в неё входит колонна) → чёрный к краям.
    /// </summary>
    private static Texture2D CreatePlateGlowMask()
    {
        string path = $"{TEXTURES_DIR}/PlateGlow.png";
        Texture2D tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
        Color[] px = new Color[TEX_SIZE * TEX_SIZE];

        float cx = 0.5f;
        float cy = 0.5f;
        float r = Mathf.Max(0.0001f, plateGlowRadius);
        float soft = Mathf.Max(0.0001f, plateGlowSoftness);

        for (int y = 0; y < TEX_SIZE; y++)
        {
            for (int x = 0; x < TEX_SIZE; x++)
            {
                float u = x / (TEX_SIZE - 1f);
                float v = y / (TEX_SIZE - 1f);

                float dx = u - cx;
                float dy = v - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // 1 в центре, плавно падает до 0 за пределами r
                float val;
                if (dist <= r)
                    val = 1f;
                else
                    val = Mathf.Clamp01(1f - (dist - r) / soft);

                px[y * TEX_SIZE + x] = new Color(val, val, val, 1f);
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        SavePNG(tex, path);
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void SavePNG(Texture2D tex, string path)
    {
        byte[] png = tex.EncodeToPNG();
        File.WriteAllBytes(path, png);
    }

    // ============================================================
    //  МАТЕРИАЛ
    // ============================================================
    private static Material CreateURPMaterial(
        string name,
        Texture2D baseTex,
        Color baseColor,
        Texture2D emissionTex,
        Color emissionColor,
        float emissionIntensity)
    {
        string path = $"{MATERIALS_DIR}/{name}.mat";

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[MaterialSetup] Не найден URP/Lit шейдер.");
            return null;
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }

        // Base
        if (baseTex != null) mat.SetTexture("_BaseMap", baseTex);
        else mat.SetTexture("_BaseMap", null);
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.15f);

        // Emission
        if (emissionIntensity > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            if (emissionTex != null) mat.SetTexture("_EmissionMap", emissionTex);
            else mat.SetTexture("_EmissionMap", null);

            Color hdr = emissionColor * emissionIntensity;
            hdr.a = 1f;
            mat.SetColor("_EmissionColor", hdr);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", null);
            mat.SetColor("_EmissionColor", Color.black);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string full = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(full))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif