using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

//  OutlineFx © NullTale - https://x.com/NullTale/
namespace OutlineFx
{
    public partial class OutlineFxFeature : ScriptableRendererFeature
    {
        private const string k_OutlineShader = "Hidden/OutlineFx/Main";
        		
        private static readonly int s_MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int s_ColorId   = Shader.PropertyToID("_Color");
		
        private static List<ShaderTagId> k_ShaderTags;
        private static Mesh              k_ScreenMesh;
        
        public static Mesh ScreenMesh => k_ScreenMesh;

        public float Solid
        {
            get => _solid;
            set => _solid = Mathf.Clamp01(value);
        }

        public float Thickness
        {
            get => _thickness;
            set => _thickness = Mathf.Clamp01(value);
        }
        
        public bool Mask
        {
            get => _solidMask._enabled;
            set
            {
                if (_solidMask._enabled == value)
                    return;
                    
                _solidMask._enabled = value;
                
                if (_solidMask._enabled)
                    _outlineMat.EnableKeyword("ALPHA_MASK");
                else
                    _outlineMat.DisableKeyword("ALPHA_MASK");
            }
        }

        [SerializeField]
        [Tooltip("When draw outline")]
        private RenderPassEvent _event = RenderPassEvent.AfterRenderingPostProcessing;
        [Range(0, 1)]
        [SerializeField]
        [Tooltip("Solid fill of outline")]
        private float  _solid;

        [Range(0, 1f)]
        [Tooltip("Outline thickness")]
        public  float  _thickness = 0.001f;
        [Range(0, 1)]
        [SerializeField]
        [Tooltip("Alpha cutout threshold for transparent objects")]
        private float  _alphaCutout = .5f;

        [Tooltip("Edge filter")]
        public  Mode   _mode   = Mode.Hard;
        [HideInInspector]
        public Filter  _filter = Filter.Box;
        [HideInInspector]
        public bool    _attachDepth = true;
        
        public Optional<string> _output = new Optional<string>("_globalTex", false);
        
        public SolidMask _solidMask = new SolidMask();
        
        [SerializeField] [HideInInspector]
        public Shader _shader;
        
        private Material _outlineMat;
        private Vector4  _step;
        private Pass     _pass;
        
        private static List<Outline> _renderers = new List<Outline>();

        // =======================================================================
        public class RenderTarget
        {
            public RTHandle Handle;
            public int      Id;
			
            private bool    _allocated;
            
            // =======================================================================
            public RenderTarget Allocate(RenderTexture rt, string name)
            {
                Handle = RTHandles.Alloc(rt, name);
                Id     = Shader.PropertyToID(name);
				
                return this;
            }
			
            public RenderTarget Allocate(string name)
            {
                Handle = _alloc(name);
                Id     = Shader.PropertyToID(name);
				
                return this;
            }
			
            public void Get(CommandBuffer cmd, in RenderTextureDescriptor desc)
            {
                _allocated = true;
                cmd.GetTemporaryRT(Id, desc);
            }
			
            public void Release(CommandBuffer cmd)
            {
                if (_allocated == false)
                    return;
                
                _allocated = false;
                cmd.ReleaseTemporaryRT(Id);
            }
        }

        [Serializable]
        public class SolidMask
        {
            public bool      _enabled;
            public Texture2D _pattern;
            public float     _scale = 50f;
            public Vector2   _velocity = new Vector2(0, 0);
        }
        
        public enum Mode
        {
            Hard,
            Soft
        }

        public enum Filter
        {
            Cross,
            Box
        }

        // =======================================================================
        public override void Create()
        {
            
            _pass = new Pass() { _owner = this };
            _pass.Init();
            _renderers.Clear();
            
            _validateContent();
            _validateMaterial();
			
            if (k_ScreenMesh == null)
            {
                // init triangle
                k_ScreenMesh = new Mesh();
                _initScreenMesh(k_ScreenMesh, Matrix4x4.identity);
            }
			
            if (k_ShaderTags == null)
            {
                k_ShaderTags = new List<ShaderTagId>(new[]
                {
                    new ShaderTagId("SRPDefaultUnlit"),
                    new ShaderTagId("UniversalForward"),
                    new ShaderTagId("UniversalForwardOnly")
                });
            }
        }
        
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // in game or scene view only
            if (renderingData.cameraData.cameraType != CameraType.Game && renderingData.cameraData.cameraType != CameraType.SceneView)
                return;
            
            if (_renderers.Count == 0)
                return;
            
            var aspect = Screen.width / (float)Screen.height;
            _step.x = _thickness / aspect;
            _step.y = _thickness;
            _step  *= 0.007f;
            if (_mode == Mode.Soft)
                _step *= 2f;
            
            renderer.EnqueuePass(_pass);
        }

        public static void Render(Outline inst)
        {
            _renderers.Add(inst);
        }
        
        // =======================================================================
        private void _validateMaterial()
        {
            _outlineMat = new Material(_shader);
            switch (_mode)
            {
                case Mode.Soft:
                    _outlineMat.EnableKeyword("SOFT");
                    break;
                case Mode.Hard:
                    _outlineMat.EnableKeyword("HARD");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            switch (_filter)
            {
                case Filter.Cross:
                    _outlineMat.EnableKeyword("CROSS");
                    break;
                case Filter.Box:
                    _outlineMat.EnableKeyword("BOX");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            if (_solidMask._enabled)
            {
                _outlineMat.EnableKeyword("ALPHA_MASK");
            }
        }
        
        private void _validateContent()
        {
#if UNITY_EDITOR
            if (_shader == null)
                _shader = Shader.Find(k_OutlineShader);
			
            if (_solidMask._pattern == null)
            {
                var dir = Path.GetDirectoryName(UnityEditor.AssetDatabase.GetAssetPath(_shader));
                _solidMask._pattern = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}\\checker.png");
            }
            
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
		
        private static void _initScreenMesh(Mesh mesh, Matrix4x4 mat)
        {
            mesh.vertices  = _verts(0f);
            mesh.uv        = _texCoords();
            mesh.triangles = new int[3] { 0, 1, 2 };

            mesh.UploadMeshData(true);

            // -----------------------------------------------------------------------
            Vector3[] _verts(float z)
            {
                var r = new Vector3[3];
                for (var i = 0; i < 3; i++)
                {
                    var uv = new Vector2((i << 1) & 2, i & 2);
                    r[i] = mat.MultiplyPoint(new Vector3(uv.x * 2f - 1f, uv.y * 2f - 1f, z));
                }

                return r;
            }

            Vector2[] _texCoords()
            {
                var r = new Vector2[3];
                for (var i = 0; i < 3; i++)
                {
                    if (SystemInfo.graphicsUVStartsAtTop)
                        r[i] = new Vector2((i << 1) & 2, 1.0f - (i & 2));
                    else
                        r[i] = new Vector2((i << 1) & 2, i & 2);
                }

                return r;
            }
        }
		
        private static void _blit(CommandBuffer cmd, RTHandle from, RTHandle to, Material mat, int pass = 0)
        {
            cmd.SetGlobalTexture(s_MainTexId, from.nameID);
            cmd.SetRenderTarget(to.nameID);
            cmd.DrawMesh(k_ScreenMesh, Matrix4x4.identity, mat, 0, pass);
        }

        private static RTHandle _alloc(string id)
        {
            return RTHandles.Alloc(id, name: id);
        }
    }
}