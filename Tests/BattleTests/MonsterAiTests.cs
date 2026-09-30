using System;
using System.Collections.Generic;
using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;
using GameLogic.Entity.Monsters.AI.States;
using Xunit;

namespace GameLogic.Battle.Tests
{
	/// <summary>
	/// 怪物 AI 回归：技能书（冷却/选招/够得着）、规则函数（判定盒几何）、以及用框架真实 GF.Fsm 驱动的状态流转。
	/// 宿主用 <see cref="FakeAgent"/>（可控的感知 + 确定随机序列），不需要引擎进程。
	///
	/// 几何约定（与游戏内猴子/悟空一致，便于对照冒烟日志）：
	///  * 猴子 attack_1 判定盒推导结果（原生朝左）：X ∈ [-49, 1]，Y ∈ [-48, 2]；
	///  * 悟空受击盒（相对猴子原点，同一地面）：宽 40（±20），Y ∈ [-79, 19]。
	/// </summary>
	public class MonsterAiTests
	{
		private const float Dt = 1f / 60f;

		/// <summary>猴子 attack_1 判定盒范围（原生朝左）</summary>
		private static readonly AiBox MonkeyReach = new AiBox(-49f, 1f, -48f, 2f);

		/// <summary>猴子同款参数：欲望 70、判定 1s、巡逻 2s/10%/半径 200、僵直 0、滞回 25</summary>
		private static MonsterAiParams MonkeyParams(int desire = 70, float calm = 0f)
		{
			return new MonsterAiParams(desire, 1f, 2f, 10, 200f, calm, 25f);
		}

		/// <summary>近身普攻（带判定盒范围）</summary>
		private static MonsterSkillSpec Basic(int index, int weight = 100)
		{
			return new MonsterSkillSpec(index, 0, weight, 0f, 0f, 0f, 0f, 0f, 0f, MonkeyReach);
		}

		/// <summary>远程普攻（无判定盒，按水平距离区间）</summary>
		private static MonsterSkillSpec RangedBasic(int index, float max, int weight = 100)
		{
			return new MonsterSkillSpec(index, 0, weight, 0f, max, 0f, 0f, 0f, 0f);
		}

		/// <summary>远程技能（无判定盒）</summary>
		private static MonsterSkillSpec Skill(int index, int priority, float min, float max, float cd, float initCd = 0f,
			int weight = 100)
		{
			return new MonsterSkillSpec(index, priority, weight, min, max, cd, cd, initCd, initCd);
		}

		/// <summary>站在同一地面、水平距离 dx 的英雄受击盒</summary>
		private static AiBox Hero(float dx, float dy = 0f)
		{
			return new AiBox(dx - 20f, dx + 20f, -79f + dy, 19f + dy);
		}

		// ---------------------------------------------------------------- 规则（几何）

		[Fact]
		public void Rules_ToFacing_MirrorsOnlyWhenFacingRight()
		{
			AiBox right = MonsterAiRules.ToFacing(MonkeyReach, 1);
			Assert.Equal(-1f, right.Left, 3);
			Assert.Equal(49f, right.Right, 3);
			Assert.Equal(-48f, right.Top, 3);

			AiBox left = MonsterAiRules.ToFacing(MonkeyReach, -1);
			Assert.Equal(-49f, left.Left, 3);
			Assert.Equal(1f, left.Right, 3);
		}

		[Theory]
		[InlineData(-40f, -29f)]   // 吃进 29px
		[InlineData(-69f, 0f)]     // 恰好擦边
		[InlineData(-80f, 11f)]    // 差 11px
		public void Rules_HorizontalGap_FacingLeft(float dx, float expected)
		{
			Assert.Equal(expected, MonsterAiRules.HorizontalGap(MonkeyReach, Hero(dx)), 3);
		}

		[Fact]
		public void Rules_VerticalGap_HeroAboveHeadIsOutOfReach()
		{
			Assert.True(MonsterAiRules.VerticalGap(MonkeyReach, Hero(-30f)) < 0f);          // 同一地面：重叠
			Assert.True(MonsterAiRules.VerticalGap(MonkeyReach, Hero(-30f, -120f)) > 0f);   // 头顶 120px：分离
		}

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

		// ---------------------------------------------------------------- 技能书

		[Fact]
		public void SkillBook_MeleeReach_UsesBothAxes()
		{
			MonsterSkillBook book = new MonsterSkillBook(new[] { Basic(0) });
			Assert.Equal(0, book.SelectBasic(Hero(-40f), -1, 0f));          // 吃进 29px、同一高度
			Assert.Equal(-1, book.SelectBasic(Hero(-67f), -1, 0f));         // 只吃进 2px < ReachMargin
			Assert.Equal(-1, book.SelectBasic(Hero(-40f, -120f), -1, 0f));  // 水平够得着、在头顶
			Assert.Equal(-1, book.SelectBasic(Hero(40f), -1, 0f));          // 在身后（朝向传错不算够得着）
			Assert.Equal(0, book.SelectBasic(Hero(40f), 1, 0f));            // 转过去就够得着
		}

