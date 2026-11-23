using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class CheckpointTrigger : MonoBehaviour
{
    [Header("Настройки точки возрождения")]
    [Tooltip("Точка спавна для этого чекпоинта. Если оставить пустым — шар появится в центре самого триггера.")]
    public Transform spawnPoint;

    [Tooltip("Тег шара.")]
    public string ballTag = "Ball";

    [Tooltip("Срабатывать только один раз (чтобы при случайном возврате назад прогресс не сбрасывался).")]
    public bool activateOnce = true;

    [Header("Эффекты при активации (необязательно)")]
    [Tooltip("Сюда можно привязать звук, частицы или включение подсветки чекпоинта.")]
    public UnityEvent onActivated;

    private bool _activated;

    private void Reset()
    {
        // Автоматически делает коллайдер триггером при добавлении скрипта
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        if (spawnPoint == null)
            spawnPoint = transform;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_activated && activateOnce)
            return;

        if (!other.CompareTag(ballTag))
            return;

        if (RespawnManager.Instance != null)
        {
            RespawnManager.Instance.SetCheckpoint(spawnPoint);
            _activated = true;
            onActivated?.Invoke();
        }
        else
        {
            Debug.LogWarning("[CheckpointTrigger] На сцене не найден RespawnManager!", this);
        }
    }
}