using OctoberStudio.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

namespace OctoberStudio.Easing
{
    /// <summary>
    /// EasingManager: 全局缓动管理器单例。
    /// 负责管理基于协程的缓动动画和基于 Job 系统的高性能位置插值动画。
    /// 提供静态方法以便在任何地方轻松启动缓动效果。
    /// </summary>
    public class EasingManager : MonoBehaviour
    {
        private static EasingManager instance;

        // 负责运行高性能位置插值 Job 的处理器
        protected EasingPositionJobRunner positionJobRunner;

        // 静态访问点，供外部调用 Job 运行器
        public static EasingPositionJobRunner PositionJobRunner => instance.positionJobRunner;

        /// <summary>
        /// Awake: 初始化单例实例和 Job 运行器。
        /// </summary>
        public virtual void Awake()
        {
            instance = this;
            positionJobRunner = new EasingPositionJobRunner();
        }

        /// <summary>
        /// OnDestroy: 清理资源，释放 NativeList 内存。
        /// </summary>
        protected virtual void OnDestroy()
        {
            positionJobRunner.Clear();
        }

        /// <summary>
        /// Update: 更新 Job 运行器，调度新的位置计算任务。
        /// 在此阶段准备数据并提交 Job。
        /// </summary>
        protected virtual void Update()
        {
            positionJobRunner.Update();
        }

        /// <summary>
        /// LateUpdate: 完成 Job 计算并将结果应用到 Transform。
        /// 确保在渲染前完成所有位置更新，避免画面抖动。
        /// </summary>
        protected virtual void LateUpdate()
        {
            positionJobRunner.LateUpdate();
        }

        /// <summary>
        /// DoFloat: 创建一个浮点数值的缓动协程。
        /// 从 from 变化到 to，持续 duration 秒，每帧回调 action。
        /// </summary>
        public static IEasingCoroutine DoFloat(float from, float to, float duration, UnityAction<float> action, float delay = 0)
        {
            return new FloatEasingCoroutine(from, to, duration, delay, action);
        }

        /// <summary>
        /// DoAfter: 创建一个延迟执行动作的协程。
        /// </summary>
        /// <param name="seconds">延迟秒数</param>
        /// <param name="action">回调动作</param>
        /// <param name="unscaledTime">是否忽略时间缩放（如暂停游戏时仍计时）</param>
        public static IEasingCoroutine DoAfter(float seconds, UnityAction action, bool unscaledTime = false)
        {
            return new WaitCoroutine(seconds, unscaledTime).SetOnFinish(action);
        }

        /// <summary>
        /// DoAfter: 等待直到指定条件满足。
        /// </summary>
        public static IEasingCoroutine DoAfter(Func<bool> condition)
        {
            return new WaitForConditionCoroutine(condition);
        }

        /// <summary>
        /// DoNextFrame: 等待下一帧执行。
        /// </summary>
        public static IEasingCoroutine DoNextFrame()
        {
            return new NextFrameCoroutine();
        }

        /// <summary>
        /// DoNextFrame: 等待下一帧后执行指定动作。
        /// </summary>
        public static IEasingCoroutine DoNextFrame(UnityAction action)
        {
            return new NextFrameCoroutine().SetOnFinish(action);
        }

        /// <summary>
        /// DoNextFixedFrame: 等待下一个物理帧 (FixedUpdate)。
        /// </summary>
        public static IEasingCoroutine DoNextFixedFrame()
        {
            return new NextFixedFrameCoroutine();
        }

        /// <summary>
        /// StartCustomCoroutine: 启动一个标准的 Unity 协程。
        /// </summary>
        public static Coroutine StartCustomCoroutine(IEnumerator coroutine)
        {
            return instance.StartCoroutine(coroutine);
        }

        /// <summary>
        /// StopCustomCoroutine: 停止一个标准协程。
        /// </summary>
        public static void StopCustomCoroutine(Coroutine coroutine)
        {
            if (instance != null) instance.StopCoroutine(coroutine);
        }
    }

