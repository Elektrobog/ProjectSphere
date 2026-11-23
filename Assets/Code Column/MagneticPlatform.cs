using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Платформа в стиле колонны: графитовый корпус со скошенными или закруглёнными углами, фасками и светящимися канавками,
/// точный физический хитбокс по контуру и магнитное поле, выходящее из труб колонны (шейдер SciFi/MagneticFieldPlatform).
/// Реализует IPlatformVelocityProvider, чтобы шар корректно катался и удерживался при вращении платформы.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Custom/Магнитная платформа")]
public class MagneticPlatform : MonoBehaviour, IPlatformVelocityProvider
{
    public const string GenPrefix = "~gen_";

    public enum ColliderMode
    {
        [InspectorName("Точный по контуру (гладкая дека)")]
        ExactSmoothDeck = 0,

        [InspectorName("Точный по визуальному мешу (с канавками)")]
        ExactWithGrooves = 1,

        [InspectorName("Упрощённые BoxCollider")]
        CompoundBoxes = 2
    }

    [Tooltip("Сохранять постоянную ширину по всей длине (прямой мостик вместо веера).")]
    public bool constantWidth = true;

    [Tooltip("Угловая ширина основания платформы у колонны в градусах.")]
    public float arcDegrees = 36f;

    [Tooltip("Радиус внешней поверхности плит колонны в метрах.")]
    public float columnRadius = 5f;

    [Tooltip("Ширина зазора в метрах между колонной и платформой, где отображается магнитное поле.")]
    public float gap = 0.9f;

    [Tooltip("Длина платформы от внутреннего края наружу в метрах.")]
    public float depth = 4f;

    [Tooltip("Толщина (высота) платформы в метрах.")]
    public float thickness = 1.01f;

    [Tooltip("Ширина шва между плитами колонны (нужна, чтобы магнитные дуги выходили точно из горизонтальных колец труб).")]
    public float groove = 0.24f;

    [Tooltip("Размер среза или радиус закругления на дальних углах прямого мостика в метрах.")]
    public float cornerBevel = 0.35f;

    [Tooltip("Плавное закругление внешних углов по дуге вместо прямого скоса под 45°.")]
    public bool roundCorners = false;

    [Tooltip("Ширина декоративного скоса (фаски) по краям платформы в метрах.")]
    public float chamfer = 0.06f;

    [Tooltip("Количество поперечных канавок со светящимися полосами вдоль платформы (от 0 до 6).")]
    [Range(0, 6)] public int grooveCount = 3;

    [Tooltip("Отступ декоративных канавок от переднего и заднего краёв платформы в метрах.")]
    public float grooveInset = 0.45f;

    [Tooltip("Ширина декоративной канавки на поверхности платформы в метрах.")]
    public float grooveWidth = 0.10f;

    [Tooltip("Глубина декоративной канавки на поверхности платформы в метрах.")]
    public float grooveDepth = 0.045f;

    [Tooltip("Вставлять светящиеся неоновые полосы внутрь поперечных канавок.")]
    public bool glowGrooves = true;

    [Tooltip("Добавлять тонкие светящиеся вертикальные метки на боковых стенках платформы напротив канавок.")]
    public bool glowSideAccents = true;

    [Tooltip("Цвет светящихся вставок в канавках (используется, если не назначен отдельный материал подсветки).")]
    public Color glowColor = new Color(0.15f, 0.88f, 1f, 1f);

    [Tooltip("Интенсивность свечения (HDR Emission) неоновых полос.")]
    [Range(0f, 8f)] public float glowIntensity = 2.2f;

    [Tooltip("Высота дугообразного прогиба магнитных потоков от колец труб к платформе (м).")]
    public float fieldReach = 0.5f;

    [Tooltip("Количество внутренних радиальных слоёв плазмы между колонной и платформой (от 1 до 6).")]
    [Range(1, 6)] public int fieldLayers = 3;

    [Tooltip("Азимут первой вертикальной трубы колонны в радианах (передаётся спавнером для синхронизации шейдера поля).")]
    public float tubeAzimuth0Rad = 0f;

    [Tooltip("Направление нумерации вертикальных труб колонны (+1 или −1).")]
    public float tubeDirection = 1f;

    [Tooltip("Общее количество вертикальных труб по окружности колонны.")]
    public int tubeCount = 18;

    [Tooltip("Материал твёрдого корпуса платформы. Если пусто — создаётся стандартный материал в цвет колонны.")]
    public Material bodyMaterial;

    [Tooltip("Материал светящихся вставок в канавках. Если пусто — создаётся автоматически из цвета и яркости выше.")]
    public Material glowMaterial;

    [Tooltip("Материал магнитного поля с шейдером SciFi/MagneticFieldPlatform.")]
    public Material fieldMaterial;

    [Tooltip("Включить вращение платформы вокруг центральной оси колонны во время игры.")]
    public bool rotate = false;

    [Tooltip("Скорость вращения платформы в градусах за секунду (положительное значение = по часовой стрелке сверху).")]
    public float speedDegPerSec = 15f;

    [Tooltip("Вращать платформу в обратную сторону (против часовой стрелки при взгляде сверху).")]
    public bool reverse = false;

    [Tooltip("Угол поворота до возврата обратно (в градусах). Если 0 — платформа крутится бесконечно по кругу.")]
    [Range(0f, 360f)] public float rotationAngle = 0f;

    [Tooltip("Пауза в секундах в крайних точках перед движением в обратную сторону (работает при угле > 0).")]
    [Min(0f)] public float rotationPause = 0f;

    [Tooltip("Автоматически создавать физические коллайдеры по форме платформы.")]
    public bool addCollider = true;

    [Tooltip("Режим построения коллайдера платформы.")]
    public ColliderMode colliderMode = ColliderMode.ExactSmoothDeck;

    [Tooltip("Учитывать фаску по периметру в физическом хитбоксе.")]
    public bool colliderChamfer = true;

#if UNITY_6000_0_OR_NEWER
    [Tooltip("Физический материал (трение и упругость), назначаемый на коллайдер платформы.")]
    public PhysicsMaterial physicMaterial;
#else
    [Tooltip("Физический материал (трение и упругость), назначаемый на коллайдер платформы.")]
    public PhysicMaterial physicMaterial;
#endif

    [HideInInspector]
    public bool managedBySpawner = false;

    Rigidbody rb;

    // Состояние ограниченного (маятникового) вращения
    Quaternion startRotation;
    bool hasStartRotation;
    float currentAngleOffset;
    int pingPongDir = 1;
    float pauseTimer;
    float currentSignedSpeedDeg;

    // Кэш общих авто-материалов для снижения расхода памяти и улучшения батчинга
    static Material s_SharedBodyMaterial;
    static Material s_SharedFieldMaterial;
    static readonly Dictionary<int, Material> s_SharedGlowMaterials = new Dictionary<int, Material>();

    public static void ClearMaterialCache()
    {
        if (s_SharedBodyMaterial != null) { DestroyObj(s_SharedBodyMaterial); s_SharedBodyMaterial = null; }
        if (s_SharedFieldMaterial != null) { DestroyObj(s_SharedFieldMaterial); s_SharedFieldMaterial = null; }
        foreach (var kv in s_SharedGlowMaterials)
        {
            if (kv.Value != null) DestroyObj(kv.Value);
        }
        s_SharedGlowMaterials.Clear();
    }

    void OnEnable()
    {
        SyncRigidbodyState();
#if UNITY_EDITOR
        if (!Application.isPlaying) ScheduleRebuild();
#endif
    }

    void Start()
    {
        if (Application.isPlaying)
        {
            startRotation = transform.rotation;
            hasStartRotation = true;
            currentAngleOffset = 0f;
            pingPongDir = 1;
            pauseTimer = 0f;

            // Если платформа уже собрана в редакторе, просто синхронизируем физику без пересборки мешей
            if (transform.Find(GenPrefix + "Body") != null)
                SyncRigidbodyState();
            else
                Rebuild();
        }
    }

    void FixedUpdate()
    {
        if (!Application.isPlaying || !rotate || managedBySpawner) return;
        StepRotation(Time.fixedDeltaTime);
    }

