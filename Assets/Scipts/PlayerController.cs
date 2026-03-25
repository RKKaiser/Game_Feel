using UnityEngine;
using System; // 引入 System 以使用 Action 事件
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    // --- 新增：死亡事件 (观察者模式) ---
    // 当玩家死亡时触发，不包含任何具体逻辑，只通知“发生了死亡”
    public static event Action OnPlayerDied;

    [Header("移动设置")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;

    [Header("武器设置")]
    public Transform weaponPivot;
    public List<Weapon> availableWeapons = new List<Weapon>();
    private Weapon currentWeapon;

    [Header("状态")]
    public bool isDead = false;
    public int maxHealth = 1;
    private int currentHealth;

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

    void HandleMovement()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        Vector2 moveDirection = new Vector2(moveX, moveY).normalized;
        rb.velocity = moveDirection * moveSpeed;
    }

    void HandleAiming()
    {
        // 如果没有挂载 Pivot 或者当前没有激活的武器，则不执行
        if (weaponPivot == null) return;

        // 1. 获取鼠标世界坐标
        Vector3 mouseScreenPos = Input.mousePosition;
        // 修正 Z 轴，确保 2D 坐标转换正确 (假设相机正交且垂直于屏幕)
        float zDistance = -Camera.main.transform.position.z; 
        mouseScreenPos.z = zDistance;
        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);

        // 2. 计算从玩家中心到鼠标的方向向量
        Vector2 aimDirection = (mouseWorldPos - (Vector2)transform.position).normalized;

        // 防止方向向量为零导致后续计算错误
        if (aimDirection.sqrMagnitude > 0.001f)
        {
            // 3. 计算目标角度 (弧度转角度)
            float targetAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

            // --- 核心逻辑开始 ---

            // A. 定义武器围绕玩家旋转的半径 (根据手感调整，例如 1.5 或 2.0)
            float weaponRadius = 1.5f;

            // B. 计算 WeaponPivot 的目标世界位置
            // 公式：玩家位置 + (归一化方向 * 半径)
            // 这确保了 Pivot 永远在玩家周围的圆周上
            Vector2 targetPosition = (Vector2)transform.position + aimDirection * weaponRadius;

            // C. 应用变换到 WeaponPivot
            // 1. 设置位置 (实现公转)
            weaponPivot.position = targetPosition;
            
            // 2. 设置旋转 (实现指向鼠标)
            // 因为 Weapon 是 Pivot 的子物体且 LocalPos 为 (0,0,0)，
            // Pivot 旋转时，Weapon 会跟着转到正确的朝向
            weaponPivot.rotation = Quaternion.Euler(0, 0, targetAngle);

            // --- 核心逻辑结束 ---
            
            // [可选] 调试绘制：在 Scene 视图画出半径圆和射线，方便观察
            // Debug.DrawLine(transform.position, (Vector3)targetPosition, Color.yellow);
            // Debug.DrawRay((Vector3)targetPosition, aimDirection * 2f, Color.green);
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

    public void TakeDamage(int damage)
    {
        if (isDead) return;
        currentHealth -= damage;

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
        if (isDead) return;
        
        isDead = true;
        rb.velocity = Vector2.zero;
        if(playerCollider != null) playerCollider.enabled = false;

        Debug.Log("玩家死亡：触发死亡事件。");

        // --- 核心修改：只触发事件，不执行任何游戏逻辑 ---
        OnPlayerDied?.Invoke();
        
        // 可选：播放死亡动画或粒子
        // gameObject.SetActive(false); 
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;
        if (collision.gameObject.CompareTag("Enemy") || collision.gameObject.GetComponent<Enemy>() != null)
        {
            TakeDamage(999);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;
        if (other.CompareTag("Enemy") || other.GetComponent<Enemy>() != null)
        {
            TakeDamage(999);
        }
    }
    
    // 清理静态事件订阅，防止内存泄漏（虽然单例生命周期长，但这是好习惯）
    void OnDestroy()
    {
        // 如果有任何针对此实例的订阅需要清理，在这里做
        // 静态事件通常不需要在实例销毁时清理，除非订阅者也是静态且长期存在的
    }
}