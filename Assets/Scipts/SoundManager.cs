using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 1. 重新定义枚举：完全对应你图片中的音频文件名
public enum SoundType
{
    None,
    Gatling,            // 加特林音效
    Fail,               // 失败音效
    GrenadeThrow,       // 手雷投掷
    GrenadeExplosion,   // 手雷爆炸
    EnemyHit,           // 敌人受击
    Shotgun,            // 散弹枪
    BGM,                // 游戏bgm
    Click,              // 点击音效
    CrabUpgrade         // 螃蟹升级
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
    // 单例模式
    public static SoundManager Instance;

    [Header("音效配置")]
    public List<SoundData> soundDatas; // 在Inspector中拖入你的9个音频文件

    private Dictionary<SoundType, SoundData> soundDict;

    [Header("音频源")]
    public AudioSource bgmSource; // 专门播放 BGM
    public AudioSource sfxSource; // 专门播放其他音效

    private void Awake()
    {
        // 单例初始化
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

        // 将列表转换为字典，提高查找效率
        soundDict = new Dictionary<SoundType, SoundData>();
        foreach (var data in soundDatas)
        {
            if (!soundDict.ContainsKey(data.soundType))
            {
                soundDict.Add(data.soundType, data);
            }
        }
    }

    // 播放音效的公共方法
    public void PlaySound(SoundType type)
    {
        if (soundDict.TryGetValue(type, out SoundData data))
        {
            // 如果是背景音乐
            if (type == SoundType.BGM)
            {
                if (bgmSource != null && data.clip != null)
                {
                    bgmSource.clip = data.clip;
                    bgmSource.volume = data.volume;
                    bgmSource.loop = data.loop;
                    bgmSource.Play();
                }
            }
            // 如果是普通音效
            else
            {
                if (sfxSource != null && data.clip != null)
                {
                    sfxSource.PlayOneShot(data.clip, data.volume);
                }
            }
        }
        else
        {
            Debug.LogWarning($"SoundManager: 未找到音效类型 {type}");
        }
    }

    // 专门用于停止背景音乐的方法
    public void StopBGM()
    {
        if (bgmSource != null)
        {
            bgmSource.Stop();
        }
    }
}