    public void StepRotation(float dt)
    {
        if (rb == null) SyncRigidbodyState();
        if (rb == null) return;

        if (!hasStartRotation)
        {
            startRotation = rb.rotation;
            hasStartRotation = true;
        }

        float baseDir = reverse ? -1f : 1f;

        // Режим бесконечного вращения по кругу
        if (rotationAngle <= 0.01f)
        {
            currentSignedSpeedDeg = baseDir * speedDegPerSec;
            float d = currentSignedSpeedDeg * dt;
            rb.MoveRotation(Quaternion.Euler(0f, d, 0f) * rb.rotation);
            return;
        }

        // Режим ограниченного поворота на заданный угол и обратно
        if (pauseTimer > 0f)
        {
            pauseTimer -= dt;
            currentSignedSpeedDeg = 0f;
            return;
        }

        float step = speedDegPerSec * dt * pingPongDir;
        currentAngleOffset += step;

        if (currentAngleOffset >= rotationAngle)
        {
            currentAngleOffset = rotationAngle;
            pingPongDir = -1;
            pauseTimer = rotationPause;
        }
        else if (currentAngleOffset <= 0f)
        {
            currentAngleOffset = 0f;
            pingPongDir = 1;
            pauseTimer = rotationPause;
        }

        currentSignedSpeedDeg = baseDir * speedDegPerSec * pingPongDir;
        rb.MoveRotation(Quaternion.Euler(0f, baseDir * currentAngleOffset, 0f) * startRotation);
    }

    // ---------------------------------------------------------------- IPlatformVelocityProvider

    public Vector3 GetVelocityAtPoint(Vector3 worldPoint)
    {
        if (!rotate || !Application.isPlaying)
            return Vector3.zero;

        Vector3 omega = GetAngularVelocity();
        Vector3 r = worldPoint - transform.position;
        return Vector3.Cross(omega, r);
    }

    public Vector3 GetAngularVelocity()
    {
        if (!rotate || !Application.isPlaying)
            return Vector3.zero;

        float activeSpeed = rotationAngle <= 0.01f
            ? (reverse ? -1f : 1f) * speedDegPerSec
            : currentSignedSpeedDeg;

        return Vector3.up * (activeSpeed * Mathf.Deg2Rad);
    }

    // ----------------------------------------------------------------

    void SyncRigidbodyState()
    {
        rb = GetComponent<Rigidbody>();
        if (rotate)
        {
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
        else if (rb != null)
        {
            DestroyObj(rb);
            rb = null;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying) ScheduleRebuild();
    }

    void ScheduleRebuild()
    {
        EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled && !Application.isPlaying) Rebuild();
        };
    }
#endif

    [ContextMenu("Перестроить платформу")]
    public void RebuildNow() { Rebuild(); }

    public void Rebuild()
    {
        if (!gameObject.scene.IsValid()) return;

        ClearGenerated();
        SyncRigidbodyState();

        float h = Mathf.Max(0.05f, thickness * 0.5f);
        float rIn = columnRadius + Mathf.Max(0.05f, gap);
        float rOut = rIn + Mathf.Max(0.3f, depth);
        float arc = Mathf.Clamp(arcDegrees, 1f, 350f);

        // --- корпус (из кэша общих мешей) ---
        Mesh body = constantWidth
            ? MagneticPlatformMeshBuilder.GetOrBuildStraightBody(
                arc, rIn, rOut, h, cornerBevel, roundCorners, chamfer, grooveCount, grooveInset, grooveWidth, grooveDepth, false)
            : MagneticPlatformMeshBuilder.GetOrBuildBody(
                arc, rIn, rOut, h, chamfer, grooveInset, grooveWidth, grooveDepth, false);
        CreatePart(GenPrefix + "Body", body, GetBodyMaterial(), false);

        // --- светящиеся вставки в канавках (из кэша общих мешей) ---
        if (constantWidth && glowGrooves && grooveCount > 0)
        {
            Mesh glow = MagneticPlatformMeshBuilder.GetOrBuildStraightGlow(
                arc, rIn, rOut, h, cornerBevel, roundCorners, chamfer, grooveCount, grooveInset, grooveWidth, grooveDepth, glowSideAccents);
            if (glow != null && glow.vertexCount > 0)
                CreatePart(GenPrefix + "Glow", glow, GetGlowMaterial(), true);
        }

        // --- поле (из кэша общих мешей) ---
        Material fm = GetFieldMaterial();
        if (fm != null)
        {
            Mesh field = MagneticPlatformMeshBuilder.GetOrBuildField(arc, columnRadius, rIn, h, groove, fieldReach, fieldLayers);
            GameObject fieldGo = CreatePart(GenPrefix + "Field", field, fm, true);

            var mpb = new MaterialPropertyBlock();
            mpb.SetVector("_ColumnCenter", new Vector4(transform.position.x, 0f, transform.position.z, 0f));
            mpb.SetFloat("_TubeAz0", tubeAzimuth0Rad);
            mpb.SetFloat("_TubeDir", tubeDirection >= 0f ? 1f : -1f);
            mpb.SetFloat("_TubeCount", Mathf.Max(1, tubeCount));
            fieldGo.GetComponent<MeshRenderer>().SetPropertyBlock(mpb);
        }

        // --- коллизия (с облегчённой сеткой для быстрой симуляции PhysX) ---
        if (addCollider)
        {
            switch (colliderMode)
            {
                case ColliderMode.ExactSmoothDeck:
                    BuildExactMeshCollider(arc, rIn, rOut, h, false, body);
                    break;
                case ColliderMode.ExactWithGrooves:
                    BuildExactMeshCollider(arc, rIn, rOut, h, true, body);
                    break;
                case ColliderMode.CompoundBoxes:
                    if (constantWidth) BuildStraightColliders(arc, rIn, rOut, h);
                    else BuildColliders(arc, rIn, rOut, h);
                    break;
            }
        }
    }

    GameObject CreatePart(string goName, Mesh mesh, Material mat, bool isEmissiveOrField)
    {
        var go = new GameObject(goName);
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = isEmissiveOrField ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows = !isEmissiveOrField;
        return go;
    }

    void BuildExactMeshCollider(float arc, float rIn, float rOut, float h, bool includeGrooves, Mesh visualBody)
    {
        Mesh colMesh;
        if (includeGrooves && visualBody != null)
        {
            colMesh = visualBody;
        }
        else
        {
            float colChamfer = colliderChamfer ? chamfer : 0f;
            colMesh = constantWidth
                ? MagneticPlatformMeshBuilder.GetOrBuildStraightBody(
                    arc, rIn, rOut, h, cornerBevel, roundCorners, colChamfer, 0, 0f, 0f, 0f, true)
                : MagneticPlatformMeshBuilder.GetOrBuildBody(
                    arc, rIn, rOut, h, colChamfer, 0f, 0f, 0f, true);
        }

        var go = new GameObject(GenPrefix + "Collider");
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);

        var mc = go.AddComponent<MeshCollider>();
        mc.convex = false;
        mc.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                          | MeshColliderCookingOptions.EnableMeshCleaning
                          | MeshColliderCookingOptions.WeldColocatedVertices;
        mc.sharedMesh = colMesh;
        if (physicMaterial != null)
            mc.sharedMaterial = physicMaterial;
    }

    void BuildStraightColliders(float arc, float rIn, float rOut, float h)
    {
        float halfRad = Mathf.Clamp(arc, 1f, 160f) * 0.5f * Mathf.Deg2Rad;
        float zHalf = rIn * Mathf.Sin(halfRad);
        float xCorner = rIn * Mathf.Cos(halfRad);
        float xOut = Mathf.Max(xCorner + 0.15f, rOut);

        var mainGo = new GameObject(GenPrefix + "ColMain");
        mainGo.hideFlags = HideFlags.DontSave;
        mainGo.transform.SetParent(transform, false);
        mainGo.transform.localPosition = new Vector3((xCorner + xOut) * 0.5f, 0f, 0f);
        var mainBc = mainGo.AddComponent<BoxCollider>();
        mainBc.size = new Vector3(xOut - xCorner, h * 2f, Mathf.Max(0.05f, zHalf * 2f));
        if (physicMaterial != null) mainBc.sharedMaterial = physicMaterial;

        float lipDepth = rIn - xCorner;
        if (lipDepth > 0.04f)
        {
            var lipGo = new GameObject(GenPrefix + "ColLip");
            lipGo.hideFlags = HideFlags.DontSave;
            lipGo.transform.SetParent(transform, false);
            lipGo.transform.localPosition = new Vector3(xCorner + lipDepth * 0.5f, 0f, 0f);
            var lipBc = lipGo.AddComponent<BoxCollider>();
            lipBc.size = new Vector3(lipDepth, h * 2f, Mathf.Max(0.05f, zHalf * 1.15f));
            if (physicMaterial != null) lipBc.sharedMaterial = physicMaterial;
        }
    }

    void BuildColliders(float arc, float rIn, float rOut, float h)
    {
        int n = Mathf.Clamp(Mathf.CeilToInt(arc / 8f), 1, 24);
        float step = arc / n;
        float rMid = (rIn + rOut) * 0.5f;
        for (int i = 0; i < n; i++)
        {
            float aDeg = -arc * 0.5f + step * (i + 0.5f);
            float a = aDeg * Mathf.Deg2Rad;

            var go = new GameObject(GenPrefix + "Col" + i);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(Mathf.Cos(a) * rMid, 0f, Mathf.Sin(a) * rMid);
            go.transform.localRotation = Quaternion.Euler(0f, -aDeg, 0f);

            var bc = go.AddComponent<BoxCollider>();
            float width = 2f * rMid * Mathf.Tan(step * 0.5f * Mathf.Deg2Rad) * 1.02f;
            bc.size = new Vector3(rOut - rIn, h * 2f, Mathf.Max(0.05f, width));
            if (physicMaterial != null) bc.sharedMaterial = physicMaterial;
        }
    }

    void ClearGenerated()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform ch = transform.GetChild(i);
            if (!ch.name.StartsWith(GenPrefix)) continue;

            var mc = ch.GetComponent<MeshCollider>();
            if (mc != null && mc.sharedMesh != null && !MagneticPlatformMeshBuilder.IsCached(mc.sharedMesh))
                DestroyObj(mc.sharedMesh);

            var mf = ch.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && !MagneticPlatformMeshBuilder.IsCached(mf.sharedMesh))
                DestroyObj(mf.sharedMesh);

            DestroyObj(ch.gameObject);
        }
    }

    static void DestroyObj(UnityEngine.Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    public void DrawHitboxGizmo()
    {
        Gizmos.color = new Color(0.25f, 1f, 0.35f, 0.85f);
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform ch = transform.GetChild(i);
            if (!ch.name.StartsWith(GenPrefix)) continue;

            var mc = ch.GetComponent<MeshCollider>();
            if (mc != null && mc.sharedMesh != null)
            {
                Gizmos.DrawWireMesh(mc.sharedMesh, ch.position, ch.rotation, ch.lossyScale);
                continue;
            }

            var bc = ch.GetComponent<BoxCollider>();
            if (bc != null)
            {
                Matrix4x4 prev = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(ch.position, ch.rotation, ch.lossyScale);
                Gizmos.DrawWireCube(bc.center, bc.size);
                Gizmos.matrix = prev;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        DrawHitboxGizmo();
    }

    Material GetBodyMaterial()
    {
        if (bodyMaterial != null) return bodyMaterial;
        if (s_SharedBodyMaterial != null) return s_SharedBodyMaterial;

        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh) { name = "Platform Armor (shared)", enableInstancing = true };
        Color c = new Color(0.12f, 0.17f, 0.23f, 1f);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.58f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.10f);
        m.hideFlags = HideFlags.DontSave;
        s_SharedBodyMaterial = m;
        return m;
    }

    Material GetGlowMaterial()
    {
        if (glowMaterial != null) return glowMaterial;

        int key = HashCode.Combine(
            Mathf.RoundToInt(glowColor.r * 255f),
            Mathf.RoundToInt(glowColor.g * 255f),
            Mathf.RoundToInt(glowColor.b * 255f),
            Mathf.RoundToInt(glowIntensity * 100f));

        if (s_SharedGlowMaterials.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh) { name = "Platform Glow Strip (shared)", enableInstancing = true };
        Color baseCol = new Color(glowColor.r, glowColor.g, glowColor.b, 1f);
        Color hdrEmission = baseCol * Mathf.Max(0f, glowIntensity);

        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseCol);
        if (m.HasProperty("_Color")) m.SetColor("_Color", baseCol);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.9f);
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", hdrEmission);
        }
        m.hideFlags = HideFlags.DontSave;
        s_SharedGlowMaterials[key] = m;
        return m;
    }

    Material GetFieldMaterial()
    {
        if (fieldMaterial != null) return fieldMaterial;
        if (s_SharedFieldMaterial != null) return s_SharedFieldMaterial;

        Shader sh = Shader.Find("SciFi/MagneticFieldPlatform");
        if (sh == null)
        {
            Debug.LogWarning("[MagneticPlatform] Не назначен материал магнитного поля и не найден шейдер SciFi/MagneticFieldPlatform — поле не будет показано.", this);
            return null;
        }
        var m = new Material(sh) { name = "Magnetic Field (shared)" };
        m.hideFlags = HideFlags.DontSave;
        s_SharedFieldMaterial = m;
        return m;
    }
}

