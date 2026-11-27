using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Управление настройками игры: раздельный ввод чувствительности по X/Y (слайдер + поле ввода)
/// и лимит FPS с живым счётчиком кадров и сохранением в PlayerPrefs.
/// </summary>
public class GameSettings : MonoBehaviour
{
    public const string SensXKey = "MouseSensitivityX";
    public const string SensYKey = "MouseSensitivityY";
    public const string FpsKey = "TargetFPS";

    public const float MinSensitivity = 0.1f;
    public const float MaxSensitivity = 10f;
    public const float DefaultSensitivity = 1.0f; // Базовая чувствительность 1.0
    public const int DefaultFpsIndex = 1; // 0 = 30 FPS, 1 = 60 FPS, 2 = 120 FPS, 3 = 180 FPS, 4 = Unlimited

    [Header("Горизонтальная чувствительность (X)")]
    public Slider horizontalSensSlider;
    public InputField horizontalSensInput;

    [Header("Вертикальная чувствительность (Y)")]
    public Slider verticalSensSlider;
    public InputField verticalSensInput;

    [Header("Лимит FPS")]
    public Dropdown fpsDropdown;
    public Text currentFpsText;

    private static readonly int[] FpsValues = { 30, 60, 120, 180, -1 };

    private float fpsTimer;
    private int fpsFrames;
    private int lastMeasuredFps;
    private bool listenersBound;

    /// <summary>
    /// Применяет сохранённые настройки сразу при старте сцены, даже если панель настроек выключена.
    /// </summary>
    public static void ApplySavedSettings()
    {
        float savedX = Mathf.Clamp(PlayerPrefs.GetFloat(SensXKey, DefaultSensitivity), MinSensitivity, MaxSensitivity);
        float savedY = Mathf.Clamp(PlayerPrefs.GetFloat(SensYKey, DefaultSensitivity), MinSensitivity, MaxSensitivity);
        int savedFpsIndex = Mathf.Clamp(PlayerPrefs.GetInt(FpsKey, DefaultFpsIndex), 0, FpsValues.Length - 1);

        ApplySensitivityToCamera(savedX, savedY);
        ApplyFpsValue(savedFpsIndex);
    }

    private void Awake()
    {
        ApplySavedSettings();
    }

    private void Start()
    {
        BindUI();
    }

    private void OnEnable()
    {
        SyncUIFromPrefs();
        fpsTimer = 0f;
        fpsFrames = 0;
    }

    public void BindUI()
    {
        if (!listenersBound)
        {
            if (horizontalSensSlider != null)
            {
                horizontalSensSlider.minValue = MinSensitivity;
                horizontalSensSlider.maxValue = MaxSensitivity;
                horizontalSensSlider.onValueChanged.AddListener(OnHorizontalSliderChanged);
            }

            if (horizontalSensInput != null)
            {
                horizontalSensInput.contentType = InputField.ContentType.DecimalNumber;
                horizontalSensInput.onEndEdit.AddListener(OnHorizontalInputSubmitted);
            }

            if (verticalSensSlider != null)
            {
                verticalSensSlider.minValue = MinSensitivity;
                verticalSensSlider.maxValue = MaxSensitivity;
                verticalSensSlider.onValueChanged.AddListener(OnVerticalSliderChanged);
            }

            if (verticalSensInput != null)
            {
                verticalSensInput.contentType = InputField.ContentType.DecimalNumber;
                verticalSensInput.onEndEdit.AddListener(OnVerticalInputSubmitted);
            }

            if (fpsDropdown != null)
            {
                fpsDropdown.onValueChanged.AddListener(OnFpsDropdownChanged);
            }

            listenersBound = true;
        }

        if (fpsDropdown != null && fpsDropdown.options.Count != FpsValues.Length)
        {
            fpsDropdown.options = new List<Dropdown.OptionData>
            {
                new Dropdown.OptionData("30 FPS"),
                new Dropdown.OptionData("60 FPS"),
                new Dropdown.OptionData("120 FPS"),
                new Dropdown.OptionData("180 FPS"),
                new Dropdown.OptionData("Unlimited")
            };
        }

        SyncUIFromPrefs();
    }

    private void SyncUIFromPrefs()
    {
        float savedX = Mathf.Clamp(PlayerPrefs.GetFloat(SensXKey, DefaultSensitivity), MinSensitivity, MaxSensitivity);
        float savedY = Mathf.Clamp(PlayerPrefs.GetFloat(SensYKey, DefaultSensitivity), MinSensitivity, MaxSensitivity);
        int savedFpsIndex = Mathf.Clamp(PlayerPrefs.GetInt(FpsKey, DefaultFpsIndex), 0, FpsValues.Length - 1);

        if (horizontalSensSlider != null)
            horizontalSensSlider.SetValueWithoutNotify(savedX);
        if (horizontalSensInput != null)
            horizontalSensInput.SetTextWithoutNotify(FormatSens(savedX));

        if (verticalSensSlider != null)
            verticalSensSlider.SetValueWithoutNotify(savedY);
        if (verticalSensInput != null)
            verticalSensInput.SetTextWithoutNotify(FormatSens(savedY));

        if (fpsDropdown != null)
        {
            fpsDropdown.SetValueWithoutNotify(savedFpsIndex);
            fpsDropdown.RefreshShownValue();
        }

        ApplySensitivityToCamera(savedX, savedY);
        ApplyFpsValue(savedFpsIndex);
        UpdateFpsLabel();
    }

