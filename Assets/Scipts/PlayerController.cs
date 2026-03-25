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
        if (weaponPivot == null) return;
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 aimDirection = (mousePos - (Vector2)transform.position).normalized;
        float targetAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        weaponPivot.rotation = Quaternion.Euler(0, 0, targetAngle);
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