#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public static class MaterialAssigner
{
    [MenuItem("Tools/Ball Plates/Assign Materials")]
    public static void Assign()
    {
        Material plateMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PlateMaterial.mat");
        Material neckMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/NeckMaterial.mat");
        Material outlineMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/OutlineMaterial.mat");

        if (plateMat == null || neckMat == null || outlineMat == null)
        {
            Debug.LogError("[MaterialAssigner] Не найдены материалы. Сначала запусти Tools/Ball Plates/Setup Materials.");
            return;
        }

        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogError("[MaterialAssigner] Выдели корневой объект шара в Hierarchy.");
            return;
        }

        int plateCount = 0, neckCount = 0, outlineCount = 0;

        foreach (Transform child in selected.GetComponentsInChildren<Transform>(true))
        {
            var mr = child.GetComponent<MeshRenderer>();
            if (mr == null) continue;

            string n = child.name;

            if (n.StartsWith("Plate_"))
            {
                mr.sharedMaterial = plateMat;
                plateCount++;
            }
            else if (n.StartsWith("Neck_"))
            {
                mr.sharedMaterial = neckMat;
                neckCount++;
            }
            else if (n == "Sphere_Outline")
            {
                mr.sharedMaterial = outlineMat;
                outlineCount++;
            }
        }

        EditorUtility.SetDirty(selected);
        Debug.Log($"[MaterialAssigner] Назначено: {plateCount} Plate, {neckCount} Neck, {outlineCount} Outline");
    }
}
#endif