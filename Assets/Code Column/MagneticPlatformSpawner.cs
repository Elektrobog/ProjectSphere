using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Расстановка магнитных платформ по сетке колонны: РЯД (плита по высоте) x КОЛОНКА (плита по кругу).
/// Сетка определяется автоматически по объектам Glass_Vertical_NN / Glass_Horizontal_NN внутри колонны.
/// Ставить платформы можно кликом по колонне в Scene View (режим «Рисование»).
/// </summary>
[AddComponentMenu("Custom/Магнитные платформы: Спавнер")]
public class MagneticPlatformSpawner : MonoBehaviour
{
    [Serializable]
    public class Slot
    {
        [Tooltip("Номер ряда по высоте снизу вверх (0 = самый нижний ряд у основания колонны).")]
        [Min(0)] public int row = 0;

        [Tooltip("Номер колонки по кругу (0 = сектор между вертикальной трубой №1 и №2). Можно указывать числа больше общего числа колонок — позиция пересчитается по кругу.")]
        public int column = 0;

        [Tooltip("Ширина платформы в колонках плит (сколько секторов между вертикальными трубами она занимает).")]
        [Min(1)] public int columnSpan = 2;

        [Tooltip("Высота платформы в рядах плит (1 = стандартная толщина одной плиты колонны).")]
        [Min(1)] public int rowSpan = 1;

        [Tooltip("Дополнительный поворот платформы по кругу в градусах для точной ручной подгонки.")]
        public float angleOffset = 0f;

        [Tooltip("Индивидуальная длина этой платформы от колонны наружу (в метрах). Если стоит 0 — берётся общая длина из настроек спавнера.")]
        public float depth = 0f;

        [Tooltip("Включить вращение этой платформы вокруг центральной оси колонны во время игры.")]
        public bool rotate = false;

        [Tooltip("Скорость вращения платформы вокруг колонны в градусах за секунду.")]
        public float speed = 15f;

        [Tooltip("Вращать платформу в обратную сторону (против часовой стрелки при взгляде сверху).")]
        public bool reverse = false;

        [Tooltip("Угол поворота до возврата обратно (в градусах). Если 0 — платформа крутится бесконечно по кругу.")]
        [Range(0f, 360f)] public float rotationAngle = 0f;

        [Tooltip("Пауза в секундах в крайних точках перед движением в обратную сторону (работает при угле > 0).")]
        [Min(0f)] public float rotationPause = 0f;

        // --- Индивидуальные переопределения внешнего вида (если useCustomStyle = false, берутся стандартные из спавнера) ---

        [Tooltip("Если выключено — платформа использует стандартные настройки внешнего вида из спавнера. Если включено — применяются индивидуальные параметры ниже.")]
        public bool useCustomStyle = false;

        [Tooltip("Прямой мостик постоянной ширины (если выключить — расширяется веером).")]
        public bool constantWidth = true;

        [Tooltip("Ширина зазора с магнитным полем между колонной и этой платформой (м).")]
        public float gap = 0.9f;

        [Tooltip("Размер среза или радиус закругления на внешних углах платформы (м).")]
        public float cornerBevel = 0.35f;

        [Tooltip("Плавное закругление внешних углов по дуге вместо прямого среза под 45°.")]
        public bool roundCorners = false;

        [Tooltip("Ширина фаски по верхним и нижним краям платформы (м).")]
        public float chamfer = 0.06f;

        [Tooltip("Количество поперечных канавок со светящимися полосами вдоль этой платформы (от 0 до 6).")]
        [Range(0, 6)] public int grooveCount = 3;

        [Tooltip("Ширина поперечной канавки и светящейся полосы (м).")]
        public float grooveWidth = 0.10f;

        [Tooltip("Глубина поперечной канавки (м).")]
        public float grooveDepth = 0.045f;

        [Tooltip("Включить светящиеся вставки внутри канавок этой платформы.")]
        public bool glowGrooves = true;

        [Tooltip("Включить боковые световые метки на стенках этой платформы.")]
        public bool glowSideAccents = true;

        [Tooltip("Индивидуальный цвет подсветки этой платформы.")]
        public Color glowColor = new Color(0.15f, 0.88f, 1f, 1f);

        [Tooltip("Индивидуальная яркость свечения (HDR) этой платформы.")]
        [Range(0f, 8f)] public float glowIntensity = 2.2f;
    }

    // ---------------------------------------------------------------- колонна

    [Tooltip("Корневой объект колонны в сцене, внутри которого находятся трубы Glass_Vertical_* и Glass_Horizontal_*. Если оставить пустым — скрипт найдёт колонну сам.")]
    public Transform column;

    [Tooltip("Автоматически считывать радиус, количество рядов и колонок из труб колонны при каждой генерации. Рекомендуется держать включённым.")]
    public bool autoDetect = true;

    [Tooltip("Количество вертикальных секторов (колонок) по окружности колонны. Заполняется автоматически при включённом автоопределении.")]
    public int columns = 18;

    [Tooltip("Количество горизонтальных рядов плит по высоте колонны. Заполняется автоматически при включённом автоопределении.")]
    public int rows = 40;

    [Tooltip("Высота одного ряда плит в метрах. Заполняется автоматически при включённом автоопределении.")]
    public float rowHeight = 1.25f;

    [Tooltip("Ширина зазора (шва) между плитами по высоте в метрах. Напрямую влияет на толщину платформы и высоту выхода магнитных дуг из горизонтальных колец труб.")]
    public float groove = 0.24f;

    [Tooltip("Радиус внешней поверхности плит колонны в метрах. Вычисляется автоматически по трубам.")]
    public float columnRadius = 5f;

    [Tooltip("Локальная высота самого нижнего горизонтального кольца относительно центра колонны (м). Вычисляется автоматически.")]
    public float ringY0Local = -25f;

    [Tooltip("Угол (азимут) первой вертикальной трубы в градусах (0° = направление +X). Вычисляется автоматически.")]
    public float tubeAzimuth0Deg = 0f;

    [Tooltip("Направление нумерации вертикальных труб по кругу (+1 или −1). Вычисляется автоматически.")]
    public int tubeDirection = 1;

    [Tooltip("Расстояние от центральной оси вертикальной трубы до поверхности плит колонны (м). Позволяет точно подогнать старт магнитного поля под толщину трубок.")]
    public float tubeToSurface = 0.19f;

    // ---------------------------------------------------------------- стандартные настройки платформ

    [Tooltip("Если включено — платформа строится прямым мостиком постоянной ширины от колонны наружу. Если выключено — расширяется веером как сектор круга.")]
    public bool constantWidth = true;

    [Tooltip("Ширина воздушного зазора в метрах между стенкой колонны и началом самой платформы. Внутри этого зазора отображается магнитное поле.")]
    public float gap = 0.9f;

    [Tooltip("Базовая длина платформ от внутреннего края наружу (в метрах). Применяется ко всем слотам, у которых собственная длина равна 0.")]
    public float depth = 4f;

    [Tooltip("Размер среза или радиус закругления на дальних углах прямого мостика (в метрах).")]
    public float cornerBevel = 0.35f;

    [Tooltip("Если включено — дальние углы платформы плавно закругляются по дуге. Если выключено — делается прямой скос под 45°.")]
    public bool roundCorners = false;

    [Tooltip("Ширина декоративного скоса (фаски) по верхним и нижним рёбрам платформы в метрах.")]
    public float chamfer = 0.06f;

    [Tooltip("Количество поперечных канавок со светящимися полосами вдоль платформы (от 0 до 6).")]
    [Range(0, 6)] public int grooveCount = 3;

    [Tooltip("Отступ от переднего и заднего краёв платформы до крайних поперечных канавок (в метрах).")]
    public float grooveInset = 0.45f;

    [Tooltip("Ширина поперечной канавки на поверхности платформы (в метрах).")]
    public float grooveWidth = 0.10f;

    [Tooltip("Глубина поперечной канавки на поверхности платформы (в метрах).")]
    public float grooveDepth = 0.045f;

    [Tooltip("Добавлять светящиеся вставки внутрь поперечных канавок платформы.")]
    public bool glowGrooves = true;

    [Tooltip("Добавлять тонкие светящиеся вертикальные метки на боковых стенках платформы напротив канавок.")]
    public bool glowSideAccents = true;

    [Tooltip("Цвет светящихся вставок в канавках (используется, если не назначен отдельный материал подсветки).")]
    public Color glowColor = new Color(0.15f, 0.88f, 1f, 1f);

