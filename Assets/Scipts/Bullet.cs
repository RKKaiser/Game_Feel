using UnityEngine;
using System;

public class Bullet : MonoBehaviour
{
    [Header("调试")]
    public bool debugMode = false;

    private int damage;
    private float speed;
    private GameObject owner;
    private Action<Bullet> returnToPoolAction;
    private Vector2 velocity;
    private bool isActive = false;
    
    private Rigidbody2D rb; // 缓存组件

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("[Bullet] Missing Rigidbody2D! Adding one automatically.");
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        
        // 关键设置：防止物理引擎自动旋转子弹
        rb.freezeRotation = true; 
        rb.gravityScale = 0;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive) return;

        // 忽略自己人
        if (other.gameObject == owner) return;
        if (owner != null && other.transform.IsChildOf(owner.transform)) return;

        // 伤害逻辑
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy == null) enemy = other.GetComponentInParent<Enemy>();

        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            if(debugMode) Debug.Log($"Hit {enemy.name}");
        }

        // 击中任何非自己人的物体都回收
        ReturnToPool();
    }

    public void InitData(int dmg, float spd, GameObject own, Action<Bullet> onReturn)
    {
        damage = dmg;
        speed = spd;
        owner = own;
        returnToPoolAction = onReturn;
        isActive = false;
    }

    public void Activate(Vector2 pos, Quaternion rot, Vector2 vel)
    {
        isActive = true;
        transform.position = pos;
        transform.rotation = rot;
        velocity = vel;
        gameObject.SetActive(true);

        // 【核心修复】直接设置 Rigidbody 的速度，不再使用 Translate
        if (rb != null)
        {
            rb.velocity = velocity;
            rb.angularVelocity = 0; // 清除旋转速度
            rb.simulated = true;
            rb.WakeUp(); // 确保唤醒
        }
    }

    public void Deactivate()
    {
        isActive = false;
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0;
            rb.simulated = false; // 停止物理模拟以节省性能
            rb.Sleep();
        }
        gameObject.SetActive(false);
    }

    void ReturnToPool()
    {
        if (!isActive) return;
        isActive = false;

        if (returnToPoolAction != null)
        {
            returnToPoolAction.Invoke(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 【重要】删除了 Update 中的 transform.Translate 逻辑！
    // 现在移动完全由 Rigidbody2D 物理引擎接管
    void Update()
    {
        if (!isActive) return;

        // 仅用于保底回收逻辑
        if (Camera.main != null)
        {
            if (Vector2.Distance(transform.position, Camera.main.transform.position) > 50f)
            {
                ReturnToPool();
            }
        }
    }
}