    /// <summary>
    /// IEasingCoroutine: 缓动协程的统一接口。
    /// 支持链式调用配置（如设置缓动类型、回调、延迟等）。
    /// </summary>
    public interface IEasingCoroutine
    {
        bool IsActive { get; }
        IEasingCoroutine SetEasing(EasingType easingType);
        IEasingCoroutine SetEasingCurve(AnimationCurve easingCurve);
        IEasingCoroutine SetOnFinish(UnityAction callback);
        IEasingCoroutine SetUnscaledTime(bool unscaledTime);
        IEasingCoroutine SetDelay(float delay);
        void Stop();
    }

    /// <summary>
    /// EmptyCoroutine: 协程基类，实现通用配置逻辑。
    /// </summary>
    public abstract class EmptyCoroutine : IEasingCoroutine
    {
        protected Coroutine coroutine;

        public bool IsActive { get; protected set; }
        protected UnityAction finishCallback;
        protected EasingType easingType = EasingType.Linear;
        protected float delay = -1;
        protected bool unscaledTime;
        protected bool useCurve;
        protected AnimationCurve easingCurve;

        public IEasingCoroutine SetEasing(EasingType easingType)
        {
            this.easingType = easingType;
            useCurve = false;
            return this;
        }

        public IEasingCoroutine SetOnFinish(UnityAction callback)
        {
            finishCallback = callback;
            return this;
        }

        public IEasingCoroutine SetUnscaledTime(bool unscaledTime)
        {
            this.unscaledTime = unscaledTime;
            return this;
        }

        public IEasingCoroutine SetEasingCurve(AnimationCurve curve)
        {
            easingCurve = curve;
            useCurve = true;
            return this;
        }

        public IEasingCoroutine SetDelay(float delay)
        {
            this.delay = delay;
            return this;
        }

        public void Stop()
        {
            EasingManager.StopCustomCoroutine(coroutine);
            IsActive = false;
        }
    }

    /// <summary>
    /// NextFrameCoroutine: 仅等待一帧即完成的协程。
    /// </summary>
    public class NextFrameCoroutine : EmptyCoroutine
    {
        public NextFrameCoroutine()
        {
            coroutine = EasingManager.StartCustomCoroutine(Coroutine());
        }

        private IEnumerator Coroutine()
        {
            IsActive = true;
            yield return null; // 等待一帧
            finishCallback?.Invoke();
            IsActive = false;
        }
    }

    /// <summary>
    /// NextFixedFrameCoroutine: 等待下一个物理更新帧完成的协程。
    /// </summary>
    public class NextFixedFrameCoroutine : EmptyCoroutine
    {
        public NextFixedFrameCoroutine()
        {
            coroutine = EasingManager.StartCustomCoroutine(Coroutine());
        }

        private IEnumerator Coroutine()
        {
            IsActive = true;
            yield return new WaitForFixedUpdate();
            finishCallback?.Invoke();
            IsActive = false;
        }
    }

    /// <summary>
    /// WaitCoroutine: 等待指定时间的协程，支持延迟和不忽略时间缩放。
    /// </summary>
    public class WaitCoroutine : EmptyCoroutine
    {
        protected float duration;

        public WaitCoroutine(float duration, bool unscaledTime = false)
        {
            this.duration = duration;
            this.unscaledTime = unscaledTime;
            coroutine = EasingManager.StartCustomCoroutine(Coroutine());
        }

        private IEnumerator Coroutine()
        {
            IsActive = true;
            // 先处理额外的延迟（如果有通过 SetDelay 设置）
            while (delay > 0)
            {
                yield return null;
                delay -= unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            }
            // 等待主持续时间
            if (unscaledTime)
            {
                yield return new WaitForSecondsRealtime(duration);
            }
            else
            {
                yield return new WaitForSeconds(duration);
            }
            finishCallback?.Invoke();
            IsActive = false;
        }
    }

    /// <summary>
    /// WaitForConditionCoroutine: 循环等待直到条件函数返回 true。
    /// </summary>
    public class WaitForConditionCoroutine : EmptyCoroutine
    {
        private Func<bool> condition;

