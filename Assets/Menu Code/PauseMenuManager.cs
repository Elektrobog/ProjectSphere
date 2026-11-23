using System;
using System.Collections.Generic;
using System.Reflection;
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
/// Стилизованное меню паузы с полной автогенерацией элементов и настройкой из инспектора.
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

    [Tooltip("Необязательно: панель настроек (для кнопки Settings).")]
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
    [Tooltip("Необязательный подзаголовок под надписью PAUSED (оставьте пустым, чтобы скрыть).")]
    public string subtitleText = "GAME PAUSED";
    [Range(20, 72)] public int titleFontSize = 38;
    [Range(10, 32)] public int subtitleFontSize = 13;
    public Color titleColor = new Color(0.92f, 0.97f, 1f, 1f);
    public Color subtitleColor = new Color(0.12f, 0.75f, 1f, 0.85f);

    [Header("Задник и карточка меню (Sci-Fi стиль)")]
    [Tooltip("Автоматически обновлять внешний вид меню в редакторе при изменении параметров в инспекторе.")]
    public bool livePreviewInEditor = true;

    [Tooltip("Цвет полноэкранного затемнения позади меню.")]
    public Color backdropColor = new Color(0.02f, 0.04f, 0.07f, 0.78f);

    [Tooltip("Цвет центральной карточки меню.")]
    public Color windowColor = new Color(0.08f, 0.12f, 0.17f, 0.96f);

    [Tooltip("Акцентный неоновый цвет (рамка, верхняя полоса, разделитель, индикаторы кнопок).")]
    public Color accentColor = new Color(0.12f, 0.75f, 1f, 0.9f);

    [Min(260f)] public float windowWidth = 420f;
    [Min(12f)] public float windowPadding = 32f;
    [Min(0f)] public float borderThickness = 2f;
    public bool showTopAccentBar = true;
    public bool showDivider = true;
    public float verticalOffset = 0f;

    [Header("Стиль и размеры кнопок")]
    [Tooltip("Применять единый Sci-Fi стиль к кнопкам (если выключить — сохранится дизайн вашей шаблонной кнопки).")]
    public bool applyButtonStyle = true;
    [Min(160f)] public float buttonWidth = 330f;
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

    private bool _paused;

#if UNITY_EDITOR
    private bool _pendingRebuild;