/// <summary>Построение и кэширование мешей корпуса, светящихся вставок, точных коллайдеров и магнитного поля.</summary>
public static class MagneticPlatformMeshBuilder
{
    static readonly Dictionary<int, Mesh> s_MeshCache = new Dictionary<int, Mesh>();
    static readonly HashSet<Mesh> s_CachedMeshSet = new HashSet<Mesh>();

    public static int CachedMeshCount { get { return s_MeshCache.Count; } }
    public static bool IsCached(Mesh m) { return m != null && s_CachedMeshSet.Contains(m); }

    public static void ClearCache()
    {
        foreach (var kv in s_MeshCache)
        {
            if (kv.Value != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(kv.Value);
                else UnityEngine.Object.DestroyImmediate(kv.Value);
            }
        }
        s_MeshCache.Clear();
        s_CachedMeshSet.Clear();
    }

    static int Q(float v) { return Mathf.RoundToInt(v * 10000f); }

    static Mesh RegisterCache(int key, Mesh mesh)
    {
        if (mesh == null) return null;
        mesh.hideFlags = HideFlags.DontSave;
        s_MeshCache[key] = mesh;
        s_CachedMeshSet.Add(mesh);
        return mesh;
    }

    struct StraightLayout
    {
        public float zHalf;
        public float xCorner;
        public float xOut;
        public float cb;
        public float c;
        public float zIn;
        public float xArcCornerIn;
        public float xOutIn;
        public float cbIn;
        public int segs;
        public List<Vector2> pOut;
        public List<Vector2> pIn;
        public List<Vector2> grooves;
        public float gd;
    }

