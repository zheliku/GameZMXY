using System;
using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;
using GameLogic.Entity.Monsters.AI.States;
using Xunit;

namespace GameLogic.Battle.Tests
{
	/// <summary>
	/// 怪物 AI 回归：判定盒几何、当前近战招式选取，以及用框架真实 GF.Fsm 驱动的猴子状态流转。
	/// 宿主用 <see cref="FakeAgent"/>（可控的感知 + 确定随机序列），不需要引擎进程。
	///
	/// 几何约定（与游戏内猴子/悟空一致，便于对照冒烟日志）：
	///  * 猴子 attack_1 判定盒推导结果（原生朝左）：X ∈ [-49, 1]，Y ∈ [-48, 2]；
	///  * 悟空受击盒（相对猴子原点，同一地面）：宽 40（±20），Y ∈ [-79, 19]。
	/// </summary>
	public class MonsterAiTests
	{
		private const float Dt = 1f / 60f; // 单个物理帧的时长。

		private static readonly AiBox MonkeyReach = new AiBox(-49f, 1f, -48f, 2f); // 猴子 attack_1 判定盒范围（原生朝左）。

		private static MonsterAiParams MonkeyParams(int desire = 70, float calm = 0f, float pace = 60f) // 猴子同款参数：欲望 70、判定 1s、巡逻 2s/10%/半径 200、僵直 0、滞回 25、踱步半幅 60。
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

		private static MonsterAttackSpec Basic(int index, int weight = 100) // 创建带判定盒范围的近身普攻。
		{
			return new MonsterAttackSpec { Index = index, Weight = weight, Reach = MonkeyReach };
		}

		private static MonsterAttackSpec RangedBasic(int index, float max, int weight = 100) // 创建按水平距离选取的远程普攻。
		{
			return new MonsterAttackSpec { Index = index, Weight = weight, Range = (0f, max) };
		}

		private static MonsterAttackSpec Priority(int index, int priority, float min, float max, float cooldown, int weight = 100) // 创建带优先级、距离范围及固定冷却的招式。
		{
			return new MonsterAttackSpec
			{
				Index = index,
				Priority = priority,
				Weight = weight,
				Range = (min, max),
				Cooldown = (cooldown, cooldown),
			};
		}

		private static AiBox Hero(float dx, float dy = 0f) // 创建同一地面上指定偏移的英雄受击盒。
		{
			return new AiBox(dx - 20f, dx + 20f, -79f + dy, 19f + dy);
		}

		// ---------------------------------------------------------------- 规则（几何）

		/// <summary>验证攻击判定盒仅在朝右时镜像。</summary>
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

		/// <summary>验证朝左攻击判定盒与英雄受击盒的水平间距。</summary>
		[Theory]
		[InlineData(-40f, -29f)]   // 吃进 29px
		[InlineData(-69f, 0f)]     // 恰好擦边
		[InlineData(-80f, 11f)]    // 差 11px
		/// <param name="dx">英雄受击盒相对攻击者的水平偏移。</param>
		/// <param name="expected">预期的水平间距。</param>
		public void Box_GapX_FacingLeft(float dx, float expected)
		{
			Assert.Equal(expected, MonkeyReach.GapX(Hero(dx)), 3);
		}

		/// <summary>验证目标在同一高度时范围重叠、位于头顶时超出攻击范围。</summary>
		[Fact]
		public void Box_GapY_HeroAboveHeadIsOutOfReach()
		{
			Assert.True(MonkeyReach.GapY(Hero(-30f)) < 0f);          // 同一地面：重叠
			Assert.True(MonkeyReach.GapY(Hero(-30f, -120f)) > 0f);   // 头顶 120px：分离
		}

		// ---------------------------------------------------------------- 招式书

