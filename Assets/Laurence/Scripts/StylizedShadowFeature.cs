using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class StylizedShadowFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        public Color shadowColor = new Color(0.1f, 0.1f, 0.2f, 0.85f);
        public Color lightColor = new Color(1f, 0.7f, 0.4f, 0.5f);
        public int maxLights = 16;

        [Header("Edge Wobble")]
        [Tooltip("How much the light edge wobbles in world units")]
        [Range(0f, 2f)]
        public float wobbleAmount = 0.3f;

        [Tooltip("How fast the wobble animates")]
        [Range(0f, 5f)]
        public float wobbleSpeed = 1.5f;

        [Tooltip("How many wobbles around the light edge (higher = more detailed)")]
        [Range(1f, 20f)]
        public float wobbleFrequency = 6f;

        [Header("Edge Softness")]
        [Tooltip("Width of the soft/blurry edge in world units")]
        [Range(0f, 3f)]
        public float edgeSoftness = 0.5f;

        [Header("Debug")]
        public bool debugMode = false;
    }

    public Settings settings = new Settings();
    private StylizedShadowPass shadowPass;
    private Material shadowMaterial;

    public override void Create()
    {
        Shader shader = Shader.Find("Hidden/StylizedShadowOverlay");
        if (shader == null)
        {
            Debug.LogError("StylizedShadowFeature: Could not find shader");
            return;
        }

        shadowMaterial = CoreUtils.CreateEngineMaterial(shader);
        if (shadowMaterial == null)
        {
            Debug.LogError("StylizedShadowFeature: Failed to create material");
            return;
        }

        shadowPass = new StylizedShadowPass(shadowMaterial, settings);
        shadowPass.renderPassEvent = settings.renderPassEvent;

        if (settings.debugMode)
            Debug.Log("StylizedShadowFeature: Created successfully");
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (shadowMaterial == null || shadowPass == null)
            return;
        if (renderingData.cameraData.cameraType == CameraType.Preview)
            return;

        shadowPass.Setup(settings, shadowMaterial);
        renderer.EnqueuePass(shadowPass);
    }

    protected override void Dispose(bool disposing)
    {
        if (shadowMaterial != null)
        {
            CoreUtils.Destroy(shadowMaterial);
            shadowMaterial = null;
        }
    }
}

public class StylizedShadowPass : ScriptableRenderPass
{
    private Material material;
    private StylizedShadowFeature.Settings settings;

    private static readonly int ShadowColorID = Shader.PropertyToID("_ShadowColor");
    private static readonly int LightColorID = Shader.PropertyToID("_LightColor");
    private static readonly int LightCountID = Shader.PropertyToID("_LightCount");
    private static readonly int LightPositionsID = Shader.PropertyToID("_LightPositions");
    private static readonly int LightRangesID = Shader.PropertyToID("_LightRanges");

    private static readonly int WobbleAmountID = Shader.PropertyToID("_WobbleAmount");
    private static readonly int WobbleSpeedID = Shader.PropertyToID("_WobbleSpeed");
    private static readonly int WobbleFrequencyID = Shader.PropertyToID("_WobbleFrequency");
    private static readonly int EdgeSoftnessID = Shader.PropertyToID("_EdgeSoftness");

    private Vector4[] lightPositions = new Vector4[16];
    private float[] lightRanges = new float[16];

    public StylizedShadowPass(Material material, StylizedShadowFeature.Settings settings)
    {
        this.material = material;
        this.settings = settings;
        ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
        requiresIntermediateTexture = true;
    }

    public void Setup(StylizedShadowFeature.Settings settings, Material mat)
    {
        this.settings = settings;
        this.material = mat;
        if (lightPositions.Length < settings.maxLights)
        {
            lightPositions = new Vector4[settings.maxLights];
            lightRanges = new float[settings.maxLights];
        }
    }

    private int GatherLightData()
    {
        var controller = StylizedShadowController.Instance;
        if (controller == null) return 0;

        var lights = controller.GetTrackedLights();
        int count = Mathf.Min(lights.Count, settings.maxLights);

        for (int i = 0; i < count; i++)
        {
            var ld = lights[i];
            lightPositions[i] = new Vector4(ld.position.x, ld.position.y, ld.position.z, 1f);
            lightRanges[i] = ld.range;
        }
        return count;
    }

    private class PassData
    {
        public TextureHandle source;
        public TextureHandle temp;
        public Material material;
        public int lightCount;
        public Color shadowColor;
        public Color lightColor;
        public Vector4[] lightPositions;
        public float[] lightRanges;

        public float wobbleAmount;
        public float wobbleSpeed;
        public float wobbleFrequency;
        public float edgeSoftness;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        var resourceData = frameData.Get<UniversalResourceData>();
        var cameraData = frameData.Get<UniversalCameraData>();

        if (resourceData.isActiveTargetBackBuffer)
            return;

        int lightCount = GatherLightData();

        if (settings.debugMode && Time.frameCount % 180 == 0)
            Debug.Log($"StylizedShadowPass: {lightCount} lights");

        var source = resourceData.activeColorTexture;

        var desc = cameraData.cameraTargetDescriptor;
        desc.depthBufferBits = 0;
        desc.msaaSamples = 1;

        var temp = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_TempShadow", false);

        // Use UnsafePass for full CommandBuffer control
        using (var builder = renderGraph.AddUnsafePass<PassData>("Stylized Shadow", out var passData))
        {
            passData.source = source;
            passData.temp = temp;
            passData.material = material;
            passData.lightCount = lightCount;
            passData.shadowColor = settings.shadowColor;
            passData.lightColor = settings.lightColor;
            passData.lightPositions = lightPositions;
            passData.lightRanges = lightRanges;

            passData.wobbleAmount = settings.wobbleAmount;
            passData.wobbleSpeed = settings.wobbleSpeed;
            passData.wobbleFrequency = settings.wobbleFrequency;
            passData.edgeSoftness = settings.edgeSoftness;

            builder.UseTexture(source, AccessFlags.ReadWrite);
            builder.UseTexture(temp, AccessFlags.ReadWrite);

            builder.SetRenderFunc((PassData data, UnsafeGraphContext ctx) =>
            {
                CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);

                data.material.SetColor(ShadowColorID, data.shadowColor);
                data.material.SetColor(LightColorID, data.lightColor); 
                data.material.SetInt(LightCountID, data.lightCount);

                data.material.SetFloat(WobbleAmountID, data.wobbleAmount);
                data.material.SetFloat(WobbleSpeedID, data.wobbleSpeed);
                data.material.SetFloat(WobbleFrequencyID, data.wobbleFrequency);
                data.material.SetFloat(EdgeSoftnessID, data.edgeSoftness);

                if (data.lightCount > 0)
                {
                    data.material.SetVectorArray(LightPositionsID, data.lightPositions);
                    data.material.SetFloatArray(LightRangesID, data.lightRanges);
                }

                // Blit source -> temp with effect
                Blitter.BlitCameraTexture(cmd, data.source, data.temp, data.material, 0);

                // Blit temp -> source (copy back)
                Blitter.BlitCameraTexture(cmd, data.temp, data.source);
            });
        }
    }
}