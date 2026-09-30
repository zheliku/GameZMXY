using System;
using System.Collections.Generic;
using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;
using GameLogic.Entity.Monsters.AI.States;
using Xunit;

namespace GameLogic.Battle.Tests
{
	/// <summary>
	/// 怪物 AI 回归：判定盒几何（AiBox）、招式书（冷却/选招/够得着）、以及用框架真实 GF.Fsm 驱动的状态流转与预设原型。
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

		/// <summary>猴子同款参数：欲望 70、判定 1s、巡逻 2s/10%/半径 200、僵直 0、滞回 25、踱步半幅 60</summary>
		private static MonsterAiParams MonkeyParams(int desire = 70, float calm = 0f, float pace = 60f)
		{
			return new MonsterAiParams
			{
				AttackDesire = desire,
				AttackInterval = 1f,
				PatrolInterval = 2f,
				PatrolIdleChance = 10,
				PatrolRadius = 200f,
				CalmTime = calm,
				AttackRangeSlack = 25f,
				PaceRange = pace,
			};
		}

		/// <summary>近身普攻（带判定盒范围）</summary>
		private static MonsterAttackSpec Basic(int index, int weight = 100)
		{
			return new MonsterAttackSpec { Index = index, Weight = weight, Reach = MonkeyReach };
		}

		/// <summary>远程普攻（无判定盒，按水平距离区间）</summary>
		private static MonsterAttackSpec RangedBasic(int index, float max, int weight = 100)
		{
			return new MonsterAttackSpec { Index = index, Weight = weight, Range = (0f, max) };
		}

		/// <summary>远程优先招（无判定盒）</summary>
		private static MonsterAttackSpec Priority(int index, int priority, float min, float max, float cd, int weight = 100)
		{
			return new MonsterAttackSpec
			{
				Index = index, Priority = priority, Weight = weight, Range = (min, max), Cooldown = (cd, cd),
			};
		}

		/// <summary>站在同一地面、水平距离 dx 的英雄受击盒</summary>
		private static AiBox Hero(float dx, float dy = 0f)
		{
			return new AiBox(dx - 20f, dx + 20f, -79f + dy, 19f + dy);
		}

		// ---------------------------------------------------------------- 规则（几何）

		[Fact]
		public void Box_Facing_MirrorsOnlyWhenFacingRight()
		{
			AiBox right = MonkeyReach.Facing(1);
			Assert.Equal(-1f, right.Left, 3);
			Assert.Equal(49f, right.Right, 3);
			Assert.Equal(-48f, right.Top, 3);

			AiBox left = MonkeyReach.Facing(-1);
			Assert.Equal(-49f, left.Left, 3);
			Assert.Equal(1f, left.Right, 3);
		}

		[Theory]
		[InlineData(-40f, -29f)]   // 吃进 29px
		[InlineData(-69f, 0f)]     // 恰好擦边
		[InlineData(-80f, 11f)]    // 差 11px
		public void Box_GapX_FacingLeft(float dx, float expected)
		{
			Assert.Equal(expected, MonkeyReach.GapX(Hero(dx)), 3);
		}

		[Fact]
		public void Box_GapY_HeroAboveHeadIsOutOfReach()
		{
			Assert.True(MonkeyReach.GapY(Hero(-30f)) < 0f);          // 同一地面：重叠
			Assert.True(MonkeyReach.GapY(Hero(-30f, -120f)) > 0f);   // 头顶 120px：分离
		}

		// ---------------------------------------------------------------- 招式书

		[Fact]
		public void AttackBook_MeleeReach_UsesBothAxes()
		{
			MonsterAttackBook book = new MonsterAttackBook(new[] { Basic(0) });
			Assert.Equal(0, book.SelectBasic(Hero(-40f), -1, 0f));          // 吃进 29px、同一高度
			Assert.Equal(-1, book.SelectBasic(Hero(-67f), -1, 0f));         // 只吃进 2px < ReachMargin
			Assert.Equal(-1, book.SelectBasic(Hero(-40f, -120f), -1, 0f));  // 水平够得着、在头顶
			Assert.Equal(-1, book.SelectBasic(Hero(40f), -1, 0f));          // 在身后（朝向传错不算够得着）
			Assert.Equal(0, book.SelectBasic(Hero(40f), 1, 0f));            // 转过去就够得着
		}

