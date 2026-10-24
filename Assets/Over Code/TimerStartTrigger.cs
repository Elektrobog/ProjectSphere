using UnityEngine;

public class TimerStartTrigger : MonoBehaviour
{
    [Header("References")]
    public GameTimer gameTimer;           // Ссылка на таймер
    public string playerTag = "Ball";     // Тег игрока

    private bool timerStarted = false;    // Чтобы не запускать повторно

    private void OnTriggerEnter(Collider other)
    {
        if (!timerStarted && other.CompareTag(playerTag))
        {
            if (gameTimer != null)
                gameTimer.StartTimer();
            else
                Debug.LogWarning("GameTimer not assigned!");

            timerStarted = true; // Можно и не блокировать, если нужно перезапускать при повторном входе
        }
    }

    // Опционально: сброс флага, если игрок вышел и снова вошёл (для тестирования)
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            timerStarted = false;
        }
    }
}