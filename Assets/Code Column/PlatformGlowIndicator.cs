using UnityEngine;
using System.Collections.Generic;

// Заготовка на будущее: индикатор вращения платформы.
// Динамически меняет цвет светящегося материала "GlowMat 1" на колонне:
//   - платформа НЕ крутится -> красное свечение
//   - платформа КРУТИТСЯ   -> зелёное свечение
//
// ВАЖНО ПРО МАТЕРИАЛЫ: колонна может состоять из нескольких материалов
// (например, тело + светящиеся полосы). Скрипт меняет цвет ТОЛЬКО у слота
// с материалом "GlowMat 1" — остальные материалы renderer'а не трогаются,
// блок свойств ставится на конкретный индекс материала.
//
// Использование:
//   1. Повесьте скрипт на колонну (или на платформу).
//   2. Renderer'ы с материалом "GlowMat 1" найдутся автоматически в детях.
//   3. Платформы (RotatingPlatform) тоже находятся автоматически среди детей.
//      Порядок приоритета состояния:
//        a) ручное управление из кода: SetRotating(true/false);
//        b) платформа, указанная вручную в поле platform;
//        c) все дочерние RotatingPlatform: хотя бы одна крутится -> зелёный.
//
// ВАЖНО: у материала "GlowMat 1" в инспекторе должна быть включена Emission,
// иначе свечение видно не будет. Цвет меняется через MaterialPropertyBlock,
// поэтому копии материалов не плодятся и батчинг не ломается.
public class PlatformGlowIndicator : MonoBehaviour
{
    // Пара "renderer + индекс материала GlowMat 1 на нём"
    private class GlowTarget
    {
        public Renderer renderer;
        public int materialIndex;
    }

    [Header("Ссылки")]
    [Tooltip("Renderer'ы с материалом GlowMat 1. Если пусто — автопоиск по имени материала в детях")]
    public Renderer[] glowRenderers;
    [Tooltip("Платформа вручную (перекрывает автопоиск). Если пусто — берутся все дочерние RotatingPlatform")]
    public RotatingPlatform platform;

    [Header("Настройки")]
    [Tooltip("Имя материала для автопоиска")]
    public string materialName = "GlowMat 1";
    [Tooltip("Свечение, когда платформа НЕ крутится")]
    public Color idleColor = new Color(1f, 0.1f, 0.05f);
    [Tooltip("Свечение, когда платформа КРУТИТСЯ")]
    public Color rotatingColor = new Color(0.1f, 1f, 0.2f);
    [Tooltip("Сила свечения (множитель над цветом)")]
    public float emissionIntensity = 2.5f;
    [Tooltip("Скорость плавного перехода цвета")]
    [Range(0.01f, 10f)]
    public float lerpSpeed = 4f;
    [Tooltip("Как часто (в секундах) перепроверять дочерние платформы, если их ещё нет. Нужно, потому что SpiralStepSpawner создаёт платформы в рантайме")]
    public float rescanInterval = 0.5f;

    private MaterialPropertyBlock propBlock;
    private List<GlowTarget> glowTargets = new List<GlowTarget>();
    private RotatingPlatform[] childPlatforms;
    private bool manualSet;            // кто-то вызвал SetRotating вручную — автоопределение отключается до сброса
    private bool isRotating;
    private float nextScanTime;
    private Color currentColor;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();

        if (glowRenderers != null && glowRenderers.Length > 0)
        {
            // Ручной список renderer'ов: индекс GlowMat 1 ищем на каждом из них
            foreach (var r in glowRenderers)
            {
                if (r == null) continue;
                int idx = FindGlowMaterialIndex(r);
                glowTargets.Add(new GlowTarget { renderer = r, materialIndex = idx });
            }
        }
        else
        {
            // Автопоиск renderer'ов с материалом GlowMat 1 среди детей
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                int idx = FindGlowMaterialIndex(r);
                if (idx >= 0)
                    glowTargets.Add(new GlowTarget { renderer = r, materialIndex = idx });
            }
        }

        if (glowTargets.Count == 0)
            Debug.LogWarning($"[PlatformGlowIndicator] Материал '{materialName}' не найден ни на одном Renderer в '{name}'.");

        currentColor = idleColor;
        nextScanTime = Time.time;
    }

    // Возвращает индекс слота с материалом GlowMat 1 у renderer'а, либо -1
    private int FindGlowMaterialIndex(Renderer r)
    {
        var mats = r.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
        {
            var mat = mats[i];
            if (mat == null) continue;
            // В рантайме имя материала может быть с суффиксом " (Instance)" — убираем его
            string matName = mat.name.Replace(" (Instance)", "").Trim();
            if (matName == materialName) return i;
        }
        return -1;
    }

    // Публичное управление из любого кода (кнопки, триггеры):
    // SetRotating(true) -> зелёный, SetRotating(false) -> красный.
    // После ручного вызова автоопределение по платформам отключается,
    // пока не вызвать ResetToAuto().
    public void SetRotating(bool rotating)
    {
        manualSet = true;
        isRotating = rotating;
    }

    // Вернуть автоматическое определение состояния по платформам
    public void ResetToAuto() => manualSet = false;

    private void Update()
    {
        if (!manualSet)
        {
            if (platform != null)
            {
                // Ручная ссылка на платформу — приоритетнее автопоиска
                Vector3 av = platform.GetAngularVelocity();
                isRotating = av.sqrMagnitude > 0.0001f;
            }
            else
            {
                // Автопоиск дочерних платформ с периодическим пересканированием:
                // SpiralStepSpawner может создать их позже, чем стартанёт этот скрипт
                if ((childPlatforms == null || childPlatforms.Length == 0) && Time.time >= nextScanTime)
                {
                    childPlatforms = GetComponentsInChildren<RotatingPlatform>();
                    nextScanTime = Time.time + rescanInterval;
                }

                // Хоть одна дочерняя платформа крутится -> зелёный
                isRotating = false;
                if (childPlatforms != null)
                {
                    for (int i = 0; i < childPlatforms.Length; i++)
                    {
                        if (childPlatforms[i] != null &&
                            childPlatforms[i].GetAngularVelocity().sqrMagnitude > 0.0001f)
                        {
                            isRotating = true;
                            break;
                        }
                    }
                }
            }
        }

        Color target = isRotating ? rotatingColor : idleColor;
        currentColor = Color.Lerp(currentColor, target, Time.deltaTime * lerpSpeed);

        foreach (var t in glowTargets)
        {
            var r = t.renderer;
            if (r == null) continue;

            // Материал именно того слота, который перекрашиваем
            var mats = r.sharedMaterials;
            if (t.materialIndex < 0 || t.materialIndex >= mats.Length) continue;
            Material mat = mats[t.materialIndex];
            if (mat == null) continue;

            r.GetPropertyBlock(propBlock, t.materialIndex);

            // _BaseColor — URP, _Color — Built-in: ставим то, что есть в шейдере
            if (mat.HasProperty("_BaseColor")) propBlock.SetColor("_BaseColor", currentColor);
            if (mat.HasProperty("_Color")) propBlock.SetColor("_Color", currentColor);
            if (mat.HasProperty("_EmissionColor")) propBlock.SetColor("_EmissionColor", currentColor * emissionIntensity);

            // Ставим блок ТОЛЬКО на слот GlowMat 1 — второй материал не трогаем
            r.SetPropertyBlock(propBlock, t.materialIndex);
        }
    }
}
