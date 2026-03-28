using UnityEngine;
using System.Collections;

/// <summary>
/// 敌人基类
/// 支持三种类型：寄居蟹、海鸥、海龟
/// 适配对象池、属性随时间增长、GameFeel挤压动画
/// </summary>
public class Enemy : MonoBehaviour
{
    public enum EnemyType { SoldierCrab, SeaGull, SeaTurtle }
    
    [Header("敌人类型")]

    public EnemyType type = EnemyType.SoldierCrab;

    [Header("基础属性 (Inspector可配)")]
    public float baseMaxHealth = 10f;
    public float baseMoveSpeed = 2f;
    public int baseDamage = 1; // 对玩家的伤害
    public int baseExpValue = 10;
    
    [Header("成长系数 (对应文档公式)")]
    public float statGrowthRate = 0.05f; // 增长系数

    [Header("海龟特殊属性 (仅当类型为海龟时生效)")]
    public float chargeInterval = 3f;
    public float chargeSpeedMultiplier = 3f;
    public float chargeDuration = 0.5f;

    // --- 外观切换配置 ---
    [Header("外观切换 (仅视觉效果)")]
    public float switchTimeThreshold = 60f; // 切换形态的游戏时间阈值
    public Sprite normalSprite;             // 基础形态图片
    public Sprite evolvedSprite;            // 进阶形态图片

    // --- 运行时属性 (动态计算后) ---
    [HideInInspector] public float currentMaxHealth;
    [HideInInspector] public float currentMoveSpeed;
    [HideInInspector] public float currentHealth;
    [HideInInspector] public int currentExpValue;
    [HideInInspector] public int currentDamage;

    // --- 组件引用 ---
    private EnemySpawner spawner;
    private Rigidbody2D rb;
    private Transform playerTransform;
    private SpriteRenderer sr;
    private Color originalColor;
    private Vector3 initialScale;

