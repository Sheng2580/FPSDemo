using Combat;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private static GameManager instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateBeforeSceneLoad()
    {
        // 每次进入播放都清理事件中心 兼容编辑器关闭域重载
        EventCenter.Instance.Clear();
        CombatFeedbackManager.EnsureExists();

        if (instance != null)
        {
            return;
        }

        GameObject obj = new GameObject(nameof(GameManager));
        instance = obj.AddComponent<GameManager>();
        DontDestroyOnLoad(obj);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        ApplyMobileScreenSettings();
        ApplyMobileGraphicsSettings();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            ApplyMobileScreenSettings();
            ApplyMobileGraphicsSettings();
        }
    }

    private void ApplyMobileScreenSettings()
    {
#if UNITY_ANDROID || UNITY_IOS
        // 移动端启动时强制横屏
        Screen.orientation = ScreenOrientation.LandscapeLeft;

        // 禁止手机方向传感器切回竖屏
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;

        // 保持全屏显示
        Screen.fullScreen = true;
#endif
    }

    private void ApplyMobileGraphicsSettings()
    {
#if UNITY_ANDROID || UNITY_IOS
        MobileGraphicsQuality.ApplyDefaultMobileProfile();
#endif
    }
}
