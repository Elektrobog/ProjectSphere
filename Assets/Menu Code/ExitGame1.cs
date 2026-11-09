using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// Кнопка выхода из игры с подтверждением.
public class ExitGame : MonoBehaviour
{
    [Tooltip("Кнопка выхода в меню")]
    public Button exitButton;
    [Tooltip("Панель с вопросом «Ты уверен, что хочешь выйти?»")]
    public GameObject confirmPanel;

    private void Awake()
    {
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
        if (exitButton != null)
            exitButton.onClick.AddListener(RequestExit);
    }

    private void Update()
    {
        if (confirmPanel == null || !confirmPanel.activeSelf)
            return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CancelExit();
    }

    /// Показать панель подтверждения (вешается на кнопку Exit).
    public void RequestExit()
    {
        if (confirmPanel != null)
            confirmPanel.SetActive(true);
    }

    /// Скрыть панель подтверждения (кнопка «Отмена» или Esc).
    public void CancelExit()
    {
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
    }

    /// Выйти из игры (вешается на кнопку «Да»).
    public void ExitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Удобные псевдонимы для уже существующих привязок.
    public void Exit() { ExitApplication(); }
    public void QuitGame() { ExitApplication(); }
    public void ExitNow() { ExitApplication(); }
    public void ConfirmExit() { ExitApplication(); }
}