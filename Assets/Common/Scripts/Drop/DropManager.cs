using OctoberStudio.Drop;
using OctoberStudio.Easing;
using OctoberStudio.Extensions;
using OctoberStudio.Pool;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OctoberStudio
{
    /// <summary>
    /// DropManager: 管理游戏中掉落物（如宝石、金币等）的核心控制器。
    /// 负责掉落物的生成、对象池管理、磁力吸附检测（使用 Jobs 系统优化性能）以及拾取逻辑。
    /// </summary>
    public class DropManager : MonoBehaviour
    {
        // 掉落物数据库，包含所有掉落物的配置数据（预制体、冷却时间等）
        [SerializeField] DropDatabase database;

        // 对象池字典：根据掉落类型管理对应的对象池，用于高效复用掉落物实例
        public Dictionary<DropType, PoolComponent<DropBehavior>> dropPools = new Dictionary<DropType, PoolComponent<DropBehavior>>();

        // 记录每种掉落类型上次生成的时间，用于处理生成冷却时间
        public Dictionary<DropType, float> lastTimeDropped = new Dictionary<DropType, float>();

        // 当前场景中所有活跃掉落物的列表
        public List<DropBehavior> dropList = new List<DropBehavior>();

        // 等待加入活跃列表的掉落物（当 Job 正在运行时新生成的掉落物会暂存于此）
        protected List<DropBehavior> waitingDrop = new List<DropBehavior>();

        // NativeList: 存储所有活跃掉落物的二维位置 (x, y)，供 Job 系统并行读取
        protected NativeList<float2> dropPositions;

        // NativeList: 存储对应位置的掉落物是否在玩家磁力范围内，由 Job 系统并行写入
        protected NativeList<bool> isInside;

        // Job 系统的句柄，用于追踪异步任务的执行状态
        protected JobHandle insideMagnetJobHandle;

        // 定义磁力检测的 Job 结构体实例
        protected InsideMagnetJob insideMagnetJob;

        // 标记当前是否有一个磁力检测 Job 正在运行
        protected bool isJobRunning;

        // 缓存的列表容量，用于检测列表扩容后需要重新绑定 Job 数组
        protected int cachedCapacity;

        // 标记是否在 Job 完成后立即拾取所有掉落物（用于技能或特殊效果）
        protected bool pickUpAllWhenFinished = false;

        /// <summary>
        /// 初始化管理器：创建对象池，分配原生内存集合，配置 Job 参数。
        /// </summary>
        public virtual void Init()
        {
            // 遍历数据库中所有宝石配置
            for (int i = 0; i < database.GemsCount; i++)
            {
                var data = database.GetGemData(i);
                // 为每种掉落类型创建一个对象池，初始容量100
                var pool = new PoolComponent<DropBehavior>($"Drop_{data.DropType}", data.Prefab, 100);
                dropPools.Add(data.DropType, pool);
                lastTimeDropped.Add(data.DropType, 0);
            }
            // 分配持久化的原生内存列表，用于多线程/Job 安全访问
            dropPositions = new NativeList<float2>(500, Allocator.Persistent);
            isInside = new NativeList<bool>(500, Allocator.Persistent);
            cachedCapacity = dropPositions.Capacity;

            // 初始化 Job 结构体并绑定原生数组
            insideMagnetJob = new InsideMagnetJob();
            insideMagnetJob.positions = dropPositions.AsDeferredJobArray();
            insideMagnetJob.isInside = isInside.AsDeferredJobArray();
        }

        /// <summary>
        /// Update: 每帧调度磁力检测 Job。
        /// 将掉落物位置与玩家位置进行并行距离计算，判断是否在磁力范围内。
        /// </summary>
        protected virtual void Update()
        {
            if (dropList.Count == 0) return;

            // 设置 Job 所需的玩家位置和磁力半径平方（避免开方运算，提高性能）
            insideMagnetJob.playerPosition = PlayerBehavior.CenterPosition;
            insideMagnetJob.magnetDistanceSqr = PlayerBehavior.Player.MagnetRadiusSqr;

            // 调度并行 Job，批处理大小为64
            insideMagnetJobHandle = insideMagnetJob.Schedule(dropList.Count, 64);
            JobHandle.ScheduleBatchedJobs();
            isJobRunning = true;
        }

        /// <summary>
        /// LateUpdate: 在 Job 完成后处理掉落物的拾取逻辑。
        /// 确保在渲染前完成所有位置更新和对象移除。
        /// </summary>
        protected virtual void LateUpdate()
        {
            // 如果 Job 还没运行完，直接返回（等待下一帧）
            if (!isJobRunning)
            {
                pickUpAllWhenFinished = false;
                MoveWaitingDropToActive();
                return;
            }

            // 标记 Job 结束，并等待其完成以确保数据就绪
            isJobRunning = false;
            insideMagnetJobHandle.Complete();

            if (pickUpAllWhenFinished)
            {
                // 如果标记了“全部拾取”，则无视距离直接拾取所有受磁力影响的掉落物
                PickUpAll();
            }
            else
            {
                // 否则，仅拾取那些被 Job 标记为“在磁力范围内”的掉落物
                var delay = 0f;
                for (int i = 0; i < dropList.Count; i++)
                {
                    if (isInside[i])
                    {
                        // 执行移动动画：飞向玩家中心，使用 BackIn 缓动效果
                        dropList[i].transform.DoPositionJob(PlayerBehavior.CenterTransform, 0.25f, delay, false, EasingType.BackIn)
                            .SetOnFinish(dropList[i].OnPickedUp);

                        // 增加微小的延迟，使掉落物依次飞入，避免重叠
                        delay += 0.002f;

                        // 从活跃列表和原生数组中移除已拾取的掉落物
                        dropList.RemoveAtSwapBack(i);
                        dropPositions.RemoveAtSwapBack(i);
                        isInside.RemoveAtSwapBack(i);
                        i--; // 索引回退，因为移除后后续元素前移
                    }
                }
            }
            // 将等待队列中的掉落物加入活跃列表
            MoveWaitingDropToActive();
        }

        /// <summary>
        /// 将等待队列中的掉落物移动到活跃列表中。
        /// 通常在 Job 未运行时调用，避免在 Job 读取数据时修改列表。
        /// </summary>
        protected virtual void MoveWaitingDropToActive()
        {
            if (waitingDrop != null && waitingDrop.Count > 0)
            {
                for (int i = 0; i < waitingDrop.Count; i++)
                {
                    AddDropToList(waitingDrop[i]);
                }
                waitingDrop.Clear();
            }
        }

        /// <summary>
        /// 公开方法：请求拾取所有掉落物。
        /// 如果 Job 正在运行，则设置标志位等待 Job 完成后执行；否则立即执行。
        /// </summary>
        public virtual void PickUpAllDrop()
        {
            if (isJobRunning)
            {
                pickUpAllWhenFinished = true;
            }
            else
            {
                PickUpAll();
            }
        }

        /// <summary>
        /// 执行全部拾取逻辑：将所有受磁力影响的掉落物飞向玩家。
        /// </summary>
        protected virtual void PickUpAll()
        {
            var delay = 0f;
            for (int i = 0; i < dropList.Count; i++)
            {
                // 仅处理配置中允许被磁力吸引的掉落物
                if (dropList[i].DropData.AffectedByMagnet)
                {
                    dropList[i].transform.DoPositionJob(PlayerBehavior.CenterTransform, 0.25f, delay, false, EasingType.BackIn)
                        .SetOnFinish(dropList[i].OnPickedUp);

                    delay += 0.001f;

                    dropList.RemoveAtSwapBack(i);
                    dropPositions.RemoveAtSwapBack(i);
                    isInside.RemoveAtSwapBack(i);
                    i--;
                }
            }
        }

        /// <summary>
        /// 检查指定类型的掉落物是否处于冷却时间内。
        /// </summary>
        public virtual bool CheckDropCooldown(DropType dropType)
        {
            return Time.time - lastTimeDropped[dropType] >= database.GetGemData(dropType).DropCooldown;
        }

        /// <summary>
        /// 生成一个掉落物。
        /// 从对象池获取实例，初始化数据，设置位置，并根据 Job 状态决定是立即加入列表还是放入等待队列。
        /// </summary>
        public virtual void Drop(DropType dropType, Vector3 position)
        {
            var drop = dropPools[dropType].GetEntity();
            drop.Init(database.GetGemData(dropType));
            drop.transform.position = position;
            lastTimeDropped[dropType] = Time.time;

            if (!isJobRunning)
            {
                // Job 未运行时，直接加入活跃列表
                AddDropToList(drop);
            }
            else
            {
                // Job 运行时，数据可能被读取，先放入等待队列以防并发冲突
                waitingDrop.Add(drop);
            }
        }

        /// <summary>
        /// 将单个掉落物添加到活跃列表及对应的原生数组中。
        /// 如果底层数组容量发生变化，需重新绑定 Job 的数组引用。
        /// </summary>
        protected virtual void AddDropToList(DropBehavior drop)
        {
            dropList.Add(drop);
            dropPositions.Add(drop.transform.position.XY()); // 只取 XZ 平面作为 2D 坐标
            isInside.Add(false);

            // 检测容量变化，若扩容则需更新 Job 中的数组引用
            if (cachedCapacity != isInside.Capacity)
            {
                insideMagnetJob.positions = dropPositions.AsDeferredJobArray();
                insideMagnetJob.isInside = isInside.AsDeferredJobArray();
                cachedCapacity = isInside.Capacity; // 更新缓存容量（原代码逻辑隐含需要更新此处以防多次判断，虽原代码未显式写，但逻辑上应同步）
            }
        }

        /// <summary>
        /// 销毁时释放原生内存，防止内存泄漏。
        /// </summary>
        protected virtual void OnDestroy()
        {
            dropPositions.Dispose();
            isInside.Dispose();
        }

        /// <summary>
        /// InsideMagnetJob: 使用 Burst 编译的并行 Job。
        /// 功能：计算每个掉落物与玩家的距离平方，判断是否在磁力半径内。
        /// </summary>
        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        protected struct InsideMagnetJob : IJobParallelFor
        {
            // 只读：所有掉落物的位置数组
            [ReadOnly] public NativeArray<float2> positions;

            // 只读：玩家中心位置
            [ReadOnly] public float2 playerPosition;

            // 只读：磁力半径的平方值
            [ReadOnly] public float magnetDistanceSqr;

            // 只写：输出结果，标记该索引的掉落物是否在范围内
            [WriteOnly] public NativeArray<bool> isInside;

            public void Execute(int index)
            {
                // 计算距离平方并与阈值比较，避免昂贵的开方运算
                isInside[index] = math.distancesq(positions[index], playerPosition) <= magnetDistanceSqr;
            }
        }
    }
}