    // --- 状态控制 ---
    private bool isCharging = false;
    private float nextChargeTime = 0f;
    private Coroutine chargeCoroutine;
    private bool isInitialized = false;
    private bool hasEvolvedVisual = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) originalColor = sr.color;
        initialScale = transform.localScale;

        // 查找 Spawner (用于回池)
        // 优先查找 Tag，其次查找名称，防止对象池失效
        GameObject spawnerObj = GameObject.FindGameObjectWithTag("Spawner");
        if (spawnerObj == null) spawnerObj = GameObject.Find("SpawnerManager");
        
        if (spawnerObj != null)
            spawner = spawnerObj.GetComponent<EnemySpawner>();
        else
            Debug.LogWarning($"[Enemy] {gameObject.name} 未找到 EnemySpawner，死亡后将直接销毁而非回池！");
    }

    /// <summary>
    /// 对象池激活时调用：初始化状态
    /// </summary>
    void OnEnable()
    {
        if (!isInitialized)
        {
            // 首次初始化（如果Awake没拿到玩家，这里再试一次）
            FindPlayer();
            isInitialized = true;
        }

        // 重置状态
        currentHealth = currentMaxHealth;
        isCharging = false;
        nextChargeTime = Time.time + (type == EnemyType.SeaTurtle ? chargeInterval : 0f);

        if (sr != null) sr.color = originalColor;
        if (rb != null) rb.velocity = Vector2.zero;

        // 恢复缩放
        transform.localScale = initialScale;

        float gameTime = Time.timeSinceLevelLoad;
        UpdateVisualBasedOnTime(gameTime);
    }

    /// <summary>
    /// 对象池禁用时调用：清理协程
    /// </summary>
    void OnDisable()
    {
        if (chargeCoroutine != null)
        {
            StopCoroutine(chargeCoroutine);
            chargeCoroutine = null;
        }
    }

    void Update()
    {
        if (!gameObject.activeSelf) return;

        // 如果玩家丢失，尝试重新查找
        if (playerTransform == null) FindPlayer();
        if (playerTransform == null) return;

        // 海龟冲撞逻辑检查
        if (type == EnemyType.SeaTurtle && !isCharging && Time.time >= nextChargeTime)
        {
            StartChargeAttack();
        }

        // 移动逻辑
        MoveTowardsPlayer();

        // GameFeel: 挤压动画 (上下弹性)
        // 频率随速度变化，幅度固定
        float stretchAmount = 0.1f;
        float stretchSpeed = currentMoveSpeed * 2.5f; 
        float stretch = Mathf.Sin(Time.time * stretchSpeed) * stretchAmount;

        // 保持初始缩放比例基础上进行形变
        transform.localScale = new Vector3(
            initialScale.x - stretch,
            initialScale.y + stretch,
            initialScale.z
        );

        float currentGameTime = Time.timeSinceLevelLoad;
        UpdateVisualBasedOnTime(currentGameTime);

        // 面向玩家
        FacePlayer();
    }

    void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTransform = playerObj.transform;
    }

    void MoveTowardsPlayer()
    {
        Vector2 direction = (playerTransform.position - transform.position).normalized;
        
        // 如果正在冲撞，使用冲撞速度，否则使用正常速度
        float speed = isCharging ? currentMoveSpeed * chargeSpeedMultiplier : currentMoveSpeed;
        
        // 冲撞期间不改变速度方向（惯性），非冲撞期间实时更新方向
        if (!isCharging)
        {
            rb.velocity = direction * speed;
        }
        // 注意：冲撞逻辑在协程中控制速度，这里不再覆盖
    }

    void FacePlayer()
    {
        if (playerTransform == null) return;
        Vector2 direction = (playerTransform.position - transform.position).normalized;
        
        // 根据方向翻转X轴缩放
        float scaleX = transform.localScale.x;
        if (direction.x < 0)
        {
            if (scaleX > 0) transform.localScale = new Vector3(-Mathf.Abs(scaleX), transform.localScale.y, transform.localScale.z);
        }
        else
        {
            if (scaleX < 0) transform.localScale = new Vector3(Mathf.Abs(scaleX), transform.localScale.y, transform.localScale.z);
        }
    }

    void StartChargeAttack()
    {
        if (chargeCoroutine != null) StopCoroutine(chargeCoroutine);
        chargeCoroutine = StartCoroutine(ChargeAttackRoutine());
    }

    IEnumerator ChargeAttackRoutine()
    {
        isCharging = true;
        Vector2 chargeDir = (playerTransform.position - transform.position).normalized;
        Vector2 chargeVel = chargeDir * currentMoveSpeed * chargeSpeedMultiplier;
        
        float timer = 0f;
        
        // 冲撞过程
        while (timer < chargeDuration)
        {
            rb.velocity = chargeVel;
            timer += Time.deltaTime;
            yield return null;
        }

        // 冲撞结束，短暂停顿或重置
        isCharging = false;
        rb.velocity = Vector2.zero;
        nextChargeTime = Time.time + chargeInterval;
        chargeCoroutine = null;
    }

    /// <summary>
    /// 外部调用：根据游戏时间应用数值成长
    /// 公式: 基础值 * (1 + 增长系数 * 时间)
    /// </summary>
    public void ApplyScaling(float gameTimeSeconds)
    {
        float multiplier = 1f + (statGrowthRate * gameTimeSeconds);
        
        currentMaxHealth = baseMaxHealth * multiplier;
        currentMoveSpeed = baseMoveSpeed * multiplier;
        currentDamage = Mathf.RoundToInt(baseDamage * multiplier);
        currentExpValue = Mathf.RoundToInt(baseExpValue * multiplier); // 经验值也随难度提升？通常不变，看设计需求，此处按统一公式处理
        
        // 如果当前血量小于最大血量（例如刚生成时），则治疗到满血
        if (currentHealth <= 0 || currentHealth > currentMaxHealth) 
        {
            currentHealth = currentMaxHealth;
        }
    }

    /// <summary>
    /// 初始化属性（由Spawner调用）
    /// </summary>
    public void InitStats(float health, float speed, int damage, int exp)
    {
        currentMaxHealth = health;
        currentHealth = health;
        currentMoveSpeed = speed;
        currentDamage = damage;
        currentExpValue = exp;
    }

    public void TakeDamage(int damage)
    {
        if (!gameObject.activeSelf) return;

        currentHealth -= damage;

        // 受击反馈：闪白
        if (sr != null)
        {
            sr.color = Color.red;
            // 取消之前的恢复调用，避免冲突
            CancelInvoke(nameof(ResetColor));
            Invoke(nameof(ResetColor), 0.05f);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void UpdateVisualBasedOnTime(float gameTime)
    {
        // 检查是否达到了切换时间
        bool shouldEvolve = gameTime >= switchTimeThreshold;

        // 只有当状态发生变化时才执行切换（优化性能，避免每帧重复赋值同一个Texture）
        if (shouldEvolve != hasEvolvedVisual)
        {
            hasEvolvedVisual = shouldEvolve;

            // 执行图片切换
            if (hasEvolvedVisual && evolvedSprite != null)
            {
                sr.sprite = evolvedSprite;
                // 可选：如果新图片的尺寸差异很大，可以在这里重置 initialScale 以适配挤压动画
                // initialScale = new Vector3(1.5f, 1.5f, 1); // 手动调整或根据Sprite Bounds计算
            }
            else if (normalSprite != null) // 回到基础形态（虽然通常游戏不会倒流时间）
            {
                sr.sprite = normalSprite;
                // initialScale = ... // 恢复原始大小
            }
        }
    }

    void ResetColor()
    {
        if (sr != null && gameObject.activeSelf)
            sr.color = originalColor;
    }

    void Die()
    {
        if (!gameObject.activeSelf) return;

        // 1. 通知管理器加分/加经验
        // 假设 GameManager 或 ScoreManager 是单例
        // ScoreManager.Instance.AddScore(currentExpValue); 
        // XPManager.Instance.GainExp(currentExpValue);
        // 由于文档未提供具体单例接口，此处打印日志，实际项目中请取消注释并调用对应单例
        Debug.Log($"[Enemy] {type} 死亡，提供经验: {currentExpValue}, 伤害值: {currentDamage}");

        GameManager.Instance.AddKillCount(1);

        // 3. 回收到对象池
        if (spawner != null)
        {
            spawner.ReturnEnemyToPool(this);
        }
        else
        {
            // 兜底：如果没有找到Spawner，必须销毁以防内存泄漏
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 被 Spawner 调用，完全重置对象状态以便复用
    /// </summary>
    public void ReturnToPool()
    {
        // 停止所有逻辑
        if (chargeCoroutine != null) StopCoroutine(chargeCoroutine);
        chargeCoroutine = null;
        isCharging = false;
        CancelInvoke(nameof(ResetColor));

        // 物理重置
        if (rb != null) rb.velocity = Vector2.zero;
        if (rb != null) rb.angularVelocity = 0f;

        // 变换重置
        transform.position = Vector3.zero; // 位置由Spawner设置
        transform.rotation = Quaternion.identity;
        transform.localScale = initialScale;

        // 渲染重置
        if (sr != null) sr.color = originalColor;

        // 禁用
        gameObject.SetActive(false);
    }

    // 调试用：在编辑器中显示当前数值
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)(Vector2.up * currentMoveSpeed));
    }
}