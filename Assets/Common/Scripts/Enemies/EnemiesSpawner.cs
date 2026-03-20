using OctoberStudio.Bossfight;
using OctoberStudio.Extensions;
using OctoberStudio.Pool;
using OctoberStudio.Timeline;
using OctoberStudio.UI;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

namespace OctoberStudio
{
    // 设置脚本执行顺序为 -10，确保在其他相关脚本之前初始化
    [DefaultExecutionOrder(-10)]
    public class EnemiesSpawner : MonoBehaviour
    {
        // 存储当前场景中所有活跃敌人的列表
        protected List<EnemyBehavior> enemies = new List<EnemyBehavior>();

        [SerializeField] protected EnemiesDatabase database; // 敌人数据库，包含所有敌人的配置数据
        [SerializeField] protected BossfightDatabase bossfightDatabase; // Boss战数据库，包含Boss的配置数据

        [Space]
        [SerializeField] protected ScalingLabelBehavior enemiesDiedLabel; // 用于显示已击杀敌人数量的UI标签

        [Space]
        // [原有注释] 同时存在的最大敌人数量。在现有敌人被击败前，不会生成更多敌人
        // [新注释] 这是一个硬限制，防止场景中敌人过多导致性能下降或游戏失衡
        [Tooltip("Maximum amount of alive enemies at a time. No more enemies will be spawned until some of existing aren't defeated")]
        [SerializeField] protected int enemiesCap = 2000;

        [Header("Offscreen Teleport")] // 屏幕外传送设置
        // [原有注释] 启用时，位于玩家身后的敌人将传送到前方
        // [新注释] 此功能用于防止敌人卡在玩家视野之外，保持游戏节奏紧凑
        [Tooltip("When enabled, enemies that are behaind of the player will teleport to the front")]
        [SerializeField] protected bool isOffscreenTeleportEnabled = true;

        // [原有注释] 玩家到敌人的距离，其中 1 代表相机对角线长度
        // [新注释] 乘以该系数后决定传送的目标距离，确保敌人出现在屏幕边缘附近而非太远或太近
        [Tooltip("Distance from the player to enemy, where 1 is a camera diagonal length")]
        [SerializeField] protected float diagonalDistanceMultiplier = 1.3f;

        [SerializeField, Range(0, 1f)] protected float teleportConeSize = 0.8f; // 传送锥体大小，控制判定敌人是否在玩家身后的角度范围

        // [原有注释] 如果敌人数量超过此值，敌人将停止传送
        // [新注释] 当屏幕上敌人过多时，关闭传送逻辑以节省性能开销
        [Tooltip("Enemy will stop teleporting if there are more than this amount of enemies")]
        [SerializeField] protected int enemiesTeleportCap = 100;

        protected int enemiesDiedCounter; // 已击杀敌人计数器
        protected Dictionary<EnemyType, PoolComponent<EnemyBehavior>> enemyPools; // 敌人对象池字典，按敌人类型分类
        protected Dictionary<EnemyType, EnemyData> enemyDataDictionary; // 敌人数据字典，用于快速查找配置
        protected StageSave stageSave; // 关卡存档数据

        public bool IsBossfightActive { get; set; } // 标记当前是否正在进行Boss战

        protected Camera mainCamera; // 主相机引用

        protected virtual void Awake()
        {
            mainCamera = Camera.main; // 获取主相机实例
        }