    [Tooltip("Интенсивность свечения (HDR Emission) вставок в канавках.")]
    [Range(0f, 8f)] public float glowIntensity = 2.2f;

    [Tooltip("Боковой отступ в градусах от вертикальных труб колонны до краёв платформы, чтобы платформа не врезалась в трубы.")]
    public float endInsetDeg = 2f;

    [Tooltip("Высота прогиба магнитных дуг, идущих от горизонтальных колец труб на платформу.")]
    public float fieldReach = 0.5f;

    [Tooltip("Количество внутренних радиальных слоёв плазмы между колонной и платформой (от 1 до 6).")]
    [Range(1, 6)] public int fieldLayers = 3;

    // ---------------------------------------------------------------- физика и хитбоксы

    [Tooltip("Создавать гладкий цилиндрический MeshCollider на самой колонне, чтобы мяч не застревал в швах между плитами и трубами.")]
    public bool columnCollider = true;

    [Tooltip("Количество сегментов по окружности для коллайдера колонны (36–72 обеспечивает плавное скольжение без граней).")]
    [Range(18, 96)] public int columnColliderSegments = 48;

    [Tooltip("Дополнительный запас высоты коллайдера колонны сверху и снизу за крайними кольцами (в метрах).")]
    [Min(0f)] public float columnHeightPadding = 0.5f;

    [Tooltip("Режим построения физического коллайдера для всех платформ. «Точный по контуру (гладкая дека)» идеально подходит для катящегося мяча.")]
    public MagneticPlatform.ColliderMode colliderMode = MagneticPlatform.ColliderMode.ExactSmoothDeck;

    [Tooltip("Учитывать фаску по краям в физическом хитбоксе (помогает мячу плавно съезжать с края без резкого зацепа).")]
    public bool colliderChamfer = true;

#if UNITY_6000_0_OR_NEWER
    [Tooltip("Физический материал (трение и упругость), назначаемый на хитбоксы колонны и всех платформ.")]
    public PhysicsMaterial physicMaterial;
#else
    [Tooltip("Физический материал (трение и упругость), назначаемый на хитбоксы колонны и всех платформ.")]
    public PhysicMaterial physicMaterial;
#endif

    [Tooltip("Отображать зелёную сетку реальных физических хитбоксов (платформ и колонны) в окне Scene View.")]
    public bool showHitboxes = false;

    [Tooltip("Материал твёрдого корпуса платформы. Если не назначен — автоматически создаётся тёмно-графитовый материал в цвет колонны.")]
    public Material bodyMaterial;

    [Tooltip("Материал светящихся вставок в канавках. Если не назначен — создаётся автоматически из выбранного цвета и яркости.")]
    public Material glowMaterial;

    [Tooltip("Материал магнитного поля с шейдером SciFi/MagneticFieldPlatform. Если не назначен — шейдер ищется в проекте автоматически.")]
    public Material fieldMaterial;

    [Tooltip("Список всех платформ, привязанных к ячейкам сетки колонны.")]
    public List<Slot> slots = new List<Slot>();

    [Tooltip("Автоматически сгенерировать платформы при запуске игры, если в сцене они ещё не были созданы вручную.")]
    public bool generateOnStart = false;

    [Tooltip("Режим интерактивного рисования в окне Scene View: ЛКМ по колонне — добавить платформу, Shift + ЛКМ — удалить платформу под курсором.")]
    public bool paintMode = false;

    [Tooltip("Ширина в колонках для новых платформ, которые вы ставите кликом мыши в режиме рисования.")]
    [Min(1)] public int paintColumnSpan = 2;

    [Tooltip("Высота в рядах для новых платформ, которые вы ставите кликом мыши в режиме рисования.")]
    [Min(1)] public int paintRowSpan = 1;

    [Tooltip("Длина в метрах для новых платформ при рисовании. Если оставить 0 — будет использоваться базовая длина из общих настроек спавнера.")]
    public float paintDepth = 0f;

    [Tooltip("Сразу включать вращение вокруг колонны у новых платформ, создаваемых в режиме рисования или через шаблоны.")]
    public bool paintRotate = false;

    [Tooltip("Скорость вращения (°/сек) для новых платформ, создаваемых в режиме рисования или через шаблоны.")]
    public float paintSpeed = 15f;

    [Tooltip("Угол поворота до возврата обратно (°) для новых платформ (0 = полный круг без возврата).")]
    [Range(0f, 360f)] public float paintRotationAngle = 0f;

    [Tooltip("Пауза в крайних точках (сек) для новых платформ при ограниченном повороте.")]
    [Min(0f)] public float paintRotationPause = 0f;

    public const string ContainerName = "Platforms (generated)";
    public const string ColumnColliderName = "~gen_ColumnCollider";

    // Единый реестр вращающихся платформ для централизованного обновления в одном FixedUpdate
    readonly List<MagneticPlatform> rotatingPlatforms = new List<MagneticPlatform>();

    // ---------------------------------------------------------------- геометрия сетки

    public float StepDeg { get { return 360f / Mathf.Max(1, columns); } }

    public Vector3 Center
    {
        get { Transform c = column != null ? column : transform; return c.position; }
    }

    public float SlotCenterAz(Slot s)
    {
        return tubeAzimuth0Deg + tubeDirection * (s.column + s.columnSpan * 0.5f) * StepDeg + s.angleOffset;
    }

    public float SlotArc(Slot s) { return Mathf.Max(1f, s.columnSpan * StepDeg - 2f * endInsetDeg); }

    public float SlotCenterY(Slot s) { return Center.y + ringY0Local + (s.row + s.rowSpan * 0.5f) * rowHeight; }

    public float SlotThickness(Slot s) { return Mathf.Max(0.1f, s.rowSpan * rowHeight - groove); }

    public Vector3 Polar(float azDeg, float r, float y)
    {
        Vector3 c = Center;
        float a = azDeg * Mathf.Deg2Rad;
        return new Vector3(c.x + Mathf.Cos(a) * r, y, c.z + Mathf.Sin(a) * r);
    }

    /// <summary>Замкнутый контур основания платформы на высоте y (для превью и гизмо).</summary>
    public Vector3[] FootprintLoop(Slot s, float y)
    {
        bool isStraight = s.useCustomStyle ? s.constantWidth : constantWidth;
        float slotGap = s.useCustomStyle ? s.gap : gap;
        float slotBevel = s.useCustomStyle ? s.cornerBevel : cornerBevel;
        bool slotRound = s.useCustomStyle ? s.roundCorners : roundCorners;

        float rIn = columnRadius + slotGap;
        float rOut = rIn + (s.depth > 0f ? s.depth : depth);
        float az = SlotCenterAz(s);
        float half = SlotArc(s) * 0.5f;
        int n = Mathf.Max(2, Mathf.CeilToInt(half * 2f / 4f));

        var pts = new List<Vector3>();
        if (isStraight)
        {
            float clampedHalf = Mathf.Min(half, 80f);
            float halfRad = clampedHalf * Mathf.Deg2Rad;
            float zHalf = rIn * Mathf.Sin(halfRad);
            float xCorner = rIn * Mathf.Cos(halfRad);
            float xOut = Mathf.Max(xCorner + 0.15f, rOut);
            float maxCb = Mathf.Min(zHalf * 0.75f, (xOut - xCorner) * 0.45f);
            float cb = Mathf.Clamp(slotBevel, 0f, Mathf.Max(0f, maxCb));

            float azRad = az * Mathf.Deg2Rad;
            Vector3 centerY = new Vector3(Center.x, y, Center.z);
            Vector3 dirX = new Vector3(Mathf.Cos(azRad), 0f, Mathf.Sin(azRad));
            Vector3 dirZ = new Vector3(-Mathf.Sin(azRad), 0f, Mathf.Cos(azRad));

            for (int i = 0; i <= n; i++)
            {
                float a = (-clampedHalf + 2f * clampedHalf * i / n) * Mathf.Deg2Rad;
                pts.Add(centerY + dirX * (Mathf.Cos(a) * rIn) + dirZ * (Mathf.Sin(a) * rIn));
            }

            if (cb > 1e-4f)
            {
                if (slotRound)
                {
                    int cSegs = 5;
                    for (int i = 0; i <= cSegs; i++)
                    {
                        float a = Mathf.Lerp(Mathf.PI * 0.5f, 0f, i / (float)cSegs);
                        float lx = (xOut - cb) + Mathf.Cos(a) * cb;
                        float lz = (zHalf - cb) + Mathf.Sin(a) * cb;
                        pts.Add(centerY + dirX * lx + dirZ * lz);
                    }
                    for (int i = 0; i <= cSegs; i++)
                    {
                        float a = Mathf.Lerp(0f, -Mathf.PI * 0.5f, i / (float)cSegs);
                        float lx = (xOut - cb) + Mathf.Cos(a) * cb;
                        float lz = (-zHalf + cb) + Mathf.Sin(a) * cb;
                        pts.Add(centerY + dirX * lx + dirZ * lz);
                    }
                }
                else
                {
                    pts.Add(centerY + dirX * (xOut - cb) + dirZ * zHalf);
                    pts.Add(centerY + dirX * xOut + dirZ * (zHalf - cb));
                    pts.Add(centerY + dirX * xOut - dirZ * (zHalf - cb));
                    pts.Add(centerY + dirX * (xOut - cb) - dirZ * zHalf);
                }
            }
            else
            {
                pts.Add(centerY + dirX * xOut + dirZ * zHalf);
                pts.Add(centerY + dirX * xOut - dirZ * zHalf);
            }
        }
        else
        {
            for (int i = 0; i <= n; i++) pts.Add(Polar(az - half + 2f * half * i / n, rIn, y));
            for (int i = n; i >= 0; i--) pts.Add(Polar(az - half + 2f * half * i / n, rOut, y));
        }

        pts.Add(pts[0]);
        return pts.ToArray();
    }