		/// <summary>验证近战招式选取同时检查水平与垂直范围及朝向。</summary>
		[Fact]
		public void AttackBook_MeleeReach_UsesBothAxes()
		{
			MonsterAttackBook book = new MonsterAttackBook(new[] { Basic(0) });
			Assert.Equal(0, book.SelectBasic(Hero(-40f), -1, () => 0f));          // 吃进 29px、同一高度
			Assert.Equal(-1, book.SelectBasic(Hero(-67f), -1, () => 0f));         // 只吃进 2px < ReachMargin
			Assert.Equal(-1, book.SelectBasic(Hero(-40f, -120f), -1, () => 0f));  // 水平够得着、在头顶
			Assert.Equal(-1, book.SelectBasic(Hero(40f), -1, () => 0f));          // 在身后（朝向传错不算够得着）
			Assert.Equal(0, book.SelectBasic(Hero(40f), 1, () => 0f));            // 转过去就够得着
		}

		/// <summary>验证最近水平间距不受高度影响，而有效范围会检查高度。</summary>
		[Fact]
		public void AttackBook_ClosestGapX_IgnoresHeight_HasAttackInReachDoesNot()
		{
			MonsterAttackBook book = new MonsterAttackBook(new[] { Basic(0) });
			Assert.Equal(-29f, book.BasicGapX(Hero(-40f, -120f), -1), 3);
			Assert.False(book.BasicInReach(Hero(-40f, -120f), -1));
			Assert.True(book.BasicInReach(Hero(-40f), -1));
			Assert.Equal(float.PositiveInfinity, book.BasicGapX(default, -1));   // 无目标
		}

		/// <summary>验证缺少范围定义的普攻不会被 AI 选中。</summary>
		[Fact]
		public void AttackBook_MissingReach_IsNotAnAiAttack()
		{
			MonsterAttackBook book = new MonsterAttackBook(new[] { new MonsterAttackSpec { Index = 0, Weight = 100 } });
			Assert.Equal(float.PositiveInfinity, book.BasicGapX(Hero(-40f), -1));
			Assert.False(book.BasicInReach(Hero(-40f), -1));
			Assert.Equal(-1, book.SelectBasic(Hero(-40f), -1, () => 0f));
		}

		/// <summary>验证远程普攻按水平距离选取且忽略目标高度。</summary>
		[Fact]
		public void AttackBook_RangedAttack_UsesHorizontalRange()
		{
			MonsterAttackBook book = new MonsterAttackBook(new[] { RangedBasic(0, 300f) });
			Assert.Equal(0, book.SelectBasic(Hero(-250f, -120f), -1, () => 0f));
			Assert.Equal(-1, book.SelectBasic(Hero(-350f), -1, () => 0f));
			Assert.Equal(50f, book.BasicGapX(Hero(-350f), -1), 3);
		}

		/// <summary>验证可用优先招式中选择优先级最高者。</summary>
		[Fact]
		public void AttackBook_HighestReadyPriorityWins()
		{
			MonsterAttackBook book = new MonsterAttackBook(new[]
			{
				Basic(0), Priority(1, 1, 0, 400, 5), Priority(2, 2, 0, 200, 5),
			});
			Assert.Equal(2, book.HighestPriority(Hero(-150f), -1));
			Assert.Equal(1, book.HighestPriority(Hero(-300f), -1));
			Assert.Equal(-1, book.HighestPriority(Hero(-500f), -1));
			Assert.Equal(2, book.SelectPriority(Hero(-150f), -1, 2, 0f));
		}

		/// <summary>验证招式冷却期间不可选，冷却结束后恢复可用。</summary>
		[Fact]
		public void AttackBook_Cooldown_BlocksUntilTickedDown()
		{
			MonsterAttackBook book = new MonsterAttackBook(new[] { Priority(0, 1, 0, 400, 3) });
			book.MarkUsed(0, () => 0f);
			Assert.Equal(-1, book.HighestPriority(Hero(-100f), -1));
			book.Tick(2.9f);
			Assert.Equal(-1, book.HighestPriority(Hero(-100f), -1));
			book.Tick(0.2f);
			Assert.Equal(1, book.HighestPriority(Hero(-100f), -1));
			Assert.Equal(0, book.SelectPriority(Hero(-100f), -1, 1, 0f));
		}

