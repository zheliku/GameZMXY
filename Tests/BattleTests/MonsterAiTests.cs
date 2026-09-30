using System;
using System.Collections.Generic;
using GameFramework.Fsm;
using GameLogic.Entity.AI;
using Xunit;

namespace GameLogic.Battle.Tests
{
	/// <summary>
	/// 怪物 AI 回归：技能书（冷却/选招）、规则函数、以及用框架真实 GF.Fsm 驱动的状态流转。
	/// 宿主用 <see cref="FakeAgent"/>（可控的感知 + 确定随机序列），不需要引擎进程。
	/// </summary>
	public class MonsterAiTests
	{
		private const float Dt = 1f / 60f;

		/// <summary>猴子同款参数：站定 45、滞回 25、欲望 70、判定 1s、巡逻 2s/10%/半径 200、僵直 0</summary>
		private static MonsterAiParams MonkeyParams(int desire = 70, float calm = 0f)
		{
			return new MonsterAiParams(45f, desire, 1f, 2f, 10, 200f, calm, 25f);
		}

		private static MonsterSkillSpec Basic(int index, int weight = 100, float max = 70f)
		{
			return new MonsterSkillSpec(index, 0, weight, 0f, max, 0f, 0f, 0f, 0f);
		}

		private static MonsterSkillSpec Skill(int index, int priority, float min, float max, float cd, float initCd = 0f,
			int weight = 100)
		{
			return new MonsterSkillSpec(index, priority, weight, min, max, cd, cd, initCd, initCd);
		}

		// ---------------------------------------------------------------- 技能书

		[Fact]
		public void SkillBook_SelectSkill_HighestPriorityUsableWins()
		{
			MonsterSkillBook book = new MonsterSkillBook(new[]
			{
				Basic(0), Skill(1, 1, 0, 400, 5), Skill(2, 2, 0, 200, 5),
			});
			Assert.Equal(2, book.SelectSkill(150f, 0f));   // 两个技能都够得着 → 优先级 2
			Assert.Equal(1, book.SelectSkill(300f, 0f));   // 只有 1 够得着
			Assert.Equal(-1, book.SelectSkill(500f, 0f));  // 都够不着；普攻不参与技能选择
		}

		[Fact]
		public void SkillBook_Cooldown_BlocksUntilTickedDown()
		{
			MonsterSkillBook book = new MonsterSkillBook(new[] { Skill(0, 1, 0, 400, 3) });
			book.MarkUsed(0, 0f);
			Assert.Equal(-1, book.SelectSkill(100f, 0f));
			book.Tick(2.9f);
			Assert.Equal(-1, book.SelectSkill(100f, 0f));
			book.Tick(0.2f);
			Assert.Equal(0, book.SelectSkill(100f, 0f));
		}

		[Fact]
		public void SkillBook_Reset_RollsInitialCooldownInRange()
		{
			MonsterSkillBook book = new MonsterSkillBook(new[]
			{
				new MonsterSkillSpec(0, 1, 100, 0, 400, 10, 10, 3, 5),
			});
			book.Reset(() => 0.5f);
			Assert.Equal(4f, book.GetCooldown(0), 3);
		}

		[Fact]
		public void SkillBook_SelectBasic_WeightedAndRangeFiltered()
		{
			MonsterSkillBook book = new MonsterSkillBook(new[]
			{
				Basic(0, weight: 30), Basic(1, weight: 70), Basic(2, weight: 100, max: 10),
			});
			// 距离 50：普攻 2 够不着，只在 0/1 间按 30:70 抽
			Assert.Equal(0, book.SelectBasic(50f, 0.29f));
			Assert.Equal(1, book.SelectBasic(50f, 0.31f));
			Assert.Equal(1, book.SelectBasic(50f, 0.9999f));
			Assert.True(book.HasBasicInRange(50f));
			Assert.False(book.HasBasicInRange(80f));
		}

		[Fact]
		public void SkillBook_ZeroWeight_NeverSelected()
		{
			MonsterSkillBook book = new MonsterSkillBook(new[] { Basic(0, weight: 0) });
			Assert.Equal(-1, book.SelectBasic(10f, 0f));
		}