    static StraightLayout ComputeStraightLayout(float arcDeg, float rIn, float rOut, float h,
                                                float cornerBevel, bool roundCorners, float chamfer,
                                                int grooveCount, float grooveInset, float grooveWidth, float grooveDepth,
                                                bool lowPolyForCollider)
    {
        var L = new StraightLayout();
        float halfSpan = Mathf.Clamp(arcDeg, 1f, 160f) * 0.5f * Mathf.Deg2Rad;
        L.zHalf = rIn * Mathf.Sin(halfSpan);
        L.xCorner = rIn * Mathf.Cos(halfSpan);
        L.xOut = Mathf.Max(L.xCorner + 0.15f, rOut);

        float maxCb = Mathf.Min(L.zHalf * 0.75f, (L.xOut - L.xCorner) * 0.45f);
        L.cb = Mathf.Clamp(cornerBevel, 0f, Mathf.Max(0f, maxCb));

        float maxC = Mathf.Min(h * 0.45f, Mathf.Min(L.zHalf * 0.4f, (L.xOut - L.xCorner) * 0.2f));
        L.c = Mathf.Clamp(chamfer, 0f, Mathf.Max(0f, maxC));

        L.segs = Mathf.Max(3, Mathf.CeilToInt(arcDeg / (lowPolyForCollider ? 5f : 3f)));
        int cornerSegs = lowPolyForCollider ? 3 : 6;

        L.pOut = new List<Vector2>(L.segs + 16);
        for (int i = 0; i <= L.segs; i++)
        {
            float ang = Mathf.Lerp(halfSpan, -halfSpan, i / (float)L.segs);
            L.pOut.Add(new Vector2(Mathf.Cos(ang) * rIn, Mathf.Sin(ang) * rIn));
        }

        if (L.cb > 1e-4f)
        {
            if (roundCorners)
            {
                for (int i = 0; i <= cornerSegs; i++)
                {
                    float a = Mathf.Lerp(-Mathf.PI * 0.5f, 0f, i / (float)cornerSegs);
                    L.pOut.Add(new Vector2((L.xOut - L.cb) + Mathf.Cos(a) * L.cb, (-L.zHalf + L.cb) + Mathf.Sin(a) * L.cb));
                }
                for (int i = 0; i <= cornerSegs; i++)
                {
                    float a = Mathf.Lerp(0f, Mathf.PI * 0.5f, i / (float)cornerSegs);
                    L.pOut.Add(new Vector2((L.xOut - L.cb) + Mathf.Cos(a) * L.cb, (L.zHalf - L.cb) + Mathf.Sin(a) * L.cb));
                }
            }
            else
            {
                L.pOut.Add(new Vector2(L.xOut - L.cb, -L.zHalf));
                L.pOut.Add(new Vector2(L.xOut, -L.zHalf + L.cb));
                L.pOut.Add(new Vector2(L.xOut, L.zHalf - L.cb));
                L.pOut.Add(new Vector2(L.xOut - L.cb, L.zHalf));
            }
        }
        else
        {
            L.pOut.Add(new Vector2(L.xOut, -L.zHalf));
            L.pOut.Add(new Vector2(L.xOut, L.zHalf));
        }

        float rArcIn = rIn + L.c;
        L.zIn = Mathf.Max(0.01f, L.zHalf - L.c);
        float halfSpanIn = Mathf.Asin(Mathf.Clamp(L.zIn / rArcIn, 0f, 0.999f));

        L.pIn = new List<Vector2>(L.segs + 16);
        for (int i = 0; i <= L.segs; i++)
        {
            float ang = Mathf.Lerp(halfSpanIn, -halfSpanIn, i / (float)L.segs);
            L.pIn.Add(new Vector2(Mathf.Cos(ang) * rArcIn, Mathf.Sin(ang) * rArcIn));
        }
        L.xArcCornerIn = L.pIn[0].x;
        L.xOutIn = Mathf.Max(L.xArcCornerIn + 0.04f, L.xOut - L.c);

        if (L.cb > 1e-4f)
        {
            if (roundCorners)
            {
                L.cbIn = Mathf.Max(0f, L.cb - L.c);
                for (int i = 0; i <= cornerSegs; i++)
                {
                    float a = Mathf.Lerp(-Mathf.PI * 0.5f, 0f, i / (float)cornerSegs);
                    L.pIn.Add(new Vector2((L.xOut - L.cb) + Mathf.Cos(a) * L.cbIn, (-L.zHalf + L.cb) + Mathf.Sin(a) * L.cbIn));
                }
                for (int i = 0; i <= cornerSegs; i++)
                {
                    float a = Mathf.Lerp(0f, Mathf.PI * 0.5f, i / (float)cornerSegs);
                    L.pIn.Add(new Vector2((L.xOut - L.cb) + Mathf.Cos(a) * L.cbIn, (L.zHalf - L.cb) + Mathf.Sin(a) * L.cbIn));
                }
            }
            else
            {
                L.cbIn = Mathf.Max(0f, L.cb - L.c * 0.41421356f);
                if (L.cbIn > 1e-4f)
                {
                    L.pIn.Add(new Vector2(L.xOutIn - L.cbIn, -L.zIn));
                    L.pIn.Add(new Vector2(L.xOutIn, -L.zIn + L.cbIn));
                    L.pIn.Add(new Vector2(L.xOutIn, L.zIn - L.cbIn));
                    L.pIn.Add(new Vector2(L.xOutIn - L.cbIn, L.zIn));
                }
                else
                {
                    L.pIn.Add(new Vector2(L.xOutIn, -L.zIn));
                    L.pIn.Add(new Vector2(L.xOutIn, L.zIn));
                }
            }
        }
        else
        {
            L.cbIn = 0f;
            L.pIn.Add(new Vector2(L.xOutIn, -L.zIn));
            L.pIn.Add(new Vector2(L.xOutIn, L.zIn));
        }

        L.grooves = new List<Vector2>();
        float xStart = L.xArcCornerIn;
        float xEnd = roundCorners && L.cb > 1e-4f ? (L.xOut - L.cb) : (L.xOutIn - L.cbIn);
        float deckLen = xEnd - xStart;

        float gi = Mathf.Clamp(grooveInset, 0.05f, Mathf.Max(0.05f, deckLen * 0.35f));
        float gw = Mathf.Clamp(grooveWidth, 0f, Mathf.Max(0f, deckLen * 0.15f));
        L.gd = Mathf.Clamp(grooveDepth, 0f, h * 0.3f);

        int count = Mathf.Clamp(grooveCount, 0, 6);
        if (count > 0 && gw > 1e-4f && L.gd > 1e-4f)
        {
            if (count == 1)
            {
                if (deckLen > gw + 0.12f)
                {
                    float mid = (xStart + xEnd) * 0.5f;
                    L.grooves.Add(new Vector2(mid - gw * 0.5f, mid + gw * 0.5f));
                }
            }
            else
            {
                float firstStart = xStart + gi;
                float lastEnd = xEnd - gi;
                float span = lastEnd - firstStart;
                while (count > 1 && span < count * gw + (count - 1) * 0.06f)
                    count--;

                if (count == 1)
                {
                    if (deckLen > gw + 0.12f)
                    {
                        float mid = (xStart + xEnd) * 0.5f;
                        L.grooves.Add(new Vector2(mid - gw * 0.5f, mid + gw * 0.5f));
                    }
                }
                else if (count >= 2)
                {
                    float step = (span - gw) / (count - 1);
                    for (int k = 0; k < count; k++)
                    {
                        float gx0 = firstStart + k * step;
                        L.grooves.Add(new Vector2(gx0, gx0 + gw));
                    }
                }
            }
        }

        return L;
    }

    // -------------------------------------------------------------- прямой корпус (с кэшем)

    public static Mesh GetOrBuildStraightBody(float arcDeg, float rIn, float rOut, float h, float cornerBevel,
                                              bool roundCorners, float chamfer, int grooveCount, float grooveInset,
                                              float grooveWidth, float grooveDepth, bool lowPolyForCollider)
    {
        var hc = new HashCode();
        hc.Add(1);
        hc.Add(Q(arcDeg)); hc.Add(Q(rIn)); hc.Add(Q(rOut)); hc.Add(Q(h));
        hc.Add(Q(cornerBevel)); hc.Add(roundCorners); hc.Add(Q(chamfer));
        hc.Add(grooveCount); hc.Add(Q(grooveInset)); hc.Add(Q(grooveWidth)); hc.Add(Q(grooveDepth));
        hc.Add(lowPolyForCollider);
        int key = hc.ToHashCode();

        if (s_MeshCache.TryGetValue(key, out Mesh cached) && cached != null)
            return cached;

        Mesh created = BuildStraightBody(arcDeg, rIn, rOut, h, cornerBevel, roundCorners, chamfer,
                                         grooveCount, grooveInset, grooveWidth, grooveDepth, lowPolyForCollider);
        return RegisterCache(key, created);
    }

