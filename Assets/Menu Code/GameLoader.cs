using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Загрузчик уровня в стиле хороших игр: меню плавно растворяется,
/// появляется экран загрузки, уровень грузится аддитивно, шейдеры прогреваются
/// ДО активации, сцена активируется под экраном загрузки с замороженным временем,
/// и только когда всё прогрето, игрок плавно попадает в уровень без фризов.
/// </summary>
public class GameLoader : MonoBehaviour
{
    [Header("Ссылки на меню")]
    public GameObject mainMenuPanel;
    public GameObject loadingPanel;
    public Image progressFill;
    public RectTransform progressFillRect;
    public TMP_Text percentageText;
    public TMP_Text statusText;
    public Button startButton;

    [Header("Сцена и время")]
    [Tooltip("Имя сцены, которая будет загружена")]
    public string targetScene = "Level 1";
    [Tooltip("Минимальное время показа экрана загрузки")]
    public float minLoadTime = 2f;
    [Tooltip("Пауза после активации уровня: физика, GC, первые кадр")]
    public float postActivationWarmupTime = 0.6f;
    [Tooltip("Максимальная скорость полосы прогресса (0..1 в секунду)")]
    public float maxProgressFillSpeed = 0.7f;

    [Header("Прогрев шейдеров")]
    [Tooltip("Коллекция вариантов шейдеров для прогрева")]
    public ShaderVariantCollection shaderWarmupCollection;
    [Tooltip("Если коллекция не задана, прогреть все шейдеры (медленнее, но надёжнее)")]
    public bool fallbackShaderWarmup = true;
    [Tooltip("Заморозить игровое время, пока уровень не прогрет")]
    public bool freezeGameTimeDuringWarmup = true;
    [Tooltip("Выгрузить неиспользуемые ассеты до старта уровня")]
    public bool unloadUnusedAssetsDuringLoad = true;

    [Header("Плавные переходы")]
    [Tooltip("Плавно растворять экран загрузки в начале уровня")]
    public bool loadingScreenFadeOut = true;
    [Tooltip("Длительность растворения меню, сек")]
    public float menuFadeOutDuration = 0.25f;
    [Tooltip("Длительность появления экрана загрузки, сек")]
    public float loadingFadeInDuration = 0.25f;

    [Header("Полоса прогресса")]
    public Color startColor = Color.red;
    public Color middleColor = Color.yellow;
    public Color endColor = Color.green;
    [Tooltip("Советы, показываемые во время загрузки")]
    public string[] loadingTips;
    public TMP_Text tipText;
    [Tooltip("Растягивать полосу по ширине, а не по fillAmount")]
    public bool useWidthMethod = true;
    public float maxProgressBarWidth = 250f;

    private const float LoadingFadeOutDuration = 0.35f;
    private const float TipInterval = 2.5f;

    private AsyncOperation _loadOp;
    private bool _isLoading;
    private float _displayProgress;
    private float _tipTimer;
    private int _tipIndex;
    private CanvasGroup _menuGroup;
    private CanvasGroup _loadingGroup;

