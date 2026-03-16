using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using Unity.Collections;
using System.Collections.Generic;

/// <summary>
/// URP Renderer Feature that applies the hard-edge shadow overlay as a fullscreen post-process pass.
/// The shadow overlay only affects pixels belonging to objects on the occluderLayers mask.
/// </summary>
public class HardShadowFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Header("Colors")]
        public Color shadowColor = new Color(0.05f, 0.05f, 0.1f, 0.9f);
        public Color lightColor  = new Color(1f, 0.85f, 0.6f, 0.4f);

        [Header("Edge Wobble")]
        [Range(0f, 2f)]  public float wobbleAmount    = 0.3f;
        [Range(0f, 5f)]  public float wobbleSpeed     = 1.5f;
        [Range(1f, 20f)] public float wobbleFrequency = 6f;

        [Header("Edge Softness")]
        [Range(0f, 3f)]  public float edgeSoftness = 0.5f;

        [Header("Occluder Layers")]
        [Tooltip("Only pixels belonging to objects on these layers will receive the shadow overlay.")]
        public LayerMask occluderLayers = 0;

        [Header("Misc")]
        public int maxLights = 16;
        public bool debugMode = false;
    }

    public Settings settings = new Settings();

    private HardShadowOccluderMaskPass maskPass;
    private HardShadowOverlayPass      overlayPass;
    private Material overlayMaterial;
    private Material maskMaterial;

    public override void Create()
    {
        Shader overlayShader = Shader.Find("Hidden/HardShadowOverlay");
        if (overlayShader == null)
        {
            Debug.LogError("HardShadowFeature: Cannot find shader 'Hidden/HardShadowOverlay'.");
            return;
        }

        Shader maskShader = Shader.Find("Hidden/HardShadowOccluderMask");
        if (maskShader == null)
        {
            Debug.LogError("HardShadowFeature: Cannot find shader 'Hidden/HardShadowOccluderMask'.");
            return;
        }

        overlayMaterial = CoreUtils.CreateEngineMaterial(overlayShader);
        maskMaterial    = CoreUtils.CreateEngineMaterial(maskShader);

        maskPass    = new HardShadowOccluderMaskPass(maskMaterial, settings);
        overlayPass = new HardShadowOverlayPass(overlayMaterial, settings);

        maskPass.renderPassEvent    = settings.renderPassEvent - 1;
        overlayPass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (overlayMaterial == null || maskMaterial == null)
            return;
        if (renderingData.cameraData.cameraType == CameraType.Preview)
            return;

        renderer.EnqueuePass(maskPass);
        renderer.EnqueuePass(overlayPass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(overlayMaterial);
        CoreUtils.Destroy(maskMaterial);
    }
    
    // Pass 1: Render occluder-layer geometry into a single-channel mask texture.
    private class HardShadowOccluderMaskPass : ScriptableRenderPass
    {
        private readonly Material material;
        private readonly Settings settings;

        public static TextureHandle MaskTexture;

        public HardShadowOccluderMaskPass(Material mat, Settings s)
        {
            material = mat;
            settings = s;
        }

        private class PassData
        {
            public RendererListHandle rendererList;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData  = frameData.Get<UniversalResourceData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var cameraData    = frameData.Get<UniversalCameraData>();

            var desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples     = 1;
            desc.colorFormat     = RenderTextureFormat.R8;

            MaskTexture = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, desc, "_HardShadowOccluderMask", true);

            var filterSettings = new FilteringSettings(RenderQueueRange.opaque, settings.occluderLayers);

            var sortingSettings = new SortingSettings(cameraData.camera)
            {
                criteria = SortingCriteria.CommonOpaque
            };

            var drawSettings = new DrawingSettings(new ShaderTagId("UniversalForward"), sortingSettings)
            {
                overrideMaterial          = material,
                overrideMaterialPassIndex = 0
            };

            var rlParams     = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);
            var rendererList = renderGraph.CreateRendererList(rlParams);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("HardShadow_OccluderMask", out var passData))
            {
                passData.rendererList = rendererList;

                builder.SetRenderAttachment(MaskTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                builder.UseRendererList(rendererList);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.ClearRenderTarget(false, true, Color.black);
                    ctx.cmd.DrawRendererList(data.rendererList);
                });
            }
        }
    }
    
    // Pass 2: Fullscreen overlay — only darkens occluder-mask pixels.
    private class HardShadowOverlayPass : ScriptableRenderPass
    {
        private const string PassName = "HardShadowOverlay";

        private static readonly int ShadowColorID    = Shader.PropertyToID("_ShadowColor");
        private static readonly int LightColorID     = Shader.PropertyToID("_LightColor");
        private static readonly int LightCountID     = Shader.PropertyToID("_LightCount");
        private static readonly int LightPositionsID = Shader.PropertyToID("_LightPositions");
        private static readonly int LightRangesID    = Shader.PropertyToID("_LightRanges");
        private static readonly int LightIndicesID   = Shader.PropertyToID("_LightIndices");
        private static readonly int WobbleAmountID   = Shader.PropertyToID("_WobbleAmount");
        private static readonly int WobbleSpeedID    = Shader.PropertyToID("_WobbleSpeed");
        private static readonly int WobbleFreqID     = Shader.PropertyToID("_WobbleFrequency");
        private static readonly int EdgeSoftnessID   = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int OccluderMaskID   = Shader.PropertyToID("_HardShadowOccluderMask");

        private readonly Material material;
        private readonly Settings settings;

        private readonly Vector4[] lightPositions;
        private readonly float[]   lightRanges;
        private readonly float[]   lightIndices;

        public HardShadowOverlayPass(Material mat, Settings s)
        {
            material                    = mat;
            settings                    = s;
            lightPositions              = new Vector4[s.maxLights];
            lightRanges                 = new float[s.maxLights];
            lightIndices                = new float[s.maxLights];
            requiresIntermediateTexture = true;
        }

        private class PassData
        {
            public TextureHandle source;
            public TextureHandle temp;
            public TextureHandle occluderMask;
            public Material material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData   = frameData.Get<UniversalCameraData>();

            int lightCount = GatherAndUploadLights(frameData);

            if (settings.debugMode && Time.frameCount % 180 == 0)
                Debug.Log($"HardShadowPass: {lightCount} lights");

            var desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples     = 1;

            var source = resourceData.activeColorTexture;
            var temp   = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_HardShadowTemp", false);

            using (var builder = renderGraph.AddUnsafePass<PassData>(PassName, out var passData))
            {
                passData.source       = source;
                passData.temp         = temp;
                passData.occluderMask = HardShadowOccluderMaskPass.MaskTexture;
                passData.material     = material;

                builder.UseTexture(source,                  AccessFlags.ReadWrite);
                builder.UseTexture(temp,                    AccessFlags.ReadWrite);
                builder.UseTexture(passData.occluderMask,   AccessFlags.Read);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext ctx) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                    data.material.SetTexture(OccluderMaskID, data.occluderMask);
                    Blitter.BlitCameraTexture(cmd, data.source, data.temp, data.material, 0);
                    Blitter.BlitCameraTexture(cmd, data.temp, data.source);
                });
            }
        }

        /// <summary>
        /// Builds per-frame light arrays, resolves each light's URP additional light index,
        /// and uploads all shader properties.
        /// </summary>
        private int GatherAndUploadLights(ContextContainer frameData)
        {
            IReadOnlyCollection<LightSource> sources = HardShadowManager.GetLights();
            var indexLookup = BuildURPIndexLookup(frameData);

            int count = 0;
            foreach (LightSource src in sources)
            {
                if (count >= settings.maxLights) break;
                if (!src.isActiveAndEnabled) continue;

                Light unityLight = src.GetLight();
                if (unityLight == null) continue;

                lightPositions[count] = src.transform.position;
                lightRanges[count]    = src.GetRange();
                lightIndices[count]   = indexLookup.TryGetValue(unityLight, out int idx) ? idx : -1f;
                count++;
            }

            material.SetColor(ShadowColorID,          settings.shadowColor);
            material.SetColor(LightColorID,           settings.lightColor);
            material.SetInt(LightCountID,             count);
            material.SetVectorArray(LightPositionsID, lightPositions);
            material.SetFloatArray(LightRangesID,     lightRanges);
            material.SetFloatArray(LightIndicesID,    lightIndices);
            material.SetFloat(WobbleAmountID,         settings.wobbleAmount);
            material.SetFloat(WobbleSpeedID,          settings.wobbleSpeed);
            material.SetFloat(WobbleFreqID,           settings.wobbleFrequency);
            material.SetFloat(EdgeSoftnessID,         settings.edgeSoftness);

            return count;
        }

        /// <summary>
        /// Maps each visible Unity Light to its URP additional light index (directional lights excluded).
        /// </summary>
        private Dictionary<Light, int> BuildURPIndexLookup(ContextContainer frameData)
        {
            var lookup        = new Dictionary<Light, int>();
            var renderingData = frameData.Get<UniversalRenderingData>();

            NativeArray<VisibleLight> visibleLights = renderingData.cullResults.visibleLights;
            int additionalIndex = 0;

            for (int i = 0; i < visibleLights.Length; i++)
            {
                VisibleLight vl = visibleLights[i];
                if (vl.lightType == LightType.Directional)
                    continue;
                if (vl.light != null)
                    lookup[vl.light] = additionalIndex;
                additionalIndex++;
            }

            return lookup;
        }
    }
}