    public static Mesh BuildStraightBody(float arcDeg, float rIn, float rOut, float h, float cornerBevel,
                                         bool roundCorners, float chamfer, int grooveCount, float grooveInset,
                                         float grooveWidth, float grooveDepth, bool lowPolyForCollider = false)
    {
        StraightLayout L = ComputeStraightLayout(arcDeg, rIn, rOut, h, cornerBevel, roundCorners, chamfer,
                                                 grooveCount, grooveInset, grooveWidth, grooveDepth, lowPolyForCollider);

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        int m = L.pOut.Count;
        for (int k = 0; k < m; k++)
        {
            int next = (k + 1) % m;
            bool isArcEdge = k < L.segs;

            AddPerimeterQuad(verts, norms, uvs, tris,
                L.pOut[k], -h + L.c, L.pOut[next], -h + L.c,
                L.pOut[k], h - L.c, L.pOut[next], h - L.c, isArcEdge);

            if (L.c > 1e-4f)
            {
                AddPerimeterQuad(verts, norms, uvs, tris,
                    L.pOut[k], h - L.c, L.pOut[next], h - L.c,
                    L.pIn[k], h, L.pIn[next], h, false);

                AddPerimeterQuad(verts, norms, uvs, tris,
                    L.pIn[k], -h, L.pIn[next], -h,
                    L.pOut[k], -h + L.c, L.pOut[next], -h + L.c, false);
            }
        }

        if (L.grooves.Count == 0)
        {
            AddCapPolygon(verts, norms, uvs, tris, L.pIn, h, Vector3.up);
            AddCapPolygon(verts, norms, uvs, tris, L.pIn, -h, Vector3.down);
        }
        else
        {
            float firstX0 = L.grooves[0].x;
            var nearPoly = new List<Vector2>(L.segs + 3);
            for (int i = 0; i <= L.segs; i++) nearPoly.Add(L.pIn[i]);
            nearPoly.Add(new Vector2(firstX0, -L.zIn));
            nearPoly.Add(new Vector2(firstX0, L.zIn));

            AddCapPolygon(verts, norms, uvs, tris, nearPoly, h, Vector3.up);
            AddCapPolygon(verts, norms, uvs, tris, nearPoly, -h, Vector3.down);

            for (int k = 0; k < L.grooves.Count - 1; k++)
            {
                float xA = L.grooves[k].y;
                float xB = L.grooves[k + 1].x;
                AddHorizontalRect(verts, norms, uvs, tris, xA, xB, -L.zIn, L.zIn, h, Vector3.up);
                AddHorizontalRect(verts, norms, uvs, tris, xA, xB, -L.zIn, L.zIn, -h, Vector3.down);
            }

            float lastX1 = L.grooves[L.grooves.Count - 1].y;
            var farPoly = new List<Vector2>(16);
            farPoly.Add(new Vector2(lastX1, L.zIn));
            farPoly.Add(new Vector2(lastX1, -L.zIn));
            for (int i = L.segs + 1; i < L.pIn.Count; i++)
                farPoly.Add(L.pIn[i]);

            AddCapPolygon(verts, norms, uvs, tris, farPoly, h, Vector3.up);
            AddCapPolygon(verts, norms, uvs, tris, farPoly, -h, Vector3.down);

            for (int k = 0; k < L.grooves.Count; k++)
            {
                float gx0 = L.grooves[k].x;
                float gx1 = L.grooves[k].y;
                AddTransverseGroove(verts, norms, uvs, tris, gx0, gx1, L.zIn, h, -L.gd);
                AddTransverseGroove(verts, norms, uvs, tris, gx0, gx1, L.zIn, -h, L.gd);
            }
        }

        var mesh = new Mesh { name = lowPolyForCollider ? "Platform Collider (shared)" : "Platform Straight Body (shared)" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        if (!lowPolyForCollider) mesh.RecalculateTangents();
        return mesh;
    }

    // -------------------------------------------------------------- светящиеся полосы в канавках (с кэшем)

    public static Mesh GetOrBuildStraightGlow(float arcDeg, float rIn, float rOut, float h, float cornerBevel,
                                              bool roundCorners, float chamfer, int grooveCount, float grooveInset,
                                              float grooveWidth, float grooveDepth, bool sideAccents)
    {
        var hc = new HashCode();
        hc.Add(2);
        hc.Add(Q(arcDeg)); hc.Add(Q(rIn)); hc.Add(Q(rOut)); hc.Add(Q(h));
        hc.Add(Q(cornerBevel)); hc.Add(roundCorners); hc.Add(Q(chamfer));
        hc.Add(grooveCount); hc.Add(Q(grooveInset)); hc.Add(Q(grooveWidth)); hc.Add(Q(grooveDepth));
        hc.Add(sideAccents);
        int key = hc.ToHashCode();

        if (s_MeshCache.TryGetValue(key, out Mesh cached))
            return cached;

        Mesh created = BuildStraightGlow(arcDeg, rIn, rOut, h, cornerBevel, roundCorners, chamfer,
                                         grooveCount, grooveInset, grooveWidth, grooveDepth, sideAccents);
        return RegisterCache(key, created);
    }

    public static Mesh BuildStraightGlow(float arcDeg, float rIn, float rOut, float h, float cornerBevel,
                                         bool roundCorners, float chamfer, int grooveCount, float grooveInset,
                                         float grooveWidth, float grooveDepth, bool sideAccents)
    {
        StraightLayout L = ComputeStraightLayout(arcDeg, rIn, rOut, h, cornerBevel, roundCorners, chamfer,
                                                 grooveCount, grooveInset, grooveWidth, grooveDepth, false);
        if (L.grooves.Count == 0) return null;

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        float yTop = h - L.gd * 0.35f;
        float yBot = -h + L.gd * 0.35f;
        float zStrip = Mathf.Max(0.02f, L.zIn - 0.015f);

        for (int k = 0; k < L.grooves.Count; k++)
        {
            float gx0 = L.grooves[k].x;
            float gx1 = L.grooves[k].y;

            AddHorizontalRect(verts, norms, uvs, tris, gx0, gx1, -zStrip, zStrip, yTop, Vector3.up);
            AddHorizontalRect(verts, norms, uvs, tris, gx0, gx1, -zStrip, zStrip, yBot, Vector3.down);

            if (sideAccents && (h - L.c) > 0.03f)
            {
                float yLow = -h + L.c;
                float yHigh = h - L.c;
                float zRight = -(L.zHalf + 0.003f);
                float zLeft = L.zHalf + 0.003f;

                AddWallQuad(verts, norms, uvs, tris,
                    new Vector3(gx0, yLow, zRight), new Vector3(gx1, yLow, zRight),
                    new Vector3(gx1, yHigh, zRight), new Vector3(gx0, yHigh, zRight), Vector3.back);

                AddWallQuad(verts, norms, uvs, tris,
                    new Vector3(gx1, yLow, zLeft), new Vector3(gx0, yLow, zLeft),
                    new Vector3(gx0, yHigh, zLeft), new Vector3(gx1, yHigh, zLeft), Vector3.forward);
            }
        }

        var mesh = new Mesh { name = "Platform Glow Strips (shared)" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddPerimeterQuad(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
                                 Vector2 b0, float yB0, Vector2 b1, float yB1,
                                 Vector2 t0, float yT0, Vector2 t1, float yT1, bool smoothArc)
    {
        Vector3 v0 = new Vector3(b0.x, yB0, b0.y);
        Vector3 v1 = new Vector3(b1.x, yB1, b1.y);
        Vector3 v2 = new Vector3(t1.x, yT1, t1.y);
        Vector3 v3 = new Vector3(t0.x, yT0, t0.y);

        Vector2 e = b1 - b0;
        if (e.sqrMagnitude < 1e-10f) return;

        Vector3 flatNormal = Vector3.Cross(v3 - v0, v1 - v0);
        if (flatNormal.sqrMagnitude < 1e-10f)
            flatNormal = new Vector3(e.y, 0f, -e.x);
        flatNormal.Normalize();

        Vector3 n0 = smoothArc ? new Vector3(-b0.x, 0f, -b0.y).normalized : flatNormal;
        Vector3 n1 = smoothArc ? new Vector3(-b1.x, 0f, -b1.y).normalized : flatNormal;

        int idx = verts.Count;
        verts.Add(v0); norms.Add(n0); uvs.Add(new Vector2(b0.x + b0.y, yB0));
        verts.Add(v1); norms.Add(n1); uvs.Add(new Vector2(b1.x + b1.y, yB1));
        verts.Add(v2); norms.Add(n1); uvs.Add(new Vector2(t1.x + t1.y, yT1));
        verts.Add(v3); norms.Add(n0); uvs.Add(new Vector2(t0.x + t0.y, yT0));

        AddTri(tris, verts, idx, idx + 1, idx + 2, flatNormal);
        AddTri(tris, verts, idx, idx + 2, idx + 3, flatNormal);
    }

    static void AddCapPolygon(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
                              List<Vector2> poly, float y, Vector3 normal)
    {
        List<int> capTris = EarClip(poly);
        int baseIdx = verts.Count;
        for (int i = 0; i < poly.Count; i++)
        {
            verts.Add(new Vector3(poly[i].x, y, poly[i].y));
            norms.Add(normal);
            uvs.Add(new Vector2(poly[i].x, poly[i].y));
        }
        for (int t = 0; t < capTris.Count; t += 3)
            AddTri(tris, verts, baseIdx + capTris[t], baseIdx + capTris[t + 1], baseIdx + capTris[t + 2], normal);
    }

    static void AddHorizontalRect(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
                                  float x0, float x1, float z0, float z1, float y, Vector3 normal)
    {
        int idx = verts.Count;
        verts.Add(new Vector3(x0, y, z0)); norms.Add(normal); uvs.Add(new Vector2(x0, z0));
        verts.Add(new Vector3(x1, y, z0)); norms.Add(normal); uvs.Add(new Vector2(x1, z0));
        verts.Add(new Vector3(x1, y, z1)); norms.Add(normal); uvs.Add(new Vector2(x1, z1));
        verts.Add(new Vector3(x0, y, z1)); norms.Add(normal); uvs.Add(new Vector2(x0, z1));

        AddTri(tris, verts, idx, idx + 1, idx + 2, normal);
        AddTri(tris, verts, idx, idx + 2, idx + 3, normal);
    }

    static void AddTransverseGroove(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
                                    float x0, float x1, float zHalf, float ySurface, float dy)
    {
        Vector3 nCap = dy < 0f ? Vector3.up : Vector3.down;
        float yFloor = ySurface + dy;

        AddHorizontalRect(verts, norms, uvs, tris, x0, x1, -zHalf, zHalf, yFloor, nCap);

        AddWallQuad(verts, norms, uvs, tris,
            new Vector3(x0, yFloor, -zHalf), new Vector3(x0, yFloor, zHalf),
            new Vector3(x0, ySurface, zHalf), new Vector3(x0, ySurface, -zHalf), Vector3.right);

        AddWallQuad(verts, norms, uvs, tris,
            new Vector3(x1, yFloor, -zHalf), new Vector3(x1, yFloor, zHalf),
            new Vector3(x1, ySurface, zHalf), new Vector3(x1, ySurface, -zHalf), Vector3.left);
    }

    static void AddWallQuad(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
                            Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 normal)
    {
        int idx = verts.Count;
        verts.Add(v0); norms.Add(normal); uvs.Add(new Vector2(v0.z, v0.y));
        verts.Add(v1); norms.Add(normal); uvs.Add(new Vector2(v1.z, v1.y));
        verts.Add(v2); norms.Add(normal); uvs.Add(new Vector2(v2.z, v2.y));
        verts.Add(v3); norms.Add(normal); uvs.Add(new Vector2(v3.z, v3.y));

        AddTri(tris, verts, idx, idx + 1, idx + 2, normal);
        AddTri(tris, verts, idx, idx + 2, idx + 3, normal);
    }

    // -------------------------------------------------------------- веерный профиль (с кэшем)

    static List<Vector2> BuildProfile(float rIn, float rOut, float h, float c, float gi, float gw, float gd)
    {
        float dep = rOut - rIn;
        c = Mathf.Clamp(c, 0f, Mathf.Min(h * 0.45f, dep * 0.2f));
        gi = Mathf.Clamp(gi, c + 0.02f, dep * 0.35f);
        gw = Mathf.Clamp(gw, 0f, dep * 0.1f);
        gd = Mathf.Clamp(gd, 0f, h * 0.3f);
        bool grooves = gw > 1e-4f && gd > 1e-4f;

        var pts = new List<Vector2>();
        pts.Add(new Vector2(rIn + c, -h));
        if (grooves)
        {
            pts.Add(new Vector2(rIn + gi, -h));
            pts.Add(new Vector2(rIn + gi, -h + gd));
            pts.Add(new Vector2(rIn + gi + gw, -h + gd));
            pts.Add(new Vector2(rIn + gi + gw, -h));
            pts.Add(new Vector2(rOut - gi - gw, -h));
            pts.Add(new Vector2(rOut - gi - gw, -h + gd));
            pts.Add(new Vector2(rOut - gi, -h + gd));
            pts.Add(new Vector2(rOut - gi, -h));
        }
        pts.Add(new Vector2(rOut - c, -h));
        pts.Add(new Vector2(rOut, -h + c));
        pts.Add(new Vector2(rOut, h - c));
        pts.Add(new Vector2(rOut - c, h));
        if (grooves)
        {
            pts.Add(new Vector2(rOut - gi, h));
            pts.Add(new Vector2(rOut - gi, h - gd));
            pts.Add(new Vector2(rOut - gi - gw, h - gd));
            pts.Add(new Vector2(rOut - gi - gw, h));
            pts.Add(new Vector2(rIn + gi + gw, h));
            pts.Add(new Vector2(rIn + gi + gw, h - gd));
            pts.Add(new Vector2(rIn + gi, h - gd));
            pts.Add(new Vector2(rIn + gi, h));
        }
        pts.Add(new Vector2(rIn + c, h));
        pts.Add(new Vector2(rIn, h - c));
        pts.Add(new Vector2(rIn, -h + c));
        return pts;
    }

    public static Mesh GetOrBuildBody(float arcDeg, float rIn, float rOut, float h, float chamfer,
                                      float grooveInset, float grooveWidth, float grooveDepth, bool lowPolyForCollider)
    {
        var hc = new HashCode();
        hc.Add(3);
        hc.Add(Q(arcDeg)); hc.Add(Q(rIn)); hc.Add(Q(rOut)); hc.Add(Q(h));
        hc.Add(Q(chamfer)); hc.Add(Q(grooveInset)); hc.Add(Q(grooveWidth)); hc.Add(Q(grooveDepth));
        hc.Add(lowPolyForCollider);
        int key = hc.ToHashCode();

        if (s_MeshCache.TryGetValue(key, out Mesh cached) && cached != null)
            return cached;

        Mesh created = BuildBody(arcDeg, rIn, rOut, h, chamfer, grooveInset, grooveWidth, grooveDepth, lowPolyForCollider);
        return RegisterCache(key, created);
    }

    public static Mesh BuildBody(float arcDeg, float rIn, float rOut, float h, float chamfer,
                                 float grooveInset, float grooveWidth, float grooveDepth, bool lowPolyForCollider = false)
    {
        List<Vector2> prof = BuildProfile(rIn, rOut, h, chamfer, grooveInset, grooveWidth, grooveDepth);
        int n = prof.Count;
        int segs = Mathf.Max(3, Mathf.CeilToInt(arcDeg / (lowPolyForCollider ? 5f : 3f)));
        float span = arcDeg * Mathf.Deg2Rad;
        float a0 = -span * 0.5f;
        float rMid = (rIn + rOut) * 0.5f;

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        float[] cum = new float[n + 1];
        for (int k = 0; k < n; k++)
            cum[k + 1] = cum[k] + (prof[(k + 1) % n] - prof[k]).magnitude;

        for (int k = 0; k < n; k++)
        {
            Vector2 a = prof[k];
            Vector2 b = prof[(k + 1) % n];
            Vector2 e = b - a;
            if (e.sqrMagnitude < 1e-10f) continue;
            Vector2 n2 = new Vector2(e.y, -e.x).normalized;

            int baseIdx = verts.Count;
            for (int i = 0; i <= segs; i++)
            {
                float ang = a0 + span * i / segs;
                float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                float u = (ang - a0) * rMid;
                Vector3 nn = new Vector3(n2.x * ca, n2.y, n2.x * sa);

                verts.Add(new Vector3(a.x * ca, a.y, a.x * sa)); norms.Add(nn); uvs.Add(new Vector2(u, cum[k]));
                verts.Add(new Vector3(b.x * ca, b.y, b.x * sa)); norms.Add(nn); uvs.Add(new Vector2(u, cum[k + 1]));
            }

            for (int i = 0; i < segs; i++)
            {
                int i0 = baseIdx + i * 2, i1 = i0 + 1, i2 = i0 + 2, i3 = i0 + 3;
                float am = a0 + span * (i + 0.5f) / segs;
                Vector3 want = new Vector3(n2.x * Mathf.Cos(am), n2.y, n2.x * Mathf.Sin(am));
                AddTri(tris, verts, i0, i1, i3, want);
                AddTri(tris, verts, i0, i3, i2, want);
            }
        }

        List<int> capTris = EarClip(prof);
        for (int cap = 0; cap < 2; cap++)
        {
            float ang = cap == 0 ? a0 : a0 + span;
            float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
            Vector3 tangent = new Vector3(-sa, 0f, ca);
            Vector3 nn = cap == 0 ? -tangent : tangent;

            int baseIdx = verts.Count;
            for (int k = 0; k < n; k++)
            {
                verts.Add(new Vector3(prof[k].x * ca, prof[k].y, prof[k].x * sa));
                norms.Add(nn);
                uvs.Add(new Vector2(prof[k].x, prof[k].y));
            }
            for (int t = 0; t < capTris.Count; t += 3)
                AddTri(tris, verts, baseIdx + capTris[t], baseIdx + capTris[t + 1], baseIdx + capTris[t + 2], nn);
        }

        var mesh = new Mesh { name = lowPolyForCollider ? "Platform Fan Collider (shared)" : "Platform Body (shared)" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        if (!lowPolyForCollider) mesh.RecalculateTangents();
        return mesh;
    }

    // -------------------------------------------------------------- магнитное поле от труб к платформе (с кэшем)

    public static Mesh GetOrBuildField(float arcDeg, float rCol, float rIn, float h, float groove, float reach, int layers)
    {
        var hc = new HashCode();
        hc.Add(4);
        hc.Add(Q(arcDeg)); hc.Add(Q(rCol)); hc.Add(Q(rIn)); hc.Add(Q(h));
        hc.Add(Q(groove)); hc.Add(Q(reach)); hc.Add(layers);
        int key = hc.ToHashCode();

        if (s_MeshCache.TryGetValue(key, out Mesh cached) && cached != null)
            return cached;

        Mesh created = BuildField(arcDeg, rCol, rIn, h, groove, reach, layers);
        return RegisterCache(key, created);
    }

    public static Mesh BuildField(float arcDeg, float rCol, float rIn, float h, float groove, float reach, int layers)
    {
        layers = Mathf.Max(1, layers);
        int arcSegs = Mathf.Max(10, Mathf.CeilToInt(arcDeg / 2f));
        int radSegs = 14;
        float span = arcDeg * Mathf.Deg2Rad;
        float a0 = -span * 0.5f;

        float rStart = Mathf.Max(0.1f, rCol - 0.04f);
        float rEnd = Mathf.Max(rStart + 0.08f, rIn + 0.12f);

        float ringOffset = Mathf.Max(0.05f, groove * 0.5f);
        float yRing = h + ringOffset;
        float yPlat = h + 0.008f;

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uv0 = new List<Vector2>();
        var uv1 = new List<Vector4>();
        var tris = new List<int>();

        int clampShells = layers >= 3 ? 2 : 1;
        for (int sIdx = 0; sIdx < clampShells; sIdx++)
        {
            float shellOffset = sIdx * 0.045f;
            float archSag = Mathf.Max(0.04f, reach * 0.18f) + shellOffset * 0.5f;

            for (int sign = -1; sign <= 1; sign += 2)
            {
                int baseIdx = verts.Count;
                float y0 = sign * (yRing + shellOffset);
                float y1 = sign * (yPlat + shellOffset * 0.35f);

                for (int ir = 0; ir <= radSegs; ir++)
                {
                    float t = ir / (float)radSegs;
                    float r = Mathf.Lerp(rStart, rEnd, t);
                    float baseY = Mathf.Lerp(y0, y1, Mathf.SmoothStep(0f, 1f, t));
                    float arch = Mathf.Sin(t * Mathf.PI) * archSag * sign;
                    float y = baseY + arch;

                    for (int ia = 0; ia <= arcSegs; ia++)
                    {
                        float s = ia / (float)arcSegs;
                        float ang = a0 + span * s;
                        float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);

                        verts.Add(new Vector3(ca * r, y, sa * r));
                        norms.Add(new Vector3(ca * 0.25f, sign, sa * 0.25f).normalized);
                        uv0.Add(new Vector2(t, s));
                        uv1.Add(new Vector4(1f, sign, sIdx, 0f));
                    }
                }

                AddGridTriangles(tris, baseIdx, radSegs, arcSegs, sign < 0);
            }
        }

        int coreCount = Mathf.Max(1, layers);
        for (int cIdx = 0; cIdx < coreCount; cIdx++)
        {
            float lt = coreCount == 1 ? 0f : Mathf.Lerp(-1f, 1f, cIdx / (float)(coreCount - 1));
            float yCore = lt * (h * 0.72f);
            float bow = lt * Mathf.Max(0.02f, reach * 0.12f);
            int baseIdx = verts.Count;

            for (int ir = 0; ir <= radSegs; ir++)
            {
                float t = ir / (float)radSegs;
                float r = Mathf.Lerp(rStart, rIn + 0.03f, t);
                float y = yCore + Mathf.Sin(t * Mathf.PI) * bow;

                for (int ia = 0; ia <= arcSegs; ia++)
                {
                    float s = ia / (float)arcSegs;
                    float ang = a0 + span * s;
                    float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);

                    verts.Add(new Vector3(ca * r, y, sa * r));
                    norms.Add(Vector3.up);
                    uv0.Add(new Vector2(t, s));
                    uv1.Add(new Vector4(0f, lt, cIdx, 0f));
                }
            }

            AddGridTriangles(tris, baseIdx, radSegs, arcSegs, false);
        }

        int vSegs = 8;
        float veilH = h + ringOffset * 0.85f;
        float rVeil = Mathf.Lerp(rStart, rIn, 0.35f);
        int veilBase = verts.Count;
        for (int iy = 0; iy <= vSegs; iy++)
        {
            float vy = iy / (float)vSegs;
            float y = Mathf.Lerp(-veilH, veilH, vy);
            float tProxy = 0.25f + Mathf.Abs(vy - 0.5f) * 0.6f;

            for (int ia = 0; ia <= arcSegs; ia++)
            {
                float s = ia / (float)arcSegs;
                float ang = a0 + span * s;
                float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);

                verts.Add(new Vector3(ca * rVeil, y, sa * rVeil));
                norms.Add(new Vector3(ca, 0f, sa));
                uv0.Add(new Vector2(tProxy, s));
                uv1.Add(new Vector4(2f, vy * 2f - 1f, 0f, 0f));
            }
        }
        AddGridTriangles(tris, veilBase, vSegs, arcSegs, false);

        var mesh = new Mesh { name = "Platform Field (shared)" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(1, uv1);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddGridTriangles(List<int> tris, int baseIdx, int rows, int cols, bool flip)
    {
        int stride = cols + 1;
        for (int ir = 0; ir < rows; ir++)
        {
            for (int ic = 0; ic < cols; ic++)
            {
                int i0 = baseIdx + ir * stride + ic;
                int i1 = i0 + 1;
                int i2 = i0 + stride;
                int i3 = i2 + 1;

                if (!flip)
                {
                    tris.Add(i0); tris.Add(i2); tris.Add(i1);
                    tris.Add(i1); tris.Add(i2); tris.Add(i3);
                }
                else
                {
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    tris.Add(i1); tris.Add(i3); tris.Add(i2);
                }
            }
        }
    }

    // -------------------------------------------------------------- геометрические помощники

    static void AddTri(List<int> tris, List<Vector3> v, int a, int b, int c, Vector3 desiredNormal)
    {
        Vector3 nrm = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (Vector3.Dot(nrm, desiredNormal) < 0f) { int t = b; b = c; c = t; }
        tris.Add(a); tris.Add(b); tris.Add(c);
    }

    static float Cross2(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }

    static bool StrictlyInside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        const float eps = 1e-9f;
        return Cross2(b - a, p - a) > eps && Cross2(c - b, p - b) > eps && Cross2(a - c, p - c) > eps;
    }

    static List<int> EarClip(List<Vector2> p)
    {
        var idx = new List<int>();
        for (int i = 0; i < p.Count; i++) idx.Add(i);
        var res = new List<int>();

        int guard = 0;
        while (idx.Count > 3 && guard++ < 2000)
        {
            bool clipped = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int ia = idx[(i + idx.Count - 1) % idx.Count];
                int ib = idx[i];
                int ic = idx[(i + 1) % idx.Count];
                Vector2 A = p[ia], B = p[ib], C = p[ic];

                float cr = Cross2(B - A, C - B);
                if (Mathf.Abs(cr) < 1e-9f) { idx.RemoveAt(i); clipped = true; break; }
                if (cr < 0f) continue;

                bool inside = false;
                for (int j = 0; j < idx.Count; j++)
                {
                    int ij = idx[j];
                    if (ij == ia || ij == ib || ij == ic) continue;
                    if (StrictlyInside(p[ij], A, B, C)) { inside = true; break; }
                }
                if (inside) continue;

                res.Add(ia); res.Add(ib); res.Add(ic);
                idx.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped) break;
        }
        if (idx.Count == 3) { res.Add(idx[0]); res.Add(idx[1]); res.Add(idx[2]); }
        return res;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(MagneticPlatform))]
public class MagneticPlatformEditor : Editor
{
    bool showExtraSettings = true;

