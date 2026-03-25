using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState
    {
        MainMenu,
        Playing,
        GameOver,
        Paused
    }

    public GameState currentState = GameState.MainMenu;
    public int currentScore = 0;

    // --- 观察者模式事件 ---
    public event Action<int> OnScoreChanged;
    public event Action OnGameStarted;
    public event Action OnGameOver;
    public event Action OnReturnToMainMenu;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        currentState = GameState.MainMenu;
        currentScore = 0;

        // --- 核心修改：订阅玩家死亡事件 ---
        // 当玩家死亡时，自动调用 HandlePlayerDeath 方法
        PlayerController.OnPlayerDied += HandlePlayerDeath;
        
        Debug.Log("[GameManager] 初始化完成，已监听玩家死亡事件。");
    }

    // --- 新增：处理死亡逻辑的核心方法 ---
    private void HandlePlayerDeath()
    {
        if (currentState == GameState.GameOver || currentState == GameState.Paused)
        {
            return; // 防止重复触发
        }

        Debug.Log("[GameManager] 接收到玩家死亡信号，正在处理游戏结束流程...");

        // 1. 暂停时间 (冻结所有物理和 Update 逻辑)
        Time.timeScale = 0f;

        // 2. 切换状态
        currentState = GameState.GameOver;

        // 3. 通知 UI 显示结算面板
        OnGameOver?.Invoke();
    }

    public void StartGame(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        if (currentState == GameState.Playing) return;

        ResetGameStats();
        currentState = GameState.Playing;
        
        // 确保时间流速正常（防止从上次死亡回来还是0）
        Time.timeScale = 1f;

        Debug.Log($"[GameManager] 开始游戏，加载场景: {sceneName}");
        SceneManager.LoadScene(sceneName);
        
        OnGameStarted?.Invoke();
    }

    // 注意：现在公有的 GameOver() 方法可能不再需要由外部调用了，
    // 因为死亡逻辑已经通过事件自动处理。
    // 但保留它作为备用（例如关卡时间到导致的游戏结束）。
    public void GameOver()
    {
        if (currentState == GameState.GameOver) return;
        
        if (Time.timeScale > 0f) Time.timeScale = 0f;
        currentState = GameState.GameOver;
        OnGameOver?.Invoke();
    }

    public void ReturnToMainMenu()
    {
        // 【关键】恢复时间流速，否则主菜单会静止
        Time.timeScale = 1f;
        
        currentState = GameState.MainMenu;
        ResetGameStats();
        
        string mainMenuScene = "MainMenu"; 
        if (SceneManager.GetActiveScene().name == mainMenuScene)
        {
            OnReturnToMainMenu?.Invoke();
            return;
        }

        SceneManager.LoadScene(mainMenuScene);
        OnReturnToMainMenu?.Invoke();
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f; // 恢复时间
        ResetGameStats();
        currentState = GameState.Playing;
        
        string currentSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentSceneName);
        OnGameStarted?.Invoke();
    }

    public void AddScore(int amount)
    {
        if (currentState != GameState.Playing) return;
        currentScore += amount;
        OnScoreChanged?.Invoke(currentScore);
    }

    void ResetGameStats()
    {
        currentScore = 0;
        OnScoreChanged?.Invoke(0);
    }

    public bool IsPlaying()
    {
        return currentState == GameState.Playing;
    }

    // 销毁时取消订阅，防止内存泄漏（虽然单例通常随应用关闭，但这是好习惯）
    void OnDestroy()
    {
        PlayerController.OnPlayerDied -= HandlePlayerDeath;
    }
}