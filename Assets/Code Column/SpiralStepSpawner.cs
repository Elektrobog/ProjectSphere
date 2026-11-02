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
        ConvexMesh, // выпуклый MeshCollider — повторяет форму меша платформы
        Box         // BoxCollider по габаритам меша (простой параллелепипед)
    }

    // Плоскость вращения
    public enum RotationPlane
    {
        XZ,  // Горизонтальная
        XY,  // Вертикальная
        YZ   // Вертикальная
    }

    [System.Serializable]
    public class PlatformSettings
    {
        [Header("Позиция")]
        public float height = 5f;
        public float customRadius = 0f;

        [Header("Стартовая точка спавна")]
        [Tooltip("Угол платформы (°) ОТ СТАРТОВОЙ ТОЧКИ. Итоговый угол = Start Angle + это значение. Для плоскости XZ: 0 = вправо (X+), 90 = вперёд (Z+), 180 = влево (X-), 270 = назад (Z-).")]
        [Range(0f, 360f)]
        public float spawnPosition = 0f;

        [Header("Вращение")]
        public bool enableRotation = true;
        public float rotationSpeed = 45f;
        public bool clockwise = true;

        [Header("Визуал")]
        public Material customMaterial;
    }

    [Header("ОСНОВНЫЕ НАСТРОЙКИ")]
    public GameObject platformPrefab;

    [Header("ВРАЩЕНИЕ ПЛАТФОРМ")]
    [Tooltip("Per Platform — по галочке Enable Rotation у каждой платформы; All Rotate — все платформы крутятся; None Rotate — все стоят (галочки у платформ игнорируются)")]
    public RotationMode rotationMode = RotationMode.PerPlatform;

    [Header("КОЛЛИЗИИ")]
    [Tooltip("Какой коллайдер ставить платформе, если у префаба нет ни одного: Convex Mesh — повторяет форму меша, Box — параллелепипед по габаритам")]
    public AutoColliderMode autoColliderMode = AutoColliderMode.ConvexMesh;

    [Header("ЦЕНТР КОЛОННЫ")]
    public Transform columnCenter;
    public bool autoFindColumn = true;
    public string[] columnSearchNames = new string[] { "Column", "Column_Full", "MainColumn" };

    [Header("НАСТРОЙКИ ГЕНЕРАЦИИ")]
    public float defaultRadius = 3.4f;

    [Tooltip("Доворот платформы вокруг вертикали (на позицию не влияет). В плоскости XZ: 0 = нос платформы строго наружу от колонны, 90 = боком, 180 = носом к колонне.")]
    public float rotationOffset = 0f;

    [Tooltip("Стартовая точка на окружности (0-360°). Итоговый угол каждой платформы = Start Angle + её «Угол» из списка.")]
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

    [Header("Плоскость вращения")]
    [Tooltip("Выберите плоскость, в которой будут располагаться платформы")]
    public SpiralStepSpawner.RotationPlane rotationPlane = SpiralStepSpawner.RotationPlane.XZ;

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

    // ИТОГОВЫЙ УГОЛ ПЛАТФОРМЫ = Start Angle (стартовая точка) + «Угол» платформы.
    private float GetAbsoluteSpawnAngle(PlatformSettings settings)
    {
        return Mathf.Repeat(startAngle + settings.spawnPosition, 360f);
    }

    // Возвращает вектор и вращение в зависимости от выбранной плоскости
    private void GetPlaneVectors(float angle, out Vector3 outwardLocal, out Quaternion rotation)
    {
        switch (rotationPlane)
        {
            case SpiralStepSpawner.RotationPlane.XZ:
                outwardLocal = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
                rotation = Quaternion.LookRotation(outwardLocal, Vector3.up) * Quaternion.Euler(0f, rotationOffset, 0f);
                break;
            case SpiralStepSpawner.RotationPlane.XY:
                outwardLocal = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f);
                rotation = Quaternion.LookRotation(outwardLocal, Vector3.forward) * Quaternion.Euler(0f, 0f, rotationOffset);
                break;
            case SpiralStepSpawner.RotationPlane.YZ:
                outwardLocal = new Vector3(0f, Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                rotation = Quaternion.LookRotation(outwardLocal, Vector3.right) * Quaternion.Euler(rotationOffset, 0f, 0f);
                break;
            default:
                outwardLocal = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
                rotation = Quaternion.LookRotation(outwardLocal, Vector3.up) * Quaternion.Euler(0f, rotationOffset, 0f);
                break;
        }
    }

    private void GeneratePlatformsInternal(bool destroyImmediate)
    {
        if (platformPrefab == null)
        {
            Debug.LogError("[Спавнер] Префаб платформы (Platform Prefab) НЕ НАЗНАЧЕН в инспекторе!");
            return;
        }

        Transform center = FindColumnCenter();
        if (center == null)
        {
            Debug.LogError("[Спавнер] Колонна (Column Center) не найдена!");
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(center.gameObject))
        {
            Debug.LogError("[Спавнер] Column Center указывает на префаб из папки Project! Перетащи в поле объект со сцены (Hierarchy).");
            return;
        }
#endif

        if (destroyPreviousOnGenerate) ClearPlatforms(destroyImmediate);

        if (platforms.Count == 0)
        {
            Debug.LogWarning("[Спавнер] Список платформ ПУСТ! Нажми кнопку 'ДОБАВИТЬ ПЛАТФОРМУ' в инспекторе.");
            return;
        }

        Vector3 worldCenter = center.position;

        for (int i = 0; i < platforms.Count; i++)
        {
            var settings = platforms[i];
            float effectiveRadius = settings.customRadius > 0 ? settings.customRadius : defaultRadius;
            float positionAngle = GetAbsoluteSpawnAngle(settings);

            Vector3 outwardLocal;
            Quaternion localRotation;
            GetPlaneVectors(positionAngle, out outwardLocal, out localRotation);
            Quaternion spawnRotation = center.rotation * localRotation;

            // Спавним префаб
            GameObject platformObj = Instantiate(platformPrefab, worldCenter, spawnRotation);
            platformObj.transform.SetParent(center, true);

#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(platformObj, "Generate Platform");
#endif

            // Метка спавнера — на КАЖДУЮ платформу, даже на стоячую
            if (platformObj.GetComponent<SpiralPlatformMarker>() == null)
            {
                platformObj.AddComponent<SpiralPlatformMarker>();
            }

            // ФИЗИКА: делаем платформу кинематиком, чтобы она не падала под гравитацией
            Rigidbody platformRb = platformObj.GetComponent<Rigidbody>();
            if (platformRb == null) platformRb = platformObj.AddComponent<Rigidbody>();
            platformRb.isKinematic = true;
            platformRb.useGravity = false;

            // КОЛЛАЙДЕР: если у префаба нет коллайдера — добавляем автоматически
            Collider platformCol = platformObj.GetComponentInChildren<Collider>();
            if (platformCol == null)
            {
                AddAutoCollider(platformObj, i + 1);
            }

            Renderer meshRenderer = platformObj.GetComponentInChildren<Renderer>();
            if (meshRenderer == null)
            {
                Debug.LogWarning($"[Спавнер] В префабе '{platformPrefab.name}' нет ни одного Renderer'а (Mesh Renderer). Платформа не будет видна!");
                continue;
            }

            Vector3 outwardWorld = (center.rotation * outwardLocal).normalized;

            // ✅ ИСПРАВЛЕНО: Считываем реальные вершины меша, чтобы отступ был одинаковым на любом угле
            Mesh platformMesh = null;
            Transform platformMeshT = null;

            MeshFilter mf = platformObj.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                platformMesh = mf.sharedMesh;
                platformMeshT = mf.transform;
            }
            else
            {
                SkinnedMeshRenderer smr = platformObj.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null && smr.sharedMesh != null)
                {
                    platformMesh = smr.sharedMesh;
                    platformMeshT = smr.transform;
                }
            }

            if (platformMesh != null && platformMeshT != null)
            {
                float minProjR = float.MaxValue;
                float minProjY = float.MaxValue;

                Vector3[] verts = platformMesh.vertices;
                for (int v = 0; v < verts.Length; v++)
                {
                    Vector3 wv = platformMeshT.TransformPoint(verts[v]);
                    Vector3 rel = wv - worldCenter;

                    float projR = Vector3.Dot(rel, outwardWorld);
                    if (projR < minProjR) minProjR = projR;

                    float projY = rel.y;
                    if (projY < minProjY) minProjY = projY;
                }

                float shiftR = effectiveRadius - minProjR;
                float shiftY = settings.height - minProjY;

                platformObj.transform.position = worldCenter + outwardWorld * shiftR + Vector3.up * shiftY;
            }
            else
            {
                // Резервный вариант (если вдруг нет меша) — используем Bounds
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
            }

            // Установка материала
            if (settings.customMaterial != null) meshRenderer.sharedMaterial = settings.customMaterial;
            else if (defaultMaterial != null) meshRenderer.sharedMaterial = defaultMaterial;

            // Вращение
            if (ShouldRotate(settings))
            {
                RotatingPlatform rotating = platformObj.GetComponent<RotatingPlatform>();
                if (rotating == null) rotating = platformObj.AddComponent<RotatingPlatform>();
                rotating.Initialize(center);
                rotating.angularSpeed = settings.rotationSpeed;
                rotating.clockwise = settings.clockwise;
            }

            spawnedPlatforms.Add(platformObj);
        }

        Debug.Log($"[Спавнер] Успешно сгенерировано {spawnedPlatforms.Count} платформ.");
    }

    // Автоколлайдер для платформы, у которой нет ни одного коллайдера.
    private void AddAutoCollider(GameObject platformObj, int index)
    {
        if (autoColliderMode == AutoColliderMode.ConvexMesh)
        {
            MeshFilter mf = platformObj.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = true;
                return;
            }
        }

        Renderer colRend = platformObj.GetComponentInChildren<Renderer>();
        if (colRend != null)
        {
            BoxCollider box = platformObj.AddComponent<BoxCollider>();
            Vector3 lo = platformObj.transform.InverseTransformPoint(colRend.bounds.min);
            Vector3 hi = platformObj.transform.InverseTransformPoint(colRend.bounds.max);
            box.center = (lo + hi) * 0.5f;
            box.size = hi - lo;
        }
    }

    [ContextMenu("Clear Platforms")]
    public void ClearPlatforms() => ClearPlatforms(true);

    private void ClearPlatforms(bool destroyImmediate)
    {
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

    private void CollectPlatformObjects(Transform root, List<GameObject> list)
    {
        foreach (Transform child in root)
        {
            if (child.GetComponent<SpiralPlatformMarker>() != null ||
                child.GetComponent<RotatingPlatform>() != null)
            {
                list.Add(child.gameObject);
                continue;
            }
            CollectPlatformObjects(child, list);
        }
    }

    [ContextMenu("Add Platform")]
    public void AddPlatform() => platforms.Add(new PlatformSettings());

    [ContextMenu("Clear Platform List")]
    public void ClearPlatformList() => platforms.Clear();

    [ContextMenu("Distribute Evenly Around Column")]
    public void DistributeEvenly()
    {
        if (platforms.Count == 0) return;
        float step = 360f / platforms.Count;
        for (int i = 0; i < platforms.Count; i++)
        {
            platforms[i].spawnPosition = Mathf.Repeat(i * step, 360f);
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        Gizmos.color = gizmoColor;
        Transform center = columnCenter != null ? columnCenter : FindColumnCenter();
        if (center == null) return;

        foreach (var settings in platforms)
        {
            float effectiveRadius = settings.customRadius > 0 ? settings.customRadius : defaultRadius;
            float positionAngle = GetAbsoluteSpawnAngle(settings);
            Vector3 outwardLocal;
            Quaternion gizmoRotation;
            GetPlaneVectors(positionAngle, out outwardLocal, out gizmoRotation);
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

        EditorGUILayout.HelpBox("Итоговый угол каждой платформы = Start Angle (стартовая точка, в доп. настройках) + «Угол» платформы из списка. Пример: Start Angle = 0, угол платформы = 90 — платформа встанет на 90° от стартовой точки по окружности колонны (XZ: 0 = вправо X+, 90 = вперёд Z+). Поворот платформы (XZ): нос всегда наружу от колонны + Rotation Offset (0 = строго наружу).", MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("ДОБАВИТЬ ПЛАТФОРМУ"))
        {
            spawner.AddPlatform();
        }
        if (GUILayout.Button("РАСПРЕДЕЛИТЬ РАВНОМЕРНО"))
        {
            spawner.DistributeEvenly();
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
            p.customRadius = EditorGUILayout.FloatField("Custom Radius", p.customRadius);
            p.spawnPosition = EditorGUILayout.Slider("Угол (° от стартовой точки)", p.spawnPosition, 0f, 360f);

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
        spawner.rotationOffset = EditorGUILayout.FloatField("Rotation Offset (0 = нос наружу)", spawner.rotationOffset);
        spawner.startAngle = EditorGUILayout.Slider("Start Angle (стартовая точка)", spawner.startAngle, 0f, 360f);
        spawner.autoFindColumn = EditorGUILayout.Toggle("Auto Find Column", spawner.autoFindColumn);
        spawner.rotationPlane = (SpiralStepSpawner.RotationPlane)EditorGUILayout.EnumPopup("Rotation Plane", spawner.rotationPlane);
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