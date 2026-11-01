using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Метка "эта платформа создана спавнером". Кладётся на КАЖДУЮ сгенерированную
/// платформу (и на вращающиеся, и на стоящие) и сохраняется в сцене — по ней
/// спавнер находит свои платформы после Play и переоткрытия сцены.
/// </summary>
public class SpiralPlatformMarker : MonoBehaviour
{
}

[AddComponentMenu("Custom/Spiral Step Spawner")]
public class SpiralStepSpawner : MonoBehaviour
{
    // Правило вращения для всех создаваемых платформ
    public enum RotationMode
    {
        PerPlatform,  // по галочке Enable Rotation у каждой платформы
        AllRotate,    // все платформы крутятся
        NoneRotate    // все платформы стоят
    }

    // Какой коллайдер ставить платформе, если у неё нет ни одного
    public enum AutoColliderMode
    {
        TopDisc,    // цилиндр по ВЕРХНЕЙ поверхности платформы — шар падает ровно с видимого края
        ConvexMesh, // выпуклый MeshCollider — повторяет форму меша, но у скруглённых платформ верх выходит за видимый край
        Box         // BoxCollider по габаритам меша (простой параллелепипед)
    }

    [System.Serializable]
    public class PlatformSettings
    {
        [Header("Позиция")]
        public float height = 5f;
        public float angleOffset = 0f;
        public float customRadius = 0f;

        [Header("Вращение")]
        public bool enableRotation = true;
        public float rotationSpeed = 45f;
        public bool clockwise = true;

        [Header("Визуал")]
        public Material customMaterial;
        public Color platformColor = Color.white;
    }

    [Header("ОСНОВНЫЕ НАСТРОЙКИ")]
    public GameObject platformPrefab;

    [Header("ВРАЩЕНИЕ ПЛАТФОРМ")]
    [Tooltip("Per Platform — по галочке Enable Rotation у каждой платформы; All Rotate — все платформы крутятся; None Rotate — все стоят (галочки у платформ игнорируются)")]
    public RotationMode rotationMode = RotationMode.PerPlatform;

    [Header("КОЛЛИЗИИ")]
    [Tooltip("Какой коллайдер ставить платформе, если у префаба нет ни одного: Top Disc — цилиндр по верхней дорожке (шар падает с видимого края), Convex Mesh — выпуклая оболочка меша, Box — параллелепипед")]
    public AutoColliderMode autoColliderMode = AutoColliderMode.TopDisc;

    [Header("ЦЕНТР КОЛОННЫ")]
    public Transform columnCenter;
    public bool autoFindColumn = true;
    public string[] columnSearchNames = new string[] { "Column", "Column_Full", "MainColumn" };

    [Header("НАСТРОЙКИ ГЕНЕРАЦИИ")]
    public float defaultRadius = 3.4f;
    public float rotationOffset = 0f;
    public float startAngle = 0f;
    public bool generateOnStart = true;
    public bool destroyPreviousOnGenerate = true;

    [Header("СПИСОК ПЛАТФОРМ")]
    public List<PlatformSettings> platforms = new List<PlatformSettings>();

    [Header("МАТЕРИАЛЫ")]
    public Material defaultMaterial;
    public Color defaultColor = new Color(0.75f, 0.78f, 0.85f, 1.0f);

    [Header("ДОПОЛНИТЕЛЬНО")]
    public bool parentToColumn = true;
    public bool showGizmos = true;
    public Color gizmoColor = Color.cyan;

    private List<GameObject> spawnedPlatforms = new List<GameObject>();

    private Transform FindColumnCenter()
    {
        if (columnCenter != null) return columnCenter;
        foreach (string name in columnSearchNames)
        {
            GameObject found = GameObject.Find(name);
            if (found != null) return found.transform;
        }
        return transform;
    }

