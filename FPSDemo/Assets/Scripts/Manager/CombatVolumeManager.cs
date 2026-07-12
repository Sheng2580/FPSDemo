using System.Collections;
using Combat;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 战斗画面后处理管理器
/// 统一管理 CombatVolume 并监听战斗事件播放屏幕反馈
/// </summary>
[DisallowMultipleComponent]
public class CombatVolumeManager : MonoBehaviour
{
    private const string DefaultCombatVolumeName = "CombatVolume";

    private static CombatVolumeManager instance;
    private static bool instanceWasAutoCreated;

    [Header("Combat Volume")]
    [SerializeField] private string combatVolumeName = DefaultCombatVolumeName;
    [SerializeField] private Volume combatVolume;
    [SerializeField] private bool createRuntimeVolumeIfMissing = true;
    [SerializeField] private bool enablePostProcessingOnCameras = true;
    [SerializeField] private bool debugLog;

    [Header("玩家受伤效果配置")]
    [SerializeField] private CombatVolumeEffectConfigAsset playerDamageConfigAsset;
    [SerializeField] private string playerDamageConfigResourcePath = "CombatVolumeEffectConfigs/PlayerDamageVolumeEffectConfig";

    private VolumeProfile runtimeProfile;
    private CombatVolumeEffectConfig playerDamageConfig;
    private Vignette vignette;
    private ColorAdjustments colorAdjustments;
    private Bloom bloom;

    private bool hasSnapshot;
    private float baseVignetteIntensity;
    private float baseVignetteSmoothness;
    private Color baseVignetteColor;
    private float basePostExposure;
    private float baseSaturation;
    private Color baseColorFilter;
    private float baseBloomIntensity;
    private Color baseBloomTint;

