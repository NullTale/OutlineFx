using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

namespace UrpOutline
{
    public partial class OutlineFeature : ScriptableRendererFeature
    {
        private const string k_OutlineShader = "Hidden/Outline/Outline";
        
        private const Filter k_Filter = Filter.Box;
		
        private static readonly int s_MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int s_ColorId   = Shader.PropertyToID("_Color");
		
        private static List<ShaderTagId> k_ShaderTags;
        private static Mesh              k_ScreenMesh;
        
        public static Mesh ScreenMesh => k_ScreenMesh;


        [SerializeField]
        private RenderPassEvent _event = RenderPassEvent.AfterRenderingPostProcessing;
        [Range(0, 1)]
        [SerializeField]
        private float  _solid;

        [Range(0, 0.007f)]
        public  float  _thickness = 0.001f;
        [Range(0, 1)]
        [SerializeField]
        private float    _alphaCutout = .5f;

        public  Mode   _mode   = Mode.Hard;

        
        public bool      _attachDepth = true;
        
        public Optional<string> _output = new Optional<string>("_Tex", false);
        
        public AlphaMask _solidMask;
        
        [SerializeField]
        private ShaderCollection     _shaders = new ShaderCollection();
        
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
        public class ShaderCollection
        {
            public Shader _outline;
        }

        [Serializable]
        public class AlphaMask
        {
            public bool      _enable;
            public Texture2D _texture;
            public float     _scale = 33f;
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
            
            _validateShaders();
			
            _initMaterials();
			
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
            if (_mode == Mode.Soft)
                _step *= 2f;
            
            renderer.EnqueuePass(_pass);
        }

        public static void Render(Outline inst)
        {
            _renderers.Add(inst);
        }
        
        // =======================================================================
        private void _initMaterials()
        {
            _outlineMat = new Material(_shaders._outline);
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
            switch (k_Filter)
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
            if (_solidMask._enable)
            {
                _outlineMat.EnableKeyword("ALPHA_MASK");
            }
        }
        
        private void _validateShaders()
        {
#if UNITY_EDITOR
            _validate(ref _shaders._outline, k_OutlineShader);
			
            UnityEditor.EditorUtility.SetDirty(this);
            // -----------------------------------------------------------------------
            void _validate(ref Shader shader, string path)
            {
                if (shader != null)
                    return;
				
                shader = Shader.Find(path);
            }
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