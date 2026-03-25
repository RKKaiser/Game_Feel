// ScoreEntry.cs
[System.Serializable]
public class ScoreEntry
{
    public string playerName;   // 玩家昵称
    public float survivalTime;  // 存活时长（秒）

    public ScoreEntry(string name, float time)
    {
        playerName = name;
        survivalTime = time;
    }
}