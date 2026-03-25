using UnityEngine;

public class Weapon : MonoBehaviour
{
    [Header("武器基础")]
    public string weaponName = "Default Weapon";

    [Header("关联物体")]
    public Transform weaponPivot;
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
        if (weaponPivot == null) weaponPivot = transform;
        if (firePoint == null)
        {
            // 尝试找子物体作为枪口
            if (transform.childCount > 0)
                firePoint = transform.GetChild(0);
            else
                Debug.LogWarning("[Weapon] FirePoint missing and no children found!");
        }
    }

    void Update()
    {
        // --- 【核心修复】检查游戏是否处于暂停或结束状态 ---
        // 如果 GameManager 存在且当前不是 Playing 状态，则直接返回，不执行任何武器逻辑
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
        {
            return; 
        }
        
        // 备选方案：如果不想依赖 GameManager，也可以直接用 Time.timeScale 判断
        // if (Time.timeScale <= 0f) return;

        if (weaponPivot == null || mainCamera == null) return;

        // 1. 旋转逻辑 (仅在游戏进行时执行)
        if (autoAim)
        {
            Vector2 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 pivotPos = weaponPivot.position;
            Vector2 direction = mouseWorldPos - pivotPos;
            
            // 防止除以零或无效向量导致的 NaN
            if (direction.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                weaponPivot.eulerAngles = new Vector3(0, 0, angle);
            }
        }

        // 2. 射击输入 (仅在游戏进行时执行)
        // 注意：这里假设 Input 是在 Update 里调用的。如果游戏暂停，这段代码现在不会被执行到。
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
        Transform current = transform;
        while (current != null)
        {
            if (current.GetComponent<PlayerController>() != null) return current.gameObject;
            current = current.parent;
        }
        return gameObject;
    }
}