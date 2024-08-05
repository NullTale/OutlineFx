using UnityEngine;

namespace OutlineFx
{
    [ExecuteAlways]
    public class OutlineFxOverride : Outline
    {
        public Color    _color = Color.white;
        public Material _material;

        public override Color Color
        {
            get => _color;
            set => _color = value;
        }
        
        public override Material Material => _material;

        public float Alpha
        {
            get => _color.a;
            set => _color.a = value;
        }
    }
}