    private void Update()
    {
        // Считаем реальный FPS по unscaledDeltaTime, чтобы счётчик работал даже на паузе
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        fpsTimer += dt;
        fpsFrames++;

        if (fpsTimer >= 0.35f)
        {
            lastMeasuredFps = Mathf.RoundToInt(fpsFrames / fpsTimer);
            fpsTimer = 0f;
            fpsFrames = 0;
            UpdateFpsLabel();
        }
    }

    // --- Горизонтальная чувствительность (X) ---

    public void OnHorizontalSliderChanged(float value)
    {
        float clamped = Mathf.Round(Mathf.Clamp(value, MinSensitivity, MaxSensitivity) * 100f) / 100f;
        if (horizontalSensInput != null)
            horizontalSensInput.SetTextWithoutNotify(FormatSens(clamped));

        SetHorizontalSensitivity(clamped);
    }

    public void OnHorizontalInputSubmitted(string rawText)
    {
        float current = PlayerPrefs.GetFloat(SensXKey, DefaultSensitivity);
        float parsed = ParseSensitivityInput(rawText, current);

        if (horizontalSensSlider != null)
            horizontalSensSlider.SetValueWithoutNotify(parsed);
        if (horizontalSensInput != null)
            horizontalSensInput.SetTextWithoutNotify(FormatSens(parsed));

        SetHorizontalSensitivity(parsed);
    }

    private void SetHorizontalSensitivity(float value)
    {
        PlayerPrefs.SetFloat(SensXKey, value);
        PlayerPrefs.Save();

        OrbitCamera cam = FindAnyObjectByType<OrbitCamera>(FindObjectsInactive.Include);
        if (cam != null)
            cam.SetSensitivityX(value);
    }

    // --- Вертикальная чувствительность (Y) ---

    public void OnVerticalSliderChanged(float value)
    {
        float clamped = Mathf.Round(Mathf.Clamp(value, MinSensitivity, MaxSensitivity) * 100f) / 100f;
        if (verticalSensInput != null)
            verticalSensInput.SetTextWithoutNotify(FormatSens(clamped));

        SetVerticalSensitivity(clamped);
    }

    public void OnVerticalInputSubmitted(string rawText)
    {
        float current = PlayerPrefs.GetFloat(SensYKey, DefaultSensitivity);
        float parsed = ParseSensitivityInput(rawText, current);

        if (verticalSensSlider != null)
            verticalSensSlider.SetValueWithoutNotify(parsed);
        if (verticalSensInput != null)
            verticalSensInput.SetTextWithoutNotify(FormatSens(parsed));

        SetVerticalSensitivity(parsed);
    }

    private void SetVerticalSensitivity(float value)
    {
        PlayerPrefs.SetFloat(SensYKey, value);
        PlayerPrefs.Save();

        OrbitCamera cam = FindAnyObjectByType<OrbitCamera>(FindObjectsInactive.Include);
        if (cam != null)
            cam.SetSensitivityY(value);
    }

    // --- Лимит FPS ---

    public void OnFpsDropdownChanged(int index)
    {
        int clampedIndex = Mathf.Clamp(index, 0, FpsValues.Length - 1);
        ApplyFpsValue(clampedIndex);
        PlayerPrefs.SetInt(FpsKey, clampedIndex);
        PlayerPrefs.Save();
        UpdateFpsLabel();
    }

    public void ResetToDefaults()
    {
        PlayerPrefs.SetFloat(SensXKey, DefaultSensitivity);
        PlayerPrefs.SetFloat(SensYKey, DefaultSensitivity);
        PlayerPrefs.SetInt(FpsKey, DefaultFpsIndex);
        PlayerPrefs.Save();

        SyncUIFromPrefs();
    }

    private static void ApplySensitivityToCamera(float sensX, float sensY)
    {
        OrbitCamera cam = FindAnyObjectByType<OrbitCamera>(FindObjectsInactive.Include);
        if (cam != null)
        {
            cam.SetSensitivityX(sensX);
            cam.SetSensitivityY(sensY);
        }
    }

    private static void ApplyFpsValue(int index)
    {
        if (index < 0 || index >= FpsValues.Length) return;

        int targetFps = FpsValues[index];
        QualitySettings.vSyncCount = 0; // Отключаем VSync, иначе Unity игнорирует targetFrameRate
        Application.targetFrameRate = targetFps;
    }

    private void UpdateFpsLabel()
    {
        if (currentFpsText == null) return;

        int target = Application.targetFrameRate;
        string targetStr = target <= 0 ? "Unlimited" : target + " FPS";
        string measuredStr = lastMeasuredFps > 0 ? lastMeasuredFps + " FPS" : "...";
        currentFpsText.text = $"Current: {measuredStr}  |  Target: {targetStr}";
    }

    private static float ParseSensitivityInput(string raw, float fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        string normalized = raw.Trim().Replace(',', '.');
        if (float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
        {
            return Mathf.Round(Mathf.Clamp(val, MinSensitivity, MaxSensitivity) * 100f) / 100f;
        }

        return fallback;
    }

    private static string FormatSens(float value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}