    /// <summary>Какая ячейка сетки под лучом (пересечение луча с цилиндром колонны).</summary>
    public bool RaycastCell(Ray ray, out int row, out int col)
    {
        row = 0; col = 0;
        Vector3 c = Center;
        Vector2 o = new Vector2(ray.origin.x - c.x, ray.origin.z - c.z);
        Vector2 d = new Vector2(ray.direction.x, ray.direction.z);
        float a = Vector2.Dot(d, d);
        if (a < 1e-8f) return false;
        float b = 2f * Vector2.Dot(o, d);
        float cc = Vector2.Dot(o, o) - columnRadius * columnRadius;
        float disc = b * b - 4f * a * cc;
        if (disc < 0f) return false;

        float sq = Mathf.Sqrt(disc);
        float t = (-b - sq) / (2f * a);
        if (t < 0f) t = (-b + sq) / (2f * a);
        if (t < 0f) return false;

        Vector3 p = ray.origin + ray.direction * t;
        row = Mathf.FloorToInt((p.y - (c.y + ringY0Local)) / rowHeight);
        if (row < 0 || row >= rows) return false;

        float az = Mathf.Atan2(p.z - c.z, p.x - c.x) * Mathf.Rad2Deg;
        float colF = (az - tubeAzimuth0Deg) * tubeDirection / StepDeg;
        col = Mathf.FloorToInt(Mathf.Repeat(colF, columns));
        return true;
    }

    // ---------------------------------------------------------------- автоопределение сетки

    [ContextMenu("Определить сетку по колонне")]
    public bool DetectGrid()
    {
        if (column == null)
        {
            GameObject platesObj = GameObject.Find("Armor Plates");
            if (platesObj != null && platesObj.transform.parent != null)
                column = platesObj.transform.parent;
            else
            {
                GameObject tube = GameObject.Find("Glass_Vertical_All");
                if (tube == null) tube = GameObject.Find("Glass_Vertical_01");
                if (tube != null && tube.transform.parent != null) column = tube.transform.parent;
            }
        }

        Transform root = column != null ? column : transform;
        Vector3 c = root.position;

        Transform platesTr = null;
        Transform mergedVert = null;
        Transform mergedRing = null;
        var verts = new List<Transform>();
        var rings = new List<Transform>();

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Armor Plates") platesTr = t;
            else if (t.name == "Glass_Vertical_All") mergedVert = t;
            else if (t.name == "Glass_Horizontal_All") mergedRing = t;
            else if (t.name.StartsWith("Glass_Vertical_", StringComparison.Ordinal)) verts.Add(t);
            else if (t.name.StartsWith("Glass_Horizontal_", StringComparison.Ordinal)) rings.Add(t);
        }

        // Новая объединённая модель
        if (mergedVert != null && mergedRing != null)
        {
            var vMf = mergedVert.GetComponent<MeshFilter>();
            var rMf = mergedRing.GetComponent<MeshFilter>();
            if (vMf != null && vMf.sharedMesh != null && rMf != null && rMf.sharedMesh != null)
            {
                // 1. Вертикальные трубы: группируем вершины по уникальным азимутам (с шагом 5°)
                Vector3[] vVerts = vMf.sharedMesh.vertices;
                var azBuckets = new SortedDictionary<int, List<Vector2>>();
                for (int i = 0; i < vVerts.Length; i++)
                {
                    Vector3 wp = mergedVert.TransformPoint(vVerts[i]);
                    Vector2 xz = new Vector2(wp.x - c.x, wp.z - c.z);
                    if (xz.sqrMagnitude < 0.25f) continue;

                    float deg = Mathf.Repeat(Mathf.Atan2(xz.y, xz.x) * Mathf.Rad2Deg, 360f);
                    int key = Mathf.RoundToInt(deg / 5f) * 5;
                    if (key == 360) key = 0;
                    if (!azBuckets.TryGetValue(key, out var list))
                    {
                        list = new List<Vector2>();
                        azBuckets[key] = list;
                    }
                    list.Add(xz);
                }

                if (azBuckets.Count >= 2)
                {
                    var angles = new List<float>(azBuckets.Count);
                    float avgDist = 0f;
                    foreach (var kv in azBuckets)
                    {
                        Vector2 sum = Vector2.zero;
                        for (int k = 0; k < kv.Value.Count; k++) sum += kv.Value[k];
                        Vector2 avg = sum / kv.Value.Count;
                        avgDist += avg.magnitude;
                        angles.Add(Mathf.Atan2(avg.y, avg.x) * Mathf.Rad2Deg);
                    }

                    columns = angles.Count;
                    tubeAzimuth0Deg = angles[0];
                    tubeDirection = Mathf.DeltaAngle(angles[0], angles[1]) >= 0f ? 1 : -1;
                    columnRadius = (avgDist / columns) + tubeToSurface;
                }

                // 2. Горизонтальные кольца: ищем локальные скопления вершин по высоте Y
                Vector3[] rVerts = rMf.sharedMesh.vertices;
                float[] allYs = new float[rVerts.Length];
                for (int i = 0; i < rVerts.Length; i++)
                    allYs[i] = mergedRing.TransformPoint(rVerts[i]).y;
                Array.Sort(allYs);

                var ringCentersY = new List<float>();
                if (allYs.Length > 0)
                {
                    double clusterSum = allYs[0];
                    int clusterCount = 1;
                    float prevY = allYs[0];

                    for (int i = 1; i < allYs.Length; i++)
                    {
                        if (allYs[i] - prevY > 0.45f)
                        {
                            ringCentersY.Add((float)(clusterSum / clusterCount));
                            clusterSum = allYs[i];
                            clusterCount = 1;
                        }
                        else
                        {
                            clusterSum += allYs[i];
                            clusterCount++;
                        }
                        prevY = allYs[i];
                    }
                    ringCentersY.Add((float)(clusterSum / clusterCount));
                }

                if (ringCentersY.Count >= 2)
                {
                    rows = ringCentersY.Count - 1;
                    ringY0Local = ringCentersY[0] - c.y;
                    rowHeight = (ringCentersY[ringCentersY.Count - 1] - ringCentersY[0]) / rows;
                }

                // 3. Точный внешний радиус и ширина шва по мешу Armor Plates
                if (platesTr != null && rows > 0)
                {
                    var pMf = platesTr.GetComponent<MeshFilter>();
                    if (pMf != null && pMf.sharedMesh != null)
                    {
                        Vector3[] pVerts = pMf.sharedMesh.vertices;
                        float minY = float.MaxValue;
                        float maxR = 0f;

                        for (int i = 0; i < pVerts.Length; i++)
                        {
                            Vector3 wp = platesTr.TransformPoint(pVerts[i]);
                            if (wp.y < minY) minY = wp.y;
                            float r = new Vector2(wp.x - c.x, wp.z - c.z).magnitude;
                            if (r > maxR) maxR = r;
                        }

                        if (maxR > 0.5f)
                            columnRadius = maxR;

                        float bottomRingWorldY = c.y + ringY0Local;
                        float detectedGroove = (minY - bottomRingWorldY) * 2f;
                        if (detectedGroove > 0.02f && detectedGroove < rowHeight * 0.8f)
                            groove = detectedGroove;
                    }
                }

                Debug.Log($"[Спавнер] Сетка обновлена: колонок={columns}, рядов={rows}, шаг={rowHeight:0.###}м, ringY0={ringY0Local:0.###}м, радиус={columnRadius:0.###}м, шов={groove:0.###}м", this);
                return true;
            }
        }

