// LeaderboardUI.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class LeaderboardUI : MonoBehaviour
{
    [Header("UI 引用")]
    public Transform entryContainer;     // 条目父物体
    public GameObject entryPrefab;       // 条目预制体

    private void OnEnable()
    {
        Refresh();
    }

    /// <summary>
    /// 刷新排行榜显示
    /// </summary>
    public void Refresh()
    {
        // 清除现有条目
        foreach (Transform child in entryContainer)
        {
            Destroy(child.gameObject);
        }

        List<ScoreEntry> entries = LeaderboardManager.Instance.GetLeaderboard();
        for (int i = 0; i < entries.Count; i++)
        {
            GameObject entry = Instantiate(entryPrefab, entryContainer);
            // 设置排名（从1开始）
            entry.transform.Find("RankText").GetComponent<Text>().text = (i + 1).ToString();
            // 设置昵称
            entry.transform.Find("NameText").GetComponent<Text>().text = entries[i].playerName;
            // 设置时间
            entry.transform.Find("TimeText").GetComponent<Text>().text = FormatTime(entries[i].survivalTime);
        }
    }

    /// <summary>
    /// 将秒数转换为 "XX分XX.X秒" 格式
    /// </summary>
    private string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60);
        float remainingSeconds = seconds % 60;
        return string.Format("{0}分{1:F1}秒", minutes, remainingSeconds);
    }
}