using UnityEngine;

//  OutlineFx © NullTale - https://x.com/NullTale/
namespace OutlineFx
{
    [ExecuteAlways] [DisallowMultipleComponent]
    public abstract class Outline : MonoBehaviour
    {
        internal Renderer _renderer;

        public abstract Color Color {get; set; }
        
        // =======================================================================
        private void OnEnable()
        {
            _renderer = GetComponent<Renderer>();
        }

        private void OnWillRenderObject()
        {
#if UNITY_EDITOR
            if (Application.isEditor && Equals(_renderer, null) == false)
            {
                if (TryGetComponent<Renderer>(out _renderer) == false)
                    return;
            }
#endif
            
            OutlineFxFeature.Render(this);
        }
    }
}