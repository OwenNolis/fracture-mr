using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity.VRTemplate
{
    /// <summary>
    /// URP Renderer Feature that applies the Glitch shader as a post-processing effect.
    /// Add this to your URP Renderer Data asset.
    /// </summary>
    public class OverseerGlitchFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("When to apply the glitch effect in the render pipeline.")]
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Fallback material if OverseerGlitchEffect is not found.")]
            public Material fallbackMaterial;
        }

        public Settings settings = new Settings();

        class GlitchRenderPass : ScriptableRenderPass
        {
            private RTHandle m_TempTexture;
            private Material m_FallbackMaterial;
            private static readonly int BlitTextureID = Shader.PropertyToID("_BlitTexture");

            public void SetFallbackMaterial(Material mat)
            {
                m_FallbackMaterial = mat;
            }

            private Material GetGlitchMaterial()
            {
                // Priority 1: OverseerGlitchEffect instance
                var overseerEffect = OverseerGlitchEffect.Instance;
                if (overseerEffect != null && overseerEffect.GlitchMaterial != null && overseerEffect.isActiveAndEnabled)
                {
                    return overseerEffect.GlitchMaterial;
                }

                // Priority 2: Find GlitchController in scene
                var glitchController = Object.FindFirstObjectByType<GlitchController>();
                if (glitchController != null && glitchController.material != null)
                {
                    return glitchController.material;
                }

                // Priority 3: Fallback material from settings
                return m_FallbackMaterial;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                // Setup is handled in Execute
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                Material glitchMaterial = GetGlitchMaterial();

                if (glitchMaterial == null)
                {
                    return;
                }

                // Check if we should skip (no glitch active)
                var glitchController = Object.FindFirstObjectByType<GlitchController>();
                if (glitchController != null)
                {
                    // Skip if all values are essentially zero (no visible effect)
                    if (glitchController.noiseAmount < 0.001f &&
                        glitchController.glitchStrength < 0.001f &&
                        glitchController.scanLineStrength < 0.001f)
                    {
                        return;
                    }
                }

                CommandBuffer cmd = CommandBufferPool.Get("Overseer Glitch");

                var source = renderingData.cameraData.renderer.cameraColorTargetHandle;

                RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;

                RenderingUtils.ReAllocateIfNeeded(ref m_TempTexture, desc, name: "_OverseerGlitchTemp");

                // Set the blit texture - required for URP Sample Buffer nodes with "blit" source
                cmd.SetGlobalTexture(BlitTextureID, source);

                // Also set _MainTex for compatibility with older shaders
                cmd.SetGlobalTexture("_MainTex", source);

                // Set the source texture on the material directly as well
                glitchMaterial.SetTexture(BlitTextureID, source);

                // Blit from Source to Temp with Glitch Material
                Blitter.BlitCameraTexture(cmd, source, m_TempTexture, glitchMaterial, 0);

                // Blit from Temp back to Source
                Blitter.BlitCameraTexture(cmd, m_TempTexture, source);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                m_TempTexture?.Release();
            }

            public void Dispose()
            {
                m_TempTexture?.Release();
            }
        }

        GlitchRenderPass m_ScriptablePass;

        public override void Create()
        {
            m_ScriptablePass = new GlitchRenderPass();
            m_ScriptablePass.renderPassEvent = settings.renderPassEvent;
            m_ScriptablePass.SetFallbackMaterial(settings.fallbackMaterial);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Only apply to Game and Scene cameras
            if (renderingData.cameraData.cameraType == CameraType.Game ||
                renderingData.cameraData.cameraType == CameraType.SceneView)
            {
                renderer.EnqueuePass(m_ScriptablePass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            m_ScriptablePass?.Dispose();
        }
    }
}
