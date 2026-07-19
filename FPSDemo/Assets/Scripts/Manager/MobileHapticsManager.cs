using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 移动端震动反馈管理器
/// 统一处理爆炸震动 距离衰减和连续触发节流
/// </summary>
public sealed class MobileHapticsManager : UnitySingleTonMono<MobileHapticsManager>
{
    private const string HapticsEnabledPreferenceKey = "MobileHapticsEnabled";

    [Header("震动设置")]
    [SerializeField] private bool hapticsEnabled = true;
    [SerializeField] private float minimumTriggerInterval = 0.15f;
    [SerializeField] private int minimumDurationMilliseconds = 42;
    [SerializeField] private int maximumDurationMilliseconds = 105;
    [SerializeField] private int minimumAmplitude = 64;
    [SerializeField] private int maximumAmplitude = 255;

    private Transform _listenerTransform;
    private float _nextAllowedTriggerTime;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject _vibrator;
    private int _androidSdkVersion;
    private bool _initializationAttempted;
    private bool _vibratorAvailable;
    private bool _hasAmplitudeControl;
    private bool _failureLogged;
#endif

    public bool HapticsEnabled => hapticsEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        _ = Instance;
    }

    public override void Awake()
    {
        base.Awake();
        if (Instance != this)
        {
            return;
        }

        hapticsEnabled = PlayerPrefs.GetInt(
            HapticsEnabledPreferenceKey,
            hapticsEnabled ? 1 : 0) != 0;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EventCenter.Instance.AddEventListener<ExplosionOccurredEventData>(
            GameEvent.ExplosionOccurred,
            OnExplosionOccurred);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EventCenter.Instance.RemoveEventListener<ExplosionOccurredEventData>(
            GameEvent.ExplosionOccurred,
            OnExplosionOccurred);
        CancelVibration();
    }

    protected override void OnDestroy()
    {
        CancelVibration();
#if UNITY_ANDROID && !UNITY_EDITOR
        _vibrator?.Dispose();
        _vibrator = null;
#endif
        base.OnDestroy();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            CancelVibration();
        }
    }

    public void SetHapticsEnabled(bool enabled)
    {
        hapticsEnabled = enabled;
        PlayerPrefs.SetInt(HapticsEnabledPreferenceKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        if (!enabled)
        {
            CancelVibration();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _listenerTransform = null;
        _nextAllowedTriggerTime = 0f;
        CancelVibration();
    }

    private void OnExplosionOccurred(ExplosionOccurredEventData eventData)
    {
        if (!hapticsEnabled || Time.unscaledTime < _nextAllowedTriggerTime)
        {
            return;
        }

        Transform listener = ResolveListenerTransform();
        if (listener == null)
        {
            return;
        }

        float distance = Vector3.Distance(listener.position, eventData.position);
        float distanceFactor = Mathf.Clamp01(1f - distance / eventData.maxDistance);
        distanceFactor = distanceFactor * distanceFactor * (3f - 2f * distanceFactor);
        float resolvedIntensity = Mathf.Clamp01(eventData.intensity * distanceFactor);
        if (resolvedIntensity <= 0.02f)
        {
            return;
        }

        int duration = Mathf.RoundToInt(Mathf.Lerp(
            minimumDurationMilliseconds,
            maximumDurationMilliseconds,
            resolvedIntensity));
        int amplitude = Mathf.RoundToInt(Mathf.Lerp(
            minimumAmplitude,
            maximumAmplitude,
            resolvedIntensity));

        if (TryVibrate(duration, amplitude))
        {
            _nextAllowedTriggerTime = Time.unscaledTime + Mathf.Max(0.02f, minimumTriggerInterval);
        }
    }

    private Transform ResolveListenerTransform()
    {
        if (_listenerTransform != null)
        {
            return _listenerTransform;
        }

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            _listenerTransform = player.transform;
            return _listenerTransform;
        }

        Camera mainCamera = Camera.main;
        _listenerTransform = mainCamera != null ? mainCamera.transform : null;
        return _listenerTransform;
    }

    private bool TryVibrate(int durationMilliseconds, int amplitude)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!TryInitializeAndroidVibrator())
        {
            return false;
        }

        try
        {
            long duration = Math.Max(1, durationMilliseconds);
            if (_androidSdkVersion >= 26)
            {
                using AndroidJavaClass effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                int resolvedAmplitude = _hasAmplitudeControl ? Mathf.Clamp(amplitude, 1, 255) : -1;
                using AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>(
                    "createOneShot",
                    duration,
                    resolvedAmplitude);
                _vibrator.Call("vibrate", effect);
            }
            else
            {
                _vibrator.Call("vibrate", duration);
            }

            return true;
        }
        catch (Exception exception)
        {
            LogAndroidFailure(exception);
            return false;
        }
#else
        return false;
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private bool TryInitializeAndroidVibrator()
    {
        if (_initializationAttempted)
        {
            return _vibratorAvailable;
        }

        _initializationAttempted = true;
        try
        {
            using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            _vibrator = activity?.Call<AndroidJavaObject>("getSystemService", "vibrator");
            _vibratorAvailable = _vibrator != null && _vibrator.Call<bool>("hasVibrator");
            if (!_vibratorAvailable)
            {
                return false;
            }

            using AndroidJavaClass versionClass = new AndroidJavaClass("android.os.Build$VERSION");
            _androidSdkVersion = versionClass.GetStatic<int>("SDK_INT");
            _hasAmplitudeControl = _androidSdkVersion >= 26
                                   && _vibrator.Call<bool>("hasAmplitudeControl");
            return true;
        }
        catch (Exception exception)
        {
            _vibratorAvailable = false;
            LogAndroidFailure(exception);
            return false;
        }
    }

    private void LogAndroidFailure(Exception exception)
    {
        if (_failureLogged)
        {
            return;
        }

        _failureLogged = true;
        Debug.LogWarning($"[MobileHaptics] Android 震动调用失败 {exception.Message}", this);
    }
#endif

    private void CancelVibration()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_vibrator == null)
        {
            return;
        }

        try
        {
            _vibrator.Call("cancel");
        }
        catch (Exception exception)
        {
            LogAndroidFailure(exception);
        }
#endif
    }
}
