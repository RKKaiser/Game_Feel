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

    // 新增：升级类型枚举 (供 XPManager 使用)
    // 这决定了当玩家升级时，这个武器能提供哪些升级选项
    public enum UpgradeType
    {
        Damage,
        FireRate,
        // 机枪特有
        ReloadSpeed,
        // 霰弹枪特有
        PelletCount,
        // 手雷特有
        ExplosionRange
    }

    void Start()
    {
        // 自动寻找枪口 
        if (firePoint == null && transform.childCount > 0)
        {
            firePoint = transform.GetChild(0);
        }

        // 初始化弹药 
        if (weaponType == WeaponType.MachineGun)
        {
            currentAmmo = maxAmmo;
            // 初始隐藏非机枪武器 (如果需要在Hierarchy中管理多武器，可以在这里控制SetActive)
            // 但根据文档，初始只有机枪，其他武器可能在升级时才实例化或激活
        }
        else
        {
            currentAmmo = -1;
        }
    }

    void Update()
    {
        void Update()
        {
            // 游戏状态检查 (假设 GameManager 控制)
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
            {
                return;
            }

            // 射击逻辑
            if (!isReloading && (Input.GetButton("Fire1")))
            {
                TryShoot();
            }

            // 换弹逻辑 (仅机枪)
            if (weaponType == WeaponType.MachineGun && !isReloading && currentAmmo <= 0 && Input.GetKeyDown(KeyCode.R))
            {
                StartCoroutine(Reload());
            }
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
    /// 升级武器属性
    /// </summary>
    /// <param name="upgradeType">升级的类型</param>
    /// <param name="value">升级的数值 (具体数值由 XPManager 根据配置决定)</param>
    public void ApplyUpgrade(UpgradeType upgradeType, float value)
    {
        // 1. 首次解锁逻辑：如果该武器当前是未激活状态，先激活它
        // (假设你的设计是：所有武器预制体都在场景里，只是初始隐藏；或者这里需要 Instantiate)
        if (!gameObject.activeSelf)
        {
            UnlockWeapon();
        }

        // 2. 根据升级类型修改属性
        switch (upgradeType)
        {
            case UpgradeType.Damage:
                damage += Mathf.RoundToInt(value);
                Debug.Log($"{weaponName} 伤害提升! 当前伤害: {damage}");
                break;

            case UpgradeType.FireRate:
                // 射速提升通常是减少 fireRate 变量的值
                fireRate = Mathf.Max(0.05f, fireRate - value);
                Debug.Log($"{weaponName} 射速提升! 当前间隔: {fireRate:F2}s");
                break;

            case UpgradeType.ReloadSpeed:
                // 机枪：换弹速度提升 (减少时间)
                if (weaponType == WeaponType.MachineGun)
                {
                    reloadTime = Mathf.Max(0.2f, reloadTime - value);
                    Debug.Log($"{weaponName} 换弹速度提升! 当前时间: {reloadTime:F2}s");
                }
                break;

            case UpgradeType.PelletCount:
                // 霰弹枪：增加弹丸数量
                if (weaponType == WeaponType.Shotgun)
                {
                    shotgunPellets += Mathf.RoundToInt(value);
                    Debug.Log($"{weaponName} 弹丸数量增加! 当前数量: {shotgunPellets}");
                }
                break;

            case UpgradeType.ExplosionRange:
                // 手雷：增加爆炸范围
                if (weaponType == WeaponType.Grenade)
                {
                    explosionRange += value;
                    Debug.Log($"{weaponName} 爆炸范围扩大! 当前范围: {explosionRange}");
                }
                break;
        }
    }

    /// <summary>
    /// 解锁并激活该武器
    /// 场景：玩家初始只有机枪，当升级选项中选中霰弹枪/手雷时调用
    /// </summary>
    void UnlockWeapon()
    {
        gameObject.SetActive(true);
        // 这里可以播放一个“解锁”的特效或音效
        Debug.Log($"恭喜解锁新武器: {weaponName}!");

        // 如果是机枪，确保弹药满
        if (weaponType == WeaponType.MachineGun)
        {
            currentAmmo = maxAmmo;
        }
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