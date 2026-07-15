using System.Collections;
using System.Collections.Generic;
using Combat;
using PlayerData;
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
    private const string DefaultSpeedLinesComponentName = "SpeedLines";

    private static CombatVolumeManager instance;
    private static bool instanceWasAutoCreated;

    [Header("Combat Volume")]
    [SerializeField] private string combatVolumeName = DefaultCombatVolumeName;
    [SerializeField] private Volume combatVolume;
    [SerializeField] private bool createRuntimeVolumeIfMissing = true;
    [SerializeField] private bool enablePostProcessingOnCameras = true;
    [SerializeField] private bool debugLog;
    [SerializeField] private string speedLinesComponentName = DefaultSpeedLinesComponentName;

    [Header("玩家受伤效果配置")]
    [SerializeField] private CombatVolumeEffectConfigAsset playerDamageConfigAsset;
    [SerializeField] private string playerDamageConfigResourcePath = "CombatVolumeEffectConfigs/PlayerDamageVolumeEffectConfig";

    [Header("技能后处理配置")]
    [SerializeField] private CombatVolumeEffectConfigAsset dodgeConfigAsset;
    [SerializeField] private string dodgeConfigResourcePath = "CombatVolumeEffectConfigs/DodgeVolumeEffectConfig";
    [SerializeField] private CombatVolumeEffectConfigAsset pushConfigAsset;
    [SerializeField] private string pushConfigResourcePath = "CombatVolumeEffectConfigs/PushVolumeEffectConfig";
    [SerializeField] private CombatVolumeEffectConfigAsset grenadeConfigAsset;
    [SerializeField] private string grenadeConfigResourcePath = "CombatVolumeEffectConfigs/GrenadeVolumeEffectConfig";

    [Header("狂暴后处理")]
    [SerializeField, Range(0f, 1f)] private float berserkChromaticIntensity = 0.65f;
    [SerializeField, Range(0f, 0.3f)] private float berserkChromaticPulseAmount = 0.15f;
    [SerializeField] private float berserkChromaticPulseSpeed = 4.5f;
    [SerializeField] private float berserkFadeInTime = 0.18f;
    [SerializeField] private float berserkFadeOutTime = 0.35f;

    private VolumeProfile runtimeProfile;
    private readonly Dictionary<CombatVolumeEffectType, CombatVolumeEffectConfig> skillEffectConfigs =
        new Dictionary<CombatVolumeEffectType, CombatVolumeEffectConfig>();
    private CombatVolumeEffectConfig playerDamageConfig;
    private CombatVolumeEffectConfig activeSkillConfig;
    private Vignette vignette;
    private ColorAdjustments colorAdjustments;
    private Bloom bloom;
    private ChromaticAberration chromaticAberration;
    private VolumeComponent speedLinesComponent;

    private bool hasSnapshot;
    private float baseVignetteIntensity;
    private float baseVignetteSmoothness;
    private Color baseVignetteColor;
    private float basePostExposure;
    private float baseSaturation;
    private Color baseColorFilter;
    private float baseBloomIntensity;
    private Color baseBloomTint;
    private float baseChromaticIntensity;

    private float damageTarget;
    private float damageBlend;
    private float damageHoldEndTime;
    private float skillBlend;
    private float berserkBlend;
    private bool berserkActive;
    private Coroutine damagePulseRoutine;
    private Coroutine skillPulseRoutine;
    private Coroutine berserkPulseRoutine;
    private Coroutine cameraRefreshRoutine;
    private bool usingRuntimeCreatedVolume;
    private bool speedLinesMissingLogged;

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
        EventCenter.Instance.AddEventListener<SkillCastEventData>(GameEvent.SkillCastStarted, OnSkillCastStarted);
        EventCenter.Instance.AddEventListener<SkillHitEnemyEventData>(GameEvent.SkillHitEnemy, OnSkillHitEnemy);
        EventCenter.Instance.AddEventListener<SkillVisualEventData>(GameEvent.SkillVisualStarted, OnSkillVisualStarted);
        EventCenter.Instance.AddEventListener<PlayerBerserkChangedEventData>(GameEvent.PlayerBerserkChanged, OnPlayerBerserkChanged);
        EnsurePlayerDamageConfig();
        EnsureCombatVolume(false);
        StartCameraRefreshRoutine();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EventCenter.Instance.RemoveEventListener<PlayerDamagedEventData>(GameEvent.PlayerDamaged, OnPlayerDamaged);
        EventCenter.Instance.RemoveEventListener<SkillCastEventData>(GameEvent.SkillCastStarted, OnSkillCastStarted);
        EventCenter.Instance.RemoveEventListener<SkillHitEnemyEventData>(GameEvent.SkillHitEnemy, OnSkillHitEnemy);
        EventCenter.Instance.RemoveEventListener<SkillVisualEventData>(GameEvent.SkillVisualStarted, OnSkillVisualStarted);
        EventCenter.Instance.RemoveEventListener<PlayerBerserkChangedEventData>(GameEvent.PlayerBerserkChanged, OnPlayerBerserkChanged);
        StopDamagePulseRoutine();
        StopSkillPulseRoutine();
        StopBerserkPulseRoutine();
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
        ResetTransientEffectsForSceneChange();
        if (combatVolume == null || usingRuntimeCreatedVolume)
        {
            if (usingRuntimeCreatedVolume && combatVolume != null)
            {
                Destroy(combatVolume.gameObject);
            }

            combatVolume = null;
            runtimeProfile = null;
            hasSnapshot = false;
            chromaticAberration = null;
            speedLinesComponent = null;
            speedLinesMissingLogged = false;
            usingRuntimeCreatedVolume = false;
        }

        EnsurePlayerDamageConfig();
        EnsureCombatVolume();
        RefreshCameraPostProcessing();
    }

    private void ResetTransientEffectsForSceneChange()
    {
        StopDamagePulseRoutine();
        StopSkillPulseRoutine();
        StopBerserkPulseRoutine();
        damageHoldEndTime = 0f;
        RestoreBaseValues();
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

    private void OnSkillCastStarted(SkillCastEventData eventData)
    {
        if (string.IsNullOrEmpty(eventData.postProcessKey))
        {
            return;
        }

        float intensity = eventData.skillType == SkillType.Push ? 0.45f : 1f;
        PlaySkillPulse(eventData.skillType, eventData.postProcessKey, intensity);
    }

    private void OnSkillHitEnemy(SkillHitEnemyEventData eventData)
    {
        if (eventData.skillType != SkillType.Push)
        {
            return;
        }

        PlaySkillPulse(eventData.skillType, "Skill_Push_ImpactPulse", 0.85f);
    }

    private void OnSkillVisualStarted(SkillVisualEventData eventData)
    {
        if (eventData.skillType != SkillType.Grenade || string.IsNullOrEmpty(eventData.postProcessKey))
        {
            return;
        }

        PlaySkillPulse(eventData.skillType, eventData.postProcessKey, eventData.intensity);
    }

    private void OnPlayerBerserkChanged(PlayerBerserkChangedEventData eventData)
    {
        PlayBerserk(eventData.active, eventData.postProcessKey);
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

    private CombatVolumeEffectConfig EnsureSkillEffectConfig(CombatVolumeEffectType effectType)
    {
        if (skillEffectConfigs.TryGetValue(effectType, out CombatVolumeEffectConfig cachedConfig))
        {
            return cachedConfig;
        }

        CombatVolumeEffectConfigAsset configAsset = ResolveSkillConfigAsset(effectType);
        CombatVolumeEffectConfig config = configAsset != null
            ? configAsset.CreateRuntimeConfig()
            : CreateDefaultSkillConfig(effectType);

        config?.ApplyMissingDefaults();
        if (config != null)
        {
            skillEffectConfigs[effectType] = config;
        }

        return config;
    }

    private CombatVolumeEffectConfigAsset ResolveSkillConfigAsset(CombatVolumeEffectType effectType)
    {
        switch (effectType)
        {
            case CombatVolumeEffectType.Dodge:
                if (dodgeConfigAsset == null && !string.IsNullOrEmpty(dodgeConfigResourcePath))
                {
                    dodgeConfigAsset = Resources.Load<CombatVolumeEffectConfigAsset>(dodgeConfigResourcePath);
                }

                return dodgeConfigAsset;
            case CombatVolumeEffectType.Push:
                if (pushConfigAsset == null && !string.IsNullOrEmpty(pushConfigResourcePath))
                {
                    pushConfigAsset = Resources.Load<CombatVolumeEffectConfigAsset>(pushConfigResourcePath);
                }

                return pushConfigAsset;
            case CombatVolumeEffectType.Grenade:
                if (grenadeConfigAsset == null && !string.IsNullOrEmpty(grenadeConfigResourcePath))
                {
                    grenadeConfigAsset = Resources.Load<CombatVolumeEffectConfigAsset>(grenadeConfigResourcePath);
                }

                return grenadeConfigAsset;
            default:
                return null;
        }
    }

    private CombatVolumeEffectConfig CreateDefaultSkillConfig(CombatVolumeEffectType effectType)
    {
        switch (effectType)
        {
            case CombatVolumeEffectType.Dodge:
                return CombatVolumeEffectConfig.CreateDefaultDodge();
            case CombatVolumeEffectType.Push:
                return CombatVolumeEffectConfig.CreateDefaultPush();
            case CombatVolumeEffectType.Grenade:
                return CombatVolumeEffectConfig.CreateDefaultGrenade();
            case CombatVolumeEffectType.Berserk:
                return CombatVolumeEffectConfig.CreateDefaultBerserk();
            default:
                return null;
        }
    }

    private void PlaySkillPulse(SkillType skillType, string effectKey, float intensity)
    {
        CombatVolumeEffectType effectType = ResolveSkillVolumeEffectType(skillType);
        CombatVolumeEffectConfig config = EnsureSkillEffectConfig(effectType);
        EnsureCombatVolume();
        if (config == null || combatVolume == null || !hasSnapshot)
        {
            return;
        }

        if (!string.IsNullOrEmpty(effectKey) && !string.Equals(config.effectKey, effectKey))
        {
            return;
        }

        if (skillPulseRoutine != null)
        {
            StopCoroutine(skillPulseRoutine);
        }

        activeSkillConfig = config;
        skillPulseRoutine = StartCoroutine(SkillPulseRoutine(Mathf.Clamp01(Mathf.Max(config.minIntensity, intensity))));

        if (debugLog)
        {
            Debug.Log($"[CombatVolume] 技能画面反馈 Type={skillType} Intensity={intensity:0.00}", this);
        }
    }

    private void PlayBerserk(bool active, string effectKey)
    {
        EnsureCombatVolume();
        berserkActive = active;
        if (berserkPulseRoutine != null)
        {
            StopCoroutine(berserkPulseRoutine);
        }

        if (active)
        {
            SetSpeedLinesActive(true);
        }

        berserkPulseRoutine = StartCoroutine(BerserkBlendRoutine(active));

        if (debugLog)
        {
            Debug.Log($"[CombatVolume] 狂暴 Active={active} SpeedLines保留 色散目标={berserkChromaticIntensity:0.00}", this);
        }
    }

    private CombatVolumeEffectType ResolveSkillVolumeEffectType(SkillType skillType)
    {
        switch (skillType)
        {
            case SkillType.Push:
                return CombatVolumeEffectType.Push;
            case SkillType.Grenade:
                return CombatVolumeEffectType.Grenade;
            case SkillType.Dodge:
            default:
                return CombatVolumeEffectType.Dodge;
        }
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

        if (!runtimeProfile.TryGet(out chromaticAberration))
        {
            chromaticAberration = runtimeProfile.Add<ChromaticAberration>(true);
        }

        runtimeProfile.TryGet(out bloom);
        vignette.active = true;
        colorAdjustments.active = true;
        chromaticAberration.active = true;
        vignette.color.overrideState = true;
        vignette.intensity.overrideState = true;
        vignette.smoothness.overrideState = true;
        colorAdjustments.postExposure.overrideState = true;
        colorAdjustments.saturation.overrideState = true;
        colorAdjustments.colorFilter.overrideState = true;
        chromaticAberration.intensity.overrideState = true;

        if (bloom != null)
        {
            bloom.intensity.overrideState = true;
            bloom.tint.overrideState = true;
        }

        EnsureSpeedLinesComponent();
        SnapshotBaseValues();
    }

    private void EnsureSpeedLinesComponent()
    {
        if (runtimeProfile == null || speedLinesComponent != null)
        {
            return;
        }

        string targetName = string.IsNullOrWhiteSpace(speedLinesComponentName)
            ? DefaultSpeedLinesComponentName
            : speedLinesComponentName;

        List<VolumeComponent> components = runtimeProfile.components;
        for (int i = 0; i < components.Count; i++)
        {
            VolumeComponent component = components[i];
            if (component == null)
            {
                continue;
            }

            if (component.name == targetName || component.GetType().Name == targetName)
            {
                speedLinesComponent = component;
                return;
            }
        }
    }

    private void SetSpeedLinesActive(bool active)
    {
        EnsureCombatVolume();
        EnsureSpeedLinesComponent();
        if (speedLinesComponent == null)
        {
            if (debugLog && !speedLinesMissingLogged)
            {
                Debug.LogWarning($"[CombatVolume] 找不到 {speedLinesComponentName} 后处理组件", this);
                speedLinesMissingLogged = true;
            }

            return;
        }

        speedLinesComponent.active = active;
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

        baseChromaticIntensity = chromaticAberration != null
            ? chromaticAberration.intensity.value
            : 0f;

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

    private IEnumerator SkillPulseRoutine(float target)
    {
        CombatVolumeEffectConfig config = activeSkillConfig;
        if (config == null)
        {
            skillPulseRoutine = null;
            yield break;
        }

        yield return FadeSkillAmount(skillBlend, target, config.fadeInTime);

        float holdEndTime = Time.unscaledTime + config.holdTime;
        while (Time.unscaledTime < holdEndTime)
        {
            SetSkillAmount(target);
            yield return null;
        }

        yield return FadeSkillAmount(skillBlend, 0f, config.fadeOutTime);
        activeSkillConfig = null;
        skillPulseRoutine = null;
    }

    private void StopSkillPulseRoutine()
    {
        if (skillPulseRoutine != null)
        {
            StopCoroutine(skillPulseRoutine);
            skillPulseRoutine = null;
        }

        skillBlend = 0f;
        activeSkillConfig = null;
    }

    private void StopBerserkPulseRoutine()
    {
        if (berserkPulseRoutine != null)
        {
            StopCoroutine(berserkPulseRoutine);
            berserkPulseRoutine = null;
        }

        berserkActive = false;
        berserkBlend = 0f;
        if (speedLinesComponent != null)
        {
            speedLinesComponent.active = false;
        }
        ApplyVolumeValues();
    }

    private IEnumerator BerserkBlendRoutine(bool active)
    {
        float target = active ? 1f : 0f;
        float duration = active ? berserkFadeInTime : berserkFadeOutTime;
        duration = Mathf.Max(0.01f, duration);
        float start = berserkBlend;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Smooth01(elapsed / duration);
            SetBerserkAmount(Mathf.Lerp(start, target, t));
            yield return null;
        }

        SetBerserkAmount(target);
        while (active && berserkActive)
        {
            ApplyVolumeValues();
            yield return null;
        }

        if (!active)
        {
            SetSpeedLinesActive(false);
        }

        berserkPulseRoutine = null;
    }

    private void SetBerserkAmount(float amount)
    {
        berserkBlend = Mathf.Clamp01(amount);
        ApplyVolumeValues();
    }

    private IEnumerator FadeSkillAmount(float from, float to, float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Smooth01(elapsed / duration);
            SetSkillAmount(Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetSkillAmount(to);
    }

    private void SetSkillAmount(float amount)
    {
        skillBlend = Mathf.Clamp01(amount);
        ApplyVolumeValues();
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
        if (!hasSnapshot || vignette == null || colorAdjustments == null)
        {
            return;
        }

        float vignetteIntensity = baseVignetteIntensity;
        float vignetteSmoothness = baseVignetteSmoothness;
        float postExposure = basePostExposure;
        float saturation = baseSaturation;
        float bloomIntensity = baseBloomIntensity;
        Color vignetteColorValue = baseVignetteColor;
        Color colorFilterValue = baseColorFilter;
        Color bloomTintValue = baseBloomTint;

        AccumulateVolumeEffect(
            EnsurePlayerDamageConfig(),
            Smooth01(damageBlend),
            ref vignetteIntensity,
            ref vignetteSmoothness,
            ref vignetteColorValue,
            ref postExposure,
            ref saturation,
            ref colorFilterValue,
            ref bloomIntensity,
            ref bloomTintValue);

        AccumulateVolumeEffect(
            activeSkillConfig,
            Smooth01(skillBlend),
            ref vignetteIntensity,
            ref vignetteSmoothness,
            ref vignetteColorValue,
            ref postExposure,
            ref saturation,
            ref colorFilterValue,
            ref bloomIntensity,
            ref bloomTintValue);

        vignette.intensity.value = Mathf.Clamp01(vignetteIntensity);
        vignette.smoothness.value = Mathf.Clamp01(vignetteSmoothness);
        vignette.color.value = vignetteColorValue;
        colorAdjustments.postExposure.value = postExposure;
        colorAdjustments.saturation.value = saturation;
        colorAdjustments.colorFilter.value = colorFilterValue;

        if (bloom != null)
        {
            bloom.intensity.value = Mathf.Max(0f, bloomIntensity);
            bloom.tint.value = bloomTintValue;
        }

        if (chromaticAberration != null)
        {
            float berserkAmount = Smooth01(berserkBlend);
            float pulse = berserkActive && berserkAmount > 0.001f
                ? (Mathf.Sin(Time.unscaledTime * Mathf.Max(0f, berserkChromaticPulseSpeed)) * 0.5f + 0.5f)
                  * Mathf.Max(0f, berserkChromaticPulseAmount)
                : 0f;
            float chromaticBoost = (berserkChromaticIntensity + pulse) * berserkAmount;
            chromaticAberration.intensity.value = Mathf.Clamp01(baseChromaticIntensity + chromaticBoost);
        }
    }

    private void AccumulateVolumeEffect(
        CombatVolumeEffectConfig config,
        float amount,
        ref float vignetteIntensity,
        ref float vignetteSmoothness,
        ref Color vignetteColorValue,
        ref float postExposure,
        ref float saturation,
        ref Color colorFilterValue,
        ref float bloomIntensity,
        ref Color bloomTintValue)
    {
        if (config == null || amount <= 0f)
        {
            return;
        }

        vignetteIntensity += config.vignetteIntensityBoost * amount;
        vignetteSmoothness += config.vignetteSmoothnessBoost * amount;
        vignetteColorValue = Color.Lerp(vignetteColorValue, config.vignetteColor, amount);
        postExposure += config.postExposureOffset * amount;
        saturation += config.saturationOffset * amount;
        colorFilterValue = Color.Lerp(colorFilterValue, config.colorFilter, amount);

        if (config.enableBloomPulse)
        {
            bloomIntensity += config.bloomIntensityBoost * amount;
            bloomTintValue = Color.Lerp(bloomTintValue, config.colorFilter, amount * config.bloomTintBlend);
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

        if (chromaticAberration != null)
        {
            chromaticAberration.intensity.value = baseChromaticIntensity;
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
