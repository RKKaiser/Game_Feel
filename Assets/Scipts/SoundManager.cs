using System.Collections.Generic;
using UnityEngine;

// 1. 定义音效类型的枚举 (Enum)
// 这里包含了文档中提到的所有需要音效的场景
public enum SoundType
{
    None, // 空选项
    
    // 玩家相关
    PlayerShoot, // 玩家射击
    PlayerDeath, // 玩家死亡
    
    // 敌人相关
    EnemyDeath,  // 敌人死亡 (海鸥/寄居蟹/海龟)
    EnemySpawn,  // 敌人生成
    TurtleCharge, // 海龟冲撞 (特殊效果)
    
    // UI 相关
    ButtonHover, // 按钮高亮
    ButtonClick, // 按钮点击
    UpgradeSelect, // 升级界面选择
    
    // 背景音乐
    BackgroundMusic // 背景音乐
}

// 2. 定义音效数据类 (Data Class)
// 使用 [System.Serializable] 可以让 Unity 在 Inspector 中显示并编辑这个类
[System.Serializable]
public class SoundData
{
    public SoundType soundType; // 音效类型 (用于索引)
    public AudioClip clip;      // 音频剪辑 (拖入你的 wav 或 mp3 文件)
    
    [Range(0f, 1f)]
    public float volume = 1.0f; // 音量大小
    
    public bool loop = false;   // 是否循环播放 (主要用于背景音乐)
}

// 3. 核心管理类 (Manager Class)
public class SoundManager : MonoBehaviour
{
    // 单例 (Singleton)
    public static SoundManager Instance;

    // Inspector 配置
    [Header("音效配置")]
    public List<SoundData> soundList; // 音效列表，将在 Inspector 中填充

    // 内部组件
    private Dictionary<SoundType, SoundData> soundDict = new Dictionary<SoundType, SoundData>();
    private AudioSource musicSource; // 背景音乐源
    private AudioSource sfxSource;   // 音效源

    void Awake()
    {
        // 单例初始化逻辑
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 文档提到从 MainMenu 创建，需要跨场景
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 初始化组件
        Initialize();
    }

    void Initialize()
    {
        // 1. 创建 AudioSource 组件
        // 背景音乐
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true; // 默认开启循环，配合背景音乐使用
        }

        // 音效 (SFX)
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false; // 音效通常不循环
        }

        // 2. 构建字典 (Dictionary)
        // 将 List 转化为 Dictionary，实现 O(1) 时间复杂度的查找
        soundDict.Clear();
        foreach (var sound in soundList)
        {
            if (sound.clip != null && !soundDict.ContainsKey(sound.soundType))
            {
                soundDict[sound.soundType] = sound;
            }
        }
    }

    /// <summary>
    /// 播放音效的公共接口 (Public API)
    /// </summary>
    /// <param name="type">要播放的音效类型</param>
    public void PlaySound(SoundType type)
    {
        // 防御性检查
        if (type == SoundType.None || !soundDict.ContainsKey(type)) return;

        SoundData data = soundDict[type];

        // 区分背景音乐和普通音效
        if (data.loop)
        {
            // 如果是循环音乐且当前没有播放该曲目，则切换并播放
            if (musicSource.clip != data.clip)
            {
                musicSource.clip = data.clip;
                musicSource.volume = data.volume;
                musicSource.Play();
            }
        }
        else
        {
            // 普通音效使用 PlayOneShot，不会打断当前正在播放的音效
            sfxSource.PlayOneShot(data.clip, data.volume);
        }
    }

    /// <summary>
    /// 停止背景音乐
    /// </summary>
    public void StopMusic()
    {
        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.Stop();
        }
    }
}