    void DrawProp(string propName, string label, string tooltip)
    {
        SerializedProperty p = serializedObject.FindProperty(propName);
        if (p != null)
            EditorGUILayout.PropertyField(p, new GUIContent(label, tooltip));
    }

    public override void OnInspectorGUI()
    {
        var mp = (MagneticPlatform)target;
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Пометка: если эта платформа создана через Спавнер, при нажатии «СГЕНЕРИРОВАТЬ» в Спавнере ручные правки здесь перезапишутся настройками слота.",
            MessageType.Info);

        GUILayout.Label("1. ОСНОВНЫЕ РАЗМЕРЫ И ВРАЩЕНИЕ", EditorStyles.boldLabel);
        DrawProp("constantWidth", "Прямой мостик (постоянная ширина)",
            "Сохранять постоянную ширину по всей длине (прямой мостик вместо расширения веером).");
        DrawProp("arcDegrees", "Ширина основания по дуге (°)",
            "Угловая ширина основания платформы у колонны в градусах.");
        DrawProp("depth", "Длина от колонны (м)",
            "Длина платформы от внутреннего края наружу в метрах.");
        DrawProp("gap", "Зазор с магнитным полем (м)",
            "Ширина воздушного зазора между колонной и платформой, где отображается поле (в метрах).");
        DrawProp("thickness", "Толщина платформы (м)",
            "Высота (толщина) корпуса платформы в метрах.");

