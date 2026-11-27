using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
#endif

/// <summary>
/// Стилизованное меню паузы с полной автогенерацией элементов, панели настроек
/// (раздельная чувствительность X/Y со слайдером и полем ввода + лимит FPS) и настройкой из инспектора.
/// </summary>
public class PauseMenuManager : MonoBehaviour
{
    [Serializable]
    public class MenuButtonConfig
    {
        public string objectName;
        public string label;
        public bool enabled = true;
        [HideInInspector] public UnityAction runtimeAction;
    }

    [Header("UI Панели")]
    [Tooltip("Главная панель меню паузы.")]
    public GameObject pausePanel;

    [Tooltip("Панель настроек (генерируется или привязывается автоматически).")]
    public GameObject settingsPanel;

    [Tooltip("Необязательно: скрипт подтверждения выхода.")]
    public ExitGame exitGame;

    [Header("Рестарт и сцены")]
    [Tooltip("Ссылка на RespawnManager (если пусто — найдётся автоматически).")]
    public RespawnManager respawnManager;

    public string mainMenuSceneName = "MainMenu";
    public string levelSceneName = "Level 1";

    [Header("Заголовок паузы")]
    public string titleText = "PAUSED";
    [Tooltip("Необязательный подзаголовок под надписью PAUSED.")]
    public string subtitleText = "GAME PAUSED";
    [Range(20, 72)] public int titleFontSize = 38;
    [Range(10, 32)] public int subtitleFontSize = 13;
    public Color titleColor = new Color(0.92f, 0.97f, 1f, 1f);
    public Color subtitleColor = new Color(0.12f, 0.75f, 1f, 0.85f);

    [Header("Задник и карточка меню (Sci-Fi стиль)")]
    public bool livePreviewInEditor = true;
    public Color backdropColor = new Color(0.02f, 0.04f, 0.07f, 0.78f);
    public Color windowColor = new Color(0.08f, 0.12f, 0.17f, 0.96f);
    public Color accentColor = new Color(0.12f, 0.75f, 1f, 0.9f);

    [Min(260f)] public float windowWidth = 440f;
    [Min(12f)] public float windowPadding = 32f;
    [Min(0f)] public float borderThickness = 2f;
    public bool showTopAccentBar = true;
    public bool showDivider = true;
    public float verticalOffset = 0f;

    [Header("Стиль и размеры кнопок")]
    public bool applyButtonStyle = true;
    [Min(160f)] public float buttonWidth = 340f;
    [Min(32f)] public float buttonHeight = 50f;
    [Min(4f)] public float buttonSpacing = 12f;
    [Range(12, 36)] public int buttonFontSize = 21;

    public Color buttonNormalColor = new Color(0.13f, 0.19f, 0.26f, 0.95f);
    public Color buttonHighlightColor = new Color(0.16f, 0.34f, 0.48f, 1f);
    public Color buttonPressedColor = new Color(0.09f, 0.55f, 0.76f, 1f);
    public Color buttonTextColor = new Color(0.93f, 0.97f, 1f, 1f);
    public bool addButtonAccentStrip = true;

    [Header("Набор генерируемых кнопок")]
    public MenuButtonConfig resumeButton = new MenuButtonConfig { objectName = "ResumeButton", label = "Resume", enabled = true };
    public MenuButtonConfig restartCheckpointButton = new MenuButtonConfig { objectName = "RestartButton", label = "Restart", enabled = true };
    public MenuButtonConfig restartLevelButton = new MenuButtonConfig { objectName = "RestartLevelButton", label = "Restart Level", enabled = true };
    public MenuButtonConfig settingsButton = new MenuButtonConfig { objectName = "SettingsButton", label = "Settings", enabled = true };
    public MenuButtonConfig mainMenuButton = new MenuButtonConfig { objectName = "MainMenuButton", label = "Main Menu", enabled = true };
    public MenuButtonConfig quitButton = new MenuButtonConfig { objectName = "QuitButton", label = "Quit", enabled = true };

    private const string BackdropObjectName = "PauseBackdrop";
    private const string WindowObjectName = "MenuWindow";
    private const string SettingsPanelObjectName = "SettingsPanel";

    private bool _paused;

#if UNITY_EDITOR
    private bool _pendingRebuild;
#endif

    private void Awake()
    {
        // Применяем сохранённую чувствительность и FPS сразу при старте уровня
        GameSettings.ApplySavedSettings();

        if (pausePanel == null)
        {
            Transform found = transform.Find("PausePanel");
            if (found != null)
                pausePanel = found.gameObject;
        }

        if (settingsPanel == null && pausePanel != null)
        {
            Transform foundSettings = pausePanel.transform.Find(SettingsPanelObjectName);
            if (foundSettings != null)
                settingsPanel = foundSettings.gameObject;
        }

        if (exitGame == null)
            exitGame = FindAnyObjectByType<ExitGame>(FindObjectsInactive.Include);

        if (respawnManager == null)
            respawnManager = RespawnManager.Instance != null
                ? RespawnManager.Instance
                : FindAnyObjectByType<RespawnManager>(FindObjectsInactive.Include);

        if (pausePanel != null)
        {
            bool needsRebuild = pausePanel.transform.Find(WindowObjectName) == null
                             || settingsPanel == null
                             || settingsPanel.transform.Find(WindowObjectName) == null
                             || settingsPanel.transform.Find(WindowObjectName)?.Find("HorizontalSensInput") == null;

            if (needsRebuild)
                GenerateAllMenuButtons(false);
            else
                RebindExistingButtonsRuntime();
        }

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!livePreviewInEditor || Application.isPlaying || pausePanel == null)
            return;

