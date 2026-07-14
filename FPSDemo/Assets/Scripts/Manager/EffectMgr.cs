using System.Collections.Generic;
using PlayerData;
using UnityEngine;

public class EffectMgr : UnitySingleTonMono<EffectMgr>
{
    public const string SkillEffectBundleName = "skill";
    private const string PushRoarEffectName = "Onomatopoeia_Roar Variant";
    private const float PushRoarFallbackLifeTime = 2.5f;
    private const float GrenadeExplosionFallbackLifeTime = 2.5f;

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

    private void OnEnable()
    {
        EventCenter.Instance.AddEventListener<SkillVisualEventData>(GameEvent.SkillVisualStarted, OnSkillVisualStarted);
    }

    private void OnDisable()
    {
        EventCenter.Instance.RemoveEventListener<SkillVisualEventData>(GameEvent.SkillVisualStarted, OnSkillVisualStarted);
    }

    private void OnSkillVisualStarted(SkillVisualEventData eventData)
    {
        if (eventData.skillType == SkillType.Grenade)
        {
            PlayGrenadeExplosion(eventData);
            return;
        }

        if (eventData.skillType != SkillType.Push
            || !string.Equals(eventData.effectKey, PushRoarEffectName))
        {
            return;
        }

        Transform cameraTransform = ResolvePlayerCamera(eventData.player);
        if (cameraTransform == null)
        {
            Debug.LogWarning("[EffectMgr] 推人特效找不到玩家摄像机", this);
            return;
        }

        PlayEffectForAB(
            eventData.effectKey,
            cameraTransform,
            Vector3.zero,
            Quaternion.identity,
            SkillEffectBundleName,
            PushRoarFallbackLifeTime,
            true);
    }

    private void PlayGrenadeExplosion(SkillVisualEventData eventData)
    {
        if (!string.IsNullOrEmpty(eventData.effectKey))
        {
            PlayEffectForAB(
                eventData.effectKey,
                eventData.position,
                Quaternion.identity,
                SkillEffectBundleName,
                GrenadeExplosionFallbackLifeTime);
        }

        if (!string.IsNullOrEmpty(eventData.audioKey))
        {
            MusicMgr.Instance?.PlayWorldSoundForAB(
                eventData.audioKey,
                eventData.position,
                SkillEffectBundleName,
                1f,
                0.04f,
                2f,
                34f,
                96,
                true,
                1.18f,
                2.1f);
        }
    }

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

    public void PlayEffectForAB(
        string effectName,
        Transform parent,
        Vector3 localPosition,
        Quaternion localRotation,
        string abName,
        float fallbackLifeTime,
        bool forceLocalSimulation)
    {
        if (string.IsNullOrEmpty(effectName) || parent == null)
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

            PlayEffectObject(
                effectName,
                effectObj,
                parent,
                localPosition,
                localRotation,
                fallbackLifeTime,
                forceLocalSimulation);
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

        effectObj.transform.SetParent(null, true);
        effectObj.transform.position = position;
        effectObj.transform.rotation = rotation;

        PlayEffectObject(poolName, effectObj, fallbackLifeTime, false);
    }

    private void PlayEffectObject(
        string poolName,
        GameObject effectObj,
        Transform parent,
        Vector3 localPosition,
        Quaternion localRotation,
        float fallbackLifeTime,
        bool forceLocalSimulation)
    {
        RemovePlayingEffect(effectObj);
        effectObj.transform.SetParent(parent, false);
        effectObj.transform.localPosition = localPosition;
        effectObj.transform.localRotation = localRotation;
        effectObj.transform.localScale = Vector3.one;

        PlayEffectObject(poolName, effectObj, fallbackLifeTime, forceLocalSimulation);
    }

    private void PlayEffectObject(string poolName, GameObject effectObj, float fallbackLifeTime, bool forceLocalSimulation)
    {
        ParticleSystem[] particleSystems = effectObj.GetComponentsInChildren<ParticleSystem>(true);
        if (particleSystems.Length == 0)
        {
            Debug.LogError($"[EffectMgr] 特效资源没有粒子系统 {poolName}", effectObj);
            PoolMgr.Instance.pushObj(poolName, effectObj);
            return;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem.MainModule main = particleSystems[i].main;
            main.loop = false;
            if (forceLocalSimulation)
            {
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
            }
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

    private static Transform ResolvePlayerCamera(PlayerController player)
    {
        if (player != null
            && player.CameraController != null
            && player.CameraController.PlayerCamera != null)
        {
            return player.CameraController.PlayerCamera.transform;
        }

        Camera mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.transform : null;
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
