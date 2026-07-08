using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MusicMgr : UnitySingleTonMono<MusicMgr>
{
    private AudioSource bkMusic; //音频组件
    private float bkVolume = 1; //背景音乐大小
    private float soundVolume = 1; //音效大小
    private Coroutine bgMusicTransitionCoroutine;
    private string currentBkMusicName;
    private int bgMusicRequestId;
    private const float DefaultBGMusicFadeDuration = 1f;
    
    private List<AudioSource> soundlist = new List<AudioSource>();

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
        ABManager.Instance.LoadResAsync<AudioClip>("bgm", name, clip =>
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

            SwitchBGMusic(clip, "AB:bgm/" + name, DefaultBGMusicFadeDuration);
        });
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

        //获取音频对象  //第一次肯定拿不到
        GameObject soundObj = PoolMgr.Instance.getObj(soundName);
        if (worldPosition.HasValue)
        {
            soundObj.transform.position = worldPosition.Value;
        }

        //获取音频组件
        AudioSource source = soundObj.GetComponent<AudioSource>();
        if (soundObj.GetComponent<AudioSource>() == null) 
            source = soundObj.AddComponent<AudioSource>();//如果音频组件为空则添加一个音频组件
        source.spatialBlend = worldPosition.HasValue ? 1f : 0f;
        //source.clip = ResMgr.Instance.load<AudioClip>("Music/Sound/" + soundName);//加载音频文件
         ABManager.Instance.LoadResAsync(abName,soundName,typeof(AudioClip), (obj) =>
        {
            if (source == null)
            {
                return;
            }

            AudioClip clip = obj as AudioClip;
            if (clip == null)
            {
                Debug.LogError("[MusicMgr] Load sound failed: " + soundName);
                PoolMgr.Instance.pushObj(soundName, soundObj);
                return;
            }

            //加载完事件
            source.clip = clip;
            source.volume = soundVolume;//设置音效大小
            source.loop = isLoop;//设置是否要循环播放
            source.Play();
            soundlist.Add(source);//将音频组件添加到音效列表中 
        });
    }
    
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
        for (int i = soundlist.Count - 1; i >= 0; i--)
        {
            if (soundlist[i] == null)
            {
                soundlist.RemoveAt(i);
                continue;
            }

            soundlist[i].volume = soundVolume;
        }
    }

    private void Update()
    {
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
