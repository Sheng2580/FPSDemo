using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    // 资源表放在 ABRes 下 由数据 key 映射到特效和音效资源
    [CreateAssetMenu(menuName = "FPSDemo/Combat/Combat Feedback Resources", fileName = "CombatFeedbackResources")]
    public class CombatFeedbackResourceLibrary : ScriptableObject
    {
        public const string DefaultAssetBundleName = "combat_feedback";

        [Serializable]
        public class EffectEntry
        {
            public string key;
            public string assetBundleName = DefaultAssetBundleName;
            public string assetName;
            public GameObject prefab;

            public string ResolveAssetBundleName()
            {
                return string.IsNullOrEmpty(assetBundleName) ? DefaultAssetBundleName : assetBundleName;
            }

            public string ResolveAssetName()
            {
                if (!string.IsNullOrEmpty(assetName))
                {
                    return assetName;
                }

                return prefab != null ? prefab.name : key;
            }
        }

        [Serializable]
        public class AudioEntry
        {
            public string key;
            public string assetBundleName = DefaultAssetBundleName;
            public string assetName;
            public AudioClip clip;

            public string ResolveAssetBundleName()
            {
                return string.IsNullOrEmpty(assetBundleName) ? DefaultAssetBundleName : assetBundleName;
            }

            public string ResolveAssetName()
            {
                if (!string.IsNullOrEmpty(assetName))
                {
                    return assetName;
                }

                return clip != null ? clip.name : key;
            }
        }

        [SerializeField] private EffectEntry[] effectEntries;
        [SerializeField] private AudioEntry[] audioEntries;

        private Dictionary<string, EffectEntry> _effectMap;
        private Dictionary<string, AudioEntry> _audioMap;

        public GameObject GetEffectPrefab(string key)
        {
            EnsureMaps();
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            _effectMap.TryGetValue(key, out EffectEntry entry);
            return entry?.prefab;
        }

        public AudioClip GetAudioClip(string key)
        {
            EnsureMaps();
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            _audioMap.TryGetValue(key, out AudioEntry entry);
            return entry?.clip;
        }

        public bool TryGetEffectAssetLocation(string key, out string assetBundleName, out string assetName)
        {
            assetBundleName = string.Empty;
            assetName = string.Empty;
            EnsureMaps();

            if (string.IsNullOrEmpty(key) || !_effectMap.TryGetValue(key, out EffectEntry entry) || entry == null)
            {
                return false;
            }

            assetBundleName = entry.ResolveAssetBundleName();
            assetName = entry.ResolveAssetName();
            return !string.IsNullOrEmpty(assetBundleName) && !string.IsNullOrEmpty(assetName);
        }

        public bool TryGetAudioAssetLocation(string key, out string assetBundleName, out string assetName)
        {
            assetBundleName = string.Empty;
            assetName = string.Empty;
            EnsureMaps();

            if (string.IsNullOrEmpty(key) || !_audioMap.TryGetValue(key, out AudioEntry entry) || entry == null)
            {
                return false;
            }

            assetBundleName = entry.ResolveAssetBundleName();
            assetName = entry.ResolveAssetName();
            return !string.IsNullOrEmpty(assetBundleName) && !string.IsNullOrEmpty(assetName);
        }

        private void EnsureMaps()
        {
            if (_effectMap != null && _audioMap != null)
            {
                return;
            }

            _effectMap = new Dictionary<string, EffectEntry>(StringComparer.OrdinalIgnoreCase);
            _audioMap = new Dictionary<string, AudioEntry>(StringComparer.OrdinalIgnoreCase);

            if (effectEntries != null)
            {
                for (int i = 0; i < effectEntries.Length; i++)
                {
                    EffectEntry entry = effectEntries[i];
                    if (entry == null || string.IsNullOrEmpty(entry.key))
                    {
                        continue;
                    }

                    _effectMap[entry.key] = entry;
                }
            }

            if (audioEntries != null)
            {
                for (int i = 0; i < audioEntries.Length; i++)
                {
                    AudioEntry entry = audioEntries[i];
                    if (entry == null || string.IsNullOrEmpty(entry.key))
                    {
                        continue;
                    }

                    _audioMap[entry.key] = entry;
                }
            }
        }
    }
}
