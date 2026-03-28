using UnityEngine;
using TMPro;

public class HUDManager : MonoBehaviour
{
    [Header("数据源引用 (自动查找或手动拖拽)")]
    public GameManager gameManager;   // 如果为空，会自动查找
    public Weapon currentWeapon;      // 如果为空，会自动查找玩家身上的武器

    [Header("显示项配置 (最多9个)")]
    public DisplayItem[] displayItems; // 数组大小设为9

    [System.Serializable]
    public class DisplayItem
    {
        public TextMeshProUGUI textComponent; // 绑定的 TMP 文本
        public DataType dataType;             // 要显示的数据类型
        public string format = "{0}";         // 格式化字符串，如 "杀敌: {0}"
    }

    public enum DataType
    {
        // GameManager 数据
        Score,          // 当前得分
        KillCount,      // 杀敌数

        // Weapon 数据
        CurrentAmmo,    // 当前弹药（仅机枪有效）
        MaxAmmo,        // 最大弹药（仅机枪有效）
        IsReloading,    // 是否换弹中（返回"换弹中"/"就绪"）
        Damage,         // 武器伤害
        FireRate,       // 射速（每秒攻击次数）
        BulletSpeed,    // 子弹速度
        ExplosionRange  // 爆炸范围（仅手雷有效）
    }

    private void Awake()
    {
        // 自动查找 GameManager 实例
        if (gameManager == null)
            gameManager = GameManager.Instance;

        // 如果还没有武器引用，尝试查找玩家身上的武器组件
        if (currentWeapon == null)
            FindWeapon();
    }

    private void Update()
    {
        // 如果武器引用丢失（例如玩家死亡后被销毁），尝试重新查找
        if (currentWeapon == null)
            FindWeapon();

        // 更新所有显示项
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
        switch (type)
        {
            // GameManager 数据
            case DataType.Score:
                return gameManager != null ? gameManager.currentScore : 0;
            case DataType.KillCount:
                return gameManager != null ? gameManager.killCount : 0;

            // Weapon 数据
            case DataType.CurrentAmmo:
                if (currentWeapon == null) return -1;
                int ammo = currentWeapon.GetCurrentAmmo();
                return ammo < 0 ? "∞" : ammo.ToString(); // 无限弹药显示 ∞
            case DataType.MaxAmmo:
                if (currentWeapon == null) return -1;
                return currentWeapon.GetMaxAmmo();
            case DataType.IsReloading:
                if (currentWeapon == null) return "无武器";
                return currentWeapon.IsReloading() ? "换弹中" : "就绪";
            case DataType.Damage:
                return currentWeapon != null ? currentWeapon.damage : 0;
            case DataType.FireRate:
                // 将 fireRate（间隔秒）转换为每秒攻击次数
                return currentWeapon != null ? Mathf.RoundToInt(1f / currentWeapon.fireRate) : 0;
            case DataType.BulletSpeed:
                return currentWeapon != null ? currentWeapon.bulletSpeed : 0;
            case DataType.ExplosionRange:
                return currentWeapon != null ? currentWeapon.explosionRange : 0;
            default:
                return null;
        }
    }

    private void FindWeapon()
    {
        // 查找玩家（假设有 PlayerController 标签或组件）
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            currentWeapon = player.GetComponentInChildren<Weapon>();
        }
    }
}