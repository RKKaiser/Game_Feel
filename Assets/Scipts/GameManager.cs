using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class GameManager : MonoBehaviour 
{
    public static GameManager Instance { get; private set; }

    // --- 1. 游戏状态枚举 ---
    public enum GameState 
    { 
        MainMenu, 
        Playing, 
        GameOver, 
        Paused 
    }
    public GameState currentState = GameState.MainMenu;

    // --- 2. 游戏数据 ---
    public int currentScore = 0;
    public int killCount = 0;

    // --- 3. 观察者模式事件 (用于UI更新) ---
    public event Action<int> OnScoreChanged;
    public event Action<int> OnKillCountChanged;
    public event Action OnGameStarted;
    public event Action OnGameOver;
    public event Action OnReturnToMainMenu;

    private GameOverUI gameOverUI;

    void Awake() 
    {
        // 单例模式：防止重复创建
        if (Instance != null && Instance != this) 
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        currentState = GameState.MainMenu;
        ResetGameStats();

        // --- 核心逻辑：订阅玩家死亡事件 ---
        PlayerController.OnPlayerDied += HandlePlayerDeath;
        Debug.Log("[GameManager] 初始化完成，已监听玩家死亡事件。");
    }

    // --- 4. 处理玩家死亡的核心方法 ---
    private void HandlePlayerDeath() 
    {
        if (currentState == GameState.GameOver || currentState == GameState.Paused) return;

        Debug.Log("[GameManager] 接收到玩家死亡信号，正在处理游戏结束流程...");
        
        // 1. 暂停时间
        Time.timeScale = 0f;
        
        // 2. 切换状态
        currentState = GameState.GameOver;
        
        // 3. 显示结算面板
        int finalKills = killCount;
        GameOverUI[] gameOverUIs = FindObjectsOfType<GameOverUI>(true);
        
        if (gameOverUIs.Length > 0) 
        {
            GameOverUI gameOverUI = gameOverUIs[0];
            gameOverUI.Show(finalKills);
        } 
        else 
        {
            Debug.LogError("[GameManager] 场景中找不到任何 GameOverUI 组件！");
        }
        
        // 4. 通知其他组件
        OnGameOver?.Invoke();

        // **************************************************
        // 【BGM逻辑】游戏结束，停止背景音乐
        // **************************************************
        SoundManager.Instance.StopSound(SoundType.BGM);

        // 播放失败音效
        SoundManager.Instance.PlaySound(SoundType.Fail);
    }

    // --- 5. 游戏流程控制 ---
    public void StartGame(string sceneName) 
    {
        if (string.IsNullOrEmpty(sceneName) || currentState == GameState.Playing) return;
        
        ResetGameStats();
        currentState = GameState.Playing;
        Time.timeScale = 1f;
        Debug.Log($"[GameManager] 开始游戏，加载场景: {sceneName}");
        
        // **************************************************
        // 【BGM逻辑】游戏开始，播放背景音乐
        // **************************************************
        SoundManager.Instance.PlaySound(SoundType.BGM);

        // 播放点击音效
        SoundManager.Instance.PlaySound(SoundType.Click);

        SceneManager.LoadScene(sceneName);
        OnGameStarted?.Invoke();
    }

    // 备用的游戏结束方法
    public void GameOver() 
    {
        if (currentState == GameState.GameOver) return;
        
        if (Time.timeScale > 0f) Time.timeScale = 0f;
        currentState = GameState.GameOver;
        OnGameOver?.Invoke();
        
        // 播放失败音效
        SoundManager.Instance.PlaySound(SoundType.Fail);
    }

    public void ReturnToMainMenu() 
    {
        Time.timeScale = 1f;
        currentState = GameState.MainMenu;
        ResetGameStats();
        
        string mainMenuScene = "MainMenu";
        
        // **************************************************
        // 【BGM逻辑】返回菜单，停止背景音乐
        // (注意：如果主菜单有独立的BGM，这里也可以改为播放主菜单BGM)
        // **************************************************
        SoundManager.Instance.StopSound(SoundType.BGM);

        // 播放点击音效
        SoundManager.Instance.PlaySound(SoundType.Click);

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
        Time.timeScale = 1f;
        ResetGameStats();
        currentState = GameState.Playing;
        
        // 播放重新开始音效
        SoundManager.Instance.PlaySound(SoundType.Click);

        string currentSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentSceneName);
        OnGameStarted?.Invoke();
    }

    // --- 6. 分数与杀敌逻辑 ---
    public void AddScore(int amount) 
    {
        if (currentState != GameState.Playing) return;
        currentScore += amount;
        
        // 播放加分音效 (可选)
        // SoundManager.Instance.PlaySound(SoundType.ScoreUp); 

        OnScoreChanged?.Invoke(currentScore);
    }

    // 增加杀敌数逻辑
    public void AddKillCount(int amount = 1) 
    {
        if (currentState != GameState.Playing) return;
        
        killCount += amount;
        AddScore(amount * 10); 
        
        // 播放击杀音效 (可选)
        // SoundManager.Instance.PlaySound(SoundType.EnemyHit); 

        OnKillCountChanged?.Invoke(killCount);
    }

    void ResetGameStats() 
    {
        currentScore = 0;
        killCount = 0;
        OnScoreChanged?.Invoke(0);
        OnKillCountChanged?.Invoke(0);
    }

    public bool IsPlaying() 
    {
        return currentState == GameState.Playing;
    }

    // 销毁时取消订阅
    void OnDestroy() 
    {
        PlayerController.OnPlayerDied -= HandlePlayerDeath;
    }
}