        public WaitForConditionCoroutine(Func<bool> condition)
        {
            this.condition = condition;
            coroutine = EasingManager.StartCustomCoroutine(Coroutine());
        }

        private IEnumerator Coroutine()
        {
            IsActive = true;
            // 先处理延迟
            while (delay > 0)
            {
                yield return null;
                delay -= unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            }
            // 循环检查条件
            do
            {
                yield return null;
            } while (!condition());

            finishCallback?.Invoke();
            IsActive = false;
        }
    }

    /// <summary>
    /// EasingCoroutine<T>: 泛型缓动协程基类。
    /// 处理从起始值到目标值的插值逻辑，支持自定义缓动函数和曲线。
    /// </summary>
    public abstract class EasingCoroutine<T> : EmptyCoroutine
    {
        protected T from;
        protected T to;
        protected float duration;
        protected UnityAction<T> callback;

        // 线性插值抽象方法，由子类具体实现（如 Vector3.Lerp, float.Lerp 等）
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract T Lerp(T a, T b, float t);

        public EasingCoroutine(T from, T to, float duration, float delay, UnityAction<T> callback)
        {
            this.from = from;
            this.to = to;
            this.duration = duration;
            this.callback = callback;
            this.delay = delay;
            coroutine = EasingManager.StartCustomCoroutine(Coroutine());
        }

        private IEnumerator Coroutine()
        {
            IsActive = true;
            float time = 0;

            // 主循环：直到达到持续时间
            while (time < duration)
            {
                yield return null;

                // 处理延迟阶段
                if (delay > 0)
                {
                    delay -= unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    if (delay > 0) continue; // 延迟未结束，跳过本帧计算
                }

                // 累加时间
                time += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

                float t;
                // 计算归一化进度 (0~1)，应用缓动函数或曲线
                if (useCurve)
                {
                    t = easingCurve.Evaluate(time / duration);
                }
                else
                {
                    t = EasingFunctions.ApplyEasing(time / duration, easingType);
                }

                // 计算当前帧的值并回调
                T value = Lerp(from, to, t);
                callback?.Invoke(value);
            }

            // 确保最后一帧精确等于目标值
            callback.Invoke(to);
            finishCallback?.Invoke();
            IsActive = false;
        }
    }

    /// <summary>
    /// FloatEasingCoroutine: 专门用于 float 类型的缓动协程。
    /// </summary>
    public class FloatEasingCoroutine : EasingCoroutine<float>
    {
        public FloatEasingCoroutine(float from, float to, float duration, float delay, UnityAction<float> callback) : base(from, to, duration, delay, callback)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override float Lerp(float a, float b, float t)
        {
            return Mathf.LerpUnclamped(a, b, t);
        }
    }

