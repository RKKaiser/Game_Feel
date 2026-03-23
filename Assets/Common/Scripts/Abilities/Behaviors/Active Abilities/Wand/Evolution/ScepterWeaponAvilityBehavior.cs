using OctoberStudio.Easing;
using OctoberStudio.Extensions;
using OctoberStudio.Pool;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 添加新输入系统命名空间

namespace OctoberStudio.Abilities
{
    public class ScepterWeaponAvilityBehavior : AbilityBehavior<ScepterWeaponAbilityData, ScepterWeaponAbilityLevel>
    {
        public static readonly int SEPTER_PROJECTILE_LAUNCH_HASH = "Septer Projectile Launch".GetHashCode();

        [SerializeField] GameObject projectilePrefab;
        public GameObject ProjectilePrefab => projectilePrefab;

        private PoolComponent<SimplePlayerProjectileBehavior> projectilePool;
        public List<SimplePlayerProjectileBehavior> projectiles = new List<SimplePlayerProjectileBehavior>();

        IEasingCoroutine projectileCoroutine;
        Coroutine abilityCoroutine;

        // 扇形参数（可自行调整）
        private const float SPREAD_ANGLE = 60f;    // 总扇形角度
        private const int PROJECTILE_COUNT = 10;   // 子弹数量

        private float AbilityCooldown => AbilityLevel.AbilityCooldown * PlayerBehavior.Player.CooldownMultiplier;

        private void Awake()
        {
            projectilePool = new PoolComponent<SimplePlayerProjectileBehavior>("Scepter Projectile", ProjectilePrefab, 50);
        }

        protected override void SetAbilityLevel(int stageId)
        {
            base.SetAbilityLevel(stageId);

            if (abilityCoroutine != null) Disable();

            abilityCoroutine = StartCoroutine(AbilityCoroutine());
        }

        private IEnumerator AbilityCoroutine()
        {
            var lastTimeSpawned = Time.time - AbilityCooldown;

            while (true)
            {
                while (lastTimeSpawned + AbilityCooldown < Time.time)
                {
                    var spawnTime = lastTimeSpawned + AbilityCooldown;

                    // 获取鼠标指向的中心方向
                    Vector2 centerDirection = GetMouseDirection();

                    // 生成扇形子弹
                    for (int i = 0; i < PROJECTILE_COUNT; i++)
                    {
                        // 计算偏移角度（均匀分布）
                        float angleOffset = (i - (PROJECTILE_COUNT - 1) / 2f) * (SPREAD_ANGLE / (PROJECTILE_COUNT - 1));
                        Vector2 direction = RotateVector(centerDirection, angleOffset * Mathf.Deg2Rad);

                        var projectile = projectilePool.GetEntity();

                        var aliveDuration = Time.time - spawnTime;
                        var position = PlayerBehavior.CenterPosition + direction * aliveDuration * AbilityLevel.ProjectileSpeed * PlayerBehavior.Player.ProjectileSpeedMultiplier;

                        projectile.Init(position, direction);
                        projectile.Speed = AbilityLevel.ProjectileSpeed * PlayerBehavior.Player.ProjectileSpeedMultiplier;
                        projectile.transform.localScale = Vector3.one * AbilityLevel.ProjectileSize * PlayerBehavior.Player.SizeMultiplier;
                        projectile.LifeTime = AbilityLevel.ProjectileLifetime;
                        projectile.DamageMultiplier = AbilityLevel.Damage;

                        projectile.onFinished += OnProjectileFinished;
                        projectiles.Add(projectile);
                    }

                    lastTimeSpawned += AbilityCooldown;

                    GameController.AudioManager.PlaySound(SEPTER_PROJECTILE_LAUNCH_HASH);
                }

                yield return null;
            }
        }

        // 获取鼠标方向（使用新 Input System）
        private Vector2 GetMouseDirection()
        {
            var mouse = Mouse.current;
            if (mouse == null) return Vector2.up;

            Vector2 mouseScreenPos = mouse.position.ReadValue();
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
            mouseWorldPos.z = 0f;

            Vector2 mousePos2D = new Vector2(mouseWorldPos.x, mouseWorldPos.y);
            Vector2 direction = mousePos2D - PlayerBehavior.CenterPosition;

            if (direction.sqrMagnitude < 0.001f) return Vector2.up;
            return direction.normalized;
        }

        // 旋转向量
        private Vector2 RotateVector(Vector2 v, float rad)
        {
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        private void OnProjectileFinished(SimplePlayerProjectileBehavior projectile)
        {
            projectile.onFinished -= OnProjectileFinished;

            projectiles.Remove(projectile);
        }

        private void Disable()
        {
            projectileCoroutine.StopIfExists();

            for (int i = 0; i < projectiles.Count; i++)
            {
                projectiles[i].gameObject.SetActive(false);
            }

            projectiles.Clear();

            StopCoroutine(abilityCoroutine);
        }

        public override void Clear()
        {
            Disable();

            base.Clear();
        }
    }
}