using UnityEngine;
using UnityEngine.Rendering.Universal;

public class AutoExposureRendererFeature : ScriptableRendererFeature
{
    public AutoExposureRenderSettings autoExposureRenderSettings;

    private AutoExposureRenderPass autoExposureRenderPass;

    public override void Create()
    {
        autoExposureRenderPass = new AutoExposureRenderPass(autoExposureRenderSettings);
        autoExposureRenderPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(autoExposureRenderPass);
    }
}
