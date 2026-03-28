using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 敌人生成器 & 对象池管理器
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("对象池设置")]
    public int initialPoolSize = 15; 
    public int poolExpandStep = 5;   
    public bool allowDynamicExpand = true;

    [Header("生成层级管理")]
    public Transform enemiesParent;

    [Header("敌人预制体 (必须赋值)")]
    public GameObject crabPrefab;   // 寄居蟹
    public GameObject gullPrefab;   // 海鸥
    public GameObject turtlePrefab; // 海龟

    [Header("阶段控制 (时间秒数)")]
    [Tooltip("第一阶段持续时间：只生成寄居蟹")]
    public float stage1Duration = 60f; 
    
    [Tooltip("第二阶段持续时间：海鸥逐渐增多至与寄居蟹持平")]
    public float stage2Duration = 120f; 
    
    [Tooltip("第三阶段持续时间：海龟逐渐增多至三者持平 (之后保持平衡)")]
    public float stage3Duration = 180f;

    [Header("生成区域控制")]
    public float spawnRadius = 18f;       
    public float minSpawnDistance = 10f;  
    public float bufferZone = 2f;         

    [Header("生成频率")]
    public float baseSpawnInterval = 2.0f; 
    public float minSpawnInterval = 0.4f;  
    public float difficultyGrowthRate = 0.03f; 

    // --- 内部数据结构 ---
    private Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();
    private Dictionary<GameObject, GameObject> instanceToPrefabMap = new Dictionary<GameObject, GameObject>();

    private Transform playerTransform;
    private Camera mainCamera;
    private float gameTime = 0f;
    private float currentSpawnInterval;
    private float nextSpawnTime = 0f;

    // 阶段状态缓存
    private int currentStage = 1;
    private float crabWeight = 1f;
    private float gullWeight = 0f;
    private float turtleWeight = 0f;

    void Awake()
    {
        mainCamera = Camera.main;
        
        // 初始化池子
        InitializePool(crabPrefab);
        InitializePool(gullPrefab);
        InitializePool(turtlePrefab);

        currentSpawnInterval = baseSpawnInterval;
    }

    void Start()
    {
        FindPlayer();
    }

    void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTransform = playerObj.transform;
        else
            Debug.LogWarning("[Spawner] 未找到玩家，生成暂停。");
    }

    void Update()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        gameTime += Time.deltaTime;

        // 1. 更新阶段权重
        UpdateSpawnWeights();

        // 2. 动态计算生成间隔 (随时间加快)
        float difficultyMultiplier = 1f + (difficultyGrowthRate * gameTime);
        currentSpawnInterval = Mathf.Max(minSpawnInterval, baseSpawnInterval / difficultyMultiplier);

        // 3. 触发生成
        if (Time.time >= nextSpawnTime)
        {
            SpawnEnemyByWeights();
            nextSpawnTime = Time.time + currentSpawnInterval;
        }
    }

    /// <summary>
    /// 核心逻辑：根据当前时间计算三种敌人的生成权重
    /// </summary>
    void UpdateSpawnWeights()
    {
        // 确定当前处于哪个时间段
        float t1End = stage1Duration;
        float t2End = stage1Duration + stage2Duration;
        float t3End = stage1Duration + stage2Duration + stage3Duration;

        if (gameTime < t1End)
        {
            // === 阶段 1: 只有寄居蟹 ===
            currentStage = 1;
            crabWeight = 1f;
            gullWeight = 0f;
            turtleWeight = 0f;
        }
        else if (gameTime < t2End)
        {
            // === 阶段 2: 寄居蟹 -> 海鸥 (线性过渡) ===
            currentStage = 2;
            float progress = (gameTime - t1End) / stage2Duration; // 0.0 ~ 1.0
            
            // 寄居蟹从 1.0 降到 0.5
            crabWeight = Mathf.Lerp(1f, 0.5f, progress);
            // 海鸥从 0.0 升到 0.5
            gullWeight = Mathf.Lerp(0f, 0.5f, progress);
            turtleWeight = 0f;
        }
        else if (gameTime < t3End)
        {
            // === 阶段 3: 前两者 -> 海龟 (线性过渡到三者持平) ===
            currentStage = 3;
            float progress = (gameTime - t2End) / stage3Duration; // 0.0 ~ 1.0
            
            // 目标都是 1/3 (约 0.333)
            float targetWeight = 1f / 3f;

            // 寄居蟹从 0.5 降到 0.333
            crabWeight = Mathf.Lerp(0.5f, targetWeight, progress);
            // 海鸥从 0.5 降到 0.333
            gullWeight = Mathf.Lerp(0.5f, targetWeight, progress);
            // 海龟从 0.0 升到 0.333
            turtleWeight = Mathf.Lerp(0f, targetWeight, progress);
        }
        else
        {
            // === 阶段 3 之后: 保持三者持平 ===
            currentStage = 3;
            float targetWeight = 1f / 3f;
            crabWeight = targetWeight;
            gullWeight = targetWeight;
            turtleWeight = targetWeight;
        }
    }

    void InitializePool(GameObject prefab)
    {
        if (prefab == null) return;
        Queue<GameObject> pool = new Queue<GameObject>();
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreatePooledObject(prefab, pool);
        }
        pools[prefab] = pool;
    }

    void CreatePooledObject(GameObject prefab, Queue<GameObject> pool)
    {
        GameObject obj = Instantiate(prefab, transform);
        obj.SetActive(false);
        obj.name = prefab.name + "_Pool";
        pool.Enqueue(obj);
    }

    GameObject GetFromPool(GameObject prefab)
    {
        if (!pools.ContainsKey(prefab)) return null;

        Queue<GameObject> pool = pools[prefab];
        GameObject obj = null;

        if (pool.Count > 0)
        {
            obj = pool.Dequeue();
        }
        else if (allowDynamicExpand)
        {
            Debug.Log($"[Spawner] 池 {prefab.name} 扩容 +{poolExpandStep}");
            for (int i = 0; i < poolExpandStep; i++) CreatePooledObject(prefab, pool);
            obj = pool.Dequeue();
        }
        else
        {
            return null;
        }

        if (!instanceToPrefabMap.ContainsKey(obj))
            instanceToPrefabMap[obj] = prefab;

        return obj;
    }

    public void ReturnEnemyToPool(Enemy enemy)
    {
        if (enemy == null || enemy.gameObject == null) return;
        GameObject enemyObj = enemy.gameObject;

        enemy.ReturnToPool(); // 清理自身状态

        GameObject sourcePrefab;
        if (!instanceToPrefabMap.TryGetValue(enemyObj, out sourcePrefab))
        {
            sourcePrefab = FindPrefabByName(enemyObj.name);
            if (sourcePrefab == null)
            {
                Destroy(enemyObj);
                return;
            }
        }

        if (pools.ContainsKey(sourcePrefab))
        {
            pools[sourcePrefab].Enqueue(enemyObj);
        }
        else
        {
            enemyObj.SetActive(false);
            enemyObj.transform.SetParent(transform);
        }
    }

    GameObject FindPrefabByName(string instanceName)
    {
        string cleanName = instanceName.Replace("(Clone)", "").Trim();
        if (cleanName.EndsWith("_Pool")) cleanName = cleanName.Substring(0, cleanName.Length - 5);
        
        foreach (var prefab in pools.Keys)
        {
            if (prefab.name == cleanName) return prefab;
        }
        return null;
    }

    /// <summary>
    /// 根据计算出的权重随机生成敌人
    /// </summary>
    void SpawnEnemyByWeights()
    {
        // 防止所有权重为0 (例如配置错误)
        if (crabWeight <= 0 && gullWeight <= 0 && turtleWeight <= 0) return;

        float totalWeight = crabWeight + gullWeight + turtleWeight;
        float randomValue = Random.Range(0f, totalWeight);

        GameObject selectedPrefab = null;

        // 轮盘赌选择
        if (randomValue < crabWeight)
        {
            selectedPrefab = crabPrefab;
        }
        else if (randomValue < crabWeight + gullWeight)
        {
            selectedPrefab = gullPrefab;
        }
        else
        {
            selectedPrefab = turtlePrefab;
        }

        // 防御性检查：如果选中的预制体没赋值 (例如阶段2选了海鸥但没拖入海鸥预制体)
        if (selectedPrefab == null)
        {
            // 尝试回退到有权重且不为空的预制体
            if (crabWeight > 0 && crabPrefab != null) selectedPrefab = crabPrefab;
            else if (gullWeight > 0 && gullPrefab != null) selectedPrefab = gullPrefab;
            else if (turtleWeight > 0 && turtlePrefab != null) selectedPrefab = turtlePrefab;
            
            if (selectedPrefab == null) return; // 实在没法生成就跳过
        }

        GameObject enemyObj = GetFromPool(selectedPrefab);
        if (enemyObj == null) return;

        Vector2 spawnPos = GetRandomSpawnPosition();
        enemyObj.transform.position = spawnPos;
        enemyObj.transform.rotation = Quaternion.identity;

        // 设置父物体逻辑
        if (enemiesParent != null)
        {
            enemyObj.transform.SetParent(enemiesParent); // 设置为 Enemies 的子物体
        }
        else
        {
            enemyObj.transform.SetParent(null); // 如果没赋值，保持在根层级（防止报错）
        }

        enemyObj.SetActive(true);

        Enemy enemyScript = enemyObj.GetComponent<Enemy>();
        if (enemyScript != null)
        {
            enemyScript.ApplyScaling(gameTime);
        }
    }

    Vector2 GetRandomSpawnPosition()
    {
        if (playerTransform == null || mainCamera == null) return Vector2.zero;

        Vector2 playerPos = (Vector2)playerTransform.position;
        
        float cameraWidth = mainCamera.orthographicSize * mainCamera.aspect;
        float cameraHeight = mainCamera.orthographicSize;
        float maxViewDist = Mathf.Sqrt(cameraWidth * cameraWidth + cameraHeight * cameraHeight) * 0.6f;
        
        float effectiveMinDist = Mathf.Max(minSpawnDistance, maxViewDist + bufferZone);
        float effectiveMaxDist = Mathf.Max(effectiveMinDist + 1f, spawnRadius);

        Vector2 result = playerPos;
        int attempts = 0;
        
        while (attempts < 20)
        {
            float angle = Random.Range(0f, 2f * Mathf.PI);
            float dist = Random.Range(effectiveMinDist, effectiveMaxDist);
            Vector2 candidate = playerPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

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
        int obstacleLayer = LayerMask.GetMask("Obstacle", "Wall", "Ground");
        if (obstacleLayer == 0) return false;
        return Physics2D.OverlapCircle(pos, 0.5f, obstacleLayer) != null;
    }

    void OnDrawGizmosSelected()
    {
        Vector2 center = playerTransform ? (Vector2)playerTransform.position : transform.position;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, spawnRadius);
        
        Gizmos.color = Color.red;
        float debugMin = minSpawnDistance; 
        if(mainCamera != null && playerTransform != null)
        {
             float camW = mainCamera.orthographicSize * mainCamera.aspect;
             float camH = mainCamera.orthographicSize;
             debugMin = Mathf.Max(minSpawnDistance, Mathf.Sqrt(camW*camW + camH*camH)*0.6f + bufferZone);
        }
        Gizmos.DrawWireSphere(center, debugMin);

        // 绘制当前阶段信息
        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.normal.textColor = Color.white;
        UnityEditor.Handles.Label((Vector3)center + Vector3.up * (spawnRadius + 2), $"Stage: {currentStage} | C:{crabWeight:F2} G:{gullWeight:F2} T:{turtleWeight:F2}", style);
    }
}