    private void Start()
    {
        if (autoFindColumn) columnCenter = FindColumnCenter();
        if (generateOnStart)
        {
            // Если платформы уже сгенерированы в редакторе (или стоят в сцене) —
            // НЕ спавним дубликаты, а принимаем существующие в учёт
            List<GameObject> existing = new List<GameObject>();
            CollectPlatformObjects(FindColumnCenter(), existing);
            existing.RemoveAll(p => p == null);
            if (existing.Count > 0)
            {
                spawnedPlatforms.AddRange(existing);
                Debug.Log($"[Спавнер] На колонне уже есть {existing.Count} платформ — повторная генерация пропущена.");
            }
            else
            {
                GeneratePlatformsInternal(false);
            }
        }
    }

    public void GeneratePlatformsRuntime() => GeneratePlatformsInternal(false);

    [ContextMenu("Generate Platforms")]
    public void GeneratePlatforms() => GeneratePlatformsInternal(true);

    // Итоговое решение, крутится ли платформа: глобальный режим приоритетнее галочки
    private bool ShouldRotate(PlatformSettings settings)
    {
        switch (rotationMode)
        {
            case RotationMode.AllRotate: return true;
            case RotationMode.NoneRotate: return false;
            default: return settings.enableRotation;
        }
    }

    private void GeneratePlatformsInternal(bool destroyImmediate)
    {
        Debug.Log("=== [1] НАЧАЛО ГЕНЕРАЦИИ ===");

        if (platformPrefab == null)
        {
            Debug.LogError("[ОШИБКА] Префаб платформы (Platform Prefab) НЕ НАЗНАЧЕН в инспекторе!");
            return;
        }
        Debug.Log($"[2] Префаб назначен: {platformPrefab.name}");

        Transform center = FindColumnCenter();
        if (center == null)
        {
            Debug.LogError("[ОШИБКА] Колонна (Column Center) не найдена!");
            return;
        }
        Debug.Log($"[3] Колонна найдена: {center.name}");

#if UNITY_EDITOR
        if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(center.gameObject))
        {
            Debug.LogError("[ОШИБКА] Column Center указывает на префаб из папки Project! Перетащи в поле объект со сцены (Hierarchy).");
            return;
        }
#endif
        Debug.Log("[4] Проверка на префаб-ассет пройдена.");

        if (destroyPreviousOnGenerate) ClearPlatforms(destroyImmediate);

        if (platforms.Count == 0)
        {
            Debug.LogWarning("[ПРЕДУПРЕЖДЕНИЕ] Список платформ ПУСТ! Нажми кнопку 'ДОБАВИТЬ ПЛАТФОРМУ' в инспекторе.");
            return;
        }
        Debug.Log($"[5] Платформ в списке: {platforms.Count}");

        Vector3 worldCenter = center.position;

