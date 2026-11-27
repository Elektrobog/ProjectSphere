using UnityEngine;

public class RespawnManager : MonoBehaviour
{
    public static RespawnManager Instance { get; private set; }

    [Header("Игрок")]
    [Tooltip("Ссылка на объект шара (если оставить пустым — найдётся автоматически по BallController).")]
    public Transform player;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private Vector3 checkpointPosition;
    private Quaternion checkpointRotation;

    /// <summary>True, если игрок реально активировал хотя бы один CheckpointTrigger.</summary>
    public bool HasCheckpoint { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        HasCheckpoint = false;
    }

    private void Start()
    {
        ResolvePlayer();

        if (player != null)
        {
            startPosition = player.position;
            startRotation = player.rotation;
        }

        // Гарантированный сброс на старте уровня, чтобы исключить телепорт вперёд
        HasCheckpoint = false;
    }

    private void ResolvePlayer()
    {
        if (player != null)
            return;

        BallController ball = FindAnyObjectByType<BallController>(FindObjectsInactive.Include);
        if (ball != null)
        {
            player = ball.transform;
            return;
        }

        GameObject taggedBall = GameObject.FindGameObjectWithTag("Ball");
        if (taggedBall != null)
            player = taggedBall.transform;
    }

    /// <summary>Этот метод вызывает ваш CheckpointTrigger.cs (передавая spawnPoint).</summary>
    public void SetCheckpoint(Transform checkpointTransform)
    {
        if (checkpointTransform == null)
            return;

        checkpointPosition = checkpointTransform.position;
        checkpointRotation = checkpointTransform.rotation;
        HasCheckpoint = true;
    }

    public void ClearCheckpoints()
    {
        HasCheckpoint = false;
    }

    public void Respawn()
    {
        ResolvePlayer();
        if (player == null)
        {
            Debug.LogError("[RespawnManager] Не найден объект шара для возрождения!", this);
            return;
        }

        Vector3 targetPos = HasCheckpoint ? checkpointPosition : startPosition;
        Quaternion targetRot = HasCheckpoint ? checkpointRotation : startRotation;

        if (player.TryGetComponent(out Rigidbody rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = targetPos;
            rb.rotation = targetRot;
        }

        player.SetPositionAndRotation(targetPos, targetRot);
        Physics.SyncTransforms();
    }
}