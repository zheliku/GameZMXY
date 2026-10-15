using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig;
using GameConfig.Hero;
using GameConfig.Monster;
using GameConfig.Sound;
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
        var configuration = this.GetModel<ConfigurationModel>();
        ValidateTables(configuration.Tables, configuration.ExperienceCurve);
    }

    /// <summary>校验全部启动约束，汇总所有错误后一次抛出。</summary>
    /// <param name="tables">已加载的配置总表。</param>
    /// <param name="curve">模型已构建的共享曲线；独立校验未提供时才构建。</param>
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

        // 关卡音乐由关卡表声明，避免新关卡仍播放花果山硬编码音乐。
        ValidateDrops(tables, errors);
        ValidateEquipment(tables, errors);
        ValidateSkillCatalog(tables.TbSkillConfig, tables.TbPassiveConfig, errors);
        ValidateSkillCombat(tables, errors);
        foreach (var level in tables.TbLevelConfig.DataList)
        {
            foreach (var stage in level.Stages)
            {
                foreach (var recipe in stage.Recipes)
                {
                    if (recipe.EquipmentDropPoolOverride != EquipmentDropPoolId.UseMonster &&
                        recipe.EquipmentDropPoolOverride != EquipmentDropPoolId.None &&
                        tables.TbEquipmentDropPoolConfig.GetOrDefault(recipe.EquipmentDropPoolOverride) == null)
                        errors.Add($"关卡 {level.LevelId} 配方 {recipe.SpawnPointId}：装备掉落池不存在。");
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

                if (stage.FeaturedOpponentId == GameConfig.Entity.EntityId.None)
                    continue;
                if (stage.Recipes.Where(x => x.MonsterEntityId == stage.FeaturedOpponentId).Sum(x => x.Count) != 1)
                    errors.Add($"关卡 {level.LevelId} 阶段 {stage.StageOrder}：守关展示必须唯一关联一只本阶段怪物。");
            }

            if (level.MusicId == SoundId.None)
                continue;
            var music = tables.TbSoundConfig.GetOrDefault((int)level.MusicId);
            if (music == null || music.Group != "Music")
                errors.Add($"关卡 {level.LevelId}：MusicId={level.MusicId} 必须引用 Music 组音效配置。");
        }

        if (!float.IsFinite(tables.TbBattleConfig.DeathSettlementDelay) ||
            tables.TbBattleConfig.DeathSettlementDelay < 0f ||
            !float.IsFinite(tables.TbBattleConfig.MonsterHealthBarFadeDuration) ||
            tables.TbBattleConfig.MonsterHealthBarFadeDuration <= 0f)
        {
            errors.Add("战斗常数：死亡等待必须非负，小怪血条淡出秒数必须为正，且均为有限数。");
        }

        var battle = tables.TbBattleConfig.Data;
        if (battle.WsMax <= 0 || !float.IsFinite(battle.WsDrainPerSecond) || battle.WsDrainPerSecond <= 0f
            || !float.IsFinite(battle.WsIntroPauseDuration) || battle.WsIntroPauseDuration <= 0f
            || !float.IsFinite(battle.WsGainPerDamageRatio) || battle.WsGainPerDamageRatio <= 0f
            || !float.IsFinite(battle.WsDecayPerSecond) || battle.WsDecayPerSecond <= 0f
            || !float.IsFinite(battle.WsAttackMultiplier) || battle.WsAttackMultiplier < 1f
            || !float.IsFinite(battle.WsMoveSpeedMultiplier) || battle.WsMoveSpeedMultiplier < 1f
            || !float.IsFinite(battle.WsJumpSpeedMultiplier) || battle.WsJumpSpeedMultiplier < 1f
            || !float.IsFinite(battle.WsGravityMultiplier) || battle.WsGravityMultiplier < 1f)
        {
            errors.Add("战斗常数：无双上限、每秒衰减/消耗、蓄力系数和开启停顿须为正；攻击、移速、跳跃和重力倍率须有限且至少为 1。");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException("配置校验失败：\n" + string.Join("\n", errors));
        }

        return curve;
    }

    /// <summary>在预加载边界校验掉落数量、概率与飞行参数，运行时直接消费。</summary>
    /// <param name="tables">只读配置总表。</param>
    /// <param name="errors">集中错误列表。</param>
    private static void ValidateDrops(Tables tables, List<string> errors)
    {
        var drop = tables.TbDropConfig.Data;
        if (!float.IsFinite(drop.GroundLifetime) || drop.GroundLifetime <= 0 ||
            !float.IsFinite(drop.GroundGravity) || drop.GroundGravity <= 0 ||
            !float.IsFinite(drop.GroundPickupRadius) || drop.GroundPickupRadius <= 0)
            errors.Add("地面掉落：存在秒数、重力与接触半径必须为有限正数。");
        float chance = 0;
        foreach (var pickup in tables.TbPickupConfig.DataList)
        {
            if (pickup.Id == PickupId.None || !Enum.IsDefined(pickup.Effect) || pickup.RestoreAmount <= 0 ||
                !float.IsFinite(pickup.DropChance) || pickup.DropChance is < 0 or > 1 ||
                !pickup.IconPath.StartsWith("res://TheGame/", StringComparison.Ordinal))
                errors.Add($"恢复品 {pickup.Id}：恢复量、概率或资源路径非法。");
            chance += pickup.DropChance;
        }

        if (chance > 1)
            errors.Add("恢复品互斥掉落概率总和不能超过 1。");
        if (drop.MusouPerOrb <= 0 ||
            drop.ManaMinimum < 0 || !float.IsFinite(drop.ManaMaxFraction) || drop.ManaMaxFraction < 0f ||
            !float.IsFinite(drop.ScatterRadius) || drop.ScatterRadius < 0f ||
            !float.IsFinite(drop.Delay) || drop.Delay < 0f ||
            !float.IsFinite(drop.RiseDuration) || drop.RiseDuration <= 0f ||
            !drop.RiseDistance.IsFinite() || drop.RiseDistance.X < 0f ||
            drop.RiseDistance.Y < drop.RiseDistance.X ||
            !float.IsFinite(drop.InitialSpeed) || drop.InitialSpeed <= 0f ||
            !float.IsFinite(drop.Acceleration) || drop.Acceleration < 0f ||
            !float.IsFinite(drop.MaxSpeed) || drop.MaxSpeed < drop.InitialSpeed ||
            !float.IsFinite(drop.PickupRadius) || drop.PickupRadius <= 0f)
            errors.Add("掉落：数量、回蓝、散布、时间、速度与接触半径配置非法。");
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
                !item.IconPath.StartsWith("res://TheGame/", StringComparison.Ordinal) ||
                !item.GroundIconPath.StartsWith("res://TheGame/", StringComparison.Ordinal) ||
                item.AttributeRanges.Count == 0)
                errors.Add($"装备 {item.Id}：身份、角色、部位、品质或资源非法。");
            HashSet<(GameConfig.Attributes.AttributeType, GameConfig.Attributes.AttributeModifierKind)> attributes =
                new();
            foreach (var roll in item.AttributeRanges)
                if (!Enum.IsDefined(roll.Attribute) ||
                    roll.Kind != GameConfig.Attributes.AttributeModifierKind.Flat || roll.Minimum < 0 ||
                    roll.Maximum < roll.Minimum ||
                    !attributes.Add((roll.Attribute, roll.Kind)))
                    errors.Add($"装备 {item.Id}：随机范围非法或属性重复。");
        }

        foreach (var pool in tables.TbEquipmentDropPoolConfig.DataList)
        {
            long total = 0;
            if (pool.Id is EquipmentDropPoolId.None or EquipmentDropPoolId.UseMonster || pool.Entries.Count == 0)
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
        // 先逐行检查外键、等级范围与生命上限，再按英雄逐级检查缺失（联合索引保证不重复）。
        foreach (HeroGrowthConfig row in tables.TbHeroGrowthConfig.DataList)
        {
            if (tables.TbHeroConfig.GetOrDefault(row.HeroId) == null)
            {
                errors.Add($"成长表：英雄 {row.HeroId} 不在 HeroConfig 中。");
            }

            if (row.Level < 1 || row.Level > maxLevel)
            {
                errors.Add($"成长表：英雄 {row.HeroId} 的等级 {row.Level} 超出 1..{maxLevel}。");
            }

            if (row.BaseAttributes.MaxHp <= 0)
            {
                errors.Add($"成长表：英雄 {row.HeroId} 的 {row.Level} 级 MaxHp 必须为正。");
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

            for (int level = 1; level <= maxLevel; level++)
            {
                if (tables.TbHeroGrowthConfig.Get(hero.Id, level) == null)
                {
                    errors.Add($"成长表：英雄 {hero.Id} 缺少 {level} 级。");
                    break;
                }
            }
        }
    }

    /// <summary>汇总已准备技能的资源声明、秒制时间、威力和跨表引用错误。</summary>
    /// <param name="tables">与游戏共用的正式配置表。</param>
    /// <param name="errors">预加载阶段持有的错误集合。</param>
    internal static void ValidateSkillCombat(Tables tables, ICollection<string> errors)
    {
        // 伤害键独立于动作；火魔三个阶段和唐僧追加释放各有明确的定义。
        foreach (var hit in tables.TbSkillHitConfig.DataList)
        {
            if (hit.Id == SkillHitId.None || !Enum.IsDefined(hit.Id) ||
                tables.TbSkillConfig.GetOrDefault(hit.SkillId) == null ||
                string.IsNullOrWhiteSpace(hit.NameCn) || string.IsNullOrWhiteSpace(hit.LegacyId) ||
                !Enum.IsDefined(hit.PowerRounding) || !Enum.IsDefined(hit.DamageKind) ||
                !IsNonnegative(hit.PowerScale) || !IsNonnegative(hit.FlatPower) ||
                hit.PowerScale + hit.FlatPower <= 0 || !IsFinite(hit.Knockback) ||
                hit.HitBudget <= 0 || !float.IsFinite(hit.RepeatIntervalSeconds) || hit.RepeatIntervalSeconds <= 0)
                errors.Add($"技能伤害 {hit.Id}：身份、威力、击退、总命中预算或重复间隔无效。");
        }

        // 本轮只准备悟空动作；唐僧目录与数值可存在，而不伪造尚未迁入的资源。
        foreach (var skill in tables.TbSkillConfig.DataList.Where(x => x.Hero == SkillHero.Wukong))
            if (tables.TbSkillActionConfig.GetOrDefault(skill.Id) == null)
                errors.Add($"技能 {skill.Id}：缺少本轮要求的悟空动作定义。");
        foreach (var action in tables.TbSkillActionConfig.DataList)
        {
            var skill = tables.TbSkillConfig.GetOrDefault(action.SkillId);
            if (skill == null || action.NameCn != skill.NameCn || string.IsNullOrWhiteSpace(action.LegacyId) ||
                !IsResourcePath(action.AnimationLibraryPath, ".tres") || string.IsNullOrWhiteSpace(action.Animation) ||
                !IsNonnegative(action.InvincibleDelaySeconds) || !IsNonnegative(action.InvincibleDurationSeconds) ||
                !IsNonnegative(action.CancelNormalAttackFrameRatio) || action.CancelNormalAttackFrameRatio > 1 ||
                (action.InvincibleDurationSeconds == 0 && action.InvincibleDelaySeconds != 0))
                errors.Add($"技能动作 {action.SkillId}：身份、动画资源或无敌时间无效。");
            ValidateHitReference(tables, action.BodyHitId, action.SkillId, $"技能动作 {action.SkillId}", errors);
            ValidateSound(tables, action.SoundId, $"技能动作 {action.SkillId}", errors);
        }

        foreach (var effect in tables.TbSkillEffectConfig.DataList)
        {
            if (effect.Id == SkillEffectId.None || !Enum.IsDefined(effect.Id) ||
                tables.TbSkillActionConfig.GetOrDefault(effect.SkillId) == null ||
                string.IsNullOrWhiteSpace(effect.NameCn) || string.IsNullOrWhiteSpace(effect.LegacyId) ||
                !IsResourcePath(effect.ScenePath, ".tscn") || !IsFinite(effect.SpawnOffset) ||
                !IsPositiveScale(effect.Scale) || !IsPositiveScale(effect.MusouScale) || !Enum.IsDefined(effect.Mirror))
                errors.Add($"技能特效 {effect.Id}：身份、场景、偏移、缩放或朝向无效。");
            ValidateHitReference(tables, effect.HitId, effect.SkillId, $"技能特效 {effect.Id}", errors);
            ValidateSound(tables, effect.SoundId, $"技能特效 {effect.Id}", errors);
        }

        ValidateTimelines(tables, errors);
    }

    /// <summary>检查准备时间轴的所有者、局部秒数、分支与各类事件的具名参数。</summary>
    /// <param name="tables">正式技能表。</param>
    /// <param name="errors">启动错误集合。</param>
    private static void ValidateTimelines(Tables tables, ICollection<string> errors)
    {
        // 时间以角色或独立特效为原点；同时间的多条事件保持源表顺序。
        foreach (var group in tables.TbSkillTimelineConfig.DataList.GroupBy(x => (x.SkillId, x.SourceEffectId)))
        {
            float previous = 0;
            foreach (var entry in group)
            {
                if (entry.AtSeconds < previous)
                    errors.Add($"技能事件 {entry.Id}：所属时间轴必须按秒数递增排列。");
                previous = entry.AtSeconds;
            }
        }

        foreach (var action in tables.TbSkillActionConfig.DataList)
            if (!tables.TbSkillTimelineConfig.DataList.Any(x => x.SkillId == action.SkillId &&
                                                                x.SourceEffectId == SkillEffectId.None &&
                                                                x.Kind == SkillTimelineKind.CompleteActor))
                errors.Add($"技能动作 {action.SkillId}：缺少角色时间轴结束事件。");

        foreach (var entry in tables.TbSkillTimelineConfig.DataList)
        {
            if (tables.TbSkillActionConfig.GetOrDefault(entry.SkillId) == null ||
                string.IsNullOrWhiteSpace(entry.LegacyId) ||
                !Enum.IsDefined(entry.Kind) || !Enum.IsDefined(entry.Condition) || !Enum.IsDefined(entry.Direction) ||
                !Enum.IsDefined(entry.Anchor) || !IsNonnegative(entry.AtSeconds) ||
                !IsNonnegative(entry.UntilSeconds) ||
                !IsNonnegative(entry.SeekSeconds) || !IsNonnegative(entry.XMultiplier) ||
                !IsNonnegative(entry.TargetRange) || !IsNonnegative(entry.ViewportInset) ||
                !IsNonnegative(entry.ActionLockSeconds) ||
                !IsFinite(entry.Velocity) || !IsFinite(entry.MusouVelocity) || entry.Velocity.X < 0 ||
                entry.MusouVelocity.X < 0)
                errors.Add($"技能事件 {entry.Id}：身份、局部秒数、条件或位移参数无效。");
            ValidateEffectReference(tables, entry.SourceEffectId, entry.SkillId, entry.Id, errors);
            ValidateEffectReference(tables, entry.EffectId, entry.SkillId, entry.Id, errors);

            bool spawn = entry.Kind is SkillTimelineKind.SpawnEffect or SkillTimelineKind.SpawnLandingAndPauseActor;
            bool seek = entry.Kind is SkillTimelineKind.SeekActor or SkillTimelineKind.FinishLandingEffect;
            bool search = entry.Kind == SkillTimelineKind.SearchTarget;
            bool landing = entry.Kind == SkillTimelineKind.SpawnLandingAndPauseActor;
            bool motion = entry.Kind is SkillTimelineKind.SetVelocity or SkillTimelineKind.SetVelocityX;
            if (spawn != (entry.EffectId != SkillEffectId.None) || seek != (entry.SeekSeconds > 0) ||
                search != (entry.TargetRange > 0 && entry.ViewportInset > 0) ||
                (!search && (entry.TargetRange != 0 || entry.ViewportInset != 0)) ||
                (landing
                    ? entry.UntilSeconds < entry.AtSeconds || entry.Condition != SkillTimelineCondition.OnFloor
                    : entry.UntilSeconds != 0) ||
                (entry.Kind == SkillTimelineKind.MultiplyVelocityX ? entry.XMultiplier <= 0 : entry.XMultiplier != 0) ||
                (!motion && (entry.Velocity != Vector2.Zero || entry.MusouVelocity != Vector2.Zero)) ||
                (entry.Kind != SkillTimelineKind.TeleportToTarget && entry.ActionLockSeconds != 0))
                errors.Add($"技能事件 {entry.Id}：参数与事件职责不匹配。");
            if ((entry.Kind is SkillTimelineKind.CompleteEffect or SkillTimelineKind.FinishLandingEffect) &&
                entry.SourceEffectId == SkillEffectId.None)
                errors.Add($"技能事件 {entry.Id}：独立特效结束必须声明所属特效。");
        }
    }

    /// <summary>保证事件的来源或生成特效属于同一技能。</summary>
    /// <param name="tables">正式配置。</param>
    /// <param name="effectId">来源或生成特效，None 表示角色或不生成。</param>
    /// <param name="skillId">事件所属技能。</param>
    /// <param name="eventId">错误信息中的事件键。</param>
    /// <param name="errors">启动错误集合。</param>
    private static void ValidateEffectReference(Tables tables, SkillEffectId effectId, SkillId skillId,
        string eventId, ICollection<string> errors)
    {
        if (effectId == SkillEffectId.None)
            return;
        var effect = tables.TbSkillEffectConfig.GetOrDefault(effectId);
        if (effect == null || effect.SkillId != skillId)
            errors.Add($"技能事件 {eventId}：特效 {effectId} 不存在或不属于该技能。");
    }

    /// <summary>校验可选伤害引用属于同一技能，防止动作与特效误用其他招式的伤害。</summary>
    /// <param name="tables">正式配置表。</param>
    /// <param name="hitId">None 表示当前来源只负责表现。</param>
    /// <param name="skillId">拥有该动作或特效的技能。</param>
    /// <param name="owner">错误信息中的来源名称。</param>
    /// <param name="errors">启动错误集合。</param>
    private static void ValidateHitReference(Tables tables, SkillHitId hitId, SkillId skillId,
        string owner, ICollection<string> errors)
    {
        if (hitId == SkillHitId.None)
            return;
        var hit = tables.TbSkillHitConfig.GetOrDefault(hitId);
        if (hit == null || hit.SkillId != skillId)
            errors.Add($"{owner}：伤害阶段 {hitId} 不存在或不属于该技能。");
    }

    /// <summary>技能声音必须引用 GGF 的 SFX 组；None 不创建播放请求。</summary>
    /// <param name="tables">正式配置表。</param>
    /// <param name="soundId">配置声音标识。</param>
    /// <param name="owner">错误信息中的来源名称。</param>
    /// <param name="errors">启动错误集合。</param>
    private static void ValidateSound(Tables tables, SoundId soundId, string owner, ICollection<string> errors)
    {
        if (soundId == SoundId.None)
            return;
        var sound = tables.TbSoundConfig.GetOrDefault((int)soundId);
        if (sound == null || sound.Group != "SFX")
            errors.Add($"{owner}：声音 {soundId} 必须引用 GGF SFX 配置。");
    }

    /// <summary>检查资源路径的协议和类型；资源能否加载由引擎装配验证负责。</summary>
    /// <param name="path">表中声明的资源路径。</param>
    /// <param name="extension">要求的文件扩展名。</param>
    /// <returns>是否是对应类型的 Godot 项目资源路径。</returns>
    private static bool IsResourcePath(string path, string extension) =>
        path.StartsWith("res://", StringComparison.Ordinal) && path.EndsWith(extension, StringComparison.Ordinal);

    /// <summary>检查有限非负的秒数或威力。</summary>
    /// <param name="value">待检查值。</param>
    /// <returns>值是否有限且非负。</returns>
    private static bool IsNonnegative(float value) => float.IsFinite(value) && value >= 0;

    /// <summary>检查向量两个分量均为有限值。</summary>
    /// <param name="value">偏移或击退向量。</param>
    /// <returns>是否不含非数值与无穷大。</returns>
    private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);

    /// <summary>检查独立特效的实际缩放，镜像由独立朝向字段表达。</summary>
    /// <param name="value">普通或无双缩放。</param>
    /// <returns>两个分量是否均为有限正数。</returns>
    private static bool IsPositiveScale(Vector2 value) => IsFinite(value) && value.X > 0 && value.Y > 0;

    /// <summary>集中检查面板身份、行序、费用、资源路径和被动等级覆盖。</summary>
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
                string.IsNullOrWhiteSpace(skill.LegacyId) || string.IsNullOrWhiteSpace(skill.KeyHint) ||
                !skill.UnlearnedIconPath.StartsWith("res://", StringComparison.Ordinal) ||
                !skill.LearnedIconPath.StartsWith("res://", StringComparison.Ordinal))
                errors.Add($"技能 {skill.Id}：身份、费用、文字或图标路径无效。");
        }

        foreach (PassiveId id in Enum.GetValues<PassiveId>())
        {
            var levels = passive.DataList.Where(x => x.PassiveId == id).OrderBy(x => x.Level).ToArray();
            if (levels.Length != 5 || !levels.Select(x => x.Level).SequenceEqual(Enumerable.Range(1, 5)))
                errors.Add($"被动 {id}：必须完整覆盖 1..5 级。");
            if (levels.Select(x => (x.NameCn, x.Effect)).Distinct().Count() != 1)
                errors.Add($"被动 {id}：各级名称和效果类型必须一致。");
        }

        foreach (var level in passive.DataList)
        {
            if (!Enum.IsDefined(level.PassiveId) || !Enum.IsDefined(level.Effect) || level.LearnSoul < 0 ||
                !float.IsFinite(level.Value) || level.Value < 0 ||
                (level.Effect == PassiveEffect.CriticalChance && level.Value > 1) ||
                string.IsNullOrWhiteSpace(level.NameCn) || string.IsNullOrWhiteSpace(level.Description))
                errors.Add($"被动 {level.PassiveId}/{level.Level}：费用、效果或文字无效。");
        }
    }
}
