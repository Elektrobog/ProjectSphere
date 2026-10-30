using UnityEngine;
using System.Collections.Generic;

// Заготовка на будущее: индикатор вращения платформы.
// Динамически меняет цвет светящегося материала "GlowMat 1" на колонне:
//   - платформа НЕ крутится -> красное свечение
//   - платформа КРУТИТСЯ   -> зелёное свечение
//
// Использование:
//   1. Повесьте скрипт на колонну (или на платформу).
//   2. Renderer'ы с материалом "GlowMat 1" найдутся автоматически в детях.
//      Можно также заполнить список вручную в инспекторе.
//   3. Поле platform — ссылка на RotatingPlatform: тогда состояние
//      определяется автоматически. Если оставить пустым — управляйте
//      вручную из любого кода: GetComponent<PlatformGlowIndicator>().SetRotating(true);
//
// ВАЖНО: у материала "GlowMat 1" в инспекторе должна быть включена Emission,
// иначе свечение видно не будет. Цвет меняется через MaterialPropertyBlock,
// поэтому копии материалов не плодятся и батчинг не ломается.
public class PlatformGlowIndicator : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Renderer'ы с материалом GlowMat 1. Если пусто — автопоиск по имени материала в детях")]
    public List<Renderer> glowRenderers = new List<Renderer>();
    [Tooltip("Вращающаяся платформа. Если пусто — управление через SetRotating() из кода")]
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

    private MaterialPropertyBlock propBlock;
    private bool isRotating;
    private Color currentColor;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        if (glowRenderers.Count == 0) AutoFindRenderers();
        currentColor = idleColor;
    }

    // Автопоиск renderer'ов, у которых среди материалов есть "GlowMat 1"
    private void AutoFindRenderers()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                // В инспекторе имя материала может быть с суффиксом " (Instance)" — убираем его
                string matName = mat.name.Replace(" (Instance)", "").Trim();
                if (matName == materialName)
                {
                    glowRenderers.Add(r);
                    break;
                }
            }
        }

        if (glowRenderers.Count == 0)
            Debug.LogWarning($"[PlatformGlowIndicator] Материал '{materialName}' не найден ни на одном Renderer в '{name}'. Заполните список glowRenderers вручную.");
    }

    // Публичное управление из любого кода (кнопки, триггеры, катсцены):
    // SetRotating(true) -> зелёный, SetRotating(false) -> красный
    public void SetRotating(bool rotating) => isRotating = rotating;

    private void Update()
    {
        // Если платформа задана — состояние определяется автоматически:
        // угловая скорость ~0 (остановлена) -> красный, крутится -> зелёный
        if (platform != null)
        {
            Vector3 av = platform.GetAngularVelocity();
            isRotating = av.sqrMagnitude > 0.0001f;
        }

        Color target = isRotating ? rotatingColor : idleColor;
        currentColor = Color.Lerp(currentColor, target, Time.deltaTime * lerpSpeed);

        foreach (var r in glowRenderers)
        {
            if (r == null || r.sharedMaterial == null) continue;

            r.GetPropertyBlock(propBlock);

            // _BaseColor — URP, _Color — Built-in: ставим то, что есть в шейдере
            if (r.sharedMaterial.HasProperty("_BaseColor")) propBlock.SetColor("_BaseColor", currentColor);
            if (r.sharedMaterial.HasProperty("_Color")) propBlock.SetColor("_Color", currentColor);
            if (r.sharedMaterial.HasProperty("_EmissionColor")) propBlock.SetColor("_EmissionColor", currentColor * emissionIntensity);

            r.SetPropertyBlock(propBlock);
        }
    }
}
