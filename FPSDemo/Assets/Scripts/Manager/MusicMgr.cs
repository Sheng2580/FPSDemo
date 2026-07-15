using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MusicMgr : UnitySingleTonMono<MusicMgr>
{
    public const string BGMusicBundleName = "bgm";
    public const string DefaultBGMusicName = "bgm";
    public const string UIAudioBundleName = "ui_audio";
    public const string EnemyAudioBundleName = "enemy_audio";
    public const string CombatFeedbackAudioBundleName = "combat_feedback";
    public const string UIButtonSound = "SnappyButton1";
    public const string UISelectSound = "ClickyButton9a";
    public const string UIConfirmSound = "GenericButton4";
    public const string UIOpenSound = "OpenOrEnable2";
    public const string UICloseSound = "CloseOrDisable2";
    public const string UISuccessSound = "Success3";
    public const string UIErrorSound = "Error2";
    public const string PickupCollectedSound = "GenericNotification3";
    public const string PlayerHitSound = "hit";

#if UNITY_EDITOR
    private const string EditorUIAudioRoot = "Assets/Art/ABRes/CombatFeedback/Audio/Cyberleaf - Modern UI SFX";
    private const string EditorEnemyAudioRoot = "Assets/Art/Audio/Zombie Sounds Pro";
    private const string EditorCombatAudioRoot = "Assets/Art/Audio";
    private const string EditorDefaultBGMusicPath = "Assets/Art/Audio/bgm.mp3";
#endif

    private AudioSource bkMusic; //音频组件
    private AudioSource uiSoundSource;
    private float bkVolume = 1; //背景音乐大小
    private float soundVolume = 1; //音效大小
    private Coroutine bgMusicTransitionCoroutine;
    private string currentBkMusicName;
    private int bgMusicRequestId;
    private const float DefaultBGMusicFadeDuration = 1f;
    private const int MaxRuntimeSoundSources = 12;
    private const float BerserkAudioFadeInTime = 0.18f;
    private const float BerserkAudioFadeOutTime = 0.35f;
    private const float BerserkLowPassCutoff = 5200f;
    private const float BerserkEchoDelay = 68f;
    private const float PlayerHitSoundInterval = 0.08f;

    private List<AudioSource> soundlist = new List<AudioSource>();
    private readonly List<AudioSource> runtimeSoundSources = new List<AudioSource>();
    private readonly Dictionary<string, AudioClip> loadedSoundClips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Action<AudioClip>>> loadingSoundCallbacks = new Dictionary<string, List<Action<AudioClip>>>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> loggedUISoundClips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private AudioListener activeAudioListener;
    private AudioListener berserkFilterListener;
    private AudioLowPassFilter berserkLowPassFilter;
    private AudioChorusFilter berserkChorusFilter;
    private AudioEchoFilter berserkEchoFilter;
    private Coroutine berserkAudioRoutine;
    private float berserkAudioBlend;
    private float lastPlayerHitSoundTime = float.NegativeInfinity;

    public override void Awake()
    {
        base.Awake();
        if (Instance != this)
        {
            return;
        }

        EnsureBGMusicSource();
        EnsureUISoundSource();
    }

    private void OnEnable()
    {
        EventCenter.Instance.AddEventListener<PickupCollectedEventData>(GameEvent.PickupCollected, OnPickupCollected);
        EventCenter.Instance.AddEventListener<PlayerBerserkChangedEventData>(GameEvent.PlayerBerserkChanged, OnPlayerBerserkChanged);
        EventCenter.Instance.AddEventListener<PlayerDamagedEventData>(GameEvent.PlayerDamaged, OnPlayerDamaged);
    }

    private void OnDisable()
    {
        EventCenter.Instance.RemoveEventListener<PickupCollectedEventData>(GameEvent.PickupCollected, OnPickupCollected);
        EventCenter.Instance.RemoveEventListener<PlayerBerserkChangedEventData>(GameEvent.PlayerBerserkChanged, OnPlayerBerserkChanged);
        EventCenter.Instance.RemoveEventListener<PlayerDamagedEventData>(GameEvent.PlayerDamaged, OnPlayerDamaged);
        StopBerserkAudioEffect();
    }

    public void PreloadDefaultUISounds()
    {
        LoadSoundClip(UIAudioBundleName, UIButtonSound, null);
        LoadSoundClip(UIAudioBundleName, UISelectSound, null);
        LoadSoundClip(UIAudioBundleName, UIConfirmSound, null);
        LoadSoundClip(UIAudioBundleName, UIOpenSound, null);
        LoadSoundClip(UIAudioBundleName, UICloseSound, null);
        LoadSoundClip(UIAudioBundleName, UISuccessSound, null);
        LoadSoundClip(UIAudioBundleName, UIErrorSound, null);
        LoadSoundClip(UIAudioBundleName, PickupCollectedSound, null);
    }

    private void OnPickupCollected(PickupCollectedEventData eventData)
    {
        if (eventData.config == null || eventData.collector == null)
        {
            return;
        }

        PlayerController player = eventData.collector.GetComponent<PlayerController>();
        player ??= eventData.collector.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        PlayUISound(PickupCollectedSound, 0.9f);
    }

    private void OnPlayerBerserkChanged(PlayerBerserkChangedEventData eventData)
    {
        SetBerserkAudioEffect(eventData.active);
    }

    private void OnPlayerDamaged(PlayerDamagedEventData eventData)
    {
        if (eventData.player == null
            || eventData.damage <= 0
            || Time.unscaledTime - lastPlayerHitSoundTime < PlayerHitSoundInterval)
        {
            return;
        }

        lastPlayerHitSoundTime = Time.unscaledTime;
        PlaySoundForAB(PlayerHitSound, CombatFeedbackAudioBundleName);
    }

    private void SetBerserkAudioEffect(bool active)
    {
        if (berserkAudioRoutine != null)
        {
            StopCoroutine(berserkAudioRoutine);
        }

        if (active)
        {
            EnsureBerserkAudioFilters();
        }

        berserkAudioRoutine = StartCoroutine(BerserkAudioBlendRoutine(active));
    }

    private IEnumerator BerserkAudioBlendRoutine(bool active)
    {
        float target = active ? 1f : 0f;
        float duration = active ? BerserkAudioFadeInTime : BerserkAudioFadeOutTime;
        float start = berserkAudioBlend;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = SmoothAudioBlend(elapsed / duration);
            SetBerserkAudioBlend(Mathf.Lerp(start, target, t));
            yield return null;
        }

        SetBerserkAudioBlend(target);
        berserkAudioRoutine = null;
    }

    private void SetBerserkAudioBlend(float amount)
    {
        berserkAudioBlend = Mathf.Clamp01(amount);
        if (berserkAudioBlend > 0.001f && !EnsureBerserkAudioFilters())
        {
            return;
        }

        ApplyBerserkAudioValues();
    }

    private bool EnsureBerserkAudioFilters()
    {
        ResolveAudioListenerTransform();
        if (activeAudioListener == null)
        {
            return false;
        }

        if (berserkFilterListener == activeAudioListener
            && berserkLowPassFilter != null
            && berserkChorusFilter != null
            && berserkEchoFilter != null)
        {
            return true;
        }

        DisableBerserkAudioFilters();
        berserkFilterListener = activeAudioListener;
        berserkLowPassFilter = berserkFilterListener.gameObject.AddComponent<AudioLowPassFilter>();
        berserkChorusFilter = berserkFilterListener.gameObject.AddComponent<AudioChorusFilter>();
        berserkEchoFilter = berserkFilterListener.gameObject.AddComponent<AudioEchoFilter>();
        berserkLowPassFilter.enabled = false;
        berserkChorusFilter.enabled = false;
        berserkEchoFilter.enabled = false;
        return true;
    }

    private void ApplyBerserkAudioValues()
    {
        if (berserkLowPassFilter == null || berserkChorusFilter == null || berserkEchoFilter == null)
        {
            return;
        }

        float amount = SmoothAudioBlend(berserkAudioBlend);
        bool enabled = amount > 0.001f;
        berserkLowPassFilter.enabled = enabled;
        berserkChorusFilter.enabled = enabled;
        berserkEchoFilter.enabled = enabled;
        if (!enabled)
        {
            return;
        }

        berserkLowPassFilter.cutoffFrequency = Mathf.Lerp(22000f, BerserkLowPassCutoff, amount);
        berserkLowPassFilter.lowpassResonanceQ = Mathf.Lerp(1f, 1.8f, amount);
        berserkChorusFilter.dryMix = Mathf.Lerp(1f, 0.82f, amount);
        berserkChorusFilter.wetMix1 = Mathf.Lerp(0f, 0.2f, amount);
        berserkChorusFilter.wetMix2 = Mathf.Lerp(0f, 0.1f, amount);
        berserkChorusFilter.wetMix3 = Mathf.Lerp(0f, 0.06f, amount);
        berserkChorusFilter.delay = Mathf.Lerp(8f, 22f, amount);
        berserkChorusFilter.rate = Mathf.Lerp(0.3f, 1.6f, amount);
        berserkChorusFilter.depth = Mathf.Lerp(0.02f, 0.35f, amount);
        berserkEchoFilter.delay = Mathf.Lerp(10f, BerserkEchoDelay, amount);
        berserkEchoFilter.decayRatio = Mathf.Lerp(0f, 0.22f, amount);
        berserkEchoFilter.dryMix = Mathf.Lerp(1f, 0.92f, amount);
        berserkEchoFilter.wetMix = Mathf.Lerp(0f, 0.18f, amount);
    }

    private void StopBerserkAudioEffect()
    {
        if (berserkAudioRoutine != null)
        {
            StopCoroutine(berserkAudioRoutine);
            berserkAudioRoutine = null;
        }

        berserkAudioBlend = 0f;
        DisableBerserkAudioFilters();
    }

    private void DisableBerserkAudioFilters()
    {
        if (berserkLowPassFilter != null)
        {
            berserkLowPassFilter.enabled = false;
        }

        if (berserkChorusFilter != null)
        {
            berserkChorusFilter.enabled = false;
        }

        if (berserkEchoFilter != null)
        {
            berserkEchoFilter.enabled = false;
        }
    }

    private static float SmoothAudioBlend(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    public void PlayUISound(string soundName, float volumeScale = 1f)
    {
        LoadSoundClip(UIAudioBundleName, soundName, clip =>
        {
            if (clip == null)
            {
                return;
            }

            PlayLoadedUISound(clip, volumeScale);
        });
    }

    private void PlayLoadedUISound(AudioClip clip, float volumeScale)
    {
        if (clip == null)
        {
            return;
        }

        if (clip.loadState == AudioDataLoadState.Unloaded)
        {
            clip.LoadAudioData();
        }

        if (clip.loadState == AudioDataLoadState.Loading)
        {
            StartCoroutine(PlayUISoundWhenLoaded(clip, volumeScale));
            return;
        }

        if (clip.loadState == AudioDataLoadState.Failed)
        {
            Debug.LogWarning($"[MusicMgr] UI 音效数据加载失败 {clip.name}", this);
            return;
        }

        EnsureUISoundSource();
        uiSoundSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        if (loggedUISoundClips.Add(clip.name))
        {
            Debug.Log($"[MusicMgr] UI 音效已播放 {clip.name} LoadState={clip.loadState}", this);
        }
    }

    private IEnumerator PlayUISoundWhenLoaded(AudioClip clip, float volumeScale)
    {
        float timeout = Time.realtimeSinceStartup + 1.5f;
        while (clip != null
               && clip.loadState == AudioDataLoadState.Loading
               && Time.realtimeSinceStartup < timeout)
        {
            yield return null;
        }

        if (clip == null || clip.loadState != AudioDataLoadState.Loaded)
        {
            Debug.LogWarning($"[MusicMgr] UI 音效等待加载超时 {clip?.name}", this);
            yield break;
        }

        EnsureUISoundSource();
        uiSoundSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    public void PlayWorldSoundForAB(
        string soundName,
        Vector3 worldPosition,
        string abName,
        float volumeScale = 1f,
        float pitchRandom = 0.04f,
        float minDistance = 2f,
        float maxDistance = 28f,
        int priority = 128,
        bool allowSteal = false,
        float basePitch = 1f,
        float maxPlaybackDuration = 0f)
    {
        if (!IsWorldSoundAudible(worldPosition, maxDistance))
        {
            return;
        }

        LoadSoundClip(abName, soundName, clip =>
        {
            if (clip == null || !IsWorldSoundAudible(worldPosition, maxDistance))
            {
                return;
            }

            AudioSource source = GetRuntimeSoundSource(priority, allowSteal);
            if (source == null)
            {
                return;
            }

            source.Stop();
            source.transform.position = worldPosition;
            source.clip = clip;
            source.volume = soundVolume * Mathf.Clamp01(volumeScale);
            source.pitch = Mathf.Clamp(
                basePitch + UnityEngine.Random.Range(-Mathf.Abs(pitchRandom), Mathf.Abs(pitchRandom)),
                0.1f,
                3f);
            source.loop = false;
            source.spatialBlend = 1f;
            source.priority = Mathf.Clamp(priority, 0, 256);
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = Mathf.Max(0.1f, minDistance);
            source.maxDistance = Mathf.Max(source.minDistance, maxDistance);
            source.dopplerLevel = 0f;
            source.Play();
            if (maxPlaybackDuration > 0f
                && clip.length / source.pitch > maxPlaybackDuration)
            {
                source.SetScheduledEndTime(AudioSettings.dspTime + maxPlaybackDuration);
            }
        });
    }

    /// <summary>
    /// 播放背景音乐  在一个对象上放音频组件
    /// </summary>
    public void PlayBGMusic(string name)
    {
        //加载背景音乐
        AudioClip clip = ResMgr.Instance.load<AudioClip>("Music/BG/" + name);
        if (clip == null)
        {
            Debug.LogError("[MusicMgr] Load bgm failed: " + name);
            return;
        }

        SwitchBGMusic(clip, "Resources:" + name, DefaultBGMusicFadeDuration);
    }


    public void PlayBGMusicForAB(string name)
    {
        int requestId = ++bgMusicRequestId;
        ABManager.Instance.LoadResAsync<AudioClip>(BGMusicBundleName, name, clip =>
        {
            if (requestId != bgMusicRequestId)
            {
                return;
            }

            if (clip == null)
            {
                Debug.LogError("[MusicMgr] Load bgm failed: " + name);
                return;
            }

            SwitchBGMusic(clip, $"AB:{BGMusicBundleName}/" + name, DefaultBGMusicFadeDuration);
        });
    }

    public void PlayDefaultBGMusic()
    {
#if UNITY_EDITOR
        AudioClip editorClip = AssetDatabase.LoadAssetAtPath<AudioClip>(EditorDefaultBGMusicPath);
        if (editorClip != null)
        {
            SwitchBGMusic(editorClip, "Editor:" + DefaultBGMusicName, DefaultBGMusicFadeDuration);
            return;
        }
#endif

        PlayBGMusicForAB(DefaultBGMusicName);
    }



    /// <summary>
    /// 停止背景音乐
    /// </summary>
    public void StopBKMusic()
    {
        if (bkMusic == null) return;
        StopBGMusicTransition();
        currentBkMusicName = null;
        bkMusic.Stop();
    }

    /// <summary>
    /// 暂停背景音乐
    /// </summary>
    public void PauseBKMusic()
    {
        if (bkMusic == null) return;
        bkMusic.Pause();
    }

    public void changeBkVolume(float volume)
    {
        bkVolume = volume;
        if (bkMusic == null) return;
        bkMusic.volume = volume;
    }

    private void SwitchBGMusic(AudioClip clip, string musicName, float fadeDuration)
    {
        EnsureBGMusicSource();
        if (bkMusic == null || clip == null)
        {
            return;
        }

        if (currentBkMusicName == musicName && bkMusic.clip == clip && bkMusic.isPlaying)
        {
            bkMusic.volume = bkVolume;
            return;
        }

        StopBGMusicTransition();
        bgMusicTransitionCoroutine = StartCoroutine(SwitchBGMusicCoroutine(clip, musicName, fadeDuration));
    }

    private IEnumerator SwitchBGMusicCoroutine(AudioClip clip, string musicName, float fadeDuration)
    {
        fadeDuration = Mathf.Max(0f, fadeDuration);
        float fadeOutDuration = bkMusic.isPlaying ? fadeDuration * 0.5f : 0f;
        float fadeInDuration = fadeDuration * 0.5f;

        if (fadeOutDuration > 0f)
        {
            float startVolume = bkMusic.volume;
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                if (bkMusic == null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                bkMusic.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeOutDuration);
                yield return null;
            }
        }

        if (bkMusic == null)
        {
            yield break;
        }

        bkMusic.Stop();
        bkMusic.clip = clip;
        bkMusic.loop = true;
        bkMusic.volume = fadeInDuration > 0f ? 0f : bkVolume;
        bkMusic.Play();
        currentBkMusicName = musicName;

        if (fadeInDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                if (bkMusic == null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                bkMusic.volume = Mathf.Lerp(0f, bkVolume, elapsed / fadeInDuration);
                yield return null;
            }
        }

        if (bkMusic != null)
        {
            bkMusic.volume = bkVolume;
        }

        bgMusicTransitionCoroutine = null;
    }

    private void EnsureBGMusicSource()
    {
        if (bkMusic != null)
        {
            return;
        }

        GameObject obj = new GameObject("BGMusic");
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;
        bkMusic = obj.AddComponent<AudioSource>();
        bkMusic.playOnAwake = false;
        bkMusic.loop = true;
        bkMusic.spatialBlend = 0f;
        bkMusic.volume = bkVolume;
        bkMusic.mute = false;
        bkMusic.ignoreListenerPause = true;
        bkMusic.priority = 0;
    }

    private void EnsureUISoundSource()
    {
        if (uiSoundSource != null)
        {
            return;
        }

        GameObject obj = new GameObject("UISound");
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;
        uiSoundSource = obj.AddComponent<AudioSource>();
        uiSoundSource.playOnAwake = false;
        uiSoundSource.loop = false;
        uiSoundSource.spatialBlend = 0f;
        uiSoundSource.volume = soundVolume;
        uiSoundSource.mute = false;
        uiSoundSource.ignoreListenerPause = true;
        uiSoundSource.priority = 32;
    }

    private void StopBGMusicTransition()
    {
        if (bgMusicTransitionCoroutine == null)
        {
            return;
        }

        StopCoroutine(bgMusicTransitionCoroutine);
        bgMusicTransitionCoroutine = null;
    }

    /// <summary>
    /// 播放音效
    /// </summary>
    /// <param name="soundName"></param>
    public void PlaySound(string soundName, bool isLoop = false)
    {
        //获取音频对象
        GameObject soundObj = PoolMgr.Instance.getObj("Music/Sound/" + soundName);
        //获取音频组件
        AudioSource source = soundObj.GetComponent<AudioSource>();
        if (soundObj.GetComponent<AudioSource>() == null)
            source = soundObj.AddComponent<AudioSource>();//如果音频组件为空则添加一个音频组件
        source.clip = ResMgr.Instance.load<AudioClip>("Music/Sound/" + soundName);//加载音频文件
        source.volume = soundVolume;//设置音效大小
        source.loop = isLoop;//设置是否要循环播放
        source.Play();
        soundlist.Add(source);//将音频组件添加到音效列表中
    }

    //默认位音效文件夹
    public void PlaySoundForAB(string soundName,string abName = "sound", bool isLoop = false)
    {
        PlaySoundForABInternal(soundName, null, abName, isLoop);
    }

    // Play an AB sound at a world position. Useful for hit sounds and attack sounds in combat.
    public void PlaySoundForAB(string soundName, Vector3 worldPosition, string abName = "sound", bool isLoop = false)
    {
        PlaySoundForABInternal(soundName, worldPosition, abName, isLoop);
    }

    private void PlaySoundForABInternal(string soundName, Vector3? worldPosition, string abName, bool isLoop)
    {
        if (string.IsNullOrEmpty(soundName))
        {
            return;
        }

        LoadSoundClip(abName, soundName, clip =>
        {
            if (clip == null)
            {
                return;
            }

            AudioSource source = GetRuntimeSoundSource(128, true);
            if (source == null)
            {
                return;
            }

            source.Stop();
            source.transform.position = worldPosition ?? transform.position;
            source.clip = clip;
            source.volume = soundVolume;
            source.pitch = 1f;
            source.loop = isLoop;
            source.spatialBlend = worldPosition.HasValue ? 1f : 0f;
            source.priority = 128;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = 28f;
            source.dopplerLevel = 0f;
            source.Play();
        });
    }

    private void LoadSoundClip(string abName, string soundName, Action<AudioClip> callback)
    {
        if (string.IsNullOrWhiteSpace(abName) || string.IsNullOrWhiteSpace(soundName))
        {
            callback?.Invoke(null);
            return;
        }

        string cacheKey = abName + "/" + soundName;
        if (loadedSoundClips.TryGetValue(cacheKey, out AudioClip cachedClip) && cachedClip != null)
        {
            callback?.Invoke(cachedClip);
            return;
        }

        if (loadingSoundCallbacks.TryGetValue(cacheKey, out List<Action<AudioClip>> callbacks))
        {
            if (callback != null)
            {
                callbacks.Add(callback);
            }

            return;
        }

        callbacks = new List<Action<AudioClip>>();
        if (callback != null)
        {
            callbacks.Add(callback);
        }

        loadingSoundCallbacks.Add(cacheKey, callbacks);

#if UNITY_EDITOR
        if (TryLoadEditorSound(abName, soundName, out AudioClip editorClip))
        {
            CompleteSoundLoad(cacheKey, editorClip);
            return;
        }
#endif

        ABManager.Instance.LoadAssetAsync<AudioClip>(abName, soundName, clip =>
        {
            if (clip == null)
            {
                Debug.LogWarning($"[MusicMgr] 音效加载失败 AB={abName} Asset={soundName}", this);
            }

            CompleteSoundLoad(cacheKey, clip);
        });
    }

    private void CompleteSoundLoad(string cacheKey, AudioClip clip)
    {
        if (clip != null)
        {
            loadedSoundClips[cacheKey] = clip;
        }

        if (!loadingSoundCallbacks.TryGetValue(cacheKey, out List<Action<AudioClip>> callbacks))
        {
            return;
        }

        loadingSoundCallbacks.Remove(cacheKey);
        for (int i = 0; i < callbacks.Count; i++)
        {
            callbacks[i]?.Invoke(clip);
        }
    }

    private AudioSource GetRuntimeSoundSource(int priority, bool allowSteal)
    {
        AudioSource stealCandidate = null;
        for (int i = runtimeSoundSources.Count - 1; i >= 0; i--)
        {
            AudioSource source = runtimeSoundSources[i];
            if (source == null)
            {
                runtimeSoundSources.RemoveAt(i);
                continue;
            }

            if (!source.isPlaying)
            {
                return source;
            }

            if (source.priority >= priority
                && (stealCandidate == null || source.priority > stealCandidate.priority))
            {
                stealCandidate = source;
            }
        }

        if (runtimeSoundSources.Count >= MaxRuntimeSoundSources)
        {
            if (!allowSteal || stealCandidate == null)
            {
                return null;
            }

            stealCandidate.Stop();
            return stealCandidate;
        }

        GameObject obj = new GameObject("RuntimeSound");
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;
        AudioSource audioSource = obj.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.ignoreListenerPause = false;
        runtimeSoundSources.Add(audioSource);
        return audioSource;
    }

    private bool IsWorldSoundAudible(Vector3 worldPosition, float maxDistance)
    {
        Transform listener = ResolveAudioListenerTransform();
        if (listener == null)
        {
            return true;
        }

        float audibleDistance = Mathf.Max(1f, maxDistance) * 1.1f;
        return (listener.position - worldPosition).sqrMagnitude <= audibleDistance * audibleDistance;
    }

    private Transform ResolveAudioListenerTransform()
    {
        if (activeAudioListener != null
            && activeAudioListener.enabled
            && activeAudioListener.gameObject.activeInHierarchy)
        {
            return activeAudioListener.transform;
        }

        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener == null || !listener.enabled || !listener.gameObject.activeInHierarchy)
            {
                continue;
            }

            activeAudioListener = listener;
            return listener.transform;
        }

        activeAudioListener = null;
        return null;
    }

