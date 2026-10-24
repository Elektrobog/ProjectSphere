using UnityEngine;

public class UIRotate : MonoBehaviour
{
    [SerializeField] private float speed = 50f;
    private RectTransform rectTransform;
    private float angle;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void Update()
    {
        angle += speed * Time.deltaTime;
        rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
    }
}
