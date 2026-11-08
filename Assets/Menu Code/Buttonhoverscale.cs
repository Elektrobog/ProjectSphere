using UnityEngine;
using UnityEngine.EventSystems;

/// Плавное масштабирование кнопки при наведении и нажатии.
public class ButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public float normalScale = 1f;
    public float hoverScale = 1.1f;
    public float pressedScale = 0.92f;
    public float animationSpeed = 12f;

    private Transform _target;
    private float _current;
    private bool _hover;
    private bool _pressed;

    private void Awake()
    {
        _target = transform;
        _current = normalScale;
    }

    private void OnEnable()
    {
        _hover = false;
        _pressed = false;
        _current = normalScale;
    }

    private void Update()
    {
        float wanted = _pressed ? pressedScale : (_hover ? hoverScale : normalScale);
        _current = Mathf.Lerp(_current, wanted, Time.deltaTime * animationSpeed);
        _target.localScale = new Vector3(_current, _current, _current);
    }

    public void OnPointerEnter(PointerEventData eventData) { _hover = true; }
    public void OnPointerExit(PointerEventData eventData) { _hover = false; _pressed = false; }
    public void OnPointerDown(PointerEventData eventData) { _pressed = true; }
    public void OnPointerUp(PointerEventData eventData) { _pressed = false; }
}