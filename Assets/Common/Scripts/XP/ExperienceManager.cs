using OctoberStudio.Easing;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace OctoberStudio
{
    /// <summary>
    /// 经验值管理器：负责处理玩家经验值的获取、升级逻辑以及UI更新。
    /// </summary>
    public class ExperienceManager : MonoBehaviour
    {
        // 经验值数据配置（包含各级别所需经验值等）
        [SerializeField] ExperienceData experienceData;

        // 经验值相关的UI组件（用于显示进度条、等级文本等）
        [SerializeField] ExperienceUI experienceUI;

        // "Level Up" 声音事件的哈希值，用于优化音频播放性能
        private static readonly int LEVEL_UP_HASH = "Level Up".GetHashCode();

        // 当前经验值 (只读，外部不可直接修改)
        public float XP { get; private set; }

        // 升级到下一级所需的目标经验值 (只读)
        public float TargetXP { get; private set; }

        // 当前等级 (只读)
        public int Level { get; private set; }

        // 当等级发生变化时触发的事件，监听者可在此响应升级逻辑
        public event UnityAction<int> onXpLevelChanged;

        // 关卡存档数据引用
        StageSave stageSave;

        /// <summary>
        /// 初始化经验系统
        /// </summary>
        /// <param name="testingPreset">测试预设数据（用于调试或特定模式），若为null则读取存档</param>
        public void Init(PresetData testingPreset)
        {
            // 获取关卡存档数据
            stageSave = GameController.SaveManager.GetSave<StageSave>("Stage");

            // 默认重置经验和等级
            XP = 0;
            Level = 0;

            // 优先级1：如果有测试预设数据，使用预设中的等级
            if (testingPreset != null)
            {
                Level = testingPreset.XPLevel;
            }
            // 优先级2：如果没有重置存档数据，则读取存档中的等级和经验
            else if (!stageSave.ResetStageData)
            {
                Level = stageSave.XPLEVEL;
                XP = stageSave.XP;
            }
            // 优先级3：如果是新游戏或需要重置，将存档中的等级和经验清零
            else
            {
                stageSave.XPLEVEL = 0;
                stageSave.XP = 0;
            }

            // 根据当前等级计算下一级所需的目标经验值
            TargetXP = experienceData.GetXP(Level);

            // 在下一帧更新UI进度条（避免初始化时的视觉闪烁或逻辑冲突）
            EasingManager.DoNextFrame().SetOnFinish(() => experienceUI.SetProgress(XP / TargetXP));

            // 更新UI显示的等级文本（显示为 Level + 1，通常因为内部等级从0开始计数，而显示给玩家的是从1开始）
            experienceUI.SetLevelText(Level + 1);
        }

        /// <summary>
        /// 增加经验值
        /// </summary>
        /// <param name="xp">基础经验值</param>
        public void AddXP(float xp)
        {
            // 累加经验值，并乘以玩家的經驗倍率修饰符
            XP += xp * PlayerBehavior.Player.XPMultiplier;

            // 同步更新存档中的经验值
            stageSave.XP = XP;

            // 如果当前经验值达到或超过升级目标
            if (XP >= TargetXP)
            {
                // 计算下一级的目标经验值
                var nextTarget = experienceData.GetXP(Level + 1);

                // 如果经验值足够连续升多级（当前经验 >= 当前目标 + 下一级目标）
                if (XP >= TargetXP + nextTarget)
                {
                    // 启动协程，逐帧处理升级，以便让其他系统（如技能选择面板）有时间介入
                    StartCoroutine(IncreaseLevelCoroutine());
                }
                else
                {
                    // 仅升一级
                    IncreaseLevel();
                }
            }

            // 更新UI进度条
            experienceUI.SetProgress(XP / TargetXP);
        }

        /// <summary>
        /// 协程：处理连续升级逻辑
        /// 允许在每次升级之间暂停极短时间，以便其他管理器（如技能管理器）暂停时间并显示升级面板
        /// </summary>
        private IEnumerator IncreaseLevelCoroutine()
        {
            // 只要经验值还足够升级，就循环执行
            while (XP >= TargetXP)
            {
                IncreaseLevel();

                // 暂停一帧（0.001秒），给予其他系统处理升级事件的时间（例如显示技能选择界面）
                yield return new WaitForSeconds(0.001f);
            }

            // 循环结束后，确保UI进度条是最新的
            experienceUI.SetProgress(XP / TargetXP);
        }

        /// <summary>
        /// 执行单次升级逻辑
        /// </summary>
        private void IncreaseLevel()
        {
            // 等级加1
            Level++;

            // 扣除当前升级所需的经验值（保留溢出部分用于下一级）
            XP -= TargetXP;

            // 更新存档中的等级和经验
            stageSave.XPLEVEL = Level;
            stageSave.XP = XP;

            // 重新计算下一级的目标经验值
            TargetXP = experienceData.GetXP(Level);

            // 更新UI显示的等级文本
            experienceUI.SetLevelText(Level + 1);

            // 播放升级音效
            GameController.AudioManager.PlaySound(LEVEL_UP_HASH);

            // 触发等级变更事件，通知其他系统（如解锁新技能、增加属性等）
            onXpLevelChanged?.Invoke(Level);
        }
    }
}