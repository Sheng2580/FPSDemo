using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public enum MobileGraphicsPreset
{
    Smooth,
    Balanced,
    Clear
}

public readonly struct MobileGraphicsQualityProfile
{
    public readonly int targetFrameRate;
    public readonly int msaaSamples;
    public readonly float renderScale;
    public readonly float lodBias;
    public readonly AnisotropicFiltering anisotropicFiltering;

    public MobileGraphicsQualityProfile(
        int targetFrameRate,
        int msaaSamples,
        float renderScale,
        float lodBias,
        AnisotropicFiltering anisotropicFiltering)
    {
        this.targetFrameRate = Mathf.Max(30, targetFrameRate);
        this.msaaSamples = Mathf.Clamp(msaaSamples, 1, 8);
        this.renderScale = Mathf.Clamp(renderScale, 0.7f, 1.2f);
        this.lodBias = Mathf.Max(0.5f, lodBias);
        this.anisotropicFiltering = anisotropicFiltering;
    }

    public static MobileGraphicsQualityProfile Create(MobileGraphicsPreset preset)
    {
        switch (preset)
        {
            case MobileGraphicsPreset.Smooth:
                return new MobileGraphicsQualityProfile(60, 1, 1f, 1f, AnisotropicFiltering.Enable);

            case MobileGraphicsPreset.Clear:
                return new MobileGraphicsQualityProfile(60, 4, 1f, 1.5f, AnisotropicFiltering.ForceEnable);

            default:
                return new MobileGraphicsQualityProfile(60, 2, 1f, 1.2f, AnisotropicFiltering.ForceEnable);
        }
    }
}

public static class MobileGraphicsQuality
{
    public static MobileGraphicsPreset CurrentPreset { get; private set; } = MobileGraphicsPreset.Balanced;

    public static void ApplyDefaultMobileProfile()
    {
        ApplyPreset(MobileGraphicsPreset.Balanced);
    }

    public static void ApplyPreset(MobileGraphicsPreset preset)
    {
        CurrentPreset = preset;
        ApplyProfile(MobileGraphicsQualityProfile.Create(preset));
    }

    public static void ApplyProfile(MobileGraphicsQualityProfile profile)
    {
        // 后续设置系统只需要修改这里传入的 Profile
        Application.targetFrameRate = profile.targetFrameRate;

        // 避免移动端把贴图整体降级导致远处地面和墙面变糊
        QualitySettings.globalTextureMipmapLimit = 0;

        // 强制斜视角纹理过滤 地面和墙面远处会更清楚
        QualitySettings.anisotropicFiltering = profile.anisotropicFiltering;

        // Built-in 管线和部分平台会读取这里 URP 管线主要读取管线资源
        QualitySettings.antiAliasing = profile.msaaSamples <= 1 ? 0 : profile.msaaSamples;
        QualitySettings.lodBias = profile.lodBias;

        // 清掉可能存在的动态缩放影响
        ScalableBufferManager.ResizeBuffers(profile.renderScale, profile.renderScale);

        ApplyUniversalRenderPipelineProfile(profile);
    }

    private static void ApplyUniversalRenderPipelineProfile(MobileGraphicsQualityProfile profile)
    {
        RenderPipelineAsset pipelineAsset = GraphicsSettings.currentRenderPipeline;
        if (pipelineAsset == null)
        {
            pipelineAsset = QualitySettings.renderPipeline;
        }

        if (pipelineAsset is not UniversalRenderPipelineAsset urpAsset)
        {
            return;
        }

        // URP 手机端真正生效的是这里的 MSAA 和 Render Scale
        urpAsset.msaaSampleCount = profile.msaaSamples;
        urpAsset.renderScale = profile.renderScale;
    }
}