		// ---------------------------------------------------------------- 规则

		[Theory]
		[InlineData(0f, 200f, 0)]
		[InlineData(150f, 200f, 0)]
		[InlineData(250f, 200f, -1)]
		[InlineData(-250f, 200f, 1)]
		[InlineData(9999f, 0f, 0)]   // 半径 0 = 不限
		public void Rules_LeashDirection(float homeDx, float radius, int expected)
		{
			Assert.Equal(expected, MonsterAiRules.LeashDirection(homeDx, radius));
		}

		[Theory]
		[InlineData(0f, 0, false)]
		[InlineData(0.6999f, 70, true)]
		[InlineData(0.7f, 70, false)]
		[InlineData(0.9999f, 100, true)]
		public void Rules_Chance(float roll, int percent, bool expected)
		{
			Assert.Equal(expected, MonsterAiRules.Chance(roll, percent));
		}

		// ---------------------------------------------------------------- 状态流转（真实 GF.Fsm）

		[Fact]
		public void Fsm_NoTarget_PatrolsAndPausesToIdle()
		{
			// 随机序列：进入巡逻选方向 0.9 → 右；2s 决策点掷停留 0.05 < 10% → Idle
			using AiHarness h = new AiHarness(MonkeyParams(), new[] { Basic(0) }, 0.9f, 0.05f);
			h.Step(1);
			Assert.Equal("Patrol", h.State);
			Assert.Equal(1, h.Agent.MoveDir);

			h.Run(2.05f);
			Assert.Equal("Idle", h.State);
			Assert.Equal(0, h.Agent.MoveDir);
		}

