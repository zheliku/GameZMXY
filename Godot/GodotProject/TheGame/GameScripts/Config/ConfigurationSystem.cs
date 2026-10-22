using GameConfig.Entity;
using GameLogic.Battle.Attributes;
using GameConfig.Attributes;
using GameLogic.Enemies.Body;
using GameLogic.Heroes.Body;
using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig;
using GameConfig.Hero;
using GameConfig.Monster;
using GameLogic.Profile;
using GameConfig.Equipment;
using GameConfig.Loot;
using GameConfig.Skills;
using Godot;
using GameLogic.Architecture;

namespace GameLogic.Config;

/// <summary>启动期的配置业务检查；集中校验跨表引用和玩法约束，失败阻止进入菜单。</summary>
public sealed class ConfigurationSystem : AbstractSystem
{
    /// <summary>绑定所属架构。</summary>
    /// <param name="architecture">所属游戏入口。</param>
    public ConfigurationSystem(IArchitecture architecture) : base(architecture) { }

    /// <summary>配置模型先注册，随后执行一次跨表校验。</summary>
    protected override void OnInit()
    {
        ValidateTables(this.GetModel<ConfigurationModel>().Tables, this.GetUtility<ExperienceCurve>());
    }

    /// <summary>校验全部启动约束，汇总所有错误后一次抛出。</summary>
    /// <param name="tables">已加载的配置总表。</param>
    /// <param name="curve">已注册的共享曲线；独立校验未提供时才构建。</param>
    /// <returns>经过校验的共享经验曲线（供档案与存档使用，避免重复构建）。</returns>
    /// <exception cref="ArgumentNullException">总表为空。</exception>
    /// <exception cref="InvalidOperationException">任一约束不满足；消息列出全部问题。</exception>
    internal static ExperienceCurve ValidateTables(Tables tables, ExperienceCurve curve = null)
    {
        ArgumentNullException.ThrowIfNull(tables);
        List<string> errors = new();

        // 经验曲线：构造函数内校验等级连续、末级为零、累计不溢出。
        if (curve == null)
        {
            try
            {
                curve = new ExperienceCurve(tables.TbHeroLevelConfig.DataList);
            }
            catch (ArgumentException exception)
            {
                errors.Add($"经验表：{exception.Message}");
            }
        }

        // 成长表：每名英雄覆盖 1..MaxLevel，且没有多余等级。
        if (curve != null)
        {
            ValidateGrowth(tables, curve.MaxLevel, errors);
        }

        // 建档规则：出战英雄存在。
        if (tables.TbHeroConfig.GetOrDefault(tables.TbProfileConfig.StartHeroId) == null)
        {
            errors.Add($"建档规则：StartHeroId={tables.TbProfileConfig.StartHeroId} 不在 HeroConfig 中。");
        }

        if (tables.TbProfileConfig.StartSoul < 0)
        {
            errors.Add("建档规则：StartSoul 不能为负。");
        }

        // 怪物：生命上限为正、经验奖励非负、等级为正。
        foreach (MonsterConfig monster in tables.TbMonsterConfig.DataList)
        {
            if (monster.BaseAttributes.MaxHp <= 0 || monster.AddExp < 0 || monster.Level < 1)
            {
                errors.Add($"怪物 {monster.Id}：MaxHp 必须为正、AddExp 非负、Level 至少为 1。");
            }
        }

        foreach (var group in tables.TbMonsterConfig.DataList.GroupBy(x => x.EntityId))
        {
            if (group.Count() != 1)
                errors.Add($"怪物实体 {group.Key} 必须只有一条基础配置，关卡差异通过刷怪配方描述。");
        }

        // 概率与整数共用一份属性定义，启动时拒绝把旧属性点误填成概率。
        foreach (var attributes in tables.TbHeroGrowthConfig.DataList.SelectMany(x => x.Levels).Select(x => x.BaseAttributes)
                     .Concat(tables.TbMonsterConfig.DataList.Select(x => x.BaseAttributes)))
            foreach (AttributeType attribute in Enum.GetValues<AttributeType>())
                if (attribute.IsProbability && (!float.IsFinite(attributes.Get(attribute)) || attributes.Get(attribute) is < 0 or > 1))
                    errors.Add($"基础属性 {attribute} 必须为 0..1 的有限概率。");

        // 跨表数值引用在预加载时集中校验；场景装配由其拥有者校验。
        ValidateDrops(tables, errors);
        ValidateEquipment(tables, errors);
        ValidateSkillCatalog(tables.TbSkillConfig, tables.TbPassiveConfig, errors);
        ValidateSkillCombat(tables, errors);
        if (!tables.TbLevelConfig.DataList.Select(level => level.LevelId).OrderBy(id => id)
                .SequenceEqual(Enumerable.Range(1, tables.TbLevelConfig.DataList.Count)))
            errors.Add("关卡主键必须从 1 连续递增，供逐关解锁与进度校验使用。");
        foreach (var level in tables.TbLevelConfig.DataList)
        {
            foreach (var stage in level.Stages)
            {
                foreach (var recipe in stage.Recipes)
                {
                    if (tables.TbMonsterConfig.DataList.Count(x => x.EntityId == recipe.MonsterEntityId) != 1)
                        errors.Add(
                            $"关卡 {level.LevelId} 阶段 {stage.StageOrder}：怪物 {recipe.MonsterEntityId} 必须关联唯一基础配置。");
                    if (!float.IsFinite(recipe.HealthMultiplier) || recipe.HealthMultiplier <= 0f ||
                        !float.IsFinite(recipe.DefenseMultiplier) || recipe.DefenseMultiplier < 0f ||
                        !float.IsFinite(recipe.AttackMultiplier) || recipe.AttackMultiplier < 0f ||
                        recipe.ExperienceOverride < -1)
                        errors.Add(
                            $"关卡 {level.LevelId} 配方 {recipe.SpawnPointId}：生命倍率必须为有限正数，防御/攻击倍率须有限且非负，经验覆盖须至少为 -1。");
                }

            }

        }

        var battle = tables.TbBattleConfig.Data;
        if (!float.IsFinite(battle.MonsterKnockbackRetentionPerSecond)
            || battle.MonsterKnockbackRetentionPerSecond <= 0f || battle.MonsterKnockbackRetentionPerSecond > 1f)
            errors.Add("战斗常数：怪物击退每秒保留比例须在 (0,1] 内。");
        if (battle.WsMax <= 0 || !float.IsFinite(battle.WsDrainPerSecond) || battle.WsDrainPerSecond <= 0f
            || !float.IsFinite(battle.WsGainPerDamageRatio) || battle.WsGainPerDamageRatio <= 0f
            || !float.IsFinite(battle.WsDecayPerSecond) || battle.WsDecayPerSecond <= 0f
            || !float.IsFinite(battle.WsAttackMultiplier) || battle.WsAttackMultiplier < 1f
            || !float.IsFinite(battle.WsMoveSpeedMultiplier) || battle.WsMoveSpeedMultiplier < 1f
            || !float.IsFinite(battle.WsJumpSpeedMultiplier) || battle.WsJumpSpeedMultiplier < 1f
            || !float.IsFinite(battle.WsGravityMultiplier) || battle.WsGravityMultiplier < 1f)
        {
            errors.Add("战斗常数：无双上限、每秒衰减/消耗、蓄力系数须为正；攻击、移速、跳跃和重力倍率须有限且至少为 1。");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException("配置校验失败：\n" + string.Join("\n", errors));
        }

        return curve;
    }

