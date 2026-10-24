using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.UI;
using TMPro;

public class GameLoader : MonoBehaviour
{
    [Header("=== Панели ===")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject loadingPanel;

    [Header("=== UI прогресса ===")]
    [SerializeField] private Image progressFill;          // для метода Fill
    [SerializeField] private RectTransform progressFillRect; // для метода ширины
    [SerializeField] private TextMeshProUGUI percentageText;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("=== Кнопки ===")]
    [SerializeField] private Button startButton;

    [Header("=== Сцена ===")]
    [SerializeField] private string targetScene = "Level1";

    [Tooltip("Минимальное время, которое загрузочный экран будет виден, до активации сцены")]
    [SerializeField] private float minLoadTime = 2.0f;

    [Tooltip("СКОЛЬКО секунд держать загрузочный экран ПОСЛЕ активации сцены. " +
             "Это время нужно движку, чтобы отработать Awake/Start новых объектов, " +
             "физику, NavMesh и первые тяжёлые кадры рендера — без этого запаса " +
             "фриз просто переносится с загрузочного экрана на геймплей.")]
    [SerializeField] private float postActivationWarmupTime = 0.6f;

    [Tooltip("Максимальная скорость роста бара (доля прогресса в секунду). " +
             "Не даёт бару 'долетать' до 100% рывком, даже если реальная загрузка резко ускорилась.")]
    [SerializeField] private float maxProgressFillSpeed = 0.7f;

    [Header("=== Прогрев шейдеров (сильно снижает фриз при первом повороте камеры) ===")]
    [Tooltip("Опционально: ShaderVariantCollection со всеми материалами/шейдерами уровня. " +
             "Прогревается ЗАРАНЕЕ, пока пользователь видит загрузочный экран, а не когда камера " +
             "впервые смотрит на объект в игре. Как создать — см. пояснение в чате.")]
    [SerializeField] private ShaderVariantCollection shaderWarmupCollection;

    [Header("=== Цвета ===")]
    [SerializeField] private Color startColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color middleColor = new Color(1f, 1f, 0.3f, 1f);
    [SerializeField] private Color endColor = new Color(0.3f, 1f, 0.3f, 1f);

    [Header("=== Советы ===")]
    [SerializeField] private string[] loadingTips;
    [SerializeField] private TextMeshProUGUI tipText;

    [Header("=== Метод бара ===")]
    [SerializeField] private bool useWidthMethod = true;
    [SerializeField] private float maxProgressBarWidth = 600f;

    private bool isLoading = false;
    private float currentProgress = 0f;
    private float targetProgress = 0f;

    // GameLoader и Canvas — РАЗНЫЕ root-объекты в иерархии сцены (см. скриншот).
    // DontDestroyOnLoad работает только на корневые объекты, поэтому оба нужно
    // персистить отдельно, иначе Canvas с прогресс-баром уничтожится при
    // выгрузке сцены меню, а ссылки в этом скрипте станут Missing.
    private GameObject canvasRoot;

    void Start()
    {
        InitializeUI();

        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(StartGameLoading);
        }

