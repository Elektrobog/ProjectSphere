using UnityEngine;

/// Управление курсором: в меню обычный курсор, в уровне скрытый и захваченный.
public class CursorManager : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Захватить курсор при старте (включай для уровня, в меню курсор должен быть свободным)")]
    public bool lockCursorOnStart = false;
    [Tooltip("Режим захвата курсора")]
    public CursorLockMode lockMode = CursorLockMode.Locked;
    [Tooltip("Показывать курсор, когда он освобождён")]
    public bool visibleWhenUnlocked = true;

    public static CursorManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (lockCursorOnStart)
            LockCursor();
        else
            UnlockCursor();
    }

    /// Захватить и спрятать курсор (для геймплея).
    public void LockCursor()
    {
        Cursor.lockState = lockMode;
        Cursor.visible = false;
    }

    /// Освободить и показать курсор (для меню и паузы).
    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = visibleWhenUnlocked;
    }
}