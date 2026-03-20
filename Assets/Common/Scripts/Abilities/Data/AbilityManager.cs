using System.Collections.Generic;
using UnityEngine;

namespace OctoberStudio.Abilities
{
    using OctoberStudio.Easing;
    using OctoberStudio.Extensions;

    /// <summary>
    /// AbilityManager: 游戏技能/能力管理的核心控制器。
    /// 负责技能的获取、升级、进化、宝箱奖励逻辑以及基于权重的随机技能选择。
    /// </summary>
    public class AbilityManager : MonoBehaviour
    {
        // 技能数据库，包含所有技能的配置数据
        [SerializeField] protected AbilitiesDatabase abilitiesDatabase;

        [Space]
        // 宝箱奖励 tiers 的概率配置：Tier 5 (5个技能) 和 Tier 3 (3个技能) 的触发概率
        [SerializeField, Range(0, 1)] protected float chestChanceTier5;
        [SerializeField, Range(0, 1)] protected float chestChanceTier3;

        // 当前玩家已获取的所有技能行为实例列表
        protected List<IAbilityBehavior> aquiredAbilities = new List<IAbilityBehavior>();

        // 记录因进化而被移除的技能类型列表（防止它们再次出现）
        protected List<AbilityType> removedAbilities = new List<AbilityType>();

        // 技能存档数据对象
        protected AbilitiesSave save;

        // 关卡存档数据对象
        protected StageSave stageSave;

        // 主动技能槽位上限（从数据库读取）
        public int ActiveAbilitiesCapacity => abilitiesDatabase.ActiveAbilitiesCapacity;

        // 被动技能槽位上限（从数据库读取）
        public int PassiveAbilitiesCapacity => abilitiesDatabase.PassiveAbilitiesCapacity;

        /// <summary>
        /// Awake: 初始化存档系统。
        /// 如果玩家是正常退出后继续游戏（未死亡），则保留技能数据；否则清空。
        /// </summary>
        protected virtual void Awake()
        {
            save = GameController.SaveManager.GetSave<AbilitiesSave>("Abilities Save");
            save.Init();

            stageSave = GameController.SaveManager.GetSave<StageSave>("Stage");
            // 通常只有在玩家未死亡而关闭游戏并继续时，数据才不会被重置
            if (stageSave.ResetStageData) save.Clear();
        }

        /// <summary>
        /// Init: 初始化技能系统。
        /// 根据测试预设、存档数据或角色初始配置加载起始技能。
        /// 注册经验值升级事件。
        /// </summary>
        public virtual void Init(PresetData testingPreset, CharacterData characterData)
        {
            // 监听经验值等级变化事件
            StageController.ExperienceManager.onXpLevelChanged += OnXpLevelChanged;

            if (testingPreset != null)
            {
                // [新注] 如果分配了测试预设（用于开发调试），则从中加载起始技能
                for (int i = 0; testingPreset.Abilities.Count > i; i++)
                {
                    AbilityType type = testingPreset.Abilities[i].abilityType;
                    AbilityData data = abilitiesDatabase.GetAbility(type);
                    AddAbility(data, testingPreset.Abilities[i].level);
                }
            }
            else if (!stageSave.ResetStageData)
            {
                // [原注翻译] 游戏在玩家未死亡退出的情况下继续。从存档文件加载技能。
                var savedAbilities = save.GetSavedAbilities();
                for (int i = 0; i < savedAbilities.Count; i++)
                {
                    AbilityType type = savedAbilities[i];
                    AbilityData data = abilitiesDatabase.GetAbility(type);
                    AddAbility(data, save.GetAbilityLevel(type));
                }

                // [原注翻译] 如果存档中没有存储任何技能，则加载角色的起始技能；如果没有，则显示武器选择窗口。
                if (savedAbilities.Count == 0)
                {
                    if (characterData.HasStartingAbility)
                    {
                        AbilityData data = abilitiesDatabase.GetAbility(characterData.StartingAbility);
                        AddAbility(data, 0);
                    }
                    else
                    {
                        EasingManager.DoAfter(0.3f, ShowWeaponSelectScreen);
                    }
                }
            }
            else if (characterData.HasStartingAbility)
            {
                // [原注翻译] 没有测试预设或存档技能，加载角色起始技能。
                AbilityData data = abilitiesDatabase.GetAbility(characterData.StartingAbility);
                AddAbility(data, 0);
            }
            else
            {
                // [原注翻译] 仅延迟显示武器选择窗口
                EasingManager.DoAfter(0.3f, ShowWeaponSelectScreen);
            }
        }