        EditorGUILayout.Space();
        DrawProp("rotate", "Вращать вокруг колонны",
            "Включить вращение платформы вокруг центральной оси колонны во время игры.");
        DrawProp("speedDegPerSec", "Скорость вращения (°/сек)",
            "Скорость вращения в градусах за секунду (положительное значение = по часовой стрелке сверху).");
        DrawProp("reverse", "Обратное направление",
            "Вращать платформу в обратную сторону (против часовой стрелки при взгляде сверху).");
        DrawProp("rotationAngle", "Угол поворота (0 = полный круг)",
            "На сколько градусов повернётся платформа перед тем, как поехать обратно. Если 0 — вращается бесконечно по кругу.");
        using (new EditorGUI.DisabledScope(mp.rotationAngle <= 0.01f))
        {
            DrawProp("rotationPause", "Пауза на краях (сек)",
                "Задержка в секундах перед сменой направления в крайних точках.");
        }

        EditorGUILayout.Space();
        showExtraSettings = EditorGUILayout.Foldout(
            showExtraSettings,
            "2. Закругления, фаски, подсветка, поле и хитбоксы",
            true);

        if (showExtraSettings)
        {
            EditorGUI.indentLevel++;
            DrawProp("columnRadius", "Радиус колонны (м)",
                "Радиус внешней поверхности плит колонны в метрах (задаётся спавнером).");
            DrawProp("groove", "Ширина шва колонны (м)",
                "Ширина горизонтального шва между плитами колонны — определяет высоту выхода магнитных дуг из труб.");
            DrawProp("cornerBevel", "Закругление / срез внешних углов (м)",
                "Размер скоса или радиус закругления на дальних углах прямого мостика в метрах.");
            DrawProp("roundCorners", "Плавное закругление углов",
                "Если включено — внешние углы плавно закругляются по дуге. Если выключено — делается прямой скос под 45°.");
            DrawProp("chamfer", "Размер фаски по периметру (м)",
                "Ширина декоративного скоса по верхним и нижним краям платформы в метрах.");
            DrawProp("grooveCount", "Количество полос / канавок",
                "Сколько поперечных канавок со светящимися полосами разместить вдоль платформы (от 0 до 6).");
            DrawProp("grooveInset", "Отступ крайних канавок (м)",
                "Расстояние от внутреннего и внешнего краёв платформы до крайних канавок (м).");
            DrawProp("grooveWidth", "Ширина полосы / канавки (м)",
                "Ширина поперечной канавки и светящейся вставки в метрах.");
            DrawProp("grooveDepth", "Глубина канавки (м)",
                "Глубина декоративной канавки на поверхности платформы в метрах.");
            DrawProp("glowGrooves", "Подсветка в канавках",
                "Вставлять светящиеся неоновые полосы внутрь поперечных канавок.");
            DrawProp("glowSideAccents", "Боковые световые метки",
                "Добавлять тонкие светящиеся вертикальные полоски на боковых стенках платформы напротив канавок.");
            DrawProp("glowColor", "Цвет подсветки",
                "Цвет неоновых вставок в канавках (если не назначен отдельный материал подсветки).");
            DrawProp("glowIntensity", "Яркость свечения (HDR)",
                "Интенсивность свечения (Emission) неоновых полос.");

            EditorGUILayout.Space();
            DrawProp("fieldReach", "Прогиб магнитных дуг (м)",
                "Высота дугообразного прогиба энергетических потоков, идущих от колец труб к платформе.");
            DrawProp("fieldLayers", "Количество слоёв плазмы",
                "Количество внутренних радиальных слоёв плазмы между колонной и платформой (от 1 до 6).");
            DrawProp("tubeAzimuth0Rad", "Начальный угол трубы (рад)",
                "Азимут вертикальной трубы №1 в радианах (передаётся спавнером для синхронизации шейдера поля).");
            DrawProp("tubeDirection", "Направление нумерации труб",
                "Направление нумерации вертикальных труб колонны (+1 или −1).");
            DrawProp("tubeCount", "Количество труб по кругу",
                "Общее количество вертикальных труб по окружности колонны.");

            EditorGUILayout.Space();
            DrawProp("bodyMaterial", "Материал корпуса",
                "Материал твёрдой части платформы. Если пусто — автоматически создаётся стандартный материал в цвет колонны.");
            DrawProp("glowMaterial", "Материал подсветки (необяз.)",
                "Кастомный материал для светящихся полос. Если пусто — создаётся автоматически из цвета и яркости выше.");
            DrawProp("fieldMaterial", "Материал магнитного поля",
                "Материал с шейдером SciFi/MagneticFieldPlatform.");

            EditorGUILayout.Space();
            DrawProp("addCollider", "Создавать коллайдер",
                "Автоматически генерировать физический хитбокс по форме платформы.");
            DrawProp("colliderMode", "Тип хитбокса",
                "Выбор между точным контуром с гладкой верхней гранью, точным мешем с канавками или упрощёнными BoxCollider.");
            DrawProp("colliderChamfer", "Фаска в хитбоксе",
                "Добавлять скошенную кромку по краям хитбокса, чтобы мяч плавно съезжал с края.");
            DrawProp("physicMaterial", "Физический материал",
                "Physics Material (трение и упругость), назначаемый на коллайдер платформы.");
            EditorGUI.indentLevel--;
        }

        if (serializedObject.ApplyModifiedProperties() && !Application.isPlaying)
        {
            mp.Rebuild();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button(new GUIContent("Перестроить платформу", "Принудительно пересоздать 3D-меш, подсветку, магнитное поле и точный хитбокс этой платформы"), GUILayout.Height(24)))
            mp.Rebuild();
    }
}
#endif