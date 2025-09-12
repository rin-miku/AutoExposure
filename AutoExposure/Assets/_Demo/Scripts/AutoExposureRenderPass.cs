using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class AutoExposureRenderPass : ScriptableRenderPass
{
    private AutoExposureRenderSettings settings;
    private AutoExposureParameters[] parameters;
    private ComputeBuffer computeBuffer;
    private Vector3Int numThreads = new Vector3Int(16, 16, 1);
    

    public struct AutoExposureParameters
    {
        uint importance;
        uint luminance;
    }

    public class PassData
    {
        public ComputeShader computeShader;
        public TextureHandle screenTexture;
    }

    public AutoExposureRenderPass(AutoExposureRenderSettings autoExposureRenderSettings)
    {
        settings = autoExposureRenderSettings;

        parameters = new AutoExposureParameters[1];
        computeBuffer = new ComputeBuffer(1, Marshal.SizeOf(typeof(AutoExposureParameters)));
        computeBuffer.SetData(parameters);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

        RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
        descriptor.enableRandomWrite = true;
        descriptor.colorFormat = RenderTextureFormat.ARGB32;
        descriptor.depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
        TextureHandle screenTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_ScreenTexture", false);
        renderGraph.AddBlitPass(resourceData.cameraColor, screenTexture, Vector2.one, Vector2.zero);

        using (var builder = renderGraph.AddComputePass("Auto Exposure", out PassData passData))
        {
            passData.computeShader = settings.computeShader;
            passData.screenTexture = screenTexture;

            builder.AllowPassCulling(false);
            builder.UseTexture(passData.screenTexture);

            builder.SetRenderFunc((PassData data, ComputeGraphContext context) =>
            {
                ComputeShader computeShader = data.computeShader;
                ComputeCommandBuffer ccb = context.cmd;

                int kernelIndex = computeShader.FindKernel("AccumulateLuminance");
                ccb.SetComputeTextureParam(computeShader, kernelIndex, "_ScreenTexture", data.screenTexture);
                ccb.SetComputeBufferParam(computeShader, kernelIndex, "_Parameters", computeBuffer);
                ccb.DispatchCompute(computeShader, kernelIndex, Screen.width / numThreads.x, Screen.height / numThreads.y, numThreads.z);
            });
        }

        resourceData.cameraColor = screenTexture;
    }
}
