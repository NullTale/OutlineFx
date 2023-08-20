#nullable enable
using System;
using UnityEngine;

namespace UrpOutline
{
    [ExecuteAlways]
    public class Outline : MonoBehaviour
    {
        public   Color    _color = Color.magenta;
        internal Renderer _renderer;
        
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
            
            OutlineFeature.Render(this);
        }
    }
}