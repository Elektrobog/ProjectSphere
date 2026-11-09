#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Ищет и (по желанию) удаляет «пропавшие» скрипты (Missing Script) в открытых сценах и во всех префабах папки Assets.
/// Меню: Tools → Missing Scripts → Find / Remove.
/// Положите файл в любую папку Editor (например Assets/Editor/).
/// </summary>
public static class MissingScriptsTool
{
    [MenuItem("Tools/Missing Scripts/Find (Scenes + Prefabs)")]
    private static void Find() => Run(false);

    [MenuItem("Tools/Missing Scripts/Remove (Scenes + Prefabs)")]
    private static void Remove()
    {
        if (!EditorUtility.DisplayDialog("Удалить Missing Scripts",
                "Будут удалены все компоненты с пропавшим скриптом в открытых сценах и во всех префабах проекта. Продолжить?\n\n" +
                "Рекомендуется сначала сделать коммит или копию проекта.", "Удалить", "Отмена"))
            return;
        Run(true);
    }

    private static void Run(bool remove)
    {
        int total = 0;

        // Открытые сцены
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;
            int inScene = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                inScene += Process(root, remove, "Сцена '" + scene.name + "'");
            if (remove && inScene > 0) EditorSceneManager.MarkSceneDirty(scene);
            total += inScene;
        }

        // Все префабы в Assets
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int n = Process(root, remove, "Префаб " + path);
                if (remove && n > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                total += n;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        Debug.Log(total == 0
            ? "[Missing Scripts] Пропавших скриптов не найдено."
            : $"[Missing Scripts] {(remove ? "Удалено" : "Найдено")}: {total}. Подробности — в предупреждениях выше.");
    }

    private static int Process(GameObject root, bool remove, string where)
    {
        int count = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            if (n == 0) continue;
            count += n;
            Debug.LogWarning($"[Missing Scripts] {where}: {GetPath(t)} (пропавших компонентов: {n})", remove ? null : t.gameObject);
            if (remove)
            {
                Undo.RegisterCompleteObjectUndo(t.gameObject, "Remove Missing Scripts");
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            }
        }
        return count;
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
#endif
