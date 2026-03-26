using UnityEngine;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    // --- 观察者模式：死亡事件 ---
    // 用于通知 GameManager 处理游戏结束、分数结算
    // 用于通知 SoundManager 播放死亡音效
    public static event Action OnPlayerDied;

    [Header("移动设置")]
    public float moveSpeed = 5f;
    // 用于限制玩家在游戏场景边界内移动 (需在 Inspector 中由关卡设计者设置，或通过代码动态获取)
    public float minX = -10f;
    public float maxX = 10f;
    public float minY = -10f;
    public float maxY = 10f;
    public bool useBoundary = false; // 是否启用边界限制

    [Header("武器设置")]
    public Transform weaponPivot; // 武器的旋转中心点
    public List<Weapon> availableWeapons = new List<Weapon>(); // 初始可用武器列表
    private Weapon currentWeapon;

    [Header("状态")]
    public bool isDead = false;

    private Rigidbody2D rb;
    private Collider2D playerCollider;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();

        isDead = false;

        // 初始化武器
        if (availableWeapons.Count > 0 && currentWeapon == null)
        {
            SwitchWeapon(0);
        }
    }

    void Update()
    {
        if (isDead) return;

        HandleMovement();
        HandleAiming();
        HandleShooting();
        
        // 如果启用了边界限制，则在每帧修正位置
        if (useBoundary)
        {
            ClampPosition();
        }
    }

    void HandleMovement()
    {
        // 文档要求：使用 WASD 控制玩家移动
        // GetAxisRaw 提供无加速度的即时输入，适合此类游戏的手感
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        
        Vector2 moveDirection = new Vector2(moveX, moveY).normalized;
        
        // 直接设置速度，保证移动响应无延迟
        rb.velocity = moveDirection * moveSpeed;
    }

    void HandleAiming()
    {
        if (weaponPivot == null) return;

        // 1. 获取鼠标世界坐标
        Vector3 mouseScreenPos = Input.mousePosition;
        // 修正 Z 轴，确保 2D 坐标转换正确
        float zDistance = -Camera.main.transform.position.z; 
        mouseScreenPos.z = zDistance;
        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);

        // 2. 计算从玩家中心到鼠标的方向向量
        Vector2 aimDirection = (mouseWorldPos - (Vector2)transform.position).normalized;

        if (aimDirection.sqrMagnitude > 0.001f)
        {
            // 3. 计算目标角度
            float targetAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

            // 4. 计算武器旋转半径 (可根据具体武器手感在 Inspector 调整或硬编码)
            float weaponRadius = 1.5f;

            // 5. 计算 Pivot 目标位置：实现武器绕玩家公转
            Vector2 targetPosition = (Vector2)transform.position + aimDirection * weaponRadius;

            // 6. 应用变换
            weaponPivot.position = targetPosition;
            weaponPivot.rotation = Quaternion.Euler(0, 0, targetAngle);
        }
    }

    void HandleShooting()
    {
        if (currentWeapon == null) return;

        // 文档要求：点击或按住鼠标左键射击
        if (Input.GetMouseButton(0)) 
        {
            currentWeapon.TryShoot();
        }
    }

    // 边界限制逻辑
    void ClampPosition()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        transform.position = pos;
    }

    /// <summary>
    /// 切换武器
    /// </summary>
    /// <param name="index">武器列表索引</param>
    public void SwitchWeapon(int index)
    {
        if (index < 0 || index >= availableWeapons.Count) return;

        // 隐藏所有武器
        foreach (var wp in availableWeapons)
        {
            if(wp != null) wp.gameObject.SetActive(false);
        }

        // 激活新武器
        currentWeapon = availableWeapons[index];
        if (currentWeapon != null)
        {
            currentWeapon.gameObject.SetActive(true);
            // 将武器挂载到 Pivot 下，确保随鼠标旋转
            currentWeapon.transform.SetParent(weaponPivot, false);
            currentWeapon.transform.localPosition = Vector3.zero;
            currentWeapon.transform.localRotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// 文档逻辑：玩家被怪物碰到会直接死亡
    /// 不再需要血量计算，直接触发死亡流程
    /// </summary>
    public void Die()
    {
        if (isDead) return;

        isDead = true;
        
        // 停止物理运动
        rb.velocity = Vector2.zero;
        rb.simulated = false; // 禁用物理模拟
        
        // 禁用碰撞体，防止死亡后继续触发碰撞
        if(playerCollider != null) playerCollider.enabled = false;

        Debug.Log("玩家死亡：触发游戏结束流程。");

        // 触发观察者事件
        // GameManager 监听此事件以停止生成敌人、显示结算界面
        // SoundManager 监听此事件以播放死亡音效
        OnPlayerDied?.Invoke();
    }

    // 碰撞检测：接触敌人即死
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;
        
        // 检查标签或组件
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

    void OnDestroy()
    {
        // 实例销毁时不需要清理静态事件，因为事件是静态的且订阅者通常也是单例或长期存在的
        // 但如果未来有针对实例的动态订阅，需在此处移除
    }
}