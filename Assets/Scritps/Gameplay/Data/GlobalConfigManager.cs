using System.Collections.Generic;
using BusSort;
using UnityEngine;
namespace Wayfu.Lamkn
{
    [CreateAssetMenu(fileName = "GlobalConfigManager", menuName = "Wayfu/GlobalConfigManager")]
    public class GlobalConfigManager : ScriptableObject
    {
        public static GlobalConfigManager Current;

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