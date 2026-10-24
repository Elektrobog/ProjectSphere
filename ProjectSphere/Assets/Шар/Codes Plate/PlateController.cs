using UnityEngine;
using System.Collections.Generic;

public class PlateController : MonoBehaviour
{
    [Header("━━━ ПОРШЕНЬ (задаётся BallJump) ━━━")]
    [SerializeField, HideInInspector] private float extensionMeters = 0.35f;
    [SerializeField, HideInInspector] private float extendSpeed = 4f;
    [SerializeField, HideInInspector] private float retractSpeed = 6f;

    [Header("━━━ ПОВЕДЕНИЕ ━━━")]
    [Tooltip("Ручной выбор колонны для теста. -1 = авто (нижняя).")]
    [SerializeField] private int manualColumnIndex = -1;

    [Header("━━━ ОТЛАДКА ━━━")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private bool drawGizmos = true;

    // ============================================================
    //  ПУБЛИЧНЫЕ
    // ============================================================
    public float ExtensionMeters => extensionMeters;
    public float CurrentExtension => currentExtension;
    public float ExtendSpeed => extendSpeed;
    public float RetractSpeed => retractSpeed;
    public float ExtendDuration => extendSpeed > 0f ? extensionMeters / extendSpeed : 0f;
    public float RetractDuration => retractSpeed > 0f ? extensionMeters / retractSpeed : 0f;
    public bool HasActiveColumn => activeIndex >= 0;
    public Vector3 ActiveOutwardDir => activeIndex >= 0 ? columns[activeIndex].outwardDir : Vector3.up;
    public int ColumnCount => columns.Count;

    // ============================================================
    //  ДАННЫЕ КОЛОННЫ
    // ============================================================
    private struct Column
    {
        public Transform transform;
        public Vector3 baseLocalPos;
        public Vector3 outwardDir;
    }

    private List<Column> columns = new List<Column>();
    private int activeIndex = -1;
    private float currentExtension;
    private float targetExtension;
    private bool initialized;
    private bool useExternalControl = false;

    // ============================================================
    //  UNITY
    // ============================================================
    private void Awake()
    {
        BuildColumns();
    }

    private void Start()
    {
        if (!initialized) BuildColumns();
        currentExtension = 0f;
        targetExtension = 0f;
    }

    private void OnValidate()
    {
        if (Application.isPlaying && !initialized) BuildColumns();
    }

    // ============================================================
    //  ИНИЦИАЛИЗАЦИЯ
    // ============================================================
    private void BuildColumns()
    {
        columns.Clear();
        initialized = true;

        var allTransforms = GetComponentsInChildren<Transform>(true);
        if (debugLogs)
            Debug.Log($"[PlateController] Всего трансформов: {allTransforms.Length}");

        Vector3 sphereCenter = transform.position;

        foreach (var t in allTransforms)
        {
            if (!t.name.StartsWith("Column_")) continue;

            Column c;
            c.transform = t;
            c.baseLocalPos = transform.InverseTransformPoint(t.position);

            Vector3 outwardWorld = t.position - sphereCenter;
            if (outwardWorld.magnitude < 0.001f) outwardWorld = t.up;
            c.outwardDir = transform.InverseTransformDirection(outwardWorld.normalized);

            columns.Add(c);
        }

        if (debugLogs)
            Debug.Log($"[PlateController] Колонн найдено: {columns.Count}");
    }

    // ============================================================
    //  ПУБЛИЧНОЕ API
    // ============================================================
    public void Configure(float meters, float extendSpd, float retractSpd)
    {
        extensionMeters = Mathf.Max(0.001f, meters);
        extendSpeed = Mathf.Max(0.001f, extendSpd);
        retractSpeed = Mathf.Max(0.001f, retractSpd);
    }

    /// <summary>
    /// Установить длительность втягивания в секундах (вместо скорости).
    /// Используется BallJump для синхронизации втягивания со временем падения.
    /// </summary>
    public void SetRetractDuration(float seconds)
    {
        if (seconds <= 0.001f)
        {
            retractSpeed = 1000f;   // практически мгновенно
        }
        else
        {
            retractSpeed = extensionMeters / seconds;
        }

        if (debugLogs)
            Debug.Log($"[PlateController] retractSpeed установлен = {retractSpeed:F2} м/с " +
                      $"(втягивание за {seconds:F2} с)");
    }

    public void StartExtend()
    {
        if (debugLogs) Debug.Log("[PlateController] StartExtend");

        if (!initialized) BuildColumns();

        useExternalControl = false;

        activeIndex = manualColumnIndex >= 0 && manualColumnIndex < columns.Count
            ? manualColumnIndex
            : FindBottomIndex();

        targetExtension = extensionMeters;
    }

    public void StartRetract()
    {
        if (debugLogs) Debug.Log("[PlateController] StartRetract");
        useExternalControl = false;
        targetExtension = 0f;
    }

    /// <summary>
    /// Установить выдвижение напрямую (без таймера). Используется BallJump
    /// для синхронизации с высотой шара.
    /// </summary>
    public void SetExtensionDirect(float meters)
    {
        if (!initialized) BuildColumns();

        if (activeIndex < 0)
        {
            activeIndex = manualColumnIndex >= 0 && manualColumnIndex < columns.Count
                ? manualColumnIndex
                : FindBottomIndex();
        }

        useExternalControl = true;

        currentExtension = Mathf.Clamp(meters, 0f, extensionMeters);
        targetExtension = currentExtension;

        if (activeIndex >= 0)
            ApplyExtension(activeIndex, currentExtension);
    }

    public int FindBottomIndex()
    {
        int best = -1;
        float bestDot = float.NegativeInfinity;

        for (int i = 0; i < columns.Count; i++)
        {
            Vector3 worldDir = transform.TransformDirection(columns[i].outwardDir);
            float dot = Vector3.Dot(worldDir, Vector3.down);
            if (dot > bestDot) { bestDot = dot; best = i; }
        }
        return best;
    }

    // ============================================================
    //  ОБНОВЛЕНИЕ
    // ============================================================
    private void Update()
    {
        if (useExternalControl) return;
        if (!initialized) return;
        if (activeIndex < 0) return;
        if (Mathf.Approximately(currentExtension, targetExtension)) return;

        float speed = targetExtension > currentExtension ? extendSpeed : retractSpeed;
        currentExtension = Mathf.MoveTowards(currentExtension, targetExtension, speed * Time.deltaTime);

        ApplyExtension(activeIndex, currentExtension);

        if (currentExtension <= 0f && targetExtension <= 0f)
            activeIndex = -1;
    }

    private void ApplyExtension(int index, float ext)
    {
        var c = columns[index];
        if (c.transform == null) return;

        Vector3 localPos = c.baseLocalPos + c.outwardDir * ext;
        c.transform.position = transform.TransformPoint(localPos);
    }

    // ============================================================
    //  ГИЗМО
    // ============================================================
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        if (columns.Count == 0 && !Application.isPlaying) BuildColumns();

        int bottom = Application.isPlaying ? activeIndex : FindBottomIndex();

        for (int i = 0; i < columns.Count; i++)
        {
            var c = columns[i];
            if (c.transform == null) continue;

            bool isBottom = (i == bottom);
            Gizmos.color = isBottom ? Color.green : new Color(1f, 1f, 1f, 0.15f);

            Vector3 dir = transform.TransformDirection(c.outwardDir);
            Gizmos.DrawRay(c.transform.position, dir * 0.5f);

            if (isBottom)
                Gizmos.DrawWireSphere(c.transform.position + dir * 0.5f, 0.05f);
        }
    }
}