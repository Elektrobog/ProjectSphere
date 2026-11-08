using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Пассивная полоса прогресса: значения задаёт GameLoader через SetProgress.
/// </summary>
public class SimpleProgressBar : MonoBehaviour
{
    [Header("Ссылки")]
    public RectTransform fillRect;
    public TMP_Text percentText;
    public TMP_Text statusText;
    public Image fillImage;

    [Header("Настройки")]
    public float maxWidth = 600f;
    public float smoothTime = 0.2f;
    public Color startColor = Color.red;
    public Color middleColor = Color.yellow;
    public Color endColor = Color.green;

    /// Задать прогресс (0..1).
    public void SetProgress(float value)
    {
        float v = Mathf.Clamp01(value);
        if (fillRect != null)
            fillRect.sizeDelta = new Vector2(Mathf.Lerp(0f, maxWidth, v), fillRect.sizeDelta.y);
        if (fillImage != null)
        {
            fillImage.fillAmount = v;
            fillImage.color = v < 0.5f
                ? Color.Lerp(startColor, middleColor, v * 2f)
                : Color.Lerp(middleColor, endColor, (v - 0.5f) * 2f);
        }
        if (percentText != null)
            percentText.text = Mathf.RoundToInt(v * 100f) + "%";
    }

    /// Задать статус загрузки.
    public void SetStatus(string text)
    {
        if (statusText != null)
            statusText.text = text;
    }
}