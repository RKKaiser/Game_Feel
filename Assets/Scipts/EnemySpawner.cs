using UnityEngine;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [Header("生成设置")]
    public Transform player;
    public float spawnInterval = 0.5f;
    public float minSpawnRange = 8f;
    public float maxSpawnRange = 15f;

    [Header("敌人预制体")]
    public GameObject hermitCrabPrefab;
    public GameObject seagullPrefab;
    public GameObject turtlePrefab;

    [Header("对象池设置")]
    public int initialPoolSize = 20; // 每种敌人预先创建的数量

    [Header("难度成长公式")]
    public float growthCoefficient = 0.05f;

    [Header("阶段控制 (秒)")]
    public float stage2StartTime = 60f;
    public float stage3StartTime = 180f;

    [Header("调试信息")]
    public float gameTime = 0f;
    public int currentStage = 1;

    // 对象池列表
    private List<GameObject> crabPool = new List<GameObject>();
    private List<GameObject> seagullPool = new List<GameObject>();
    private List<GameObject> turtlePool = new List<GameObject>();

    private Transform enemiesParent;
    private float nextSpawnTime = 0f;

    void Start()
    {
        // 1. 初始化玩家
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        // 2. 初始化父物体
        GameObject enemiesObj = GameObject.Find("Enemies");
        if (enemiesObj == null)
        {
            enemiesObj = new GameObject("Enemies");
        }
        enemiesParent = enemiesObj.transform;

        // 3. 【核心修改】初始化对象池
        InitializePool(hermitCrabPrefab, crabPool);
        InitializePool(seagullPrefab, seagullPool);
        InitializePool(turtlePrefab, turtlePool);
        
        // 给 Spawner 自己打个 Tag 方便 Enemy 查找
        if(gameObject.tag != "Spawner") 
            gameObject.tag = "Spawner"; // 确保有个Tag叫Spawner，或者在Inspector手动加
    }

    // 初始化单个类型的池子
    void InitializePool(GameObject prefab, List<GameObject> pool)
    {
        if (prefab == null) return;

        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject obj = Instantiate(prefab, enemiesParent);
            obj.SetActive(false); // 初始隐藏
            pool.Add(obj);
        }
        Debug.Log($"[ObjectPool] 初始化 {prefab.name} 池，数量: {initialPoolSize}");
    }

    void Update()
    {
        if (player == null) return;

        gameTime += Time.deltaTime;
        UpdateStage();

        if (Time.time >= nextSpawnTime)
        {
            SpawnEnemy();
            nextSpawnTime = Time.time + spawnInterval;
        }
    }

    void UpdateStage()
    {
        if (gameTime >= stage3StartTime) currentStage = 3;
        else if (gameTime >= stage2StartTime) currentStage = 2;
        else currentStage = 1;
    }

    void SpawnEnemy()
    {
        GameObject selectedPrefab = DetermineEnemyType();
        if (selectedPrefab == null) return;

        // 根据预制体类型选择对应的池子
        List<GameObject> targetPool = GetPoolForPrefab(selectedPrefab);
        if (targetPool == null) return;

        // 【核心修改】从池中获取对象
        GameObject enemyObj = GetFromPool(targetPool, selectedPrefab);
        
        // 设置位置和旋转
        Vector2 spawnPos = GetRandomSpawnPosition();
        enemyObj.transform.position = spawnPos;
        enemyObj.transform.rotation = Quaternion.identity;
        
        // 激活对象
        enemyObj.SetActive(true);

        // 应用难度成长
        ApplyDifficultyScaling(enemyObj);
    }

    // 辅助：根据预制体返回对应的池子列表
    List<GameObject> GetPoolForPrefab(GameObject prefab)
    {
        if (prefab == hermitCrabPrefab) return crabPool;
        if (prefab == seagullPrefab) return seagullPool;
        if (prefab == turtlePrefab) return turtlePool;
        return null;
    }

    // 【核心方法】从池中获取对象，如果池空了则扩容
    GameObject GetFromPool(List<GameObject> pool, GameObject prefab)
    {
        // 1. 寻找第一个非激活的对象
        foreach (GameObject obj in pool)
        {
            if (!obj.activeInHierarchy)
            {
                return obj;
            }
        }

        // 2. 如果没找到，说明池子满了，动态扩容 (Instantiate 一个新的加入池)
        Debug.Log($"[ObjectPool] 池子已满 ({prefab.name})，动态扩容 +1");
        GameObject newObj = Instantiate(prefab, enemiesParent);
        newObj.SetActive(false);
        pool.Add(newObj);
        return newObj;
    }

    // 【核心方法】供 Enemy 调用，回收对象
    public void ReturnEnemyToPool(Enemy enemy)
    {
        // 调用 Enemy 自身的重置逻辑
        enemy.ReturnToPool();
        // 此时物体已经 SetActive(false) 并重置了状态，只需留在列表中即可
        // 列表不需要移除操作，因为下次 GetFromPool 会遍历找到这个 inactive 的对象
    }

    GameObject DetermineEnemyType()
    {
        float rand = Random.value;
        if (currentStage == 1) return hermitCrabPrefab;
        else if (currentStage == 2)
        {
            float stage2Duration = stage3StartTime - stage2StartTime;
            float timeInStage2 = Mathf.Max(0, gameTime - stage2StartTime);
            float progress = Mathf.Clamp01(timeInStage2 / stage2Duration);
            float seagullChance = 0.5f * progress;
            return (rand < seagullChance) ? seagullPrefab : hermitCrabPrefab;
        }
        else if (currentStage == 3)
        {
            float stage3Duration = 120f;
            float timeInStage3 = Mathf.Max(0, gameTime - stage3StartTime);
            float progress = Mathf.Clamp01(timeInStage3 / stage3Duration);
            float turtleChance = 0.33f * progress;
            float remainingChance = 1f - turtleChance;
            float seagullChance = remainingChance * 0.5f;
            
            if (rand < turtleChance) return turtlePrefab;
            else if (rand < turtleChance + seagullChance) return seagullPrefab;
            else return hermitCrabPrefab;
        }
        return hermitCrabPrefab;
    }

    Vector2 GetRandomSpawnPosition()
    {
        float angle = Random.Range(0f, 2f * Mathf.PI);
        float distance = Random.Range(minSpawnRange, maxSpawnRange);
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        return (Vector2)player.position + offset;
    }

    void ApplyDifficultyScaling(GameObject enemyObj)
    {
        Enemy enemyScript = enemyObj.GetComponent<Enemy>();
        if (enemyScript == null) return;

        float multiplier = 1f + (growthCoefficient * gameTime);
        float speedMultiplier = 1f + (growthCoefficient * 0.2f * gameTime);

        float newHealth = enemyScript.baseMaxHealth * multiplier;
        float newSpeed = enemyScript.baseMoveSpeed * speedMultiplier;
        int newExp = Mathf.RoundToInt(enemyScript.baseExpValue * multiplier);

        enemyScript.SetStats(newHealth, newSpeed, newExp);
    }
}