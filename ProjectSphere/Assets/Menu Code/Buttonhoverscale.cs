using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Вешается прямо на кнопку (или любой UI-элемент с RectTransform).
/// При наведении курсора — плавно увеличивается, при зажатии — плавно уменьшается.
/// Работает через unscaled time, так что не ломается при Time.timeScale = 0 (пауза).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Масштабы")]
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float pressedScale = 0.92f;

    [Header("Скорость анимации")]
    [SerializeField] private float animationSpeed = 12f; // чем больше — тем быстрее реакция

    private RectTransform rectTransform;
    private Vector3 targetScale;
    private Coroutine scaleCoroutine;
    private bool isPointerOver = false;
    private bool isPressed = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        targetScale = Vector3.one * normalScale;
    }

    void OnEnable()
    {
        // на случай повторной активации объекта (например, после Destroy/пересоздания меню)
        isPointerOver = false;
        isPressed = false;
        rectTransform.localScale = Vector3.one * normalScale;
        targetScale = rectTransform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        UpdateTargetScale();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        isPressed = false; // если увели курсор зажатым — считаем, что отпустили
        UpdateTargetScale();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        UpdateTargetScale();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        UpdateTargetScale();
    }

    private void UpdateTargetScale()
    {
        float scale;

        if (isPressed)
            scale = pressedScale;
        else if (isPointerOver)
            scale = hoverScale;
        else
            scale = normalScale;

        targetScale = Vector3.one * scale;

        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(AnimateScale());
    }

    private IEnumerator AnimateScale()
    {
        while (Vector3.Distance(rectTransform.localScale, targetScale) > 0.001f)
        {
            rectTransform.localScale = Vector3.Lerp(
                rectTransform.localScale,
                targetScale,
                animationSpeed * Time.unscaledDeltaTime
            );
            yield return null;
        }

        rectTransform.localScale = targetScale;
    }
}