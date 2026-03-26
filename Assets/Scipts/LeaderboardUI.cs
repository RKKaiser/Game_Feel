using UnityEngine;
using TMPro;
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
            // 设置排名
            entry.transform.Find("RankText").GetComponent<TextMeshProUGUI>().text = (i + 1).ToString();
            // 设置昵称
            entry.transform.Find("NameText").GetComponent<TextMeshProUGUI>().text = entries[i].playerName;
            // 设置杀敌数（显示为 “XX 杀”）
            entry.transform.Find("KillText").GetComponent<TextMeshProUGUI>().text = $"{entries[i].killCount} 杀";
        }
    }
}