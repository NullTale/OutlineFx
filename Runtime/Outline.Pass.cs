using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

namespace UrpOutline
{
    public partial class OutlineFeature
    {
        private static readonly int s_Alpha   = Shader.PropertyToID("_Alpha");
        private static readonly int s_MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int s_Step    = Shader.PropertyToID("_Step");
        private static readonly int s_Color   = Shader.PropertyToID("_Color");
        private static readonly int s_Solid   = Shader.PropertyToID("_Solid");
        
        private static readonly int s_AlphaTex    = Shader.PropertyToID("_AlphaTex");
        private static readonly int s_AlphaTO = Shader.PropertyToID("_AlphaTO");
        
        private class Pass : ScriptableRenderPass
        {
            public OutlineFeature _owner;
            
            private FilteringSettings       _filtering;
            private RenderStateBlock        _override;
            private RenderTarget            _buffer;
            private RTHandle                _output;

            // =======================================================================
            public void Init()
            {
                renderPassEvent = _owner._event;
                _buffer         = new RenderTarget().Allocate(nameof(_buffer));
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // allocate resources
                var cmd  = CommandBufferPool.Get(nameof(OutlineFeature));
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.colorFormat = RenderTextureFormat.ARGB32;
                _buffer.Get(cmd, desc);
                
                _owner._outlineMat.SetFloat(s_Alpha, _owner._alpha);
                _owner._outlineMat.SetFloat(s_Solid, _owner._solid);
                
                if (_owner._alphaMask._enable)
                {
                    _owner._outlineMat.SetTexture(s_AlphaTex, _owner._alphaMask._texture);
                    var xPeriod = 1f / (_owner._alphaMask._velocity.x / 1000f);
                    var yPeriod = 1f / (_owner._alphaMask._velocity.y / 1000f);
                    var xOffset = _owner._alphaMask._velocity.x == 0 ? 0 : (Time.unscaledTime % xPeriod) / xPeriod * _owner._alphaMask._scale;
                    var yOffset = _owner._alphaMask._velocity.y == 0 ? 0 : (Time.unscaledTime % yPeriod) / yPeriod * _owner._alphaMask._scale;
                    
                    var aspectTex = _owner._alphaMask._texture.width / (float)_owner._alphaMask._texture.height;
                    
                    _owner._outlineMat.SetVector(s_AlphaTO, new Vector4(_owner._alphaMask._scale * (Screen.width / (float)Screen.height) / aspectTex, _owner._alphaMask._scale, xOffset, yOffset));
                }
                
#if !UNITY_2022_1_OR_NEWER
                if (_owner._output.Enabled == false)
                    _output = RTHandles.Alloc(renderingData.cameraData.renderer.cameraColorTarget);
#else
				_output = renderingData.cameraData.renderer.cameraColorTargetHandle;
#endif
                if (_owner._output.Enabled)
                    _output = _alloc(_owner._output.Value);
                
                // render with layer mask
                cmd.SetRenderTarget(_buffer.Handle.nameID);
                cmd.ClearRenderTarget(RTClearFlags.Color, Color.clear, 1f, 0);
                
                if (_owner._attachDepth)
                {
#if !UNITY_2022_1_OR_NEWER
                    var depth = renderingData.cameraData.renderer.cameraDepthTarget == BuiltinRenderTextureType.CameraTarget
                        ? renderingData.cameraData.renderer.cameraColorTarget
                        : renderingData.cameraData.renderer.cameraDepthTarget;
#else
                    var depth = renderingData.cameraData.renderer.cameraDepthTargetHandle;
#endif
                    cmd.SetRenderTarget(_buffer.Handle, depth);
                }
                else
                {
                    cmd.SetRenderTarget(_buffer.Handle);
                }
                
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
                foreach (var inst in _renderers)
                {
                    cmd.SetGlobalTexture(s_MainTex, inst._renderer.sharedMaterial.mainTexture);
                    cmd.SetGlobalColor(s_Color, inst._color);
                    cmd.DrawRenderer(inst._renderer, _owner._outlineMat, 0, 0);
                }
                _renderers.Clear();
                
                cmd.SetGlobalVector(s_Step, _owner._step);
                _blit(_buffer.Handle, _output, _owner._outlineMat, 1);

                _execute();
				
                // -----------------------------------------------------------------------
                void _blit(RTHandle from, RTHandle to, Material mat, int pass = 0)
                {
                    OutlineFeature._blit(cmd, from, to, mat, pass);
                }

                void _execute()
                {
                    context.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                }
            }

            public override void FrameCleanup(CommandBuffer cmd)
            {
                _buffer.Release(cmd);
                
#if !UNITY_2022_1_OR_NEWER
                RTHandles.Release(_output);
#else
                if (_owner._output.Enabled)
                    RTHandles.Release(_output);
#endif
            }
        }
    }
}