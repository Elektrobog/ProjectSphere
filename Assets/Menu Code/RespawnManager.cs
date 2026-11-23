using UnityEngine;

public class RespawnManager : MonoBehaviour
{
    public static RespawnManager Instance { get; private set; }

    [Header("Ссылки")]
    [Tooltip("Объект шара (если оставить пустым — найдётся автоматически по тегу Ball).")]
    public Transform ball;

    [Tooltip("Стартовая или текущая контрольная точка (если пусто — запомнится начальная позиция шара).")]
    public Transform currentCheckpoint;

    [Header("Настройки")]
    public string ballTag = "Ball";

    private Vector3 _savedPosition;
    private Quaternion _savedRotation;
    private bool _hasSavedPoint;
    private Rigidbody _ballRb;

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
        ResolveBall();

        if (currentCheckpoint != null)
        {
            SetCheckpoint(currentCheckpoint);
        }
        else if (ball != null)
        {
            _savedPosition = ball.position;
            _savedRotation = ball.rotation;
            _hasSavedPoint = true;
        }
    }

    private void ResolveBall()
    {
        if (ball == null && !string.IsNullOrEmpty(ballTag))
        {
            GameObject found = GameObject.FindGameObjectWithTag(ballTag);
            if (found != null)
                ball = found.transform;
        }

        if (ball != null && _ballRb == null)
            _ballRb = ball.GetComponent<Rigidbody>();
    }

    /// <summary>Сохранить новую контрольную точку (вызывается триггерами чекпоинтов).</summary>
    public void SetCheckpoint(Transform checkpoint)
    {
        if (checkpoint == null) return;
        currentCheckpoint = checkpoint;
        _savedPosition = checkpoint.position;
        _savedRotation = checkpoint.rotation;
        _hasSavedPoint = true;
    }

    /// <summary>Вернуть шар на последнюю сохранённую контрольную точку.</summary>
    public void Respawn(GameObject targetBall = null)
    {
        if (targetBall != null)
        {
            ball = targetBall.transform;
            _ballRb = targetBall.GetComponent<Rigidbody>();
        }
        else
        {
            ResolveBall();
        }

        if (ball == null)
        {
            Debug.LogWarning("[RespawnManager] Не найден объект шара для возрождения!", this);
            return;
        }

        if (currentCheckpoint != null)
        {
            _savedPosition = currentCheckpoint.position;
            _savedRotation = currentCheckpoint.rotation;
            _hasSavedPoint = true;
        }

        if (!_hasSavedPoint)
        {
            Debug.LogWarning("[RespawnManager] Не задана контрольная точка возрождения!", this);
            return;
        }

        if (_ballRb != null)
        {
            _ballRb.linearVelocity = Vector3.zero;
            _ballRb.angularVelocity = Vector3.zero;
            _ballRb.position = _savedPosition;
            _ballRb.rotation = _savedRotation;
        }

        ball.SetPositionAndRotation(_savedPosition, _savedRotation);
        Physics.SyncTransforms();
    }
}