using UnityEngine; 
using System.Collections.Generic; 

/// <summary> 
/// 经验与升级管理器 (全自动随机版 - 无UI逻辑)
/// 功能：
/// 1. 管理经验值与等级
/// 2. 升级时自动随机选择武器进行强化
/// 3. 如果随机到的属性属于未拥有的武器，自动解锁并激活该武器
/// </summary>
public class XPManager : MonoBehaviour 
{
    public static XPManager Instance;

    [Header("升级配置 (文档要求)")]
    [Tooltip("基础经验阈值")]
    public float BaseXPsThresh = 100f;
    [Tooltip("经验阈值增长系数")]
    public float XPThreshGrowthRate = 0.2f;

    [Header("武器引用")]
    [Tooltip("玩家身上挂载的所有武器预制体 (包括初始未激活的)")]
    public List<Weapon> allWeaponPrefabs = new List<Weapon>();

    // 内部状态
    private float currentXP;
    private int currentLevel = 1;

    void Awake() 
    {
        if (Instance == null) 
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        } 
        else 
        {
            Destroy(gameObject);
        }
    }

    void Start() 
    {
        // 确保初始状态正确，比如只激活机枪
        InitializeWeapons();
    }

    /// <summary>
    /// 初始化武器状态
    /// 假设列表里的第一个武器（机枪）是初始激活的，其他是隐藏的
    /// </summary>
    void InitializeWeapons() 
    {
        for (int i = 0; i < allWeaponPrefabs.Count; i++) 
        {
            if (i == 0) 
            { // 假设第一个是初始武器
                allWeaponPrefabs[i].gameObject.SetActive(true);
            } 
            else 
            {
                allWeaponPrefabs[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 外部调用：增加经验
    /// </summary>
    public void AddExperience(float xp) 
    {
        currentXP += xp;

        // 循环检查，防止一次获得大量经验连升多级
        while (currentXP >= GetRequiredXPForNextLevel()) 
        {
            LevelUp();
        }
    }

    /// <summary>
    /// 核心升级逻辑 (全自动随机)
    /// </summary>
    void LevelUp() 
    {
        currentLevel++;
        currentXP -= GetRequiredXPForNextLevel(); // 扣除升级所需经验
        
        Debug.Log($"[XPManager] 升级了! 当前等级: {currentLevel}");

        // --- 自动随机升级逻辑 ---
        PerformRandomUpgrade();
    }

    /// <summary>
    /// 执行随机升级
    /// </summary>
    void PerformRandomUpgrade() 
    {
        // 1. 收集所有可能的升级选项
        List<UpgradeOption> possibleOptions = new List<UpgradeOption>();
        // 遍历所有武器预制体（无论是否激活）
        foreach (Weapon w in allWeaponPrefabs) 
        {
            List<Weapon.UpgradeType> types = w.GetAvailableUpgrades();
            foreach (var type in types) 
            {
                possibleOptions.Add(new UpgradeOption { weapon = w, type = type });
            }
        }
        if (possibleOptions.Count == 0) return;

        // 2. 随机选择一个选项
        UpgradeOption selectedOption = possibleOptions[Random.Range(0, possibleOptions.Count)];

        // 3. 确定目标武器
        Weapon targetWeapon = null;
        bool hasWeapon = false;
        foreach (Weapon w in allWeaponPrefabs) 
        {
            if (w.weaponType == selectedOption.weapon.weaponType && w.gameObject.activeSelf) 
            {
                hasWeapon = true;
                targetWeapon = w;
                break;
            }
        }

        // 4. 逻辑分支：解锁 vs 升级
        if (!hasWeapon) 
        {
            // --- 情况 A: 解锁新武器 ---
            selectedOption.weapon.gameObject.SetActive(true);
            targetWeapon = selectedOption.weapon;
            Debug.Log($"[XPManager] 解锁了武器: {targetWeapon.weaponName}");
        } 
        // 注意：原UI提示 ShowNotification 已被删除

        // 5. 应用升级数值
        float upgradeValue = GetUpgradeValue(selectedOption.type);
        targetWeapon.ApplyUpgrade(selectedOption.type, upgradeValue);
    }

    /// <summary>
    /// 获取升级的具体数值 (可根据类型调整)
    /// </summary>
    float GetUpgradeValue(Weapon.UpgradeType type) 
    {
        switch (type) 
        {
            case Weapon.UpgradeType.Damage: return 5f;
            case Weapon.UpgradeType.FireRate: return 0.05f; // 减少间隔
            case Weapon.UpgradeType.ReloadSpeed: return 0.2f; // 减少换弹时间
            case Weapon.UpgradeType.PelletCount: return 2f; // 增加2个弹丸
            case Weapon.UpgradeType.ExplosionRange: return 0.5f;
            default: return 1f;
        }
    }

    // --- 数学公式 ---
    public float GetRequiredXPForNextLevel() 
    {
        return BaseXPsThresh * Mathf.Pow(1 + XPThreshGrowthRate, currentLevel - 1);
    }

    // 内部类：用于存储升级选项
    class UpgradeOption 
    {
        public Weapon weapon;
        public Weapon.UpgradeType type;
    }
}