    /// <summary>在预加载边界校验掉落数量、概率与奖励，运行时直接消费。</summary>
    /// <param name="tables">只读配置总表。</param>
    /// <param name="errors">集中错误列表。</param>
    private static void ValidateDrops(Tables tables, List<string> errors)
    {
        var drop = tables.TbBattleConfig.Data;
        if (!float.IsFinite(drop.GroundLifetime) || drop.GroundLifetime <= 0)
            errors.Add("地面掉落：存在秒数必须为有限正数。");
        float chance = 0;
        foreach (var pickup in tables.TbPickupConfig.DataList)
        {
            if (pickup.Id == PickupId.None || !Enum.IsDefined(pickup.Effect) || pickup.RestoreAmount <= 0 ||
                !float.IsFinite(pickup.DropChance) || pickup.DropChance is < 0 or > 1)
                errors.Add($"恢复品 {pickup.Id}：恢复量或概率非法。");
            chance += pickup.DropChance;
        }

        if (chance > 1)
            errors.Add("恢复品互斥掉落概率总和不能超过 1。");
        if (drop.MusouPerOrb <= 0 || drop.ManaMinimum < 0 ||
            !float.IsFinite(drop.ManaMaxFraction) || drop.ManaMaxFraction < 0f)
            errors.Add("掉落：无双收益与回蓝数值非法。");
        foreach (var monster in tables.TbMonsterConfig.DataList)
        {
            if (!float.IsFinite(monster.EquipmentDropChance) || monster.EquipmentDropChance is < 0 or > 1 ||
                monster.EquipmentDropPoolId != EquipmentDropPoolId.None &&
                tables.TbEquipmentDropPoolConfig.GetOrDefault(monster.EquipmentDropPoolId) == null)
                errors.Add($"怪物 {monster.Id}：装备概率或基础装备池非法。");
            if (monster.SoulPerOrb < 0 || monster.SoulCount.X < 0 || monster.SoulCount.Y < monster.SoulCount.X ||
                !float.IsFinite(monster.ManaDropChance) || monster.ManaDropChance is < 0f or > 1f ||
                monster.ManaCount.X < 1 || monster.ManaCount.Y < monster.ManaCount.X ||
                !float.IsFinite(monster.MusouDropChance) || monster.MusouDropChance is < 0f or > 1f ||
                monster.MusouCount.X < 1 || monster.MusouCount.Y < monster.MusouCount.X)
                errors.Add($"怪物 {monster.Id}：灵魂数量和蓝球/无双球概率、数量配置非法。");
        }
    }

