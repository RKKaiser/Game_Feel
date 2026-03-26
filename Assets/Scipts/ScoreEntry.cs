[System.Serializable]
public class ScoreEntry
{
    public string playerName;   // 玩家昵称
    public int killCount;       // 杀敌数（用于排名）

    public ScoreEntry(string name, int kills)
    {
        playerName = name;
        killCount = kills;
    }
}