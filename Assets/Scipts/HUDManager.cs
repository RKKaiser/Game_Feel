using UnityEngine;
using TMPro;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }

    [Header("自动查找的数据源")]
    private GameManager gameManager;
    private Weapon playerWeapon;   // 自动查找玩家身上的武器

    [Header("显示项配置（最多9个）")]
    public DisplayItem[] displayItems;  // 数组大小设为你需要的数量（最大9）

    [System.Serializable]
    public class DisplayItem
    {
        public TextMeshProUGUI textComponent; // 绑定的 TMP 文本
        public DataType dataType;             // 要显示的数据类型
        public string format = "{0}";         // 格式化字符串
    }

    public enum DataType
    {
        // GameManager 数据
        KillCount,

        // Weapon 数据（直接读取字段，不区分武器类型）
        Gun_Damage,
        Pen_Damage,
        Boom_Damage,
        Gun_FireRate,           // 射速（次/秒）= 1 / fireRate
        Pen_FireRate,
        Boom_FireRate,
        ReloadTime,         // 换弹时间（秒）
        ExplosionRange,     // 爆炸范围
        ShotgunPellets,        // 当前弹药
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        gameManager = GameManager.Instance;
    }

    private void Start()
    {
        // 初始查找武器
        FindWeapon();
    }

    private void Update()
    {
        // 如果武器引用丢失，尝试重新查找（例如玩家重生后）
        if (playerWeapon == null)
            FindWeapon();

        // 刷新所有显示项
        foreach (var item in displayItems)
        {
            if (item.textComponent == null) continue;
            object value = GetDataValue(item.dataType);
            if (value != null)
            {
                item.textComponent.text = string.Format(item.format, value);
            }
        }
    }

    private object GetDataValue(DataType type)
    {
        // GameManager 数据
        if (type == DataType.KillCount)
            return gameManager != null ? gameManager.killCount : 0;

        // 武器数据
        if (playerWeapon == null)
            return 0;

        switch (type)
        {
            //最终按照伤害和射速按照公式来
            case DataType.Gun_Damage:
                return playerWeapon.damage;
            case DataType.Pen_Damage:
                return playerWeapon.damage*2;
            case DataType.Boom_Damage:
                return playerWeapon.damage*5;
            case DataType.Gun_FireRate:
                return playerWeapon.fireRate;
            case DataType.Pen_FireRate:
                return playerWeapon.fireRate*2;
            case DataType.Boom_FireRate:
                return playerWeapon.fireRate*3;
            case DataType.ReloadTime:
                return playerWeapon.reloadTime;
            case DataType.ExplosionRange:
                return playerWeapon.explosionRange.ToString("F1");
            case DataType.ShotgunPellets:
                return playerWeapon.shotgunPellets;
            default:
                return null;
        }
    }

    private void FindWeapon()
    {
        // 查找玩家身上的 Weapon 组件（假设武器挂在玩家或其子物体上）
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            playerWeapon = player.GetComponentInChildren<Weapon>();
        }
        else
        {
            // 如果没有玩家，尝试直接查找场景中的第一个 Weapon
            playerWeapon = FindObjectOfType<Weapon>();
        }
    }
}