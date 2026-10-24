using UnityEngine;

public class LiftResetter : MonoBehaviour
{
    [System.Serializable]
    public class LiftData
    {
        public Transform lift;          // Ссылка на куб-лифт
        public float targetY;           // Целевая высота по оси Y
        public float moveSpeed = 3f;    // Скорость движения
    }

    public LiftData[] lifts;            // Массив лифтов
    private bool resetTriggered = false;

    void Update()
    {
        if (resetTriggered)
        {
            foreach (var l in lifts)
            {
                if (l.lift != null)
                {
                    Vector3 pos = l.lift.position;
                    float newY = Mathf.MoveTowards(
                        pos.y,
                        l.targetY,
                        l.moveSpeed * Time.deltaTime
                    );
                    l.lift.position = new Vector3(pos.x, newY, pos.z);
                }
            }
        }
    }

    // Этот метод вызывается кнопкой
    public void TriggerReset()
    {
        resetTriggered = true;
    }
}