		[Fact]
		public void AttackBook_BasicGapX_IgnoresHeight_BasicInReachDoesNot()
		{
			MonsterAttackBook book = new MonsterAttackBook(new[] { Basic(0) });
			Assert.Equal(-29f, book.BasicGapX(Hero(-40f, -120f), -1), 3);
			Assert.False(book.BasicInReach(Hero(-40f, -120f), -1));
			Assert.True(book.BasicInReach(Hero(-40f), -1));
			Assert.Equal(float.PositiveInfinity, book.BasicGapX(default, -1));   // 无目标
		}

		[Fact]
		public void AttackBook_RangedBasic_UsesHorizontalDistanceOnly()
		{
			MonsterAttackBook book = new MonsterAttackBook(new[] { RangedBasic(0, 300f) });
			Assert.Equal(0, book.SelectBasic(Hero(-250f, -120f), -1, 0f));   // 远程招不看高度
			Assert.Equal(-1, book.SelectBasic(Hero(-350f), -1, 0f));
			Assert.Equal(50f, book.BasicGapX(Hero(-350f), -1), 3);
		}

		[Fact]
		public void AttackBook_SelectPriority_HighestUsableWins_BasicsExcluded()
		{
			MonsterAttackBook book = new MonsterAttackBook(new[]
			{
				Basic(0), Priority(1, 1, 0, 400, 5), Priority(2, 2, 0, 200, 5),
			});
			Assert.Equal(2, book.SelectPriority(Hero(-150f), -1, 0f));   // 两招都够得着 → 优先级 2
			Assert.Equal(1, book.SelectPriority(Hero(-300f), -1, 0f));   // 只有 1 够得着
			Assert.Equal(-1, book.SelectPriority(Hero(-500f), -1, 0f));  // 都够不着；普攻（下标 0）即使够得着也不参与
		}

		[Fact]
		public void AttackBook_Cooldown_BlocksUntilTickedDown()
		{
			MonsterAttackBook book = new MonsterAttackBook(new[] { Priority(0, 1, 0, 400, 3) });
			book.MarkUsed(0, 0f);
			Assert.Equal(-1, book.SelectPriority(Hero(-100f), -1, 0f));
			book.Tick(2.9f);
			Assert.Equal(-1, book.SelectPriority(Hero(-100f), -1, 0f));
			book.Tick(0.2f);
			Assert.Equal(0, book.SelectPriority(Hero(-100f), -1, 0f));
		}

		[Fact]
		public void AttackBook_Reset_RollsInitialCooldownInRange()
		{
			MonsterAttackBook book = new MonsterAttackBook(new[]
			{
				Priority(0, 1, 0, 400, 10) with { InitCooldown = (3f, 5f) },
			});
			book.Reset(() => 0.5f);   // 初始冷却 4s
			book.Tick(3.9f);
			Assert.Equal(-1, book.SelectPriority(Hero(-100f), -1, 0f));
			book.Tick(0.2f);
			Assert.Equal(0, book.SelectPriority(Hero(-100f), -1, 0f));
		}

		[Fact]
		public void AttackBook_SelectBasic_Weighted()
		{
			MonsterAttackBook book = new MonsterAttackBook(new[] { Basic(0, weight: 30), Basic(1, weight: 70) });
			Assert.Equal(0, book.SelectBasic(Hero(-40f), -1, 0.29f));
			Assert.Equal(1, book.SelectBasic(Hero(-40f), -1, 0.31f));
			Assert.Equal(1, book.SelectBasic(Hero(-40f), -1, 0.9999f));
		}

		[Fact]
		public void AttackBook_ZeroWeight_NeverSelected()
		{
			MonsterAttackBook book = new MonsterAttackBook(new[] { Basic(0, weight: 0) });
			Assert.Equal(-1, book.SelectBasic(Hero(-40f), -1, 0f));
			Assert.Equal(float.PositiveInfinity, book.BasicGapX(Hero(-40f), -1));
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
		public void Fsm_TargetAboveHead_HoldsBelowWithoutAttacking_ThenStrikesWhenLanded()
		{
			using AiHarness h = new AiHarness(MonkeyParams(desire: 100), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-30f, -120f));   // 正上方偏前 30px、高 120px
			h.Step(3);                           // Patrol→Chase→Attack→（高度够不着）Hold
			Assert.Equal("Hold", h.State);

			h.Run(3f);                           // 欲望 100 也不出招
			Assert.Equal("Hold", h.State);
			Assert.Equal(-1, h.Agent.RequestedAttack);

			h.Agent.Target(Hero(-30f));          // 落地
			h.Step(3);                           // Hold→Attack→首帧即掷（欲望 100）
			Assert.Equal(0, h.Agent.RequestedAttack);
		}