		[Fact]
		public void Fsm_Patrol_LeashTurnsBackTowardHome()
		{
			using AiHarness h = new AiHarness(MonkeyParams(), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			Assert.Equal(1, h.Agent.MoveDir);
			h.Agent.HomeDeltaX = 250f;   // 走出半径 200
			h.Step(1);
			Assert.Equal(-1, h.Agent.MoveDir);
		}

		[Fact]
		public void Fsm_TargetSeen_ChasesThenAttacks()
		{
			using AiHarness h = new AiHarness(MonkeyParams(desire: 100), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			h.Agent.Target(-200f);
			h.Step(2);   // 帧1 Patrol→Chase（切换帧不 Tick 新状态），帧2 Chase 决策
			Assert.Equal("Chase", h.State);
			Assert.Equal(-1, h.Agent.MoveDir);

			h.Agent.Target(-40f);
			h.Step(1);
			Assert.Equal("Attack", h.State);
			h.Step(1);   // 进入 Attack 后首个决策帧即掷（欲望 100 必中）
			Assert.Equal(0, h.Agent.RequestedAttack);
			Assert.Equal(-1, h.Agent.FaceDir);
		}

		[Fact]
		public void Fsm_Attack_HysteresisKeepsStanceWithinSlack()
		{
			using AiHarness h = new AiHarness(MonkeyParams(desire: 0), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			h.Agent.Target(-40f);
			h.Step(2);
			Assert.Equal("Attack", h.State);

			h.Agent.Target(-65f);   // 45 < 65 ≤ 45+25：保持站定
			h.Step(1);
			Assert.Equal("Attack", h.State);

			h.Agent.Target(-75f);   // 超出滞回 → 追击
			h.Step(1);
			Assert.Equal("Chase", h.State);
		}

		[Fact]
		public void Fsm_Attacking_DoesNotLeaveStateUntilRecovered()
		{
			using AiHarness h = new AiHarness(MonkeyParams(desire: 100), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			h.Agent.Target(-40f);
			h.Step(3);   // Patrol→Chase→Attack→出招
			Assert.True(h.Agent.IsAttacking);

			h.Agent.Target(-300f);   // 目标跑远，但出招中不决策
			h.Step(3);
			Assert.Equal("Attack", h.State);

			h.Agent.IsAttacking = false;
			h.Step(1);
			Assert.Equal("Chase", h.State);
		}

		[Fact]
		public void Fsm_Chase_CastsReadySkillOnTheWay()
		{
			// 技能 1：优先级 1，距离 100~400（突进/远程），追击途中就绪即放
			using AiHarness h = new AiHarness(MonkeyParams(), new[] { Basic(0), Skill(1, 1, 100, 400, 8) }, 0.9f);
			h.Step(1);
			h.Agent.Target(-300f);
			h.Step(2);
			Assert.Equal("Chase", h.State);
			Assert.Equal(1, h.Agent.RequestedAttack);
		}

		[Fact]
		public void Fsm_CcLocked_InterruptsAnyState_ThenReturnsAfterCalmTime()
		{
			using AiHarness h = new AiHarness(MonkeyParams(calm: 0.5f), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			h.Agent.Target(-200f);
			h.Step(1);
			Assert.Equal("Chase", h.State);

			h.Agent.IsCcLocked = true;
			h.Step(1);
			Assert.Equal("CcLocked", h.State);
			Assert.Equal(0, h.Agent.MoveDir);

			h.Agent.IsCcLocked = false;
			h.Run(0.4f);
			Assert.Equal("CcLocked", h.State);   // 僵直未满
			h.Run(0.2f);
			Assert.Equal("Chase", h.State);
		}

		[Fact]
		public void Fsm_Death_IsTerminal()
		{
			using AiHarness h = new AiHarness(MonkeyParams(), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			h.Agent.IsDead = true;
			h.Step(1);
			Assert.Equal("Death", h.State);

			h.Agent.IsDead = false;   // 死亡状态不自己离开（复用由实体重建状态机）
			h.Agent.Target(-10f);
			h.Step(5);
			Assert.Equal("Death", h.State);
		}

		[Fact]
		public void StateSet_BindReplacesRole_OtherStatesJumpToReplacement()
		{
			MonsterAiStateSet set = MonsterAiStateSet.CreateDefault();
			set.Bind(new HoverChaseState());
			using AiHarness h = new AiHarness(MonkeyParams(), new[] { Basic(0) }, set, 0.9f);
			h.Step(1);
			h.Agent.Target(-200f);
			h.Step(1);
			Assert.Equal("HoverChase", h.State);   // Patrol → 按角色跳 Chase → 落到替换实现
		}

		[Fact]
		public void StateSet_ExtraState_ReachableByType_AndReturnsViaRole()
		{
			MonsterAiStateSet set = MonsterAiStateSet.CreateDefault();
			set.Bind(new EnrageOnceChaseState());
			set.AddExtra(new EnrageState());
			using AiHarness h = new AiHarness(MonkeyParams(), new[] { Basic(0) }, set, 0.9f);
			h.Step(1);
			h.Agent.Target(-200f);
			h.Step(2);   // Patrol→EnrageOnceChase→Enrage
			Assert.Equal("Enrage", h.State);
			h.Run(1.1f);
			Assert.Equal("EnrageOnceChase", h.State);
		}

		[Fact]
		public void StateSet_MissingRoleOrDuplicateType_Throws()
		{
			MonsterAiStateSet incomplete = new MonsterAiStateSet();
			incomplete.Bind(new MonsterPatrolState());
			Assert.Throws<InvalidOperationException>(() => incomplete.ToArray());

			MonsterAiStateSet duplicate = MonsterAiStateSet.CreateDefault();
			duplicate.AddExtra(new MonsterIdleState());
			Assert.Throws<InvalidOperationException>(() => duplicate.ToArray());
		}

		// ---------------------------------------------------------------- 测试替身

		/// <summary>替换 Chase 角色的示例（飞行/悬停怪）：不移动，只记录进入。</summary>
		private sealed class HoverChaseState : MonsterAiState
		{
			public override MonsterAiRole Role => MonsterAiRole.Chase;
			public override string StateName => "HoverChase";

			protected override void Tick(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds)
			{
			}
		}

		/// <summary>首次追击先狂暴一次（Boss 阶段演出的最小原型）</summary>
		private sealed class EnrageOnceChaseState : MonsterChaseState
		{
			private bool m_Enraged;
			public override string StateName => "EnrageOnceChase";

			protected override void Tick(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds)
			{
				if (!m_Enraged)
				{
					m_Enraged = true;
					ChangeState<EnrageState>(fsm);
					return;
				}

				base.Tick(fsm, agent, elapseSeconds);
			}
		}

		/// <summary>额外状态：演出 1 秒，不可被受控打断，结束按角色回 Chase</summary>
		private sealed class EnrageState : MonsterAiState
		{
			public override MonsterAiRole Role => MonsterAiRole.Chase;   // 仅用于回落语义；AddExtra 不占槽
			public override string StateName => "Enrage";
			protected override bool CanBeCcLocked => false;

			protected override void Tick(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds)
			{
				if (fsm.CurrentStateTime >= 1f)
				{
					ChangeRole(fsm, MonsterAiRole.Chase);
				}
			}
		}

		/// <summary>可控宿主：感知由测试直接写，随机数取固定序列（用尽后重复最后一个）。</summary>
		private sealed class FakeAgent : IMonsterAiAgent
		{
			private readonly float[] m_Randoms;
			private int m_RandomIndex;

			public FakeAgent(MonsterAiParams p, MonsterSkillBook skills, MonsterAiStateSet states, float[] randoms)
			{
				Params = p;
				Skills = skills;
				States = states;
				m_Randoms = randoms.Length == 0 ? new[] { 0.5f } : randoms;
			}

			public bool IsDead { get; set; }
			public bool IsCcLocked { get; set; }
			public bool IsAttacking { get; set; }
			public bool HasTarget { get; set; }
			public float TargetDeltaX { get; set; }
			public float HomeDeltaX { get; set; }
			public MonsterAiParams Params { get; }
			public MonsterSkillBook Skills { get; }
			public MonsterAiStateSet States { get; }

			public int MoveDir { get; private set; }
			public int FaceDir { get; private set; }
			public int RequestedAttack { get; private set; } = -1;

			public void Target(float dx)
			{
				HasTarget = true;
				TargetDeltaX = dx;
			}

			public float NextRandom()
			{
				float v = m_Randoms[Math.Min(m_RandomIndex, m_Randoms.Length - 1)];
				m_RandomIndex++;
				return v;
			}

			public void Move(int dir) => MoveDir = dir;

			public void Face(int dir) => FaceDir = dir;

			public bool RequestAttack(int index)
			{
				if (IsAttacking || IsCcLocked || IsDead)
				{
					return false;
				}

				RequestedAttack = index;
				IsAttacking = true;
				Skills.MarkUsed(index, 0f);
				return true;
			}
		}

		/// <summary>用框架真实 FsmManager 驱动一台 AI 状态机（与游戏内 GF.Fsm 同一实现）。</summary>
		private sealed class AiHarness : IDisposable
		{
			private readonly FsmManager m_Manager = new FsmManager();
			private readonly IFsm<IMonsterAiAgent> m_Fsm;

			public FakeAgent Agent { get; }

			public AiHarness(MonsterAiParams p, MonsterSkillSpec[] specs, params float[] randoms)
				: this(p, specs, MonsterAiStateSet.CreateDefault(), randoms)
			{
			}

			public AiHarness(MonsterAiParams p, MonsterSkillSpec[] specs, MonsterAiStateSet set, params float[] randoms)
			{
				Agent = new FakeAgent(p, new MonsterSkillBook(specs), set, randoms);
				m_Fsm = m_Manager.CreateFsm<IMonsterAiAgent>(Guid.NewGuid().ToString(), Agent, set.ToArray());
				m_Fsm.Start(set.Resolve(set.InitialRole));
			}

			public string State => ((MonsterAiState)m_Fsm.CurrentState).StateName;

			public void Step(int frames)
			{
				for (int i = 0; i < frames; i++)
				{
					Agent.Skills.Tick(Dt);
					m_Manager.Update(Dt, Dt);
				}
			}

			public void Run(float seconds)
			{
				Step((int)Math.Ceiling(seconds / Dt));
			}

			public void Dispose()
			{
				m_Manager.Shutdown();
			}
		}
	}
}