    /// <summary>
    /// VectorEasingCoroutine3: 专门用于 Vector3 类型的缓动协程。
    /// </summary>
    public class VectorEasingCoroutine3 : EasingCoroutine<Vector3>
    {
        public VectorEasingCoroutine3(Vector3 from, Vector3 to, float duration, float delay, UnityAction<Vector3> callback) : base(from, to, duration, delay, callback)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            return Vector3.LerpUnclamped(a, b, t);
        }
    }

    /// <summary>
    /// VectorEasingCoroutine2: 专门用于 Vector2 类型的缓动协程。
    /// </summary>
    public class VectorEasingCoroutine2 : EasingCoroutine<Vector2>
    {
        public VectorEasingCoroutine2(Vector2 from, Vector2 to, float duration, float delay, UnityAction<Vector2> callback) : base(from, to, duration, delay, callback)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Vector2 Lerp(Vector2 a, Vector2 b, float t)
        {
            return Vector2.LerpUnclamped(a, b, t);
        }
    }

    /// <summary>
    /// ColorEasingCoroutine: 专门用于 Color 类型的缓动协程。
    /// </summary>
    public class ColorEasingCoroutine : EasingCoroutine<Color>
    {
        public ColorEasingCoroutine(Color from, Color to, float duration, float delay, UnityAction<Color> callback) : base(from, to, duration, delay, callback)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Color Lerp(Color a, Color b, float t)
        {
            return Color.LerpUnclamped(a, b, t);
        }
    }

    /// <summary>
    /// EasingJobAnimation: 基于 Job 系统的动画基类。
    /// 存储动画的时间信息、缓动类型和回调，不直接操作 Transform，而是由 Job 计算数据。
    /// </summary>
    public abstract class EasingJobAnimation
    {
        protected float startTime;
        public float StartTime => startTime;
        protected float endTime;
        public float EndTime => endTime;
        protected bool useUnscaledTime;
        public bool UseUnscaledTime => useUnscaledTime;
        protected EasingType easingType;
        public EasingType EasingType => easingType;
        protected UnityAction finishCallback;

        protected EasingJobAnimation(float duration, float delay, bool useUnscaledTime, EasingType easingType)
        {
            this.useUnscaledTime = useUnscaledTime;
            var time = useUnscaledTime ? Time.unscaledTime : Time.time;
            startTime = time + delay;
            endTime = startTime + duration;
            this.easingType = easingType;
        }

        // 检查动画是否仍在进行中
        public bool IsActive => useUnscaledTime ? Time.unscaledTime < endTime : Time.time < endTime;

        // 检查动画是否已经开始（过了延迟时间）
        public bool IsStarted => useUnscaledTime ? startTime <= Time.unscaledTime : startTime <= Time.time;

        public virtual void SetOnFinish(UnityAction finishCallback)
        {
            this.finishCallback = finishCallback;
        }

        public virtual void Finish()
        {
            finishCallback?.Invoke();
        }
    }

    /// <summary>
    /// PositionEasingJobAnimation: 专门用于 Transform 位置移动的 Job 动画。
    /// 持有源 Transform 和目标 Transform 的引用，并将数据注册到 JobRunner 中。
    /// </summary>
    public class PositionEasingJobAnimation : EasingJobAnimation
    {
        protected Transform transform;
        protected Transform targetTransform;

        // 使用 float2 (x, y) 进行计算以提高 Job 效率，忽略 Z 轴或假设 2D 环境
        public float2 Position { get => transform.position.XY(); set => transform.position = (Vector2)value; }
        public float2 Target => targetTransform.position.XY();

        public PositionEasingJobAnimation(Transform transform, Transform targetTransform, float duration, float delay, bool useUnscaledTime, EasingType easingType) : base(duration, delay, useUnscaledTime, easingType)
        {
            this.transform = transform;
            this.targetTransform = targetTransform;
            IsValid = true;
            // 注册到全局运行器
            EasingManager.PositionJobRunner.AddJobAnimaiton(this);
        }

        public bool IsValid { get; protected set; }
    }

    /// <summary>
    /// EasingPositionJobRunner: 高性能位置缓动作业运行器。
    /// 使用 Unity Jobs System 和 Burst Compiler 并行处理大量物体的位置插值。
    /// 流程：Update 收集数据 -> Schedule Job -> LateUpdate 等待完成并应用结果。
    /// </summary>
    public class EasingPositionJobRunner
    {
        // 等待开始（处于延迟阶段）的动画列表
        protected List<PositionEasingJobAnimation> waitingAnimations;
        // 正在运行的动画列表
        protected List<PositionEasingJobAnimation> activeAnimations;

        // NativeList: 用于 Job 系统安全访问的内存集合
        [ReadOnly] public NativeList<float2> timeData;       // x: startTime, y: endTime
        [ReadOnly] public NativeList<float> useUnscaledTime; // 标记是否使用真实时间 (1.0f 或 0.0f)
        [ReadOnly] public NativeList<FunctionPointer<EasingFunctions.EasingFunction>> easingFunctions; // 缓动函数指针
        [ReadOnly] public NativeList<float2> startPositions; // 起始位置
        [ReadOnly] public NativeList<float2> targets;        // 目标位置（可能动态变化）
        [WriteOnly] public NativeList<float2> positions;     // 输出：计算后的新位置

        public bool isJobRunning = false;
        protected DoPosition2DJob doPosition2DJob;
        protected JobHandle doPosition2DJobHandle;
        protected int capacityCache; // 缓存容量以检测数组扩容

        public EasingPositionJobRunner()
        {
            waitingAnimations = new List<PositionEasingJobAnimation>(10);
            activeAnimations = new List<PositionEasingJobAnimation>(50);

            // 分配持久化内存，避免每帧分配 GC
            timeData = new NativeList<float2>(50, Allocator.Persistent);
            useUnscaledTime = new NativeList<float>(50, Allocator.Persistent);
            easingFunctions = new NativeList<FunctionPointer<EasingFunctions.EasingFunction>>(50, Allocator.Persistent);
            startPositions = new NativeList<float2>(50, Allocator.Persistent);
            targets = new NativeList<float2>(50, Allocator.Persistent);
            positions = new NativeList<float2>(50, Allocator.Persistent);

            doPosition2DJob = new DoPosition2DJob();
            capacityCache = timeData.Capacity;
            ReinitializeJob();
        }

        /// <summary>
        /// AddJobAnimaiton: 添加一个新的位置动画任务。
        /// 如果动画已开始且当前没有 Job 在运行，直接加入活跃列表；否则放入等待队列。
        /// </summary>
        public virtual void AddJobAnimaiton(PositionEasingJobAnimation jobAnimation)
        {
            if (jobAnimation.IsStarted && !isJobRunning)
            {
                AddActiveAnimation(jobAnimation);
            }
            else
            {
                waitingAnimations.Add(jobAnimation);
            }
        }

        /// <summary>
        /// Update: 每帧调用。
        /// 1. 将等待队列中已到期的动画移入活跃列表。
        /// 2. 更新活跃动画的目标位置（支持动态目标）。
        /// 3. 调度 Job 进行并行计算。
        /// </summary>
        public virtual void Update()
        {
            // 处理等待队列
            if (waitingAnimations.Count > 0)
            {
                for (int i = 0; i < waitingAnimations.Count; i++)
                {
                    if (waitingAnimations[i].IsStarted)
                    {
                        AddActiveAnimation(waitingAnimations[i]);
                        waitingAnimations.RemoveAt(i);
                        i--;
                    }
                }
            }

            if (activeAnimations.Count == 0) return;

            // 更新目标位置并检查有效性
            for (int i = 0; i < activeAnimations.Count; i++)
            {
                var animation = activeAnimations[i];
                if (animation.IsValid)
                {
                    targets[i] = activeAnimations[i].Target;
                }
                else
                {
                    RemoveActiveAnimation(i);
                    i--;
                }
            }

            // 准备 Job 数据
            doPosition2DJob.scaledTime = Time.time;
            doPosition2DJob.unscaledTime = Time.unscaledTime;

            // 调度 Job，批处理大小为 16
            doPosition2DJobHandle = doPosition2DJob.Schedule(activeAnimations.Count, 16);
            JobHandle.ScheduleBatchedJobs();
            isJobRunning = true;
        }

        /// <summary>
        /// ReinitializeJob: 当底层 NativeList 容量发生变化时，重新绑定 Job 的数组引用。
        /// </summary>
        protected virtual void ReinitializeJob()
        {
            doPosition2DJob.timeData = timeData.AsDeferredJobArray();
            doPosition2DJob.useUnscaledTime = useUnscaledTime.AsDeferredJobArray();
            doPosition2DJob.easingFunctions = easingFunctions.AsDeferredJobArray();
            doPosition2DJob.startPositions = startPositions.AsDeferredJobArray();
            doPosition2DJob.targets = targets.AsDeferredJobArray();
            doPosition2DJob.positions = positions.AsDeferredJobArray();
        }

        /// <summary>
        /// AddActiveAnimation: 将动画添加到活跃列表，并同步数据到 NativeList 供 Job 读取。
        /// </summary>
        protected virtual void AddActiveAnimation(PositionEasingJobAnimation jobAnimation)
        {
            activeAnimations.Add(jobAnimation);
            timeData.Add(new float2(jobAnimation.StartTime, jobAnimation.EndTime));
            useUnscaledTime.Add(jobAnimation.UseUnscaledTime ? 1f : 0f);
            easingFunctions.Add(EasingFunctions.Functions[(int)jobAnimation.EasingType]);
            startPositions.Add(jobAnimation.Position);
            targets.Add(jobAnimation.Target);
            positions.Add(float2.zero);

            // 如果容量改变，需要重新绑定 Job 数组
            if (timeData.Capacity != capacityCache)
            {
                capacityCache = timeData.Capacity;
                ReinitializeJob();
            }
        }

        /// <summary>
        /// LateUpdate: 等待 Job 完成，并将计算结果应用回 Transform。
        /// 移除已结束的动画并触发完成回调。
        /// </summary>
        public virtual void LateUpdate()
        {
            if (!isJobRunning) return;

            isJobRunning = false;
            doPosition2DJobHandle.Complete(); // 阻塞直到 Job 完成

            for (int i = 0; i < activeAnimations.Count; i++)
            {
                var animation = activeAnimations[i];
                var remove = false;

                if (animation.IsValid)
                {
                    // 应用 Job 计算出的新位置
                    activeAnimations[i].Position = positions[i];

                    // 检查是否结束
                    if (!animation.IsActive)
                    {
                        animation.Finish();
                        remove = true;
                    }
                }
                else
                {
                    remove = true;
                }

                if (remove)
                {
                    RemoveActiveAnimation(i);
                    i--;
                }
            }
        }

        /// <summary>
        /// RemoveActiveAnimation: 从所有列表中移除指定索引的动画数据。
        /// 保持多个列表的索引同步。
        /// </summary>
        protected virtual void RemoveActiveAnimation(int index)
        {
            activeAnimations.RemoveAt(index);
            timeData.RemoveAt(index);
            useUnscaledTime.RemoveAt(index);
            easingFunctions.RemoveAt(index);
            startPositions.RemoveAt(index);
            targets.RemoveAt(index);
            positions.RemoveAt(index);
        }

        /// <summary>
        /// Clear: 销毁时释放所有 NativeList 内存。
        /// </summary>
        public virtual void Clear()
        {
            if (isJobRunning) doPosition2DJobHandle.Complete();
            timeData.Dispose();
            useUnscaledTime.Dispose();
            easingFunctions.Dispose();
            startPositions.Dispose();
            targets.Dispose();
            positions.Dispose();
        }

        /// <summary>
        /// DoPosition2DJob: 核心计算 Job。
        /// 使用 Burst 编译，并行计算每个动画在当前帧的位置。
        /// 公式：Pos = Start + (Target - Start) * Easing(t)
        /// </summary>
        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public struct DoPosition2DJob : IJobParallelFor
        {
            // x - startTime, y = endTime
            [ReadOnly] public NativeArray<float2> timeData;
            [ReadOnly] public NativeArray<float> useUnscaledTime;
            // 函数指针数组，允许在 Job 中调用不同的缓动函数
            [ReadOnly] public NativeArray<FunctionPointer<EasingFunctions.EasingFunction>> easingFunctions;
            [ReadOnly] public NativeArray<float2> startPositions;
            [ReadOnly] public NativeArray<float2> targets;
            [WriteOnly] public NativeArray<float2> positions;
            [ReadOnly] public float scaledTime;
            [ReadOnly] public float unscaledTime;

            public void Execute(int i)
            {
                // 根据标记选择使用游戏时间还是真实时间
                var time = math.select(unscaledTime, scaledTime, useUnscaledTime[i] == 0f);

                // 计算归一化进度 t (0~1)，unlerp 处理超出范围的情况
                var t = math.unlerp(timeData[i].x, timeData[i].y, time);

                // 调用对应的缓动函数处理 t 值
                t = easingFunctions[i].Invoke(math.saturate(t));

                // 线性插值计算最终位置
                positions[i] = startPositions[i] + (targets[i] - startPositions[i]) * t;
            }
        }
    }
}