        for (int i = 0; i < platforms.Count; i++)
        {
            var settings = platforms[i];
            float effectiveRadius = settings.customRadius > 0 ? settings.customRadius : defaultRadius;
            float angle = startAngle + settings.angleOffset;

            Quaternion spawnRotation = center.rotation * Quaternion.Euler(0f, -angle + 90f + rotationOffset, 0f);

            // Спавним префаб
            GameObject platformObj = Instantiate(platformPrefab, worldCenter, spawnRotation);
            platformObj.transform.SetParent(center, true);

#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(platformObj, "Generate Platform");
#endif

            // Метка спавнера — на КАЖДУЮ платформу, даже на стоящую:
            // раньше стоящие платформы нельзя было найти (у них нет RotatingPlatform)
            // и они дублировались при Play
            if (platformObj.GetComponent<SpiralPlatformMarker>() == null)
            {
                platformObj.AddComponent<SpiralPlatformMarker>();
            }

            // ФИЗИКА/КОЛЛИЗИИ: у префаба есть Rigidbody, но кинематичным его делает
            // только RotatingPlatform.Awake — то есть только вращающиеся платформы.
            // Стоящая платформа с обычным Rigidbody падала под гравитацией,
            // и её выталкивало коллайдером колонны (сбиты поворот, высота, позиция).
            // Поэтому каждую созданную платформу делаем кинематиком без гравитации.
            Rigidbody platformRb = platformObj.GetComponent<Rigidbody>();
            if (platformRb == null) platformRb = platformObj.AddComponent<Rigidbody>();
            platformRb.isKinematic = true;
            platformRb.useGravity = false;

            // КОЛЛАЙДЕР: Rigidbody без коллайдера бесполезен — шар проваливался.
            // Если у префаба нет ни одного коллайдера — ставим свой (см. AddAutoCollider)
            Collider platformCol = platformObj.GetComponentInChildren<Collider>();
            if (platformCol == null)
            {
                AddAutoCollider(platformObj, i + 1);
            }

            Renderer meshRenderer = platformObj.GetComponentInChildren<Renderer>();
            if (meshRenderer == null)
            {
                Debug.LogWarning($"[ПРЕДУПРЕЖДЕНИЕ] В префабе '{platformPrefab.name}' нет ни одного Renderer'а (Mesh Renderer). Платформа не будет видна!");
                continue;
            }

            Vector3 outwardLocal = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
            Vector3 outwardWorld = (center.rotation * outwardLocal).normalized;

            Bounds b = meshRenderer.bounds;
            float minProjR = float.MaxValue;
            float minProjY = float.MaxValue;

            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = b.center + new Vector3(
                    (c & 1) == 0 ? b.extents.x : -b.extents.x,
                    (c & 2) == 0 ? b.extents.y : -b.extents.y,
                    (c & 4) == 0 ? b.extents.z : -b.extents.z
                );
                Vector3 rel = corner - worldCenter;
                float projR = Vector3.Dot(rel, outwardWorld);
                if (projR < minProjR) minProjR = projR;
                float projY = rel.y;
                if (projY < minProjY) minProjY = projY;
            }

            float shiftR = effectiveRadius - minProjR;
            float shiftY = settings.height - minProjY;

            platformObj.transform.position = worldCenter + outwardWorld * shiftR + Vector3.up * shiftY;

            if (settings.customMaterial != null) meshRenderer.sharedMaterial = settings.customMaterial;
            else if (defaultMaterial != null) meshRenderer.sharedMaterial = defaultMaterial;

            if (ShouldRotate(settings))
            {
                RotatingPlatform rotating = platformObj.GetComponent<RotatingPlatform>();
                if (rotating == null) rotating = platformObj.AddComponent<RotatingPlatform>();
                rotating.Initialize(center);
                rotating.angularSpeed = settings.rotationSpeed;
                rotating.clockwise = settings.clockwise;
            }

