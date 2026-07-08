using System.Collections.Generic;
using UnityEngine;

public class EffectMgr : UnitySingleTonMono<EffectMgr>
{
    private class PlayingEffect
    {
        public string poolName;
        public GameObject obj;
        public ParticleSystem[] particleSystems;
        public float startTime;
        public float fallbackLifeTime;
    }

    private readonly List<PlayingEffect> playingEffects = new List<PlayingEffect>();
    private const float DefaultFallbackLifeTime = 3f;

    public void PlayEffectForAB(string effectName, Vector3 position, Quaternion rotation, string abName = "effects", float fallbackLifeTime = DefaultFallbackLifeTime)
    {
        if (string.IsNullOrEmpty(effectName))
        {
            return;
        }

        PoolMgr.Instance.GetObjForAB(abName, effectName, effectObj =>
        {
            if (effectObj == null)
            {
                Debug.LogError("[EffectMgr] Load effect failed: " + effectName);
                return;
            }

            PlayEffectObject(effectName, effectObj, position, rotation, fallbackLifeTime);
        });
    }

    // public void PlayEffectForAB(Effects effectData, Transform origin, string abName = "effects", float fallbackLifeTime = DefaultFallbackLifeTime)
    // {
    //     if (effectData == null || origin == null || string.IsNullOrEmpty(effectData.effectsName))
    //     {
    //         return;
    //     }
    //
    //     Vector3 position = origin.TransformPoint(effectData.effectsPos);
    //     Quaternion rotation = origin.rotation * Quaternion.Euler(effectData.effectRot);
    //     PlayEffectForAB(effectData.effectsName, position, rotation, abName, fallbackLifeTime);
    // }

    private void PlayEffectObject(string poolName, GameObject effectObj, Vector3 position, Quaternion rotation, float fallbackLifeTime)
    {
        RemovePlayingEffect(effectObj);

        effectObj.transform.position = position;
        effectObj.transform.rotation = rotation;

        ParticleSystem[] particleSystems = effectObj.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem.MainModule main = particleSystems[i].main;
            main.loop = false;
            particleSystems[i].Clear(true);
            particleSystems[i].Play(true);
        }

        TrailRenderer[] trails = effectObj.GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trails.Length; i++)
        {
            trails[i].Clear();
        }

        playingEffects.Add(new PlayingEffect
        {
            poolName = poolName,
            obj = effectObj,
            particleSystems = particleSystems,
            startTime = Time.time,
            fallbackLifeTime = Mathf.Max(0.1f, fallbackLifeTime)
        });
    }

    private void Update()
    {
        for (int i = playingEffects.Count - 1; i >= 0; i--)
        {
            PlayingEffect effect = playingEffects[i];
            if (effect.obj == null)
            {
                playingEffects.RemoveAt(i);
                continue;
            }

            if (!IsEffectFinished(effect))
            {
                continue;
            }

            PoolMgr.Instance.pushObj(effect.poolName, effect.obj);
            playingEffects.RemoveAt(i);
        }
    }

    private bool IsEffectFinished(PlayingEffect effect)
    {
        if (Time.time - effect.startTime >= effect.fallbackLifeTime)
        {
            return true;
        }

        if (effect.particleSystems != null && effect.particleSystems.Length > 0)
        {
            for (int i = 0; i < effect.particleSystems.Length; i++)
            {
                if (effect.particleSystems[i] != null && effect.particleSystems[i].IsAlive(true))
                {
                    return false;
                }
            }

            return true;
        }

        return Time.time - effect.startTime >= effect.fallbackLifeTime;
    }

    private void RemovePlayingEffect(GameObject effectObj)
    {
        for (int i = playingEffects.Count - 1; i >= 0; i--)
        {
            if (playingEffects[i].obj == effectObj)
            {
                playingEffects.RemoveAt(i);
            }
        }
    }
}