        /// <summary>
        /// AddAbility: 添加一个新技能或实例化一个已有技能。
        /// 处理进化逻辑：如果添加的是进化技能，检查并移除作为进化素材的旧技能。
        /// </summary>
        public virtual void AddAbility(AbilityData abilityData, int level = 0)
        {
            // 实例化技能预制体并获取行为组件
            IAbilityBehavior ability = Instantiate(abilityData.Prefab).GetComponent<IAbilityBehavior>();
            ability.Init(abilityData, level);

            if (abilityData.IsEvolution)
            {
                // [原注翻译] 对于进化技能，我们需要搜索每一个需求，
                // 确认是否满足，并移除所有被进化的旧技能。
                for (int i = 0; i < abilityData.EvolutionRequirements.Count; i++)
                {
                    var requirement = abilityData.EvolutionRequirements[i];
                    if (requirement.ShouldRemoveAfterEvolution)
                    {
                        var requiredAbility = GetAquiredAbility(requirement.AbilityType);
                        if (requiredAbility != null)
                        {
                            // [原注翻译] 技能的游戏对象将在 Clear() 内部被销毁
                            requiredAbility.Clear();
                            aquiredAbilities.Remove(requiredAbility);
                            save.RemoveAbility(requiredAbility.AbilityType);
                            removedAbilities.Add(requiredAbility.AbilityType); // 标记为已移除，防止再次生成
                        }
                    }
                }
            }

            save.SetAbilityLevel(abilityData.AbilityType, level);
            aquiredAbilities.Add(ability);
        }

        /// <summary>
        /// 获取当前已拥有的主动技能数量。
        /// [原注翻译] 方法比较粗糙，但调用频率低，所以无害。
        /// </summary>
        public virtual int GetActiveAbilitiesCount()
        {
            int counter = 0;
            foreach (var ability in aquiredAbilities)
            {
                if (ability.AbilityData.IsActiveAbility) counter++;
            }
            return counter;
        }

        /// <summary>
        /// 获取当前已拥有的被动技能数量。
        /// [原注翻译] 方法比较粗糙，但调用频率低，所以无害。
        /// </summary>
        public virtual int GetPassiveAbilitiesCount()
        {
            int counter = 0;
            foreach (var ability in aquiredAbilities)
            {
                if (!ability.AbilityData.IsActiveAbility) counter++;
            }
            return counter;
        }

        /// <summary>
        /// 显示武器选择屏幕。
        /// 从数据库中筛选出所有非进化的武器技能，随机选择最多3个供玩家选择。
        /// </summary>
        protected virtual void ShowWeaponSelectScreen()
        {
            var weaponAbilities = new List<AbilityData>();
            // [原注翻译] 查找所有不是进化技能的武器技能
            for (int i = 0; i < abilitiesDatabase.AbilitiesCount; i++)
            {
                var abilityData = abilitiesDatabase.GetAbility(i);
                if (abilityData.IsWeaponAbility && !abilityData.IsEvolution)
                {
                    weaponAbilities.Add(abilityData);
                }
            }

            // [原注翻译] 随机选择最多三个
            var selectedAbilities = new List<AbilityData>();
            while (weaponAbilities.Count > 0 && selectedAbilities.Count < 3)
            {
                var abilityData = weaponAbilities.PopRandom();
                selectedAbilities.Add(abilityData);
            }

            // [原注翻译] 安全检查：如果没有选中任何技能，说明出了问题。
            // 这种情况下应该检查技能数据库中是否分配了任何武器技能。
            if (selectedAbilities.Count > 0)
            {
                StageController.GameScreen.ShowAbilitiesPanel(selectedAbilities, false);
            }
        }

