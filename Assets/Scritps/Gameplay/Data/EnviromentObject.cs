using UnityEngine;

namespace Wayfu.Lamkn
{
    [System.Serializable]
    public class EnviromentObject
    {
        public TypeEnviroment typeEnviroment;
        public Material matRoad;
        public Material matEnviroment;

        [Header("Per-environment UI + car materials")]
        [Tooltip("UI prefab spawned under Canvas/Enviroment on level init. Cleared between levels.")]
        public GameObject enviromentUI;
        [Tooltip("Main car body color. Applied to the car body's secondary submesh + the car top.")]
        public Material matCarBody;
        [Tooltip("Detail trim color. Applied to the car body's primary submesh.")]
        public Material matCarDetail;

        public Material GetMatRoad(TypeEnviroment typeRoad) => matRoad;
        public Material GetMatEviroment(TypeEnviroment typeEnviroment) => matEnviroment;
        public GameObject GetEnviromentUI() => enviromentUI;
        public Material GetMatCarBody() => matCarBody;
        public Material GetMatCarDetail() => matCarDetail;
    }
}
