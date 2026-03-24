using UnityEngine;
using System.Collections.Generic;

public enum WeaponType { MachineGun, Shotgun }

public class Weapon : MonoBehaviour
{
    [Header("武器基础设置")]
    public WeaponType weaponType = WeaponType.MachineGun;
    public string weaponName;
    
    [Tooltip("射击间隔 (秒)。散弹枪指两次喷发之间的间隔，机枪指连发间隔")]
    public float fireRate = 0.1f; 
    
    public int damagePerBullet = 5; // 单颗子弹伤害
    public float bulletSpeed = 15f;
    public GameObject bulletPrefab; 

    [Header("散弹枪专属设置")]
    [Tooltip("仅当类型为散弹枪时生效：一次发射多少颗子弹")]
    public int shotgunPelletCount = 5;
    [Tooltip("仅当类型为散弹枪时生效：散射角度 (度)，例如 30 表示左右各 15 度")]
    public float shotgunSpreadAngle = 30f;

    [Header("发射点")]
    public Transform firePoint; 

    [Header("特效")]
    public ParticleSystem muzzleFlash;
    public AudioClip shootSound;

    private float nextFireTime = 0f;
    private AudioSource audioSrc;

    void Awake()
    {
        audioSrc = GetComponent<AudioSource>();
        if (firePoint == null) firePoint = transform;
        
        // 根据类型自动命名方便调试
        if (string.IsNullOrEmpty(weaponName))
        {
            weaponName = weaponType.ToString();
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
        // 播放特效
        if (muzzleFlash != null) muzzleFlash.Play();
        if (shootSound != null && audioSrc != null)
        {
            // 如果是散弹枪，声音可能更沉闷，可以在此处做差异化处理，暂时统一播放
            audioSrc.PlayOneShot(shootSound);
        }

        if (bulletPrefab == null)
        {
            Debug.LogError($"武器 {weaponName} 缺少子弹预制体!");
            return;
        }

        // --- 核心逻辑分支 ---
        if (weaponType == WeaponType.MachineGun)
        {
            // 1. 机枪模式：发射单颗子弹
            SpawnBullet(firePoint.rotation);
        }
        else if (weaponType == WeaponType.Shotgun)
        {
            // 2. 散弹枪模式：发射多颗扇形子弹
            SpawnShotgunBurst();
        }
    }

    // 发射单颗子弹
    void SpawnBullet(Quaternion rotation)
    {
        GameObject bulletObj = Instantiate(bulletPrefab, firePoint.position, rotation);
        InitBullet(bulletObj);
    }

    // 发射散弹 burst
    void SpawnShotgunBurst()
    {
        float startAngle = -shotgunSpreadAngle / 2f;
        float angleStep = shotgunSpreadAngle / (shotgunPelletCount - 1); // 如果只有1颗，步长为0

        // 特殊情况：如果只有1颗，直接打中间
        if (shotgunPelletCount <= 1)
        {
            SpawnBullet(firePoint.rotation);
            return;
        }

        for (int i = 0; i < shotgunPelletCount; i++)
        {
            // 计算当前子弹的角度
            float currentAngle = startAngle + (angleStep * i);
            
            // 基于枪口朝向旋转当前角度
            Quaternion spreadRotation = firePoint.rotation * Quaternion.Euler(0, 0, currentAngle);
            
            SpawnBullet(spreadRotation);
        }
    }

    void InitBullet(GameObject bulletObj)
    {
        Bullet bulletScript = bulletObj.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            // 获取玩家引用 (假设武器的父父物体是 Player，或者通过其他方式传递)
            // 这里简单处理：向上查找 PlayerController
            PlayerController player = transform.root.GetComponent<PlayerController>();
            if (player == null) player = transform.parent.parent.GetComponent<PlayerController>();
            
            bulletScript.Init(damagePerBullet, bulletSpeed, player != null ? player.gameObject : gameObject);
        }
    }
}