        /// <summary>
        /// OnXpLevelChanged: 当玩家经验值升级时触发。
        /// 计算可用技能列表，基于权重算法随机选择3个技能供玩家升级或获取。
        /// 权重考虑因素：是否已拥有、主动/被动平衡、是否为进化技能、早期等级加成等。
        /// </summary>
        protected virtual void OnXpLevelChanged(int level)
        {
            var abilities = GetAvailableAbilities();
            var selectedAbilities = new List<AbilityData>();
            var weightedAbilities = new List<WeightedAbility>();

            bool firstLevels = level < 10; // 是否是游戏前期（前10级）
            var activeCount = GetActiveAbilitiesCount();
            var passiveCount = GetPassiveAbilitiesCount();
            bool moreActive = activeCount > passiveCount; // 主动技能是否多于被动
            bool morePassive = passiveCount > activeCount; // 被动技能是否多于主动

            // [原注翻译] 这里我们用权重填充技能列表。
            // 根据技能数据库中的乘数，某些技能被选中的几率会更高。
            // 例如，通常进化技能在可用时应每次都被选中。
            foreach (var ability in abilities)
            {
                var weight = 1f;
                // 如果已拥有该技能（升级），增加权重
                if (IsAbilityAquired(ability.AbilityType)) weight *= abilitiesDatabase.AquiredAbilityWeightMultiplier;

                if (ability.IsActiveAbility)
                {
                    if (firstLevels) weight *= abilitiesDatabase.FirstLevelsActiveAbilityWeightMultiplier; // 前期主动技能加权
                    if (morePassive) weight *= abilitiesDatabase.LessAbilitiesOfTypeWeightMultiplier; // 如果被动多，则加权主动以平衡
                    if (ability.IsEvolution) weight *= abilitiesDatabase.EvolutionAbilityWeightMultiplier; // 进化技能高权重
                }
                else
                {
                    // 如果该技能是某个已拥有进化技能的必要素材，增加权重
                    if (IsRequiredForAquiredEvolution(ability.AbilityType)) weight *= abilitiesDatabase.RequiredForEvolutionWeightMultiplier;
                    if (moreActive) weight *= abilitiesDatabase.LessAbilitiesOfTypeWeightMultiplier; // 如果主动多，则加权被动以平衡
                }
                weightedAbilities.Add(new WeightedAbility() { abilityData = ability, weight = weight });
            }

            // 循环选择3个技能
            while (abilities.Count > 0 && selectedAbilities.Count < 3)
            {
                // [原注翻译] 这里我们将权重归一化，使它们的总和正好为1
                float weightSum = 0f;
                foreach (var container in weightedAbilities) weightSum += container.weight;
                foreach (var container in weightedAbilities) container.weight /= weightSum;

                // [原注翻译] 获取0到1之间的随机值，
                // 遍历技能直到权重之和大于该随机值。
                // 如果随机值为0，选择第一个技能；如果为1，选择最后一个。
                float random = Random.value;
                float progress = 0;
                AbilityData selectedAbility = null;

                foreach (var container in weightedAbilities)
                {
                    progress += container.weight;
                    if (random <= progress)
                    {
                        selectedAbility = container.abilityData;
                        break;
                    }
                }

                // [原注翻译] 如果成功选中了一个技能（理应如此），将其从可用池中移除。
                // 如果出错，我们有故障保护机制——直接从可用技能中完全随机选择一个。
                if (selectedAbility != null)
                {
                    abilities.Remove(selectedAbility);
                }
                else
                {
                    selectedAbility = abilities.PopRandom();
                }

                // 从加权列表中移除已选中的技能
                foreach (var container in weightedAbilities)
                {
                    if (container.abilityData == selectedAbility)
                    {
                        weightedAbilities.Remove(container);
                        break;
                    }
                }

                selectedAbilities.Add(selectedAbility);
            }

            if (selectedAbilities.Count > 0)
            {
                StageController.GameScreen.ShowAbilitiesPanel(selectedAbilities, true);
            }
        }

