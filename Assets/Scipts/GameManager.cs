using UnityEngine; 
using UnityEngine.SceneManagement; 
using System; 

public class GameManager : MonoBehaviour 
{
    public static GameManager Instance { get; private set; } 
    
    // 游戏状态枚举
    public enum GameState { MainMenu, Playing, GameOver, Paused } 
    public GameState currentState = GameState.MainMenu; 
    
    // 分数与杀敌数
    public int currentScore = 0; 
    public int killCount = 0; // 新增：杀敌计数器

    // --- 观察者模式事件 ---
    public event Action<int> OnScoreChanged; 
    public event Action<int> OnKillCountChanged; // 新增：杀敌数变化事件
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
        
        // --- 核心修改：订阅玩家死亡事件 ---
        // 当玩家死亡时，自动调用 HandlePlayerDeath 方法
        PlayerController.OnPlayerDied += HandlePlayerDeath; 
        Debug.Log("[GameManager] 初始化完成，已监听玩家死亡事件。"); 
    } 

    // --- 新增：处理死亡逻辑的核心方法 ---
    private void HandlePlayerDeath()
{
    if (currentState == GameState.GameOver || currentState == GameState.Paused)
        return;
    Debug.Log("[GameManager] 接收到玩家死亡信号，正在处理游戏结束流程...");

    // 1. 暂停时间
    Time.timeScale = 0f;

    // 2. 切换状态
    currentState = GameState.GameOver;

    // 3. 获取当前杀敌数并显示结算面板
    int finalKills = killCount;
    // 尝试查找场景中的 GameOverUI 组件
    if (gameOverUI == null)
        gameOverUI = FindObjectOfType<GameOverUI>();
    if (gameOverUI != null)
        gameOverUI.Show(finalKills);
    else
        Debug.LogError("[GameManager] 场景中找不到 GameOverUI 组件！");

    // 4. 通知其他 UI 组件（可选）
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

    // --- 修改：增加分数逻辑 ---
    public void AddScore(int amount) 
    {
        if (currentState != GameState.Playing) return; 
        currentScore += amount; 
        OnScoreChanged?.Invoke(currentScore); 
    } 

    // --- 新增：增加杀敌数逻辑 ---
    // 这是你的接口，敌人死亡时调用这个即可
    public void AddKillCount(int amount = 1) 
    {
        if (currentState != GameState.Playing) return; 
        
        killCount += amount;
        
        // 文档要求：击杀敌人获得分数
        // 这里假设每杀一个怪给 10 分，你可以根据需要调整数值
        AddScore(amount * 10); 
        
        // 触发事件（虽然你现在不显示，但留着以后扩展好用）
        OnKillCountChanged?.Invoke(killCount); 
    }

    void ResetGameStats() 
    { 
        currentScore = 0; 
        killCount = 0; // 重置时也要重置杀敌数
        OnScoreChanged?.Invoke(0); 
        OnKillCountChanged?.Invoke(0);
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