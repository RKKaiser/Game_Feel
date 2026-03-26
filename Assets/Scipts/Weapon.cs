using UnityEngine;
using System.Collections;

/// <summary>
/// 武器核心逻辑 (重构版)
/// 功能：处理射击、弹药、换弹、武器类型差异化逻辑、升级接口
/// 挂载位置：具体的武器物体 (如 Shotgun_Weapon, MachineGun_Weapon, Grenade_Weapon)
/// 依赖：BulletPoolManager, GameManager (用于检查游戏状态)
/// </summary>
public class Weapon : MonoBehaviour
{
    [Header("武器基础信息")]
    public string weaponName = "Default Weapon";
    public Transform firePoint; // 枪口/投掷点
    
    [Tooltip("武器类型：决定射击行为模式")]
    public WeaponType weaponType = WeaponType.Shotgun;

    [Header("通用战斗属性")]
    public int damage = 10;
    public float fireRate = 0.2f; // 射击间隔 (秒)
    public float bulletSpeed = 15f;
    public string bulletPoolTag = "Default"; // 对象池标签，区分子弹/手雷
    
    [Header("弹药系统 (适用于机枪等)")]
    public int maxAmmo = 30;
    public float reloadTime = 1.5f;
    private int currentAmmo;
    private bool isReloading = false;

    [Header("霰弹枪专属")]
    public int shotgunPellets = 5;
    public float spreadAngle = 15f;

    [Header("手雷专属")]
    public float explosionRange = 3f; // 爆炸范围，传递给子弹/手雷脚本

    [Header("内部状态")]
    private float nextFireTime = 0f;
    private bool canShoot = true; // 用于控制升级暂停时的射击

    // 武器类型枚举
    public enum WeaponType
    {
        Shotgun,    // 霰弹枪：一次多发，散射
        MachineGun, // 机枪：单发高射速，需换弹
        Grenade     // 手雷：投掷物，爆炸范围
    }

    void Start()
    {
        // 自动寻找枪口
        if (firePoint == null && transform.childCount > 0)
        {
            firePoint = transform.GetChild(0);
        }

        if (firePoint == null)
        {
            Debug.LogWarning($"[{weaponName}] 未找到 FirePoint! 将从武器中心发射。");
        }

        // 初始化弹药
        if (weaponType == WeaponType.MachineGun)
        {
            currentAmmo = maxAmmo;
        }
        else
        {
            currentAmmo = -1; // -1 表示无限弹药或不适用
        }
    }

    void Update()
    {
        // 1. 游戏状态检查 (暂停、结束等)
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
        {
            canShoot = false;
            return;
        }
        canShoot = true;

        // 2. 监听射击输入
        // 鼠标左键点击或按住 (根据武器类型，内部会判断是否达到射速限制)
        if (canShoot && !isReloading && (Input.GetButton("Fire1") || Input.GetKey(KeyCode.Space)))
        {
            TryShoot();
        }

        // 3. 监听换弹输入 (仅限机枪，按 'R' 键)
        if (weaponType == WeaponType.MachineGun && !isReloading && currentAmmo <= 0 && Input.GetKeyDown(KeyCode.R))
        {
            StartCoroutine(Reload());
        }
        // 可选：允许手动换弹
        if (weaponType == WeaponType.MachineGun && !isReloading && currentAmmo > 0 && currentAmmo < maxAmmo && Input.GetKeyDown(KeyCode.R))
        {
            StartCoroutine(Reload());
        }
    }

    public void TryShoot()
    {
        if (Time.time >= nextFireTime)
        {
            // 机枪检查弹药
            if (weaponType == WeaponType.MachineGun)
            {
                if (currentAmmo <= 0)
                {
                    // 自动尝试换弹
                    if (!isReloading) StartCoroutine(Reload());
                    return;
                }
                currentAmmo--;
            }

            Shoot();
            nextFireTime = Time.time + fireRate;
        }
    }

    void Shoot()
    {
        GameObject owner = GetPlayerOwner();
        Vector2 shootDirection = firePoint != null ? firePoint.right : transform.right;

        switch (weaponType)
        {
            case WeaponType.Shotgun:
                FireShotgun(shootDirection, owner);
                break;
            case WeaponType.MachineGun:
                FireSingleBullet(shootDirection, owner);
                break;
            case WeaponType.Grenade:
                FireGrenade(shootDirection, owner);
                break;
        }
        
        // 这里可以添加后坐力触发事件，例如：
        // AudioManager.Instance.PlaySound("ShootSound"); 
        // PlayerController.Instance.AddRecoil(...);
    }

