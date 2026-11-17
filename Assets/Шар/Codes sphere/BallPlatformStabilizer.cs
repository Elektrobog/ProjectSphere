using UnityEngine;

/// <summary>
/// Стабилизатор шара на платформе.
/// Решает проблему соскальзывания с движущихся платформ.
/// </summary>
[RequireComponent(typeof(BallController))]
public class BallPlatformStabilizer : MonoBehaviour
{
    [Header("Настройки стабилизации")]
    [Tooltip("Сила притягивания шара к платформе (0 = выключено)")]
    [Min(0f)] public float anchorStrength = 10f;

    [Tooltip("Максимальная скорость коррекции позиции")]
    [Min(0f)] public float maxCorrectionSpeed = 5f;

    [Tooltip("Радиус поиска платформы под шаром")]
    [Min(0.1f)] public float platformCheckRadius = 0.6f;

    [Tooltip("Слой платформ (по умолчанию: Default)")]
    public LayerMask platformLayer = 1;

    private Rigidbody rb;
    private BallController ballController;
    private Transform activePlatformTransform;
    private Vector3 anchorLocalPosition;
    private bool hasAnchor;
    private int lastContactCount;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ballController = GetComponent<BallController>();
    }

    private void FixedUpdate()
    {
        if (ballController.PlatformVelocity.sqrMagnitude < 0.01f)
        {
            // Платформа не движется — якорь не нужен
            hasAnchor = false;
            return;
        }

        // Проверяем, есть ли платформа под шаром
        if (Physics.Raycast(
            transform.position,
            Vector3.down,
            out RaycastHit hit,
            platformCheckRadius,
            platformLayer))
        {
            IPlatformVelocityProvider platform = hit.collider.GetComponent<IPlatformVelocityProvider>();
            if (platform != null)
            {
                Transform newPlatformTransform = (platform as MonoBehaviour)?.transform;

                // Обновляем платформу, если:
                // 1. Нет активной платформы ИЛИ
                // 2. Новая платформа отличается ИЛИ
                // 3. Новое столкновение имеет больше контактов
                if (activePlatformTransform == null ||
                    !ReferenceEquals(activePlatformTransform, newPlatformTransform))
                {
                    activePlatformTransform = newPlatformTransform;
                    hasAnchor = false; // Сбрасываем якорь при смене платформы
                }
            }
        }
        else
        {
            // Нет платформы под шаром
            activePlatformTransform = null;
            hasAnchor = false;
        }

        // Устанавливаем якорь, если платформа движется и шар почти неподвижен
        if (activePlatformTransform != null && !hasAnchor)
        {
            Vector3 relativeVelocity = rb.linearVelocity - ballController.PlatformVelocity;
            if (relativeVelocity.sqrMagnitude < 0.1f) // Почти стоит относительно платформы
            {
                anchorLocalPosition = activePlatformTransform.InverseTransformPoint(rb.position);
                hasAnchor = true;
            }
        }

        // Корректируем позицию, если якорь установлен
        if (hasAnchor && activePlatformTransform != null)
        {
            Vector3 targetPosition = activePlatformTransform.TransformPoint(anchorLocalPosition);
            Vector3 positionError = targetPosition - rb.position;
            positionError.y = 0f; // Игнорируем вертикаль

            Vector3 correctionVelocity = Vector3.ClampMagnitude(
                positionError * anchorStrength,
                maxCorrectionSpeed
            );

            rb.linearVelocity = new Vector3(
                correctionVelocity.x,
                rb.linearVelocity.y,
                correctionVelocity.z
            );
        }
    }
}