    /// <summary>校验装备定义、实例随机范围和掉落池外键，不等到实际掉落时才报错。</summary>
    /// <param name="tables">只读配置总表。</param>
    /// <param name="errors">集中错误列表。</param>
    private static void ValidateEquipment(Tables tables, List<string> errors)
    {
        if (tables.TbProfileConfig.EquipmentCapacity is <= 0 or > 75)
            errors.Add("背包容量必须在当前三页、每页 25 格的界面范围内。");
        foreach (var item in tables.TbEquipmentConfig.DataList)
        {
            if (item.Id == EquipmentId.None || !Enum.IsDefined(item.Owner) || !Enum.IsDefined(item.Slot) ||
                !Enum.IsDefined(item.Quality) || item.SellPrice < 0 || string.IsNullOrWhiteSpace(item.NameCn) ||
                item.AttributeRanges.Count == 0)
                errors.Add($"装备 {item.Id}：身份、角色、部位、品质或资源非法。");
            HashSet<(GameConfig.Attributes.AttributeType, GameConfig.Attributes.AttributeModifierKind)> attributes =
                new();
            foreach (var roll in item.AttributeRanges)
                if (!Enum.IsDefined(roll.Attribute) ||
                    roll.Kind != GameConfig.Attributes.AttributeModifierKind.Flat ||
                    !float.IsFinite(roll.Minimum) || !float.IsFinite(roll.Maximum) || roll.Minimum < 0 ||
                    roll.Maximum < roll.Minimum ||
                    (roll.Attribute.IsProbability ? roll.Maximum > 1
                        : roll.Minimum != MathF.Truncate(roll.Minimum) || roll.Maximum != MathF.Truncate(roll.Maximum)) ||
                    !attributes.Add((roll.Attribute, roll.Kind)))
                    errors.Add($"装备 {item.Id}：随机范围非法或属性重复。");
        }

        foreach (var pool in tables.TbEquipmentDropPoolConfig.DataList)
        {
            long total = 0;
            if (pool.Id == EquipmentDropPoolId.None || pool.Entries.Count == 0)
                errors.Add($"装备池 {pool.Id}：编号或条目非法。");
            foreach (var entry in pool.Entries)
            {
                total += entry.Weight;
                if (entry.Weight <= 0 || tables.TbEquipmentConfig.GetOrDefault(entry.EquipmentId) == null)
                    errors.Add($"装备池 {pool.Id}：权重须为正且装备必须存在。");
            }

            if (total > int.MaxValue)
                errors.Add($"装备池 {pool.Id}：总权重溢出。");
        }
    }