            spawnedPlatforms.Add(platformObj);
            Debug.Log($"[6] Платформа #{i + 1} успешно создана!");
        }

        Debug.Log($"=== [7] ГОТОВО. Сгенерировано {spawnedPlatforms.Count} платформ ===");
    }

    // Автоколлайдер для платформы, у которой нет ни одного коллайдера.
    // По умолчанию — выпуклый MeshCollider, он повторяет форму меша (скруглённые
    // края платформы, в отличие от прямого BoxCollider).
    // На кинематическом Rigidbody Unity разрешает только CONVEX MeshCollider,
    // поэтому выпуклая оболочка — потолок точности, точнее только свой коллайдер в префабе.
    private void AddAutoCollider(GameObject platformObj, int index)
    {
        // Лучший режим: цилиндр по верхней "дорожке" платформы. Convex MeshCollider
        // у скруглённой платформы расплющивает верх до самой широкой точки меша —
        // шар получает невидимую полку за краем и не падает. Top Disc строит
        // цилиндр по фактическому радиусу верхней поверхности (с запасом 2% внутрь).
        if (autoColliderMode == AutoColliderMode.TopDisc)
        {
            if (TryAddTopDiscCollider(platformObj, index)) return;
            Debug.LogWarning($"[Спавнер] Платформе #{index} не удалось построить цилиндр-коллайдер (Top Disc) — пробую Convex Mesh.");
        }

        if (autoColliderMode == AutoColliderMode.ConvexMesh)
        {
            MeshFilter mf = platformObj.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                // Вешаем на тот же объект, где лежит меш, иначе коллайдер сместится
                MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = true;
                Debug.Log($"[Спавнер] Платформе #{index} добавлен Convex MeshCollider (по форме меша).");
                return;
            }
            Debug.LogWarning($"[Спавнер] У платформы #{index} не найден MeshFilter — ставлю запасной BoxCollider.");
        }

        // Запасной вариант: BoxCollider по габаритам меша
        Renderer colRend = platformObj.GetComponentInChildren<Renderer>();
        if (colRend != null)
        {
            BoxCollider box = platformObj.AddComponent<BoxCollider>();
            Vector3 lo = platformObj.transform.InverseTransformPoint(colRend.bounds.min);
            Vector3 hi = platformObj.transform.InverseTransformPoint(colRend.bounds.max);
            box.center = (lo + hi) * 0.5f;
            box.size = hi - lo;
            Debug.Log($"[Спавнер] Платформе #{index} добавлен BoxCollider по мешу.");
        }
    }

    // Строит цилиндр-коллайдер по верхней поверхности платформы.
    // ВСЁ считается относительно ЦЕНТРА ГАБАРИТОВ меша (mesh.bounds.center),
    // а не локального нуля: у FBX-моделей пивот часто стоит в стороне
    // (например, в центре сцены), и расчёт от нуля давал гигантские
    // "случайные" коллайдеры. Здесь это не влияет на результат.
    private bool TryAddTopDiscCollider(GameObject platformObj, int index)
    {
        MeshFilter mf = platformObj.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return false;

        Mesh mesh = mf.sharedMesh;
        Vector3[] vertices;
        try { vertices = mesh.vertices; }
        catch { return false; } // меш без Read/Write — уходим в Convex Mesh

        Bounds mb = mesh.bounds;
        Vector3 c = mb.center; // центр габаритов в локальных координатах меша

        // Верхняя поверхность: вершины в верхних 10% высоты меша
        float yTop = mb.max.y - mb.size.y * 0.10f;
        float radius = 0f;
        bool found = false;
        foreach (Vector3 v in vertices)
        {
            if (v.y < yTop) continue;
            float dx = v.x - c.x;
            float dz = v.z - c.z;
            float r = Mathf.Sqrt(dx * dx + dz * dz);
            if (!found || r > radius) { radius = r; found = true; }
        }
        if (!found || radius < 0.05f) return false;

        radius *= 0.98f; // небольшой запас внутрь, чтобы шар отпускался у самого края

        Mesh cylinder = BuildCylinderMesh(c.x, c.z, radius, mb.min.y, mb.max.y, 20);
        MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = cylinder;
        mc.convex = true;
        Debug.Log($"[Спавнер] Платформе #{index} добавлен цилиндр-коллайдер Top Disc (центр меша локально: {c.x:F2}, {c.z:F2}; радиус {radius:F2}).");
        return true;
    }

    // Низкополигональный цилиндр вокруг точки (cx, cz) для convex MeshCollider
    private Mesh BuildCylinderMesh(float cx, float cz, float radius, float y0, float y1, int segments)
    {
        Vector3[] verts = new Vector3[segments * 2 + 2];
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            float x = cx + Mathf.Cos(a) * radius;
            float z = cz + Mathf.Sin(a) * radius;
            verts[i] = new Vector3(x, y0, z);
            verts[i + segments] = new Vector3(x, y1, z);
        }
        int bottomCenter = segments * 2;
        int topCenter = bottomCenter + 1;
        verts[bottomCenter] = new Vector3(cx, y0, cz);
        verts[topCenter] = new Vector3(cx, y1, cz);

        int[] tris = new int[segments * 6];
        int t = 0;
        for (int i = 0; i < segments; i++)
        {
            int j = (i + 1) % segments;
            tris[t++] = i; tris[t++] = j; tris[t++] = j + segments;
            tris[t++] = i; tris[t++] = j + segments; tris[t++] = i + segments;
        }

        Mesh m = new Mesh();
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    [ContextMenu("Clear Platforms")]
    public void ClearPlatforms() => ClearPlatforms(true);

    private void ClearPlatforms(bool destroyImmediate)
    {
        // Уничтожаем ВСЕ платформы-дети колонны, а не только запомненные в списке:
        // список не сериализуется, и после Play / переоткрытия сцены он пуст
        Transform center = FindColumnCenter();
        if (center != null)
        {
            List<GameObject> found = new List<GameObject>();
            CollectPlatformObjects(center, found);
            foreach (GameObject obj in found)
            {
                if (obj == null) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying) Undo.DestroyObjectImmediate(obj);
                else
#endif
                    if (destroyImmediate) DestroyImmediate(obj);
                    else Destroy(obj);
            }
        }

        // Подстраховка: платформы из списка, потерявшие родителя
        foreach (var platform in spawnedPlatforms)
        {
            if (platform != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) Undo.DestroyObjectImmediate(platform);
                else
#endif
                    if (destroyImmediate) DestroyImmediate(platform);
                    else Destroy(platform);
            }
        }
        spawnedPlatforms.Clear();
    }

    // Все платформы-дети колонны (даже не попавшие в список — сгенерированные раньше).
    // Ищем по метке спавнера ИЛИ по RotatingPlatform — покрываем и старые платформы,
    // созданные до появления метки
    private void CollectPlatformObjects(Transform root, List<GameObject> list)
    {
        foreach (Transform child in root)
        {
            if (child.GetComponent<SpiralPlatformMarker>() != null ||
                child.GetComponent<RotatingPlatform>() != null)
            {
                list.Add(child.gameObject);
                continue; // под платформу не заходим
            }
            CollectPlatformObjects(child, list);
        }
    }

    [ContextMenu("Add Platform")]
    public void AddPlatform() => platforms.Add(new PlatformSettings());

    [ContextMenu("Clear Platform List")]
    public void ClearPlatformList() => platforms.Clear();

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        Gizmos.color = gizmoColor;
        Transform center = columnCenter != null ? columnCenter : FindColumnCenter();
        if (center == null) return;

        foreach (var settings in platforms)
        {
            float effectiveRadius = settings.customRadius > 0 ? settings.customRadius : defaultRadius;
            float angle = startAngle + settings.angleOffset;
            Vector3 outwardLocal = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
            Vector3 outwardWorld = (center.rotation * outwardLocal).normalized;
            Vector3 pos = center.position + outwardWorld * effectiveRadius + Vector3.up * settings.height;
            Gizmos.DrawWireSphere(pos, 0.3f);
            Gizmos.DrawLine(center.position + Vector3.up * settings.height, pos);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SpiralStepSpawner))]
