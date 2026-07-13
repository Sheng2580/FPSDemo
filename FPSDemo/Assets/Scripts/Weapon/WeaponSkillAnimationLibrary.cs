using System;
using System.Collections.Generic;
using UnityEngine;

namespace Weapon
{
    [CreateAssetMenu(menuName = "FPSDemo/Weapon/Skill Animation Library", fileName = "WeaponSkillAnimationLibrary")]
    public class WeaponSkillAnimationLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string key;
            public AnimationClip clip;
        }

        [SerializeField] private Entry[] entries;

        private Dictionary<string, AnimationClip> _clipLookup;

        public bool TryGetClip(string key, out AnimationClip clip)
        {
            clip = null;
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            EnsureLookup();
            return _clipLookup.TryGetValue(key, out clip) && clip != null;
        }

        private void EnsureLookup()
        {
            if (_clipLookup != null)
            {
                return;
            }

            _clipLookup = new Dictionary<string, AnimationClip>();
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.key) || entry.clip == null)
                {
                    continue;
                }

                _clipLookup[entry.key] = entry.clip;
            }
        }
    }
}