        // [原有注释] 我们只为阶段时间轴中存在的敌人创建对象池
        // [新注释] 初始化方法，分析时间轴中的波次配置，预加载所需敌人类型的对象池，避免运行时动态实例化带来的卡顿
        public virtual void Init(PlayableDirector director)
        {
            enemyDataDictionary = database.GetEnemyDataDictionary(); // 从数据库获取所有敌人数据
            stageSave = GameController.SaveManager.GetSave<StageSave>("Stage"); // 获取当前关卡的存档数据

            // 统计关卡中每种敌人类型的最大需求量
            Dictionary<EnemyType, int> enemiesOnLevel = new Dictionary<EnemyType, int>();

            var waves = director.GetAssets<WaveTrack, WaveAsset>(); // 获取所有波次资产
            for (int i = 0; i < waves.Count; i++)
            {
                var wave = waves[i];
                var enemyType = wave.EnemyType;
                var enemiesCount = wave.EnemiesCount;

                // 如果字典中已存在该类型，则更新为更大的数量需求
                if (enemiesOnLevel.ContainsKey(enemyType))
                {
                    if (enemiesOnLevel[enemyType] < enemiesCount)
                    {
                        enemiesOnLevel[enemyType] = enemiesCount;
                    }
                }
                else
                {
                    enemiesOnLevel.Add(enemyType, enemiesCount);
                }
            }

            // 收集时间轴轨道中涉及的所有敌人类型
            var trackEnemies = new List<EnemyType>();
            foreach (var output in director.playableAsset.outputs)
            {
                if (output.sourceObject is WaveTrack waveTrack)
                {
                    if (!trackEnemies.Contains(waveTrack.EnemyType))
                    {
                        trackEnemies.Add(waveTrack.EnemyType);
                    }
                }
            }

            enemyPools = new Dictionary<EnemyType, PoolComponent<EnemyBehavior>>();

            // 为统计出的敌人类型创建对象池
            foreach (var enemyType in enemiesOnLevel.Keys)
            {
                var data = database.GetEnemyData(enemyType);
                var amount = enemiesOnLevel[enemyType];

                // 限制单个池子的初始容量，避免内存浪费
                if (amount > 100) amount = 100;
                if (amount < 0) amount = 1;

                var pool = new PoolComponent<EnemyBehavior>($"Enemy {enemyType}", data.Prefab, amount);
                enemyPools.Add(data.Type, pool);
            }

            // 确保轨道中提到的所有敌人类型都有对应的池子（即使数量为1）
            foreach (var enemyType in trackEnemies)
            {
                if (!enemyPools.ContainsKey(enemyType))
                {
                    var data = database.GetEnemyData(enemyType);
                    var pool = new PoolComponent<EnemyBehavior>($"Enemy {enemyType}", data.Prefab, 1);
                    enemyPools.Add(data.Type, pool);
                }
            }

            // 初始化击杀计数器
            enemiesDiedCounter = 0;
            if (!stageSave.ResetStageData)
            {
                // 如果不是重置关卡，则读取之前的击杀数（用于断点续玩等场景）
                enemiesDiedCounter = stageSave.EnemiesKilled;
            }

            enemiesDiedLabel.SetAmount(enemiesDiedCounter); // 更新UI显示
        }

        protected virtual void Update()
        {
            // 如果禁用传送、正在打Boss或敌人数量超过上限，则跳过传送逻辑
            if (!isOffscreenTeleportEnabled || IsBossfightActive || enemies.Count > enemiesTeleportCap) return;

            // 计算相机对角线长度的平方，用于距离判断
            var diagonalSqr = (CameraManager.HalfWidth * CameraManager.HalfWidth + CameraManager.HalfHeight * CameraManager.HalfHeight) * diagonalDistanceMultiplier;
            var diagonal = Mathf.Sqrt(diagonalSqr);
            var dotValue = teleportConeSize - 1; // 计算点积阈值，用于判断角度

            // 动态调整检查频率：敌人越多，每帧检查的比例越小，以优化性能
            var modValue = Mathf.Clamp(enemies.Count / 20, 1, 100);
            int frame = Time.frameCount % modValue;

            // 分批处理敌人，避免单帧计算量过大
            for (int i = frame; i < enemies.Count; i += modValue)
            {
                var enemy = enemies[i];
                // 如果该敌人所在的波次配置禁用了屏幕外传送，则跳过
                if (enemy.WaveOverride != null && enemy.WaveOverride.DisableOffscreenTeleport) continue;

                var enemyToPlayer = enemy.transform.position - PlayerBehavior.Player.transform.position;
                var direction = enemyToPlayer.normalized;
                // 计算敌人方向与玩家朝向的点积
                var dot = Vector2.Dot(direction, PlayerBehavior.Player.LookDirection);

                // 如果敌人距离玩家超过设定距离 且 位于玩家身后（点积小于阈值），则执行传送
                if (diagonalSqr < enemyToPlayer.sqrMagnitude && dot < dotValue)
                {
                    // 计算传送目标位置：玩家前方随机角度处
                    var teleportPosition = PlayerBehavior.Player.transform.position + Quaternion.Euler(0, 0, Random.Range(-45, 45)) * PlayerBehavior.Player.LookDirection * diagonal;
                    enemy.transform.position = teleportPosition;
                }
            }
        }

