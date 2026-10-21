using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_2023_3_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

//  OutlineFx © NullTale - https://x.com/NullTale/
namespace OutlineFx
{
    public partial class OutlineFxFeature
    {
        private static readonly int s_Alpha   = Shader.PropertyToID("_Alpha");
        private static readonly int s_MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int s_Step    = Shader.PropertyToID("_Step");
        private static readonly int s_Color   = Shader.PropertyToID("_Color");
        private static readonly int s_Solid   = Shader.PropertyToID("_Solid");
        
        private static readonly int s_AlphaTex    = Shader.PropertyToID("_AlphaTex");
        private static readonly int s_AlphaTO = Shader.PropertyToID("_AlphaTO");
        private static readonly int s_BaseMap = Shader.PropertyToID("_BaseMap");

        private class Pass : ScriptableRenderPass
        {
            public OutlineFxFeature _owner;
            
            private FilteringSettings       _filtering;
            private RenderStateBlock        _override;
            private RenderTarget            _buffer;
            private RenderTarget            _outputTex;
            private RTHandle                _output;

            // =======================================================================
            public void Init()
            {
                renderPassEvent = _owner._event;
                _buffer = new RenderTarget().Allocate(nameof(_buffer));
                if (_owner._output.Enabled)
                    _outputTex = new RenderTarget().Allocate(_owner._output.value);
            }

#if UNITY_2023_3_OR_NEWER
            // Unity 6 (URP 17) RenderGraph path.
            // URP 17 no longer calls Execute() unless Compatibility Mode is enabled; passes must instead
            // declare their resources in RecordRenderGraph() and do the actual rendering in a render function.
            // PassData carries everything the render function needs, because it runs later and must not
            // access the pass instance or per-frame URP data directly.
            private class PassData
            {
                public Material Material;
                public TextureHandle Buffer;
                public TextureHandle Target;
                public TextureHandle Depth;
                public bool AttachDepth;
                public bool ClearTarget;
                public Vector4 Step;
                public Vector4 UnitStep;
                public float Solid;
                public List<Outline> Renderers = new List<Outline>();
            }

            /// <summary>
            /// Pushes the feature settings to the outline material (same logic as the legacy Execute path).
            /// </summary>
            private void _setupMaterial()
            {
                _owner._outlineMat.SetFloat(s_Alpha, _owner._alphaCutout);
                _owner._outlineMat.SetFloat(s_Solid, _owner._solid);

                if (_owner._solidMask._enabled && _owner._solidMask._pattern != null)
                {
                    var sm = _owner._solidMask;
                    _owner._outlineMat.SetTexture(s_AlphaTex, sm._pattern);
                    var xPeriod = 1f / (sm._velocity.x / 1000f);
                    var yPeriod = 1f / (sm._velocity.y / 1000f);
                    var xOffset = sm._velocity.x == 0 ? 0 : (Time.unscaledTime % xPeriod) / xPeriod * sm._scale;
                    var yOffset = sm._velocity.y == 0 ? 0 : (Time.unscaledTime % yPeriod) / yPeriod * sm._scale;

                    var aspectTex = sm._pattern.width / (float)sm._pattern.height;

                    _owner._outlineMat.SetVector(s_AlphaTO, new Vector4(sm._scale * (Screen.width / (float)Screen.height) / aspectTex, sm._scale, xOffset, yOffset));
                }
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_owner._outlineMat == null)
                {
                    _renderers.Clear();
                    return;
                }

                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();

                // the outline is drawn on top of the camera color; it cannot be read/written when URP renders straight to the back buffer
                if (resourceData.isActiveTargetBackBuffer)
                {
                    _renderers.Clear();
                    return;
                }

                _setupMaterial();

                // the mask buffer replaces the legacy GetTemporaryRT: RenderGraph allocates and releases it automatically
                var desc = cameraData.cameraTargetDescriptor;
                desc.colorFormat = RenderTextureFormat.ARGB32;
                desc.depthBufferBits = 0;

                if (_owner._attachDepth && resourceData.activeDepthTexture.IsValid())
                {
                    // buffer must match the depth attachment, otherwise depth test is skipped and cutout edges differ
                    var depthDesc = renderGraph.GetTextureDesc(resourceData.activeDepthTexture);
                    desc.width       = depthDesc.width;
                    desc.height      = depthDesc.height;
                    desc.msaaSamples = (int)depthDesc.msaaSamples;
                }
                else
                {
                    desc.msaaSamples = 1;
                }

                var buffer = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, nameof(_buffer), true);