    void FireShotgun(Vector2 baseDir, GameObject owner)
    {
        for (int i = 0; i < shotgunPellets; i++)
        {
            float angleOffset = Random.Range(-spreadAngle, spreadAngle);
            float currentAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
            float finalAngle = currentAngle + angleOffset;

            Vector2 finalDir = new Vector2(
                Mathf.Cos(finalAngle * Mathf.Deg2Rad),
                Mathf.Sin(finalAngle * Mathf.Deg2Rad)
            );

            SpawnProjectile(firePoint != null ? firePoint.position : (Vector2)transform.position, finalDir, owner, damage, bulletSpeed, explosionRange);
        }
    }

    void FireSingleBullet(Vector2 dir, GameObject owner)
    {
        SpawnProjectile(firePoint != null ? firePoint.position : (Vector2)transform.position, dir, owner, damage, bulletSpeed, explosionRange);
    }

    void FireGrenade(Vector2 dir, GameObject owner)
    {
        // 手雷可能需要稍微不同的初速度或逻辑，这里暂时复用，但在SpawnProjectile中会根据Tag区分
        // 也可以在这里给一个向上的初始力如果是抛物线，但2D游戏通常是直线或简单弧线由子弹脚本处理
        SpawnProjectile(firePoint != null ? firePoint.position : (Vector2)transform.position, dir, owner, damage, bulletSpeed, explosionRange);
    }

    void SpawnProjectile(Vector2 pos, Vector2 direction, GameObject owner, int dmg, float speed, float eRange)
    {
        if (BulletPoolManager.Instance == null)
        {
            Debug.LogError("BulletPoolManager 未初始化!");
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0, 0, angle);

        // 【修复点 1】：将返回类型从 GameObject 改为 Bullet，匹配对象池的返回类型
        Bullet proj = BulletPoolManager.Instance.SpawnBullet(bulletPoolTag, pos, rot, dmg, speed, owner);
        
        if (proj == null) return;

        // 【修复点 2】：如果是手雷，配置爆炸属性
        if (weaponType == WeaponType.Grenade)
        {
            proj.isExplosive = true;
            proj.explosionRadius = eRange;
            // 可选：设置层级掩码，只炸敌人 (假设敌人层是 "Enemy")
            // proj.explosionLayerMask = LayerMask.GetMask("Enemy"); 
        }
        else
        {
            // 确保普通子弹不是爆炸物 (防止对象池复用导致脏数据)
            proj.isExplosive = false;
        }
    }

    IEnumerator Reload()
    {
        isReloading = true;
        // 播放换弹音效或动画触发点
        Debug.Log($"[{weaponName}] 开始换弹...");
        
        yield return new WaitForSeconds(reloadTime);
        
        currentAmmo = maxAmmo;
        isReloading = false;
        Debug.Log($"[{weaponName}] 换弹完成!");
    }

    /// <summary>
    /// 升级武器接口
    /// 由 XPManager 或升级 UI 调用，根据升级选项修改属性
    /// </summary>
    public void UpgradeWeapon(string upgradeType, float value)
    {
        switch (upgradeType)
        {
            case "Damage":
                damage += Mathf.RoundToInt(value);
                break;
            case "FireRate":
                // value 可能是减少的时间间隔，或者是增加的射速百分比，这里假设是直接减少间隔
                fireRate = Mathf.Max(0.05f, fireRate - value); 
                break;
            case "PelletCount": // 仅霰弹枪
                if (weaponType == WeaponType.Shotgun)
                    shotgunPellets += Mathf.RoundToInt(value);
                break;
            case "ReloadSpeed": // 仅机枪
                if (weaponType == WeaponType.MachineGun)
                    reloadTime = Mathf.Max(0.1f, reloadTime - value);
                break;
            case "ExplosionRange": // 仅手雷
                if (weaponType == WeaponType.Grenade)
                    explosionRange += value;
                break;
            case "BulletSpeed":
                bulletSpeed += value;
                break;
        }
        
        Debug.Log($"[{weaponName}] 升级成功! 类型:{upgradeType}, 新数值相关参数已更新。");
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
    
    // 供外部查询当前弹药（用于UI显示）
    public int GetCurrentAmmo() => currentAmmo;
    public int GetMaxAmmo() => maxAmmo;
    public bool IsReloading() => isReloading;
}