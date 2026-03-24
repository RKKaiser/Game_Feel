using UnityEngine;
using System.Collections;

public class Enemy : MonoBehaviour
{
    [Header("基础属性")]
    public string enemyName = "Enemy";
    public float baseMaxHealth = 10f;
    public float baseMoveSpeed = 2f;
    public int baseDamage = 1;
    public int baseExpValue = 10;

    [Header("特殊效果")]
    public bool isTurtle = false;
    public float chargeInterval = 3f;
    public float chargeSpeedMultiplier = 3f;

    // 运行时属性
    [HideInInspector] public float currentMaxHealth;
    [HideInInspector] public float currentMoveSpeed;
    [HideInInspector] public float currentHealth;
    [HideInInspector] public int currentExpValue;

    // 对象池引用
    private EnemySpawner spawner;
    
    private Rigidbody2D rb;
    private Transform playerTransform;
    private bool isCharging = false;
    private float nextChargeTime = 0f;
    private Vector3 initialScale;
    private SpriteRenderer sr;
    private Color originalColor;

    // 用于在返回池子时停止协程
    private Coroutine chargeCoroutine;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        if(sr != null) originalColor = sr.color;
        initialScale = transform.localScale;
        
        // 自动查找 Spawner (假设场景里只有一个 EnemySpawner)
        GameObject spawnerObj = GameObject.FindGameObjectWithTag("Spawner"); 
        // 如果没有Tag，也可以用 FindObjectOfType<EnemySpawner>()
        if (spawnerObj == null) spawnerObj = GameObject.Find("SpawnerManager");
        
        if (spawnerObj != null)
            spawner = spawnerObj.GetComponent<EnemySpawner>();
        else
            Debug.LogWarning("未找到 EnemySpawner，对象池回收可能失效！");
    }

    void Start()
    {
        // 仅在第一次激活或初始时查找玩家
        // 注意：如果对象池复用，Start不会再次调用，所以初始化逻辑最好放在 OnEnable 或专门的 Init 中
        // 这里为了简单，我们在 OnEnable 中处理动态依赖
    }

    // 当对象从池中取出被激活时调用
    void OnEnable()
    {
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        // 重置冲撞时间
        if (isTurtle)
        {
            nextChargeTime = Time.time + chargeInterval;
        }
        
        // 确保颜色恢复
        if(sr != null) sr.color = originalColor;
    }

    void Update()
    {
        if (!gameObject.activeSelf || playerTransform == null) return;

        Vector2 direction = (playerTransform.position - transform.position).normalized;
        float speed = currentMoveSpeed;

        // 海龟冲撞逻辑
        if (isTurtle && Time.time >= nextChargeTime && !isCharging)
        {
            if (chargeCoroutine != null) StopCoroutine(chargeCoroutine);
            chargeCoroutine = StartCoroutine(ChargeAttack(direction));
        }

        // 简单的挤压动画
        float stretch = Mathf.Sin(Time.time * (speed * 2)) * 0.1f;
        transform.localScale = new Vector3(initialScale.x - stretch, initialScale.y + stretch, initialScale.z);

        // 移动
        rb.velocity = direction * speed;
        
        // 面向玩家
        if (direction.x < 0)
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        else
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
    }

    IEnumerator ChargeAttack(Vector2 dir)
    {
        isCharging = true;
        float duration = 0.5f;
        float timer = 0f;
        Vector2 chargeVel = dir * currentMoveSpeed * chargeSpeedMultiplier;
        
        while (timer < duration)
        {
            rb.velocity = chargeVel;
            timer += Time.deltaTime;
            yield return null;
        }
        
        isCharging = false;
        rb.velocity = Vector2.zero;
        chargeCoroutine = null;
    }

    public void TakeDamage(int damage)
    {
        if (!gameObject.activeSelf) return;

        currentHealth -= damage;
        
        // 受击闪白
        if (sr != null)
        {
            sr.color = Color.red;
            Invoke(nameof(ResetColor), 0.05f);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void ResetColor()
    {
        if(sr != null && gameObject.activeSelf) sr.color = originalColor;
    }

    void Die()
    {
        if (!gameObject.activeSelf) return;

        // TODO: 生成粒子特效 (粒子系统需要独立于敌人存在，或者使用对象池管理粒子)
        // 简单起见，这里暂时不生成粒子，或者确保粒子系统不被销毁
        
        // 通知玩家加分/经验 (需要访问 GameManager，这里暂略，假设由 Spawner 或全局事件处理)
        // GameManager.Instance.AddScore(currentExpValue);
        Debug.Log($"{enemyName} 死亡 (已回池)，获得经验 {currentExpValue}");

        // 【关键修改】不再 Destroy，而是回收到池中
        if (spawner != null)
        {
            spawner.ReturnEnemyToPool(this);
        }
        else
        {
            // 如果找不到 Spawner，只能销毁以防内存泄漏
            Destroy(gameObject);
        }
    }

    // 【新方法】被 Spawner 调用，用于完全重置状态以便复用
    public void ReturnToPool()
    {
        // 1. 停止所有协程
        if (chargeCoroutine != null) StopCoroutine(chargeCoroutine);
        chargeCoroutine = null;
        isCharging = false;
        
        // 2. 重置物理速度
        if(rb != null) rb.velocity = Vector2.zero;

        // 3. 重置变换
        transform.position = Vector3.zero; // 位置会在下次生成时设置
        transform.rotation = Quaternion.identity;
        transform.localScale = initialScale;

        // 4. 重置渲染
        if(sr != null) sr.color = originalColor;

        // 5. 禁用物体
        gameObject.SetActive(false);
    }
    
    // 供 Spawner 设置属性
    public void SetStats(float health, float speed, int exp)
    {
        currentMaxHealth = health;
        currentHealth = health;
        currentMoveSpeed = speed;
        currentExpValue = exp;
    }
}