		/// <summary>验证重置时按随机值设置初始冷却。</summary>
		[Fact]
		public void AttackBook_Reset_RollsInitialCooldown()
		{
			MonsterAttackSpec spec = Priority(0, 1, 0, 400, 10) with { InitCooldown = (3f, 5f) };
			MonsterAttackBook book = new MonsterAttackBook(new[] { spec });
			book.Reset(() => 0.5f);
			Assert.Equal(-1, book.HighestPriority(Hero(-100f), -1));
			book.Tick(3.9f);
			Assert.Equal(-1, book.HighestPriority(Hero(-100f), -1));
			book.Tick(0.2f);
			Assert.Equal(1, book.HighestPriority(Hero(-100f), -1));
		}

		/// <summary>验证普攻按权重区间选择。</summary>
		[Fact]
		public void AttackBook_SelectBasic_Weighted()
		{
			MonsterAttackBook book = new MonsterAttackBook(new[] { Basic(0, weight: 30), Basic(1, weight: 70) });
			Assert.Equal(0, book.SelectBasic(Hero(-40f), -1, () => 0.29f));
			Assert.Equal(1, book.SelectBasic(Hero(-40f), -1, () => 0.31f));
			Assert.Equal(1, book.SelectBasic(Hero(-40f), -1, () => 0.9999f));
		}

		/// <summary>验证权重为零的普攻不可选。</summary>
		[Fact]
		public void AttackBook_ZeroWeight_NeverSelected()
		{
			MonsterAttackBook book = new MonsterAttackBook(new[] { Basic(0, weight: 0) });
			Assert.Equal(-1, book.SelectBasic(Hero(-40f), -1, () => 0f));
			Assert.Equal(float.PositiveInfinity, book.BasicGapX(Hero(-40f), -1));
		}

		/// <summary>验证没有可选招式时不会消耗随机值。</summary>
		[Fact]
		public void AttackBook_DoesNotConsumeRandomnessWithoutAChoice()
		{
			MonsterAttackBook book = new MonsterAttackBook(new[] { Basic(0) });
			int randomCalls = 0;

			Assert.Equal(-1, book.SelectBasic(Hero(-500f), -1, () => ++randomCalls));
			MonsterAttackBook fixedCooldown = new MonsterAttackBook(new[] { Priority(0, 1, 0, 400, 0) });
			fixedCooldown.Reset(() => ++randomCalls);
			fixedCooldown.MarkUsed(0, () => ++randomCalls);

			Assert.Equal(0, randomCalls);
		}

		// ---------------------------------------------------------------- 状态流转（真实 GF.Fsm）

		/// <summary>验证无目标时怪物游荡并可进入暂停状态。</summary>
		[Fact]
		public void Fsm_NoTarget_WandersAndPauses()
		{
			// 随机序列：进入游荡选方向 0.9 → 右；2s 决策点掷停留 0.05 < 10% → Pause
			using AiHarness h = new AiHarness(MonkeyParams(), new[] { Basic(0) }, 0.9f, 0.05f);
			h.Step(1);
			Assert.Equal("Wander", h.State);
			Assert.Equal(1, h.Agent.MoveDir);

			h.Run(2.05f);
			Assert.Equal("Pause", h.State);
			Assert.Equal(0, h.Agent.MoveDir);
		}

		/// <summary>验证游荡超出巡逻半径后转向出生点。</summary>
		[Fact]
		public void Fsm_Wander_LeashTurnsBackTowardHome()
		{
			using AiHarness h = new AiHarness(MonkeyParams(), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			Assert.Equal(1, h.Agent.MoveDir);
			h.Agent.HomeDeltaX = 250f;   // 走出半径 200
			h.Step(1);
			Assert.Equal(-1, h.Agent.MoveDir);
		}

		/// <summary>验证发现目标后追击并在攻击范围内发起攻击。</summary>
		[Fact]
		public void Fsm_TargetSeen_ChasesThenAttacks()
		{
			using AiHarness h = new AiHarness(MonkeyParams(desire: 100), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-200f));
			h.Step(2);   // 帧1 Wander→WalkToTarget，帧2 追击决策
			Assert.Equal("WalkToTarget", h.State);
			Assert.Equal(-1, h.Agent.MoveDir);

			h.Agent.Target(Hero(-66f));   // 吃进 3px：还不够 ReachMargin，继续追
			h.Step(1);
			Assert.Equal("WalkToTarget", h.State);

			h.Agent.Target(Hero(-40f));
			h.Step(1);
			Assert.Equal("StandAndStrike", h.State);
			h.Step(1);   // 进入 StandAndStrike 后首个决策帧即掷（欲望 100 必中）
			Assert.Equal(0, h.Agent.RequestedAttack);
			Assert.Equal(-1, h.Agent.FaceDir);
		}

