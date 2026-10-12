using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig;
using GameConfig.Hero;
using GameConfig.Monster;
using GameConfig.Sound;
using GameLogic.Profile;

namespace GameLogic.Config
{
	/// <summary>
	/// 启动期集中校验跨表约束（预加载流程调用一次，失败即停止启动）。
	/// 校验经验曲线、成长覆盖、建档规则、怪物属性与掉落数值，避免战斗中途才暴露配置错误。
	/// 关卡与场景装配的约束仍由各自的初始化边界（LevelModule 等）校验。
	/// </summary>
	public static class ConfigHelper
	{
		/// <summary>校验全部启动约束，汇总所有错误后一次抛出。</summary>
		/// <param name="tables">已加载的配置总表。</param>
		/// <returns>经过校验的共享经验曲线（供档案与存档使用，避免重复构建）。</returns>
		/// <exception cref="ArgumentNullException">总表为空。</exception>
		/// <exception cref="InvalidOperationException">任一约束不满足；消息列出全部问题。</exception>
		public static ExperienceCurve ValidateAll(Tables tables)
		{
			ArgumentNullException.ThrowIfNull(tables);
			List<string> errors = new();

			// 经验曲线：构造函数内校验等级连续、末级为零、累计不溢出。
			ExperienceCurve curve = null;
			try
			{
				curve = new ExperienceCurve(tables.TbHeroLevelConfig.DataList);
			}
			catch (ArgumentException exception)
			{
				errors.Add($"经验表：{exception.Message}");
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
				if (monster.Stats.MaxHp <= 0 || monster.AddExp < 0 || monster.Level < 1)
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
            foreach (var level in tables.TbLevelConfig.DataList)
            {
                foreach (var stage in level.Stages)
                {
                    foreach (var recipe in stage.Recipes)
                    {
                        if (tables.TbMonsterConfig.DataList.Count(x => x.EntityId == recipe.MonsterEntityId) != 1)
                            errors.Add($"关卡 {level.LevelId} 阶段 {stage.StageOrder}：怪物 {recipe.MonsterEntityId} 必须关联唯一基础配置。");
                        if (!float.IsFinite(recipe.HealthMultiplier) || recipe.HealthMultiplier <= 0f ||
                            !float.IsFinite(recipe.DefenseMultiplier) || recipe.DefenseMultiplier < 0f ||
                            !float.IsFinite(recipe.AttackMultiplier) || recipe.AttackMultiplier < 0f || recipe.ExperienceOverride < -1)
                            errors.Add($"关卡 {level.LevelId} 配方 {recipe.SpawnPointId}：生命倍率必须为有限正数，防御/攻击倍率须有限且非负，经验覆盖须至少为 -1。");
                    }
                    if (stage.FeaturedOpponentId == GameConfig.Entity.EntityId.None) continue;
                    if (stage.Recipes.Where(x => x.MonsterEntityId == stage.FeaturedOpponentId).Sum(x => x.Count) != 1)
                        errors.Add($"关卡 {level.LevelId} 阶段 {stage.StageOrder}：守关展示必须唯一关联一只本阶段怪物。");
                }
                if (level.MusicId == SoundId.None) continue;
                var music = tables.TbSoundConfig.GetOrDefault((int)level.MusicId);
                if (music == null || music.Group != "Music")
                    errors.Add($"关卡 {level.LevelId}：MusicId={level.MusicId} 必须引用 Music 组音效配置。");
            }

			if (!float.IsFinite(tables.TbBattleConfig.DeathSettlementDelay) || tables.TbBattleConfig.DeathSettlementDelay < 0f ||
                !float.IsFinite(tables.TbBattleConfig.MonsterHealthBarFadeDuration) || tables.TbBattleConfig.MonsterHealthBarFadeDuration <= 0f)
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
            if (drop.MusouPerOrb <= 0 ||
                drop.ManaMinimum < 0 || !float.IsFinite(drop.ManaMaxFraction) || drop.ManaMaxFraction < 0f ||
                !float.IsFinite(drop.ScatterRadius) || drop.ScatterRadius < 0f ||
                !float.IsFinite(drop.Delay) || drop.Delay < 0f ||
                !float.IsFinite(drop.RiseDuration) || drop.RiseDuration <= 0f ||
                !drop.RiseDistance.IsFinite() || drop.RiseDistance.X < 0f || drop.RiseDistance.Y < drop.RiseDistance.X ||
                !float.IsFinite(drop.InitialSpeed) || drop.InitialSpeed <= 0f ||
                !float.IsFinite(drop.Acceleration) || drop.Acceleration < 0f ||
                !float.IsFinite(drop.MaxSpeed) || drop.MaxSpeed < drop.InitialSpeed ||
                !float.IsFinite(drop.PickupRadius) || drop.PickupRadius <= 0f)
                errors.Add("掉落：数量、回蓝、散布、时间、速度与接触半径配置非法。");
            foreach (var monster in tables.TbMonsterConfig.DataList)
            {
                if (monster.SoulPerOrb < 0 || monster.SoulCount.X < 0 || monster.SoulCount.Y < monster.SoulCount.X ||
                    !float.IsFinite(monster.ManaDropChance) || monster.ManaDropChance is < 0f or > 1f ||
                    monster.ManaCount.X < 1 || monster.ManaCount.Y < monster.ManaCount.X ||
                    !float.IsFinite(monster.MusouDropChance) || monster.MusouDropChance is < 0f or > 1f ||
                    monster.MusouCount.X < 1 || monster.MusouCount.Y < monster.MusouCount.X)
                    errors.Add($"怪物 {monster.Id}：灵魂数量和蓝球/无双球概率、数量配置非法。");
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

				if (row.Stats.MaxHp <= 0)
				{
					errors.Add($"成长表：英雄 {row.HeroId} 的 {row.Level} 级 MaxHp 必须为正。");
				}
			}

			foreach (HeroConfig hero in tables.TbHeroConfig.DataList)
			{
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
	}
}
