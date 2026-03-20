using OctoberStudio.Bossfight;
using OctoberStudio.Extensions;
using OctoberStudio.Pool;
using OctoberStudio.Timeline.Bossfight;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace OctoberStudio
{
    /// <summary>
    /// 关卡场地管理器：负责管理游戏场景的边界、背景、生成逻辑以及Boss战围栏。
    /// 它根据关卡类型（无尽、矩形等）动态切换行为模式，并处理Boss围栏的实例化与交互。
    /// </summary>
    public class StageFieldManager : MonoBehaviour
    {
        // 单例实例引用，用于全局访问
        private static StageFieldManager instance;

        // Boss战数据库，用于查找不同Boss对应的配置数据（如围栏预制体）
        [SerializeField] BossfightDatabase bossfightDatabase;

        // 当前关卡类型 (只读)
        public StageType StageType { get; private set; }

        // 背景预制体引用 (只读)
        public GameObject BackgroundPrefab { get; private set; }

        // 当前激活的Boss围栏行为组件 (只读)
        public BossFenceBehavior Fence { get; private set; }

        // 当前场地的具体行为接口实现（如无尽模式、矩形模式等）
        private IFieldBehavior field;

        // 缓存所有可能的Boss围栏实例，避免重复实例化
        // Key: Boss类型, Value: 对应的围栏行为组件
        private Dictionary<BossType, BossFenceBehavior> fences;

        private void Awake()
        {
            // 初始化单例
            instance = this;
        }

        /// <summary>
        /// 初始化关卡场地
        /// </summary>
        /// <param name="stageData">关卡数据配置</param>
        /// <param name="director">PlayableDirector，用于获取时间轴上的Boss轨道数据</param>
        public void Init(StageData stageData, PlayableDirector director)
        {
            // 根据关卡类型实例化对应的场地行为逻辑
            switch (stageData.StageType)
            {
                case StageType.Endless:
                    field = new EndlessFieldBehavior();
                    break;
                case StageType.VerticalEndless:
                    field = new VerticalFieldBehavior();
                    break;
                case StageType.HorizontalEndless:
                    field = new HorizontalFieldBehavior();
                    break;
                case StageType.Rect:
                    field = new RectFieldBehavior();
                    break;
            }

            // 初始化场地行为，传入场地数据和生成属性
            field.Init(stageData.StageFieldData, stageData.SpawnProp);

            // 初始化围栏字典
            fences = new Dictionary<BossType, BossFenceBehavior>();

            // 从时间轴导演器中获取所有Boss轨道上的Boss资产
            var bossAssets = director.GetAssets<BossTrack, Boss>();

            // 遍历所有预设的Boss，预先生成并缓存其对应的围栏对象
            for (int i = 0; i < bossAssets.Count; i++)
            {
                var bossAsset = bossAssets[i];
                // 从数据库获取该Boss的详细战斗配置
                var bossData = bossfightDatabase.GetBossfight(bossAsset.BossType);

                // 如果该类型的围栏尚未创建，则进行实例化
                if (!fences.ContainsKey(bossData.BossType))
                {
                    // 实例化围栏预制体并获取组件
                    var fence = Instantiate(bossData.FencePrefab).GetComponent<BossFenceBehavior>();

                    // 初始状态设为非激活，等待需要时再显示
                    fence.gameObject.SetActive(false);

                    // 初始化围栏逻辑
                    fence.Init();

                    // 存入字典缓存
                    fences.Add(bossData.BossType, fence);
                }
            }
        }

        /// <summary>
        /// 生成指定类型的Boss围栏
        /// </summary>
        /// <param name="bossType">Boss类型</param>
        /// <param name="offset">位置偏移量</param>
        /// <returns>围栏生成的中心坐标</returns>
        public Vector2 SpawnFence(BossType bossType, Vector2 offset)
        {
            // 从缓存中获取对应的围栏对象并设为当前激活围栏
            Fence = fences[bossType];

            // 根据当前场地逻辑计算Boss生成的合适位置
            var center = field.GetBossSpawnPosition(Fence, offset);

            // 执行围栏生成逻辑（显示围栏、设置位置等）
            Fence.SpawnFence(center);

            return center;
        }

        /// <summary>
        /// 移除当前激活的Boss围栏
        /// </summary>
        public void RemoveFence()
        {
            if (Fence != null)
            {
                Fence.RemoveFence();
                Fence = null;
            }
        }

        /// <summary>
        /// 从当前围栏中移除所有道具/障碍物
        /// </summary>
        public void RemovePropFromFence()
        {
            if (Fence != null)
            {
                field.RemovePropFromBossFence(Fence);
            }
        }

        private void Update()
        {
            // 每帧更新场地行为逻辑（例如无尽模式的滚动、边界检测等）
            field.Update();
        }

        /// <summary>
        /// 验证给定位置是否合法（是否在场地内且不在围栏外）
        /// </summary>
        /// <param name="position">待验证的位置</param>
        /// <param name="offset">偏移量</param>
        /// <param name="withFence">是否考虑当前围栏的限制</param>
        /// <returns>位置是否合法</returns>
        public bool ValidatePosition(Vector2 position, Vector2 offset, bool withFence = true)
        {
            var isFenceValid = true;

            // 如果需要检查围栏且当前存在围栏，则验证位置是否在围栏允许范围内
            if (Fence != null && withFence)
            {
                isFenceValid = Fence.ValidatePosition(position, offset);
            }

            // 必须同时满足场地基础验证和围栏验证
            return instance.field.ValidatePosition(position) && isFenceValid;
        }

        /// <summary>
        /// 获取射线与场地边界（或围栏）的交点
        /// </summary>
        /// <param name="start">射线起点</param>
        /// <param name="end">射线终点</param>
        /// <param name="offset">偏移量</param>
        /// <param name="withFence">是否考虑围栏</param>
        /// <returns>交点坐标</returns>
        public virtual Vector2 GetIntersectionPoint(Vector2 start, Vector2 end, float offset, bool withFence)
        {
            // 优先检查是否与围栏相交
            if (Fence != null && withFence)
            {
                return Fence.GetIntersectionPoint(start, end, offset);
            }

            // 否则计算与场地基础边界的交点
            return instance.field.GetIntersectionPoint(start, end, offset);
        }

        /// <summary>
        /// 获取场地边界上的一个随机位置
        /// </summary>
        public Vector2 GetRandomPositionOnBorder()
        {
            return instance.field.GetRandomPositionOnBorder();
        }

        // 以下方法委托给具体的场地行为实现，用于检测点是否超出特定方向的边界

        public bool IsPointOutsideFieldRight(Vector2 point, out float distance)
        {
            return field.IsPointOutsideRight(point, out distance);
        }

        public bool IsPointOutsideFieldLeft(Vector2 point, out float distance)
        {
            return field.IsPointOutsideLeft(point, out distance);
        }

        public bool IsPointOutsideFieldTop(Vector2 point, out float distance)
        {
            return field.IsPointOutsideTop(point, out distance);
        }

        public bool IsPointOutsideFieldBottom(Vector2 point, out float distance)
        {
            return field.IsPointOutsideBottom(point, out distance);
        }
    }
}