using UnityEngine;
using TMPro;              // важно добавить!
using UnityEngine.Events;

public class GameTimer : MonoBehaviour
{
    [Header("UI Reference")]
    public TMP_Text timerText;             // теперь TMP_Text вместо Text

    [Header("Events")]
    public UnityEvent onTimerStart;
    public UnityEvent onTimerStop;

    [Header("Format")]
    public bool showMilliseconds = true;

    private float elapsedTime = 0f;
    private bool isRunning = false;

    void Start()
    {
        ResetTimer(); // сразу показываем нули
    }

    void Update()
    {
        if (isRunning)
        {
            elapsedTime += Time.deltaTime;
            UpdateDisplay();
        }
    }

    void UpdateDisplay()
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(elapsedTime / 60f);
        int seconds = Mathf.FloorToInt(elapsedTime % 60f);
        int milliseconds = Mathf.FloorToInt((elapsedTime * 100f) % 100f);

        if (showMilliseconds)
            timerText.text = string.Format("{0:00}:{1:00}:{2:00}", minutes, seconds, milliseconds);
        else
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    public void StartTimer()
    {
        if (!isRunning)
        {
            isRunning = true;
            onTimerStart.Invoke();
        }
    }

    public void StopTimer()
    {
        if (isRunning)
        {
            isRunning = false;
            onTimerStop.Invoke();
        }
    }

    public void ResetTimer()
    {
        elapsedTime = 0f;
        UpdateDisplay();
    }

    public void RestartTimer()
    {
        ResetTimer();
        StartTimer();
    }

    public float GetElapsedTime() => elapsedTime;
}