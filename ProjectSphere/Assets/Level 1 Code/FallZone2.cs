using UnityEngine;
using UnityEngine.Events;
using TMPro;
using UnityEngine.SceneManagement;

public class FallZone2 : MonoBehaviour
{
    [Header("Настройки")]
    public string targetTag = "Ball";

    [Header("Экран завершения")]
    public GameObject completionPanel;
    public TMP_Text completionTimeText;
    public GameTimer gameTimer;

    [Header("Элементы интерфейса для скрытия")]
    public GameObject timerUI;
    public GameObject pauseButton;

    [Header("Настройки паузы")]
    public bool freezeTimeOnComplete = true;

    [Header("События при падении")]
    public UnityEvent onFall;

    public static bool IsGameCompleted { get; private set; }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            IsGameCompleted = true;

            // Останавливаем таймер
            if (gameTimer != null)
                gameTimer.StopTimer();

            // Показываем время на панели
            if (completionTimeText != null && gameTimer != null)
            {
                float elapsed = gameTimer.GetElapsedTime();
                int minutes = Mathf.FloorToInt(elapsed / 60f);
                int seconds = Mathf.FloorToInt(elapsed % 60f);
                int milliseconds = Mathf.FloorToInt((elapsed * 100f) % 100f);
                completionTimeText.text = string.Format("{0:00}:{1:00}:{2:00}", minutes, seconds, milliseconds);
            }

            // Скрываем таймер и кнопку паузы
            if (timerUI != null) timerUI.SetActive(false);
            if (pauseButton != null) pauseButton.SetActive(false);

            // Замораживаем время (если нужно)
            if (freezeTimeOnComplete)
                Time.timeScale = 0f;

            // Показываем курсор для взаимодействия с кнопками
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Показываем панель завершения
            if (completionPanel != null)
                completionPanel.SetActive(true);

            // Вызываем дополнительные события
            onFall.Invoke();
        }
    }

    // Метод для кнопки "Заново" — перезапускает уровень
    public void RestartLevel()
    {
        Debug.Log("Restart button clicked");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Метод для кнопки "Выход"
    public void QuitGame()
    {
        Debug.Log("Quit button clicked");
        Time.timeScale = 1f;
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void OnDestroy()
    {
        IsGameCompleted = false;
    }
}