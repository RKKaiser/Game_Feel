using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LeaderboardManager : MonoBehaviour
{
    [Header("设置")]
    public int maxEntries = 10;                 // 最多保存的记录数
    private const string SaveKey = "Leaderboard";

    private List<ScoreEntry> entries = new List<ScoreEntry>();

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
        SortAndTrim();
    }

    private void Save()
    {
        Wrapper wrapper = new Wrapper { entries = entries };
        string json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 添加新分数（按杀敌数排序）
    /// </summary>
    public void AddScore(string playerName, int killCount)
    {
        entries.Add(new ScoreEntry(playerName, killCount));
        SortAndTrim();
        Save();
    }

    private void SortAndTrim()
    {
        // 按杀敌数降序排序，保留前 maxEntries 条
        entries = entries.OrderByDescending(e => e.killCount).Take(maxEntries).ToList();
    }

    public List<ScoreEntry> GetLeaderboard()
    {
        return entries;
    }

    public void Clear()
    {
        entries.Clear();
        Save();
    }

    [System.Serializable]
    private class Wrapper
    {
        public List<ScoreEntry> entries;
    }
}