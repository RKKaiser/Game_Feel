using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 经验与升级管理器 (全自动随机版)
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

    [Header("UI显示")]
    public Slider xpSlider;
    public Text levelText;
    public Text notificationText; // 用于显示 "解锁了霰弹枪!" 等提示

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
        UpdateUI();
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
        UpdateUI();

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
        currentXP -= GetRequiredXPForNextLevel();

        // 触发升级特效
        upgradeEffect.PlayEffect();

        Debug.Log($"[XPManager] 升级了! 当前等级: {currentLevel}");

        // --- 自动随机升级逻辑 ---
        PerformRandomUpgrade();

        UpdateUI();
    }

    /// <summary>
    /// 执行随机升级
    /// </summary>
    void PerformRandomUpgrade()
    {
        // 1. 收集所有可能的升级选项
        List<UpgradeOption> possibleOptions = new List<UpgradeOption>();

        // 遍历所有武器预制体（无论是否激活）
        // 这样可以保证即使没解锁霰弹枪，也有概率随机到霰弹枪的升级项
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

        // 检查玩家是否已经拥有该类型的武器 (即是否已激活)
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
            // 激活对应的武器预制体
            selectedOption.weapon.gameObject.SetActive(true);
            targetWeapon = selectedOption.weapon;

            ShowNotification($"解锁新武器: {targetWeapon.weaponName}!");
            Debug.Log($"[XPManager] 解锁了武器: {targetWeapon.weaponName}");
        }
        else
        {
            // --- 情况 B: 升级现有武器 ---
            ShowNotification($"{targetWeapon.weaponName} 属性提升!");
        }

        // 5. 应用升级数值 (这里假设每次升级数值固定，也可以做成随机范围)
        // 比如伤害+5, 射速间隔-0.05
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

    void ShowNotification(string msg)
    {
        if (notificationText != null)
        {
            notificationText.text = msg;
            // 简单处理：2秒后消失
            Invoke("ClearNotification", 2f);
        }
    }

    void ClearNotification()
    {
        if (notificationText != null) notificationText.text = "";
    }

    // --- 数学公式 ---
    public float GetRequiredXPForNextLevel()
    {
        return BaseXPsThresh * Mathf.Pow(1 + XPThreshGrowthRate, currentLevel - 1);
    }

    void UpdateUI()
    {
        if (xpSlider != null)
        {
            xpSlider.maxValue = GetRequiredXPForNextLevel();
            xpSlider.value = currentXP;
        }
        if (levelText != null) levelText.text = "Lv." + currentLevel;
    }

    // 内部类：用于存储升级选项
    class UpgradeOption
    {
        public Weapon weapon;
        public Weapon.UpgradeType type;
    }
}