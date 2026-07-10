using UnityEditor;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Level Tool: custom inspector cho LevelData. Hiển thị bảng cân bằng màu và nút validate
    /// (kiểm tra ∑bullet == ∑block theo từng màu) — yêu cầu #10.
    /// </summary>
    [CustomEditor(typeof(LevelData))]
    public class LevelDataEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var level = (LevelData)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Level Tool — Validate", EditorStyles.boldLabel);

            bool okNow = level.ValidateColorBalance(out string rep);
            EditorGUILayout.HelpBox(rep, okNow ? MessageType.Info : MessageType.Warning);

            if (GUILayout.Button("Validate Color Balance (∑bullet == ∑block)"))
            {
                bool ok = level.ValidateColorBalance(out string report);
                EditorUtility.DisplayDialog(
                    ok ? "Validate — OK" : "Validate — MISMATCH",
                    (ok ? "Số block khớp số bullet theo từng màu.\n\n" : "Lệch số lượng — level có thể không giải được:\n\n") + report,
                    "OK");
            }
        }
    }
}