        InitializeTips();
    }

    void InitializeUI()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        if (progressFillRect == null && progressFill != null)
            progressFillRect = progressFill.GetComponent<RectTransform>();

        if (useWidthMethod && progressFillRect != null)
            progressFillRect.sizeDelta = new Vector2(0, progressFillRect.sizeDelta.y);
    }

    void InitializeTips()
    {
        if (loadingTips == null || loadingTips.Length == 0)
        {
            loadingTips = new string[]
            {
                "Исследуйте локации внимательнее — не всё видно с первого взгляда",
                "Сохраняйтесь чаще, чтобы не терять прогресс",
                "Изучите управление в настройках перед стартом",
                "Обращайте внимание на подсказки на экране",
                "Не спешите — уровень вознаграждает внимательность"
            };
        }
    }

    void Update()
    {
        if (!isLoading) return;

        if (Mathf.Abs(currentProgress - targetProgress) > 0.0005f)
        {
            // MoveTowards вместо Lerp — рост ограничен фиксированной СКОРОСТЬЮ,
            // а не долей расстояния, поэтому бар не может "долететь" скачком
            currentProgress = Mathf.MoveTowards(
                currentProgress,
                targetProgress,
                maxProgressFillSpeed * Time.unscaledDeltaTime
            );
            UpdateProgressUI(currentProgress);
        }
    }

    public void StartGameLoading()
    {
        if (isLoading) return;

        if (!SceneExists(targetScene))
        {
            Debug.LogError($"Сцена '{targetScene}' не найдена в Build Settings!");
            return;
        }

        // ВАЖНО: без этого объекты (и корутина) будут уничтожены Unity'ей
        // в момент выгрузки текущей сцены, прямо посреди загрузки/warmup-фазы —
        // отсюда эффект "бар замер на 90% и тут же сцена сменилась".
        // Персистим отдельно GameLoader (где живёт корутина) и Canvas (где живёт UI),
        // так как в вашей иерархии это два разных root-объекта.
        DontDestroyOnLoad(transform.root.gameObject);

        if (loadingPanel != null)
        {
            canvasRoot = loadingPanel.transform.root.gameObject;
            if (canvasRoot != transform.root.gameObject)
                DontDestroyOnLoad(canvasRoot);
        }

        SwitchToLoadingScreen();
        StartCoroutine(LoadGameSceneCoroutine());
    }

    void SwitchToLoadingScreen()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        currentProgress = 0f;
        targetProgress = 0f;
        UpdateProgressUI(0f);

        ShowRandomTip();

        if (startButton != null)
            startButton.interactable = false;

        isLoading = true;
    }

    IEnumerator LoadGameSceneCoroutine()
    {
        float startTime = Time.unscaledTime;

        AsyncOperation op = SceneManager.LoadSceneAsync(targetScene);
        op.allowSceneActivation = false;

        // ===== ФАЗА 1: реальная загрузка ассетов сцены (Unity: 0 → 0.9) =====
        while (op.progress < 0.9f)
        {
            float realProgress = Mathf.Clamp01(op.progress / 0.9f);
            float elapsed = Time.unscaledTime - startTime;
            float timeProgress = Mathf.Clamp01(elapsed / minLoadTime);

            // цель = максимум из реального прогресса и "по времени", но не больше 0.9,
            // и без искусственного "форсажа" вперёд реальных данных
            targetProgress = Mathf.Min(0.9f, Mathf.Max(realProgress, timeProgress) * 0.9f);

            UpdateStatusText(realProgress);
            yield return null;
        }

        // добираем минимальное время показа экрана, если загрузка была быстрее него
        while (Time.unscaledTime - startTime < minLoadTime)
        {
            targetProgress = 0.9f;
            yield return null;
        }

        // ===== ФАЗА 2: прогрев шейдеров ДО того, как игрок увидит уровень =====
        if (statusText != null) statusText.text = "Подготовка сцены...";
        targetProgress = 0.95f;

        if (shaderWarmupCollection != null && !shaderWarmupCollection.isWarmedUp)
        {
            // Синхронный прогрев — это ок именно здесь: экран загрузки уже показан,
            // короткая пауза тут не ощущается как фриз в игре.
            shaderWarmupCollection.WarmUp();
            yield return null;
        }

        // ждём, пока бар (с ограниченной скоростью) реально доберётся до 0.9
        while (currentProgress < 0.9f - 0.001f)
            yield return null;

        // ===== ФАЗА 3: активация сцены =====
        targetProgress = 1f;
        op.allowSceneActivation = true;

        while (!op.isDone)
            yield return null;

        // ===== ФАЗА 4: прогрев ПОСЛЕ активации — вот это убирает фриз при первом движении =====
        // Сцена активна, но Awake/Start новых объектов, физика, NavMesh и первый
        // "тяжёлый" рендер-кадр ещё впереди. Держим загрузочный экран, пока это происходит.
        if (statusText != null) statusText.text = "Запуск!";

        float warmupTimer = 0f;
        while (warmupTimer < postActivationWarmupTime)
        {
            warmupTimer += Time.unscaledDeltaTime;
            yield return null;
        }

        // пара лишних кадров рендера "под капотом" загрузочного экрана
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        isLoading = false;

        // Мы пережили переход сцены через DontDestroyOnLoad — теперь, когда
        // уровень полностью прогрет и запущен, эти объекты больше не нужны.
        if (canvasRoot != null && canvasRoot != transform.root.gameObject)
            Destroy(canvasRoot);

        Destroy(transform.root.gameObject);
    }

    void UpdateProgressUI(float progress)
    {
        if (useWidthMethod)
        {
            if (progressFillRect != null)
            {
                float width = progress * maxProgressBarWidth;
                progressFillRect.sizeDelta = new Vector2(width, progressFillRect.sizeDelta.y);
                UpdateProgressColor(progress);
            }
        }
        else
        {
            if (progressFill != null)
            {
                progressFill.fillAmount = progress;
                UpdateProgressColor(progress);
            }
        }

        if (percentageText != null)
        {
            int percent = Mathf.RoundToInt(progress * 100);
            percentageText.text = $"{percent}%";
        }
    }

    void UpdateProgressColor(float progress)
    {
        if (progressFill == null) return;

        Color fillColor;
        if (progress < 0.5f)
        {
            float t = progress * 2f;
            fillColor = Color.Lerp(startColor, middleColor, t);
        }
        else
        {
            float t = (progress - 0.5f) * 2f;
            fillColor = Color.Lerp(middleColor, endColor, t);
        }

        progressFill.color = fillColor;
    }

    void UpdateStatusText(float realProgress)
    {
        if (statusText == null) return;

        if (realProgress < 0.2f)
            statusText.text = "Инициализация уровня...";
        else if (realProgress < 0.4f)
            statusText.text = "Загрузка ресурсов...";
        else if (realProgress < 0.6f)
            statusText.text = "Загрузка объектов...";
        else if (realProgress < 0.8f)
            statusText.text = "Построение уровня...";
        else
            statusText.text = "Финальная подготовка...";
    }

    void ShowRandomTip()
    {
        if (tipText != null && loadingTips.Length > 0)
        {
            int randomIndex = Random.Range(0, loadingTips.Length);
            tipText.text = $"Совет: {loadingTips[randomIndex]}";
        }
    }

    bool SceneExists(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (name == sceneName)
                return true;
        }
        return false;
    }
}