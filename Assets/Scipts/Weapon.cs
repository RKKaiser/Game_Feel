using UnityEngine;

/// <summary>
/// 武器核心逻辑
/// 功能：仅处理射击、弹药、伤害计算
/// 挂载位置：具体的武器物体 (如 Shotgun_Weapon)
/// </summary>
public class Weapon : MonoBehaviour
{
    [Header("武器基础")]
    public string weaponName = "Default Weapon";
    public Transform firePoint; // 枪口位置
    
    [Header("霰弹枪设置")]
    public bool isShotgun = false;
    public int shotgunPellets = 5;
    public float spreadAngle = 15f;
    public float fireRate = 0.2f;
    public int damagePerBullet = 10;
    public float bulletSpeed = 15f;
    public string bulletPoolTag = "Default";

    private float nextFireTime = 0f;

    void Start()
    {
        // 自动寻找枪口，如果没有手动指定
        if (firePoint == null && transform.childCount > 0)
        {
            firePoint = transform.GetChild(0);
        }
        
        if (firePoint == null)
        {
            Debug.LogWarning($"[{weaponName}] 未找到 FirePoint! 子弹将从武器中心发射。");
        }
    }

    void Update()
    {
        // 游戏状态检查
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
            return;

        // 监听射击输入
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
        GameObject owner = GetPlayerOwner();
        
        // 确定发射方向
        // 因为 WeaponAim 脚本已经旋转了父物体 (WeaponPivot)，
        // 所以此时 transform.right 或 firePoint.right 已经是正确的朝向鼠标的方向了！
        Vector2 shootDirection = firePoint != null ? firePoint.right : transform.right;

        if (isShotgun)
        {
            for (int i = 0; i < shotgunPellets; i++)
            {
                // 计算散射角度
                float angleOffset = Random.Range(-spreadAngle, spreadAngle);
                float currentAngle = Mathf.Atan2(shootDirection.y, shootDirection.x) * Mathf.Rad2Deg;
                float finalAngle = currentAngle + angleOffset;
                
                Vector2 finalDir = new Vector2(
                    Mathf.Cos(finalAngle * Mathf.Deg2Rad), 
                    Mathf.Sin(finalAngle * Mathf.Deg2Rad)
                );
                
                SpawnBullet(firePoint != null ? firePoint.position : (Vector2)transform.position, finalDir, owner);
            }
        }
        else
        {
            SpawnBullet(firePoint != null ? firePoint.position : (Vector2)transform.position, shootDirection, owner);
        }
    }

    void SpawnBullet(Vector2 pos, Vector2 direction, GameObject owner)
    {
        if (BulletPoolManager.Instance == null)
        {
            Debug.LogError("BulletPoolManager 未初始化!");
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0, 0, angle);
        
        BulletPoolManager.Instance.SpawnBullet(bulletPoolTag, pos, rot, damagePerBullet, bulletSpeed, owner);
    }

    // 辅助函数：向上查找玩家对象
    GameObject GetPlayerOwner()
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.GetComponent<PlayerController>() != null) 
                return current.gameObject;
            current = current.parent;
        }
        return gameObject;
    }
}