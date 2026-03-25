// LeaderboardManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LeaderboardManager : MonoBehaviour
{
    [Header("设置")]
    public int maxEntries = 10;                 // 最多保存的记录数
    private const string SaveKey = "Leaderboard";

    private List<ScoreEntry> entries = new List<ScoreEntry>();

    // 单例
    public static LeaderboardManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    /// <summary>
    /// 加载本地数据
    /// </summary>
    private void Load()
    {
        if (PlayerPrefs.HasKey(SaveKey))
        {
            string json = PlayerPrefs.GetString(SaveKey);
            Wrapper wrapper = JsonUtility.FromJson<Wrapper>(json);
            if (wrapper != null && wrapper.entries != null)
                entries = wrapper.entries;
            else
                entries = new List<ScoreEntry>();
        }
        else
        {
            entries = new List<ScoreEntry>();
        }
        SortAndTrim();  // 保证数据有序且不超限
    }

    /// <summary>
    /// 保存数据到本地
    /// </summary>
    private void Save()
    {
        Wrapper wrapper = new Wrapper { entries = entries };
        string json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 添加新分数
    /// </summary>
    public void AddScore(string playerName, float survivalTime)
    {
        entries.Add(new ScoreEntry(playerName, survivalTime));
        SortAndTrim();
        Save();
    }

    /// <summary>
    /// 按存活时长降序排序，并保留前 maxEntries 条
    /// </summary>
    private void SortAndTrim()
    {
        entries = entries.OrderByDescending(e => e.survivalTime).Take(maxEntries).ToList();
    }

    /// <summary>
    /// 获取排行榜列表（只读）
    /// </summary>
    public List<ScoreEntry> GetLeaderboard()
    {
        return entries;
    }

    /// <summary>
    /// 清除所有数据（调试用）
    /// </summary>
    public void Clear()
    {
        entries.Clear();
        Save();
    }

    // 辅助包装类，用于序列化 List
    [System.Serializable]
    private class Wrapper
    {
        public List<ScoreEntry> entries;
    }
}