    /// <summary>校验每名英雄的成长行覆盖经验表全部等级且属性生命上限为正。</summary>
    /// <param name="tables">配置总表。</param>
    /// <param name="maxLevel">经验表最高等级。</param>
    /// <param name="errors">错误汇总。</param>
    private static void ValidateGrowth(Tables tables, int maxLevel, List<string> errors)
    {
        // 身份只在外层校验一次；列表按等级排列，运行期可以直接用等级下标读取。
        foreach (HeroGrowthConfig growth in tables.TbHeroGrowthConfig.DataList)
        {
            if (tables.TbHeroConfig.GetOrDefault(growth.HeroId) == null)
            {
                errors.Add($"成长表：英雄 {growth.HeroId} 不在 HeroConfig 中。");
            }

            if (growth.Levels.Count != maxLevel ||
                !growth.Levels.Select(row => row.Level).SequenceEqual(Enumerable.Range(1, maxLevel)))
            {
                errors.Add($"成长表：英雄 {growth.HeroId} 必须按顺序完整覆盖 1..{maxLevel} 级。");
            }

            foreach (var row in growth.Levels)
            {
                if (row.BaseAttributes.MaxHp <= 0)
                    errors.Add($"成长表：英雄 {growth.HeroId} 的 {row.Level} 级 MaxHp 必须为正。");
            }
        }

        foreach (HeroConfig hero in tables.TbHeroConfig.DataList)
        {
            int comboLength = tables.TbAttackConfig.DataList.Count(attack => attack.OwnerId == hero.EntityId);
            if (hero.AttackInputCooldowns.Length != comboLength ||
                hero.AttackInputCooldowns.Any(value => !float.IsFinite(value) || value <= 0f) ||
                !float.IsFinite(hero.AirAttackCooldown) || hero.AirAttackCooldown <= 0f ||
                !float.IsFinite(hero.ComboResetTime) || hero.ComboResetTime <= 0f ||
                !float.IsFinite(hero.PlatformDropDistance) || hero.PlatformDropDistance <= 0f)
            {
                errors.Add($"英雄 {hero.Id}：普攻冷却数量须等于段数，冷却、连段超时和穿平台距离须为有限正数。");
            }

            if (tables.TbHeroGrowthConfig.GetOrDefault(hero.Id) == null)
                errors.Add($"成长表：英雄 {hero.Id} 缺少等级列表。");
        }
    }

    /// <summary>校验统一攻击数值与技能平衡参数，不解释动画或场景。</summary>
    /// <param name="tables">正式配置总表。</param>
    /// <param name="errors">启动错误集合。</param>
    internal static void ValidateSkillCombat(Tables tables, ICollection<string> errors)
    {
        foreach (var hit in tables.TbAttackConfig.DataList)
        {
            if (hit.Id <= 0 || !Enum.IsDefined(hit.DamageKind) ||
                !hit.PowerScale.IsFinite() || hit.PowerScale.X < 0 || hit.PowerScale.Y < hit.PowerScale.X ||
                !hit.FlatPower.IsFinite() || hit.FlatPower.X < 0 || hit.FlatPower.Y < hit.FlatPower.X ||
                !hit.Knockback.IsFinite() || hit.HitBudget <= 0 ||
                !float.IsFinite(hit.RepeatIntervalSeconds) || hit.RepeatIntervalSeconds < 0)
                errors.Add($"攻击 {hit.Id}：威力、击退、总命中预算或重复间隔无效。");
        }
        foreach (var hit in Enum.GetValues<SkillHitId>())
            if (hit != SkillHitId.None && tables.TbAttackConfig.GetOrDefault((int)hit) == null)
                errors.Add($"技能伤害 {hit}：统一攻击表缺少定义。");
        foreach (var skill in tables.TbSkillConfig.DataList)
            if (!float.IsFinite(skill.InvincibleDurationSeconds) || skill.InvincibleDurationSeconds < 0 ||
                !float.IsFinite(skill.NormalAttackBlockSeconds) || skill.NormalAttackBlockSeconds < 0 ||
                !float.IsFinite(skill.CancelNormalAttackFrameRatio) || skill.CancelNormalAttackFrameRatio is < 0 or > 1)
                errors.Add($"技能 {skill.Id}：无敌、普攻冷却或取消比例无效。");
    }

