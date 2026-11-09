using UnityEngine;
using System.Collections.Generic;
using System.Globalization;

#if UNITY_EDITOR
using UnityEditor;
#endif

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
        [HideInInspector] public string id;

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

        [Tooltip("Реверс: платформа крутится в обратную сторону.")]
        public bool reverse = false;

        // Старое поле (По часовой). Оставлено только для переноса старых данных в reverse.
        [HideInInspector] public bool clockwise = true;

        [Header("Визуал")]
        public Material customMaterial;
    }

    [Header("ОСНОВНЫЕ НАСТРОЙКИ")]
    public GameObject platformPrefab;

    [Header("ВРАЩЕНИЕ ПЛАТФОРМ")]
    [Tooltip("Per Platform — по галочке Enable Rotation у каждой платформы; All Rotate — все платформы крутятся; None Rotate — все стоят (галочки у платформ игнорируются)")]
    public RotationMode rotationMode = RotationMode.PerPlatform;

    [Tooltip("Развернуть направление вращения у ВСЕХ платформ сразу (суммируется с галочкой Реверс у платформы).")]
    public bool reverseAll = false;

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

    [Tooltip("При добавлении следующей платформы её высота увеличивается на это значение.")]
    public float heightStep = 2f;

    [Tooltip("При добавлении следующей платформы её угол сдвигается на это значение (0 = тот же угол, что у предыдущей).")]
    public float angleStep = 0f;

    [SerializeField, HideInInspector] private string spawnerId = "";

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
    private bool warnedNonReadable; // предупреждение про Read/Write показываем один раз за генерацию

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
        EnsurePlatformIds();
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

    private static Vector3[] BoundsCorners(Bounds b)
    {
        Vector3 c = b.center, e = b.extents;
        var pts = new Vector3[8];
        for (int k = 0; k < 8; k++)
        {
            pts[k] = c + new Vector3(
                (k & 1) == 0 ? e.x : -e.x,
                (k & 2) == 0 ? e.y : -e.y,
                (k & 4) == 0 ? e.z : -e.z);
        }
        return pts;
    }

    private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    private static bool IsFinite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
    private const float MaxCoord = 100000f; // всё, что дальше, физика Unity не переваривает (Invalid worldAABB)

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

        warnedNonReadable = false;
        EnsurePlatformIds();
        if (string.IsNullOrEmpty(spawnerId)) spawnerId = System.Guid.NewGuid().ToString("N");

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
            float platformHeight = settings.height;
            if (!IsFinite(effectiveRadius) || !IsFinite(platformHeight) ||
                Mathf.Abs(effectiveRadius) > MaxCoord || Mathf.Abs(platformHeight) > MaxCoord)
            {
                Debug.LogError($"[Спавнер] Платформа #{i + 1}: некорректные значения (высота = {platformHeight}, радиус = {effectiveRadius}). Платформа пропущена.");
                continue;
            }
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
            SpiralPlatformMarker marker = platformObj.GetComponent<SpiralPlatformMarker>();
            if (marker == null) marker = platformObj.AddComponent<SpiralPlatformMarker>();
            marker.spawnerId = spawnerId;
            marker.platformId = settings.id;

            // ФИЗИКА: делаем платформу кинематиком, чтобы она не падала под гравитацией
            Rigidbody platformRb = platformObj.GetComponent<Rigidbody>();
            if (platformRb == null) platformRb = platformObj.AddComponent<Rigidbody>();
            platformRb.isKinematic = true;
            platformRb.useGravity = false;
            // Интерполяция: физика крутит платформу 50 раз/с, интерполяция сглаживает движение между кадрами (убирает дёрганье)
            platformRb.interpolation = RigidbodyInterpolation.Interpolate;

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

            if (platformMesh != null && platformMeshT != null && platformMesh.vertexCount > 0)
            {
                float minProjR = float.MaxValue;
                float minProjY = float.MaxValue;

                // Если у меша выключен Read/Write, mesh.vertices недоступны (пустой массив + ошибка) —
                // тогда берём 8 углов mesh.bounds: они доступны всегда.
                Vector3[] verts = platformMesh.isReadable ? platformMesh.vertices : BoundsCorners(platformMesh.bounds);
                if (verts == null || verts.Length == 0) verts = BoundsCorners(platformMesh.bounds);
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
                float shiftY = platformHeight - minProjY;

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
                float shiftY = platformHeight - minProjY;

                platformObj.transform.position = worldCenter + outwardWorld * shiftR + Vector3.up * shiftY;
            }

            // Страховка: если позиция получилась NaN/бесконечной/огромной — Unity сыплет "Invalid worldAABB"
            if (!IsFinite(platformObj.transform.position) || platformObj.transform.position.magnitude > MaxCoord)
            {
                Debug.LogError($"[Спавнер] Платформа #{i + 1}: рассчитана неверная позиция {platformObj.transform.position}. " +
                               $"Проверьте меш префаба и значения высоты/радиуса. Платформа удалена.");
                if (Application.isPlaying) Destroy(platformObj); else DestroyImmediate(platformObj);
                continue;
            }

            // Установка материала
            if (settings.customMaterial != null) meshRenderer.sharedMaterial = settings.customMaterial;
            else if (defaultMaterial != null) meshRenderer.sharedMaterial = defaultMaterial;

            // Вращение
            RotatingPlatform rotating = platformObj.GetComponent<RotatingPlatform>();
            bool shouldRotate = ShouldRotate(settings);
            if (shouldRotate)
            {
                if (rotating == null) rotating = platformObj.AddComponent<RotatingPlatform>();
                rotating.enabled = true;
                rotating.Initialize(center);
                rotating.angularSpeed = IsFinite(settings.rotationSpeed) ? settings.rotationSpeed : 0f;
                rotating.clockwise = !(settings.reverse ^ reverseAll);
            }
            else if (rotating != null)
            {
                // Важно: если компонент был в самом префабе, он тоже должен отключаться.
                rotating.enabled = false;
            }

            spawnedPlatforms.Add(platformObj);
        }

        Debug.Log($"[Спавнер] Успешно сгенерировано {spawnedPlatforms.Count} платформ.");
    }

    // Автоколлайдер для платформы, у которой нет ни одного коллайдера.
    private void AddAutoCollider(GameObject platformObj, int index)
    {
        MeshFilter mf = platformObj.GetComponentInChildren<MeshFilter>();
        bool hasMesh = mf != null && mf.sharedMesh != null;

        // Выпуклый MeshCollider — только если меш читаем (Read/Write включён)
        if (autoColliderMode == AutoColliderMode.ConvexMesh && hasMesh && mf.sharedMesh.isReadable)
        {
            MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = true;
            return;
        }

        // Box по mesh.bounds (локальные габариты меша, работает и без Read/Write)
        if (hasMesh)
        {
            if (autoColliderMode == AutoColliderMode.ConvexMesh && !warnedNonReadable)
            {
                warnedNonReadable = true;
                string assetPath = "";
#if UNITY_EDITOR
                assetPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
#endif
                Debug.LogWarning($"[Спавнер] У меша '{mf.sharedMesh.name}' выключен Read/Write — вместо Convex Mesh поставлен Box Collider (для плоской платформы это нормально). " +
                                 $"Чтобы убрать предупреждение: выделите файл «{assetPath}» → Inspector → Model → Read/Write → Apply. " +
                                 $"Либо переключите Auto Collider Mode в Box. Клик по этому сообщению выделит файл.", mf.sharedMesh);
            }
            BoxCollider box = mf.gameObject.AddComponent<BoxCollider>();
            box.center = mf.sharedMesh.bounds.center;
            box.size = mf.sharedMesh.bounds.size;
            return;
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

#if UNITY_EDITOR
        // Старые платформы, у которых метка превратилась в Missing Script (она раньше лежала не в своём файле):
        // по метке их уже не найти, поэтому узнаём по имени "<префаб>(Clone)" + пропавший скрипт.
        if (center != null && platformPrefab != null)
        {
            string cloneName = platformPrefab.name + "(Clone)";
            var broken = new List<GameObject>();
            foreach (Transform child in center)
            {
                if (child.name == cloneName &&
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                    broken.Add(child.gameObject);
            }
            foreach (GameObject obj in broken)
            {
                if (obj == null) continue;
                if (!Application.isPlaying) Undo.DestroyObjectImmediate(obj);
                else Destroy(obj);
            }
        }
#endif

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
            SpiralPlatformMarker marker = child.GetComponent<SpiralPlatformMarker>();
            // Учитываем только объекты, принадлежащие этому экземпляру спавнера.
            // Это не позволяет одному спавнеру удалить платформы другого.
            if (marker != null && marker.spawnerId == spawnerId)
            {
                list.Add(child.gameObject);
                continue;
            }
            CollectPlatformObjects(child, list);
        }
    }

    /// <summary>Гарантирует уникальные ID платформ и переносит старое clockwise в reverse. Возвращает true, если что-то изменилось.</summary>
    public bool EnsurePlatformIds()
    {
        bool changed = false;
        if (string.IsNullOrEmpty(spawnerId))
        {
            spawnerId = System.Guid.NewGuid().ToString("N");
            changed = true;
        }

        var used = new HashSet<string>();
        foreach (var platform in platforms)
        {
            if (platform == null) continue;
            if (string.IsNullOrEmpty(platform.id) || used.Contains(platform.id))
            {
                platform.id = System.Guid.NewGuid().ToString("N");
                changed = true;
            }
            used.Add(platform.id);

            // Миграция: раньше было «По часовой» (false = в другую сторону), теперь «Реверс».
            if (!platform.clockwise)
            {
                platform.reverse = true;
                platform.clockwise = true;
                changed = true;
            }
        }
        return changed;
    }

    // Копирует ВСЕ параметры платформы (кроме id) и сдвигает высоту/угол на шаги.
    private PlatformSettings CreateNextFrom(PlatformSettings source)
    {
        PlatformSettings next = new PlatformSettings();
        if (source != null)
        {
            next.height = source.height + heightStep;
            next.customRadius = source.customRadius;
            next.spawnPosition = Mathf.Repeat(source.spawnPosition + angleStep, 360f);
            next.enableRotation = source.enableRotation;
            next.rotationSpeed = source.rotationSpeed;
            next.reverse = source.reverse;
            next.customMaterial = source.customMaterial;
        }
        next.id = System.Guid.NewGuid().ToString("N");
        return next;
    }

    [ContextMenu("Add Platform")]
    public void AddPlatform() => AddPlatforms(1);

    /// <summary>Добавляет count платформ подряд: каждая берёт параметры предыдущей + шаг высоты и угла.</summary>
    public void AddPlatforms(int count)
    {
        EnsurePlatformIds();
        for (int n = 0; n < Mathf.Max(1, count); n++)
        {
            PlatformSettings previous = platforms.Count > 0 ? platforms[platforms.Count - 1] : null;
            platforms.Add(CreateNextFrom(previous));
        }
    }

    /// <summary>Дублирует платформу index: копия встаёт сразу после неё (с шагом высоты/угла).</summary>
    public void DuplicatePlatformAt(int index)
    {
        if (index < 0 || index >= platforms.Count) return;
        EnsurePlatformIds();
        platforms.Insert(index + 1, CreateNextFrom(platforms[index]));
    }

    /// <summary>Сортирует список по высоте (снизу вверх). Объекты в сцене привязаны по ID и не страдают.</summary>
    public void SortByHeight()
    {
        platforms.Sort((a, b) => a.height.CompareTo(b.height));
    }

    /// <summary>Ищет в сцене объект платформы по её ID (null, если ещё не сгенерирована).</summary>
    public GameObject FindPlatformObject(string platformId)
    {
        Transform center = FindColumnCenter();
        if (center == null) return null;
        List<GameObject> found = new List<GameObject>();
        CollectPlatformObjects(center, found);
        foreach (GameObject obj in found)
        {
            if (obj == null) continue;
            SpiralPlatformMarker marker = obj.GetComponent<SpiralPlatformMarker>();
            if (marker != null && marker.platformId == platformId) return obj;
        }
        return null;
    }

    /// <summary>
    /// Удаляет ИМЕННО выбранную платформу (по ID), а не первую созданную.
    /// Если у объектов в сцене старые/потерянные метки (сцена сохранена старой версией скрипта)
    /// и нужный объект найти не удалось — перегенерирует платформы по актуальному списку,
    /// чтобы сцена точно совпадала со списком.
    /// </summary>
    public void RemovePlatformAt(int index)
    {
        if (index < 0 || index >= platforms.Count) return;
        EnsurePlatformIds();
        string idToRemove = platforms[index].id;

        GameObject target = FindPlatformObject(idToRemove);
        bool hadAnySpawned = false;
        {
            Transform center = FindColumnCenter();
            if (center != null)
            {
                List<GameObject> all = new List<GameObject>();
                CollectPlatformObjects(center, all);
                hadAnySpawned = all.Count > 0;
            }
        }

        platforms.RemoveAt(index);

        if (target != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.DestroyObjectImmediate(target);
            else
#endif
                Destroy(target);
            spawnedPlatforms.RemoveAll(obj => obj == null || obj == target);
        }
        else if (hadAnySpawned)
        {
            // Метки не совпали со списком — пересобираем сцену по списку.
            GeneratePlatformsInternal(!Application.isPlaying);
        }
    }

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
    // Ширины колонок компактной строки платформы
    private const float W_FOLD = 20f;
    private const float W_NUM = 28f;
    private const float W_BTN = 22f;
    private const int SCROLL_AFTER_ROWS = 10;      // после скольких строк включается прокрутка
    private const float SCROLL_HEIGHT = 340f;

    private string filter = "";
    private Vector2 scroll;
    private int addCount = 1;
    private readonly HashSet<string> expanded = new HashSet<string>();

    private void OnEnable()
    {
        if (serializedObject.isEditingMultipleObjects) return;
        var s = (SpiralStepSpawner)target;
        if (s.EnsurePlatformIds())
        {
            EditorUtility.SetDirty(s);
        }
    }

    private void Prop(string name, string label = null)
    {
        var p = serializedObject.FindProperty(name);
        if (p == null) return;
        if (label == null) EditorGUILayout.PropertyField(p, true);
        else EditorGUILayout.PropertyField(p, new GUIContent(label, p.tooltip), true);
    }

    // Выполнить действие над всеми выбранными спавнерами с поддержкой Undo
    private void ForEachTarget(string undoName, System.Action<SpiralStepSpawner> action)
    {
        serializedObject.ApplyModifiedProperties();
        foreach (var obj in targets)
        {
            var s = (SpiralStepSpawner)obj;
            Undo.RecordObject(s, undoName);
            action(s);
            EditorUtility.SetDirty(s);
            if (PrefabUtility.IsPartOfPrefabInstance(s))
                PrefabUtility.RecordPrefabInstancePropertyModifications(s);
        }
        serializedObject.Update();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        GUILayout.Label("SPIRAL STEP SPAWNER", EditorStyles.boldLabel);
        GUILayout.Space(6);

        // ---------- ОСНОВНЫЕ ----------
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("ОСНОВНЫЕ НАСТРОЙКИ", EditorStyles.boldLabel);
        Prop("platformPrefab", "Platform Prefab");
        Prop("columnCenter", "Column Center");
        Prop("generateOnStart", "Generate On Start");
        Prop("destroyPreviousOnGenerate", "Destroy Previous On Generate");
        Prop("rotationMode", "Rotation Mode");
        Prop("reverseAll", "Реверс ВСЕХ платформ");
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("КОЛЛИЗИИ", EditorStyles.boldLabel);
        Prop("autoColliderMode", "Auto Collider Mode");
        EditorGUILayout.EndVertical();

        // ---------- ГЕНЕРАЦИЯ ----------
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("УПРАВЛЕНИЕ ГЕНЕРАЦИЕЙ", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("СГЕНЕРИРОВАТЬ"))
        {
            ForEachTarget("Generate Platforms", s =>
            {
                if (Application.isPlaying) s.GeneratePlatformsRuntime();
                else s.GeneratePlatforms();
            });
            GUIUtility.ExitGUI();
        }
        if (GUILayout.Button("ОЧИСТИТЬ"))
        {
            ForEachTarget("Clear Platforms", s => s.ClearPlatforms());
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        // ---------- СПИСОК ----------
        DrawPlatformList();

        // ---------- ДОПОЛНИТЕЛЬНО ----------
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("ДОПОЛНИТЕЛЬНЫЕ НАСТРОЙКИ", EditorStyles.boldLabel);
        Prop("defaultRadius", "Default Radius");
        Prop("rotationOffset", "Rotation Offset (0 = нос наружу)");
        Prop("startAngle", "Start Angle (стартовая точка)");
        Prop("autoFindColumn", "Auto Find Column");
        Prop("rotationPlane", "Rotation Plane");
        Prop("parentToColumn", "Parent To Column");
        Prop("defaultMaterial", "Default Material");
        Prop("defaultColor", "Default Color");
        Prop("showGizmos", "Show Gizmos");
        Prop("gizmoColor", "Gizmo Color");
        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
    }

    private bool Matches(SerializedProperty p, int index)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;
        string f = filter.Trim().TrimStart('#').Replace(',', '.');
        string num = (index + 1).ToString(CultureInfo.InvariantCulture);
        string h = p.FindPropertyRelative("height").floatValue.ToString("0.##", CultureInfo.InvariantCulture);
        return num == f || h.StartsWith(f, System.StringComparison.Ordinal);
    }

    private void DrawPlatformList()
    {
        var platforms = serializedObject.FindProperty("platforms");
        var spawner = (SpiralStepSpawner)target;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label($"СПИСОК ПЛАТФОРМ ({platforms.arraySize})", EditorStyles.boldLabel);

        // --- Шаги для новых платформ (каждый параметр на своей строке) ---
        Prop("heightStep", "Шаг высоты новой платформы");
        Prop("angleStep", "Шаг угла новой платформы");

        EditorGUILayout.HelpBox(
            "Новая платформа копирует ВСЕ параметры предыдущей (радиус, скорость, реверс, материал) " +
            "и сдвигается на шаг высоты и шаг угла. Кнопка ⧉ в строке дублирует конкретную платформу. " +
            "Итоговый угол = Start Angle + «Угол» платформы.", MessageType.Info);

        // --- Добавление ---
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ ДОБАВИТЬ", GUILayout.Height(22)))
        {
            int n = addCount;
            ForEachTarget("Add Spiral Platform", s => s.AddPlatforms(n));
        }
        GUILayout.Label("штук:", GUILayout.Width(36));
        addCount = Mathf.Clamp(EditorGUILayout.IntField(addCount, GUILayout.Width(40), GUILayout.Height(22)), 1, 200);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("РАВНОМЕРНО ПО КРУГУ", GUILayout.Height(20)))
            ForEachTarget("Distribute Platforms", s => s.DistributeEvenly());
        if (GUILayout.Button("СОРТИРОВАТЬ ПО ВЫСОТЕ", GUILayout.Height(20)))
            ForEachTarget("Sort Platforms", s => s.SortByHeight());
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("ОЧИСТИТЬ СПИСОК", GUILayout.Height(20)))
            ForEachTarget("Clear Platform List", s => s.ClearPlatformList());

        GUILayout.Space(4);

        // --- Поиск ---
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Поиск", GUILayout.Width(40));
        filter = EditorGUILayout.TextField(new GUIContent("", "Номер платформы или высота: 3, #3, 12.5"), filter);
        if (GUILayout.Button("✕", GUILayout.Width(W_BTN))) { filter = ""; GUI.FocusControl(null); }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Свернуть все", EditorStyles.miniButton)) expanded.Clear();
        if (GUILayout.Button("Развернуть все", EditorStyles.miniButton))
        {
            for (int i = 0; i < platforms.arraySize; i++)
                expanded.Add(platforms.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue);
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(2);

        int removeIdx = -1, dupIdx = -1, selectIdx = -1;

        bool useScroll = platforms.arraySize > SCROLL_AFTER_ROWS;
        if (useScroll) scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(SCROLL_HEIGHT));

        int shown = 0;
        for (int i = 0; i < platforms.arraySize; i++)
        {
            var p = platforms.GetArrayElementAtIndex(i);
            if (!Matches(p, i)) continue;
            shown++;
            DrawRow(p, i, spawner, ref removeIdx, ref dupIdx, ref selectIdx);
        }

        if (useScroll) EditorGUILayout.EndScrollView();

        if (shown == 0 && platforms.arraySize > 0)
            EditorGUILayout.HelpBox("Ничего не найдено.", MessageType.None);

        EditorGUILayout.EndVertical();

        // Действия над списком выполняем ПОСЛЕ отрисовки, чтобы не ломать layout
        if (selectIdx >= 0)
        {
            string id = platforms.GetArrayElementAtIndex(selectIdx).FindPropertyRelative("id").stringValue;
            GameObject go = spawner.FindPlatformObject(id);
            if (go != null)
            {
                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);
                SceneView.lastActiveSceneView?.FrameSelected();
            }
            else Debug.LogWarning("[Спавнер] Эта платформа ещё не сгенерирована — нажмите «СГЕНЕРИРОВАТЬ».");
        }
        if (dupIdx >= 0)
        {
            int idx = dupIdx;
            ForEachTarget("Duplicate Spiral Platform", s => s.DuplicatePlatformAt(idx));
        }
        if (removeIdx >= 0)
        {
            int idx = removeIdx;
            ForEachTarget("Remove Spiral Platform", s => s.RemovePlatformAt(idx));
            GUIUtility.ExitGUI();
        }
    }

    private void DrawRow(SerializedProperty p, int i, SpiralStepSpawner spawner,
                         ref int removeIdx, ref int dupIdx, ref int selectIdx)
    {
        string id = p.FindPropertyRelative("id").stringValue;
        bool open = expanded.Contains(id);

        var height = p.FindPropertyRelative("height");
        var angle = p.FindPropertyRelative("spawnPosition");
        var radius = p.FindPropertyRelative("customRadius");
        var enableRot = p.FindPropertyRelative("enableRotation");
        var speed = p.FindPropertyRelative("rotationSpeed");
        var reverse = p.FindPropertyRelative("reverse");
        var material = p.FindPropertyRelative("customMaterial");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // ---------- Свёрнутая строка: только главное — высота и угол ----------
        EditorGUILayout.BeginHorizontal();

        bool newOpen = GUILayout.Toggle(open, open ? "▼" : "▶", EditorStyles.miniButton, GUILayout.Width(W_FOLD));
        if (newOpen != open)
        {
            if (newOpen) expanded.Add(id); else expanded.Remove(id);
            open = newOpen;
        }

        // Клик по номеру — выделить платформу в сцене
        if (GUILayout.Button(new GUIContent("#" + (i + 1), "Выделить платформу в сцене"), EditorStyles.boldLabel, GUILayout.Width(W_NUM + 6)))
            selectIdx = i;

        float oldLabel = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 46f;
        EditorGUILayout.PropertyField(height, new GUIContent("Высота"), GUILayout.MinWidth(90));
        EditorGUIUtility.labelWidth = 34f;
        EditorGUILayout.PropertyField(angle, new GUIContent("Угол"), GUILayout.MinWidth(70));
        EditorGUIUtility.labelWidth = oldLabel;

        if (GUILayout.Button(new GUIContent("⧉", "Дублировать (+ шаг высоты и угла)"), GUILayout.Width(W_BTN))) dupIdx = i;
        var old = GUI.backgroundColor;
        GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
        if (GUILayout.Button(new GUIContent("✕", "Удалить ЭТУ платформу"), GUILayout.Width(W_BTN))) removeIdx = i;
        GUI.backgroundColor = old;

        EditorGUILayout.EndHorizontal();

        // Краткая сводка в свёрнутом виде, чтобы не раскрывать ради проверки
        if (!open)
        {
            bool rotates = spawner.rotationMode == SpiralStepSpawner.RotationMode.AllRotate ||
                           (spawner.rotationMode == SpiralStepSpawner.RotationMode.PerPlatform && enableRot.boolValue);
            string r = radius.floatValue > 0f ? radius.floatValue.ToString("0.##") : "общий";
            string rot = rotates
                ? speed.floatValue.ToString("0.##") + "°/с" + (reverse.boolValue ? ", реверс" : "")
                : "стоит";
            var mini = new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip };
            GUILayout.Label($"    радиус: {r}   •   вращение: {rot}", mini);
        }

        // ---------- Раскрытый блок: все остальные параметры с полными названиями ----------
        if (open)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(radius, new GUIContent("Радиус (0 = общий)"));

            bool perPlatform = spawner.rotationMode == SpiralStepSpawner.RotationMode.PerPlatform;
            using (new EditorGUI.DisabledScope(!perPlatform))
            {
                EditorGUILayout.PropertyField(enableRot, new GUIContent("Вращение",
                    perPlatform ? "Крутится ли эта платформа" : "Игнорируется: Rotation Mode задан для всех платформ"));
            }

            EditorGUILayout.PropertyField(speed, new GUIContent("Скорость вращения (°/с)"));
            EditorGUILayout.PropertyField(reverse, new GUIContent("Реверс", "Вращение в обратную сторону"));
            EditorGUILayout.PropertyField(material, new GUIContent("Материал"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }
}
#endif