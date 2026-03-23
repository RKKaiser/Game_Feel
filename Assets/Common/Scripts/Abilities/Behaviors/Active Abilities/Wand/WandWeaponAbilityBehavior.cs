using OctoberStudio.Easing;
using OctoberStudio.Extensions;
using OctoberStudio.Pool;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 添加新输入系统命名空间

namespace OctoberStudio.Abilities
{
    public class WandWeaponAbilityBehavior : AbilityBehavior<WoodenWandWeaponAbilityData, WoodenWandWeaponAbilityLevel>
    {
        public static readonly int WAND_PROJECTILE_LAUNCH_HASH = "Wand Projectile Launch".GetHashCode();

        [SerializeField] GameObject projectilePrefab;
        public GameObject ProjectilePrefab => projectilePrefab;

        private PoolComponent<SimplePlayerProjectileBehavior> projectilePool;
        public List<SimplePlayerProjectileBehavior> projectiles = new List<SimplePlayerProjectileBehavior>();

        Coroutine abilityCoroutine;

        private float AbilityCooldown => AbilityLevel.AbilityCooldown * PlayerBehavior.Player.CooldownMultiplier;

        private void Awake()
        {
            projectilePool = new PoolComponent<SimplePlayerProjectileBehavior>("Wand Projectile", ProjectilePrefab, 50);
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

                    var projectile = projectilePool.GetEntity();

                    Vector2 direction = GetMouseDirection();

                    var aliveDuration = Time.time - spawnTime;
                    var position = PlayerBehavior.CenterPosition + direction * aliveDuration * AbilityLevel.ProjectileSpeed * PlayerBehavior.Player.ProjectileSpeedMultiplier;

                    projectile.Init(position, direction);
                    projectile.Speed = AbilityLevel.ProjectileSpeed * PlayerBehavior.Player.ProjectileSpeedMultiplier;
                    projectile.transform.localScale = Vector3.one * AbilityLevel.ProjectileSize * PlayerBehavior.Player.SizeMultiplier;
                    projectile.LifeTime = AbilityLevel.ProjectileLifetime;
                    projectile.DamageMultiplier = AbilityLevel.Damage;

                    projectile.onFinished += OnProjectileFinished;
                    projectiles.Add(projectile);

                    lastTimeSpawned += AbilityCooldown;

                    GameController.AudioManager.PlaySound(WAND_PROJECTILE_LAUNCH_HASH);
                }

                yield return null;
            }
        }

        private Vector2 GetMouseDirection()
        {
            // 使用新 Input System 获取鼠标位置
            var mouse = Mouse.current;
            if (mouse == null)
            {
                // 如果没有鼠标设备（例如在编辑器外），返回默认方向
                return Vector2.up;
            }

            Vector2 mouseScreenPos = mouse.position.ReadValue();
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
            mouseWorldPos.z = 0f;

            Vector2 mousePos2D = new Vector2(mouseWorldPos.x, mouseWorldPos.y);
            Vector2 direction = mousePos2D - PlayerBehavior.CenterPosition;

            if (direction.sqrMagnitude < 0.001f)
            {
                return Vector2.up;
            }
            return direction.normalized;
        }

        private void OnProjectileFinished(SimplePlayerProjectileBehavior projectile)
        {
            projectile.onFinished -= OnProjectileFinished;

            projectiles.Remove(projectile);
        }

        private void Disable()
        {
            for (int i = 0; i < projectiles.Count; i++)
            {
                projectiles[i].gameObject.SetActive(false);
            }

            projectiles.Clear();

            if (abilityCoroutine != null)
                StopCoroutine(abilityCoroutine);
        }

        public override void Clear()
        {
            Disable();

            base.Clear();
        }
    }
}