#if UNITY_EDITOR
    private static bool TryLoadEditorSound(string abName, string soundName, out AudioClip clip)
    {
        clip = null;
        string searchRoot;
        if (abName == UIAudioBundleName)
        {
            searchRoot = EditorUIAudioRoot;
        }
        else if (abName == EnemyAudioBundleName)
        {
            searchRoot = EditorEnemyAudioRoot;
        }
        else if (abName == CombatFeedbackAudioBundleName)
        {
            searchRoot = EditorCombatAudioRoot;
        }
        else if (abName == EffectMgr.SkillEffectBundleName)
        {
            searchRoot = EditorCombatAudioRoot;
        }
        else
        {
            return false;
        }

        string[] guids = AssetDatabase.FindAssets(soundName + " t:AudioClip", new[] { searchRoot });
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!string.Equals(Path.GetFileNameWithoutExtension(assetPath), soundName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip != null)
            {
                return true;
            }
        }

        return false;
    }
#endif

    /// <summary>
    /// 停止音效
    /// </summary>
    public void StopSound(string soundName, AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        soundName = "Music/Sound/" + soundName;
        if (soundlist.Contains(source))
        {
            soundlist.Remove(source);
            source.Stop();
            PoolMgr.Instance.pushObj(soundName, source.gameObject);
        }
    }

    public void ChangeSoundVolume(float volume)
    {
        soundVolume = volume;
        if (uiSoundSource != null)
        {
            uiSoundSource.volume = soundVolume;
        }

        for (int i = soundlist.Count - 1; i >= 0; i--)
        {
            if (soundlist[i] == null)
            {
                soundlist.RemoveAt(i);
                continue;
            }

            soundlist[i].volume = soundVolume;
        }

        for (int i = runtimeSoundSources.Count - 1; i >= 0; i--)
        {
            if (runtimeSoundSources[i] == null)
            {
                runtimeSoundSources.RemoveAt(i);
                continue;
            }

            runtimeSoundSources[i].volume = soundVolume;
        }
    }

    private void Update()
    {
        if (berserkAudioBlend > 0.001f && EnsureBerserkAudioFilters())
        {
            ApplyBerserkAudioValues();
        }

        if (soundlist.Count == 0) return;
        for (int i = soundlist.Count - 1; i >= 0; i--)
        {
            if (soundlist[i] == null)
            {
                soundlist.RemoveAt(i);
                continue;
            }

            string soundName = soundlist[i].name;
            if (!soundlist[i].isPlaying)
            {
                PoolMgr.Instance.pushObj(soundName, soundlist[i].gameObject);
                soundlist.RemoveAt(i);
            }
        }
    }
}
