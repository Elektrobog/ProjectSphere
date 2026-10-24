using UnityEngine;

public class SideElevatorFlexible : MonoBehaviour
{
    public enum MoveAxis { X, Z }       // выбор оси
    public enum MoveDirection { Plus, Minus } // выбор стороны (+ или -)

    [Header("Ќастройки движени€")]
    public MoveAxis moveAxis = MoveAxis.X;          // ось движени€
    public MoveDirection moveDirection = MoveDirection.Plus; // сторона
    public float moveDistance = 3f;                 // рассто€ние
    public float moveSpeed = 2f;                    // скорость

    private Vector3 startPos;
    private Vector3 targetPos;
    private bool movingToTarget = true;

    void Start()
    {
        startPos = transform.position;
        targetPos = startPos + GetDirection() * moveDistance;
    }

    void Update()
    {
        // ƒвигаем платформу к текущей цели
        transform.position = Vector3.MoveTowards(
            transform.position,
            movingToTarget ? targetPos : startPos,
            moveSpeed * Time.deltaTime
        );

        // ѕровер€ем, достигли ли цели
        if (Vector3.Distance(transform.position, movingToTarget ? targetPos : startPos) < 0.001f)
        {
            movingToTarget = !movingToTarget; // мен€ем направление
        }
    }

    // ¬озвращает направление движени€ в зависимости от оси и стороны
    private Vector3 GetDirection()
    {
        Vector3 dir;

        if (moveAxis == MoveAxis.X)
            dir = Vector3.right;   // ось X
        else
            dir = Vector3.forward; // ось Z

        if (moveDirection == MoveDirection.Minus)
            dir *= -1; // инвертируем направление

        return dir;
    }
}
