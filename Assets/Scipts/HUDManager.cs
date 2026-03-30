using UnityEngine;
using TMPro;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }

    [Header("游戏管理数据源")]
    private GameManager gameManager;
    private Weapon playerWeapon;

    [Header("显示映射表（最多9项）")]
    public DisplayItem[] displayItems; // 数组大小限制为最多9个元素

    [System.Serializable]
    public class DisplayItem
    {
        public TextMeshProUGUI textComponent; // 绑定 TMP 文本
        public DataType dataType; // 要显示的数据类型
        public string format = "{0}"; // 格式化字符串
    }

    public enum DataType
    {
        // GameManager 数据
        KillCount, 
        
        // Weapon 数据
        Gun_Damage, 
        Pen_Damage, 
        Gun_FireRate, 
        Pen_FireRate, 
        ReloadTime, 
        ShotgunPellets, // 霰弹枪弹丸数
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
        // 初始化查找武器
        FindWeapon();
    }

    private void Update()
    {
        // 如果武器引用丢失则重新查找（防止销毁重建导致的空引用）
        if (playerWeapon == null) FindWeapon();

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
        if (playerWeapon == null) return 0;

        switch (type)
        {
            // 枪械伤害与射速
            case DataType.Gun_Damage: 
                return playerWeapon.damage; 
            case DataType.Pen_Damage: 
                return playerWeapon.damage * 2; 
            case DataType.Gun_FireRate: 
                return playerWeapon.fireRate;
            case DataType.Pen_FireRate: 
                return playerWeapon.fireRate * 2; 
            case DataType.ReloadTime: 
                return playerWeapon.reloadTime; 
            case DataType.ShotgunPellets: 
                return playerWeapon.shotgunPellets; 
            default: 
                return null;
        }
    }

    private void FindWeapon()
    {
        // 查找玩家并获取子物体中的 Weapon 组件
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            playerWeapon = player.GetComponentInChildren<Weapon>();
        }
        else
        {
            // 如果没有玩家控制器，直接查找场景中的第一个 Weapon
            playerWeapon = FindObjectOfType<Weapon>();
        }
    }
}