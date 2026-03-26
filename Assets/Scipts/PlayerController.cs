using UnityEngine;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    // --- 观察者模式：事件定义 ---
    
    // 玩家死亡事件
    // 订阅者: GameManager (结束游戏), SoundManager (播放死亡音效)
    public static event Action OnPlayerDied;

    // 玩家获得经验事件
    // 订阅者: XPManager (处理升级逻辑)
    public static event Action<int> OnPlayerGainedXP;

    // 玩家升级完成事件
    public static event Action OnPlayerLevelUp;

    [Header("移动设置")]
    public float moveSpeed = 5f;
    // 注意：边界限制已通过场景中的碰撞体对象实现，此处不再需要 minX/maxX 设置

    [Header("武器设置")]
    public Transform weaponPivot; // 武器的旋转中心点 (空物体)
    
    // 【修正点】：将旋转半径暴露给 Inspector，方便策划或美术调整手感
    [Tooltip("武器绕玩家旋转的距离（半径）")]
    public float weaponRotationRadius = 1.5f; 
    
    public List<Weapon> availableWeapons = new List<Weapon>(); // 初始可用武器列表
    private Weapon currentWeapon;

    [Header("状态与数值")]
    public bool isDead = false;
    public int currentLevel = 1;
    public float currentXP = 0f;
    
    // 以下数值可由 XPManager 或升级选项动态修改
    [HideInInspector] public float weaponDamageMultiplier = 1f; 
    [HideInInspector] public float weaponFireRateMultiplier = 1f;
    [HideInInspector] public int shotgunBoltCountBonus = 0; 
    [HideInInspector] public float grenadeRadiusBonus = 0f; 

    private Rigidbody2D rb;
    private Collider2D playerCollider;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();

        isDead = false;
        currentLevel = 1;
        currentXP = 0f;

        // 初始化武器
        if (availableWeapons.Count > 0 && currentWeapon == null)
        {
            SwitchWeapon(0);
        }
        
        rb.simulated = true;
    }

    void Update()
    {
        if (isDead) return;

        // 如果游戏因升级界面而暂停，则不处理输入
        if (Time.timeScale == 0f) return;

        HandleMovement();
        HandleAiming();
        HandleShooting();
    }

    void HandleMovement()
    {
        // 使用 WASD 控制玩家移动
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        Vector2 moveDirection = new Vector2(moveX, moveY).normalized;

        // 直接设置速度
        rb.velocity = moveDirection * moveSpeed;
    }

    void HandleAiming()
    {
        if (weaponPivot == null) return;

        // 1. 获取鼠标世界坐标
        Vector3 mouseScreenPos = Input.mousePosition;
        float zDistance = -Camera.main.transform.position.z; 
        mouseScreenPos.z = zDistance;
        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);

        // 2. 计算从玩家中心到鼠标的方向向量
        Vector2 aimDirection = (mouseWorldPos - (Vector2)transform.position).normalized;

        if (aimDirection.sqrMagnitude > 0.1f)
        {
            // 3. 计算目标角度
            float targetAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

            // 【修正点】：使用 Inspector 中配置的 rotationRadius，而不是硬编码
            float radius = weaponRotationRadius;

            // 4. 计算 Pivot 目标位置：实现武器绕玩家公转
            Vector2 targetPosition = (Vector2)transform.position + aimDirection * radius;

            // 5. 应用变换
            weaponPivot.position = targetPosition;
            weaponPivot.rotation = Quaternion.Euler(0, 0, targetAngle);
        }
    }

    void HandleShooting()
    {
        if (currentWeapon == null) return;

        if (Input.GetMouseButton(0)) 
        {
            currentWeapon.TryShoot();
        }
    }

    /// <summary>
    /// 增加经验值 (由 XPManager 调用)
    /// </summary>
    public void AddXP(int amount)
    {
        if (isDead) return;

        currentXP += amount;
        OnPlayerGainedXP?.Invoke(amount);
    }

    /// <summary>
    /// 触发升级流程 (由 XPManager 调用)
    /// </summary>
    public void TriggerLevelUp()
    {
        if (isDead) return;

        currentLevel++;
        Debug.Log($"玩家升级！当前等级：{currentLevel}");
        
        // 暂停游戏时间
        Time.timeScale = 0f;
        
        // 通知 UIManager 显示升级选项面板
        // UIManager.Instance.ShowUpgradePanel(...);
        
        OnPlayerLevelUp?.Invoke();
    }

    /// <summary>
    /// 完成升级选择 (由 UI 按钮调用)
    /// </summary>
    public void FinishUpgradeSelection()
    {
        Time.timeScale = 1f;
    }

    /// <summary>
    /// 切换武器
    /// </summary>
    public void SwitchWeapon(int index)
    {
        if (index < 0 || index >= availableWeapons.Count) return;

        foreach (var wp in availableWeapons)
        {
            if(wp != null) wp.gameObject.SetActive(false);
        }

        currentWeapon = availableWeapons[index];
        if (currentWeapon != null)
        {
            currentWeapon.gameObject.SetActive(true);
            currentWeapon.transform.SetParent(weaponPivot, false);
            currentWeapon.transform.localPosition = Vector3.zero;
            currentWeapon.transform.localRotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// 玩家死亡逻辑
    /// </summary>
    public void Die()
    {
        if (isDead) return;

        isDead = true;

        rb.velocity = Vector2.zero;
        rb.simulated = false; 

        if(playerCollider != null) playerCollider.enabled = false;

        Debug.Log("玩家死亡：触发游戏结束流程。");

        // 触发观察者事件
        OnPlayerDied?.Invoke();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;

        if (collision.gameObject.CompareTag("Enemy") || collision.gameObject.GetComponent<Enemy>() != null)
        {
            Die();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;

        if (other.CompareTag("Enemy") || other.GetComponent<Enemy>() != null)
        {
            Die();
        }
    }
}