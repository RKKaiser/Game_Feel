using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : MonoBehaviour
{
    // --- 单例模式 ---
    public static GameManager Instance { get; private set; }

    // --- 游戏状态枚举 ---
    public enum GameState
    {
        MainMenu,
        Playing,
        GameOver,
        Paused
    }

    public GameState currentState = GameState.MainMenu;

    // --- 计分系统 ---
    [Header("计分设置")]
    public int currentScore = 0;
    
    // 事件：当分数改变时触发（用于更新UI）
    public delegate void ScoreChangedHandler(int newScore);
    public event ScoreChangedHandler OnScoreChanged;

    // --- 初始化 (跨场景保留) ---
    void Awake()
    {
        // 1. 单例检查
        if (Instance != null && Instance != this)
        {
            // 如果场景中已经有一个 GameManager，销毁当前这个重复的
            Destroy(gameObject);
            return;
        }

        Instance = this;
        
        // 2. 关键：加载新场景时不销毁此物体
        DontDestroyOnLoad(gameObject);
        
        // 初始状态
        currentState = GameState.MainMenu;
        currentScore = 0;
    }

    // --- 游戏流程控制 ---

    /// <summary>
    /// 从主菜单开始游戏，加载指定场景
    /// </summary>
    public void StartGame(string sceneName)
    {
        if (currentState == GameState.Playing) return;

        ResetGameStats(); // 重置分数
        currentState = GameState.Playing;
        
        Debug.Log($"开始游戏，加载场景: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// 游戏结束逻辑
    /// </summary>
    public void GameOver()
    {
        if (currentState == GameState.GameOver) return;

        currentState = GameState.GameOver;
        Debug.Log($"游戏结束！最终得分: {currentScore}");
        
        // 这里可以触发游戏结束UI，或者延迟后返回主菜单
        // Invoke(nameof(ReturnToMainMenu), 3f); 
    }

    /// <summary>
    /// 返回主菜单
    /// </summary>
    public void ReturnToMainMenu()
    {
        currentState = GameState.MainMenu;
        ResetGameStats();
        Debug.Log("返回主菜单");
        SceneManager.LoadScene("MainMenu"); // 确保你的主场景名字叫 "MainMenu"
    }

    /// <summary>
    /// 重新开始当前关卡
    /// </summary>
    public void RestartLevel()
    {
        ResetGameStats();
        currentState = GameState.Playing;
        string currentSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentSceneName);
    }

    // --- 计分逻辑 ---

    /// <summary>
    /// 增加分数
    /// </summary>
    public void AddScore(int amount)
    {
        if (currentState != GameState.Playing) return;

        currentScore += amount;
        
        // 触发事件通知UI更新
        OnScoreChanged?.Invoke(currentScore);
        Debug.Log($"得分增加: +{amount}, 总分: {currentScore}");
    }

    /// <summary>
    /// 重置游戏数据（每次新游戏开始时调用）
    /// </summary>
    void ResetGameStats()
    {
        currentScore = 0;
        OnScoreChanged?.Invoke(0);
        // 注意：这里不调用 XPManager 的重置，因为那是独立的
    }
    
    // --- 辅助：获取当前状态 ---
    public bool IsPlaying()
    {
        return currentState == GameState.Playing;
    }
}