using UnityEngine;
using UnityEngine.SceneManagement;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Подписываемся на событие загрузки сцены
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // В меню показываем курсор
        if (scene.name == "MainMenu")
            ShowCursor();
        else
            HideCursor(); // В LoadingScene и Level 1 курсор скрыт
    }

    public void HideCursor()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void ShowCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
