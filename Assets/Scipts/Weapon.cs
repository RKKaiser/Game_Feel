using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
public string bulletPoolTag = "Default"; // 对象池标签

[Header("弹药系统 (适用于机枪等)")]
public int maxAmmo = 30;
public float reloadTime = 1.5f;
private int currentAmmo = 0;
private bool isReloading = false;

[Header("霰弹枪专属")]
public int shotgunPellets = 5;
public float spreadAngle = 15f;

[Header("内部状态")]
private float nextFireTime = 0f;

// 武器类型枚举（移除了Grenade）
public enum WeaponType
{
    Shotgun,   // 霰弹枪：一次多发，散射
    MachineGun // 机枪：单发高射速，需换弹
}

// 新增：升级类型枚举 (供 XPManager 使用)
public enum UpgradeType
{
    Damage,
    FireRate,       // 机枪特有
    ReloadSpeed,    // 机枪特有
    PelletCount     // 霰弹枪特有
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
    }
    else
    {
        currentAmmo = -1;
    }
}

void Update()
{
    // 游戏状态检查 (假设 GameManager 控制)
    if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
    {
        return;
    }

    // 射击逻辑
    if (!isReloading && Input.GetButton("Fire1"))
    {
        TryShoot();
    }

    // 换弹逻辑 (仅机枪)
    if (weaponType == WeaponType.MachineGun && !isReloading && currentAmmo <= 0 && Input.GetKeyDown(KeyCode.R))
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
    }
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

        SpawnProjectile(
            firePoint != null ? firePoint.position : (Vector2)transform.position,
            finalDir,
            owner,
            damage,
            bulletSpeed
        );
    }
}

void FireSingleBullet(Vector2 dir, GameObject owner)
{
    SpawnProjectile(
        firePoint != null ? firePoint.position : (Vector2)transform.position,
        dir,
        owner,
        damage,
        bulletSpeed
    );
}

void SpawnProjectile(Vector2 pos, Vector2 direction, GameObject owner, int dmg, float speed)
{
    if (BulletPoolManager.Instance == null)
    {
        Debug.LogError("BulletPoolManager 未初始化!");
        return;
    }

    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    Quaternion rot = Quaternion.Euler(0, 0, angle);

    Bullet proj = BulletPoolManager.Instance.SpawnBullet(bulletPoolTag, pos, rot, dmg, speed, owner);

    if (proj == null) return;

    // 确保普通子弹不是爆炸物
    proj.isExplosive = false;
}

IEnumerator Reload()
{
    isReloading = true;
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
/// <param name="value">升级的数值</param>
public void ApplyUpgrade(UpgradeType upgradeType, float value)
{
    // 首次解锁逻辑
    if (!gameObject.activeSelf)
    {
        UnlockWeapon();
    }

    // 根据升级类型修改属性
    switch (upgradeType)
    {
        case UpgradeType.Damage:
            damage += Mathf.RoundToInt(value);
            Debug.Log($"{weaponName} 伤害提升! 当前伤害: {damage}");
            break;

        case UpgradeType.FireRate:
            fireRate = Mathf.Max(0.05f, fireRate - value);
            Debug.Log($"{weaponName} 射速提升! 当前间隔: {fireRate:F2}s");
            break;

        case UpgradeType.ReloadSpeed:
            if (weaponType == WeaponType.MachineGun)
            {
                reloadTime = Mathf.Max(0.2f, reloadTime - value);
                Debug.Log($"{weaponName} 换弹速度提升! 当前时间: {reloadTime:F2}s");
            }
            break;

        case UpgradeType.PelletCount:
            if (weaponType == WeaponType.Shotgun)
            {
                shotgunPellets += Mathf.RoundToInt(value);
                Debug.Log($"{weaponName} 弹丸数量增加! 当前数量: {shotgunPellets}");
            }
            break;
    }
}

public List<UpgradeType> GetAvailableUpgrades()
{
    List<UpgradeType> upgrades = new List<UpgradeType>();
    upgrades.Add(UpgradeType.Damage);
    upgrades.Add(UpgradeType.FireRate);

    switch (weaponType)
    {
        case WeaponType.MachineGun:
            upgrades.Add(UpgradeType.ReloadSpeed);
            break;
        case WeaponType.Shotgun:
            upgrades.Add(UpgradeType.PelletCount);
            break;
    }

    return upgrades;
}

void UnlockWeapon()
{
    gameObject.SetActive(true);
    Debug.Log($"恭喜解锁新武器: {weaponName}!");

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

