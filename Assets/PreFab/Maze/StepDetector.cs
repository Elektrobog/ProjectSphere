using UnityEngine;

public class StepDetector : MonoBehaviour
{
    public int gridX; // будут установлены менеджером при создании
    public int gridZ;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            GridManager.Instance.PlayerSteppedOnTile(gridX, gridZ);
        }
    }
}