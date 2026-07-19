#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Android 构建后为最终 Manifest 注入震动权限
/// 避免自定义主 Manifest 覆盖 Unity 当前生成配置
/// </summary>
public sealed class AndroidVibrationManifestPostprocessor : IPostGenerateGradleAndroidProject
{
    private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
    private const string VibrationPermission = "android.permission.VIBRATE";

    public int callbackOrder => 100;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifestPath = ResolveManifestPath(path);
        if (string.IsNullOrEmpty(manifestPath))
        {
            Debug.LogWarning("[MobileHaptics] 找不到 AndroidManifest.xml 无法注入震动权限");
            return;
        }

        XmlDocument document = new XmlDocument
        {
            PreserveWhitespace = true
        };
        document.Load(manifestPath);

        XmlElement root = document.DocumentElement;
        if (root == null || HasVibrationPermission(root))
        {
            return;
        }

        XmlElement permission = document.CreateElement("uses-permission");
        permission.SetAttribute("name", AndroidNamespace, VibrationPermission);
        XmlNode application = root.SelectSingleNode("application");
        root.InsertBefore(permission, application);
        document.Save(manifestPath);
    }

    private static string ResolveManifestPath(string path)
    {
        string directPath = Path.Combine(path, "src/main/AndroidManifest.xml");
        if (File.Exists(directPath))
        {
            return directPath;
        }

        string projectPath = Path.Combine(path, "unityLibrary/src/main/AndroidManifest.xml");
        return File.Exists(projectPath) ? projectPath : string.Empty;
    }

    private static bool HasVibrationPermission(XmlElement root)
    {
        XmlNodeList permissions = root.SelectNodes("uses-permission");
        if (permissions == null)
        {
            return false;
        }

        foreach (XmlNode permission in permissions)
        {
            if (permission is XmlElement element
                && element.GetAttribute("name", AndroidNamespace) == VibrationPermission)
            {
                return true;
            }
        }

        return false;
    }
}
#endif
