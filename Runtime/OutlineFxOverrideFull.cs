using UnityEngine;

namespace OutlineFx
{
    /// <summary>
    /// Same as <see cref="OutlineFxOverride"/>, with per-object thickness and solid fill
    /// overriding the values of the <see cref="OutlineFxFeature"/>.
    /// Each object using custom settings is rendered in its own outline pass.
    /// </summary>
    [ExecuteAlways]
    public class OutlineFxOverrideFull : Outline
    {
        public Color    _color = Color.white;
        public Material _material;

        [Tooltip("Override the thickness of the renderer feature for this object")]
        public bool  _overrideThickness;
        [Range(0, 1f)]
        [Tooltip("Outline thickness of this object")]
        public float _thickness = 0.001f;

        [Tooltip("Override the solid fill of the renderer feature for this object")]
        public bool  _overrideSolid;
        [Range(0, 1f)]
        [Tooltip("Solid fill of this object")]
        public float _solid;

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

        public float Thickness
        {
            get => _thickness;
            set => _thickness = Mathf.Clamp01(value);
        }

        public float Solid
        {
            get => _solid;
            set => _solid = Mathf.Clamp01(value);
        }

        /// <summary>
        /// True when this object needs its own outline pass.
        /// </summary>
        public bool HasCustomSettings => _overrideThickness || _overrideSolid;
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(OutlineFxOverrideFull))]
    [UnityEditor.CanEditMultipleObjects]
    internal class OutlineFxOverrideFullEditor : UnityEditor.Editor
    {
        private const string k_Warning =
            "Performance: each object with an overridden thickness or solid fill is rendered in its own outline pass " +
            "(mask render + full-screen blit). Use it on a few objects only.\n\n" +
            "Overlap: outlines rendered in separate passes are stacked on top of each other instead of merging " +
            "with the other outlines.\n\n" +
            "Leave both overrides disabled to use the renderer feature settings at no extra cost.";

        public override void OnInspectorGUI()
        {
            UnityEditor.EditorGUILayout.HelpBox(k_Warning, UnityEditor.MessageType.Warning);
            DrawDefaultInspector();
        }
    }
#endif
}