		[Fact]
		public void SkillBook_BasicHorizontalGap_IgnoresHeight()
		{
			MonsterSkillBook book = new MonsterSkillBook(new[] { Basic(0) });
			Assert.Equal(-29f, book.BasicHorizontalGap(Hero(-40f, -120f), -1), 3);
			Assert.Equal(float.PositiveInfinity, book.BasicHorizontalGap(default, -1));   // 无目标
		}

		[Fact]
		public void SkillBook_RangedBasic_UsesHorizontalDistanceOnly()
		{
			MonsterSkillBook book = new MonsterSkillBook(new[] { RangedBasic(0, 300f) });
			Assert.Equal(0, book.SelectBasic(Hero(-250f, -120f), -1, 0f));   // 远程招不看高度
			Assert.Equal(-1, book.SelectBasic(Hero(-350f), -1, 0f));
			Assert.Equal(50f, book.BasicHorizontalGap(Hero(-350f), -1), 3);
		}

		[Fact]
		public void SkillBook_SelectSkill_HighestPriorityUsableWins()
		{
			MonsterSkillBook book = new MonsterSkillBook(new[]
			{
				Basic(0), Skill(1, 1, 0, 400, 5), Skill(2, 2, 0, 200, 5),
			});
			Assert.Equal(2, book.SelectSkill(Hero(-150f), -1, 0f));   // 两个技能都够得着 → 优先级 2
			Assert.Equal(1, book.SelectSkill(Hero(-300f), -1, 0f));   // 只有 1 够得着
			Assert.Equal(-1, book.SelectSkill(Hero(-500f), -1, 0f));  // 都够不着；普攻不参与技能选择
		}

		[Fact]
		public void SkillBook_Cooldown_BlocksUntilTickedDown()
		{
			MonsterSkillBook book = new MonsterSkillBook(new[] { Skill(0, 1, 0, 400, 3) });
			book.MarkUsed(0, 0f);
			Assert.Equal(-1, book.SelectSkill(Hero(-100f), -1, 0f));
			book.Tick(2.9f);
			Assert.Equal(-1, book.SelectSkill(Hero(-100f), -1, 0f));
			book.Tick(0.2f);
			Assert.Equal(0, book.SelectSkill(Hero(-100f), -1, 0f));
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
		public void SkillBook_SelectBasic_Weighted()
		{
			MonsterSkillBook book = new MonsterSkillBook(new[] { Basic(0, weight: 30), Basic(1, weight: 70) });
			Assert.Equal(0, book.SelectBasic(Hero(-40f), -1, 0.29f));
			Assert.Equal(1, book.SelectBasic(Hero(-40f), -1, 0.31f));
			Assert.Equal(1, book.SelectBasic(Hero(-40f), -1, 0.9999f));
		}

		[Fact]
		public void SkillBook_ZeroWeight_NeverSelected()
		{
			MonsterSkillBook book = new MonsterSkillBook(new[] { Basic(0, weight: 0) });
			Assert.Equal(-1, book.SelectBasic(Hero(-40f), -1, 0f));
			Assert.Equal(float.PositiveInfinity, book.BasicHorizontalGap(Hero(-40f), -1));
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
			h.Agent.Target(Hero(-200f));
			h.Step(2);   // 帧1 Patrol→Chase（切换帧不 Tick 新状态），帧2 Chase 决策
			Assert.Equal("Chase", h.State);
			Assert.Equal(-1, h.Agent.MoveDir);

			h.Agent.Target(Hero(-66f));   // 吃进 3px：还不够 ReachMargin，继续追
			h.Step(1);
			Assert.Equal("Chase", h.State);

			h.Agent.Target(Hero(-40f));
			h.Step(1);
			Assert.Equal("Attack", h.State);
			h.Step(1);   // 进入 Attack 后首个决策帧即掷（欲望 100 必中）
			Assert.Equal(0, h.Agent.RequestedAttack);
			Assert.Equal(-1, h.Agent.FaceDir);
		}

		[Fact]
		public void Fsm_Attack_HysteresisCreepsWithinSlack_ThenChasesBeyond()
		{
			using AiHarness h = new AiHarness(MonkeyParams(desire: 0), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-40f));
			h.Step(2);
			Assert.Equal("Attack", h.State);

			h.Agent.Target(Hero(-80f));   // 间隙 11 ≤ 滞回 25：留在站定、小步贴近
			h.Step(1);
			Assert.Equal("Attack", h.State);
			Assert.Equal(-1, h.Agent.MoveDir);

			h.Agent.Target(Hero(-100f));  // 间隙 31 > 25 → 追击
			h.Step(1);
			Assert.Equal("Chase", h.State);
		}

		[Fact]
		public void Fsm_TargetAboveHead_StandsBelowWithoutAttacking()
		{
			using AiHarness h = new AiHarness(MonkeyParams(desire: 100), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-30f, -120f));   // 正上方偏前 30px、高 120px
			h.Step(2);
			Assert.Equal("Attack", h.State);     // 水平够得着 → 站到下面

