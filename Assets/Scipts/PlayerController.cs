using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 5f;
    public float boundaryPadding = 0.5f; // 距离边界的缓冲距离

    [Header("射击设置")]
    public Transform firePoint; // 子弹发射点
    public GameObject bulletPrefab; // 子弹预制体 (稍后创建)
    public float fireRate = 0.2f; // 射击间隔 (秒)
    
    // 内部变量
    private Rigidbody2D rb;
    private Vector2 movement;
    private Vector2 mouseWorldPosition;
    private float nextFireTime = 0f;
    private bool isFiring = false;
    
    // 边界缓存
    private float minX, maxX, minY, maxY;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        
        if (firePoint == null)
        {
            // 如果没有手动指定，尝试寻找子物体 "WeaponPivot"
            Transform pivot = transform.Find("WeaponPivot");
            if (pivot != null) firePoint = pivot;
            else firePoint = transform; // 如果都没有，就在中心发射
        }

    }

    void Update()
    {
        // 1. 获取输入 (移动)
        float moveX = Input.GetAxisRaw("Horizontal"); // A/D or Left/Right
        float moveY = Input.GetAxisRaw("Vertical");   // W/S or Up/Down
        
        movement = new Vector2(moveX, moveY).normalized;

        // 2. 获取鼠标位置并计算角度
        // 将鼠标屏幕坐标转换为世界坐标
        mouseWorldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        
        // 计算玩家朝向鼠标的角度
        Vector2 aimDirection = (mouseWorldPosition - (Vector2)transform.position).normalized;
        float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        
        // 旋转玩家 (只旋转Z轴) 和 武器挂载点
        // 注意：如果你的螃蟹素材默认朝右，直接赋值即可。如果默认朝上，需要 -90
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // 3. 射击逻辑
        // 支持点击 (GetButtonDown) 和 按住 (GetButton)
        if (Input.GetButton("Fire1")) // 默认是鼠标左键
        {
            isFiring = true;
        }
        else
        {
            isFiring = false;
        }

        if (isFiring && Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + fireRate;
        }
    }

    void FixedUpdate()
    {
        // 物理移动
        rb.velocity = movement * moveSpeed;
    }

    void Shoot()
    {
        if (bulletPrefab != null && firePoint != null)
        {
            // 实例化子弹
            Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
            
            // TODO: 这里稍后可以添加射击音效和屏幕震动调用
            // AudioManager.Instance.PlayShootSound();
            // ScreenShake.Instance.TriggerShake(0.1f, 0.2f);
        }
        else
        {
            Debug.LogWarning("未分配子弹预制体或发射点！");
        }
    }

}