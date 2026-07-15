using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

public static class AppBrandingBuildTools
{
    private const string ProductName = "丧尸生存Demo";
    private const string IconAssetPath = "Assets/Art/Icon/App/ZombieSurvivalDemoIcon.png";

    [InitializeOnLoadMethod]
    private static void ScheduleApplyBranding()
    {
        EditorApplication.delayCall += ApplyBrandingIfNeeded;
    }

    [MenuItem("FPSDemo/Build/应用丧尸生存Demo品牌设置")]
    public static void ApplyBranding()
    {
        AssetDatabase.ImportAsset(IconAssetPath, ImportAssetOptions.ForceUpdate);
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconAssetPath);
        if (icon == null)
        {
            Debug.LogError($"[AppBranding] 无法加载应用图标 {IconAssetPath}");
            return;
        }

        PlayerSettings.productName = ProductName;
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, new[] { icon }, IconKind.Application);
        SetAndroidPlatformIcons(AndroidPlatformIconKind.Legacy, icon);
        SetAndroidPlatformIcons(AndroidPlatformIconKind.Round, icon);

        AssetDatabase.SaveAssets();
        Debug.Log($"[AppBranding] 已应用产品名与 Android 图标 ProductName={ProductName}");
    }

    private static void ApplyBrandingIfNeeded()
    {
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconAssetPath);
        Texture2D[] currentIcons = PlayerSettings.GetIconsForTargetGroup(
            BuildTargetGroup.Android,
            IconKind.Application);
        bool productNameMatches = PlayerSettings.productName == ProductName;
        bool iconMatches = currentIcons != null && currentIcons.Length > 0 && currentIcons[0] == icon;

        if (!productNameMatches || !iconMatches)
        {
            ApplyBranding();
        }
    }

    private static void SetAndroidPlatformIcons(PlatformIconKind kind, Texture2D icon)
    {
        PlatformIcon[] platformIcons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
        for (int i = 0; i < platformIcons.Length; i++)
        {
            platformIcons[i].SetTexture(icon, 0);
        }

        PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, platformIcons);
    }
}
