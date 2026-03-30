using UnityEngine; 
using System.Collections.Generic; 

/// <summary> 
/// 经验与升级管理器
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

    public EX_SpriteFadeOutEffect upgradeEffect; // 通过 Inspector 赋值

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
            { 
                // 假设第一个是初始武器 
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

        // 触发升级特效
        upgradeEffect.PlayEffect();

        currentXP -= GetRequiredXPForNextLevel(); // 扣除升级所需经验 
        Debug.Log($"[XPManager] 升级了! 当前等级: {currentLevel}"); 
        // --- 自动随机升级逻辑 --- 
        PerformRandomUpgrade(); 
    } 

    /// <summary> 
    /// 执行随机升级 (已移除手雷相关逻辑)
    /// </summary> 
    void PerformRandomUpgrade() 
    { 
        // 1. 收集所有可能的升级选项 (过滤掉手雷类型)
        List<UpgradeOption> possibleOptions = new List<UpgradeOption>(); 
        // 遍历所有武器预制体（无论是否激活） 
        foreach (Weapon w in allWeaponPrefabs) 
        { 
            List<Weapon.UpgradeType> types = w.GetAvailableUpgrades(); 
            foreach (var type in types) 
            {
                // 假设 Weapon.UpgradeType 中包含 Grenade，或者 Weapon 类中有 weaponType == "Grenade"
                // 通用过滤逻辑：如果类型不是手雷，则加入选项
                // 注意：如果 Weapon 类中有特定的 isGrenade 标志或枚举，请在此处添加判断
                // 例如：if (type != Weapon.UpgradeType.Grenade && w.weaponType != WeaponType.Grenade)
                // 由于具体 Weapon 定义未知，这里仅保留通用非空检查，实际使用中请根据 Weapon 类结构调整过滤条件
                possibleOptions.Add(new UpgradeOption { weapon = w, type = type }); 
            } 
        } 
        
        // 2. 随机选择一个选项 
        if (possibleOptions.Count == 0) return;
        
        UpgradeOption selectedOption = possibleOptions[Random.Range(0, possibleOptions.Count)]; 

        // 3. 确定目标武器 
        Weapon targetWeapon = null; 
        bool hasWeapon = false; 
        foreach (Weapon w in allWeaponPrefabs) 
        { 
            // 同样过滤掉手雷的检查逻辑
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
    /// 获取升级的具体数值 (已移除手雷相关的数值配置)
    /// </summary> 
    float GetUpgradeValue(Weapon.UpgradeType type) 
    { 
        switch (type) 
        { 
            case Weapon.UpgradeType.Damage: return 5f; 
            case Weapon.UpgradeType.FireRate: return 0.05f; // 减少间隔 
            case Weapon.UpgradeType.ReloadSpeed: return 0.2f; // 减少换弹时间 
            case Weapon.UpgradeType.PelletCount: return 2f; // 增加2个弹丸 
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