using UnityEngine;

namespace OctoberStudio.Upgrades
{
    /// <summary>
    /// 升级管理器，负责管理游戏中所有升级的等级、数值获取和保存
    /// </summary>
    public class UpgradesManager : MonoBehaviour
    {
        private static UpgradesManager instance;  // 单例实例

        [SerializeField] UpgradesDatabase database;  // 升级数据库

        private UpgradesSave save;  // 升级存档

        private void Awake()
        {
            // 单例模式，确保全局只有一个实例
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            DontDestroyOnLoad(this);  // 场景切换时不销毁

            // 获取升级存档并初始化
            save = GameController.SaveManager.GetSave<UpgradesSave>("Upgrades Save");
            save.Init();

            // 遍历所有升级，如果当前等级低于开发初始等级，则设置为开发初始等级
            for (int i = 0; i < database.UpgradesCount; i++)
            {
                var upgrade = database.GetUpgrade(i);

                if (GetUpgradeLevel(upgrade.UpgradeType) < upgrade.DevStartLevel)
                {
                    save.SetUpgradeLevel(upgrade.UpgradeType, upgrade.DevStartLevel);
                }
            }
        }

        /// <summary>
        /// 增加指定升级的等级
        /// </summary>
        /// <param name="upgradeType">升级类型</param>
        public void IncrementUpgradeLevel(UpgradeType upgradeType)
        {
            var level = save.GetUpgradeLevel(upgradeType);
            save.SetUpgradeLevel(upgradeType, level + 1);
        }

        /// <summary>
        /// 获取指定升级的当前等级
        /// </summary>
        /// <param name="upgradeType">升级类型</param>
        /// <returns>当前等级，-1表示未获得</returns>
        public int GetUpgradeLevel(UpgradeType upgradeType)
        {
            return save.GetUpgradeLevel(upgradeType);
        }

        /// <summary>
        /// 判断指定升级是否已获得
        /// </summary>
        /// <param name="upgradeType">升级类型</param>
        /// <returns>已获得返回true，否则false</returns>
        public bool IsUpgradeAquired(UpgradeType upgradeType)
        {
            var level = save.GetUpgradeLevel(upgradeType);
            return level != -1;
        }

        /// <summary>
        /// 获取指定升级的数据配置
        /// </summary>
        /// <param name="upgradeType">升级类型</param>
        /// <returns>升级数据</returns>
        public UpgradeData GetUpgradeData(UpgradeType upgradeType)
        {
            return database.GetUpgrade(upgradeType);
        }

        /// <summary>
        /// 获取指定升级的当前数值（根据当前等级）
        /// </summary>
        /// <param name="upgradeType">升级类型</param>
        /// <returns>当前数值，如果未获得则返回0</returns>
        public float GetUpgadeValue(UpgradeType upgradeType)
        {
            var data = GetUpgradeData(upgradeType);
            var level = GetUpgradeLevel(upgradeType);

            if (level >= 0)
            {
                return data.GetLevel(level).Value;
            }

            return 0;
        }
    }
}