			h.Run(3f);                           // 欲望 100、间隔 1s：掷了 3 次，但高度够不着
			Assert.Equal("Attack", h.State);
			Assert.Equal(-1, h.Agent.RequestedAttack);
			Assert.Equal(0, h.Agent.MoveDir);
			Assert.Equal(-1, h.Agent.FaceDir);

			h.Agent.Target(Hero(-30f));          // 落地
			h.Run(1.05f);
			Assert.Equal(0, h.Agent.RequestedAttack);
		}

		[Fact]
		public void Fsm_Attacking_DoesNotDecideUntilRecovered()
		{
			using AiHarness h = new AiHarness(MonkeyParams(desire: 100), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-40f));
			h.Step(3);   // Patrol→Chase→Attack→出招
			Assert.True(h.Agent.IsAttacking);

			// 出招/收招硬直期间目标绕到身后又跑远：不转身、不移动、不离开站定
			h.Agent.Target(Hero(60f));
			h.Step(3);
			Assert.Equal("Attack", h.State);
			Assert.Equal(-1, h.Agent.FaceDir);
			Assert.Equal(0, h.Agent.MoveDir);

			h.Agent.IsAttacking = false;   // 收招硬直结束
			h.Step(1);
			Assert.Equal(1, h.Agent.FaceDir);   // 这时才转身
		}

		[Fact]
		public void Fsm_Chase_CastsReadySkillOnTheWay()
		{
			// 技能 1：优先级 1，距离 100~400（突进/远程），追击途中就绪即放
			using AiHarness h = new AiHarness(MonkeyParams(), new[] { Basic(0), Skill(1, 1, 100, 400, 8) }, 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-300f));
			h.Step(2);
			Assert.Equal("Chase", h.State);
			Assert.Equal(1, h.Agent.RequestedAttack);
		}

		[Fact]
		public void Fsm_CcLocked_InterruptsAnyState_ThenReturnsAfterCalmTime()
		{
			using AiHarness h = new AiHarness(MonkeyParams(calm: 0.5f), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-200f));
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
			h.Agent.Target(Hero(-10f));
			h.Step(5);
			Assert.Equal("Death", h.State);
		}

		[Fact]
		public void Brains_GroundMelee_BindReplacesRole_OtherStatesJumpToReplacement()
		{
			MonsterAiStateSet set = MonsterBrains.GroundMelee();
			set.Bind(new HoverChaseState());
			using AiHarness h = new AiHarness(MonkeyParams(), new[] { Basic(0) }, set, 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-200f));
			h.Step(1);
			Assert.Equal("HoverChase", h.State);   // Patrol → 按角色跳 Chase → 落到替换实现
		}

		[Fact]
		public void Brains_ExtraState_ReachableByType_AndReturnsViaRole()
		{
			MonsterAiStateSet set = MonsterBrains.GroundMelee();
			set.Bind(new EnrageOnceChaseState());
			set.AddExtra(new EnrageState());
			using AiHarness h = new AiHarness(MonkeyParams(), new[] { Basic(0) }, set, 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-200f));
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

			MonsterAiStateSet duplicate = MonsterBrains.GroundMelee();
			duplicate.AddExtra(new MonsterIdleState());
			Assert.Throws<InvalidOperationException>(() => duplicate.ToArray());
		}

		[Fact]
		public void Brains_EachCallReturnsFreshInstances()
		{
			MonsterAiState[] a = MonsterBrains.GroundMelee().ToArray();
			MonsterAiState[] b = MonsterBrains.GroundMelee().ToArray();
			HashSet<MonsterAiState> seen = new HashSet<MonsterAiState>(a);
			foreach (MonsterAiState state in b)
			{
				Assert.DoesNotContain(state, seen);
			}
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

		/// <summary>
		/// 可控宿主：感知由测试直接写，随机数取固定序列（用尽后重复最后一个）。
		/// 与真实宿主一致：出招中（IsAttacking，含收招硬直）拒绝 Face / RequestAttack。
		/// </summary>
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
			public AiBox TargetBox { get; set; }
			public float TargetDeltaX => TargetBox.IsEmpty ? 0f : TargetBox.CenterX;
			public float HomeDeltaX { get; set; }
			public MonsterAiParams Params { get; }
			public MonsterSkillBook Skills { get; }
			public MonsterAiStateSet States { get; }

			public int MoveDir { get; private set; }
			public int FaceDir { get; private set; }
			public int RequestedAttack { get; private set; } = -1;

			public void Target(AiBox box)
			{
				HasTarget = true;
				TargetBox = box;
			}

			public float NextRandom()
			{
				float v = m_Randoms[Math.Min(m_RandomIndex, m_Randoms.Length - 1)];
				m_RandomIndex++;
				return v;
			}

			public void Move(int dir) => MoveDir = dir;

			public void Face(int dir)
			{
				if (!IsAttacking)
				{
					FaceDir = dir;
				}
			}

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
				: this(p, specs, MonsterBrains.GroundMelee(), randoms)
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
