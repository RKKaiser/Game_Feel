using UnityEngine;
using System;
using System.Collections.Generic;

public class Bullet : MonoBehaviour
{
    [Header("调试")]
    public bool debugMode = false;

    [Header("手雷/爆炸设置")]
    public bool isExplosive = false;      // 是否是爆炸物
    public float explosionRadius = 3f;    // 爆炸范围
    public LayerMask explosionLayerMask;  // 爆炸影响的层级 (可选，设为0则影响所有)

    private int damage;
    private float speed;
    private GameObject owner;
    private Action<Bullet> returnToPoolAction;
    private Vector2 velocity;
    private bool isActive = false;
    private Rigidbody2D rb;

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

        if (isExplosive)
        {
            // 如果是手雷，触发爆炸逻辑，不立即回收
            Explode();
        }
        else
        {
            // 普通子弹逻辑
            ApplyDamageToTarget(other);
            ReturnToPool();
        }
    }

    void ApplyDamageToTarget(Collider2D other)
    {
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy == null) enemy = other.GetComponentInParent<Enemy>();

        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            if(debugMode) Debug.Log($"Hit {enemy.name}");
        }
    }

    void Explode()
    {
        if(debugMode) Debug.Log($"Boom! Radius: {explosionRadius}");

        // 1. 可视化效果 (可选：这里假设你有爆炸特效预制体，可以在对象池管理器里生成，或者简单画个圈)
        // DebugDrawCircle(transform.position, explosionRadius); 

        // 2. 范围检测
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, explosionLayerMask);

        foreach (Collider2D hit in hits)
        {
            // 忽略自己和所有者
            if (hit.gameObject == gameObject || hit.gameObject == owner) continue;
            if (owner != null && hit.transform.IsChildOf(owner.transform)) continue;

            // 对范围内的敌人造成伤害
            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy == null) enemy = hit.GetComponentInParent<Enemy>();

            if (enemy != null)
            {
                // 可选：根据距离计算伤害衰减
                float dist = Vector2.Distance(transform.position, hit.transform.position);
                float damageMultiplier = Mathf.Clamp01(1 - (dist / explosionRadius));
                int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damage * damageMultiplier));
                
                enemy.TakeDamage(finalDamage);
                if(debugMode) Debug.Log($"Explosion hit {enemy.name} for {finalDamage} dmg");
            }
            
            // 可选：如果有可破坏的环境物体，也可以在这里处理
            // DestructibleObject destructible = hit.GetComponent<DestructibleObject>();
            // if(destructible != null) destructible.TakeDamage(damage);
        }

        // 爆炸完成后回收
        ReturnToPool();
    }

    public void InitData(int dmg, float spd, GameObject own, Action<Bullet> onReturn)
    {
        damage = dmg;
        speed = spd;
        owner = own;
        returnToPoolAction = onReturn;
        isActive = false;
        
        // 重置爆炸相关属性 (防止对象池复用时的脏数据)
        // 注意：isExplosive 和 explosionRadius 通常由外部设置，这里不重置为默认值，
        // 而是依赖外部在激活前重新赋值，或者在 SpawnBullet 里赋值。
    }

    public void Activate(Vector2 pos, Quaternion rot, Vector2 vel)
    {
        isActive = true;
        transform.position = pos;
        transform.rotation = rot;
        velocity = vel;
        gameObject.SetActive(true);

        if (rb != null)
        {
            rb.velocity = velocity;
            rb.angularVelocity = 0;
            rb.simulated = true;
            rb.WakeUp();
        }
    }

    public void Deactivate()
    {
        isActive = false;
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0;
            rb.simulated = false;
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

    void Update()
    {
        if (!isActive) return;

        // 保底回收逻辑
        if (Camera.main != null)
        {
            if (Vector2.Distance(transform.position, Camera.main.transform.position) > 50f)
            {
                ReturnToPool();
            }
        }
    }

    // 调试用：绘制爆炸范围
    void OnDrawGizmosSelected()
    {
        if (isExplosive)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}