        /// <summary>
        /// GetAvailableAbilities: 获取当前玩家可以选择的所有可用技能列表。
        /// 过滤条件包括：是否已满级、是否已被进化移除、是否满足进化条件、槽位限制等。
        /// 如果普通技能耗尽，则返回结局技能（Endgame Abilities）。
        /// </summary>
        protected virtual List<AbilityData> GetAvailableAbilities()
        {
            var result = new List<AbilityData>();

            // [原注翻译] 统计已获得的被动和主动技能数量。
            // 我们在技能数据库中指定了每种类型技能的最大数量。
            int activeAbilitiesCount = 0;
            int passiveAbilitiesCount = 0;

            for (int i = 0; i < aquiredAbilities.Count; i++)
            {
                var abilityBehavior = aquiredAbilities[i];
                var abilityData = abilitiesDatabase.GetAbility(abilityBehavior.AbilityType);
                if (abilityData.IsActiveAbility)
                {
                    activeAbilitiesCount++;
                }
                else
                {
                    passiveAbilitiesCount++;
                }
            }

            for (int i = 0; i < abilitiesDatabase.AbilitiesCount; i++)
            {
                var abilityData = abilitiesDatabase.GetAbility(i);

                // [原注翻译] 此技能仅在没有其他技能剩余时出现。
                // 通常是某种治疗或金币技能。
                if (abilityData.IsEndgameAbility) continue;

                // [原注翻译] 技能已达到最高级。无法进一步升级。
                if (save.GetAbilityLevel(abilityData.AbilityType) >= abilityData.LevelsCount - 1) continue;

                // [原注翻译] 该技能已被进化（移除）。
                if (removedAbilities.Contains(abilityData.AbilityType)) continue;

                if (abilityData.IsEvolution)
                {
                    // [原注翻译] 如果是进化技能，检查其需求是否满足
                    bool fulfilled = true;
                    for (int j = 0; j < abilityData.EvolutionRequirements.Count; j++)
                    {
                        var evolutionRequirements = abilityData.EvolutionRequirements[j];
                        var isRequiredAbilityAquired = IsAbilityAquired(evolutionRequirements.AbilityType);
                        var requiredAbilityReachedLevel = save.GetAbilityLevel(evolutionRequirements.AbilityType) >= evolutionRequirements.RequiredAbilityLevel;

                        if (!isRequiredAbilityAquired || !requiredAbilityReachedLevel)
                        {
                            // [原注翻译] 发现未满足的需求
                            fulfilled = false;
                            break;
                        }
                    }
                    if (!fulfilled) continue;
                }
                else
                {
                    var isAbilityAquired = IsAbilityAquired(abilityData.AbilityType);

                    // [原注翻译] 玩家同时只能拥有一种武器技能
                    if (abilityData.IsWeaponAbility && !isAbilityAquired) continue;

                    // [原注翻译] 没有可用的主动技能槽位了
                    if (abilityData.IsActiveAbility && activeAbilitiesCount >= abilitiesDatabase.ActiveAbilitiesCapacity && !isAbilityAquired) continue;

                    // [原注翻译] 没有可用的被动技能槽位了
                    if (!abilityData.IsActiveAbility && passiveAbilitiesCount >= abilitiesDatabase.PassiveAbilitiesCapacity && !isAbilityAquired) continue;
                }

                result.Add(abilityData);
            }

            if (result.Count == 0)
            {
                // [原注翻译] 没有更多技能了，是时候展示结局技能了 :)
                for (int i = 0; i < abilitiesDatabase.AbilitiesCount; i++)
                {
                    var abilityData = abilitiesDatabase.GetAbility(i);
                    if (abilityData.IsEndgameAbility)
                    {
                        result.Add(abilityData);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 获取指定技能的当前等级。
        /// </summary>
        public virtual int GetAbilityLevel(AbilityType abilityType)
        {
            return save.GetAbilityLevel(abilityType);
        }

        /// <summary>
        /// 获取已拥有的指定类型的技能行为实例。
        /// </summary>
        public virtual IAbilityBehavior GetAquiredAbility(AbilityType abilityType)
        {
            for (int i = 0; i < aquiredAbilities.Count; i++)
            {
                if (aquiredAbilities[i].AbilityType == abilityType) return aquiredAbilities[i];
            }
            return null;
        }

        /// <summary>
        /// 检查玩家是否已拥有指定类型的技能。
        /// </summary>
        public virtual bool IsAbilityAquired(AbilityType ability)
        {
            for (int i = 0; i < aquiredAbilities.Count; i++)
            {
                if (aquiredAbilities[i].AbilityType == ability) return true;
            }
            return false;
        }

        /// <summary>
        /// 检查指定技能类型是否是某个已拥有进化技能的必要需求。
        /// [原注翻译] 遍历每个已拥有的技能，寻找可以进化且将此技能类型列为需求的技能。
        /// </summary>
        public virtual bool IsRequiredForAquiredEvolution(AbilityType abilityType)
        {
            foreach (var ability in aquiredAbilities)
            {
                // 只检查非进化的主动技能（通常进化素材是基础技能）
                if (!ability.AbilityData.IsActiveAbility || ability.AbilityData.IsEvolution) continue;

                foreach (var requirement in ability.AbilityData.EvolutionRequirements)
                {
                    if (requirement.AbilityType == abilityType) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 检查指定技能是否有对应的进化形态，并输出进化所需的另一个技能类型。
        /// [原注翻译] 我们假设每个进化需要一个主动和一个被动技能。
        /// 如果情况并非如此，逻辑将会失效（不会报错，但UI卡片显示的进化信息将不正确）。
        /// </summary>
        public virtual bool HasEvolution(AbilityType abilityType, out AbilityType otherRequiredAbilityType)
        {
            otherRequiredAbilityType = abilityType;
            for (int i = 0; i < abilitiesDatabase.AbilitiesCount; i++)
            {
                // [原注翻译] 遍历数据库中的每个主动进化技能，检查需求
                var ability = abilitiesDatabase.GetAbility(i);
                if (!ability.IsEvolution) continue;

                for (int j = 0; j < ability.EvolutionRequirements.Count; j++)
                {
                    var requirement = ability.EvolutionRequirements[j];
                    if (requirement.AbilityType == abilityType)
                    {
                        // 找到匹配的需求，返回另一个需求类型
                        for (int k = 0; k < ability.EvolutionRequirements.Count; k++)
                        {
                            if (k == j) continue;
                            otherRequiredAbilityType = ability.EvolutionRequirements[k].AbilityType;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 检查是否还有可用的技能供玩家选择。
        /// </summary>
        public virtual bool HasAvailableAbilities()
        {
            var abilities = GetAvailableAbilities();
            return abilities.Count > 0;
        }

        /// <summary>
        /// 获取指定类型的技能数据配置。
        /// </summary>
        public virtual AbilityData GetAbilityData(AbilityType abilityType)
        {
            return abilitiesDatabase.GetAbility(abilityType);
        }

        /// <summary>
        /// 获取所有已拥有技能的类型列表。
        /// [原注翻译] 简单遍历所有已拥有的技能并获取它们的类型。
        /// </summary>
        public virtual List<AbilityType> GetAquiredAbilityTypes()
        {
            var result = new List<AbilityType>();
            for (int i = 0; i < aquiredAbilities.Count; i++)
            {
                result.Add(aquiredAbilities[i].AbilityType);
            }
            return result;
        }

        /// <summary>
        /// ShowChest: 显示宝箱奖励界面。
        /// 根据剩余可升级次数随机决定宝箱等级（Tier 1/3/5），从中抽取技能。
        /// 处理槽位满员时的逻辑，确保不选出无法装备的技能。
        /// </summary>
        public virtual void ShowChest()
        {
            Time.timeScale = 0; // 暂停时间

            // [原注翻译] 收集每个技能及其剩余可用等级数。
            // 例如，如果技能有5级，当前在第3级（从0开始计数），数字将是 (5 - 3 - 1) = 1。
            // 这也适用于未获得的技能。(5 - -1 - 1) = 5。
            var availableAbilities = GetAvailableAbilities();
            var dictionary = new Dictionary<AbilityData, int>();

            // [原注翻译] 填充字典 <技能, 剩余等级数>
            // 同时计算游戏中还能进行多少次技能升级
            var counter = 0;
            foreach (var ability in availableAbilities)
            {
                int levelsLeft = ability.LevelsCount - save.GetAbilityLevel(ability.AbilityType) - 1;
                dictionary.Add(ability, levelsLeft);
                counter += levelsLeft;
            }

            // [原注翻译] 我们有3级宝箱，分别包含5、3或1个技能。
            // 我们随机选择层级，但要考虑上面的计数器。
            // 如果只剩下4次升级机会，我们就无法展示包含5个技能的最佳层级宝箱。
            var selectedAbilitiesCount = 1;
            var tierId = 0;
            if (counter >= 5 && Random.value < chestChanceTier5)
            {
                selectedAbilitiesCount = 5;
                tierId = 2;
            }
            else if (counter >= 3 && Random.value < chestChanceTier3)
            {
                selectedAbilitiesCount = 3;
                tierId = 1;
            }

            int activeAbilitiesCount = GetActiveAbilitiesCount();
            int passiveAbilitiesCount = GetPassiveAbilitiesCount();

            // [原注翻译] 随机选择技能
            var selectedAbilities = new List<AbilityData>();
            for (int i = 0; i < selectedAbilitiesCount; i++)
            {
                // [原注翻译] 从字典中获取随机技能
                var abilityPair = dictionary.Random();
                var ability = abilityPair.Key;
                dictionary[ability] -= 1;
                if (dictionary[ability] <= 0) dictionary.Remove(ability);

                // [原注翻译] 有可能因为选择了这个技能而达到了可用容量上限。
                // 如果该技能已经被选中或者是进化技能，则没问题。
                if (!selectedAbilities.Contains(ability) && !ability.IsEvolution)
                {
                    selectedAbilities.Add(ability);

                    // [新注] 仅检查新获得的技能对槽位的影响
                    if (!IsAbilityAquired(ability.AbilityType))
                    {
                        var abilitiesToRemove = new List<AbilityData>();
                        if (ability.IsActiveAbility)
                        {
                            // [原注翻译] 这是一个新的主动技能
                            activeAbilitiesCount++;
                            // [原注翻译] 我们已达到主动技能的容量上限
                            if (activeAbilitiesCount == ActiveAbilitiesCapacity)
                            {
                                foreach (var savedAbility in dictionary.Keys)
                                {
                                    // [原注翻译] 这个技能不再能我们从宝箱中获得了
                                    if (savedAbility.IsActiveAbility && !IsAbilityAquired(savedAbility.AbilityType) && !selectedAbilities.Contains(savedAbility))
                                    {
                                        abilitiesToRemove.Add(savedAbility);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // [原注翻译] 这是一个新的被动技能
                            passiveAbilitiesCount++;
                            // [原注翻译] 我们已达到被动技能的容量上限 (原文注释误写为active，逻辑实为passive)
                            if (passiveAbilitiesCount == PassiveAbilitiesCapacity)
                            {
                                foreach (var savedAbility in dictionary.Keys)
                                {
                                    // [原注翻译] 这个技能不再能我们从宝箱中获得了
                                    if (!savedAbility.IsActiveAbility && !IsAbilityAquired(savedAbility.AbilityType) && !selectedAbilities.Contains(savedAbility))
                                    {
                                        abilitiesToRemove.Add(savedAbility);
                                    }
                                }
                            }
                        }

                        foreach (var abilityToRemove in abilitiesToRemove)
                        {
                            dictionary.Remove(abilityToRemove);
                        }
                    }
                }
                else
                {
                    selectedAbilities.Add(ability);
                }

                if (dictionary.Count == 0) break;
            }

            // [原注翻译] 我们可能在上一步移除了一些技能，导致数量不足以支撑选定的宝箱层级
            while (selectedAbilities.Count < selectedAbilitiesCount)
            {
                tierId--;
                selectedAbilitiesCount -= 2;
                for (int i = selectedAbilitiesCount; i < selectedAbilities.Count; i++)
                {
                    selectedAbilities.RemoveAt(i);
                    i--;
                }
            }

            StageController.GameScreen.ShowChestWindow(tierId, availableAbilities, selectedAbilities);

            // [原注翻译] 应用技能
            foreach (var ability in selectedAbilities)
            {
                if (IsAbilityAquired(ability.AbilityType))
                {
                    // 升级已有技能
                    var level = save.GetAbilityLevel(ability.AbilityType);
                    if (!ability.IsEndgameAbility) level++;
                    if (level < 0) level = 0;
                    save.SetAbilityLevel(ability.AbilityType, level);
                    ability.Upgrade(level);
                }
                else
                {
                    // 添加新技能
                    AddAbility(ability);
                }
            }
        }

#if UNITY_EDITOR
        // --- 以下仅为编辑器开发调试用的方法 ---

        /// <summary>
        /// [Dev] 获取数据库中所有技能数据。
        /// </summary>
        public virtual List<AbilityData> GetAllAbilitiesDev()
        {
            var abilities = new List<AbilityData>();
            for (int i = 0; i < abilitiesDatabase.AbilitiesCount; i++)
            {
                abilities.Add(abilitiesDatabase.GetAbility(i));
            }
            return abilities;
        }

        /// <summary>
        /// [Dev] 获取指定技能的等级。
        /// </summary>
        public virtual int GetAbilityLevelDev(AbilityType type)
        {
            return save.GetAbilityLevel(type);
        }

        /// <summary>
        /// [Dev] 移除指定技能。
        /// </summary>
        public virtual void RemoveAbilityDev(AbilityData abilityData)
        {
            for (int i = 0; i < aquiredAbilities.Count; i++)
            {
                var ability = aquiredAbilities[i];
                if (ability.AbilityData == abilityData)
                {
                    ability.Clear();
                    aquiredAbilities.RemoveAt(i);
                    save.RemoveAbility(abilityData.AbilityType);
                    break;
                }
            }
        }

        /// <summary>
        /// [Dev] 降低指定技能等级。
        /// </summary>
        public virtual void DecreaseAbilityLevelDev(AbilityData abilityData)
        {
            var level = save.GetAbilityLevel(abilityData.AbilityType);
            if (level > 0)
            {
                save.SetAbilityLevel(abilityData.AbilityType, level - 1);
                for (int i = 0; i < aquiredAbilities.Count; i++)
                {
                    var ability = aquiredAbilities[i];
                    if (ability.AbilityData == abilityData)
                    {
                        abilityData.Upgrade(level - 1);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// [Dev] 提升指定技能等级。
        /// </summary>
        public virtual void IncreaseAbilityLevelDev(AbilityData abilityData)
        {
            var level = save.GetAbilityLevel(abilityData.AbilityType);
            if (level < abilityData.LevelsCount - 1)
            {
                save.SetAbilityLevel(abilityData.AbilityType, level + 1);
                for (int i = 0; i < aquiredAbilities.Count; i++)
                {
                    var ability = aquiredAbilities[i];
                    if (ability.AbilityData == abilityData)
                    {
                        abilityData.Upgrade(level + 1);
                        break;
                    }
                }
            }
        }
#endif
    }

    /// <summary>
    /// 用于编辑器开发的技能数据结构。
    /// </summary>
    [System.Serializable]
    public class AbilityDev
    {
        public AbilityType abilityType;
        public int level;
    }

    /// <summary>
    /// 带权重的技能容器，用于随机选择算法。
    /// </summary>
    public class WeightedAbility
    {
        public AbilityData abilityData;
        public float weight;
    }
}