		/// <summary>验证站定攻击状态在滞回距离内贴近，超出后转为追击。</summary>
		[Fact]
		public void Fsm_StandAndStrike_HysteresisCreepsWithinSlack_ThenChasesBeyond()
		{
			using AiHarness h = new AiHarness(MonkeyParams(desire: 0), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-40f));
			h.Step(2);
			Assert.Equal("StandAndStrike", h.State);

			h.Agent.Target(Hero(-80f));   // 间隙 11 ≤ 滞回 25：留在站定、小步贴近
			h.Step(1);
			Assert.Equal("StandAndStrike", h.State);
			Assert.Equal(-1, h.Agent.MoveDir);

			h.Agent.Target(Hero(-100f));  // 间隙 31 > 25 → 追击
			h.Step(1);
			Assert.Equal("WalkToTarget", h.State);
		}

		/// <summary>验证目标位于头顶时先踱步，落地后再进入攻击决策。</summary>
		[Fact]
		public void Fsm_TargetAboveHead_PacesBelowWithoutAttacking_ThenStrikesWhenLanded()
		{
			using AiHarness h = new AiHarness(MonkeyParams(desire: 100), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-30f, -120f));   // 正上方偏前 30px、高 120px
			h.Step(3);                           // Wander→WalkToTarget→StandAndStrike→PaceBelowTarget
			Assert.Equal("PaceBelowTarget", h.State);

			h.Run(3f);                           // 欲望 100 也不出招
			Assert.Equal("PaceBelowTarget", h.State);
			Assert.Equal(-1, h.Agent.RequestedAttack);

