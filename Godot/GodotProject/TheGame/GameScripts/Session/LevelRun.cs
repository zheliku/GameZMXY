using System;
using System.Threading.Tasks;
using GameConfig;
using GameConfig.Monster;
using GameLogic.Entity;
using GameLogic.Entity.Heroes;
using GameLogic.Level;
using GameLogic.Profile;
using GameLogic.Save;
using GodotGameFramework;

namespace GameLogic.Session
{
	/// <summary>一次关卡运行的结局。</summary>
	public enum LevelRunOutcome
	{
		/// <summary>仍在进行。</summary>
		Running,

		/// <summary>全部清波（通关）。</summary>
		Cleared,

		/// <summary>英雄死亡。</summary>
		Defeated,

		/// <summary>中途放弃或运行错误，收益已回滚。</summary>
		Abandoned,
	}

	/// <summary>
	/// 一次关卡运行（关卡作用域）的所有者：结算关内收益并管理"关卡事务"。
	/// <list type="bullet">
	/// <item>进关时在内存捕获档案快照；关内收益直接写入档案（即时升级、即时反馈）。</item>
	/// <item>通关或英雄死亡 = 提交：写一个检查点；先到者生效，只提交一次。</item>
	/// <item>中途放弃、运行错误 = 回滚到进关快照，不写盘；框架关停时不写盘（等价回滚）。</item>
	/// </list>
	/// 只订阅本作用域对象的 C# 事件（关卡控制器、英雄实体），结束后全部退订。
	/// </summary>
	public sealed class LevelRun
	{
		private readonly GameContext m_Context; // 档案作用域。
		private readonly LevelController m_Level; // 本关控制器。
		private readonly HeroEntity m_Hero; // 本关出战英雄。
		private readonly HeroRecord m_Record; // 出战英雄在档案中的记录。
		private readonly TbMonsterConfig m_Monsters; // 怪物配置（经验奖励查表）。
		private readonly ProfileSaveData m_Snapshot; // 进关时的档案快照，回滚用。

		/// <summary>结局；Running 表示仍在进行。</summary>
		public LevelRunOutcome Outcome { get; private set; } = LevelRunOutcome.Running;

		/// <summary>本关统计（结算界面只读展示）。</summary>
		public RunStats Stats { get; } = new();

		/// <summary>提交写盘的任务；未提交时为空。</summary>
		public Task<bool> Commit { get; private set; }

		/// <summary>结局确定（已提交或已回滚）时触发一次；流程据此安排重开或返回。</summary>
		public event Action<LevelRunOutcome> Ended;

		/// <summary>开始一次关卡运行：捕获进关快照并订阅本作用域事件。</summary>
		/// <param name="context">档案作用域。</param>
		/// <param name="level">已初始化并开始会话的关卡控制器。</param>
		/// <param name="hero">已显示的出战英雄。</param>
		/// <param name="tables">配置总表。</param>
		/// <exception cref="ArgumentNullException">任一参数为空。</exception>
		/// <exception cref="InvalidOperationException">英雄不是档案的出战英雄。</exception>
		public LevelRun(GameContext context, LevelController level, HeroEntity hero, Tables tables)
		{
			ArgumentNullException.ThrowIfNull(tables);
			m_Context = context ?? throw new ArgumentNullException(nameof(context));
			m_Level = level ?? throw new ArgumentNullException(nameof(level));
			m_Hero = hero ?? throw new ArgumentNullException(nameof(hero));
			if (!context.Profile.Heroes.TryGetValue(hero.HeroId, out m_Record))
			{
				throw new InvalidOperationException($"英雄 {hero.HeroId} 不在档案中。");
			}

			m_Monsters = tables.TbMonsterConfig;
			m_Snapshot = ProfileMapper.Capture(context.Profile);
			m_Level.MonsterDefeated += OnMonsterDefeated;
			m_Level.Completed += OnCompleted;
			m_Hero.Died += OnHeroDied;
		}

		/// <summary>中途放弃或运行错误：回滚到进关快照，不写盘。已结束时直接返回。</summary>
		public void Abandon()
		{
			if (Outcome != LevelRunOutcome.Running)
			{
				return;
			}

			ProfileMapper.RollBack(m_Context.Profile, m_Snapshot);
			Finish(LevelRunOutcome.Abandoned);
		}

		/// <summary>框架关停：只退订，不写盘也不回滚（进程即将结束，磁盘上仍是进关前的检查点）。</summary>
		public void Detach() => Unsubscribe();

		/// <summary>结算一次击败：查表发放经验，升级时重新注入出战装配并补满。</summary>
		/// <param name="defeat">本关怪物的一次击败。</param>
		private void OnMonsterDefeated(MonsterDefeat defeat)
		{
			if (Outcome != LevelRunOutcome.Running)
			{
				return;
			}

			Stats.Kills++;
			MonsterConfig monster = m_Monsters.GetOrDefault(defeat.MonsterId);
			if (monster == null || monster.AddExp <= 0 || !m_Hero.IsAlive)
			{
				return;
			}

			// 经验进档案；等级变化后由档案重建装配交给实体（实体不反向写档案）。
			Stats.Experience += monster.AddExp;
			if (m_Record.Progression.AddExperience(monster.AddExp) > 0)
			{
				m_Hero.ApplyLoadout(m_Context.StatBuilder.Build(m_Record), refill: true);
			}
		}

		/// <summary>通关：提交检查点。</summary>
		private void OnCompleted() => Submit(LevelRunOutcome.Cleared);

		/// <summary>英雄死亡：同样提交本关收益（死亡不清空进度）。</summary>
		/// <param name="actor">死亡的角色。</param>
		private void OnHeroDied(ActorEntity actor) => Submit(LevelRunOutcome.Defeated);

		/// <summary>先到的结局生效：写一个检查点并结束运行。</summary>
		/// <param name="outcome">通关或死亡。</param>
		private void Submit(LevelRunOutcome outcome)
		{
			if (Outcome != LevelRunOutcome.Running)
			{
				return;
			}

			Commit = m_Context.Save.CheckpointAsync(m_Context.Profile);
			Log.Info("[LevelRun] 关卡 {0} 结束：{1}，击败 {2}，经验 {3}", m_Level.LevelId, outcome, Stats.Kills, Stats.Experience);
			Finish(outcome);
		}

		/// <summary>记录结局、退订并通知流程。</summary>
		/// <param name="outcome">本次结局。</param>
		private void Finish(LevelRunOutcome outcome)
		{
			Outcome = outcome;
			Unsubscribe();
			Ended?.Invoke(outcome);
		}

		/// <summary>解除本作用域订阅，重复调用安全。</summary>
		private void Unsubscribe()
		{
			m_Level.MonsterDefeated -= OnMonsterDefeated;
			m_Level.Completed -= OnCompleted;
			m_Hero.Died -= OnHeroDied;
		}
	}

	/// <summary>一次关卡运行的统计（只读展示，不写入存档）。</summary>
	public sealed class RunStats
	{
		/// <summary>本关击败数。</summary>
		public int Kills { get; internal set; }

		/// <summary>本关获得的经验。</summary>
		public int Experience { get; internal set; }
	}
}