        if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
            return;

        if (_pendingRebuild)
            return;

        _pendingRebuild = true;
        EditorApplication.delayCall += () =>
        {
            _pendingRebuild = false;
            if (this != null && !Application.isPlaying && pausePanel != null)
            {
                GenerateAllMenuButtons(false);
            }
        };
    }
#endif

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (exitGame != null && exitGame.confirmPanel != null && exitGame.confirmPanel.activeSelf)
                return;

            if (settingsPanel != null && settingsPanel.activeSelf)
            {
                CloseSettings();
                return;
            }

            if (_paused)
                Resume();
            else
                Pause();
        }
    }

    public void Pause()
    {
        if (_paused)
            return;

        _paused = true;
        Time.timeScale = 0f;

        if (pausePanel != null)
            pausePanel.SetActive(true);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        if (!_paused)
            return;

        _paused = false;
        Time.timeScale = 1f;

        if (exitGame != null)
            exitGame.CancelExit();

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (pausePanel != null)
            pausePanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void RestartLevel()
    {
        if (respawnManager == null)
            respawnManager = RespawnManager.Instance != null
                ? RespawnManager.Instance
                : FindAnyObjectByType<RespawnManager>(FindObjectsInactive.Include);

        if (respawnManager != null)
        {
            respawnManager.Respawn();
            Resume();
            return;
        }

        ReloadFullScene();
    }

    public void ReloadFullScene()
    {
        _paused = false;
        Time.timeScale = 1f;
        string scene = string.IsNullOrEmpty(levelSceneName) ? SceneManager.GetActiveScene().name : levelSceneName;
        SceneManager.LoadScene(scene);
    }

    public void OpenSettings()
    {
        if (settingsPanel == null && pausePanel != null)
        {
            Transform found = pausePanel.transform.Find(SettingsPanelObjectName);
            if (found != null)
            {
                settingsPanel = found.gameObject;
            }
            else
            {
                GenerateAllMenuButtons(false);
                found = pausePanel.transform.Find(SettingsPanelObjectName);
                if (found != null) settingsPanel = found.gameObject;
            }
        }

        if (pausePanel != null)
            pausePanel.SetActive(true);

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            if (settingsPanel.TryGetComponent(out GameSettings gs))
                gs.BindUI();
        }
        else
        {
            Debug.LogError("[PauseMenuManager] Не удалось создать или найти панель настроек (Settings Panel)!", this);
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (pausePanel != null)
            pausePanel.SetActive(true);
    }

    public void LoadMainMenu()
    {
        _paused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void RequestQuit()
    {
        if (exitGame != null && exitGame.confirmPanel != null)
        {
            exitGame.confirmPanel.SetActive(true);
            return;
        }

        QuitGame();
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // =====================================================================
    // ГЕНЕРАЦИЯ И СТИЛИЗАЦИЯ МЕНЮ И НАСТРОЕК
    // =====================================================================

    private List<MenuButtonConfig> GetOrderedConfigs()
    {
        resumeButton.runtimeAction = Resume;
        restartCheckpointButton.runtimeAction = RestartLevel;
        restartLevelButton.runtimeAction = ReloadFullScene;
        settingsButton.runtimeAction = OpenSettings;
        mainMenuButton.runtimeAction = LoadMainMenu;
        quitButton.runtimeAction = RequestQuit;

        var list = new List<MenuButtonConfig>(6);
        if (resumeButton.enabled) list.Add(resumeButton);
        if (restartCheckpointButton.enabled) list.Add(restartCheckpointButton);
        if (restartLevelButton.enabled) list.Add(restartLevelButton);
        if (settingsButton.enabled) list.Add(settingsButton);
        if (mainMenuButton.enabled && !string.IsNullOrEmpty(mainMenuSceneName)) list.Add(mainMenuButton);
        if (quitButton.enabled) list.Add(quitButton);

        return list;
    }

    private void RebindExistingButtonsRuntime()
    {
        Transform win = pausePanel.transform.Find(WindowObjectName);
        if (win != null)
        {
            List<MenuButtonConfig> configs = GetOrderedConfigs();
            for (int i = 0; i < configs.Count; i++)
            {
                Transform btnTr = win.Find(configs[i].objectName);
                if (btnTr != null && btnTr.TryGetComponent(out Button btn))
                {
                    btn.onClick.RemoveAllListeners();
                    if (configs[i].runtimeAction != null)
                        btn.onClick.AddListener(configs[i].runtimeAction);
                }
            }
        }

        if (settingsPanel != null)
        {
            Transform settingsWin = settingsPanel.transform.Find(WindowObjectName);
            if (settingsWin != null)
            {
                Transform backBtnTr = settingsWin.Find("BackButton");
                if (backBtnTr != null && backBtnTr.TryGetComponent(out Button backBtn))
                {
                    backBtn.onClick.RemoveAllListeners();
                    backBtn.onClick.AddListener(CloseSettings);
                }

                Transform resetBtnTr = settingsWin.Find("ResetButton");
                if (resetBtnTr != null && resetBtnTr.TryGetComponent(out Button resetBtn) &&
                    settingsPanel.TryGetComponent(out GameSettings gs))
                {
                    resetBtn.onClick.RemoveAllListeners();
                    resetBtn.onClick.AddListener(gs.ResetToDefaults);
                }
            }
        }
    }

    [ContextMenu("Сгенерировать / обновить стилизованное меню")]
    public void GenerateAllButtonsInEditor()
    {
        GenerateAllMenuButtons(true);
    }

    public void GenerateAllMenuButtons(bool recordUndo)
    {
        if (pausePanel == null)
        {
            Transform found = transform.Find("PausePanel");
            if (found != null) pausePanel = found.gameObject;
        }

        if (pausePanel == null)
        {
            Debug.LogError("[PauseMenuManager] Не назначен Pause Panel — генерация невозможна.", this);
            return;
        }

        List<MenuButtonConfig> configs = GetOrderedConfigs();
        if (configs.Count == 0)
            return;

        Transform confirmRoot = (exitGame != null && exitGame.confirmPanel != null) ? exitGame.confirmPanel.transform : null;

        if (settingsPanel == null)
        {
            Transform foundSettings = pausePanel.transform.Find(SettingsPanelObjectName);
            if (foundSettings != null) settingsPanel = foundSettings.gameObject;
            else
            {
                GameObject spGo = CreateUINode(SettingsPanelObjectName, pausePanel.transform, recordUndo);
                RectTransform spRt = spGo.GetComponent<RectTransform>();
                spRt.anchorMin = Vector2.zero;
                spRt.anchorMax = Vector2.one;
                spRt.offsetMin = Vector2.zero;
                spRt.offsetMax = Vector2.zero;
                settingsPanel = spGo;
            }
        }

        Transform settingsRoot = settingsPanel.transform;

        GameObject buttonTemplate = FindOrCreateButtonTemplate(confirmRoot, settingsRoot);
        GameObject textTemplate = FindTextTemplate(pausePanel.transform, confirmRoot, settingsRoot);

        CleanupOldMenuObjects(confirmRoot, settingsRoot, recordUndo);

        if (pausePanel.TryGetComponent(out Image rootPanelImg))
        {
            rootPanelImg.color = new Color(0f, 0f, 0f, 0f);
            rootPanelImg.raycastTarget = false;
        }

        GameObject backdropGo = CreateUINode(BackdropObjectName, pausePanel.transform, recordUndo);
        backdropGo.transform.SetSiblingIndex(0);
        RectTransform backdropRt = backdropGo.GetComponent<RectTransform>();
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.offsetMin = Vector2.zero;
        backdropRt.offsetMax = Vector2.zero;

        Image backdropImg = backdropGo.AddComponent<Image>();
        backdropImg.color = backdropColor;
        backdropImg.raycastTarget = true;

        bool hasTitle = !string.IsNullOrWhiteSpace(titleText);
        bool hasSubtitle = !string.IsNullOrWhiteSpace(subtitleText);

        float titleHeight = hasTitle ? (titleFontSize + 10f) : 0f;
        float subtitleHeight = hasSubtitle ? (subtitleFontSize + 8f) : 0f;
        float headerBlockHeight = 0f;

        if (hasTitle) headerBlockHeight += titleHeight;
        if (hasSubtitle) headerBlockHeight += (hasTitle ? 2f : 0f) + subtitleHeight;
        if (hasTitle || hasSubtitle)
            headerBlockHeight += showDivider ? 26f : 18f;

        float buttonsBlockHeight = configs.Count * buttonHeight + Mathf.Max(0, configs.Count - 1) * buttonSpacing;
        float totalWindowHeight = windowPadding * 2f + headerBlockHeight + buttonsBlockHeight;

        GameObject windowGo = CreateUINode(WindowObjectName, pausePanel.transform, recordUndo);
        windowGo.transform.SetSiblingIndex(1);
        RectTransform windowRt = windowGo.GetComponent<RectTransform>();
        windowRt.anchorMin = new Vector2(0.5f, 0.5f);
        windowRt.anchorMax = new Vector2(0.5f, 0.5f);
        windowRt.pivot = new Vector2(0.5f, 0.5f);
        windowRt.sizeDelta = new Vector2(windowWidth, totalWindowHeight);
        windowRt.anchoredPosition = new Vector2(0f, verticalOffset);

        Image windowImg = windowGo.AddComponent<Image>();
        windowImg.color = windowColor;

        if (borderThickness > 0.05f)
        {
            Outline outline = windowGo.AddComponent<Outline>();
            outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, accentColor.a * 0.65f);
            outline.effectDistance = new Vector2(borderThickness, -borderThickness);
        }

        if (showTopAccentBar)
        {
            GameObject topBar = CreateUINode("TopAccentBar", windowGo.transform, recordUndo);
            RectTransform barRt = topBar.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0f, 1f);
            barRt.anchorMax = new Vector2(1f, 1f);
            barRt.pivot = new Vector2(0.5f, 1f);
            barRt.sizeDelta = new Vector2(0f, 4f);
            barRt.anchoredPosition = Vector2.zero;

            Image barImg = topBar.AddComponent<Image>();
            barImg.color = accentColor;
            barImg.raycastTarget = false;
        }

        float cursorY = totalWindowHeight * 0.5f - windowPadding;
        float contentWidth = windowWidth - windowPadding * 2f;

        if (hasTitle)
        {
            GameObject titleGo = CreateStyledText("PauseTitle", windowGo.transform, textTemplate, titleText, titleFontSize, titleColor, true, recordUndo);
            RectTransform trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(contentWidth, titleHeight);
            trt.anchoredPosition = new Vector2(0f, cursorY - titleHeight * 0.5f);
            cursorY -= titleHeight;
        }

        if (hasSubtitle)
        {
            if (hasTitle) cursorY -= 2f;
            GameObject subGo = CreateStyledText("PauseSubtitle", windowGo.transform, textTemplate, subtitleText, subtitleFontSize, subtitleColor, false, recordUndo);
            RectTransform srt = subGo.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(contentWidth, subtitleHeight);
            srt.anchoredPosition = new Vector2(0f, cursorY - subtitleHeight * 0.5f);
            cursorY -= subtitleHeight;
        }

        if (hasTitle || hasSubtitle)
        {
            if (showDivider)
            {
                cursorY -= 12f;
                GameObject divider = CreateUINode("HeaderDivider", windowGo.transform, recordUndo);
                RectTransform drt = divider.GetComponent<RectTransform>();
                drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(0.5f, 0.5f);
                drt.sizeDelta = new Vector2(Mathf.Min(contentWidth, buttonWidth), 2f);
                drt.anchoredPosition = new Vector2(0f, cursorY);

                Image dimg = divider.AddComponent<Image>();
                dimg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.35f);
                dimg.raycastTarget = false;
                cursorY -= 14f;
            }
            else
            {
                cursorY -= 18f;
            }
        }

        float finalBtnWidth = Mathf.Min(buttonWidth, contentWidth);
        for (int i = 0; i < configs.Count; i++)
        {
            MenuButtonConfig cfg = configs[i];
            GameObject btnObj = Instantiate(buttonTemplate, windowGo.transform);
            btnObj.name = cfg.objectName;
            btnObj.SetActive(true);

#if UNITY_EDITOR
            if (recordUndo && !Application.isPlaying)
                Undo.RegisterCreatedObjectUndo(btnObj, "Создать кнопку " + cfg.label);
#endif

            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(finalBtnWidth, buttonHeight);
            rt.anchoredPosition = new Vector2(0f, cursorY - buttonHeight * 0.5f);
            cursorY -= (buttonHeight + buttonSpacing);

            if (applyButtonStyle)
                StyleButtonVisuals(btnObj, recordUndo);

            SetButtonLabel(btnObj, cfg.label, applyButtonStyle ? buttonFontSize : -1, applyButtonStyle ? buttonTextColor : (Color?)null);

            Button btn = btnObj.GetComponent<Button>();
            if (btn != null)
                BindButtonAction(btn, cfg.runtimeAction, recordUndo);
        }

        BuildSettingsPanel(settingsPanel, buttonTemplate, textTemplate, recordUndo);

        if (confirmRoot != null && confirmRoot.parent == pausePanel.transform)
            confirmRoot.SetAsLastSibling();
        if (settingsPanel != null)
            settingsPanel.transform.SetAsLastSibling();

        DestroySafe(buttonTemplate);
        if (textTemplate != null)
            DestroySafe(textTemplate);
    }

    private void BuildSettingsPanel(GameObject panelGo, GameObject buttonTemplate, GameObject textTemplate, bool recordUndo)
    {
        for (int i = panelGo.transform.childCount - 1; i >= 0; i--)
        {
            DestroySafe(panelGo.transform.GetChild(i).gameObject);
        }

        GameObject bgGo = CreateUINode("SettingsBackdrop", panelGo.transform, recordUndo);
        RectTransform bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bgGo.AddComponent<Image>();
        bgImg.color = backdropColor;
        bgImg.raycastTarget = true;

        float settingsHeight = 465f;
        GameObject winGo = CreateUINode(WindowObjectName, panelGo.transform, recordUndo);
        RectTransform winRt = winGo.GetComponent<RectTransform>();
        winRt.anchorMin = winRt.anchorMax = winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = new Vector2(windowWidth, settingsHeight);
        winRt.anchoredPosition = new Vector2(0f, verticalOffset);

        Image winImg = winGo.AddComponent<Image>();
        winImg.color = windowColor;

        if (borderThickness > 0.05f)
        {
            Outline outline = winGo.AddComponent<Outline>();
            outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, accentColor.a * 0.65f);
            outline.effectDistance = new Vector2(borderThickness, -borderThickness);
        }

        if (showTopAccentBar)
        {
            GameObject topBar = CreateUINode("TopAccentBar", winGo.transform, recordUndo);
            RectTransform barRt = topBar.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0f, 1f);
            barRt.anchorMax = new Vector2(1f, 1f);
            barRt.pivot = new Vector2(0.5f, 1f);
            barRt.sizeDelta = new Vector2(0f, 4f);
            barRt.anchoredPosition = Vector2.zero;
            Image barImg = topBar.AddComponent<Image>();
            barImg.color = accentColor;
            barImg.raycastTarget = false;
        }

        float curY = settingsHeight * 0.5f - windowPadding;
        float contentW = windowWidth - windowPadding * 2f;

        // Заголовок SETTINGS
        GameObject titleGo = CreateStyledText("SettingsTitle", winGo.transform, textTemplate, "SETTINGS", titleFontSize, titleColor, true, recordUndo);
        RectTransform trt = titleGo.GetComponent<RectTransform>();
        trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
        trt.sizeDelta = new Vector2(contentW, 40f);
        trt.anchoredPosition = new Vector2(0f, curY - 18f);
        curY -= 48f;

        // 1. Горизонтальная чувствительность (Slider + InputField)
        CreateSensitivityRow(
            winGo.transform,
            textTemplate,
            "HorizontalSens",
            "Horizontal Sensitivity (X)",
            contentW,
            ref curY,
            recordUndo,
            out Slider horizSlider,
            out InputField horizInput);

        // 2. Вертикальная чувствительность (Slider + InputField)
        CreateSensitivityRow(
            winGo.transform,
            textTemplate,
            "VerticalSens",
            "Vertical Sensitivity (Y)",
            contentW,
            ref curY,
            recordUndo,
            out Slider vertSlider,
            out InputField vertInput);

        // 3. Лимит FPS
        GameObject fpsLabelGo = CreateStyledText("FpsLabel", winGo.transform, textTemplate, "Frame Rate Limit (FPS)", 15, buttonTextColor, false, recordUndo);
        RectTransform fpsLRt = fpsLabelGo.GetComponent<RectTransform>();
        fpsLRt.anchorMin = fpsLRt.anchorMax = fpsLRt.pivot = new Vector2(0.5f, 0.5f);
        fpsLRt.sizeDelta = new Vector2(contentW, 20f);
        fpsLRt.anchoredPosition = new Vector2(0f, curY);
        if (fpsLabelGo.TryGetComponent(out Text fpsLblTxt)) fpsLblTxt.alignment = TextAnchor.MiddleLeft;
        curY -= 26f;

        Dropdown dropdown = CreateFpsDropdown(winGo.transform, textTemplate, contentW, curY, recordUndo);
        curY -= 28f;

        // Живой счётчик текущего FPS для проверки на любом ПК
        GameObject liveFpsGo = CreateStyledText("CurrentFpsText", winGo.transform, textTemplate, "Current: -- FPS", 13, subtitleColor, false, recordUndo);
        RectTransform lfRt = liveFpsGo.GetComponent<RectTransform>();
        lfRt.anchorMin = lfRt.anchorMax = lfRt.pivot = new Vector2(0.5f, 0.5f);
        lfRt.sizeDelta = new Vector2(contentW, 18f);
        lfRt.anchoredPosition = new Vector2(0f, curY);
        if (liveFpsGo.TryGetComponent(out Text liveFpsTxt)) liveFpsTxt.alignment = TextAnchor.MiddleLeft;
        curY -= 38f;

        GameSettings settingsComp = panelGo.GetComponent<GameSettings>();
        if (settingsComp == null)
            settingsComp = panelGo.AddComponent<GameSettings>();

        settingsComp.horizontalSensSlider = horizSlider;
        settingsComp.horizontalSensInput = horizInput;
        settingsComp.verticalSensSlider = vertSlider;
        settingsComp.verticalSensInput = vertInput;
        settingsComp.fpsDropdown = dropdown;
        settingsComp.currentFpsText = liveFpsTxt;

        // Нижний ряд кнопок: Reset и Back
        float gap = 12f;
        float halfBtnW = (contentW - gap) * 0.5f;
        float btnY = curY - buttonHeight * 0.5f;

        GameObject resetBtnObj = Instantiate(buttonTemplate, winGo.transform);
        resetBtnObj.name = "ResetButton";
        resetBtnObj.SetActive(true);
        RectTransform rRt = resetBtnObj.GetComponent<RectTransform>();
        rRt.anchorMin = rRt.anchorMax = rRt.pivot = new Vector2(0.5f, 0.5f);
        rRt.sizeDelta = new Vector2(halfBtnW, buttonHeight);
        rRt.anchoredPosition = new Vector2(-(halfBtnW * 0.5f + gap * 0.5f), btnY);

        if (applyButtonStyle) StyleButtonVisuals(resetBtnObj, recordUndo);
        SetButtonLabel(resetBtnObj, "Reset", applyButtonStyle ? Mathf.Max(15, buttonFontSize - 2) : -1, applyButtonStyle ? buttonTextColor : (Color?)null);

        Button resetBtn = resetBtnObj.GetComponent<Button>();
        if (resetBtn != null)
            BindButtonAction(resetBtn, settingsComp.ResetToDefaults, recordUndo);

        GameObject backBtnObj = Instantiate(buttonTemplate, winGo.transform);
        backBtnObj.name = "BackButton";
        backBtnObj.SetActive(true);
        RectTransform bRt = backBtnObj.GetComponent<RectTransform>();
        bRt.anchorMin = bRt.anchorMax = bRt.pivot = new Vector2(0.5f, 0.5f);
        bRt.sizeDelta = new Vector2(halfBtnW, buttonHeight);
        bRt.anchoredPosition = new Vector2(halfBtnW * 0.5f + gap * 0.5f, btnY);

        if (applyButtonStyle) StyleButtonVisuals(backBtnObj, recordUndo);
        SetButtonLabel(backBtnObj, "Back", applyButtonStyle ? Mathf.Max(15, buttonFontSize - 2) : -1, applyButtonStyle ? buttonTextColor : (Color?)null);

        Button backBtn = backBtnObj.GetComponent<Button>();
        if (backBtn != null)
            BindButtonAction(backBtn, CloseSettings, recordUndo);
    }

    private void CreateSensitivityRow(
        Transform parent,
        GameObject textTemplate,
        string prefix,
        string title,
        float contentW,
        ref float curY,
        bool recordUndo,
        out Slider slider,
        out InputField inputField)
    {
        GameObject labelGo = CreateStyledText(prefix + "Label", parent, textTemplate, title, 15, buttonTextColor, false, recordUndo);
        RectTransform lRt = labelGo.GetComponent<RectTransform>();
        lRt.anchorMin = lRt.anchorMax = lRt.pivot = new Vector2(0.5f, 0.5f);
        lRt.sizeDelta = new Vector2(contentW, 20f);
        lRt.anchoredPosition = new Vector2(0f, curY);
        if (labelGo.TryGetComponent(out Text lblText)) lblText.alignment = TextAnchor.MiddleLeft;
        curY -= 26f;

        float inputWidth = 76f;
        float spacing = 12f;
        float sliderWidth = Mathf.Max(120f, contentW - inputWidth - spacing);

        // Слайдер слева
        GameObject sliderGo = new GameObject(prefix + "Slider", typeof(RectTransform), typeof(Slider));
        sliderGo.transform.SetParent(parent, false);
        RectTransform sRt = sliderGo.GetComponent<RectTransform>();
        sRt.anchorMin = sRt.anchorMax = sRt.pivot = new Vector2(0.5f, 0.5f);
        sRt.sizeDelta = new Vector2(sliderWidth, 30f);
        sRt.anchoredPosition = new Vector2(-(inputWidth + spacing) * 0.5f, curY);

        slider = sliderGo.GetComponent<Slider>();

        GameObject bgSlider = CreateUINode("Background", sliderGo.transform, recordUndo);
        RectTransform bgSRt = bgSlider.GetComponent<RectTransform>();
        bgSRt.anchorMin = new Vector2(0f, 0.28f);
        bgSRt.anchorMax = new Vector2(1f, 0.72f);
        bgSRt.offsetMin = bgSRt.offsetMax = Vector2.zero;
        Image bgSImg = bgSlider.AddComponent<Image>();
        bgSImg.color = new Color(0.15f, 0.22f, 0.3f, 1f);

        GameObject fillArea = CreateUINode("Fill Area", sliderGo.transform, recordUndo);
        RectTransform faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = new Vector2(0f, 0.28f);
        faRt.anchorMax = new Vector2(1f, 0.72f);
        faRt.offsetMin = faRt.offsetMax = Vector2.zero;

        GameObject fill = CreateUINode("Fill", fillArea.transform, recordUndo);
        RectTransform fRt = fill.GetComponent<RectTransform>();
        fRt.anchorMin = fRt.anchorMax = fRt.pivot = new Vector2(0f, 0.5f);
        fRt.sizeDelta = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = accentColor;
        slider.fillRect = fRt;

        GameObject handleArea = CreateUINode("Handle Slide Area", sliderGo.transform, recordUndo);
        RectTransform haRt = handleArea.GetComponent<RectTransform>();
        haRt.anchorMin = Vector2.zero;
        haRt.anchorMax = Vector2.one;
        haRt.offsetMin = new Vector2(9f, 0f);
        haRt.offsetMax = new Vector2(-9f, 0f);

        GameObject handle = CreateUINode("Handle", handleArea.transform, recordUndo);
        RectTransform hRt = handle.GetComponent<RectTransform>();
        hRt.sizeDelta = new Vector2(18f, 0f);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;
        slider.handleRect = hRt;

        // Поле ручного ввода справа
        GameObject inputGo = new GameObject(prefix + "Input", typeof(RectTransform), typeof(Image), typeof(InputField));
        inputGo.transform.SetParent(parent, false);
        RectTransform iRt = inputGo.GetComponent<RectTransform>();
        iRt.anchorMin = iRt.anchorMax = iRt.pivot = new Vector2(0.5f, 0.5f);
        iRt.sizeDelta = new Vector2(inputWidth, 32f);
        iRt.anchoredPosition = new Vector2((sliderWidth + spacing) * 0.5f, curY);

        Image inputBg = inputGo.GetComponent<Image>();
        inputBg.color = buttonNormalColor;

        Outline inputOutline = inputGo.AddComponent<Outline>();
        inputOutline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.45f);
        inputOutline.effectDistance = new Vector2(1f, -1f);

        GameObject inputTextGo = CreateStyledText("Text", inputGo.transform, textTemplate, "1", 15, accentColor, true, recordUndo);
        RectTransform itRt = inputTextGo.GetComponent<RectTransform>();
        itRt.anchorMin = Vector2.zero;
        itRt.anchorMax = Vector2.one;
        itRt.offsetMin = new Vector2(6f, 2f);
        itRt.offsetMax = new Vector2(-6f, -2f);
        Text inputTxt = inputTextGo.GetComponent<Text>();
        if (inputTxt != null)
        {
            inputTxt.alignment = TextAnchor.MiddleCenter;
            inputTxt.supportRichText = false;
            inputTxt.raycastTarget = true;
        }

        inputField = inputGo.GetComponent<InputField>();
        inputField.targetGraphic = inputBg;
        inputField.textComponent = inputTxt;
        inputField.contentType = InputField.ContentType.DecimalNumber;
        inputField.characterLimit = 5;
        inputField.text = "1";

        curY -= 40f;
    }

    private Dropdown CreateFpsDropdown(Transform parent, GameObject textTemplate, float contentW, float curY, bool recordUndo)
    {
        GameObject dropdownGo = new GameObject("FpsDropdown", typeof(RectTransform), typeof(Image), typeof(Dropdown));
        dropdownGo.transform.SetParent(parent, false);
        RectTransform ddRt = dropdownGo.GetComponent<RectTransform>();
        ddRt.anchorMin = ddRt.anchorMax = ddRt.pivot = new Vector2(0.5f, 0.5f);
        ddRt.sizeDelta = new Vector2(contentW, 36f);
        ddRt.anchoredPosition = new Vector2(0f, curY);

        Image ddImg = dropdownGo.GetComponent<Image>();
        ddImg.color = buttonNormalColor;

        Outline ddOutline = dropdownGo.AddComponent<Outline>();
        ddOutline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.35f);
        ddOutline.effectDistance = new Vector2(1f, -1f);

        Dropdown dropdown = dropdownGo.GetComponent<Dropdown>();

        GameObject ddLabelGo = CreateStyledText("Label", dropdownGo.transform, textTemplate, "60 FPS", 16, buttonTextColor, false, recordUndo);
        RectTransform ddlRt = ddLabelGo.GetComponent<RectTransform>();
        ddlRt.anchorMin = Vector2.zero;
        ddlRt.anchorMax = Vector2.one;
        ddlRt.offsetMin = new Vector2(12f, 0f);
        ddlRt.offsetMax = new Vector2(-28f, 0f);
        if (ddlRt.TryGetComponent(out Text ddText)) ddText.alignment = TextAnchor.MiddleLeft;
        dropdown.captionText = ddText;

        GameObject arrowGo = CreateStyledText("Arrow", dropdownGo.transform, textTemplate, "▼", 12, accentColor, false, recordUndo);
        RectTransform arrRt = arrowGo.GetComponent<RectTransform>();
        arrRt.anchorMin = new Vector2(1f, 0f);
        arrRt.anchorMax = new Vector2(1f, 1f);
        arrRt.pivot = new Vector2(1f, 0.5f);
        arrRt.sizeDelta = new Vector2(28f, 0f);
        arrRt.anchoredPosition = Vector2.zero;

        // Обязательный шаблон (Template) для Dropdown
        GameObject templateGo = CreateUINode("Template", dropdownGo.transform, recordUndo);
        RectTransform tRt = templateGo.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 0f);
        tRt.anchorMax = new Vector2(1f, 0f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, 2f);
        tRt.sizeDelta = new Vector2(0f, 160f);
        Image tImg = templateGo.AddComponent<Image>();
        tImg.color = buttonNormalColor;
        ScrollRect scrollRect = templateGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        templateGo.SetActive(false);

        GameObject viewportGo = CreateUINode("Viewport", templateGo.transform, recordUndo);
        RectTransform vpRt = viewportGo.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.sizeDelta = Vector2.zero;
        vpRt.offsetMin = new Vector2(4f, 4f);
        vpRt.offsetMax = new Vector2(-4f, -4f);
        viewportGo.AddComponent<Mask>().showMaskGraphic = false;
        viewportGo.AddComponent<Image>();
        scrollRect.viewport = vpRt;

        GameObject contentGo = CreateUINode("Content", viewportGo.transform, recordUndo);
        RectTransform cRt = contentGo.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0f, 1f);
        cRt.anchorMax = new Vector2(1f, 1f);
        cRt.pivot = new Vector2(0.5f, 1f);
        cRt.sizeDelta = new Vector2(0f, 150f);
        VerticalLayoutGroup vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        scrollRect.content = cRt;

        GameObject itemGo = CreateUINode("Item", contentGo.transform, recordUndo);
        RectTransform iRt = itemGo.GetComponent<RectTransform>();
        iRt.sizeDelta = new Vector2(0f, 30f);
        Toggle toggle = itemGo.AddComponent<Toggle>();
        Image itemBg = itemGo.AddComponent<Image>();
        itemBg.color = new Color(0.16f, 0.24f, 0.33f, 1f);
        toggle.targetGraphic = itemBg;
        ColorBlock tcb = toggle.colors;
        tcb.highlightedColor = buttonHighlightColor;
        tcb.pressedColor = buttonPressedColor;
        toggle.colors = tcb;

        GameObject itemLabelGo = CreateStyledText("Item Label", itemGo.transform, textTemplate, "60 FPS", 15, buttonTextColor, false, recordUndo);
        RectTransform ilRt = itemLabelGo.GetComponent<RectTransform>();
        ilRt.anchorMin = Vector2.zero;
        ilRt.anchorMax = Vector2.one;
        ilRt.offsetMin = new Vector2(10f, 0f);
        ilRt.offsetMax = new Vector2(-10f, 0f);
        if (ilRt.TryGetComponent(out Text itemText)) itemText.alignment = TextAnchor.MiddleLeft;
        toggle.graphic = itemBg;

        dropdown.template = tRt;
        dropdown.itemText = itemText;

        return dropdown;
    }

    private void StyleButtonVisuals(GameObject btnObj, bool recordUndo)
    {
        if (btnObj.TryGetComponent(out Image img))
        {
            img.color = Color.white;
        }

        if (btnObj.TryGetComponent(out Button btn))
        {
            btn.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = btn.colors;
            cb.normalColor = buttonNormalColor;
            cb.highlightedColor = buttonHighlightColor;
            cb.pressedColor = buttonPressedColor;
            cb.selectedColor = buttonHighlightColor;
            cb.disabledColor = new Color(buttonNormalColor.r, buttonNormalColor.g, buttonNormalColor.b, 0.4f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
        }

        if (btnObj.GetComponent<Outline>() == null)
        {
            Outline outl = btnObj.AddComponent<Outline>();
            outl.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.28f);
            outl.effectDistance = new Vector2(1f, -1f);
        }

        Transform oldStrip = btnObj.transform.Find("AccentStrip");
        if (oldStrip != null)
            DestroySafe(oldStrip.gameObject);

        if (addButtonAccentStrip)
        {
            GameObject strip = CreateUINode("AccentStrip", btnObj.transform, recordUndo);
            RectTransform srt = strip.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0.15f);
            srt.anchorMax = new Vector2(0f, 0.85f);
            srt.pivot = new Vector2(0f, 0.5f);
            srt.sizeDelta = new Vector2(4f, 0f);
            srt.anchoredPosition = new Vector2(4f, 0f);

            Image simg = strip.AddComponent<Image>();
            simg.color = accentColor;
            simg.raycastTarget = false;
        }
    }

    private GameObject FindOrCreateButtonTemplate(Transform confirmRoot, Transform settingsRoot)
    {
        var allButtons = pausePanel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < allButtons.Length; i++)
        {
            Button b = allButtons[i];
            if (b == null) continue;
            if (confirmRoot != null && (b.transform == confirmRoot || b.transform.IsChildOf(confirmRoot))) continue;
            if (settingsRoot != null && (b.transform == settingsRoot || b.transform.IsChildOf(settingsRoot))) continue;

            GameObject clone = Instantiate(b.gameObject);
            clone.SetActive(false);
            return clone;
        }

        var go = new GameObject("FallbackButtonTemplate", typeof(RectTransform), typeof(Image), typeof(Button));
        go.SetActive(false);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        var txt = textGo.GetComponent<Text>();
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = GetBuiltinFallbackFont();
        txt.color = buttonTextColor;

        return go;
    }

    private GameObject FindTextTemplate(Transform root, Transform confirmRoot, Transform settingsRoot)
    {
        Component[] allComps = root.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < allComps.Length; i++)
        {
            Component c = allComps[i];
            if (c == null) continue;
            if (confirmRoot != null && (c.transform == confirmRoot || c.transform.IsChildOf(confirmRoot))) continue;
            if (settingsRoot != null && (c.transform == settingsRoot || c.transform.IsChildOf(settingsRoot))) continue;

            if (c is Text)
            {
                GameObject clone = Instantiate(c.gameObject);
                clone.SetActive(false);
                for (int ch = clone.transform.childCount - 1; ch >= 0; ch--)
                    DestroySafe(clone.transform.GetChild(ch).gameObject);
                return clone;
            }
        }
        return null;
    }

    private GameObject CreateStyledText(string objName, Transform parent, GameObject textTemplate, string content, int fontSize, Color color, bool bold, bool recordUndo)
    {
        GameObject go;
        if (textTemplate != null && textTemplate.GetComponent<Text>() != null)
        {
            go = Instantiate(textTemplate, parent);
            go.name = objName;
            go.SetActive(true);
#if UNITY_EDITOR
            if (recordUndo && !Application.isPlaying)
                Undo.RegisterCreatedObjectUndo(go, "Создать текст " + objName);
#endif
        }
        else
        {
            go = CreateUINode(objName, parent, recordUndo);
            Text t = go.AddComponent<Text>();
            t.font = GetBuiltinFallbackFont();
            t.alignment = TextAnchor.MiddleCenter;
        }

        if (go.TryGetComponent(out Text uiText))
        {
            uiText.text = content;
            uiText.fontSize = fontSize;
            uiText.color = color;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            uiText.raycastTarget = false;
        }

        return go;
    }

    private void CleanupOldMenuObjects(Transform confirmRoot, Transform settingsRoot, bool recordUndo)
    {
        var toDelete = new List<GameObject>();

        Transform oldBackdrop = pausePanel.transform.Find(BackdropObjectName);
        if (oldBackdrop != null) toDelete.Add(oldBackdrop.gameObject);

        Transform oldWindow = pausePanel.transform.Find(WindowObjectName);
        if (oldWindow != null) toDelete.Add(oldWindow.gameObject);

        var buttons = pausePanel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (b == null) continue;
            if (confirmRoot != null && (b.transform == confirmRoot || b.transform.IsChildOf(confirmRoot))) continue;
            if (settingsRoot != null && (b.transform == settingsRoot || b.transform.IsChildOf(settingsRoot))) continue;
            if (!toDelete.Contains(b.gameObject))
                toDelete.Add(b.gameObject);
        }

        for (int i = 0; i < toDelete.Count; i++)
        {
            if (toDelete[i] == null) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (recordUndo) Undo.DestroyObjectImmediate(toDelete[i]);
                else DestroyImmediate(toDelete[i]);
                continue;
            }