    private void Start()
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(false);
        if (startButton != null)
            startButton.onClick.AddListener(StartGameLoading);
    }

    /// Запуск загрузки (можно повесить на кнопку Start).
    public void StartGameLoading()
    {
        if (_isLoading)
            return;
        StartCoroutine(LoadLevelFlow());
    }

    private IEnumerator LoadLevelFlow()
    {
        _isLoading = true;

        // --- Фаза 0: плавный переход меню -> экран загрузки ---
        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        _menuGroup = GetOrAddCanvasGroup(mainMenuPanel);
        _loadingGroup = GetOrAddCanvasGroup(loadingPanel);

        float t = 0f;
        while (t < menuFadeOutDuration && _menuGroup != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, menuFadeOutDuration));
            _menuGroup.alpha = 1f - k;
            if (_loadingGroup != null && loadingFadeInDuration > 0f)
                _loadingGroup.alpha = Mathf.Clamp01(t / Mathf.Max(0.01f, loadingFadeInDuration));
            yield return null;
        }

        // прячем всё меню, кроме экрана загрузки
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
        HideOtherCanvasChildren();
        if (_loadingGroup != null)
            _loadingGroup.alpha = 1f;

        SetStatus("Подготавливаем уровень...");
        SetProgress(0f, true);
        _tipIndex = -1;
        _tipTimer = TipInterval;

        // --- Фаза 1: асинхронная загрузка сцены до 90% ---
        _loadOp = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Additive);
        if (_loadOp == null)
        {
            Debug.LogError("[GameLoader] Сцена '" + targetScene + "' не найдена. Добавь её в Build Settings (File -> Build Settings).");
            RestoreMenu();
            yield break;
        }
        _loadOp.allowSceneActivation = false;

        float startTime = Time.unscaledTime;
        while (_loadOp.progress < 0.9f)
        {
            SetProgress(Mathf.Clamp01(_loadOp.progress / 0.9f));
            UpdateTips();
            yield return null;
        }
        SetProgress(1f, true);
        SetStatus("Ещё немного...");

        // не прячем экран быстрее minLoadTime
        while (Time.unscaledTime - startTime < minLoadTime)
        {
            UpdateTips();
            yield return null;
        }

        // --- Фаза 2: прогрев шейдеров ДО активации сцены ---
        SetStatus("Подготавливаем графику...");
        yield return null;
        if (shaderWarmupCollection != null)
        {
            shaderWarmupCollection.WarmUp();
        }
        else if (fallbackShaderWarmup)
        {
            Shader.WarmupAllShaders();
        }
        yield return null;

        // --- Фаза 3: активация уровня под замороженным временем ---
        if (freezeGameTimeDuringWarmup)
            Time.timeScale = 0f;
        _loadOp.allowSceneActivation = true;
        while (!_loadOp.isDone)
            yield return null;

        Scene levelScene = SceneManager.GetSceneByName(targetScene);
        if (levelScene.IsValid())
            SceneManager.SetActiveScene(levelScene);

        // гасим камеру, свет и звук меню, чтобы не мешали уровню
        MuteMenuScene();

        // --- Фаза 4: пост-активационный прогрев ---
        Physics.SyncTransforms();
        if (unloadUnusedAssetsDuringLoad)
        {
            AsyncOperation unload = Resources.UnloadUnusedAssets();
            while (unload != null && !unload.isDone)
                yield return null;
            GC.Collect();
        }
        float warmupEnd = Time.unscaledTime + postActivationWarmupTime;
        while (Time.unscaledTime < warmupEnd)
            yield return null;

        // --- Фаза 5: плавный выход в уровень ---
        if (loadingScreenFadeOut && _loadingGroup != null)
        {
            t = 0f;
            while (t < LoadingFadeOutDuration)
            {
                t += Time.unscaledDeltaTime;
                _loadingGroup.alpha = 1f - Mathf.Clamp01(t / LoadingFadeOutDuration);
                yield return null;
            }
        }

        // возвращаем время и прячем экран загрузки
        Time.timeScale = 1f;
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        EnsureLevelEventSystem(levelScene);

        Debug.Log("[GameLoader] Уровень '" + targetScene + "' загружен и прогрет за " +
                  (Time.unscaledTime - startTime).ToString("F2") + " с");

        // выгружаем сцену меню (вместе с этим объектом)
        SceneManager.UnloadSceneAsync(gameObject.scene);
    }

    private void SetProgress(float target, bool instant = false)
    {
        if (instant)
            _displayProgress = target;
        else
            _displayProgress = Mathf.MoveTowards(_displayProgress, target, maxProgressFillSpeed * Time.unscaledDeltaTime);
        ApplyProgress(_displayProgress);
    }

    private void ApplyProgress(float v)
    {
        v = Mathf.Clamp01(v);
        if (useWidthMethod && progressFillRect != null)
            progressFillRect.sizeDelta = new Vector2(Mathf.Lerp(0f, maxProgressBarWidth, v), progressFillRect.sizeDelta.y);
        else if (progressFill != null)
            progressFill.fillAmount = v;
        if (progressFill != null)
            progressFill.color = v < 0.5f
                ? Color.Lerp(startColor, middleColor, v * 2f)
                : Color.Lerp(middleColor, endColor, (v - 0.5f) * 2f);
        if (percentageText != null)
            percentageText.text = Mathf.RoundToInt(v * 100f) + "%";
    }

    private void SetStatus(string s)
    {
        if (statusText != null)
            statusText.text = s;
    }

    private void UpdateTips()
    {
        if (tipText == null || loadingTips == null || loadingTips.Length == 0)
            return;
        _tipTimer += Time.unscaledDeltaTime;
        if (_tipTimer < TipInterval)
            return;
        _tipTimer = 0f;
        _tipIndex = (_tipIndex + 1) % loadingTips.Length;
        string tip = loadingTips[_tipIndex];
        if (!string.IsNullOrEmpty(tip))
            tipText.text = tip;
    }

    private void HideOtherCanvasChildren()
    {
        if (loadingPanel == null)
            return;
        Canvas canvas = loadingPanel.GetComponentInParent<Canvas>(true);
        if (canvas == null)
            return;
        var keep = new System.Collections.Generic.HashSet<Transform>();
        Transform node = loadingPanel.transform;
        while (node != null && node != canvas.transform)
        {
            keep.Add(node);
            node = node.parent;
        }
        foreach (Transform child in canvas.transform)
            if (!keep.Contains(child))
                child.gameObject.SetActive(false);
    }

    private void MuteMenuScene()
    {
        Scene menuScene = gameObject.scene;
        foreach (Camera cam in Camera.allCameras)
            if (cam.gameObject.scene == menuScene)
                cam.enabled = false;
        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.gameObject.scene == menuScene)
                l.enabled = false;
        foreach (AudioListener al in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            if (al.gameObject.scene == menuScene)
                al.enabled = false;
    }

    private void EnsureLevelEventSystem(Scene levelScene)
    {
        Scene menuScene = gameObject.scene;
        EventSystem menuSystem = null;
        bool levelHas = false;
        foreach (EventSystem es in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
        {
            if (es.gameObject.scene == menuScene) menuSystem = es;
            else if (es.gameObject.scene == levelScene) levelHas = true;
        }
        if (levelHas || menuSystem == null)
            return;
        GameObject go = new GameObject("EventSystem");
        SceneManager.MoveGameObjectToScene(go, levelScene);
        go.AddComponent<EventSystem>();
        if (menuSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() != null)
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        else
            go.AddComponent<StandaloneInputModule>();
        Debug.LogWarning("[GameLoader] В уровне не было EventSystem, создали новый.");
    }

    private void RestoreMenu()
    {
        _isLoading = false;
        Time.timeScale = 1f;
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
            Canvas canvas = loadingPanel.GetComponentInParent<Canvas>(true);
            if (canvas != null)
                foreach (Transform child in canvas.transform)
                    if (child.name != "ExitConfirmPanel")
                        child.gameObject.SetActive(true);
        }
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);
        if (_menuGroup != null)
            _menuGroup.alpha = 1f;
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        if (go == null)
            return null;
        CanvasGroup g = go.GetComponent<CanvasGroup>();
        if (g == null)
            g = go.AddComponent<CanvasGroup>();
        return g;
    }
}