        // Старая модель с отдельными объектами
        if (verts.Count < 2 || rings.Count < 2)
        {
            Debug.LogWarning("[Спавнер] Не найдены трубы Glass_Vertical_* / Glass_Horizontal_* внутри колонны.", this);
            return false;
        }

        verts.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
        rings.Sort((x, y) => string.CompareOrdinal(x.name, y.name));

        Vector3 d0 = verts[0].position - c;
        Vector3 d1 = verts[1].position - c;
        float az0 = Mathf.Atan2(d0.z, d0.x) * Mathf.Rad2Deg;
        float az1 = Mathf.Atan2(d1.z, d1.x) * Mathf.Rad2Deg;

        columns = verts.Count;
        tubeAzimuth0Deg = az0;
        tubeDirection = Mathf.DeltaAngle(az0, az1) >= 0f ? 1 : -1;
        columnRadius = new Vector2(d0.x, d0.z).magnitude + tubeToSurface;

        rows = rings.Count - 1;
        ringY0Local = rings[0].position.y - c.y;
        rowHeight = (rings[rings.Count - 1].position.y - rings[0].position.y) / rows;

        return true;
    }

    // ---------------------------------------------------------------- коллайдер самой колонны

    [ContextMenu("Обновить коллайдер колонны")]
    public void BuildColumnCollider()
    {
        Transform colRoot = column != null ? column : transform;
        RemoveColumnCollider();

        if (!columnCollider) return;

        // Отключаем тяжёлые MeshCollider на визуальных деталях колонны, если они там есть
        Transform platformsCont = transform.Find(ContainerName);
        foreach (MeshCollider extraMc in colRoot.GetComponentsInChildren<MeshCollider>(true))
        {
            if (extraMc.transform.name == ColumnColliderName) continue;
            if (platformsCont != null && extraMc.transform.IsChildOf(platformsCont)) continue;
            extraMc.enabled = false;
        }

        float totalHeight = Mathf.Max(0.2f, rows * rowHeight + columnHeightPadding * 2f);
        float centerY = ringY0Local + (rows * rowHeight) * 0.5f;
        float r = Mathf.Max(0.1f, columnRadius);

        var go = new GameObject(ColumnColliderName);
        go.layer = colRoot.gameObject.layer;
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(colRoot, false);
        go.transform.localPosition = new Vector3(0f, centerY, 0f);
        go.transform.localRotation = Quaternion.identity;

        // CapsuleCollider с запасом высоты на радиус даёт идеально гладкий цилиндр без полигонов
        var cc = go.AddComponent<CapsuleCollider>();
        cc.direction = 1; // Ось Y
        cc.radius = r;
        cc.height = totalHeight + r * 2f;
        if (physicMaterial != null)
            cc.sharedMaterial = physicMaterial;
    }

    public void RemoveColumnCollider()
    {
        Transform colRoot = column != null ? column : transform;
        if (colRoot == null) return;

        for (int i = colRoot.childCount - 1; i >= 0; i--)
        {
            Transform ch = colRoot.GetChild(i);
            if (ch.name != ColumnColliderName) continue;

            var mc = ch.GetComponent<MeshCollider>();
            if (mc != null && mc.sharedMesh != null)
            {
                if (Application.isPlaying) Destroy(mc.sharedMesh);
                else DestroyImmediate(mc.sharedMesh);
            }
            if (Application.isPlaying) Destroy(ch.gameObject);
            else DestroyImmediate(ch.gameObject);
        }
    }

    static Mesh BuildCylinderColliderMesh(float radius, float height, int segments)
    {
        float hh = height * 0.5f;
        var verts = new List<Vector3>((segments + 1) * 2 + 2);
        var tris = new List<int>(segments * 12);

        for (int i = 0; i <= segments; i++)
        {
            float ang = (i / (float)segments) * Mathf.PI * 2f;
            float x = Mathf.Cos(ang) * radius;
            float z = Mathf.Sin(ang) * radius;
            verts.Add(new Vector3(x, -hh, z));
            verts.Add(new Vector3(x, hh, z));
        }

        for (int i = 0; i < segments; i++)
        {
            int i0 = i * 2;
            int i1 = i0 + 1;
            int i2 = i0 + 2;
            int i3 = i0 + 3;

            tris.Add(i0); tris.Add(i1); tris.Add(i3);
            tris.Add(i0); tris.Add(i3); tris.Add(i2);
        }

        int botCenter = verts.Count;
        verts.Add(new Vector3(0f, -hh, 0f));
        int topCenter = verts.Count;
        verts.Add(new Vector3(0f, hh, 0f));

        for (int i = 0; i < segments; i++)
        {
            int b0 = i * 2;
            int b1 = (i + 1) * 2;
            int t0 = b0 + 1;
            int t1 = b1 + 1;

            tris.Add(botCenter); tris.Add(b0); tris.Add(b1);
            tris.Add(topCenter); tris.Add(t1); tris.Add(t0);
        }

        var mesh = new Mesh { name = "Column Cylinder Collider (generated)" };
        mesh.hideFlags = HideFlags.DontSave;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        Transform t = obj.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
    }

    // ---------------------------------------------------------------- жизненный цикл и вращение

    void Start()
    {
        if (!Application.isPlaying) return;

        Transform cont = transform.Find(ContainerName);
        if (generateOnStart && (cont == null || cont.childCount == 0))
        {
            Generate();
        }
        else
        {
            if (columnCollider)
            {
                Transform colRoot = column != null ? column : transform;
                if (colRoot.Find(ColumnColliderName) == null)
                    BuildColumnCollider();
            }
            RefreshRotatingRegistry();
        }
    }

    void FixedUpdate()
    {
        if (!Application.isPlaying || rotatingPlatforms.Count == 0) return;

        float dt = Time.fixedDeltaTime;
        for (int i = 0; i < rotatingPlatforms.Count; i++)
        {
            MagneticPlatform mp = rotatingPlatforms[i];
            if (mp != null && mp.isActiveAndEnabled && mp.rotate)
                mp.StepRotation(dt);
        }
    }

    public void RefreshRotatingRegistry()
    {
        rotatingPlatforms.Clear();
        Transform cont = transform.Find(ContainerName);
        if (cont == null) return;

        foreach (MagneticPlatform mp in cont.GetComponentsInChildren<MagneticPlatform>(true))
        {
            mp.managedBySpawner = true;
            if (mp.rotate)
                rotatingPlatforms.Add(mp);
        }
    }

    Transform GetContainer(bool create)
    {
        Transform cont = transform.Find(ContainerName);
        if (cont == null && create)
        {
            var go = new GameObject(ContainerName);
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            cont = go.transform;
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(go, "Создать контейнер платформ");
#endif
        }
        else if (cont != null)
        {
            cont.gameObject.layer = gameObject.layer;
        }
        return cont;
    }

    [ContextMenu("Сгенерировать платформы")]
    public void Generate()
    {
        if (autoDetect) DetectGrid();
        BuildColumnCollider();
        Clear();

        if (slots.Count == 0)
        {
            Debug.LogWarning("[Спавнер] Список платформ пуст. Включите режим рисования и кликайте по колонне, либо заполните шаблоном.", this);
            return;
        }

        Transform cont = GetContainer(true);
        Vector3 center = Center;
        int made = 0;

        for (int i = 0; i < slots.Count; i++)
        {
            Slot s = slots[i];
            if (s.row < 0 || s.row + s.rowSpan > rows)
            {
                Debug.LogWarning("[Спавнер] Платформа #" + (i + 1) + ": ряд " + s.row + " (+" + s.rowSpan + ") выходит за пределы колонны (рядов: " + rows + "). Пропущена.", this);
                continue;
            }

            var go = new GameObject("Platform r" + s.row + " c" + s.column + " x" + s.columnSpan);
            go.layer = gameObject.layer;
            go.SetActive(false);
            go.transform.SetParent(cont, false);
            go.transform.SetPositionAndRotation(
                new Vector3(center.x, SlotCenterY(s), center.z),
                Quaternion.Euler(0f, -SlotCenterAz(s), 0f));

            bool custom = s.useCustomStyle;
            var mp = go.AddComponent<MagneticPlatform>();
            mp.managedBySpawner = true;
            mp.constantWidth = custom ? s.constantWidth : constantWidth;
            mp.arcDegrees = SlotArc(s);
            mp.columnRadius = columnRadius;
            mp.gap = custom ? s.gap : gap;
            mp.depth = s.depth > 0f ? s.depth : depth;
            mp.thickness = SlotThickness(s);
            mp.groove = groove;
            mp.cornerBevel = custom ? s.cornerBevel : cornerBevel;
            mp.roundCorners = custom ? s.roundCorners : roundCorners;
            mp.chamfer = custom ? s.chamfer : chamfer;
            mp.grooveCount = custom ? s.grooveCount : grooveCount;
            mp.grooveInset = grooveInset;
            mp.grooveWidth = custom ? s.grooveWidth : grooveWidth;
            mp.grooveDepth = custom ? s.grooveDepth : grooveDepth;
            mp.glowGrooves = custom ? s.glowGrooves : glowGrooves;
            mp.glowSideAccents = custom ? s.glowSideAccents : glowSideAccents;
            mp.glowColor = custom ? s.glowColor : glowColor;
            mp.glowIntensity = custom ? s.glowIntensity : glowIntensity;
            mp.fieldReach = fieldReach;
            mp.fieldLayers = fieldLayers;
            mp.tubeAzimuth0Rad = tubeAzimuth0Deg * Mathf.Deg2Rad;
            mp.tubeDirection = tubeDirection;
            mp.tubeCount = columns;
            mp.colliderMode = colliderMode;
            mp.colliderChamfer = colliderChamfer;
            mp.physicMaterial = physicMaterial;
            mp.bodyMaterial = bodyMaterial;
            mp.glowMaterial = glowMaterial;
            mp.fieldMaterial = fieldMaterial;
            mp.rotate = s.rotate;
            mp.speedDegPerSec = s.speed;
            mp.reverse = s.reverse;
            mp.rotationAngle = s.rotationAngle;
            mp.rotationPause = s.rotationPause;

#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(go, "Сгенерировать платформу");
#endif
            go.SetActive(true);
            SetLayerRecursively(go, gameObject.layer);
            made++;
        }

        RefreshRotatingRegistry();
        Debug.Log("[Спавнер] Создано платформ: " + made +
                  " (уникальных мешей в кэше: " + MagneticPlatformMeshBuilder.CachedMeshCount + ")");
    }

    [ContextMenu("Очистить платформы")]
    public void Clear()
    {
        rotatingPlatforms.Clear();
        Transform cont = GetContainer(false);
        if (cont == null) return;
        for (int i = cont.childCount - 1; i >= 0; i--)
        {
            GameObject go = cont.GetChild(i).gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying) { Undo.DestroyObjectImmediate(go); continue; }
#endif
            Destroy(go);
        }
    }

    [ContextMenu("Очистить кэш мешей и материалов")]
    public void ClearResourceCache()
    {
        MagneticPlatformMeshBuilder.ClearCache();
        MagneticPlatform.ClearMaterialCache();
    }

    // ---------------------------------------------------------------- операции над списком

    public void AddSlot(Slot s) { slots.Add(s); }

    public bool RemoveSlotAtCell(int row, int col)
    {
        for (int i = slots.Count - 1; i >= 0; i--)
        {
            Slot s = slots[i];
            if (row < s.row || row >= s.row + s.rowSpan) continue;
            int rel = (int)Mathf.Repeat(col - s.column, columns);
            if (rel < s.columnSpan) { slots.RemoveAt(i); return true; }
        }
        return false;
    }

    int WrapCol(int c) { return (int)Mathf.Repeat(c, Mathf.Max(1, columns)); }

    public void FillSpiral(int count, int startRow, int rowStep, int startCol, int colStep, int span, bool append)
    {
        if (!append) slots.Clear();
        for (int i = 0; i < count; i++)
        {
            slots.Add(new Slot
            {
                row = startRow + i * rowStep,
                column = WrapCol(startCol + i * colStep),
                columnSpan = Mathf.Max(1, span),
                depth = paintDepth,
                rotate = paintRotate,
                speed = paintSpeed,
                rotationAngle = paintRotationAngle,
                rotationPause = paintRotationPause
            });
        }
    }

    public void FillBelt(int row, int span, int gapColumns, bool append)
    {
        if (!append) slots.Clear();
        int stepCols = Mathf.Max(1, span) + Mathf.Max(0, gapColumns);
        for (int c = 0; c + span <= columns; c += stepCols)
        {
            slots.Add(new Slot
            {
                row = row,
                column = c,
                columnSpan = Mathf.Max(1, span),
                depth = paintDepth,
                rotate = paintRotate,
                speed = paintSpeed,
                rotationAngle = paintRotationAngle,
                rotationPause = paintRotationPause
            });
        }
    }

    // ---------------------------------------------------------------- гизмо

    void OnDrawGizmos()
    {
        if (!showHitboxes) return;

        Transform colRoot = column != null ? column : transform;
        Transform colCol = colRoot.Find(ColumnColliderName);
        if (colCol != null)
        {
            Gizmos.color = new Color(0.2f, 0.95f, 0.45f, 0.55f);
            var cap = colCol.GetComponent<CapsuleCollider>();
            if (cap != null)
            {
                float r = cap.radius * Mathf.Max(colCol.lossyScale.x, colCol.lossyScale.z);
                float cylHalfH = Mathf.Max(0f, (cap.height - cap.radius * 2f) * 0.5f * colCol.lossyScale.y);
                DrawWireCircleXZ(colCol.position + Vector3.up * cylHalfH, r, 32);
                DrawWireCircleXZ(colCol.position - Vector3.up * cylHalfH, r, 32);
            }
            else
            {
                var colMc = colCol.GetComponent<MeshCollider>();
                if (colMc != null && colMc.sharedMesh != null)
                    Gizmos.DrawWireMesh(colMc.sharedMesh, colCol.position, colCol.rotation, colCol.lossyScale);
            }
        }

        Transform cont = transform.Find(ContainerName);
        if (cont == null) return;

        foreach (MagneticPlatform mp in cont.GetComponentsInChildren<MagneticPlatform>())
            mp.DrawHitboxGizmo();
    }

    static void DrawWireCircleXZ(Vector3 center, float radius, int segments)
    {
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float ang = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (showHitboxes) return;

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 1f);
        foreach (Slot s in slots)
        {
            float yMid = SlotCenterY(s);
            float hh = SlotThickness(s) * 0.5f;
            Vector3[] top = FootprintLoop(s, yMid + hh);
            Vector3[] bot = FootprintLoop(s, yMid - hh);
            for (int i = 0; i < top.Length - 1; i++)
            {
                Gizmos.DrawLine(top[i], top[i + 1]);
                Gizmos.DrawLine(bot[i], bot[i + 1]);
            }
            Gizmos.DrawLine(top[0], bot[0]);
            Gizmos.DrawLine(top[top.Length / 2 - 1], bot[bot.Length / 2 - 1]);
        }
    }
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(MagneticPlatformSpawner.Slot))]
public class MagneticPlatformSlotDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float step = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        float h = step * 13f;

        SerializedProperty customProp = property.FindPropertyRelative("useCustomStyle");
        if (customProp != null && customProp.isExpanded)
        {
            h += step;
            if (customProp.boolValue)
                h += step * 13f;
            else
                h += step * 1.8f;
        }

        return h + 4f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        int r = property.FindPropertyRelative("row").intValue;
        int c = property.FindPropertyRelative("column").intValue;
        int cs = property.FindPropertyRelative("columnSpan").intValue;
        bool rot = property.FindPropertyRelative("rotate").boolValue;
        float rotAng = property.FindPropertyRelative("rotationAngle").floatValue;
        SerializedProperty customProp = property.FindPropertyRelative("useCustomStyle");
        bool isCustom = customProp != null && customProp.boolValue;

        string rotTag = "";
        if (rot)
            rotTag = rotAng > 0.01f ? $"  [маятник {rotAng:0.#}°]" : "  [вращается]";

        string summary = $"Ряд {r}  |  Колонка {c} (шир. {cs})"
                       + (isCustom ? "  [свой стиль]" : "")
                       + rotTag;

        Rect foldRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldRect, property.isExpanded, new GUIContent(summary, label.tooltip), true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            float step = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            float y = position.y + step;

            DrawField(ref y, position, property, "row", "Ряд (снизу)", "Номер ряда плит снизу вверх (0 = самый нижний ряд).");
            DrawField(ref y, position, property, "column", "Колонка (по кругу)", "Номер колонки по кругу (0 = между вертикальной трубой №1 и №2).");
            DrawField(ref y, position, property, "columnSpan", "Ширина (в колонках)", "Сколько колонок плит занимает платформа по ширине.");
            DrawField(ref y, position, property, "rowSpan", "Высота (в рядах)", "Сколько рядов плит занимает платформа по высоте (1 = толщина как у плиты).");
            DrawField(ref y, position, property, "depth", "Своя длина (м, 0 = общая)", "Индивидуальная длина этой платформы от колонны наружу (м). 0 = взять общую длину из настроек спавнера.");
            DrawField(ref y, position, property, "angleOffset", "Сдвиг по кругу (°)", "Дополнительный поворот платформы по кругу в градусах для точной подгонки.");
            DrawField(ref y, position, property, "rotate", "Вращать вокруг колонны", "Включить вращение этой платформы вокруг колонны во время игры.");
            DrawField(ref y, position, property, "speed", "Скорость вращения (°/сек)", "Скорость вращения вокруг колонны в градусах в секунду.");
            DrawField(ref y, position, property, "reverse", "Обратное направление", "Вращать платформу в обратную сторону (против часовой стрелки сверху).");
            DrawField(ref y, position, property, "rotationAngle", "Угол поворота (0 = полный круг)", "На сколько градусов повернётся платформа перед возвратом обратно. 0 = полный оборот без остановки.");

            using (new EditorGUI.DisabledScope(rotAng <= 0.01f))
            {
                DrawField(ref y, position, property, "rotationPause", "Пауза на краях (сек)", "Задержка в секундах перед сменой направления в крайних точках.");
            }

            if (customProp != null)
            {
                Rect styleFoldRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                string styleHeader = isCustom
                    ? "Индивидуальный стиль и форма (активен)"
                    : "Индивидуальный стиль и форма (стандартный)";
                customProp.isExpanded = EditorGUI.Foldout(
                    styleFoldRect,
                    customProp.isExpanded,
                    new GUIContent(styleHeader, "Раскройте, чтобы задать для этой платформы свои закругления, фаски, канавки или цвет подсветки вместо стандартных."),
                    true);
                y += step;

                if (customProp.isExpanded)
                {
                    EditorGUI.indentLevel++;

                    Rect toggleRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.PropertyField(toggleRect, customProp,
                        new GUIContent("Включить свой стиль", "Если выключено — используются стандартные настройки спавнера. Если включено — работают параметры ниже."));
                    bool toggledOn = EditorGUI.EndChangeCheck() && customProp.boolValue;
                    y += step;

                    if (toggledOn)
                        CopyDefaultsFromSpawner(property);

                    if (!customProp.boolValue)
                    {
                        Rect infoRect = EditorGUI.IndentedRect(new Rect(position.x, y, position.width, step * 1.65f));
                        EditorGUI.HelpBox(infoRect, "Используется стандартный стиль из раздела 2 спавнера.", MessageType.None);
                        y += step * 1.8f;
                    }
                    else
                    {
                        DrawField(ref y, position, property, "constantWidth", "Прямой мостик", "Постоянная ширина по всей длине (иначе расширяется веером).");
                        DrawField(ref y, position, property, "gap", "Зазор с полем (м)", "Ширина зазора магнитного поля между колонной и этой платформой.");
                        DrawField(ref y, position, property, "cornerBevel", "Закругление / срез углов (м)", "Размер среза или радиус закругления внешних углов этой платформы (м).");
                        DrawField(ref y, position, property, "roundCorners", "Плавное закругление углов", "Если включено — внешние углы плавно закругляются по дуге. Если выключено — прямой скос под 45°.");
                        DrawField(ref y, position, property, "chamfer", "Фаска по краям (м)", "Ширина декоративного скоса по верхнему и нижнему краям платформы (м).");
                        DrawField(ref y, position, property, "grooveCount", "Количество полос / канавок", "Сколько поперечных канавок со светящимися полосами разместить вдоль этой платформы.");
                        DrawField(ref y, position, property, "grooveWidth", "Ширина полосы (м)", "Ширина поперечной канавки и светящейся полосы (м).");
                        DrawField(ref y, position, property, "grooveDepth", "Глубина канавки (м)", "Глубина поперечной канавки (м).");
                        DrawField(ref y, position, property, "glowGrooves", "Подсветка в канавках", "Включить светящиеся вставки внутри канавок этой платформы.");
                        DrawField(ref y, position, property, "glowSideAccents", "Боковые световые метки", "Включить тонкие вертикальные световые полоски по бокам платформы.");
                        DrawField(ref y, position, property, "glowColor", "Цвет подсветки", "Индивидуальный цвет светящихся вставок для этой платформы.");
                        DrawField(ref y, position, property, "glowIntensity", "Яркость свечения (HDR)", "Сила свечения (Emission) неоновых полос для этой платформы.");

                        Rect btnRect = EditorGUI.IndentedRect(new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight));
                        if (GUI.Button(btnRect, new GUIContent("Скопировать значения из стандартных", "Заполнить поля выше текущими общими настройками из спавнера")))
                            CopyDefaultsFromSpawner(property);
                        y += step;
                    }

                    EditorGUI.indentLevel--;
                }
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    static void CopyDefaultsFromSpawner(SerializedProperty slotProp)
    {
        var sp = slotProp.serializedObject.targetObject as MagneticPlatformSpawner;
        if (sp == null) return;

        slotProp.FindPropertyRelative("constantWidth").boolValue = sp.constantWidth;
        slotProp.FindPropertyRelative("gap").floatValue = sp.gap;
        slotProp.FindPropertyRelative("cornerBevel").floatValue = sp.cornerBevel;
        slotProp.FindPropertyRelative("roundCorners").boolValue = sp.roundCorners;
        slotProp.FindPropertyRelative("chamfer").floatValue = sp.chamfer;
        slotProp.FindPropertyRelative("grooveCount").intValue = sp.grooveCount;
        slotProp.FindPropertyRelative("grooveWidth").floatValue = sp.grooveWidth;
        slotProp.FindPropertyRelative("grooveDepth").floatValue = sp.grooveDepth;
        slotProp.FindPropertyRelative("glowGrooves").boolValue = sp.glowGrooves;
        slotProp.FindPropertyRelative("glowSideAccents").boolValue = sp.glowSideAccents;
        slotProp.FindPropertyRelative("glowColor").colorValue = sp.glowColor;
        slotProp.FindPropertyRelative("glowIntensity").floatValue = sp.glowIntensity;
    }

    static void DrawField(ref float y, Rect root, SerializedProperty parent, string relName, string text, string tip)
    {
        SerializedProperty sp = parent.FindPropertyRelative(relName);
        if (sp == null) return;
        Rect rect = new Rect(root.x, y, root.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(rect, sp, new GUIContent(text, tip));
        y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
    }
}

