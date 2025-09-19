using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using static UnityEngine.Rendering.RenderGraphModule.Util.RenderGraphUtils;

public class AutoExposureRenderPass : ScriptableRenderPass
{
    private AutoExposureRenderSettings settings;
    private RWParameters[] rwParameters;
    private RParameters[] rParameters;
    private ComputeBuffer rwParameterBuffer;
    private ComputeBuffer rParameterBuffer;
    private Vector3Int numThreads = new Vector3Int(16, 16, 1);
    private int threadGroupsX;
    private int threadGroupsY;

    [StructLayout(LayoutKind.Sequential)]
    public struct RWParameters
    {
        public uint importance;
        public uint luminance;
        public float historyEV;
        public float exposure;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RParameters
    {
        public float deltaTime;
    }

    public class PassData
    {
        public ComputeShader computeShader;
        public TextureHandle screenTexture;
    }

    public AutoExposureRenderPass(AutoExposureRenderSettings autoExposureRenderSettings)
    {
        settings = autoExposureRenderSettings;

        rwParameters = new RWParameters[1];
        rwParameterBuffer = new ComputeBuffer(1, Marshal.SizeOf(typeof(RWParameters)), ComputeBufferType.Structured);
        rwParameterBuffer.SetData(rwParameters);

        rParameters = new RParameters[1];
        rParameterBuffer = new ComputeBuffer(1, Marshal.SizeOf(typeof(RParameters)), ComputeBufferType.Structured);
        rParameterBuffer.SetData(rParameters);

        threadGroupsX = Mathf.CeilToInt(Screen.width / (float)numThreads.x);
        threadGroupsY = Mathf.CeilToInt(Screen.height / (float)numThreads.y);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

        RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
        descriptor.msaaSamples = 1;
        descriptor.enableRandomWrite = true;
        descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
        descriptor.depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
        TextureHandle screenTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_ScreenTexture", false);
        renderGraph.AddBlitPass(resourceData.activeColorTexture, screenTexture, Vector2.one, Vector2.zero);

        using (var builder = renderGraph.AddComputePass("Auto Exposure", out PassData passData))
        {
            passData.computeShader = settings.computeShader;
            passData.screenTexture = screenTexture;

            builder.AllowPassCulling(false);
            builder.UseTexture(passData.screenTexture);

            builder.SetRenderFunc((PassData data, ComputeGraphContext context) =>
            {
                UpdateRParameters();

                ComputeShader computeShader = data.computeShader;
                ComputeCommandBuffer ccb = context.cmd;

                int kernelIndex = computeShader.FindKernel("AccumulateLuminance");
                ccb.SetComputeTextureParam(computeShader, kernelIndex, "_ScreenTexture", data.screenTexture);
                ccb.SetComputeBufferParam(computeShader, kernelIndex, "_RWParameters", rwParameterBuffer);
                ccb.DispatchCompute(computeShader, kernelIndex, threadGroupsX, threadGroupsY, 1);

                kernelIndex = computeShader.FindKernel("ComputeTargetEV");
                ccb.SetComputeTextureParam(computeShader, kernelIndex, "_ScreenTexture", data.screenTexture);
                ccb.SetComputeBufferParam(computeShader, kernelIndex, "_RWParameters", rwParameterBuffer);
                ccb.SetComputeBufferParam(computeShader, kernelIndex, "_RParameters", rParameterBuffer);
                ccb.DispatchCompute(computeShader, kernelIndex, 1, 1, 1);

                kernelIndex = computeShader.FindKernel("ApplyExposure");
                ccb.SetComputeTextureParam(computeShader, kernelIndex, "_ScreenTexture", data.screenTexture);
                ccb.SetComputeBufferParam(computeShader, kernelIndex, "_RWParameters", rwParameterBuffer);
                ccb.DispatchCompute(computeShader, kernelIndex, threadGroupsX, threadGroupsY, 1);
            });
        }
        resourceData.cameraColor = screenTexture;
    }

    private void UpdateRParameters()
    {
        rParameters[0].deltaTime = Time.deltaTime;
        rParameterBuffer.SetData(rParameters);
    }
}