[CanEditMultipleObjects]
public class SpiralStepSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var spawner = (SpiralStepSpawner)target;

        GUILayout.Label("SPIRAL STEP SPAWNER", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("ОСНОВНЫЕ НАСТРОЙКИ", EditorStyles.boldLabel);
        spawner.platformPrefab = (GameObject)EditorGUILayout.ObjectField("Platform Prefab", spawner.platformPrefab, typeof(GameObject), false);
        spawner.columnCenter = (Transform)EditorGUILayout.ObjectField("Column Center", spawner.columnCenter, typeof(Transform), true);
        spawner.generateOnStart = EditorGUILayout.Toggle("Generate On Start", spawner.generateOnStart);
        spawner.destroyPreviousOnGenerate = EditorGUILayout.Toggle("Destroy Previous On Generate", spawner.destroyPreviousOnGenerate);
        spawner.rotationMode = (SpiralStepSpawner.RotationMode)EditorGUILayout.EnumPopup("Rotation Mode", spawner.rotationMode);
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("КОЛЛИЗИИ", EditorStyles.boldLabel);
        spawner.autoColliderMode = (SpiralStepSpawner.AutoColliderMode)EditorGUILayout.EnumPopup("Auto Collider Mode", spawner.autoColliderMode);
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("УПРАВЛЕНИЕ ГЕНЕРАЦИЕЙ", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("СГЕНЕРИРОВАТЬ"))
        {
            foreach (var obj in targets)
            {
                var s = (SpiralStepSpawner)obj;
                if (Application.isPlaying) s.GeneratePlatformsRuntime();
                else s.GeneratePlatforms();
            }
        }
        if (GUILayout.Button("ОЧИСТИТЬ"))
        {
            foreach (var obj in targets)
            {
                var s = (SpiralStepSpawner)obj;
                s.ClearPlatforms();
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("СПИСОК ПЛАТФОРМ", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("ДОБАВИТЬ ПЛАТФОРМУ"))
        {
            spawner.AddPlatform();
        }
        if (GUILayout.Button("ОЧИСТИТЬ СПИСОК"))
        {
            spawner.ClearPlatformList();
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);

        for (int i = 0; i < spawner.platforms.Count; i++)
        {
            var p = spawner.platforms[i];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Платформа #{i + 1}", EditorStyles.boldLabel);

            p.height = EditorGUILayout.FloatField("Height", p.height);
            p.angleOffset = EditorGUILayout.FloatField("Angle Offset", p.angleOffset);
            p.customRadius = EditorGUILayout.FloatField("Custom Radius", p.customRadius);
            // Галочка Enable Rotation показывается только в режиме Per Platform —
            // в глобальных режимах её состояние игнорируется
            if (spawner.rotationMode == SpiralStepSpawner.RotationMode.PerPlatform)
            {
                p.enableRotation = EditorGUILayout.Toggle("Enable Rotation", p.enableRotation);
            }
            p.rotationSpeed = EditorGUILayout.FloatField("Rotation Speed", p.rotationSpeed);
            p.clockwise = EditorGUILayout.Toggle("Clockwise", p.clockwise);
            p.customMaterial = (Material)EditorGUILayout.ObjectField("Custom Material", p.customMaterial, typeof(Material), false);

            if (GUILayout.Button("Удалить"))
            {
                spawner.platforms.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("ДОПОЛНИТЕЛЬНЫЕ НАСТРОЙКИ", EditorStyles.boldLabel);
        spawner.defaultRadius = EditorGUILayout.FloatField("Default Radius", spawner.defaultRadius);
        spawner.rotationOffset = EditorGUILayout.FloatField("Rotation Offset", spawner.rotationOffset);
        spawner.startAngle = EditorGUILayout.FloatField("Start Angle", spawner.startAngle);
        spawner.autoFindColumn = EditorGUILayout.Toggle("Auto Find Column", spawner.autoFindColumn);
        spawner.parentToColumn = EditorGUILayout.Toggle("Parent To Column", spawner.parentToColumn);
        spawner.defaultMaterial = (Material)EditorGUILayout.ObjectField("Default Material", spawner.defaultMaterial, typeof(Material), false);
        spawner.defaultColor = EditorGUILayout.ColorField("Default Color", spawner.defaultColor);
        spawner.showGizmos = EditorGUILayout.Toggle("Show Gizmos", spawner.showGizmos);
        spawner.gizmoColor = EditorGUILayout.ColorField("Gizmo Color", spawner.gizmoColor);
        EditorGUILayout.EndVertical();

        if (GUI.changed) EditorUtility.SetDirty(spawner);
    }
}
#endif
