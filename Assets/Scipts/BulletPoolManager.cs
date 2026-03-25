using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;

[System.Serializable]
public class BulletPoolConfig
{
    public string poolTag;          // 池子标识符，例如 "MachineGun", "Shotgun"
    public GameObject prefab;       // 对应的预制体
    public int defaultCapacity = 20;
    public int maxCapacity = 100;
}

public class BulletPoolManager : MonoBehaviour
{
    public static BulletPoolManager Instance { get; private set; }

    [Header("子弹池配置")]
    [Tooltip("在这里添加不同的子弹类型配置")]
    public List<BulletPoolConfig> poolConfigs = new List<BulletPoolConfig>();

    // 存储多个池子：Key是类型名称，Value是对象池
    private Dictionary<string, IObjectPool<Bullet>> _pools = new Dictionary<string, IObjectPool<Bullet>>();
    
    // 存储每个池子对应的父物体容器 (可选：如果想让不同类型的子弹分不同的父物体)
    // 这里我们统一放在一个 "Bullets" 下，或者你可以扩展逻辑分开
    private Transform _bulletsContainer;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 1. 创建总父物体
        GameObject containerObj = GameObject.Find("Bullets");
        if (containerObj == null)
        {
            containerObj = new GameObject("Bullets");
        }
        _bulletsContainer = containerObj.transform;

        // 2. 根据配置初始化所有池子
        if (poolConfigs.Count == 0)
        {
            Debug.LogWarning("BulletPoolManager: No pool configs assigned!");
            return;
        }

        foreach (var config in poolConfigs)
        {
            if (config.prefab == null)
            {
                Debug.LogError($"BulletPoolManager: Prefab for tag '{config.poolTag}' is missing!");
                continue;
            }

            if (_pools.ContainsKey(config.poolTag))
            {
                Debug.LogError($"BulletPoolManager: Duplicate pool tag '{config.poolTag}'!");
                continue;
            }

            // 创建池子
            var pool = new ObjectPool<Bullet>(
                createFunc: () => CreateBullet(config.prefab),
                actionOnGet: (b) => OnGetBullet(b, config.poolTag),
                actionOnRelease: (b) => OnReleaseBullet(b),
                actionOnDestroy: (b) => OnDestroyBullet(b),
                defaultCapacity: config.defaultCapacity,
                maxSize: config.maxCapacity
            );

            _pools.Add(config.poolTag, pool);
            
            Debug.Log($"[Pool] Initialized: {config.poolTag} with {config.prefab.name}");
        }
    }

    // --- 内部回调 ---

    Bullet CreateBullet(GameObject prefab)
    {
        GameObject obj = Instantiate(prefab, _bulletsContainer);
        obj.name = $"Bullet_{prefab.name}_Instance";
        return obj.GetComponent<Bullet>();
    }

    void OnGetBullet(Bullet bullet, string tag)
    {
        bullet.transform.SetParent(_bulletsContainer);
        // 可以在这里根据 tag 做特殊处理，如果需要的话
    }

    void OnReleaseBullet(Bullet bullet)
    {
        bullet.Deactivate(); // 调用我们在 Bullet.cs 里写的清理方法
        bullet.transform.SetParent(_bulletsContainer);
    }

    void OnDestroyBullet(Bullet bullet)
    {
        Destroy(bullet.gameObject);
    }

    // --- 公开生成方法 ---

    /// <summary>
    /// 生成子弹
    /// </summary>
    /// <param name="poolTag">池子标签 (必须与 Inspector 中配置的一致)</param>
    /// <param name="position">位置</param>
    /// <param name="rotation">旋转</param>
    /// <param name="damage">伤害</param>
    /// <param name="speed">速度</param>
    /// <param name="owner">所有者</param>
    public Bullet SpawnBullet(string poolTag, Vector2 position, Quaternion rotation, int damage, float speed, GameObject owner)
    {
        if (!_pools.ContainsKey(poolTag))
        {
            Debug.LogError($"BulletPoolManager: No pool found for tag '{poolTag}'. Check your configuration!");
            return null;
        }

        IObjectPool<Bullet> pool = _pools[poolTag];
        Bullet bullet = pool.Get();

        // 初始化数据
        bullet.InitData(damage, speed, owner, (b) => pool.Release(b));

        // 计算速度
        Vector2 velocity = rotation * Vector2.right * speed;

        // 激活
        bullet.Activate(position, rotation, velocity);

        return bullet;
    }
    
    // 辅助方法：检查池子是否存在
    public bool HasPool(string tag) => _pools.ContainsKey(tag);
}