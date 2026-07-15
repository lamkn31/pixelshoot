using UnityEngine;
namespace Wayfu.Lamkn
{
    [System.Serializable]
    public class ColorObject
    {
        public TypeColor typeColor;
        public Color color;


        public Material matBlock;
        public Material matGun;
        public Material GetMaterial(TypeObject typeObject)
        {
            switch (typeObject)
            {
                case TypeObject.Block:
                    return matBlock;
                case TypeObject.Gun:
                    return matGun;
                default:
                    return matBlock;
            }
        }

        public Color GetColor()
        {
            return color;
        }
    }
    public enum TypeObject
    {
        Block = 0,
        Bullet = 1,
        Gun = 2,
    }
}

