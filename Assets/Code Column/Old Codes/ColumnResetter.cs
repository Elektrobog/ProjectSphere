using UnityEngine;

public class ColumnResetter : MonoBehaviour
{
    [System.Serializable]
    public class ColumnData
    {
        public Transform column;                // Ссылка на колонну
        public Vector3 targetEulerAngles;       // Целевое вращение в градусах
        public float rotationSpeed = 180f;      // Скорость поворота (град/сек)
    }

    public ColumnData[] columns;                // Массив колонн
    private bool resetTriggered = false;        // Флаг активации

    void Update()
    {
        if (resetTriggered)
        {
            foreach (var col in columns)
            {
                if (col.column != null)
                {
                    Quaternion targetRot = Quaternion.Euler(col.targetEulerAngles);
                    col.column.rotation = Quaternion.RotateTowards(
                        col.column.rotation,
                        targetRot,
                        col.rotationSpeed * Time.deltaTime
                    );
                }
            }
        }
    }

    // Этот метод можно вызвать с кнопки
    public void TriggerReset()
    {
        resetTriggered = true;
    }
}
