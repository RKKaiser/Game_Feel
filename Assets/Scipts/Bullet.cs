using UnityEngine;

public class Bullet : MonoBehaviour
{
    [HideInInspector] public int damage;
    [HideInInspector] public float speed;
    [HideInInspector] public GameObject owner; // 记录是谁发射的，防止打到自己

    private Rigidbody2D rb;
    private float lifeTime = 3f; // 子弹最大存在时间
    private float timer = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // 初始化方法 (类似对象池的 SetStats)
    public void Init(int dmg, float spd, GameObject player)
    {
        damage = dmg;
        speed = spd;
        owner = player;
        timer = 0f;
        
        // 确保刚激活时速度方向正确 (依赖发射时的 Rotation)
        rb.velocity = transform.right * speed; 
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifeTime)
        {
            ReturnToPool();
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // 如果碰到发射者自己，忽略
        if (collision.gameObject == owner) return;

        // 碰到敌人
        Enemy enemy = collision.gameObject.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            ReturnToPool();
            return;
        }

        // 碰到墙壁或其他障碍物
        // 可以根据 Tag 判断是否销毁
        if (!collision.gameObject.CompareTag("Player")) 
        {
            ReturnToPool();
        }
    }

    void ReturnToPool()
    {
        // 如果有子弹对象池管理器，在这里调用回收
        // BulletPool.Instance.Return(this);
        
        // 临时方案：直接销毁 (建议后续也做成对象池)
        Destroy(gameObject);
    }
}