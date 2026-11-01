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

    // Какая локальная ось префаба — его «перед» (именно она должна смотреть от колонны)
    public enum FrontAxis
    {
        PlusZ,  // Вперёд (Z+)
        MinusZ, // Назад (Z-)
        PlusY,  // Вверх (Y+)
        MinusY, // Вниз (Y-)
        PlusX,  // Вправо (X+)
        MinusX  // Влево (X-)
    }

    // Как поворачивать платформу при спавне относительно колонны
    public enum OrientationMode
    {
        PrefabAsIs,     // Ровно тот поворот, что сохранён в префабе — по умолчанию, настройка один раз
        FlatFrontOut,   // Плашмя (как префаб), разворот вокруг вертикали «носом» от колонны
        FrontFromColumn // «Перед» всегда строго от колонны (как лопасть колеса — для вертикальных колёс)
    }

    [System.Serializable]
    public class PlatformSettings
    {
        [Header("Позиция")]
        public float height = 5f;
        public float customRadius = 0f;

        [Header("Стартовая точка спавна")]
        [Tooltip("АБСОЛЮТНАЯ точка на окружности вокруг колонны (в градусах, 0-360). НЕ зависит от других настроек. Для плоскости XZ: 0 = вправо (X+), 90 = вперёд (Z+), 180 = влево (X-), 270 = назад (Z-).")]
        [Range(0f, 360f)]
        public float spawnPosition = 0f;

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
    [Tooltip("Какой коллайдер ставить платформе, если у префаба нет ни одного: Convex Mesh — повторяет форму меша, Box — параллелепипед по габаритам")]
    public AutoColliderMode autoColliderMode = AutoColliderMode.ConvexMesh;

    [Header("ЦЕНТР КОЛОННЫ")]
    public Transform columnCenter;
    public bool autoFindColumn = true;
    public string[] columnSearchNames = new string[] { "Column", "Column_Full", "MainColumn" };

    [Header("НАСТРОЙКИ ГЕНЕРАЦИИ")]
    public float defaultRadius = 3.4f;
    [Tooltip("Доворот платформ. В режиме «Точно как префаб» — поворот ВСЕХ платформ сразу вокруг вертикальной оси (0 = ровно как в префабе). В остальных режимах — доворот относительно колонны.")]
    public float rotationOffset = 0f;
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

    [Tooltip("Какая ось префаба — его «перед». Эта сторона платформы смотрит от колонны (в режимах «плашмя» и «лопасть»). Если платформа повёрнута не той стороной — попробуй другую ось.")]
    public FrontAxis frontAxis = FrontAxis.PlusZ;

    [Tooltip("Точно как префаб — платформа спавнится ровно с тем поворотом, с которым сохранён сам префаб: настроил один раз — все платформы на любых углах и колоннах стоят одинаково. Плашмя, носом от колонны — лежит как префаб и разворачивается передом от колонны. Нос от колонны — «перед» всегда перпендикулярен окружности (для вертикальных «колёс»).")]
    public OrientationMode orientationMode = OrientationMode.PrefabAsIs;

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

    // СТАРТОВАЯ ТОЧКА СПАВНА: абсолютный угол платформы на окружности.
    // Раньше угол позиции был СУММОЙ startAngle + angleOffset + spawnPosition +
    // zeroAngleDirection, поэтому слайдер Spawn Position не давал выбрать точную
    // точку. Теперь Spawn Position — единственный источник правды: платформа
    // ставится ровно туда, куда указывает этот угол, независимо от других настроек.
    private float GetAbsoluteSpawnAngle(PlatformSettings settings)
    {
        return Mathf.Repeat(settings.spawnPosition, 360f);
    }

    // Нормаль выбранной плоскости = ось орбиты вокруг колонны для этой плоскости.
    // XZ — ось Y, XY — ось Z (на камеру), YZ — ось X.
    private Vector3 GetPlaneNormal()
    {
        switch (rotationPlane)
        {
            case SpiralStepSpawner.RotationPlane.XY: return Vector3.forward;
            case SpiralStepSpawner.RotationPlane.YZ: return Vector3.right;
            default: return Vector3.up;
        }
    }

    // Возвращает направление «наружу от колонны» и поворот платформы.
    // Поворот зависит от OrientationMode: «плашмя» — платформа лежит как префаб
    // (параллельно полу) и только разворачивается вокруг вертикали «носом»
    // от колонны; «точно как префаб» — без всякого поворота; «нос от колонны» —
    // LookRotation, перед строго наружу (лопасть колеса).
    private void GetPlaneVectors(float angle, out Vector3 outwardLocal, out Quaternion rotation)
    {
        Vector3 upHint;
        switch (rotationPlane)
        {
            case SpiralStepSpawner.RotationPlane.XZ:
                outwardLocal = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
                upHint = Vector3.up;
                break;
            case SpiralStepSpawner.RotationPlane.XY:
                outwardLocal = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f);
                upHint = Vector3.back; // верх платформы смотрит в -Z (на стандартную камеру)
                break;
            case SpiralStepSpawner.RotationPlane.YZ:
                outwardLocal = new Vector3(0f, Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                upHint = Vector3.right;
                break;
            default:
                outwardLocal = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
                upHint = Vector3.up;
                break;
        }

        // 1) Разворот префаба: выбранная ось-«перед» приводится к локальной оси Z
        Quaternion frontCorrection;
        switch (frontAxis)
        {
            case SpiralStepSpawner.FrontAxis.MinusZ: frontCorrection = Quaternion.Euler(0f, 180f, 0f); break;
            case SpiralStepSpawner.FrontAxis.PlusY: frontCorrection = Quaternion.Euler(90f, 0f, 0f); break;
            case SpiralStepSpawner.FrontAxis.MinusY: frontCorrection = Quaternion.Euler(-90f, 0f, 0f); break;
            case SpiralStepSpawner.FrontAxis.PlusX: frontCorrection = Quaternion.Euler(0f, -90f, 0f); break;
            case SpiralStepSpawner.FrontAxis.MinusX: frontCorrection = Quaternion.Euler(0f, 90f, 0f); break;
            default: frontCorrection = Quaternion.identity; break; // PlusZ
        }

        switch (orientationMode)
        {
            case OrientationMode.FlatFrontOut:
                // Платформа лежит ПЛАШМЯ (параллельно полу, как префаб) и только
                // разворачивается вокруг вертикальной оси «носом» от колонны.
                // При смене Start Angle платформа НЕ наклоняется — просто поворачивается.
                rotation = Quaternion.Euler(0f, 90f - angle + rotationOffset, 0f) * frontCorrection;
                break;

            case OrientationMode.PrefabAsIs:
                // Поворот берётся прямо из префаба (см. GeneratePlatformsInternal) —
                // платформа спавнится ровно так, как лежит сам префаб
                rotation = Quaternion.identity;
                break;

            default: // FrontFromColumn — «нос от колонны», как лопасть колеса
                Quaternion facing = Quaternion.LookRotation(outwardLocal, upHint);
                // Rotation Offset — доворот ВОКРУГ направления от колонны:
                // «перед» остаётся направленным от колонны при любом значении
                rotation = Quaternion.AngleAxis(rotationOffset, outwardLocal) * facing * frontCorrection;
                break;
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
            float positionAngle = GetAbsoluteSpawnAngle(settings);

            Vector3 outwardLocal;
            Quaternion localRotation;
            GetPlaneVectors(positionAngle, out outwardLocal, out localRotation);
            // «Точно как префаб»: платформа спавнится ровно с тем поворотом,
            // с которым сохранён сам префаб, плюс один общий доворот (Rotation Offset)
            // вокруг вертикали. Никакой зависимости от угла и колонны — настроил
            // раз, и все платформы на всех колоннах стоят одинаково.
            Quaternion spawnRotation = orientationMode == OrientationMode.PrefabAsIs
                ? Quaternion.Euler(0f, rotationOffset, 0f) * platformPrefab.transform.rotation
                : center.rotation * localRotation;

            // Спавним префаб
            GameObject platformObj = Instantiate(platformPrefab, worldCenter, spawnRotation);
            platformObj.transform.SetParent(center, true);

#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(platformObj, "Generate Platform");
#endif

            // Метка спавнера — на КАЖДУЮ платформу, даже на стоячую:
            // раньше стоячие платформы нельзя было найти (у них нет RotatingPlatform)
            // и они дублировались при Play
            if (platformObj.GetComponent<SpiralPlatformMarker>() == null)
            {
                platformObj.AddComponent<SpiralPlatformMarker>();
            }

            // ФИЗИКА/КОЛЛИЗИИ: у префаба есть Rigidbody, но кинематичным его делает
            // только RotatingPlatform.Awake — то есть только вращающиеся платформы.
            // Стоячая платформа с обычным Rigidbody падала под гравитацией,
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
                // Ось орбиты = нормаль плоскости: в XY платформы крутятся вокруг Z
                // (вертикальное «колесо»), в XZ — вокруг Y, как раньше
                rotating.rotationAxis = GetPlaneNormal();
                // «Точно как префаб» — платформа всегда держит поворот префаба.
                // В вертикальных плоскостях «плашмя»-платформы тоже не кувыркаются —
                // держат поворот, как кабинки колеса обозрения
                rotating.keepOrientation = orientationMode == OrientationMode.PrefabAsIs
                    || (rotationPlane != RotationPlane.XZ && orientationMode != OrientationMode.FrontFromColumn);
                rotating.Initialize(center);
                rotating.angularSpeed = settings.rotationSpeed;
                rotating.clockwise = settings.clockwise;
            }

            spawnedPlatforms.Add(platformObj);
            Debug.Log($"[6] Платформа #{i + 1} успешно создана! Абсолютный угол: {positionAngle}°");
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

    // Быстрый способ расставить стартовые точки: равномерно по всей окружности.
    // Первая платформа — 0°, дальше с шагом 360°/кол-во платформ.
    [ContextMenu("Distribute Evenly Around Column")]
    public void DistributeEvenly()
    {
        if (platforms.Count == 0) return;
        float step = 360f / platforms.Count;
        for (int i = 0; i < platforms.Count; i++)
        {
            platforms[i].spawnPosition = Mathf.Repeat(i * step, 360f);
        }
        Debug.Log($"[Спавнер] Стартовые точки распределены равномерно: шаг {step}°.");
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

        EditorGUILayout.HelpBox("Start Angle каждой платформы — АБСОЛЮТНАЯ точка на окружности вокруг колонны (0-360°). Для плоскости XY: 0 = вправо (X+), 90 = вверх (Y+), 180 = влево (X-), 270 = вниз (Y-). Для XZ: 90 = вперёд (Z+), 270 = назад (Z-). Ориентация платформы задаётся режимом «Поворот платформы».", MessageType.Info);

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
            p.spawnPosition = EditorGUILayout.Slider("Start Angle (абсолют)", p.spawnPosition, 0f, 360f);
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
        spawner.rotationOffset = EditorGUILayout.FloatField("Доворот всех платформ (°)", spawner.rotationOffset);
        spawner.autoFindColumn = EditorGUILayout.Toggle("Auto Find Column", spawner.autoFindColumn);
        spawner.rotationPlane = (SpiralStepSpawner.RotationPlane)EditorGUILayout.EnumPopup("Rotation Plane", spawner.rotationPlane);
        string[] orientationNames = { "Точно как префаб (без поворота)", "Плашмя, носом от колонны", "Нос от колонны (как лопасть)" };
        spawner.orientationMode = (SpiralStepSpawner.OrientationMode)EditorGUILayout.Popup("Поворот платформы", (int)spawner.orientationMode, orientationNames);
        EditorGUILayout.HelpBox("«Точно как префаб» — платформа спавнится ровно с тем поворотом, с которым сохранён сам префаб: настроил префаб один раз — и ВСЕ платформы, на любых углах и любых колоннах, стоят одинаково. «Плашмя, носом от колонны» — лежит как префаб и разворачивается передом от колонны. «Нос от колонны» — перед всегда перпендикулярен окружности (для вертикальных «колёс»).", MessageType.None);
        if (spawner.orientationMode != SpiralStepSpawner.OrientationMode.PrefabAsIs)
        {
            string[] frontAxisNames = { "Вперёд (Z+)", "Назад (Z-)", "Вверх (Y+)", "Вниз (Y-)", "Вправо (X+)", "Влево (X-)" };
            spawner.frontAxis = (SpiralStepSpawner.FrontAxis)EditorGUILayout.Popup("Ось «переда» платформы", (int)spawner.frontAxis, frontAxisNames);
        }
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
