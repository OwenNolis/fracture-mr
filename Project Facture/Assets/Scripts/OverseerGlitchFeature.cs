using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity.VRTemplate
{
    public class OverseerGlitchFeature : ScriptableRendererFeature
    {
        class GlitchRenderPass : ScriptableRenderPass
        {
            private Material m_Material;
            private RTHandle m_Source;
            private RTHandle m_TempTexture;

            public void Setup(RTHandle source)
            {
                m_Source = source;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                // In modern URP, we often get the source from the renderer, but for now we'll accept it via Setup or just use cameraColorTarget
                // However, accessing cameraColorTarget directly is deprecated in newer versions.
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var overseerEffect = OverseerGlitchEffect.Instance;
                if (overseerEffect == null || overseerEffect.GlitchMaterial == null || !overseerEffect.isActiveAndEnabled)
                {
                    return;
                }

                CommandBuffer cmd = CommandBufferPool.Get("Overseer Glitch");
                
                // Fetch the latest camera target
                // Note: handling source/dest in URP varies wildly between 2020/2021/2022/6000.
                // We will use the Blitter API if available (2022+), but since we can't be sure, 
                // we'll use a standard temporary RT approach which is fairly robust.
                
                var source = renderingData.cameraData.renderer.cameraColorTargetHandle;

                RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;

                RenderingUtils.ReAllocateIfNeeded(ref m_TempTexture, desc, name: "_OverseerGlitchTemp");

                // Blit from Source to Temp with Material
                Blitter.BlitCameraTexture(cmd, source, m_TempTexture, overseerEffect.GlitchMaterial, 0);
                
                // Blit from Temp back to Source
                Blitter.BlitCameraTexture(cmd, m_TempTexture, source);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                m_TempTexture?.Release();
            }
        }

        GlitchRenderPass m_ScriptablePass;

        public override void Create()
        {
            m_ScriptablePass = new GlitchRenderPass();
            // Configurable event
            m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing; 
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Game || renderingData.cameraData.cameraType == CameraType.SceneView)
            {
                // In newer URP, we can just access cameraColorTargetHandle inside Execute, 
                // so we don't necessarily need to pass it here, but it's good practice.
                // m_ScriptablePass.Setup(renderer.cameraColorTargetHandle); 
                renderer.EnqueuePass(m_ScriptablePass);
            }
        }
    }
}