			h.Agent.Target(Hero(-30f));          // 落地
			h.Step(3);                           // PaceBelowTarget→StandAndStrike→首帧即掷（欲望 100）
			Assert.Equal(0, h.Agent.RequestedAttack);
		}

		/// <summary>验证怪物围绕高处目标往返踱步并在目标远离时追击。</summary>
		[Fact]
		public void Fsm_PaceBelowTarget_PacesBackAndForthAroundTargetX()
		{
			using AiHarness h = new AiHarness(MonkeyParams(pace: 60f), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-30f, -120f));
			h.Step(4);
			Assert.Equal("PaceBelowTarget", h.State);
			Assert.Equal(-1, h.Agent.MoveDir);   // 自己在目标右侧 30px：先往目标另一侧走

			h.Agent.Target(Hero(70f, -120f));    // 走到目标左侧 70px（超过半幅 60）：掉头
			h.Step(1);
			Assert.Equal(1, h.Agent.MoveDir);

			h.Agent.Target(Hero(-70f, -120f));   // 走到目标右侧 70px：再掉头
			h.Step(1);
			Assert.Equal(-1, h.Agent.MoveDir);

			h.Agent.Target(Hero(-200f, -120f));  // 目标在平台上走远（超过半幅 + 滞回）：去追
			h.Step(1);
			Assert.Equal("WalkToTarget", h.State);
		}

		/// <summary>验证目标丢失后怪物返回巡逻范围。</summary>
		[Fact]
		public void Fsm_TargetLost_WandersBackIntoPatrolRadius()
		{
			using AiHarness h = new AiHarness(MonkeyParams(), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-200f));
			h.Step(2);
			Assert.Equal("WalkToTarget", h.State);

			h.Agent.HomeDeltaX = -350f;          // 追到出生点左侧 350px（半径 200 之外）时丢失目标
			h.Agent.LoseTarget();
			h.Step(2);
			Assert.Equal("Wander", h.State);
			Assert.Equal(1, h.Agent.MoveDir);    // 游荡先折返回巡逻范围
		}

		/// <summary>验证追击途中会优先发动已就绪的优先招式。</summary>
		[Fact]
		public void Fsm_Chase_FiresReadyPriorityAttackOnTheWay()
		{
			using AiHarness h = new AiHarness(MonkeyParams(), new[] { Basic(0), Priority(1, 1, 100, 400, 8) }, 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-300f));
			h.Step(2);
			Assert.Equal("WalkToTarget", h.State);
			Assert.Equal(1, h.Agent.RequestedAttack);
		}

		/// <summary>验证攻击及收招硬直期间不重新决策，恢复后才转向。</summary>
		[Fact]
		public void Fsm_Attacking_DoesNotDecideUntilRecovered()
		{
			using AiHarness h = new AiHarness(MonkeyParams(desire: 100), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-40f));
			h.Step(3);   // Wander→WalkToTarget→StandAndStrike→出招
			Assert.True(h.Agent.IsAttacking);

			// 出招/收招硬直期间目标绕到身后又跑远：不转身、不移动、不离开站定
			h.Agent.Target(Hero(60f));
			h.Step(3);
			Assert.Equal("StandAndStrike", h.State);
			Assert.Equal(-1, h.Agent.FaceDir);
			Assert.Equal(0, h.Agent.MoveDir);

			h.Agent.IsAttacking = false;   // 收招硬直结束
			h.Step(1);
			Assert.Equal(1, h.Agent.FaceDir);   // 这时才转身
		}

		/// <summary>验证僵直状态可打断其他状态并在计时结束后返回。</summary>
		[Fact]
		public void Fsm_CcLocked_InterruptsAnyState_ThenReturnsAfterCalmTime()
		{
			using AiHarness h = new AiHarness(MonkeyParams(calm: 0.5f), new[] { Basic(0) }, 0.9f);
			h.Step(1);
			h.Agent.Target(Hero(-200f));
			h.Step(1);
			Assert.Equal("WalkToTarget", h.State);

			h.Agent.IsCcLocked = true;
			h.Step(1);
			Assert.Equal("CcLocked", h.State);
			Assert.Equal(0, h.Agent.MoveDir);

			h.Agent.IsCcLocked = false;
			h.Run(0.4f);
			Assert.Equal("CcLocked", h.State);   // 僵直未满
			h.Run(0.2f);
			Assert.Equal("WalkToTarget", h.State);
		}

		/// <summary>验证死亡状态为终止状态。</summary>
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

		// ---------------------------------------------------------------- 测试替身

		private sealed class FakeAgent : IMonsterAiAgent // 可控感知和确定性随机序列的 AI 测试宿主。
		{
			private readonly float[] m_Randoms; // 测试提供的确定性随机序列。
			private int m_RandomIndex; // 下一随机值的读取索引。

			/// <summary>初始化可控感知、招式簿和随机序列。</summary>
			public FakeAgent(MonsterAiParams p, MonsterAttackBook attacks, float[] randoms)
			{
				Params = p;
				Attacks = attacks;
				m_Randoms = randoms.Length == 0 ? new[] { 0.5f } : randoms;
			}

			/// <summary>宿主是否已死亡。</summary>
			public bool IsDead { get; set; }
			/// <summary>宿主是否处于受控僵直。</summary>
			public bool IsCcLocked { get; set; }
			/// <summary>宿主是否正在攻击或收招硬直。</summary>
			public bool IsAttacking { get; set; }
			/// <summary>测试直接设置的目标判定盒；默认值表示无目标。</summary>
			public AiBox TargetBox { get; set; }
			/// <summary>宿主相对出生点的水平偏移。</summary>
			public float HomeDeltaX { get; set; }
			/// <summary>怪物 AI 参数。</summary>
			public MonsterAiParams Params { get; }
			/// <summary>怪物可用招式及冷却状态。</summary>
			public MonsterAttackBook Attacks { get; }

			/// <summary>最近一次移动意图，右为 1、左为 -1、停止为 0。</summary>
			public int MoveDir { get; private set; }
			/// <summary>最近一次朝向意图，右为 1、左为 -1。</summary>
			public int FaceDir { get; private set; }
			/// <summary>最近一次攻击请求索引；尚无请求时为 -1。</summary>
			public int RequestedAttack { get; private set; } = -1;

			/// <summary>设置测试目标判定盒。</summary>
			public void Target(AiBox box) => TargetBox = box;

			/// <summary>清除当前测试目标。</summary>
			public void LoseTarget() => TargetBox = default;

			/// <summary>读取随机序列中的下一值，用尽后重复末值。</summary>
			/// <returns>范围为 0 到 1 的确定性随机值。</returns>
			public float NextRandom()
			{
				float v = m_Randoms[Math.Min(m_RandomIndex, m_Randoms.Length - 1)];
				m_RandomIndex++;
				return v;
			}

			/// <summary>记录 AI 的移动方向意图。</summary>
			public void Move(int dir) => MoveDir = dir;

			/// <summary>未处于攻击期间时记录 AI 的朝向意图。</summary>
			public void Face(int dir)
			{
				if (!IsAttacking)
				{
					FaceDir = dir;
				}
			}

			/// <summary>宿主可攻击时记录请求并标记招式已使用。</summary>
			/// <param name="index">请求的攻击段索引。</param>
			/// <returns>请求被接受时返回 <see langword="true"/>。</returns>
			public bool RequestAttack(int index)
			{
				if (IsAttacking || IsCcLocked || IsDead)
				{
					return false;
				}

				RequestedAttack = index;
				IsAttacking = true;
				Attacks.MarkUsed(index, () => 0f);
				return true;
			}
		}

		private sealed class AiHarness : IDisposable // 用真实 FsmManager 驱动一台 AI 状态机。
		{
			private readonly FsmManager m_Manager = new FsmManager(); // 驱动测试 AI 状态机的框架管理器。
			private readonly IFsm<IMonsterAiAgent> m_Fsm; // 当前测试使用的 AI 状态机。

			/// <summary>状态机绑定的可控测试宿主。</summary>
			public FakeAgent Agent { get; }

			/// <summary>创建 AI 状态机并以游荡状态启动。</summary>
			public AiHarness(MonsterAiParams p, MonsterAttackSpec[] specs, params float[] randoms)
			{
				Agent = new FakeAgent(p, new MonsterAttackBook(specs), randoms);
				m_Fsm = m_Manager.CreateFsm<IMonsterAiAgent>(Guid.NewGuid().ToString(), Agent,
					new PauseState(), new WanderState(), new WalkToTargetState(), new StandAndStrikeState(),
					new PaceBelowTargetState(), new CcLockedState(), new DeathState());
				m_Fsm.Start<WanderState>();
			}

			/// <summary>当前 AI 状态实例。</summary>
			public MonsterAiState CurrentState => (MonsterAiState)m_Fsm.CurrentState;

			/// <summary>当前 AI 状态名称。</summary>
			public string State => CurrentState.StateName;

			/// <summary>按物理帧推进招式冷却和 AI 状态机。</summary>
			/// <param name="frames">推进的物理帧数。</param>
			public void Step(int frames)
			{
				for (int i = 0; i < frames; i++)
				{
					Agent.Attacks.Tick(Dt);
					m_Manager.Update(Dt, Dt);
				}
			}

			/// <summary>按秒数换算帧数并推进 AI 状态机。</summary>
			/// <param name="seconds">推进时长（秒）。</param>
			public void Run(float seconds)
			{
				Step((int)Math.Ceiling(seconds / Dt));
			}

			/// <summary>关闭框架状态机管理器。</summary>
			public void Dispose()
			{
				m_Manager.Shutdown();
			}
		}
	}
}
