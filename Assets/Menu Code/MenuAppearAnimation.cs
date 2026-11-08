using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Быстрое каскадное появление главного меню:
/// элементы всплывают лёгкой волной почти незаметно для игрока.
/// </summary>
public class MenuAppearAnimation : MonoBehaviour
{
    [Header("Тайминги (секунды)")]
    [Tooltip("Пауза перед началом анимации")]
    public float startDelay = 0.05f;
    [Tooltip("Задержка между соседними элементами")]
    public float stagger = 0.03f;
    [Tooltip("Длительность анимации одного элемента")]
    public float elementDuration = 0.18f;

    [Header("Движение")]
    [Tooltip("Высота всплытия элемента, пиксели")]
    public float riseOffset = 12f;
    [Tooltip("Резкость кривой easing (2-3 выглядит живо и быстро)")]
    public float easingPower = 2.5f;

    [Header("Фон")]
    [Tooltip("Медленный дрейф фонового изображения (по умолчанию выключен)")]
    public bool backgroundDrift = false;
    [Tooltip("Амплитуда дрейфа фона")]
    public float driftAmount = 0.06f;
    [Tooltip("Период дрейфа фона, секунды")]
    public float driftPeriod = 18f;

    private readonly List<CanvasGroup> _groups = new List<CanvasGroup>();
    private RectTransform _background;
    private Vector2 _backgroundBasePos;

    private void Start()
    {
        CollectTargets();
        StartCoroutine(PlayCascade());
    }

    private void CollectTargets()
    {
        // заголовок "Text (TMP)" на канвасе
        AddTarget(FindByName("Text (TMP)", "Canvas"));

        // дети панели меню: фон, кнопки, версия
        GameObject panel = FindByName("MainMenuPanel");
        if (panel != null)
        {
            foreach (Transform child in panel.transform)
                AddTarget(child.gameObject);
        }

        // фон для медленного дрейфа (опционально)
        GameObject bg = FindByName("Image");
        if (bg != null && bg.transform.parent != null && bg.transform.parent.name == "MainMenuPanel")
        {
            _background = bg.transform as RectTransform;
            if (_background != null)
                _backgroundBasePos = _background.anchoredPosition;
        }
    }

    private static GameObject FindByName(string objectName, string parentName = null)
    {
        RectTransform fallback = null;
        foreach (RectTransform rt in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rt.name != objectName)
                continue;
            if (parentName == null)
                return rt.gameObject;
            if (fallback == null)
                fallback = rt;
            if (rt.parent != null && rt.parent.name == parentName)
                return rt.gameObject;
        }
        return fallback != null ? fallback.gameObject : null;
    }

    private void AddTarget(GameObject go)
    {
        if (go == null)
            return;
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        _groups.Add(cg);
    }

    private IEnumerator PlayCascade()
    {
        if (_groups.Count == 0)
            yield break;
        yield return new WaitForSecondsRealtime(startDelay);
        for (int i = 0; i < _groups.Count; i++)
        {
            StartCoroutine(ShowElement(_groups[i]));
            yield return new WaitForSecondsRealtime(stagger);
        }
    }

    private IEnumerator ShowElement(CanvasGroup cg)
    {
        RectTransform rt = cg.GetComponent<RectTransform>();
        Vector2 basePos = rt != null ? rt.anchoredPosition : Vector2.zero;
        Vector2 startPos = basePos + new Vector2(0f, -riseOffset);
        float t = 0f;
        while (t < elementDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, elementDuration));
            float eased = 1f - Mathf.Pow(1f - k, easingPower);
            cg.alpha = eased;
            if (rt != null)
                rt.anchoredPosition = Vector2.LerpUnclamped(startPos, basePos, eased);
            yield return null;
        }
        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;
        if (rt != null)
            rt.anchoredPosition = basePos;
    }

    private void Update()
    {
        if (!backgroundDrift || _background == null)
            return;
        float phase = Mathf.PI * 2f * (Time.unscaledTime / Mathf.Max(0.1f, driftPeriod));
        Vector2 offset = new Vector2(Mathf.Sin(phase) * driftAmount * 100f, Mathf.Cos(phase * 0.7f) * driftAmount * 60f);
        _background.anchoredPosition = _backgroundBasePos + offset;
    }
}