using UnityEngine;

public class Weapon : MonoBehaviour
{
        // 【新增】补回这个变量，解决 PlayerController 的报错
    [Header("武器基础")]
    public string weaponName = "Default Weapon"; 
    
    [Header("关联物体")]
    // 【重要】这里不再是 firePoint，而是武器的根节点（即位于玩家手上的那个空物体）
    public Transform weaponPivot; 
    
    // 枪口依然需要，用于生成子弹
    public Transform firePoint;

    [Header("设置")]
    public bool autoAim = true;
    public float fireRate = 0.2f;
    public int damagePerBullet = 10;
    public float bulletSpeed = 15f;
    public string bulletPoolTag = "Default";

    [Header("霰弹枪")]
    public bool isShotgun = false;
    public int shotgunPellets = 5;
    public float spreadAngle = 15f;

    private float nextFireTime = 0f;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        
        // 自动查找缺失的引用，防止报错
        if (weaponPivot == null) weaponPivot = transform; // 如果没填，默认就是当前物体
        if (firePoint == null)
        {
            firePoint = weaponPivot.GetComponentInChildren<Transform>(); // 尝试找子物体
            Debug.LogWarning("[Weapon] FirePoint not assigned, using first child as fallback.");
        }
    }

    void Update()
    {
        if (weaponPivot == null || mainCamera == null) return;

        // 1. 【核心逻辑】让武器围绕玩家旋转并指向鼠标
        if (autoAim)
        {
            Vector2 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            
            // 获取武器枢轴点的世界坐标（即玩家的手部位置）
            Vector2 pivotPos = weaponPivot.position;
            
            // 计算从手部到鼠标的向量
            Vector2 direction = mouseWorldPos - pivotPos;
            
            // 计算角度
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            
            // 【关键】旋转的是 weaponPivot，而不是 firePoint
            // 这样整个武器（包括图片）都会跟着转，且围绕玩家手部旋转
            weaponPivot.eulerAngles = new Vector3(0, 0, angle);
        }

        // 2. 射击输入
        if (Input.GetButton("Fire1") || Input.GetKey(KeyCode.Space))
        {
            TryShoot();
        }
    }

    public void TryShoot()
    {
        if (Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + fireRate;
        }
    }

    void Shoot()
    {
        if (firePoint == null) return;

        GameObject owner = GetPlayerOwner();

        // 发射方向依然是 firePoint 的 right (因为 firePoint 是 weaponPivot 的子物体，它会跟随旋转)
        Vector2 shootDirection = firePoint.right;

        if (isShotgun)
        {
            for (int i = 0; i < shotgunPellets; i++)
            {
                float angleOffset = Random.Range(-spreadAngle, spreadAngle);
                float currentAngle = Mathf.Atan2(shootDirection.y, shootDirection.x) * Mathf.Rad2Deg;
                float finalAngle = currentAngle + angleOffset;
                
                Vector2 finalDir = new Vector2(Mathf.Cos(finalAngle * Mathf.Deg2Rad), Mathf.Sin(finalAngle * Mathf.Deg2Rad));
                SpawnBullet(firePoint.position, finalDir, owner);
            }
        }
        else
        {
            SpawnBullet(firePoint.position, shootDirection, owner);
        }
    }

    void SpawnBullet(Vector2 pos, Vector2 direction, GameObject owner)
    {
        if (BulletPoolManager.Instance == null) return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0, 0, angle);

        BulletPoolManager.Instance.SpawnBullet(
            bulletPoolTag,
            pos,
            rot,
            damagePerBullet,
            bulletSpeed,
            owner
        );
    }

    GameObject GetPlayerOwner()
    {
        // 向上查找直到找到 PlayerController
        Transform current = transform;
        while (current != null)
        {
            if (current.GetComponent<PlayerController>() != null) return current.gameObject;
            current = current.parent;
        }
        return gameObject;
    }
}