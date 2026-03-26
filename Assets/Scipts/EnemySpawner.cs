using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 敌人生成器 & 对象池管理器
/// 负责管理三种敌人的对象池、动态难度调整、生成逻辑
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("对象池设置")]
    public int initialPoolSize = 15; // 初始池大小
    public int poolExpandStep = 5;   // 扩容步长
    public bool allowDynamicExpand = true; // 允许动态扩容

    [Header("敌人预制体 (必须赋值)")]
    public GameObject crabPrefab;   // 寄居蟹
    public GameObject gullPrefab;   // 海鸥
    public GameObject turtlePrefab; // 海龟

    [Header("生成区域控制")]
    public float spawnRadius = 18f;       // 最大生成半径
    public float minSpawnDistance = 10f;  // 最小生成距离 (防止贴脸)
    public float bufferZone = 2f;         // 屏幕外缓冲距离

    [Header("生成频率与难度")]
    public float baseSpawnInterval = 2.0f; // 基础生成间隔
    public float minSpawnInterval = 0.3f;  // 最小生成间隔 (防止卡顿)
    public float difficultyGrowthRate = 0.04f; // 难度增长系数

    // --- 内部数据结构 ---
    
    // 对象池字典: Key = Prefab, Value = Queue<PooledObject>
    private Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();
    
    // 实例映射字典: Key = Instance (GameObject), Value = Source Prefab (GameObject)
    // 用于在回收时快速找到对应的池子，避免字符串匹配
    private Dictionary<GameObject, GameObject> instanceToPrefabMap = new Dictionary<GameObject, GameObject>();

    private Transform playerTransform;
    private Camera mainCamera;
    private Vector2 lastPlayerPos;
    private float gameTime = 0f;
    private float currentSpawnInterval;
    private float nextSpawnTime = 0f;

    void Awake()
    {
        mainCamera = Camera.main;
        if (mainCamera == null) Debug.LogError("[Spawner] 未找到主相机！");

        // 初始化所有配置的预制体池
        InitializePool(crabPrefab);
        InitializePool(gullPrefab);
        InitializePool(turtlePrefab);

        currentSpawnInterval = baseSpawnInterval;
    }

    void Start()
    {
        FindPlayer();
        lastPlayerPos = playerTransform != null ? (Vector2)playerTransform.position : Vector2.zero;
    }

    void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTransform = playerObj.transform;
        else
            Debug.LogWarning("[Spawner] 未找到玩家 (Tag: Player)，生成逻辑暂停。");
    }

    void Update()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        gameTime += Time.deltaTime;

        // 1. 动态计算生成间隔
        // 公式: Interval = Max(MinInterval, BaseInterval / (1 + Rate * Time))
        float difficultyMultiplier = 1f + (difficultyGrowthRate * gameTime);
        currentSpawnInterval = Mathf.Max(minSpawnInterval, baseSpawnInterval / difficultyMultiplier);

        // 2. 触发生成
        if (Time.time >= nextSpawnTime)
        {
            SpawnRandomEnemy();
            nextSpawnTime = Time.time + currentSpawnInterval;
        }
    }

    /// <summary>
    /// 初始化特定预制体的对象池
    /// </summary>
    void InitializePool(GameObject prefab)
    {
        if (prefab == null) return;

        Queue<GameObject> pool = new Queue<GameObject>();
        
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreatePooledObject(prefab, pool);
        }
        
        pools[prefab] = pool;
        Debug.Log($"[Spawner] 初始化池: {prefab.name}, 容量: {initialPoolSize}");
    }

    /// <summary>
    /// 创建单个池对象并加入队列
    /// </summary>
    void CreatePooledObject(GameObject prefab, Queue<GameObject> pool)
    {
        GameObject obj = Instantiate(prefab, transform);
        obj.SetActive(false);
        obj.name = prefab.name + "_Pool"; // 统一命名方便调试
        
        pool.Enqueue(obj);
        // 初始放入池子时，暂时不加入 instanceToPrefabMap，因为还没被取出使用
        // 或者为了安全，也可以加入，但取出时会更新
    }

    /// <summary>
    /// 从池中获取敌人
    /// </summary>
    GameObject GetFromPool(GameObject prefab)
    {
        if (!pools.ContainsKey(prefab))
        {
            Debug.LogError($"[Spawner] 尝试从不存在的池子获取: {prefab.name}");
            return null;
        }

        Queue<GameObject> pool = pools[prefab];
        GameObject obj = null;

        if (pool.Count > 0)
        {
            obj = pool.Dequeue();
        }
        else
        {
            // 池空，尝试扩容
            if (allowDynamicExpand)
            {
                Debug.Log($"[Spawner] 池 {prefab.name} 耗尽，扩容 +{poolExpandStep}");
                for (int i = 0; i < poolExpandStep; i++)
                {
                    CreatePooledObject(prefab, pool);
                }
                obj = pool.Dequeue();
            }
            else
            {
                Debug.LogWarning($"[Spawner] 池 {prefab.name} 耗尽且禁止扩容，跳过本次生成。");
                return null;
            }
        }

        // 记录映射关系：这个实例是由哪个预制体生成的
        // 即使是从池里拿出来的旧对象，其映射关系也应该更新确认（虽然理论上不会变）
        if (!instanceToPrefabMap.ContainsKey(obj))
        {
            instanceToPrefabMap[obj] = prefab;
        }

        return obj;
    }

    /// <summary>
    /// 回收敌人到池中 (由 Enemy 脚本调用)
    /// </summary>
    public void ReturnEnemyToPool(Enemy enemy)
    {
        if (enemy == null || enemy.gameObject == null) return;

        GameObject enemyObj = enemy.gameObject;

        // 1. 执行敌人自身的清理逻辑 (停止协程、重置物理等)
        enemy.ReturnToPool();

        // 2. 查找对应的预制体
        GameObject sourcePrefab;
        if (!instanceToPrefabMap.TryGetValue(enemyObj, out sourcePrefab))
        {
            // 如果找不到映射（理论上不应该发生，除非手动Instantiate且未记录）
            Debug.LogWarning($"[Spawner] 无法识别敌人 {enemyObj.name} 的来源预制体，尝试通过名称匹配...");
            
            // 降级方案：通过名称前缀匹配
            sourcePrefab = FindPrefabByName(enemyObj.name);
            
            if (sourcePrefab == null)
            {
                Debug.LogError($"[Spawner] 彻底丢失 {enemyObj.name} 的归属，直接销毁以防内存泄漏。");
                Destroy(enemyObj);
                return;
            }
        }

        // 3. 放回对应的队列
        if (pools.ContainsKey(sourcePrefab))
        {
            pools[sourcePrefab].Enqueue(enemyObj);
            // 注意：这里不需要 Remove from instanceToPrefabMap，因为下次取出时会复用或覆盖
            // 保持映射可以加速下次回收
        }
        else
        {
            Debug.LogError($"[Spawner] 找到了预制体引用 {sourcePrefab.name}，但池子字典中不存在！");
            enemyObj.SetActive(false);
            enemyObj.transform.SetParent(transform);
        }
    }

    /// <summary>
    /// 降级方案：通过名称查找预制体
    /// </summary>
    GameObject FindPrefabByName(string instanceName)
    {
        string cleanName = instanceName.Replace("(Clone)", "").Trim();
        // 移除可能的 _Pool 后缀
        if (cleanName.EndsWith("_Pool")) cleanName = cleanName.Substring(0, cleanName.Length - 5);

        foreach (var prefab in pools.Keys)
        {
            if (prefab.name == cleanName) return prefab;
        }
        return null;
    }

    void SpawnRandomEnemy()
    {
        if (pools.Count == 0) return;

        // 1. 确定生成权重 (随时间变化)
        // 时间越久，海龟概率越高，寄居蟹概率越低
        float turtleChance = Mathf.Clamp01(0.1f + (gameTime * 0.005f)); // 初始10%，随时间增加
        float gullChance = 0.3f; // 固定30%
        float crabChance = 1f - turtleChance - gullChance;

        GameObject selectedPrefab = null;
        float roll = Random.value;

        if (roll < crabChance && crabPrefab != null)
        {
            selectedPrefab = crabPrefab;
        }
        else if (roll < crabChance + gullChance && gullPrefab != null)
        {
            selectedPrefab = gullPrefab;
        }
        else if (turtlePrefab != null)
        {
            selectedPrefab = turtlePrefab;
        }
        else
        {
            // 如果选中的预制体没赋值，回退到任意可用的
            if (crabPrefab != null) selectedPrefab = crabPrefab;
            else if (gullPrefab != null) selectedPrefab = gullPrefab;
            else if (turtlePrefab != null) selectedPrefab = turtlePrefab;
        }

        if (selectedPrefab == null) return;

        // 2. 获取对象
        GameObject enemyObj = GetFromPool(selectedPrefab);
        if (enemyObj == null) return;

        // 3. 计算生成位置
        Vector2 spawnPos = GetRandomSpawnPosition();

        // 4. 激活并设置
        enemyObj.transform.position = spawnPos;
        enemyObj.transform.rotation = Quaternion.identity;
        enemyObj.SetActive(true);
        enemyObj.transform.SetParent(null); // 独立于 Spawner，方便管理

        // 5. 初始化数据 (应用难度成长)
        Enemy enemyScript = enemyObj.GetComponent<Enemy>();
        if (enemyScript != null)
        {
            // 关键：传入当前游戏时间，让敌人自己计算属性
            enemyScript.ApplyScaling(gameTime);
        }
        else
        {
            Debug.LogError($"[Spawner] 生成的对象 {enemyObj.name} 缺少 Enemy 组件！");
        }
    }

    /// <summary>
    /// 计算随机生成位置
    /// 逻辑：在以玩家为中心的环形区域内，且必须在摄像机视野外
    /// </summary>
    Vector2 GetRandomSpawnPosition()
    {
        if (playerTransform == null || mainCamera == null) return Vector2.zero;

        Vector2 playerPos = (Vector2)playerTransform.position;
        
        // 计算屏幕边界在世界坐标的位置，加上缓冲区
        Vector2 screenCenter = mainCamera.WorldToViewportPoint(playerPos);
        // 简单的近似：直接用半径判断，更精确的做法是计算视锥体边缘
        // 这里采用：生成点距离玩家的距离 > max(屏幕对角线一半 + buffer, minSpawnDistance)
        
        float cameraWidth = mainCamera.orthographicSize * mainCamera.aspect;
        float cameraHeight = mainCamera.orthographicSize;
        float maxViewDist = Mathf.Sqrt(cameraWidth * cameraWidth + cameraHeight * cameraHeight) * 0.6f; // 视野内最大半径估算
        
        float effectiveMinDist = Mathf.Max(minSpawnDistance, maxViewDist + bufferZone);
        float effectiveMaxDist = Mathf.Max(effectiveMinDist + 1f, spawnRadius);

        Vector2 result = playerPos;
        int attempts = 0;
        int maxAttempts = 20;

        while (attempts < maxAttempts)
        {
            float angle = Random.Range(0f, 2f * Mathf.PI);
            float dist = Random.Range(effectiveMinDist, effectiveMaxDist);
            
            Vector2 candidate = playerPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

            // 检查是否被障碍物阻挡
            if (!IsPositionBlocked(candidate))
            {
                result = candidate;
                break;
            }
            attempts++;
        }

        return result;
    }

    bool IsPositionBlocked(Vector2 pos)
    {
        // 检测障碍物层 (需要用户在 Project Settings 中设置 "Obstacle" Layer)
        // 如果没设置，LayerMask.GetMask 返回 0，检测将失效（视为不阻挡）
        int obstacleLayer = LayerMask.GetMask("Obstacle", "Wall", "Ground");
        if (obstacleLayer == 0) return false; 

        Collider2D hit = Physics2D.OverlapCircle(pos, 0.5f, obstacleLayer);
        return hit != null;
    }

    // --- 调试 Gizmos ---
    void OnDrawGizmosSelected()
    {
        if (playerTransform == null)
        {
            // 编辑器模式下如果没有玩家，用 Spawner 自身位置代替
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
            Gizmos.DrawWireSphere(transform.position, minSpawnDistance);
            return;
        }

        Vector2 pos = (Vector2)playerTransform.position;
        
        // 绘制有效生成环
        Gizmos.color = new Color(1, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(pos, spawnRadius);
        
        // 绘制禁入区 (贴脸保护 + 屏幕缓冲)
        float cameraWidth = mainCamera ? mainCamera.orthographicSize * mainCamera.aspect : 5f;
        float cameraHeight = mainCamera ? mainCamera.orthographicSize : 5f;
        float viewDist = Mathf.Sqrt(cameraWidth * cameraWidth + cameraHeight * cameraHeight) * 0.6f;
        float debugMin = Mathf.Max(minSpawnDistance, viewDist + bufferZone);

        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawWireSphere(pos, debugMin);
        
        Gizmos.DrawLine(pos, pos + Vector2.up * debugMin);
    }
}