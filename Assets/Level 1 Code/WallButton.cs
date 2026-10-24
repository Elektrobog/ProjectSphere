using UnityEngine;
using UnityEngine.Events;

public class WallButton : MonoBehaviour
{
    [Header("Контрольные точки")]
    public Transform pressedPoint;    // точка "нажато"
    public Transform releasedPoint;   // точка "отжато"

    [Header("Событие")]
    public UnityEvent onPressed;

    [Header("Настройки")]
    public float tolerance = 0.01f;   // допуск по расстоянию

    private bool isPressed = false;

    void Update()
    {
        float distToPressed = Vector3.Distance(transform.position, pressedPoint.position);
        float distToReleased = Vector3.Distance(transform.position, releasedPoint.position);

        // Срабатывание события при достижении точки "нажато"
        if (!isPressed && distToPressed <= tolerance)
        {
            isPressed = true;
            Debug.Log("Button pressed!");
            onPressed.Invoke();
        }

        // Сброс состояния при достижении точки "отжато"
        if (isPressed && distToReleased <= tolerance)
        {
            isPressed = false;
            Debug.Log("Button released (reset)");
        }
    }

    /// <summary>
    /// Публичный метод для принудительного выполнения всех действий кнопки.
    /// Можно вызвать из другого скрипта или через события (например, по триггеру).
    /// </summary>
    public void ExecuteActions()
    {
        onPressed.Invoke();
    }

    // Для отладки в редакторе (можно вызвать через контекстное меню компонента)
    [ContextMenu("Execute Actions")]
    private void DebugExecute()
    {
        ExecuteActions();
    }
}