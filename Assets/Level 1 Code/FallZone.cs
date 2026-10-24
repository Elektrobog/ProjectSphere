using UnityEngine;

public class FallZone : MonoBehaviour
{
    [Header("��������� ��������")]
    public Transform respawnPoint;   // ����� ��������

    private void OnTriggerEnter(Collider other)
    {
        // ���������, ��� ��� ������ ���
        if (other.CompareTag("Ball"))
        {
            // ���������� ������� ����
            other.transform.position = respawnPoint.position;

            // ���� � ���� ���� Rigidbody � �������� ��������
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}
