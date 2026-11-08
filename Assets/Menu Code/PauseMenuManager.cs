using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// Меню паузы: Esc ставит игру на паузу и показывает панель.
public class PauseMenuManager : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Панель меню паузы")]
    public GameObject pausePanel;

    [Header("Сцены")]
    public string mainMenuSceneName = "MainMenu";
    public string levelSceneName = "Level 1";

    private bool _paused;

    private void Update()
    {
        if (Keyboard.current == null)
            return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (_paused)
                Resume();
            else
                Pause();
        }
    }

    /// Поставить игру на паузу.
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

    /// Продолжить игру.
    public void Resume()
    {
        if (!_paused)
            return;
        _paused = false;
        Time.timeScale = 1f;
        if (pausePanel != null)
            pausePanel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// Перезапустить уровень.
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        string scene = string.IsNullOrEmpty(levelSceneName) ? SceneManager.GetActiveScene().name : levelSceneName;
        SceneManager.LoadScene(scene);
    }

    /// Вернуться в главное меню.
    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// Выйти из игры.
    public void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}