    private float damageTarget;
    private float damageBlend;
    private float damageHoldEndTime;
    private Coroutine damagePulseRoutine;
    private Coroutine cameraRefreshRoutine;
    private bool usingRuntimeCreatedVolume;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        instance = null;
        instanceWasAutoCreated = false;
    }

    public static CombatVolumeManager EnsureExists()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<CombatVolumeManager>();
        if (instance != null)
        {
            return instance;
        }

        GameObject obj = new GameObject(nameof(CombatVolumeManager));
        instanceWasAutoCreated = true;
        return obj.AddComponent<CombatVolumeManager>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAfterSceneLoad()
    {
        EnsureExists();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            if (instanceWasAutoCreated && instance != null)
            {
                Destroy(instance.gameObject);
                instance = this;
                instanceWasAutoCreated = false;
            }
            else
            {
                Destroy(this);
                return;
            }
        }
        else
        {
            instance = this;
            instanceWasAutoCreated = gameObject.name == nameof(CombatVolumeManager);
        }

        if (gameObject.name == nameof(CombatVolumeManager))
        {
            DontDestroyOnLoad(gameObject);
        }

        if (combatVolume == null && TryGetComponent(out Volume ownVolume))
        {
            combatVolume = ownVolume;
        }

        EnsurePlayerDamageConfig();
        EnsureCombatVolume(false);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EventCenter.Instance.AddEventListener<PlayerDamagedEventData>(GameEvent.PlayerDamaged, OnPlayerDamaged);
        EnsurePlayerDamageConfig();
        EnsureCombatVolume(false);
        StartCameraRefreshRoutine();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EventCenter.Instance.RemoveEventListener<PlayerDamagedEventData>(GameEvent.PlayerDamaged, OnPlayerDamaged);
        StopDamagePulseRoutine();
        StopCameraRefreshRoutine();
        RestoreBaseValues();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            instanceWasAutoCreated = false;
        }
    }

    private void Reset()
    {
        if (combatVolume == null && TryGetComponent(out Volume ownVolume))
        {
            combatVolume = ownVolume;
        }
    }

    private void OnValidate()
    {
        if (combatVolume == null)
        {
            combatVolume = GetComponent<Volume>();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (combatVolume == null || usingRuntimeCreatedVolume)
        {
            if (usingRuntimeCreatedVolume && combatVolume != null)
            {
                Destroy(combatVolume.gameObject);
            }

            combatVolume = null;
            runtimeProfile = null;
            hasSnapshot = false;
            usingRuntimeCreatedVolume = false;
        }

        EnsurePlayerDamageConfig();
        EnsureCombatVolume();
        RefreshCameraPostProcessing();
    }

    public void PlayPlayerDamagePulse(float intensity)
    {
        float safeIntensity = Mathf.Clamp01(intensity);
        CombatVolumeEffectConfig config = EnsurePlayerDamageConfig();
        EnsureCombatVolume();
        if (config == null || combatVolume == null || !hasSnapshot)
        {
            return;
        }

        damageTarget = Mathf.Max(damageTarget, safeIntensity);
        damageHoldEndTime = Mathf.Max(damageHoldEndTime, Time.unscaledTime + config.holdTime);

        if (damagePulseRoutine == null)
        {
            damagePulseRoutine = StartCoroutine(DamagePulseRoutine());
        }

        if (debugLog)
        {
            Debug.Log($"[CombatVolume] 玩家受伤画面反馈 Intensity={safeIntensity:0.00}", this);
        }
    }

    private void OnPlayerDamaged(PlayerDamagedEventData eventData)
    {
        CombatVolumeEffectConfig config = EnsurePlayerDamageConfig();
        if (config == null)
        {
            return;
        }

        float damagePulse = eventData.damage * config.damageToIntensityScale;
        float missingHpRate = 1f - Mathf.Clamp01(eventData.currentHp / (float)eventData.maxHp);
        float finalPulse = Mathf.Clamp01(Mathf.Max(config.minIntensity, damagePulse) + missingHpRate * config.missingHpIntensityScale);
        PlayPlayerDamagePulse(finalPulse);
    }

    private CombatVolumeEffectConfig EnsurePlayerDamageConfig()
    {
        if (playerDamageConfig != null)
        {
            return playerDamageConfig;
        }

        if (playerDamageConfigAsset == null && !string.IsNullOrEmpty(playerDamageConfigResourcePath))
        {
            playerDamageConfigAsset = Resources.Load<CombatVolumeEffectConfigAsset>(playerDamageConfigResourcePath);
        }

        playerDamageConfig = playerDamageConfigAsset != null
            ? playerDamageConfigAsset.CreateRuntimeConfig()
            : CombatVolumeEffectConfig.CreateDefaultPlayerDamage();
        playerDamageConfig.ApplyMissingDefaults();

        if (playerDamageConfig.effectType != CombatVolumeEffectType.PlayerDamage && debugLog)
        {
            Debug.LogWarning(
                $"[CombatVolume] 玩家受伤配置类型不是 PlayerDamage 当前类型={playerDamageConfig.effectType}",
                this);
        }

        return playerDamageConfig;
    }

    private void EnsureCombatVolume(bool allowRuntimeCreate = true)
    {
        if (combatVolume == null)
        {
            combatVolume = FindCombatVolume();
        }

        if (combatVolume == null && createRuntimeVolumeIfMissing && allowRuntimeCreate)
        {
            combatVolume = CreateRuntimeCombatVolume();
        }

        if (combatVolume == null)
        {
            return;
        }

        combatVolume.isGlobal = true;
        combatVolume.weight = Mathf.Max(0.01f, combatVolume.weight);
        EnsureRuntimeProfile();
        EnsureVolumeOverrides();
    }

    private Volume FindCombatVolume()
    {
        if (TryGetComponent(out Volume ownVolume))
        {
            return ownVolume;
        }

        if (!string.IsNullOrEmpty(combatVolumeName))
        {
            GameObject namedObject = GameObject.Find(combatVolumeName);
            if (namedObject != null && namedObject.TryGetComponent(out Volume namedVolume))
            {
                return namedVolume;
            }
        }

        Volume[] volumes = FindObjectsOfType<Volume>(true);
        Volume bestVolume = null;
        float bestPriority = float.MinValue;
        for (int i = 0; i < volumes.Length; i++)
        {
            Volume candidate = volumes[i];
            if (candidate == null || !candidate.isGlobal)
            {
                continue;
            }

            if (candidate.priority > bestPriority)
            {
                bestPriority = candidate.priority;
                bestVolume = candidate;
            }
        }

        return bestVolume;
    }

    private Volume CreateRuntimeCombatVolume()
    {
        GameObject volumeObject = new GameObject(string.IsNullOrEmpty(combatVolumeName) ? DefaultCombatVolumeName : combatVolumeName);
        volumeObject.transform.SetParent(transform);
        volumeObject.transform.localPosition = Vector3.zero;
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 50f;
        volume.weight = 1f;
        volume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        usingRuntimeCreatedVolume = true;
        return volume;
    }

    private void EnsureRuntimeProfile()
    {
        if (runtimeProfile != null)
        {
            return;
        }

        runtimeProfile = combatVolume.profile;
        if (runtimeProfile == null)
        {
            runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            combatVolume.sharedProfile = runtimeProfile;
        }
    }

    private void EnsureVolumeOverrides()
    {
        if (runtimeProfile == null)
        {
            return;
        }

        if (!runtimeProfile.TryGet(out vignette))
        {
            vignette = runtimeProfile.Add<Vignette>(true);
        }

        if (!runtimeProfile.TryGet(out colorAdjustments))
        {
            colorAdjustments = runtimeProfile.Add<ColorAdjustments>(true);
        }

        runtimeProfile.TryGet(out bloom);
        vignette.active = true;
        colorAdjustments.active = true;
        vignette.color.overrideState = true;
        vignette.intensity.overrideState = true;
        vignette.smoothness.overrideState = true;
        colorAdjustments.postExposure.overrideState = true;
        colorAdjustments.saturation.overrideState = true;
        colorAdjustments.colorFilter.overrideState = true;

        if (bloom != null)
        {
            bloom.intensity.overrideState = true;
            bloom.tint.overrideState = true;
        }

        SnapshotBaseValues();
    }

    private void SnapshotBaseValues()
    {
        if (hasSnapshot || vignette == null || colorAdjustments == null)
        {
            return;
        }

        baseVignetteIntensity = vignette.intensity.value;
        baseVignetteSmoothness = vignette.smoothness.value;
        baseVignetteColor = vignette.color.value;
        basePostExposure = colorAdjustments.postExposure.value;
        baseSaturation = colorAdjustments.saturation.value;
        baseColorFilter = colorAdjustments.colorFilter.value;

        if (bloom != null)
        {
            baseBloomIntensity = bloom.intensity.value;
            baseBloomTint = bloom.tint.value;
        }

        hasSnapshot = true;
    }

    private IEnumerator DamagePulseRoutine()
    {
        while (true)
        {
            while (damageTarget > 0f)
            {
                if (combatVolume == null || !hasSnapshot)
                {
                    EnsureCombatVolume();
                }

                CombatVolumeEffectConfig config = EnsurePlayerDamageConfig();
                if (config == null)
                {
                    yield break;
                }

                float target = Mathf.Clamp01(damageTarget);
                yield return FadeDamageAmount(damageBlend, target, config.fadeInTime);

                while (Time.unscaledTime < damageHoldEndTime)
                {
                    if (damageTarget > target + 0.01f)
                    {
                        break;
                    }

                    SetDamageAmount(target);
                    yield return null;
                }

                if (damageTarget <= target + 0.01f)
                {
                    damageTarget = 0f;
                }
            }

            CombatVolumeEffectConfig fadeOutConfig = EnsurePlayerDamageConfig();
            yield return FadeDamageAmount(damageBlend, 0f, fadeOutConfig != null ? fadeOutConfig.fadeOutTime : 0.01f);

            if (damageTarget <= 0f)
            {
                break;
            }
        }

        damagePulseRoutine = null;
    }

    private void StopDamagePulseRoutine()
    {
        if (damagePulseRoutine == null)
        {
            return;
        }

        StopCoroutine(damagePulseRoutine);
        damagePulseRoutine = null;
        damageTarget = 0f;
        damageBlend = 0f;
    }

    private IEnumerator FadeDamageAmount(float from, float to, float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (to <= 0f && damageTarget > 0f)
            {
                yield break;
            }

            if (to > 0f && damageTarget > to + 0.01f)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Smooth01(elapsed / duration);
            SetDamageAmount(Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetDamageAmount(to);
    }

    private void SetDamageAmount(float amount)
    {
        damageBlend = Mathf.Clamp01(amount);
        ApplyVolumeValues();
    }

    private float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private void ApplyVolumeValues()
    {
        CombatVolumeEffectConfig config = EnsurePlayerDamageConfig();
        if (config == null || !hasSnapshot || vignette == null || colorAdjustments == null)
        {
            return;
        }

        float amount = Smooth01(damageBlend);
        vignette.intensity.value = Mathf.Clamp01(baseVignetteIntensity + config.vignetteIntensityBoost * amount);
        vignette.smoothness.value = Mathf.Clamp01(baseVignetteSmoothness + config.vignetteSmoothnessBoost * amount);
        vignette.color.value = Color.Lerp(baseVignetteColor, config.vignetteColor, amount);
        colorAdjustments.postExposure.value = basePostExposure + config.postExposureOffset * amount;
        colorAdjustments.saturation.value = baseSaturation + config.saturationOffset * amount;
        colorAdjustments.colorFilter.value = Color.Lerp(baseColorFilter, config.colorFilter, amount);

        if (bloom != null && config.enableBloomPulse)
        {
            bloom.intensity.value = Mathf.Max(0f, baseBloomIntensity + config.bloomIntensityBoost * amount);
            bloom.tint.value = Color.Lerp(baseBloomTint, config.colorFilter, amount * config.bloomTintBlend);
        }
    }

    private void RestoreBaseValues()
    {
        if (!hasSnapshot || vignette == null || colorAdjustments == null)
        {
            return;
        }

        vignette.intensity.value = baseVignetteIntensity;
        vignette.smoothness.value = baseVignetteSmoothness;
        vignette.color.value = baseVignetteColor;
        colorAdjustments.postExposure.value = basePostExposure;
        colorAdjustments.saturation.value = baseSaturation;
        colorAdjustments.colorFilter.value = baseColorFilter;

        if (bloom != null)
        {
            bloom.intensity.value = baseBloomIntensity;
            bloom.tint.value = baseBloomTint;
        }
    }

    private void StartCameraRefreshRoutine()
    {
        if (cameraRefreshRoutine != null)
        {
            return;
        }

        cameraRefreshRoutine = StartCoroutine(CameraRefreshRoutine());
    }

    private IEnumerator CameraRefreshRoutine()
    {
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(1f);
        while (enabled)
        {
            RefreshCameraPostProcessing();
            yield return wait;
        }

        cameraRefreshRoutine = null;
    }

    private void StopCameraRefreshRoutine()
    {
        if (cameraRefreshRoutine == null)
        {
            return;
        }

        StopCoroutine(cameraRefreshRoutine);
        cameraRefreshRoutine = null;
    }

    private void RefreshCameraPostProcessing()
    {
        if (!enablePostProcessingOnCameras)
        {
            return;
        }

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null)
            {
                continue;
            }

            UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = true;
            }
        }
    }
}