		[Fact]
		public void Fsm_Hold_PacesBackAndForthAroundTargetX()
		{
			using AiHarness h = new AiHarness(MonkeyParams(pace: 60f), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-30f, -120f));
			h.Step(4);
			Assert.Equal("Hold", h.State);
			Assert.Equal(-1, h.Agent.MoveDir);   // 自己在目标右侧 30px：先往目标另一侧走

			h.Agent.Target(Hero(70f, -120f));    // 走到目标左侧 70px（超过半幅 60）：掉头
			h.Step(1);
			Assert.Equal(1, h.Agent.MoveDir);

			h.Agent.Target(Hero(-70f, -120f));   // 走到目标右侧 70px：再掉头
			h.Step(1);
			Assert.Equal(-1, h.Agent.MoveDir);

			h.Agent.Target(Hero(-200f, -120f));  // 目标在平台上走远（超过半幅 + 滞回）：去追
			h.Step(1);
			Assert.Equal("Chase", h.State);
		}

		[Fact]
		public void Fsm_Sentry_HoldsStill_AndReturnsHomeWhenTargetLost()
		{
			using AiHarness h = new AiHarness(MonkeyParams(), new[] { Basic(0) }, MonsterBrains.Sentry(), 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-30f, -120f));
			h.Step(4);
			Assert.Equal("Hold", h.State);
			h.Step(3);
			Assert.Equal(0, h.Agent.MoveDir);    // 守卫原地等，不踱步

			h.Agent.HomeDeltaX = -150f;          // 追出去 150px 后目标丢失（实体按 LoseTargetTime 清目标）
			h.Agent.LoseTarget();
			h.Step(2);
			Assert.Equal("Patrol", h.State);
			Assert.Equal(1, h.Agent.MoveDir);    // 往回走
			h.Agent.HomeDeltaX = 0f;
			h.Step(1);
			Assert.Equal(0, h.Agent.MoveDir);    // 到岗站住
		}

		[Fact]
		public void Fsm_Brawler_TargetLost_WandersBackIntoPatrolRadius()
		{
			using AiHarness h = new AiHarness(MonkeyParams(), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-200f));
			h.Step(2);
			Assert.Equal("Chase", h.State);

			h.Agent.HomeDeltaX = -350f;          // 追到出生点左侧 350px（半径 200 之外）时丢失目标
			h.Agent.LoseTarget();
			h.Step(2);
			Assert.Equal("Patrol", h.State);
			Assert.Equal(1, h.Agent.MoveDir);    // 游荡先折返回巡逻范围
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
		public void Fsm_Chase_FiresReadyPriorityAttackOnTheWay()
		{
			// 优先招 1：优先级 1，距离 100~400（突进/远程），追击途中就绪即放
			using AiHarness h = new AiHarness(MonkeyParams(), new[] { Basic(0), Priority(1, 1, 100, 400, 8) }, 0.9f);
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
		public void Brains_BindReplacesSlot_OtherStatesJumpToReplacement()
		{
			MonsterAiStateSet set = MonsterBrains.Brawler().Bind(MonsterAiRole.Chase, new HoverState());
			using AiHarness h = new AiHarness(MonkeyParams(), new[] { Basic(0) }, set, 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-200f));
			h.Step(1);
			Assert.Equal("Chase", h.State);                  // 槽名不变
			Assert.IsType<HoverState>(h.CurrentState);      // Patrol → 按角色跳 Chase → 落到换上的行为
		}

		[Fact]
		public void Brains_SameBehaviorReusableInAnySlot()
		{
			// 同一个行为类放进另一个槽（上一个测试放在 Chase，这里放在 Patrol）：行为不写死角色，槽由组装决定
			MonsterAiStateSet set = MonsterBrains.Brawler().Bind(MonsterAiRole.Patrol, new HoverState());
			using AiHarness h = new AiHarness(MonkeyParams(), new[] { Basic(0) }, set, 0.9f);
			h.Step(1);
			Assert.Equal("Patrol", h.State);
			Assert.IsType<HoverState>(h.CurrentState);
		}

		[Fact]
		public void Brains_ExtraState_ReachableByType_AndReturnsViaRole()
		{
			MonsterAiStateSet set = MonsterBrains.Brawler()
				.Bind(MonsterAiRole.Chase, new EnrageOnceWalkState())
				.AddExtra(new EnrageState());
			using AiHarness h = new AiHarness(MonkeyParams(), new[] { Basic(0) }, set, 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-200f));
			h.Step(2);   // Patrol→Chase(EnrageOnceWalk)→Enrage
			Assert.Equal(nameof(EnrageState), h.State);   // 额外状态不占槽，报类名
			h.Run(1.1f);
			Assert.Equal("Chase", h.State);
		}

		[Fact]
		public void StateSet_MissingRoleOrDuplicateType_Throws()
		{
			MonsterAiStateSet incomplete = new MonsterAiStateSet().Bind(MonsterAiRole.Patrol, new WanderState());
			Assert.Throws<InvalidOperationException>(() => incomplete.ToArray());

			MonsterAiStateSet duplicate = MonsterBrains.Brawler().AddExtra(new PauseState());
			Assert.Throws<InvalidOperationException>(() => duplicate.ToArray());
		}

		[Fact]
		public void Brains_EachCallReturnsFreshInstances()
		{
			HashSet<MonsterAiState> seen = new(MonsterBrains.Brawler().ToArray());
			foreach (MonsterAiState state in MonsterBrains.Brawler().ToArray())
			{
				Assert.DoesNotContain(state, seen);
			}
		}

		// ---------------------------------------------------------------- 测试替身

		/// <summary>一个什么都不做的行为（悬停/炮台待机的最小替身），用来验证"行为可放进任意槽"。</summary>
		private sealed class HoverState : MonsterAiState
		{
			protected override void Tick(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds)
			{
			}
		}

		/// <summary>首次接近先狂暴一次（Boss 阶段演出的最小原型）：继承公共行为，只加差异。</summary>
		private sealed class EnrageOnceWalkState : WalkToTargetState
		{
			private bool m_Enraged;

			protected override void Engage(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, int dir, float elapseSeconds)
			{
				if (!m_Enraged)
				{
					m_Enraged = true;
					ChangeState<EnrageState>(fsm);
					return;
				}

				base.Engage(fsm, agent, dir, elapseSeconds);
			}
		}

		/// <summary>额外状态：演出 1 秒，不可被受控打断，结束按角色回 Chase</summary>
		private sealed class EnrageState : MonsterAiState
		{
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

			public FakeAgent(MonsterAiParams p, MonsterAttackBook attacks, MonsterAiStateSet states, float[] randoms)
			{
				Params = p;
				Attacks = attacks;
				States = states;
				m_Randoms = randoms.Length == 0 ? new[] { 0.5f } : randoms;
			}

			public bool IsDead { get; set; }
			public bool IsCcLocked { get; set; }
			public bool IsAttacking { get; set; }
			public AiBox TargetBox { get; set; }
			public float HomeDeltaX { get; set; }
			public MonsterAiParams Params { get; }
			public MonsterAttackBook Attacks { get; }
			public MonsterAiStateSet States { get; }

			public int MoveDir { get; private set; }
			public int FaceDir { get; private set; }
			public int RequestedAttack { get; private set; } = -1;

			public void Target(AiBox box) => TargetBox = box;

			public void LoseTarget() => TargetBox = default;

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
				Attacks.MarkUsed(index, 0f);
				return true;
			}
		}

		/// <summary>用框架真实 FsmManager 驱动一台 AI 状态机（与游戏内 GF.Fsm 同一实现）。</summary>
		private sealed class AiHarness : IDisposable
		{
			private readonly FsmManager m_Manager = new FsmManager();
			private readonly IFsm<IMonsterAiAgent> m_Fsm;

			public FakeAgent Agent { get; }

			public AiHarness(MonsterAiParams p, MonsterAttackSpec[] specs, params float[] randoms)
				: this(p, specs, MonsterBrains.Brawler(), randoms)
			{
			}

			public AiHarness(MonsterAiParams p, MonsterAttackSpec[] specs, MonsterAiStateSet set, params float[] randoms)
			{
				Agent = new FakeAgent(p, new MonsterAttackBook(specs), set, randoms);
				m_Fsm = m_Manager.CreateFsm<IMonsterAiAgent>(Guid.NewGuid().ToString(), Agent, set.ToArray());
				m_Fsm.Start(set.Resolve(set.InitialRole));
			}

			public MonsterAiState CurrentState => (MonsterAiState)m_Fsm.CurrentState;

			public string State => CurrentState.StateName;

			public void Step(int frames)
			{
				for (int i = 0; i < frames; i++)
				{
					Agent.Attacks.Tick(Dt);
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
