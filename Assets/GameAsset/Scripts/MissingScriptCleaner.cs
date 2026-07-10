#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public class MissingScriptCleaner : EditorWindow
{
    [MenuItem("Tools/Clean Missing Scripts")]
    public static void ShowWindow()
    {
        GetWindow<MissingScriptCleaner>("Missing Script Cleaner");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Xóa Missing Scripts trong Scene hiện tại", GUILayout.Height(40)))
        {
            CleanInScene();
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Xóa Missing Scripts trong Tất cả Prefabs (Project)", GUILayout.Height(40)))
        {
            CleanInPrefabs();
        }
    }

    private static void CleanInScene()
    {
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        int count = 0;

        foreach (var obj in rootObjects)
        {
            count += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
            foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
            {
                count += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            }
        }

        if (count > 0)
        {
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"<color=green>Thành công!</color> Đã xóa {count} scripts bị thiếu trong Scene.");
        }
        else
        {
            Debug.Log("Không tìm thấy script nào bị thiếu trong Scene.");
        }
    }

    private static void CleanInPrefabs()
    {
        string[] allPrefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int count = 0;

        foreach (string guid in allPrefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null)
            {
                int removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(prefab);

                // Kiểm tra cả các con của prefab
                foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
                {
                    removedCount += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                }

                if (removedCount > 0)
                {
                    count += removedCount;
                    EditorUtility.SetDirty(prefab);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"Đã dọn dẹp prefab: {path}");
                }
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"<color=cyan>Hoàn tất!</color> Tổng cộng đã xóa {count} scripts bị thiếu trong toàn bộ Prefabs.");
    }
}
#endif