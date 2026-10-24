using UnityEngine;
using UnityEngine.UI;

public class ExitGame : MonoBehaviour
{
    [SerializeField] private Button exitButton; // Ссылка на кнопку выхода

    void Start()
    {
        // Проверяем наличие кнопки
        if (exitButton != null)
        {
            // Назначаем метод обработки нажатия
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(QuitGame);
        }
        else
        {
            Debug.LogError("Exit button not assigned in ExitGame script!");
        }
    }

    /// <summary>
    /// Выход из игры
    /// </summary>
    void QuitGame()
    {
        Debug.Log("Exiting game...");

        // Выход из игры
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }
}