[CustomEditor(typeof(MagneticPlatformSpawner))]
public class MagneticPlatformSpawnerEditor : Editor
{
    bool showGridDetails = false;
    bool showPlatformDetails = true;
    bool showPhysicsDetails = true;
    bool showTemplates = false;

    int pCount = 8, pStartRow = 2, pRowStep = 3, pStartCol = 0, pColStep = 3, pSpan = 2;
    int bRow = 10, bSpan = 2, bGap = 1;
    bool append = false;

    void DrawProp(string propName, string label, string tooltip, bool includeChildren = false)
    {
        SerializedProperty p = serializedObject.FindProperty(propName);
        if (p != null)
            EditorGUILayout.PropertyField(p, new GUIContent(label, tooltip), includeChildren);
    }

    public override void OnInspectorGUI()
    {
        var sp = (MagneticPlatformSpawner)target;
        serializedObject.Update();

        // ---------------------------------------------------------------- 1. КОЛОННА
        GUILayout.Label("1. КОЛОННА И СТЫКОВКА", EditorStyles.boldLabel);
        DrawProp("column", "Объект колонны",
            "Корневой объект колонны в сцене, внутри которого лежат трубы Glass_Vertical_* и Glass_Horizontal_*. Если пусто — скрипт найдёт колонну сам.");
        DrawProp("autoDetect", "Автоопределение сетки",
            "Автоматически считывать радиус, ряды и колонки из труб колонны при каждой генерации. Рекомендуется держать включённым.");
        DrawProp("groove", "Ширина шва между плитами (м)",
            "Ширина зазора между плитами колонны по высоте. Влияет на толщину платформы и высоту выхода магнитных дуг из горизонтальных колец.");
        DrawProp("tubeToSurface", "Отступ от оси трубы до плиты (м)",
            "Расстояние от центральной оси вертикальной трубы до поверхности плит колонны. Помогает точно подогнать начало магнитного поля под толщину трубки.");

        showGridDetails = EditorGUILayout.Foldout(
            showGridDetails,
            sp.autoDetect
                ? "Параметры сетки колонны (заполняются автоматически)"
                : "Параметры сетки колонны (ручной ввод)",
            true);

        if (showGridDetails)
        {
            EditorGUI.indentLevel++;
            if (sp.autoDetect)
            {
                EditorGUILayout.HelpBox(
                    "Пометка: пока включено «Автоопределение сетки», эти поля вычисляются автоматически по трубам колонны при каждой генерации — менять их вручную не нужно.",
                    MessageType.Info);
            }
            using (new EditorGUI.DisabledScope(sp.autoDetect))
            {
                DrawProp("columns", "Количество колонок",
                    "Количество вертикальных секторов по окружности колонны. Определяется по числу труб Glass_Vertical_*.");
                DrawProp("rows", "Количество рядов",
                    "Количество горизонтальных рядов плит по высоте колонны. Определяется по числу колец Glass_Horizontal_*.");
                DrawProp("rowHeight", "Высота одного ряда (м)",
                    "Расстояние по вертикали между соседними горизонтальными кольцами колонны в метрах.");
                DrawProp("columnRadius", "Радиус колонны (м)",
                    "Радиус внешней поверхности плит колонны в метрах. От него отсчитывается зазор магнитного поля.");
                DrawProp("ringY0Local", "Высота нижнего кольца (м)",
                    "Локальная координата Y самого нижнего горизонтального кольца относительно центра колонны.");
                DrawProp("tubeAzimuth0Deg", "Начальный угол трубы (°)",
                    "Азимут вертикальной трубы №1 в градусах (0° = ось +X). Привязывает сетку платформ к трубам.");
                DrawProp("tubeDirection", "Направление нумерации (+1 / −1)",
                    "Направление по кругу (+1 или −1), в котором возрастают номера вертикальных труб.");
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        // ---------------------------------------------------------------- 2. ОБЩИЕ НАСТРОЙКИ ПЛАТФОРМ
        GUILayout.Label("2. СТАНДАРТНЫЙ ШАБЛОН ПЛАТФОРМ", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Пометка: эти настройки применяются ко всем стандартным платформам (у которых в слоте не включён индивидуальный стиль). Обновляются по кнопке «СГЕНЕРИРОВАТЬ».",
            MessageType.None);

        DrawProp("constantWidth", "Прямой мостик (постоянная ширина)",
            "Если включено — платформа вытягивается от колонны прямой балкой постоянной ширины. Если выключено — расширяется веером.");
        DrawProp("depth", "Базовая длина от колонны (м)",
            "Длина платформ от внутреннего края наружу (в метрах). Используется для всех слотов, у которых своя длина равна 0.");
        DrawProp("gap", "Зазор с магнитным полем (м)",
            "Ширина воздушного зазора в метрах между стенкой колонны и началом самой платформы. Здесь строится магнитное поле.");
        DrawProp("endInsetDeg", "Отступ от вертикальных труб (°)",
            "Боковой отступ в градусах с краёв платформы, чтобы её торцы не врезались в вертикальные трубы колонны.");

        showPlatformDetails = EditorGUILayout.Foldout(
            showPlatformDetails,
            "Закругления, фаски, подсветка и магнитное поле",
            true);

        if (showPlatformDetails)
        {
            EditorGUI.indentLevel++;
            DrawProp("cornerBevel", "Закругление / срез внешних углов (м)",
                "Размер скоса или радиус закругления на дальних углах прямого мостика в метрах.");
            DrawProp("roundCorners", "Плавное закругление углов",
                "Если включено — внешние углы плавно закругляются по дуге. Если выключено — делается прямой скос под 45°.");
            DrawProp("chamfer", "Размер фаски по периметру (м)",
                "Ширина декоративного скоса по верхнему и нижнему периметру платформы в метрах.");
            DrawProp("grooveCount", "Количество полос / канавок",
                "Сколько поперечных канавок со светящимися полосами разместить вдоль платформы (от 0 до 6).");
            DrawProp("grooveInset", "Отступ крайних канавок (м)",
                "Расстояние в метрах от внутреннего и внешнего краёв платформы до крайних канавок.");
            DrawProp("grooveWidth", "Ширина полосы / канавки (м)",
                "Ширина поперечной канавки и светящейся вставки в метрах.");
            DrawProp("grooveDepth", "Глубина канавки (м)",
                "Глубина поперечной канавки на поверхности платформы в метрах.");
            DrawProp("glowGrooves", "Подсветка в канавках",
                "Вставлять светящиеся полосы внутрь поперечных канавок платформы.");
            DrawProp("glowSideAccents", "Боковые световые метки",
                "Добавлять тонкие светящиеся вертикальные полоски на левой и правой стенках платформы напротив канавок.");
            DrawProp("glowColor", "Цвет подсветки",
                "Цвет неоновых вставок в канавках (используется, если не назначен отдельный материал подсветки).");
            DrawProp("glowIntensity", "Яркость свечения (HDR)",
                "Сила свечения (Emission) неоновых полос для эффекта Bloom.");
            DrawProp("fieldReach", "Прогиб магнитных дуг (м)",
                "Высота дугообразного прогиба энергетических потоков, идущих от горизонтальных колец труб на платформу.");
            DrawProp("fieldLayers", "Количество слоёв плазмы",
                "Число внутренних радиальных слоёв плазмы между колонной и платформой (от 1 до 6).");
            DrawProp("bodyMaterial", "Материал корпуса",
                "Материал твёрдой части платформы. Если оставить пустым — автоматически создастся тёмно-графитовый материал.");
            DrawProp("glowMaterial", "Материал подсветки (необяз.)",
                "Кастомный материал для светящихся полос. Если оставить пустым — создастся автоматически из цвета и яркости выше.");
            DrawProp("fieldMaterial", "Материал магнитного поля",
                "Материал с шейдером SciFi/MagneticFieldPlatform. Если оставить пустым — шейдер найдётся в проекте сам.");
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        showPhysicsDetails = EditorGUILayout.Foldout(
            showPhysicsDetails,
            "Физика и точные хитбоксы (колонна и платформы)",
            true);

        if (showPhysicsDetails)
        {
            EditorGUI.indentLevel++;
            DrawProp("columnCollider", "Коллайдер самой колонны",
                "Создавать гладкий цилиндрический MeshCollider на центральной колонне по её внешнему радиусу.");
            using (new EditorGUI.DisabledScope(!sp.columnCollider))
            {
                DrawProp("columnColliderSegments", "Сегменты дуги колонны",
                    "Плавность окружности физического цилиндра колонны (48 = гладкое скольжение мяча без рёбер).");
                DrawProp("columnHeightPadding", "Запас высоты колонны (м)",
                    "Дополнительный выступ коллайдера колонны выше верхнего и ниже нижнего колец (в метрах).");
            }

            EditorGUILayout.Space(4f);
            DrawProp("colliderMode", "Тип хитбокса платформ",
                "• Точный по контуру (гладкая дека) — повторяет дугу, закругления и фаску, но верхняя плоскость идеально ровная (мяч не спотыкается о канавки).\n" +
                "• Точный по визуальному мешу — полностью повторяет 3D-меш включая углубления канавок.\n" +
                "• Упрощённые BoxCollider — грубая аппроксимация прямоугольниками.");
            DrawProp("colliderChamfer", "Фаска в хитбоксе платформ",
                "Добавлять скошенную кромку по краям хитбокса, чтобы мяч плавно съезжал с платформы и не цеплялся за острый угол.");
            DrawProp("physicMaterial", "Физический материал",
                "Physics Material (трение и упругость), назначаемый на коллайдер колонны и всех платформ.");
            DrawProp("showHitboxes", "Показывать хитбоксы в сцене",
                "Отрисовывать зелёную сетку физических коллайдеров колонны и платформ в окне Scene View.");
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        // ---------------------------------------------------------------- 3. РИСОВАНИЕ В СЦЕНЕ
        GUILayout.Label("3. РИСОВАНИЕ В ОКНЕ СЦЕНЫ (КИСТЬ)", EditorStyles.boldLabel);
        DrawProp("paintMode", "Включить режим рисования",
            "При включении можно кликать прямо по колонне в окне Scene View: ЛКМ — поставить платформу, Shift + ЛКМ — удалить.");
        DrawProp("paintColumnSpan", "Ширина кисти (колонок)",
            "Сколько колонок по ширине будут занимать новые платформы при клике по колонне.");
        DrawProp("paintRowSpan", "Высота кисти (рядов)",
            "Сколько рядов по высоте будут занимать новые платформы при клике по колонне.");
        DrawProp("paintDepth", "Длина кисти (м, 0 = общая)",
            "Индивидуальная длина в метрах для новых платформ. Если 0 — берётся «Базовая длина от колонны» выше.");
        DrawProp("paintRotate", "Вращать новые платформы",
            "Сразу включать вращение вокруг колонны у платформ, создаваемых кистью или шаблонами.");
        DrawProp("paintSpeed", "Скорость вращения (°/сек)",
            "Скорость вращения вокруг колонны в градусах в секунду для новых платформ.");
        DrawProp("paintRotationAngle", "Угол поворота (0 = полный круг)",
            "На сколько градусов повернётся новая платформа перед возвратом обратно (0 = полный оборот по кругу).");
        using (new EditorGUI.DisabledScope(sp.paintRotationAngle <= 0.01f))
        {
            DrawProp("paintRotationPause", "Пауза на краях (сек)",
                "Задержка в секундах перед сменой направления в крайних точках.");
        }

        EditorGUILayout.Space();

        // ---------------------------------------------------------------- 4. СПИСОК СЛОТОВ
        GUILayout.Label("4. СПИСОК РАЗМЕЩЁННЫХ ПЛАТФОРМ", EditorStyles.boldLabel);
        DrawProp("generateOnStart", "Генерировать при запуске игры",
            "Автоматически создать платформы при старте сцены, если контейнер платформ пуст.");
        DrawProp("slots", "Слоты платформ",
            "Список всех размещённых платформ. Раскройте нужный слот, чтобы задать его позицию или включить индивидуальный стиль.", true);

        if (serializedObject.ApplyModifiedProperties())
            SceneView.RepaintAll();

        EditorGUILayout.Space();

        // ---------------------------------------------------------------- 5. УПРАВЛЕНИЕ И ШАБЛОНЫ
        GUILayout.Label("5. УПРАВЛЕНИЕ И ГЕНЕРАЦИЯ", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Текущая сетка: " + sp.columns + " колонок × " + sp.rows + " рядов | Высота ряда: " + sp.rowHeight.ToString("0.###") +
            " м | Радиус: " + sp.columnRadius.ToString("0.###") + " м\n" +
            "Толщина платформы в 1 ряд: " + (sp.rowHeight - sp.groove).ToString("0.###") + " м",
            MessageType.None);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("СГЕНЕРИРОВАТЬ", "Создать или перестроить коллайдер колонны и все платформы в сцене"), GUILayout.Height(28)))
        {
            Undo.RecordObject(sp, "Сгенерировать платформы");
            sp.Generate();
            EditorUtility.SetDirty(sp);
        }
        if (GUILayout.Button(new GUIContent("ОЧИСТИТЬ СЦЕНУ", "Удалить сгенерированные 3D-платформы из сцены (список слотов сохранится)"), GUILayout.Height(28)))
            sp.Clear();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Обновить сетку и колонну", "Считать размеры колонны по трубам и обновить её коллайдер"), GUILayout.Height(22)))
        {
            Undo.RecordObject(sp, "Определить сетку");
            if (!sp.DetectGrid()) Debug.LogWarning("[Спавнер] Сетка не определена — проверьте поле «Объект колонны».");
            sp.BuildColumnCollider();
            EditorUtility.SetDirty(sp);
        }
        if (GUILayout.Button(new GUIContent("Очистить кэш мешей", "Сбросить кэш сгенерированных мешей и материалов"), GUILayout.Height(22)))
        {
            sp.ClearResourceCache();
            sp.Generate();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        showTemplates = EditorGUILayout.Foldout(showTemplates, "Шаблоны массового заполнения (спираль / пояс)", true);
        if (showTemplates)
        {
            EditorGUI.indentLevel++;
            append = EditorGUILayout.Toggle(
                new GUIContent("Добавлять к существующим", "Если включено — новые слоты добавятся к текущему списку. Если выключено — старый список очистится."),
                append);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Спираль вокруг колонны", EditorStyles.miniBoldLabel);
            pCount = EditorGUILayout.IntField(new GUIContent("Количество платформ", "Сколько платформ создать в спирали."), pCount);
            pStartRow = EditorGUILayout.IntField(new GUIContent("Начальный ряд", "Ряд снизу, с которого начнётся первая платформа спирали."), pStartRow);
            pRowStep = EditorGUILayout.IntField(new GUIContent("Шаг вверх (рядов)", "На сколько рядов выше будет каждая следующая платформа."), pRowStep);
            pStartCol = EditorGUILayout.IntField(new GUIContent("Начальная колонка", "Колонка по кругу для первой платформы спирали."), pStartCol);
            pColStep = EditorGUILayout.IntField(new GUIContent("Сдвиг по кругу (колонок)", "На сколько колонок вбок будет смещаться каждая следующая платформа."), pColStep);
            pSpan = EditorGUILayout.IntField(new GUIContent("Ширина (колонок)", "Ширина каждой платформы в колонках."), pSpan);
            if (GUILayout.Button(new GUIContent("Заполнить спиралью", "Добавить в список серию платформ, поднимающихся по спирали")))
            {
                Undo.RecordObject(sp, "Заполнить спиралью");
                sp.FillSpiral(pCount, pStartRow, pRowStep, pStartCol, pColStep, pSpan, append);
                EditorUtility.SetDirty(sp);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Пояс (кольцо платформ на одном уровне)", EditorStyles.miniBoldLabel);
            bRow = EditorGUILayout.IntField(new GUIContent("Ряд по высоте", "Номер ряда, на котором выстроится кольцо платформ."), bRow);
            bSpan = EditorGUILayout.IntField(new GUIContent("Ширина (колонок)", "Ширина каждой платформы в поясе."), bSpan);
            bGap = EditorGUILayout.IntField(new GUIContent("Промежуток (колонок)", "Сколько пустых колонок оставлять между соседними платформами."), bGap);
            if (GUILayout.Button(new GUIContent("Заполнить поясом", "Расставить платформы по кругу на выбранном ряду")))
            {
                Undo.RecordObject(sp, "Заполнить поясом");
                sp.FillBelt(bRow, bSpan, bGap, append);
                EditorUtility.SetDirty(sp);
            }
            EditorGUILayout.EndVertical();

            if (GUILayout.Button(new GUIContent("Очистить весь список слотов", "Полностью удалить все записи из списка слотов")))
            {
                Undo.RecordObject(sp, "Очистить список слотов");
                sp.slots.Clear();
                EditorUtility.SetDirty(sp);
            }
            EditorGUI.indentLevel--;
        }
    }

    void OnSceneGUI()
    {
        var sp = (MagneticPlatformSpawner)target;
        if (!sp.paintMode) return;

        Event e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        int row, col;
        if (!sp.RaycastCell(ray, out row, out col)) return;

        var preview = new MagneticPlatformSpawner.Slot
        {
            row = Mathf.Clamp(row, 0, Mathf.Max(0, sp.rows - sp.paintRowSpan)),
            column = col,
            columnSpan = sp.paintColumnSpan,
            rowSpan = sp.paintRowSpan,
            depth = sp.paintDepth
        };

        bool removing = e.shift;
        Handles.color = removing ? new Color(1f, 0.35f, 0.35f) : new Color(0.3f, 1f, 0.5f);
        float yMid = sp.SlotCenterY(preview);
        float hh = sp.SlotThickness(preview) * 0.5f;
        Handles.DrawAAPolyLine(3f, sp.FootprintLoop(preview, yMid + hh));
        Handles.DrawAAPolyLine(3f, sp.FootprintLoop(preview, yMid - hh));

        Handles.BeginGUI();
        GUI.Label(new Rect(e.mousePosition.x + 14f, e.mousePosition.y + 8f, 240f, 20f),
                  (removing ? "Удалить: " : "Добавить: ") + "ряд " + row + ", колонка " + col);
        Handles.EndGUI();

        if (e.type == EventType.MouseMove) SceneView.RepaintAll();

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Undo.RecordObject(sp, "Нарисовать слот платформы");
            if (removing)
            {
                sp.RemoveSlotAtCell(row, col);
            }
            else
            {
                preview.rotate = sp.paintRotate;
                preview.speed = sp.paintSpeed;
                preview.rotationAngle = sp.paintRotationAngle;
                preview.rotationPause = sp.paintRotationPause;
                sp.AddSlot(preview);
            }
            EditorUtility.SetDirty(sp);
            e.Use();
            SceneView.RepaintAll();
        }
    }
}
#endif