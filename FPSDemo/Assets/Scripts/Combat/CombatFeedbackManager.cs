using System;
using System.Collections.Generic;
using UnityEngine;
using Weapon.Data;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace Combat
{
    // 监听武器事件 播放枪口火光 枪声 命中特效
    public class CombatFeedbackManager : MonoBehaviour
    {
        private const string DefaultResourceLibraryBundleName = CombatFeedbackResourceLibrary.DefaultAssetBundleName;
        private const string DefaultResourceLibraryAssetName = "CombatFeedbackResources";
#if UNITY_EDITOR
        private const string EditorResourceLibraryPath = "Assets/Art/ABRes/CombatFeedback/CombatFeedbackResources.asset";
#endif

        private class PlayingEffect
        {
            public GameObject prefab;
            public GameObject instance;
            public ParticleSystem[] particleSystems;
            public float startTime;
            public float fallbackLifeTime;
        }

        private struct EffectPlayRequest
        {
            public Vector3 position;
            public Quaternion rotation;
            public float intensity;
        }

        private struct AudioPlayRequest
        {
            public Vector3 position;
            public float volume;
            public float pitchRandom;
            public float spatialBlend;
        }

        private static CombatFeedbackManager instance;

        [Header("资源")]
        [SerializeField] private CombatFeedbackResourceLibrary resourceLibrary;
        [SerializeField] private Transform effectRoot;

        [Header("播放参数")]
        [SerializeField] private float effectFallbackLifeTime = 3f;
        [SerializeField] private float impactSurfaceOffset = 0.02f;
        [SerializeField] private float fireAudioSpatialBlend = 0.15f;
        [SerializeField] private float impactAudioSpatialBlend = 1f;

        [Header("调试")]
        [SerializeField] private bool debugFeedback = true;
#if UNITY_EDITOR
        [SerializeField] private bool preferEditorDirectReferences = true;
#endif

        private readonly Dictionary<GameObject, Queue<GameObject>> _effectPools = new Dictionary<GameObject, Queue<GameObject>>();
        private readonly List<PlayingEffect> _playingEffects = new List<PlayingEffect>();
        private readonly List<AudioSource> _audioSources = new List<AudioSource>();
        private readonly Dictionary<string, float> _nextAudioTimes = new Dictionary<string, float>();
        private readonly HashSet<string> _missingResourceWarnings = new HashSet<string>();
        private readonly Dictionary<string, GameObject> _loadedEffectPrefabs = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AudioClip> _loadedAudioClips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _loadingEffectKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _loadingAudioKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<EffectPlayRequest>> _pendingEffectRequests = new Dictionary<string, List<EffectPlayRequest>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<AudioPlayRequest>> _pendingAudioRequests = new Dictionary<string, List<AudioPlayRequest>>(StringComparer.OrdinalIgnoreCase);
        private bool _resourceLibraryLoading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            instance = null;
        }

        public static CombatFeedbackManager EnsureExists()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindObjectOfType<CombatFeedbackManager>();
            if (instance != null)
            {
                return instance;
            }

            GameObject obj = new GameObject(nameof(CombatFeedbackManager));
            return obj.AddComponent<CombatFeedbackManager>();
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
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureEffectRoot();
            BeginLoadDefaultResourceLibrary();
        }

        private void OnEnable()
        {
            EventCenter.Instance.AddEventListener<WeaponFiredEventData>(GameEvent.WeaponFired, OnWeaponFired);
            EventCenter.Instance.AddEventListener<WeaponHitEventData>(GameEvent.WeaponHit, OnWeaponHit);
        }

        private void OnDisable()
        {
            EventCenter.Instance.RemoveEventListener<WeaponFiredEventData>(GameEvent.WeaponFired, OnWeaponFired);
            EventCenter.Instance.RemoveEventListener<WeaponHitEventData>(GameEvent.WeaponHit, OnWeaponHit);
        }

        private void Update()
        {
            UpdatePlayingEffects();
            RemoveDestroyedAudioSources();
        }

        private void OnWeaponFired(WeaponFiredEventData eventData)
        {
            // 武器数据只提供 key 这里负责把 key 转成实际表现
            WeaponConfig config = eventData.config;
            string muzzleFlashKey = ResolveKey(config?.muzzleFlashEffectKey, WeaponConfig.DefaultMuzzleFlashEffectKey);
            string muzzleSmokeKey = ResolveKey(config?.muzzleSmokeEffectKey, WeaponConfig.DefaultMuzzleSmokeEffectKey);
            string fireAudioKey = ResolveKey(config?.fireAudioKey, WeaponConfig.DefaultPistolFireAudioKey);
            float volume = config != null && config.fireVolume > 0f ? config.fireVolume : WeaponConfig.DefaultFireVolume;
            float pitchRandom = config != null && config.firePitchRandom >= 0f ? config.firePitchRandom : WeaponConfig.DefaultFirePitchRandom;
            float audioCooldown = config != null && config.fireAudioCooldown >= 0f ? config.fireAudioCooldown : WeaponConfig.DefaultFireAudioCooldown;
            float intensity = config != null && config.fireFeedbackIntensity > 0f ? config.fireFeedbackIntensity : WeaponConfig.DefaultFireFeedbackIntensity;

            PlayEffect(muzzleFlashKey, eventData.muzzlePosition, eventData.muzzleRotation, intensity);
            PlayEffect(muzzleSmokeKey, eventData.muzzlePosition, eventData.muzzleRotation, intensity);
            PlayAudio(fireAudioKey, eventData.muzzlePosition, volume, pitchRandom, audioCooldown, fireAudioSpatialBlend);
        }

        private void OnWeaponHit(WeaponHitEventData eventData)
        {
            // 伤害结算已经完成 这里只处理命中后的画面和声音
            DamageInfo damageInfo = eventData.damageInfo;
            if (damageInfo.hitCollider == null)
            {
                return;
            }

            HitSurfaceType surfaceType = ResolveSurfaceType(damageInfo.hitCollider, eventData.hitEnemy);
            string impactEffectKey = ResolveImpactEffectKey(eventData.config, surfaceType);
            Vector3 position = damageInfo.hitPoint;
            if (damageInfo.hitNormal.sqrMagnitude > 0.0001f)
            {
                position += damageInfo.hitNormal.normalized * impactSurfaceOffset;
            }

            PlayEffect(impactEffectKey, position, ResolveImpactRotation(damageInfo.hitNormal), 1f);
            string impactAudioKey = ResolveImpactAudioKey(eventData.config, surfaceType);
            PlayAudio(impactAudioKey, position, 1f, 0.03f, 0f, impactAudioSpatialBlend);
        }

        private void BeginLoadDefaultResourceLibrary()
        {
            if (resourceLibrary != null || _resourceLibraryLoading)
            {
                return;
            }

#if UNITY_EDITOR
            if (preferEditorDirectReferences && TryLoadEditorResourceLibrary())
            {
                FlushPendingResourceRequests();
                return;
            }
#endif

            _resourceLibraryLoading = true;
            ABManager.Instance.LoadAssetAsync<CombatFeedbackResourceLibrary>(
                DefaultResourceLibraryBundleName,
                DefaultResourceLibraryAssetName,
                loadedLibrary =>
                {
                    _resourceLibraryLoading = false;
                    if (loadedLibrary != null)
                    {
                        resourceLibrary = loadedLibrary;
                        FlushPendingResourceRequests();
                        return;
                    }

                    if (TryLoadEditorResourceLibrary())
                    {
                        FlushPendingResourceRequests();
                        return;
                    }

                    if (debugFeedback)
                    {
                        Debug.LogWarning("[CombatFeedback] 缺少 CombatFeedbackResources 资源表", this);
                    }
                });
        }

        private bool TryLoadEditorResourceLibrary()
        {
#if UNITY_EDITOR
            resourceLibrary = AssetDatabase.LoadAssetAtPath<CombatFeedbackResourceLibrary>(EditorResourceLibraryPath);
            return resourceLibrary != null;
#else
            return false;
#endif
        }

        private void FlushPendingResourceRequests()
        {
            if (_pendingEffectRequests.Count > 0)
            {
                string[] keys = new string[_pendingEffectRequests.Keys.Count];
                _pendingEffectRequests.Keys.CopyTo(keys, 0);
                for (int i = 0; i < keys.Length; i++)
                {
                    BeginLoadEffectPrefab(keys[i]);
                }
            }

            if (_pendingAudioRequests.Count > 0)
            {
                string[] keys = new string[_pendingAudioRequests.Keys.Count];
                _pendingAudioRequests.Keys.CopyTo(keys, 0);
                for (int i = 0; i < keys.Length; i++)
                {
                    BeginLoadAudioClip(keys[i]);
                }
            }
        }

        private Transform EnsureEffectRoot()
        {
            if (effectRoot != null)
            {
                return effectRoot;
            }

            GameObject root = new GameObject("CombatFeedbackEffects");
            root.transform.SetParent(transform);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            effectRoot = root.transform;
            return effectRoot;
        }

        private void PlayEffect(string key, Vector3 position, Quaternion rotation, float intensity)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (_loadedEffectPrefabs.TryGetValue(key, out GameObject prefab) && prefab != null)
            {
                SpawnEffect(prefab, position, rotation, intensity);
                return;
            }

            QueuePendingEffect(key, position, rotation, intensity);
            BeginLoadEffectPrefab(key);
        }

        private void SpawnEffect(GameObject prefab, Vector3 position, Quaternion rotation, float intensity)
        {
            // 使用内部对象池 避免连续开火时频繁创建销毁
            GameObject effectObject = GetEffectInstance(prefab);
            Transform effectTransform = effectObject.transform;
            effectTransform.SetParent(EnsureEffectRoot(), false);
            effectTransform.position = position;
            effectTransform.rotation = rotation;
            effectTransform.localScale = prefab.transform.localScale * Mathf.Max(0.01f, intensity);
            effectObject.SetActive(true);

            ParticleSystem[] particleSystems = effectObject.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                ParticleSystem.MainModule main = particleSystem.main;
                main.loop = false;
                particleSystem.Clear(true);
                particleSystem.Play(true);
            }

            TrailRenderer[] trails = effectObject.GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trails.Length; i++)
            {
                trails[i].Clear();
            }

            _playingEffects.Add(new PlayingEffect
            {
                prefab = prefab,
                instance = effectObject,
                particleSystems = particleSystems,
                startTime = Time.time,
                fallbackLifeTime = Mathf.Max(0.1f, effectFallbackLifeTime)
            });
        }

        private void BeginLoadEffectPrefab(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (_loadedEffectPrefabs.TryGetValue(key, out GameObject prefab) && prefab != null)
            {
                FlushPendingEffectRequests(key);
                return;
            }

            if (resourceLibrary == null)
            {
                BeginLoadDefaultResourceLibrary();
                return;
            }

#if UNITY_EDITOR
            if (preferEditorDirectReferences && TryUseDirectEffectFallback(key))
            {
                FlushPendingEffectRequests(key);
                return;
            }
#endif

            if (!_loadingEffectKeys.Add(key))
            {
                return;
            }

            if (!resourceLibrary.TryGetEffectAssetLocation(key, out string abName, out string assetName))
            {
                _loadingEffectKeys.Remove(key);
                if (!TryUseDirectEffectFallback(key))
                {
                    WarnMissingResource("特效", key);
                    ClearPendingEffectRequests(key);
                    return;
                }

                FlushPendingEffectRequests(key);
                return;
            }

            ABManager.Instance.LoadAssetAsync<GameObject>(abName, assetName, loadedPrefab =>
            {
                _loadingEffectKeys.Remove(key);
                if (loadedPrefab != null)
                {
                    _loadedEffectPrefabs[key] = loadedPrefab;
                    FlushPendingEffectRequests(key);
                    return;
                }

                if (TryUseDirectEffectFallback(key))
                {
                    FlushPendingEffectRequests(key);
                    return;
                }

                WarnMissingResource("特效", key);
                ClearPendingEffectRequests(key);
            });
        }

        private bool TryUseDirectEffectFallback(string key)
        {
            GameObject fallbackPrefab = resourceLibrary != null ? resourceLibrary.GetEffectPrefab(key) : null;
            if (fallbackPrefab == null)
            {
                return false;
            }

            _loadedEffectPrefabs[key] = fallbackPrefab;
            return true;
        }

        private void QueuePendingEffect(string key, Vector3 position, Quaternion rotation, float intensity)
        {
            if (!_pendingEffectRequests.TryGetValue(key, out List<EffectPlayRequest> requests))
            {
                requests = new List<EffectPlayRequest>();
                _pendingEffectRequests.Add(key, requests);
            }

            requests.Add(new EffectPlayRequest
            {
                position = position,
                rotation = rotation,
                intensity = intensity
            });
        }

        private void FlushPendingEffectRequests(string key)
        {
            if (!_loadedEffectPrefabs.TryGetValue(key, out GameObject prefab) || prefab == null)
            {
                return;
            }

            if (!_pendingEffectRequests.TryGetValue(key, out List<EffectPlayRequest> requests))
            {
                return;
            }

            _pendingEffectRequests.Remove(key);
            for (int i = 0; i < requests.Count; i++)
            {
                EffectPlayRequest request = requests[i];
                SpawnEffect(prefab, request.position, request.rotation, request.intensity);
            }
        }

        private void ClearPendingEffectRequests(string key)
        {
            _pendingEffectRequests.Remove(key);
        }

        private GameObject GetEffectInstance(GameObject prefab)
        {
            if (!_effectPools.TryGetValue(prefab, out Queue<GameObject> pool))
            {
                pool = new Queue<GameObject>();
                _effectPools.Add(prefab, pool);
            }

            while (pool.Count > 0)
            {
                GameObject pooledObject = pool.Dequeue();
                if (pooledObject != null)
                {
                    return pooledObject;
                }
            }

            Object sourceObject = prefab;
            Object spawnedObject = Instantiate(sourceObject, EnsureEffectRoot());
            if (spawnedObject is GameObject spawnedGameObject)
            {
                return spawnedGameObject;
            }

            if (spawnedObject is Component spawnedComponent)
            {
                return spawnedComponent.gameObject;
            }

            Debug.LogError($"[CombatFeedback] 特效实例化结果不是 GameObject KeyPrefab={prefab.name}", prefab);
            Destroy(spawnedObject);
            return new GameObject(prefab.name);
        }

        private void UpdatePlayingEffects()
        {
            for (int i = _playingEffects.Count - 1; i >= 0; i--)
            {
                PlayingEffect effect = _playingEffects[i];
                if (effect.instance == null)
                {
                    _playingEffects.RemoveAt(i);
                    continue;
                }

                if (!IsEffectFinished(effect))
                {
                    continue;
                }

                ReturnEffect(effect);
                _playingEffects.RemoveAt(i);
            }
        }

        private bool IsEffectFinished(PlayingEffect effect)
        {
            if (Time.time - effect.startTime >= effect.fallbackLifeTime)
            {
                return true;
            }

            if (effect.particleSystems == null || effect.particleSystems.Length == 0)
            {
                return Time.time - effect.startTime >= effect.fallbackLifeTime;
            }

            for (int i = 0; i < effect.particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = effect.particleSystems[i];
                if (particleSystem != null && particleSystem.IsAlive(true))
                {
                    return false;
                }
            }

            return true;
        }

        private void ReturnEffect(PlayingEffect effect)
        {
            if (effect.prefab == null || effect.instance == null)
            {
                return;
            }

            effect.instance.SetActive(false);
            effect.instance.transform.SetParent(EnsureEffectRoot(), false);

            if (!_effectPools.TryGetValue(effect.prefab, out Queue<GameObject> pool))
            {
                pool = new Queue<GameObject>();
                _effectPools.Add(effect.prefab, pool);
            }

            pool.Enqueue(effect.instance);
        }

        private void PlayAudio(string key, Vector3 position, float volume, float pitchRandom, float cooldown, float spatialBlend)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (cooldown > 0f && _nextAudioTimes.TryGetValue(key, out float nextTime) && Time.time < nextTime)
            {
                return;
            }

            if (cooldown > 0f)
            {
                _nextAudioTimes[key] = Time.time + cooldown;
            }

            if (_loadedAudioClips.TryGetValue(key, out AudioClip clip) && clip != null)
            {
                PlayLoadedAudio(clip, position, volume, pitchRandom, spatialBlend);
                return;
            }

            QueuePendingAudio(key, position, volume, pitchRandom, spatialBlend);
            BeginLoadAudioClip(key);
        }

        private void PlayLoadedAudio(AudioClip clip, Vector3 position, float volume, float pitchRandom, float spatialBlend)
        {
            AudioSource source = GetAudioSource();
            source.transform.position = position;
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = 1f + UnityEngine.Random.Range(-Mathf.Abs(pitchRandom), Mathf.Abs(pitchRandom));
            source.loop = false;
            source.spatialBlend = Mathf.Clamp01(spatialBlend);
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = 45f;
            source.Play();
        }

        private void BeginLoadAudioClip(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (_loadedAudioClips.TryGetValue(key, out AudioClip clip) && clip != null)
            {
                FlushPendingAudioRequests(key);
                return;
            }

            if (resourceLibrary == null)
            {
                BeginLoadDefaultResourceLibrary();
                return;
            }

#if UNITY_EDITOR
            if (preferEditorDirectReferences && TryUseDirectAudioFallback(key))
            {
                FlushPendingAudioRequests(key);
                return;
            }
#endif

            if (!_loadingAudioKeys.Add(key))
            {
                return;
            }

            if (!resourceLibrary.TryGetAudioAssetLocation(key, out string abName, out string assetName))
            {
                _loadingAudioKeys.Remove(key);
                if (!TryUseDirectAudioFallback(key))
                {
                    WarnMissingResource("音频", key);
                    ClearPendingAudioRequests(key);
                    return;
                }

                FlushPendingAudioRequests(key);
                return;
            }

            ABManager.Instance.LoadAssetAsync<AudioClip>(abName, assetName, loadedClip =>
            {
                _loadingAudioKeys.Remove(key);
                if (loadedClip != null)
                {
                    _loadedAudioClips[key] = loadedClip;
                    FlushPendingAudioRequests(key);
                    return;
                }

                if (TryUseDirectAudioFallback(key))
                {
                    FlushPendingAudioRequests(key);
                    return;
                }

                WarnMissingResource("音频", key);
                ClearPendingAudioRequests(key);
            });
        }

        private bool TryUseDirectAudioFallback(string key)
        {
            AudioClip fallbackClip = resourceLibrary != null ? resourceLibrary.GetAudioClip(key) : null;
            if (fallbackClip == null)
            {
                return false;
            }

            _loadedAudioClips[key] = fallbackClip;
            return true;
        }

        private void QueuePendingAudio(string key, Vector3 position, float volume, float pitchRandom, float spatialBlend)
        {
            if (!_pendingAudioRequests.TryGetValue(key, out List<AudioPlayRequest> requests))
            {
                requests = new List<AudioPlayRequest>();
                _pendingAudioRequests.Add(key, requests);
            }

            requests.Add(new AudioPlayRequest
            {
                position = position,
                volume = volume,
                pitchRandom = pitchRandom,
                spatialBlend = spatialBlend
            });
        }

        private void FlushPendingAudioRequests(string key)
        {
            if (!_loadedAudioClips.TryGetValue(key, out AudioClip clip) || clip == null)
            {
                return;
            }

            if (!_pendingAudioRequests.TryGetValue(key, out List<AudioPlayRequest> requests))
            {
                return;
            }

            _pendingAudioRequests.Remove(key);
            for (int i = 0; i < requests.Count; i++)
            {
                AudioPlayRequest request = requests[i];
                PlayLoadedAudio(clip, request.position, request.volume, request.pitchRandom, request.spatialBlend);
            }
        }

        private void ClearPendingAudioRequests(string key)
        {
            _pendingAudioRequests.Remove(key);
        }

        private AudioSource GetAudioSource()
        {
            for (int i = 0; i < _audioSources.Count; i++)
            {
                AudioSource source = _audioSources[i];
                if (source != null && !source.isPlaying)
                {
                    return source;
                }
            }

            GameObject audioObject = new GameObject("CombatFeedbackAudio");
            audioObject.transform.SetParent(transform);
            audioObject.transform.localPosition = Vector3.zero;
            audioObject.transform.localRotation = Quaternion.identity;
            audioObject.transform.localScale = Vector3.one;

            AudioSource audioSource = audioObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            _audioSources.Add(audioSource);
            return audioSource;
        }

        private void RemoveDestroyedAudioSources()
        {
            for (int i = _audioSources.Count - 1; i >= 0; i--)
            {
                if (_audioSources[i] == null)
                {
                    _audioSources.RemoveAt(i);
                }
            }
        }

        private HitSurfaceType ResolveSurfaceType(Collider hitCollider, bool hitEnemy)
        {
            // 敌人命中统一当作 Flesh 没有标记的墙体走默认石头效果
            if (hitEnemy)
            {
                return HitSurfaceType.Flesh;
            }

            HitSurface hitSurface = hitCollider.GetComponentInParent<HitSurface>();
            hitSurface ??= hitCollider.GetComponentInChildren<HitSurface>();
            return hitSurface != null ? hitSurface.SurfaceType : HitSurfaceType.Default;
        }

        private string ResolveImpactEffectKey(WeaponConfig config, HitSurfaceType surfaceType)
        {
            HitSurfaceFeedbackConfig feedbackConfig = FindSurfaceFeedback(config, surfaceType);
            if (feedbackConfig != null && !string.IsNullOrEmpty(feedbackConfig.impactEffectKey))
            {
                return feedbackConfig.impactEffectKey;
            }

            if (config != null && !string.IsNullOrEmpty(config.defaultImpactEffectKey))
            {
                return config.defaultImpactEffectKey;
            }

            return WeaponConfig.GetDefaultImpactEffectKey(surfaceType);
        }

        private string ResolveImpactAudioKey(WeaponConfig config, HitSurfaceType surfaceType)
        {
            HitSurfaceFeedbackConfig feedbackConfig = FindSurfaceFeedback(config, surfaceType);
            return feedbackConfig != null ? feedbackConfig.impactAudioKey : string.Empty;
        }

        private HitSurfaceFeedbackConfig FindSurfaceFeedback(WeaponConfig config, HitSurfaceType surfaceType)
        {
            if (config?.hitSurfaceFeedbacks == null)
            {
                return null;
            }

            for (int i = 0; i < config.hitSurfaceFeedbacks.Length; i++)
            {
                HitSurfaceFeedbackConfig feedbackConfig = config.hitSurfaceFeedbacks[i];
                if (feedbackConfig != null && feedbackConfig.surfaceType == surfaceType)
                {
                    return feedbackConfig;
                }
            }

            return null;
        }

        private Quaternion ResolveImpactRotation(Vector3 normal)
        {
            if (normal.sqrMagnitude <= 0.0001f)
            {
                return Quaternion.identity;
            }

            return Quaternion.LookRotation(normal.normalized);
        }

        private string ResolveKey(string key, string fallbackKey)
        {
            return string.IsNullOrEmpty(key) ? fallbackKey : key;
        }

        private void WarnMissingResource(string resourceType, string key)
        {
            if (!debugFeedback)
            {
                return;
            }

            string warningKey = resourceType + ":" + key;
            if (!_missingResourceWarnings.Add(warningKey))
            {
                return;
            }

            Debug.LogWarning($"[CombatFeedback] 缺少{resourceType}资源 Key={key}", this);
        }
    }
}