#endif

    private void Awake()
    {
        if (pausePanel == null)
        {
            Transform found = transform.Find("PausePanel");
            if (found != null)
                pausePanel = found.gameObject;
        }

        if (exitGame == null)
            exitGame = FindFirstObjectByType<ExitGame>(FindObjectsInactive.Include);

        if (respawnManager == null)
            respawnManager = RespawnManager.Instance != null
                ? RespawnManager.Instance
                : FindFirstObjectByType<RespawnManager>(FindObjectsInactive.Include);

        if (pausePanel != null)
        {
            // Если меню ещё не сгенерировано в редакторе — строим его при запуске,
            // иначе просто гарантируем привязку всех событий кнопок.
            if (pausePanel.transform.Find(WindowObjectName) == null)
                GenerateAllMenuButtons(false);
            else
                RebindExistingButtonsRuntime();
        }

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (pausePanel != null)
            pausePanel.SetActive(false);
        else
            Debug.LogError("[PauseMenuManager] Не назначена ссылка Pause Panel в инспекторе!", this);
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
                : FindFirstObjectByType<RespawnManager>(FindObjectsInactive.Include);

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
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
        else
            Debug.LogWarning("[PauseMenuManager] Панель настроек (Settings Panel) не назначена в инспекторе!", this);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
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
    // ГЕНЕРАЦИЯ И СТИЛИЗАЦИЯ МЕНЮ
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
        if (settingsButton.enabled && (settingsPanel != null || !Application.isPlaying)) list.Add(settingsButton);
        if (mainMenuButton.enabled && !string.IsNullOrEmpty(mainMenuSceneName)) list.Add(mainMenuButton);
        if (quitButton.enabled) list.Add(quitButton);

        return list;
    }

    private void RebindExistingButtonsRuntime()
    {
        Transform win = pausePanel.transform.Find(WindowObjectName);
        if (win == null) return;

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
        Transform settingsRoot = settingsPanel != null ? settingsPanel.transform : null;

        // 1. Сохраняем визуальный шаблон из существующей кнопки или текстового компонента (чтобы подхватить шрифт проекта)
        GameObject buttonTemplate = FindOrCreateButtonTemplate(confirmRoot, settingsRoot);
        GameObject textTemplate = FindTextTemplate(pausePanel.transform, confirmRoot, settingsRoot);

        // 2. Удаляем старые сгенерированные элементы и старые кнопки меню
        CleanupOldMenuObjects(confirmRoot, settingsRoot, recordUndo);

        // Если на самом PausePanel висит Image — делаем его прозрачным, чтобы цвет задника задавался ровно одним слоем PauseBackdrop
        if (pausePanel.TryGetComponent(out Image rootPanelImg))
        {
            rootPanelImg.color = new Color(0f, 0f, 0f, 0f);
            rootPanelImg.raycastTarget = false;
        }

        // 3. Создаём полноэкранный задник (PauseBackdrop)
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

        // 4. Рассчитываем высоту центральной карточки меню
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

        // 5. Создаём центральную карточку меню (MenuWindow)
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

        // Верхняя неоновая полоска карточки
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

        // 6. Заголовок PAUSED, подзаголовок и разделитель
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

        // 7. Создаём и стилизуем все кнопки внутри карточки
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

        // Держим окно подтверждения выхода и настройки поверх карточки паузы
        if (confirmRoot != null && confirmRoot.parent == pausePanel.transform)
            confirmRoot.SetAsLastSibling();
        if (settingsRoot != null && settingsRoot.parent == pausePanel.transform)
            settingsRoot.SetAsLastSibling();

        DestroySafe(buttonTemplate);
        if (textTemplate != null)
            DestroySafe(textTemplate);
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
        // Ищем любой существующий текстовый объект (Text или TextMeshProUGUI), чтобы скопировать шрифт проекта
        Component[] allComps = root.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < allComps.Length; i++)
        {
            Component c = allComps[i];
            if (c == null) continue;
            if (confirmRoot != null && (c.transform == confirmRoot || c.transform.IsChildOf(confirmRoot))) continue;
            if (settingsRoot != null && (c.transform == settingsRoot || c.transform.IsChildOf(settingsRoot))) continue;

            if (c is Text || c.GetType().Name == "TextMeshProUGUI")
            {
                GameObject clone = Instantiate(c.gameObject);
                clone.SetActive(false);
                // Удаляем дочерние объекты, если были
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
        if (textTemplate != null)
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

        // Настраиваем стандартный Text
        if (go.TryGetComponent(out Text uiText))
        {
            uiText.text = content;
            uiText.fontSize = fontSize;
            uiText.color = color;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            uiText.raycastTarget = false;
            return go;
        }

        // Настраиваем TextMeshProUGUI через reflection (без жёсткой зависимости)
        Component[] comps = go.GetComponents<Component>();
        for (int i = 0; i < comps.Length; i++)
        {
            if (comps[i] == null) continue;
            Type tp = comps[i].GetType();
            if (tp.Name == "TextMeshProUGUI" || tp.Name == "TMP_Text")
            {
                tp.GetProperty("text")?.SetValue(comps[i], content);
                tp.GetProperty("fontSize")?.SetValue(comps[i], (float)fontSize);
                tp.GetProperty("color")?.SetValue(comps[i], color);
                tp.GetProperty("raycastTarget")?.SetValue(comps[i], false);
                break;
            }
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

        // Также убираем старые одиночные кнопки, лежащие напрямую в PausePanel
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