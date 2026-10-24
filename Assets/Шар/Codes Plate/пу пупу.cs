using UnityEngine;

public class DiagColumns : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("=== DiagColumns запущен ===");

        var all = GetComponentsInChildren<Transform>(true);
        Debug.Log($"Всего трансформов в дереве: {all.Length}");

        int columnCount = 0;
        int plатеCount = 0;
        int neckCount = 0;
        int otherCount = 0;

        for (int i = 0; i < all.Length && i < 20; i++)
        {
            string n = all[i].name;
            Debug.Log($"  [{i}] '{n}'");
        }

        foreach (var t in all)
        {
            if (t.name.StartsWith("Column_")) columnCount++;
            else if (t.name.StartsWith("Plate_")) plатеCount++;
            else if (t.name.StartsWith("Neck_")) neckCount++;
            else otherCount++;
        }

        Debug.Log($"Column_*: {columnCount}, Plate_*: {plатеCount}, Neck_*: {neckCount}, прочих: {otherCount}");
    }
}