                // an unsafe pass is used because the pass switches render targets several times and uses DrawRenderer,
                // which is not possible in a raster pass with fixed attachments
                using (var builder = renderGraph.AddUnsafePass<PassData>(nameof(OutlineFxFeature), out var passData))
                {
                    passData.Material = _owner._outlineMat;
                    passData.Buffer = buffer;
                    passData.AttachDepth = _owner._attachDepth;
                    passData.ClearTarget = _owner._output.Enabled;
                    passData.Step = _owner._step;
                    passData.UnitStep = _owner._unitStep;
                    passData.Solid = _owner._solid;
                    // copy the outlines registered this frame, since the render function runs after recording
                    passData.Renderers.Clear();
                    passData.Renderers.AddRange(_renderers);
                    _renderers.Clear();

                    if (_owner._output.Enabled)
                    {
                        // custom output: render into a graph texture and expose it as a global texture afterwards
                        passData.Target = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, _owner._output.value, true);
                        builder.SetGlobalTextureAfterPass(passData.Target, Shader.PropertyToID(_owner._output.value));
                    }
                    else
                    {
                        // replaces cameraColorTargetHandle from the legacy path
                        passData.Target = resourceData.activeColorTexture;
                    }

                    // declare every texture accessed by the pass so RenderGraph can track dependencies
                    builder.UseTexture(passData.Buffer, AccessFlags.ReadWrite);
                    builder.UseTexture(passData.Target, AccessFlags.ReadWrite);

                    if (_owner._attachDepth)
                    {
                        passData.Depth = resourceData.activeDepthTexture;
                        builder.UseTexture(passData.Depth, AccessFlags.Read);
                    }

                    // the pass has side effects (global textures) and must never be culled by RenderGraph
                    builder.AllowPassCulling(false);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((PassData data, UnsafeGraphContext ctx) => _executeRenderGraph(data, ctx));
                }
            }

            /// <summary>
            /// RenderGraph render function: same steps as the legacy Execute (mask render, then outline blit).
            /// </summary>
            private static void _executeRenderGraph(PassData data, UnsafeGraphContext ctx)
            {
                // the native CommandBuffer is required for DrawRenderer/DrawMesh
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);

                if (data.ClearTarget)
                {
                    cmd.SetRenderTarget(data.Target);
                    cmd.ClearRenderTarget(false, true, Color.clear, 1f);
                }

                _renderAll(cmd, data.Renderers, data.Material, data.Buffer, data.AttachDepth, data.AttachDepth ? data.Depth : default(RenderTargetIdentifier),
                           data.Target, data.Step, data.UnitStep, data.Solid);
                data.Renderers.Clear();
            }
#endif

// Legacy path: used before Unity 6, or in Unity 6 when the URP_COMPATIBILITY_MODE define is set
            // (RenderGraph disabled in URP Global Settings). [Obsolete] matches the base method and avoids warnings.
#if !UNITY_2023_3_OR_NEWER || URP_COMPATIBILITY_MODE
            [System.Obsolete]
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // allocate resources
                var cmd = CommandBufferPool.Get(nameof(OutlineFxFeature));
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.colorFormat = RenderTextureFormat.ARGB32;
                _buffer.Get(cmd, desc);

                if (_owner._outlineMat == null)
                    return;

                _owner._outlineMat.SetFloat(s_Alpha, _owner._alphaCutout);
                _owner._outlineMat.SetFloat(s_Solid, _owner._solid);

                if (_owner._solidMask._enabled)
                {
                    var sm = _owner._solidMask;
                    _owner._outlineMat.SetTexture(s_AlphaTex, sm._pattern);
                    var xPeriod = 1f / (sm._velocity.x / 1000f);
                    var yPeriod = 1f / (sm._velocity.y / 1000f);
                    var xOffset = sm._velocity.x == 0 ? 0 : (Time.unscaledTime % xPeriod) / xPeriod * sm._scale;
                    var yOffset = sm._velocity.y == 0 ? 0 : (Time.unscaledTime % yPeriod) / yPeriod * sm._scale;

                    var aspectTex = sm._pattern.width / (float)sm._pattern.height;

                    _owner._outlineMat.SetVector(s_AlphaTO, new Vector4(sm._scale * (Screen.width / (float)Screen.height) / aspectTex, sm._scale, xOffset, yOffset));
                }

#if !UNITY_2022_1_OR_NEWER
                if (_owner._output.Enabled == false)
                    _output = RTHandles.Alloc(renderingData.cameraData.renderer.cameraColorTarget);
#else
                _output = renderingData.cameraData.renderer.cameraColorTargetHandle;
