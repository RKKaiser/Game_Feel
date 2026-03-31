using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SoundType
{
    None,
    Gatling,            // 对应：加特林音效
    Fail,               // 对应：失败音效
    GrenadeThrow,       // 对应：手雷投掷
    GrenadeExplosion,   // 对应：手雷爆炸
    EnemyHit,           // 对应：敌人受击
    Shotgun,            // 对应：散弹弹枪
    BGM,                // 对应：游戏bgm
    Click,              // 对应：点击音效
    CrabUpgrade         // 对应：螃蟹升级
}

[System.Serializable]
public class SoundData
{
    public SoundType soundType;
    public AudioClip clip;
    [Range(0f, 1f)]
    public float volume = 1.0f;
    public bool loop = false;
}

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("音效配置")]
    public List<SoundData> soundDatas;

    [Header("音频源组件")]
    public AudioSource sfxSource;      // 用于普通音效（手雷、受击等）
    public AudioSource bgmSource;      // 用于背景音乐
    public AudioSource gatlingSource;  // 新增：专门用于加特林音效

    private Dictionary<SoundType, SoundData> soundDictionary;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 初始化字典
        soundDictionary = new Dictionary<SoundType, SoundData>();
        foreach (var data in soundDatas)
        {
            if (!soundDictionary.ContainsKey(data.soundType))
            {
                soundDictionary.Add(data.soundType, data);
            }
        }

        // 确保组件已赋值
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
        if (gatlingSource == null) gatlingSource = gameObject.AddComponent<AudioSource>();
    }

    /// <summary>
    /// 播放音效
    /// </summary>
    public void PlaySound(SoundType type)
    {
        if (!soundDictionary.ContainsKey(type)) return;

        SoundData data = soundDictionary[type];

        if (type == SoundType.BGM)
        {
            // 背景音乐逻辑
            bgmSource.clip = data.clip;
            bgmSource.volume = data.volume;
            bgmSource.loop = data.loop;
            if (!bgmSource.isPlaying)
                bgmSource.Play();
        }
        else if (type == SoundType.Gatling)
        {
            // 加特林逻辑：专门处理“新覆盖旧”
            if (data.clip != null)
            {
                gatlingSource.Stop(); // 1. 强制停止当前正在播放的
                gatlingSource.clip = data.clip;
                gatlingSource.volume = data.volume;
                gatlingSource.loop = false; // 加特林通常是短促的射击声，设为false
                gatlingSource.Play();   // 2. 重新播放
            }
        }
        else
        {
            // 普通音效逻辑 (使用 PlayOneShot 允许重叠，如手雷爆炸)
            if (data.clip != null)
            {
                sfxSource.PlayOneShot(data.clip, data.volume);
            }
        }
    }

    /// <summary>
    /// 停止指定音效
    /// </summary>
    public void StopSound(SoundType type)
    {
        if (type == SoundType.BGM)
        {
            bgmSource.Stop();
        }
        else if (type == SoundType.Gatling)
        {
            gatlingSource.Stop();
        }
        else
        {
            // 停止所有通过 sfxSource 播放的普通音效
            sfxSource.Stop();
        }
    }
}