#endif
            Destroy(toDelete[i]);
        }
    }

    private static GameObject CreateUINode(string name, Transform parent, bool recordUndo)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
#if UNITY_EDITOR
        if (recordUndo && !Application.isPlaying)
            Undo.RegisterCreatedObjectUndo(go, "Создать " + name);
#endif
        return go;
    }

    private static void DestroySafe(GameObject go)
    {
        if (go == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(go);
            return;
        }
#endif
        Destroy(go);
    }

    private static Font GetBuiltinFallbackFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    private void BindButtonAction(Button btn, UnityAction action, bool recordUndo)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (recordUndo)
                Undo.RecordObject(btn, "Привязать событие кнопки");

            for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(btn.onClick, i);

            if (action != null)
                UnityEventTools.AddPersistentListener(btn.onClick, action);

            EditorUtility.SetDirty(btn);
            return;
        }
#endif
        btn.onClick.RemoveAllListeners();
        if (action != null)
            btn.onClick.AddListener(action);
    }

    private static void SetButtonLabel(GameObject btnObj, string text, int fontSize, Color? textColor)
    {
        Text legacyText = btnObj.GetComponentInChildren<Text>(true);
        if (legacyText != null)
        {
            legacyText.text = text;
            if (fontSize > 0) legacyText.fontSize = fontSize;
            if (textColor.HasValue) legacyText.color = textColor.Value;
            return;
        }

        Component[] comps = btnObj.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < comps.Length; i++)
        {
            if (comps[i] == null) continue;
            Type type = comps[i].GetType();
            if (type.Name == "TextMeshProUGUI" || type.Name == "TMP_Text")
            {
                type.GetProperty("text")?.SetValue(comps[i], text);
                if (fontSize > 0) type.GetProperty("fontSize")?.SetValue(comps[i], (float)fontSize);
                if (textColor.HasValue) type.GetProperty("color")?.SetValue(comps[i], textColor.Value);
                return;
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(PauseMenuManager))]
public class PauseMenuManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10f);
        var mgr = (PauseMenuManager)target;

        if (GUILayout.Button("Сгенерировать / обновить стилизованное меню", GUILayout.Height(36)))
        {
            mgr.GenerateAllMenuButtons(true);
            EditorUtility.SetDirty(mgr);
            if (mgr.pausePanel != null)
                EditorUtility.SetDirty(mgr.pausePanel);
        }
    }
}
#endif