    /// <summary>集中检查面板身份、行序、费用和被动等级覆盖。</summary>
    /// <param name="active">主动技能表。</param>
    /// <param name="passive">被动技能等级表。</param>
    /// <param name="errors">启动校验汇总错误。</param>
    internal static void ValidateSkillCatalog(TbSkillConfig active, TbPassiveConfig passive, ICollection<string> errors)
    {
        foreach (SkillHero hero in Enum.GetValues<SkillHero>())
        {
            var skills = active.DataList.Where(x => x.Hero == hero).OrderBy(x => x.Order).ToArray();
            if (skills.Length != 5 || !skills.Select(x => x.Order).SequenceEqual(Enumerable.Range(1, 5)))
                errors.Add($"技能 {hero}：造一面板必须有连续的五行主动技能。");
        }

        foreach (var skill in active.DataList)
        {
            if (!Enum.IsDefined(skill.Hero) || skill.LearnSoul < 0 || skill.ManaCost < 0 ||
                string.IsNullOrWhiteSpace(skill.NameCn) || string.IsNullOrWhiteSpace(skill.Description) ||
                string.IsNullOrWhiteSpace(skill.LegacyId))
                errors.Add($"技能 {skill.Id}：身份、费用或文字无效。");
        }

        foreach (PassiveId id in Enum.GetValues<PassiveId>())
        {
            var definition = passive.GetOrDefault(id);
            if (definition == null || !definition.Levels.Select(x => x.Level).SequenceEqual(Enumerable.Range(1, 5)))
                errors.Add($"被动 {id}：必须按顺序完整覆盖 1..5 级。");
        }

        if (!passive.DataList.Select(definition => definition.PassiveId).SequenceEqual(Enum.GetValues<PassiveId>()))
            errors.Add("被动目录必须按官方编号顺序排列。");
        foreach (var definition in passive.DataList)
        {
            if (!Enum.IsDefined(definition.PassiveId) || !Enum.IsDefined(definition.Effect) ||
                string.IsNullOrWhiteSpace(definition.NameCn) || string.IsNullOrWhiteSpace(definition.LegacyId))
                errors.Add($"被动 {definition.PassiveId}：身份、效果类型或名称无效。");
            foreach (var level in definition.Levels)
                if (level.LearnSoul < 0 || !float.IsFinite(level.Value) || level.Value < 0 ||
                    (definition.Effect == PassiveEffect.CriticalChance && level.Value > 1) ||
                    string.IsNullOrWhiteSpace(level.Description))
                    errors.Add($"被动 {definition.PassiveId}/{level.Level}：费用、效果或文字无效。");
        }
    }

    /// <summary>首次加载同类英雄时建立参数，后续池实例复用。</summary>
    /// <param name="heroId">英雄配置主键。</param>
    /// <param name="create">从当前场景动画读取纯数据的工厂；模型不保留委托。</param>
    public void EnsureHeroBodyParams(int heroId, Func<HeroBodyParams> create)
    {
        if (!this.GetModel<ConfigurationModel>().HeroBodies.ContainsKey(heroId))
            this.GetModel<ConfigurationModel>().HeroBodies.Add(heroId, create());
    }

    /// <summary>首次加载同类怪物时建立参数，后续池实例复用。</summary>
    /// <param name="entityId">怪物实体类型。</param>
    /// <param name="create">从当前场景动画读取纯数据的工厂；模型不保留委托。</param>
    public void EnsureEnemyBodyParams(EntityId entityId, Func<EnemyBodyParams> create)
    {
        if (!this.GetModel<ConfigurationModel>().EnemyBodies.ContainsKey(entityId))
            this.GetModel<ConfigurationModel>().EnemyBodies.Add(entityId, create());
    }
}