        // 获取距离指定点最近的敌人
        public virtual EnemyBehavior GetClosestEnemy(Vector2 point)
        {
            EnemyBehavior closestEnemy = null;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < enemies.Count; i++)
            {
                // 清理列表中可能存在的空引用（已被销毁但未移除的敌人）
                if (enemies[i] == null)
                {
                    enemies.RemoveAt(i);
                    i--;
                    continue;
                }

                float distance = (point - enemies[i].transform.position.XY()).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestEnemy = enemies[i];
                    closestDistance = distance;
                }
            }
            return closestEnemy;
        }

        // 在指定位置生成单个敌人
        public virtual EnemyBehavior Spawn(EnemyType enemyType, Vector2 position, UnityAction<EnemyBehavior> onEnemyDiedCallback = null)
        {
            if (enemies.Count >= enemiesCap) return null; // 达到上限则不生成

            var enemyData = enemyDataDictionary[enemyType];
            // 如果池中不存在该类型，动态创建一个小型池
            if (!enemyPools.ContainsKey(enemyType))
            {
                var pool = new PoolComponent<EnemyBehavior>($"Enemy {enemyType}", enemyData.Prefab, 10);
                enemyPools.Add(enemyData.Type, pool);
            }

            var enemy = enemyPools[enemyType].GetEntity(); // 从池中获取对象
            enemy.SetData(enemyData); // 设置数据
            enemy.transform.position = position; // 设置位置
            enemy.onEnemyDied += OnEnemyDied; // 注册死亡事件
            if (onEnemyDiedCallback != null) enemy.onEnemyDied += onEnemyDiedCallback; // 注册额外回调

            enemy.Play(); // 激活敌人行为
            enemies.Add(enemy); // 加入列表

            return enemy;
        }

        // 批量生成敌人（支持圆形分布或屏幕外分布）
        public virtual void Spawn(EnemyType type, WaveOverride waveOverride, bool circularSpawn = false, int amount = 1, UnityAction<EnemyBehavior> onEnemyDiedCallback = null)
        {
            var cameraHeight = mainCamera.orthographicSize;
            var cameraWidth = cameraHeight * mainCamera.aspect;
            var cameraDiagonal = Mathf.Sqrt(cameraWidth * cameraWidth + cameraHeight * cameraHeight);

            for (int i = 0; i < amount; i++)
            {
                if (enemies.Count >= enemiesCap) return;

                var enemy = enemyPools[type].GetEntity();
                enemy.SetData(enemyDataDictionary[type]);
                enemy.SetWaveOverride(waveOverride); // 应用波次覆盖配置

                var triesCount = 0;
                var maxTriesCount = 10;
                var position = Vector3.zero;
                var foundPosition = false;

                // 尝试寻找合法生成位置
                while (triesCount < maxTriesCount)
                {
                    triesCount++;
                    // [原有注释] 当一次性生成大量敌人时，我们希望偏移它们以避免过载物理计算
                    // [新注释] 随着场上敌人增多，增加生成距离，防止重叠导致的物理引擎抖动
                    var additionalDistance = amount > 100 ? Mathf.Sqrt(enemies.Count) * 0.1f : 0;

                    if (circularSpawn)
                    {
                        // 圆形生成：以玩家为中心，在对角线距离外加随机偏移处生成
                        position = PlayerBehavior.Player.transform.position + Random.onUnitSphere.SetZ(0).normalized * (cameraDiagonal * 1.05f + Random.value * 0.2f + additionalDistance);
                    }
                    else
                    {
                        // 屏幕外生成：在相机视野外随机位置生成
                        position = CameraManager.GetRandomPointOutsideCamera(0.5f + Random.value * 0.2f + additionalDistance);
                    }

                    // 验证位置是否合法（不在障碍物内等）
                    if (StageController.FieldManager.ValidatePosition(position, Vector2.zero))
                    {
                        foundPosition = true;
                        break;
                    }
                }

                // 如果随机位置失败，尝试向玩家方向拉近位置
                if (!foundPosition)
                {
                    for (int j = 1; j < 10; j++)
                    {
                        var middlePosition = Vector3.Lerp(position, PlayerBehavior.Player.transform.position, 1 - j / 10f);
                        if (StageController.FieldManager.ValidatePosition(middlePosition, Vector2.zero))
                        {
                            foundPosition = true;
                            position = middlePosition;
                            break;
                        }
                    }
                }

                // 如果依然失败，强制放置在地图边界
                if (!foundPosition)
                {
                    position = StageController.FieldManager.GetRandomPositionOnBorder();
                }

                enemy.transform.position = position;
                enemy.onEnemyDied += OnEnemyDied;
                if (onEnemyDiedCallback != null) enemy.onEnemyDied += onEnemyDiedCallback;

                enemy.Play();
                enemies.Add(enemy);
            }
        }

        // 获取一个随机可见的敌人
        public virtual EnemyBehavior GetRandomVisibleEnemy()
        {
            if (enemies.Count == 0) return null;

            // [原有注释] 尝试寻找随机可见敌人10次
            // [新注释] 优先随机查找以提高效率，若失败则遍历整个列表
            for (int i = 0; i < 10; i++)
            {
                var randomIndex = Random.Range(0, enemies.Count);
                var enemy = enemies[randomIndex];
                if (enemy.IsVisible) return enemy;
            }

            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy.IsVisible) return enemy;
            }

            return null;
        }

        // 获取指定半径内的所有敌人
        public virtual List<EnemyBehavior> GetEnemiesInRadius(Vector2 position, float radius)
        {
            var result = new List<EnemyBehavior>();
            float radiusSqr = radius * radius; // 使用平方距离避免开方运算，提升性能

            for (int i = 0; i < enemies.Count; i++)
            {
                if ((enemies[i].transform.position.XY() - position).sqrMagnitude <= radiusSqr)
                {
                    result.Add(enemies[i]);
                }
            }
            return result;
        }

        // 杀死所有敌人
        public virtual void KillEveryEnemy()
        {
            foreach (var enemy in enemies)
            {
                enemy.onEnemyDied -= OnEnemyDied; // 移除事件监听，防止重复触发
                enemy.Kill();
            }

            enemiesDiedCounter += enemies.Count;
            stageSave.EnemiesKilled = enemiesDiedCounter;
            enemiesDiedLabel.SetAmount(enemiesDiedCounter);
            enemies.Clear();
        }

        // 对所有敌人造成伤害
        public virtual void DealDamageToAllEnemies(float damage)
        {
            var aliveEnemies = new List<EnemyBehavior>();
            foreach (var enemy in enemies)
            {
                if (enemy.HP <= damage)
                {
                    // [原有注释] 如果敌人不是Boss
                    // [新注释] 只有普通敌人才会直接死亡并掉落物品，Boss可能有特殊处理逻辑
                    if (enemy.Data != null)
                    {
                        enemy.onEnemyDied -= OnEnemyDied;
                        enemy.Kill();

                        // 处理掉落物
                        foreach (var dropData in enemy.GetDropData())
                        {
                            if (dropData.Chance == 0) continue;
                            if (Random.value * 100 <= dropData.Chance && StageController.DropManager.CheckDropCooldown(dropData.DropType))
                            {
                                StageController.DropManager.Drop(dropData.DropType, enemy.transform.position.XY() + Random.insideUnitCircle * 0.2f);
                            }
                        }
                    }
                    else
                    {
                        aliveEnemies.Add(enemy); // 如果是Boss或其他特殊单位，暂时保留
                    }
                }
                else
                {
                    // [原有注释] 如果敌人不是Boss
                    if (enemy.Data != null)
                    {
                        enemy.TakeDamage(damage);
                    }
                    aliveEnemies.Add(enemy);
                }
            }

            // 更新击杀计数和存活列表
            enemiesDiedCounter += enemies.Count - aliveEnemies.Count;
            stageSave.EnemiesKilled = enemiesDiedCounter;
            enemiesDiedLabel.SetAmount(enemiesDiedCounter);

            enemies.Clear();
            enemies.AddRange(aliveEnemies);
        }

        // 敌人死亡回调
        protected virtual void OnEnemyDied(EnemyBehavior enemy)
        {
            enemies.RemoveSwapBack(enemy); // 高效移除列表元素
            enemy.onEnemyDied -= OnEnemyDied; // 注销事件

            // 生成掉落物
            foreach (var dropData in enemy.GetDropData())
            {
                if (dropData.Chance == 0) continue;
                if (Random.value * 100 <= dropData.Chance && StageController.DropManager.CheckDropCooldown(dropData.DropType))
                {
                    StageController.DropManager.Drop(dropData.DropType, enemy.transform.position.XY() + Random.insideUnitCircle * 0.2f);
                }
            }

            enemiesDiedCounter++;
            stageSave.EnemiesKilled = enemiesDiedCounter;
            enemiesDiedLabel.SetAmount(enemiesDiedCounter);
        }

        // Boss死亡回调
        protected virtual void OnBossDied(EnemyBehavior boss)
        {
            enemies.RemoveSwapBack(boss);
            boss.onEnemyDied -= OnBossDied;

            // Boss死亡必定掉落磁铁和食物，若有可用技能槽位则掉落宝箱
            if (boss.ShouldSpawnChestOnDeath && StageController.AbilityManager.HasAvailableAbilities())
                StageController.DropManager.Drop(DropType.Chest, boss.transform.position.XY() + Random.insideUnitCircle);

            StageController.DropManager.Drop(DropType.Magnet, boss.transform.position.XY() + Random.insideUnitCircle);
            StageController.DropManager.Drop(DropType.Food, boss.transform.position.XY() + Random.insideUnitCircle);

            enemiesDiedCounter++;
            stageSave.EnemiesKilled = enemiesDiedCounter;
            enemiesDiedLabel.SetAmount(enemiesDiedCounter);
        }

        // 生成Boss
        public virtual EnemyBehavior SpawnBoss(BossType bossType, Vector2 spawnPosition, UnityAction<EnemyBehavior> onBossDied = null)
        {
            var bossData = bossfightDatabase.GetBossfight(bossType);
            // Boss通常不使用对象池，直接实例化
            var boss = Instantiate(bossData.BossPrefab).GetComponent<EnemyBehavior>();
            boss.transform.position = spawnPosition;
            boss.Play();
            boss.onEnemyDied += OnBossDied;
            boss.onEnemyDied += onBossDied;
            enemies.Add(boss);
            return boss;
        }

        // 获取Boss数据
        public virtual BossfightData GetBossData(BossType bossType)
        {
            return bossfightDatabase.GetBossfight(bossType);
        }
    }
}