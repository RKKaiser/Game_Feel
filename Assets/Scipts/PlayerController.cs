using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f; // 如果需要平滑旋转可以启用，否则直接朝向鼠标

    [Header("武器设置")]
    public Transform weaponPivot; // 武器挂载点（子物体）
    public List<Weapon> availableWeapons = new List<Weapon>(); // 可选武器列表
    private Weapon currentWeapon;

    [Header("状态")]
    public bool isDead = false;
    public int maxHealth = 1; // 设定为1，碰到即死
    private int currentHealth;

    // 组件缓存
    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private SpriteRenderer sr;
    private Color originalColor;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) originalColor = sr.color;

        currentHealth = maxHealth;
        isDead = false;

        // 初始化第一个武器，或者通过外部调用 SwitchWeapon
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
    }

    // --- 移动逻辑 (WASD) ---
    void HandleMovement()
    {
        float moveX = Input.GetAxisRaw("Horizontal"); // A/D
        float moveY = Input.GetAxisRaw("Vertical");   // W/S

        Vector2 moveDirection = new Vector2(moveX, moveY).normalized;

        // 直接设置速度，忽略物理惯性（手感更跟手）
        rb.velocity = moveDirection * moveSpeed;
    }

    // --- 瞄准逻辑 (鼠标) ---
    void HandleAiming()
    {
        if (weaponPivot == null) return;

        // 获取鼠标世界坐标
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        
        // 计算从玩家指向鼠标的向量
        Vector2 aimDirection = (mousePos - (Vector2)transform.position).normalized;

        // 计算目标角度
        float targetAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

        // 方式A: 瞬间朝向 (适合射击游戏)
        weaponPivot.rotation = Quaternion.Euler(0, 0, targetAngle);

        // 方式B: 平滑旋转 (如果希望武器转动有延迟感，取消下面注释，注释掉上面一行)
        // Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);
        // weaponPivot.rotation = Quaternion.Slerp(weaponPivot.rotation, targetRotation, Time.deltaTime * rotationSpeed);
    }

    // --- 射击逻辑 ---
    void HandleShooting()
    {
        if (currentWeapon == null) return;

        // 支持点击射击 或 按住连射
        if (Input.GetMouseButton(0)) 
        {
            currentWeapon.TryShoot();
        }
    }

    // --- 武器切换逻辑 ---
      public void SwitchWeapon(int index)
    {
        if (index < 0 || index >= availableWeapons.Count) 
        {
            Debug.LogWarning($"无效的武器索引: {index}");
            return;
        }

        // 禁用当前所有武器
        foreach (var wp in availableWeapons)
        {
            if(wp != null) wp.gameObject.SetActive(false);
        }

        // 启用新武器
        currentWeapon = availableWeapons[index];
        if (currentWeapon != null)
        {
            currentWeapon.gameObject.SetActive(true);
            
            // 强制重置位置，确保它吸附在 Pivot 上
            currentWeapon.transform.SetParent(weaponPivot, false);
            currentWeapon.transform.localPosition = Vector3.zero;
            currentWeapon.transform.localRotation = Quaternion.identity;
            
            Debug.Log($"切换武器至: {currentWeapon.weaponName}");
        }
    }


    // --- 死亡逻辑 ---
    // 当怪物碰到玩家时调用
    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        
        // 闪红反馈
        if (sr != null)
        {
            sr.color = Color.red;
            Invoke(nameof(ResetColor), 0.1f);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void ResetColor()
    {
        if (!isDead && sr != null) sr.color = originalColor;
    }

    void Die()
    {
        isDead = true;
        rb.velocity = Vector2.zero; // 停止移动
        playerCollider.enabled = false; // 关闭碰撞，防止重复触发死亡
        
        Debug.Log("玩家死亡！游戏结束。");

        // 通知 GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver();
        }

        // 可选：播放死亡动画/粒子，然后隐藏玩家
        // gameObject.SetActive(false); 
    }

    // 碰撞检测：碰到怪物直接死
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;

        // 假设怪物都有 "Enemy" Tag 或者 Enemy 组件
        if (collision.gameObject.CompareTag("Enemy") || collision.gameObject.GetComponent<Enemy>() != null)
        {
            TakeDamage(999); // 直接秒杀
        }
    }
    
    // 也可以使用 OnTriggerEnter2D，取决于你的 Collider 设置 (IsTrigger)
    void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;
        if (other.CompareTag("Enemy") || other.GetComponent<Enemy>() != null)
        {
            TakeDamage(999);
        }
    }
}