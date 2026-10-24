using UnityEngine;
using System.Collections;

public class Column : MonoBehaviour
{
    public float liftHeight = 2f;
    public float liftSpeed = 4f;

    private Vector3 startPos;
    private Coroutine moveRoutine;

    private void Awake()
    {
        startPos = transform.localPosition;
    }

    public void SetUp(bool up)
    {
        Vector3 target = up ? startPos + Vector3.up * liftHeight : startPos;

        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(Move(target));
    }

    private IEnumerator Move(Vector3 target)
    {
        Vector3 start = transform.localPosition;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * liftSpeed;
            float eased = t * t * (3f - 2f * t);
            transform.localPosition = Vector3.Lerp(start, target, eased);
            yield return null;
        }

        transform.localPosition = target;
    }
}
