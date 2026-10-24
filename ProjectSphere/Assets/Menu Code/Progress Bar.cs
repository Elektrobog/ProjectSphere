using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SimpleProgressBar : MonoBehaviour
{
    [Header("Ссылки на компоненты")]
    [SerializeField] private RectTransform fillRect;    // RectTransform объекта ProgressFill
    [SerializeField] private TextMeshProUGUI percentText; // Текст процентов
    [SerializeField] private TextMeshProUGUI statusText;  // Текст статуса
    [SerializeField] private Image fillImage;           // Image компонент заполнения

    [Header("Настройки")]
    [SerializeField] private float maxWidth = 600f;     // Максимальная ширина прогресс-бара
    [SerializeField] private float smoothTime = 0.2f;   // Время сглаживания анимации

    [Header("Цвета")]
    [SerializeField] private Color startColor = new Color(1, 0, 0, 1);    // Красный (0%)
    [SerializeField] private Color middleColor = new Color(1, 1, 0, 1);   // Желтый (50%)
    [SerializeField] private Color endColor = new Color(0, 1, 0, 1);      // Зеленый (100%)

    private float currentProgress = 0f;
    private float targetProgress = 0f;
    private float velocity = 0f;

    void Start()
    {
        // Автоматическое получение ссылок если не назначены вручную
        if (fillRect == null)
            fillRect = transform.Find("ProgressFill")?.GetComponent<RectTransform>();

        if (percentText == null)
            percentText = transform.Find("PercentageText")?.GetComponent<TextMeshProUGUI>();

        if (statusText == null)
            statusText = transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();

        if (fillImage == null && fillRect != null)
            fillImage = fillRect.GetComponent<Image>();

        // Устанавливаем начальный прогресс 0
        SetProgressImmediate(0f);

        // Убеждаемся, что начальная ширина = 0
        if (fillRect != null)
        {
            fillRect.sizeDelta = new Vector2(0, fillRect.sizeDelta.y);
        }
    }

    void Update()
    {
        // Плавное изменение прогресса (сглаживание)
        if (Mathf.Abs(currentProgress - targetProgress) > 0.001f)
        {
            currentProgress = Mathf.SmoothDamp(
                currentProgress,
                targetProgress,
                ref velocity,
                smoothTime
            );

            UpdateVisuals(currentProgress);
        }
    }

    /// <summary>
    /// Установить целевой прогресс (от 0 до 1)
    /// </summary>
    /// <param name="progress">Прогресс от 0 до 1</param>
    public void SetProgress(float progress)
    {
        targetProgress = Mathf.Clamp01(progress);
    }

    /// <summary>
    /// Установить прогресс немедленно (без анимации)
    /// </summary>
    /// <param name="progress">Прогресс от 0 до 1</param>
    public void SetProgressImmediate(float progress)
    {
        targetProgress = currentProgress = Mathf.Clamp01(progress);
        velocity = 0f;
        UpdateVisuals(currentProgress);
    }

    /// <summary>
    /// Обновить все визуальные элементы
    /// </summary>
    /// <param name="progress">Текущий прогресс</param>
    private void UpdateVisuals(float progress)
    {
        // 1. Обновляем ширину заполнения
        if (fillRect != null)
        {
            float newWidth = progress * maxWidth;
            fillRect.sizeDelta = new Vector2(newWidth, fillRect.sizeDelta.y);
        }

        // 2. Обновляем цвет заполнения (градиент от красного к зеленому)
        if (fillImage != null)
        {
            if (progress < 0.5f)
            {
                // От красного к желтому (0% - 50%)
                float t = progress * 2f; // Преобразуем 0-0.5 в 0-1
                fillImage.color = Color.Lerp(startColor, middleColor, t);
            }
            else
            {
                // От желтого к зеленому (50% - 100%)
                float t = (progress - 0.5f) * 2f; // Преобразуем 0.5-1 в 0-1
                fillImage.color = Color.Lerp(middleColor, endColor, t);
            }
        }

        // 3. Обновляем текст процентов
        if (percentText != null)
        {
            int percent = Mathf.RoundToInt(progress * 100);
            percentText.text = $"{percent}%";

            // Легкая анимация масштаба при изменении процентов
            if (Mathf.Abs(progress - targetProgress) < 0.01f)
            {
                percentText.transform.localScale = Vector3.one;
            }
            else
            {
                percentText.transform.localScale = Vector3.one * 1.05f;
            }
        }

        // 4. Обновляем текст статуса
        if (statusText != null)
        {
            string status = GetStatusMessage(progress);
            if (statusText.text != status)
            {
                statusText.text = status;
            }
        }
    }

    /// <summary>
    /// Получить текстовое сообщение в зависимости от прогресса
    /// </summary>
    private string GetStatusMessage(float progress)
    {
        if (progress < 0.2f) return "Инициализация...";
        if (progress < 0.4f) return "Загрузка данных...";
        if (progress < 0.6f) return "Подготовка ресурсов...";
        if (progress < 0.8f) return "Обработка объектов...";
        if (progress < 0.95f) return "Завершение...";
        return "Готово!";
    }

    /// <summary>
    /// Получить текущий прогресс
    /// </summary>
    /// <returns>Текущий прогресс от 0 до 1</returns>
    public float GetCurrentProgress()
    {
        return currentProgress;
    }

    /// <summary>
    /// Сбросить прогресс-бар в начальное состояние
    /// </summary>
    public void Reset()
    {
        SetProgressImmediate(0f);
    }
}