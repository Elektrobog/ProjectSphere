using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PauseMenuController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button quitButton;

    [Header("Input Settings")]
    [SerializeField] private InputActionAsset inputActions;
    private InputAction pauseAction;

    private bool isPaused = false;
    private bool canTogglePause = true;

    void Start()
    {
        resumeButton.onClick.AddListener(ResumeGame);
        quitButton.onClick.AddListener(QuitGame);
        pauseMenuPanel.SetActive(false);
        SetupInputAction();
        SetGameState(true);
    }

    void SetupInputAction()
    {
        if (inputActions != null)
        {
            pauseAction = inputActions.FindAction("Pause");
            if (pauseAction != null)
            {
                pauseAction.performed += OnPausePressed;
                pauseAction.Enable();
            }
        }
    }

    private void OnPausePressed(InputAction.CallbackContext context)
    {
        // Если уровень пройден — игнорируем нажатие
        if (FallZone2.IsGameCompleted) return;
        if (!canTogglePause) return;
        StartCoroutine(TogglePauseWithDelay());
    }

    private System.Collections.IEnumerator TogglePauseWithDelay()
    {
        TogglePause();
        canTogglePause = false;
        yield return new WaitForSecondsRealtime(0.1f);
        canTogglePause = true;
    }

    private void TogglePause()
    {
        if (isPaused) ResumeGame();
        else PauseGame();
    }

    private void PauseGame()
    {
        if (isPaused) return;
        isPaused = true;
        Time.timeScale = 0f;
        pauseMenuPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        resumeButton.Select();
    }

    public void ResumeGame()
    {
        if (!isPaused) return;
        isPaused = false;
        Time.timeScale = 1f;
        pauseMenuPanel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void SetGameState(bool playing)
    {
        if (playing)
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void OnDestroy()
    {
        if (pauseAction != null)
            pauseAction.performed -= OnPausePressed;
    }
}