using OctoberStudio.Easing;
using OctoberStudio.Extensions;
using OctoberStudio.Upgrades;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace OctoberStudio
{
    /// <summary>
    /// 玩家行为控制类，管理玩家的移动、属性、受伤、复活等核心逻辑
    /// </summary>
    public class PlayerBehavior : MonoBehaviour
    {
        // 动画状态哈希值，用于性能优化
        protected static readonly int DEATH_HASH = "Death".GetHashCode();
        protected static readonly int REVIVE_HASH = "Revive".GetHashCode();
        protected static readonly int RECEIVING_DAMAGE_HASH = "Receiving Damage".GetHashCode();

        // 玩家单例实例
        protected static PlayerBehavior instance;
        public static PlayerBehavior Player => instance;

        // 角色数据库引用
        [SerializeField] protected CharactersDatabase charactersDatabase;

        [Header("Stats")]
        [SerializeField, Min(0.01f)] protected float speed = 2;                       // 基础移动速度
        [SerializeField, Min(0.1f)] protected float defaultMagnetRadius = 0.75f;     // 默认吸引半径
        [SerializeField, Min(1f)] protected float xpMultiplier = 1;                  // 经验倍率
        [SerializeField, Range(0.1f, 1f)] protected float cooldownMultiplier = 1;    // 冷却倍率
        [SerializeField, Range(0, 100)] protected int initialDamageReductionPercent = 0; // 初始伤害减免百分比
        [SerializeField, Min(1f)] protected float initialProjectileSpeedMultiplier = 1;   // 初始投射物速度倍率
        [SerializeField, Min(1f)] protected float initialSizeMultiplier = 1f;              // 初始大小倍率
        [SerializeField, Min(1f)] protected float initialDurationMultiplier = 1f;          // 初始持续时间倍率
        [SerializeField, Min(1f)] protected float initialGoldMultiplier = 1;               // 初始金币倍率

        [Header("References")]
        [SerializeField] protected HealthbarBehavior healthbar;            // 血条组件
        [SerializeField] protected Transform centerPoint;                  // 玩家中心点
        [SerializeField] protected PlayerEnemyCollisionHelper collisionHelper; // 碰撞辅助器

        // 玩家中心点的全局访问属性
        public static Transform CenterTransform => instance.centerPoint;
        public static Vector2 CenterPosition
        {
            get
            {
                if (instance.Character != null && instance.Character.CenterTransform != null)
                {
                    return instance.Character.CenterTransform.position;
                }
                return instance.centerPoint.position;
            }
        }

        [Header("Death and Revive")]
        [SerializeField] protected ParticleSystem reviveParticle;          // 复活特效

        [Space]
        [SerializeField] protected SpriteRenderer reviveBackgroundSpriteRenderer; // 复活背景渲染器
        [SerializeField, Range(0, 1)] protected float reviveBackgroundAlpha;      // 复活背景透明度
        [SerializeField, Range(0, 1)] protected float reviveBackgroundSpawnDelay; // 复活背景出现延迟
        [SerializeField, Range(0, 1)] protected float reviveBackgroundHideDelay;  // 复活背景隐藏延迟

        [Space]
        [SerializeField] protected SpriteRenderer reviveBottomSpriteRenderer;      // 复活底部渲染器
        [SerializeField, Range(0, 1)] protected float reviveBottomAlpha;           // 复活底部透明度
        [SerializeField, Range(0, 1)] protected float reviveBottomSpawnDelay;      // 复活底部出现延迟
        [SerializeField, Range(0, 1)] protected float reviveBottomHideDelay;       // 复活底部隐藏延迟

        [Header("Other")]
        [SerializeField] protected Vector2 fenceOffset;                     // 移动边界偏移量
        [SerializeField] protected Color hitColor;                         // 受伤闪白颜色
        [SerializeField] protected float enemyInsideDamageInterval = 2f;   // 敌人内部伤害间隔

        // 玩家死亡事件
        public event UnityAction onPlayerDied;

        // 实时属性
        public float Damage { get; protected set; }                // 伤害值
        public float MagnetRadiusSqr { get; protected set; }       // 吸引半径平方
        public float Speed { get; protected set; }                 // 当前移动速度

        // 各种倍率属性
        public float XPMultiplier { get; protected set; }
        public float CooldownMultiplier { get; protected set; }
        public float DamageReductionMultiplier { get; protected set; }
        public float ProjectileSpeedMultiplier { get; protected set; }
        public float SizeMultiplier { get; protected set; }
        public float DurationMultiplier { get; protected set; }
        public float GoldMultiplier { get; protected set; }

        public Vector2 LookDirection { get; protected set; }       // 玩家面朝方向
        public bool IsMovingAlowed { get; set; }                   // 是否允许移动

        protected bool invincible = false;                         // 无敌状态标志

        protected List<EnemyBehavior> enemiesInside = new List<EnemyBehavior>(); // 当前在玩家体内的敌人列表

        protected CharactersSave charactersSave;                   // 角色存档
        public CharacterData Data { get; set; }                    // 当前角色数据
        protected ICharacterBehavior Character { get; set; }       // 当前角色行为接口

        protected virtual void Awake()
        {
            // 加载角色存档并获取选中角色数据
            charactersSave = GameController.SaveManager.GetSave<CharactersSave>("Characters");
            Data = charactersDatabase.GetCharacterData(charactersSave.SelectedCharacterId);

            // 实例化角色预制体并设置为子物体
            Character = Instantiate(Data.Prefab).GetComponent<ICharacterBehavior>();
            Character.Transform.SetParent(transform);
            Character.Transform.ResetLocal();

            instance = this;
            // 初始化血条，使用基础生命值
            healthbar.Init(Data.BaseHP);
            healthbar.SetAutoHideWhenMax(true);
            healthbar.SetAutoShowOnChanged(true);

            // 初始化各种属性
            RecalculateMagnetRadius(1);
            RecalculateMoveSpeed(1);
            RecalculateDamage(1);
            RecalculateMaxHP(1);
            RecalculateXPMuliplier(1);
            RecalculateCooldownMuliplier(1);
            RecalculateDamageReduction(0);
            RecalculateProjectileSpeedMultiplier(1f);
            RecalculateSizeMultiplier(1f);
            RecalculateDurationMultiplier(1);
            RecalculateGoldMultiplier(1);

            LookDirection = Vector2.right;

            IsMovingAlowed = true;
        }

        protected virtual void Update()
        {
            // 已死亡则不更新移动逻辑
            if (healthbar.IsZero) return;

            // 处理玩家体内的敌人周期性伤害
            foreach (var enemy in enemiesInside)
            {
                if (Time.time - enemy.LastTimeDamagedPlayer > enemyInsideDamageInterval)
                {
                    TakeDamage(enemy.GetDamage());
                    enemy.LastTimeDamagedPlayer = Time.time;
                }
            }

            if (!IsMovingAlowed) return;

            // 获取输入并移动玩家
            var input = GameController.InputManager.MovementValue;

            float joysticPower = input.magnitude;
            Character.SetSpeed(joysticPower);

            if (!Mathf.Approximately(joysticPower, 0) && Time.timeScale > 0)
            {
                var frameMovement = input * Time.deltaTime * Speed;

                // 边界校验
                if (StageController.FieldManager.ValidatePosition(transform.position + Vector3.right * frameMovement.x, fenceOffset))
                {
                    transform.position += Vector3.right * frameMovement.x;
                }

                if (StageController.FieldManager.ValidatePosition(transform.position + Vector3.up * frameMovement.y, fenceOffset))
                {
                    transform.position += Vector3.up * frameMovement.y;
                }

                collisionHelper.transform.localPosition = Vector3.zero;

                // 根据移动方向设置角色缩放（左右翻转）
                Character.SetLocalScale(new Vector3(input.x > 0 ? 1 : -1, 1, 1));

                LookDirection = input.normalized;
            }
        }

        /// <summary>
        /// 判断目标是否在玩家的吸引半径内
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool IsInsideMagnetRadius(Transform target)
        {
            return (transform.position - target.position).sqrMagnitude <= MagnetRadiusSqr;
        }

        // 以下方法用于外部修改玩家属性（如升级加成）
        public virtual void RecalculateMagnetRadius(float magnetRadiusMultiplier)
        {
            MagnetRadiusSqr = Mathf.Pow(defaultMagnetRadius * magnetRadiusMultiplier, 2);
        }

        public virtual void RecalculateMoveSpeed(float moveSpeedMultiplier)
        {
            Speed = speed * moveSpeedMultiplier;
        }

        public virtual void RecalculateDamage(float damageMultiplier)
        {
            Damage = Data.BaseDamage * damageMultiplier;
            // 如果已获得伤害升级，额外乘算
            if (GameController.UpgradesManager.IsUpgradeAquired(UpgradeType.Damage))
            {
                Damage *= GameController.UpgradesManager.GetUpgadeValue(UpgradeType.Damage);
            }
        }

        public virtual void RecalculateMaxHP(float maxHPMultiplier)
        {
            var upgradeValue = GameController.UpgradesManager.GetUpgadeValue(UpgradeType.Health);
            healthbar.ChangeMaxHP((Data.BaseHP + upgradeValue) * maxHPMultiplier);
        }

        public virtual void RecalculateXPMuliplier(float xpMultiplier)
        {
            XPMultiplier = this.xpMultiplier * xpMultiplier;
        }

        public virtual void RecalculateCooldownMuliplier(float cooldownMultiplier)
        {
            CooldownMultiplier = this.cooldownMultiplier * cooldownMultiplier;
        }

        public virtual void RecalculateDamageReduction(float damageReductionPercent)
        {
            DamageReductionMultiplier = (100f - initialDamageReductionPercent - damageReductionPercent) / 100f;

            if (GameController.UpgradesManager.IsUpgradeAquired(UpgradeType.Armor))
            {
                DamageReductionMultiplier *= GameController.UpgradesManager.GetUpgadeValue(UpgradeType.Armor);
            }
        }

        public virtual void RecalculateProjectileSpeedMultiplier(float projectileSpeedMultiplier)
        {
            ProjectileSpeedMultiplier = initialProjectileSpeedMultiplier * projectileSpeedMultiplier;
        }

        public virtual void RecalculateSizeMultiplier(float sizeMultiplier)
        {
            SizeMultiplier = initialSizeMultiplier * sizeMultiplier;
        }

        public virtual void RecalculateDurationMultiplier(float durationMultiplier)
        {
            DurationMultiplier = initialDurationMultiplier * durationMultiplier;
        }

        public virtual void RecalculateGoldMultiplier(float goldMultiplier)
        {
            GoldMultiplier = initialGoldMultiplier * goldMultiplier;
        }

        /// <summary>
        /// 按百分比恢复生命值
        /// </summary>
        public virtual void RestoreHP(float hpPercent)
        {
            healthbar.AddPercentage(hpPercent);
        }

        /// <summary>
        /// 恢复固定生命值（额外加上升级治疗效果）
        /// </summary>
        public virtual void Heal(float hp)
        {
            healthbar.AddHP(hp + GameController.UpgradesManager.GetUpgadeValue(UpgradeType.Healing));
        }

        /// <summary>
        /// 复活玩家
        /// </summary>
        public virtual void Revive()
        {
            Character.PlayReviveAnimation();
            reviveParticle.Play();

            invincible = true;            // 复活后短暂无敌
            IsMovingAlowed = false;       // 暂时禁止移动
            healthbar.ResetHP(1f);        // 生命值回满

            Character.SetSortingOrder(102); // 提高渲染顺序，避免遮挡

            // 淡出复活背景和底部特效
            reviveBackgroundSpriteRenderer.DoAlpha(0f, 0.3f, reviveBottomHideDelay).SetUnscaledTime(true).SetOnFinish(() => reviveBackgroundSpriteRenderer.gameObject.SetActive(false));
            reviveBottomSpriteRenderer.DoAlpha(0f, 0.3f, reviveBottomHideDelay).SetUnscaledTime(true).SetOnFinish(() => reviveBottomSpriteRenderer.gameObject.SetActive(false));

            GameController.AudioManager.PlaySound(REVIVE_HASH);
            EasingManager.DoAfter(1f, () =>
            {
                IsMovingAlowed = true;
                Character.SetSortingOrder(0);
            });

            EasingManager.DoAfter(3, () => invincible = false);
        }

        /// <summary>
        /// 触发器进入处理，用于碰撞检测（敌人和投射物）
        /// </summary>
        public virtual void CheckTriggerEnter2D(Collider2D collision)
        {
            if (collision.gameObject.layer == 7) // 敌人层
            {
                if (invincible) return;

                var enemy = collision.GetComponent<EnemyBehavior>();

                if (enemy != null)
                {
                    enemiesInside.Add(enemy);
                    enemy.LastTimeDamagedPlayer = Time.time;

                    enemy.onEnemyDied += OnEnemyDied;
                    TakeDamage(enemy.GetDamage()); // 接触时立即造成一次伤害
                }
            }
            else
            {
                if (invincible) return;

                var projectile = collision.GetComponent<SimpleEnemyProjectileBehavior>();
                if (projectile != null)
                {
                    TakeDamage(projectile.Damage);
                }
            }
        }

        /// <summary>
        /// 触发器退出处理
        /// </summary>
        public virtual void CheckTriggerExit2D(Collider2D collision)
        {
            if (collision.gameObject.layer == 7)
            {
                if (invincible) return;

                var enemy = collision.GetComponent<EnemyBehavior>();

                if (enemy != null)
                {
                    enemiesInside.Remove(enemy);
                    enemy.onEnemyDied -= OnEnemyDied;
                }
            }
        }

        // 敌人死亡时从列表中移除
        protected virtual void OnEnemyDied(EnemyBehavior enemy)
        {
            enemy.onEnemyDied -= OnEnemyDied;
            enemiesInside.Remove(enemy);
        }

        protected float lastTimeVibrated = 0f; // 震动冷却计时

        /// <summary>
        /// 玩家受到伤害
        /// </summary>
        public virtual void TakeDamage(float damage)
        {
            if (invincible || healthbar.IsZero) return;

            // 实际伤害 = 原始伤害 * 伤害减免倍率
            healthbar.Subtract(damage * DamageReductionMultiplier);

            Character.FlashHit(); // 播放受伤闪白效果

            if (healthbar.IsZero) // 玩家死亡
            {
                Character.PlayDefeatAnimation();
                Character.SetSortingOrder(102);

                // 显示复活相关UI特效
                reviveBackgroundSpriteRenderer.gameObject.SetActive(true);
                reviveBackgroundSpriteRenderer.DoAlpha(reviveBackgroundAlpha, 0.3f, reviveBackgroundSpawnDelay).SetUnscaledTime(true);
                reviveBackgroundSpriteRenderer.transform.position = transform.position.SetZ(reviveBackgroundSpriteRenderer.transform.position.z);

                reviveBottomSpriteRenderer.gameObject.SetActive(true);
                reviveBottomSpriteRenderer.DoAlpha(reviveBottomAlpha, 0.3f, reviveBottomSpawnDelay).SetUnscaledTime(true);

                GameController.AudioManager.PlaySound(DEATH_HASH);

                // 延迟触发死亡事件
                EasingManager.DoAfter(0.5f, () =>
                {
                    onPlayerDied?.Invoke();
                }).SetUnscaledTime(true);

                GameController.VibrationManager.StrongVibration();
            }
            else // 受伤但未死亡
            {
                if (Time.time - lastTimeVibrated > 0.05f)
                {
                    GameController.VibrationManager.LightVibration();
                    lastTimeVibrated = Time.time;
                }

                GameController.AudioManager.PlaySound(RECEIVING_DAMAGE_HASH);
            }
        }
    }
}