#endif
                if (_owner._output.Enabled)
                {
                    _outputTex.Get(cmd, desc);
                    cmd.SetRenderTarget(_outputTex.Handle);
                    cmd.ClearRenderTarget(false, true, Color.clear, 1f);
                }

                // render with layer mask
                RenderTargetIdentifier depth = default;
                if (_owner._attachDepth)
                {
#if !UNITY_2022_1_OR_NEWER
                    depth = renderingData.cameraData.renderer.cameraDepthTarget == BuiltinRenderTextureType.CameraTarget
                        ? renderingData.cameraData.renderer.cameraColorTarget
                        : renderingData.cameraData.renderer.cameraDepthTarget;
#else
                    depth = renderingData.cameraData.renderer.cameraDepthTargetHandle;
#endif
                }

                var target = _owner._output.Enabled ? _outputTex.Handle : _output;
                _renderAll(cmd, _renderers, _owner._outlineMat, _buffer.Handle, _owner._attachDepth, depth,
                           target, _owner._step, _owner._unitStep, _owner._solid);
                _renderers.Clear();

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

#endif

            private static MaterialPropertyBlock s_Props;

            /// <summary>
            /// Renders the outlines shared by both paths: one pass for all objects using the feature settings,
            /// then one pass per <see cref="OutlineFxOverrideFull"/> with custom thickness or solid fill.
            /// </summary>
            private static void _renderAll(CommandBuffer cmd, List<Outline> renderers, Material mat,
                                           RenderTargetIdentifier buffer, bool attachDepth, RenderTargetIdentifier depth,
                                           RenderTargetIdentifier target, Vector4 step, Vector4 unitStep, float solid)
            {
                _renderGroup(cmd, renderers, null, mat, buffer, attachDepth, depth, target, step, solid);

                foreach (var inst in renderers)
                {
                    if (inst is OutlineFxOverrideFull full && full != null && full.HasCustomSettings)
                    {
                        var customStep  = full._overrideThickness ? unitStep * full._thickness : step;
                        var customSolid = full._overrideSolid ? full._solid : solid;
                        _renderGroup(cmd, renderers, full, mat, buffer, attachDepth, depth, target, customStep, customSolid);
                    }
                }
            }

            /// <summary>
            /// Renders a mask (single object, or every object without custom settings when single is null)
            /// and dilates it into an outline on the target.
            /// </summary>
            private static void _renderGroup(CommandBuffer cmd, List<Outline> renderers, OutlineFxOverrideFull single, Material mat,
                                             RenderTargetIdentifier buffer, bool attachDepth, RenderTargetIdentifier depth,
                                             RenderTargetIdentifier target, Vector4 step, float solid)
            {
                if (attachDepth)
                    cmd.SetRenderTarget(buffer, depth);
                else
                    cmd.SetRenderTarget(buffer);
                cmd.ClearRenderTarget(false, true, Color.clear, 1f);

                // pass 0: render outlined renderers as a solid color into the mask buffer (alpha cutout uses _MainTex)
                var drawn = false;
                if (single != null)
                {
                    drawn = _drawMask(cmd, single, mat);
                }
                else
                {
                    foreach (var inst in renderers)
                    {
                        if (inst is OutlineFxOverrideFull full && full != null && full.HasCustomSettings)
                            continue;
                        drawn |= _drawMask(cmd, inst, mat);
                    }
                }

                if (drawn == false)
                    return;

                // pass 1: full-screen triangle that dilates the mask into an outline on the target
                s_Props ??= new MaterialPropertyBlock();
                s_Props.Clear();
                s_Props.SetFloat(s_Solid, solid);

                cmd.SetGlobalVector(s_Step, step);
                cmd.SetGlobalTexture(s_MainTex, buffer);
                cmd.SetRenderTarget(target);
                cmd.DrawMesh(k_ScreenMesh, Matrix4x4.identity, mat, 0, 1, s_Props);
            }

            private static bool _drawMask(CommandBuffer cmd, Outline inst, Material mat)
            {
                if (inst == null || inst._renderer == null)
                    return false;

                var materials = inst._renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    cmd.SetGlobalTexture(s_MainTex, _getMainTexture(materials[i]));
                    cmd.SetGlobalColor(s_Color, inst.Color);
                    cmd.DrawRenderer(inst._renderer, mat, i, 0);
                }

                return materials.Length > 0;
            }


            /// <summary>
            /// Returns the texture used for alpha cutout without logging errors when the shader has no main texture
            /// (cmd.SetGlobalTexture(s_MainTex logs an error if neither [MainTexture] nor _MainTex exists).
            /// </summary>
            private static Texture _getMainTexture(Material material)
            {
                if (material == null)
                    return Texture2D.whiteTexture;

                Texture tex = null;
                if (material.HasTexture(s_MainTex))
                    tex = material.GetTexture(s_MainTex);
                else if (material.HasTexture(s_BaseMap))
                    tex = material.GetTexture(s_BaseMap);

                return tex != null ? tex : Texture2D.whiteTexture;
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                _buffer.Release(cmd);
/*                
#if !UNITY_2022_1_OR_NEWER
                RTHandles.Release(_output);
#else
                if (_owner._output.Enabled)
                    RTHandles.Release(_output);
#endif*/
            }
        }
    }
}
