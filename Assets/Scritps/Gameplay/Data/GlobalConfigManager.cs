using System.Collections.Generic;
using BusSort;
using UnityEngine;
namespace Wayfu.Lamkn
{
    [CreateAssetMenu(fileName = "GlobalConfigManager", menuName = "Wayfu/GlobalConfigManager")]
    public class GlobalConfigManager : ScriptableObject
    {
        public static GlobalConfigManager Current;

        /// <summary>
        /// Asset config dùng chung. Runtime load từ Resources; trong Editor fallback tìm qua AssetDatabase.
        /// LƯU Ý: muốn build chạy được thì asset phải nằm trong thư mục Resources.
        /// </summary>
        public static GlobalConfigManager Instance
        {
            get
            {
                if (Current != null) return Current;
                Current = Resources.Load<GlobalConfigManager>("GlobalConfigManager");
#if UNITY_EDITOR
                if (Current == null)
                {
                    var guids = UnityEditor.AssetDatabase.FindAssets("t:GlobalConfigManager");
                    if (guids.Length > 0)
                        Current = UnityEditor.AssetDatabase.LoadAssetAtPath<GlobalConfigManager>(
                            UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
                }
#endif
                return Current;
            }
        }

        /// <summary>Material theo màu + loại object (null nếu chưa cấu hình).</summary>
        public static Material MaterialOf(TypeColor color, TypeObject type)
            => Instance != null ? Instance.GetMatColor(color, type) : null;

        /// <summary>
        /// Màu hiển thị của TypeColor để vẽ gizmo/editor. ÉP alpha = 1 vì asset đang lưu a = 0
        /// (dùng thẳng sẽ ra trong suốt).
        /// </summary>
        public static Color ColorOf(TypeColor color)
        {
            var c = Instance != null ? Instance.GetColor(color) : Color.gray;
            c.a = 1f;
            return c;
        }

        public List<ColorObject> listColor;
        public List<EnviromentObject> listEnviroment;
        [System.NonSerialized]
        private Dictionary<TypeColor, ColorObject> colorMap;
        [System.NonSerialized]
        private Dictionary<TypeEnviroment, EnviromentObject> envMap;

        private void EnsureMap()
        {
            if (colorMap != null) return;
            colorMap = new Dictionary<TypeColor, ColorObject>(listColor.Count);
            foreach (var item in listColor) colorMap[item.typeColor] = item;
        }

        private void EnsureEnvMap()
        {
            if (envMap != null) return;
            envMap = new Dictionary<TypeEnviroment, EnviromentObject>(listEnviroment != null ? listEnviroment.Count : 0);
            if (listEnviroment == null) return;
            foreach (var item in listEnviroment) envMap[item.typeEnviroment] = item;
        }

        public EnviromentObject GetEnviroment(TypeEnviroment env)
        {
            EnsureEnvMap();
            return envMap.TryGetValue(env, out var obj) ? obj : null;
        }

        public Material GetMatRoad(TypeEnviroment env)
        {
            var obj = GetEnviroment(env);
            return obj != null ? obj.GetMatRoad(env) : null;
        }

        public Material GetMatEnviroment(TypeEnviroment env)
        {
            var obj = GetEnviroment(env);
            return obj != null ? obj.GetMatEviroment(env) : null;
        }

        public GameObject GetEnviromentUI(TypeEnviroment env)
        {
            var obj = GetEnviroment(env);
            return obj != null ? obj.GetEnviromentUI() : null;
        }

        public Material GetMatColor(TypeColor color, TypeObject type)
        {
            EnsureMap();
            return colorMap.TryGetValue(color, out var obj) ? obj.GetMaterial(type) : null;
        }

        public Color GetColor(TypeColor color)
        {
            EnsureMap();
            return colorMap.TryGetValue(color, out var obj) ? obj.GetColor() : Color.white;
        }

    }
}