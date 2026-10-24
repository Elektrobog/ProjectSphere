using UnityEngine;

public class CubeMover : MonoBehaviour
{
    [Header("Настройки движения")]
    public float moveDistance = 2f;     // расстояние, на которое куб поднимается
    public float moveSpeed = 2f;        // скорость движения

    private Vector3 startPos;           // исходная позиция (сохраняется при старте)
    private Vector3 targetPos;          // целевая позиция
    private bool isMoving = false;

    void Start()
    {
        startPos = transform.position;
        targetPos = startPos;
    }

    void Update()
    {
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPos,
                moveSpeed * Time.deltaTime
            );

            if (Vector3.Distance(transform.position, targetPos) < 0.001f)
            {
                transform.position = targetPos;
                isMoving = false;
            }
        }
    }

    // Метод, который вызывает кнопка
    public void ToggleMove()
    {
        if (isMoving) return; // не начинаем новое движение, пока текущее не закончится

        // Проверяем, находится ли объект в исходной позиции (с небольшим допуском)
        bool isAtStart = Vector3.Distance(transform.position, startPos) < 0.001f;

        if (isAtStart)
        {
            // Если внизу – поднимаем
            targetPos = startPos + Vector3.up * moveDistance;
        }
        else
        {
            // Если уже поднят – возвращаем на исходную
            targetPos = startPos;
        }
        isMoving = true;
    }

    // Метод для возврата в исходную позицию (сброс)
    public void ResetToStart()
    {
        // Если уже движется к startPos – ничего не делаем
        if (isMoving && targetPos == startPos)
            return;

        // Прерываем текущее движение и ставим новую цель
        targetPos = startPos;
        isMoving = true;
    }

    // Дополнительные методы для отдельного вызова (опционально)
    public void MoveUp()
    {
        if (!isMoving && Vector3.Distance(transform.position, startPos) < 0.001f)
        {
            targetPos = startPos + Vector3.up * moveDistance;
            isMoving = true;
        }
    }

    public void MoveDown()
    {
        if (!isMoving && Vector3.Distance(transform.position, startPos + Vector3.up * moveDistance) < 0.001f)
        {
            targetPos = startPos;
            isMoving = true;
        }
    }
}