using System;
using System.Collections.Generic;
using GameFramework.Fsm;
using GameLogic.Entity.Body;
using GameLogic.Entity.Monsters.Body;
using Godot;
using Xunit;

namespace GameLogic.Battle.Tests
{
	/// <summary>
	/// 怪物身体状态机回归：框架真实 FsmManager + 假宿主，按 <see cref="BodyFsm.Tick{T}"/> 逐物理帧驱动。
	/// AI 的意图（移动、出招请求）由测试直接写进假宿主，验证"AI 只写意图、身体是唯一权威"的边界；
	/// 动作时长 = 参数里的动画长度快照，状态自己计时（A 方案：动画纯数据）。
	/// </summary>
	public class MonsterBodyTests
	{
		private const float Dt = 1f / 60f; // 单个物理帧的时长。

		private static MonsterBodyParams Params(float attackTime = 0.1f, float recovery = 0.3f,
			float hurtTime = 0.2f, float deathTime = 0.5f) // 创建用于状态机测试的怪物参数。
		{
			return new MonsterBodyParams
			{
				MoveSpeed = 80f,
				Gravity = 980f,
				HurtTime = hurtTime,
				DeathTime = deathTime,
				AttackAnims = ["attack_1"],
				AttackTimes = [attackTime],
				AttackRecovery = [recovery],
			};
		}

		/// <summary>验证移动意图控制怪物速度、动画和朝向。</summary>
		[Fact]
		public void Move_FollowsIntent_AndPicksAnim()
		{
			using MonsterHarness h = new MonsterHarness(Params());
			h.Step(1);
			Assert.Equal("Move", h.State);
			Assert.Equal("idle", h.Body.LastAnim);

			h.Body.MoveIntent = 1;
			h.Step(1);
			Assert.Equal("run", h.Body.LastAnim);
			Assert.Equal(80f, h.Body.Velocity.X, 3);
			Assert.Equal(1, h.Body.Facing);
		}

		/// <summary>验证怪物攻击、收招硬直及其后的移动状态。</summary>
		[Fact]
		public void Attack_Request_Recovery_ThenMove()
		{
			using MonsterHarness h = new MonsterHarness(Params());
			h.Body.MoveIntent = 1;
			h.Body.AttackRequest = 0;
			h.Step(1);
			Assert.Equal("Attack", h.State);
			Assert.Equal("attack_1", h.Body.LastAnim);
			Assert.Equal(0, h.Body.AttackSegment);
			Assert.Equal(1, h.Body.BeginCount);
			Assert.Equal(0f, h.Body.Velocity.X, 3);       // 出招定身

			h.StepUntil(s => s == "Recovery");            // 招式时长走完 → 收招硬直
			Assert.Equal(-1, h.Body.AttackSegment);
			Assert.Equal(1, h.Body.EndCount);

			h.Body.MoveIntent = 1;
			h.Body.AttackRequest = 0;                      // 硬直中的请求不被消费
			h.Step(15);                                    // 0.3s 内
			Assert.Equal("Recovery", h.State);
			h.StepUntil(s => s == "Attack");               // 硬直结束当帧 Move 消费请求
			Assert.Equal("Attack", h.State);
		}

		/// <summary>验证零收招时长的攻击直接返回移动状态。</summary>
		[Fact]
		public void Attack_ZeroRecovery_ReturnsToMove()
		{
			using MonsterHarness h = new MonsterHarness(Params(recovery: 0f));
			h.Body.AttackRequest = 0;
			h.Step(1);
			h.StepUntil(s => s == "Move");
			Assert.Equal("Move", h.State);
		}

		/// <summary>验证受击会中断攻击并清除未处理的攻击请求。</summary>
		[Fact]
		public void Hurt_InterruptsAttack_AndDropsRequest()
		{
			using MonsterHarness h = new MonsterHarness(Params());
			h.Body.AttackRequest = 0;
			h.Step(1);
			Assert.Equal("Attack", h.State);

			h.Body.PendingHurt = new Vector2(120f, 0f);
			h.Body.AttackRequest = 0;
			h.Step(1);
			Assert.Equal("Hurt", h.State);
			Assert.Equal("hurt", h.Body.LastAnim);
			Assert.Equal(-1, h.Body.AttackSegment);
			Assert.Equal(1, h.Body.EndCount);
			Assert.Equal(120f, h.Body.Velocity.X, 3);

			h.Step(10);                                    // 硬直时长内不结束
			Assert.Equal("Hurt", h.State);
			h.StepUntil(s => s == "Move");
			Assert.Equal("Move", h.State);
			Assert.Equal(-1, h.Body.AttackRequest);        // 硬直中的请求已作废
		}

		/// <summary>验证受击会中断收招硬直。</summary>
		[Fact]
		public void Hurt_InterruptsRecovery()
		{
			using MonsterHarness h = new MonsterHarness(Params(recovery: 1f));
			h.Body.AttackRequest = 0;
			h.Step(1);
			h.StepUntil(s => s == "Recovery");
			Assert.Equal("Recovery", h.State);

			h.Body.PendingHurt = new Vector2(-50f, 0f);
			h.Step(1);
			Assert.Equal("Hurt", h.State);
		}

		/// <summary>验证宿主不登记受击时霸体攻击不会被中断。</summary>
		[Fact]
		public void SuperArmor_HostNotRegisteringHurt_KeepsAttack()
		{
			// 霸体由宿主"不登记受击"表达：身体状态机看不到受击，出招照常走完（0.1s → Recovery）
			using MonsterHarness h = new MonsterHarness(Params());
			h.Body.AttackRequest = 0;
			h.Step(1);
			h.Step(10);
			Assert.Equal("Recovery", h.State);   // 没被打断，正常收招
		}

		/// <summary>验证死亡优先处理、一次性副作用及延时回收。</summary>
		[Fact]
		public void Death_Priority_SideEffectsOnce_RecycleAfterDeathTime()
		{
			using MonsterHarness h = new MonsterHarness(Params());
			h.Body.AttackRequest = 0;
			h.Step(1);
			h.Body.Dead = true;
			h.Body.PendingHurt = new Vector2(10f, 0f);
			h.Step(1);
			Assert.Equal("Death", h.State);
			Assert.Equal("death", h.Body.LastAnim);
			Assert.Equal(1, h.Body.DiedCount);
			Assert.Equal(-1, h.Body.AttackSegment);

			h.Step(28);                                    // deathTime 0.5s 内不回收
			Assert.Equal("Death", h.State);
			Assert.Equal(0, h.Body.RecycleCount);
			h.StepUntil(s => h.Body.RecycleCount > 0);     // 死亡时长走完才回收
			Assert.Equal(1, h.Body.RecycleCount);
			h.Step(30);
			Assert.Equal(1, h.Body.RecycleCount);          // 只请求一次
			Assert.Equal(1, h.Body.DiedCount);
		}

		/// <summary>验证状态机关闭时会结束进行中的攻击。</summary>
		[Fact]
		public void Shutdown_DuringAttack_ReleasesAttack()
		{
			MonsterHarness h = new MonsterHarness(Params());
			h.Body.AttackRequest = 0;
			h.Step(1);
			h.Dispose();
			Assert.Equal(1, h.Body.EndCount);
		}

		// ---------------------------------------------------------------- 假宿主与驱动

		private sealed class FakeMonster : FakeActorBody, IMonsterBody // 为怪物身体状态机提供可控的测试宿主。
		{
			/// <summary>初始化怪物身体状态参数。</summary>
			public FakeMonster(MonsterBodyParams p)
			{
				Params = p;
			}

			/// <summary>怪物身体状态参数。</summary>
			public MonsterBodyParams Params { get; }
			/// <summary>AI 写入的移动方向意图。</summary>
			public int MoveIntent { get; set; }
			/// <summary>待身体状态机消费的攻击段索引。</summary>
			public int AttackRequest = -1;
			/// <summary>请求回收的次数。</summary>
			public int RecycleCount;

			/// <summary>取出并清除待处理的攻击请求。</summary>
			public int TakeAttackRequest()
			{
				int r = AttackRequest;
				AttackRequest = -1;
				return r;
			}

			/// <summary>记录一次实体回收请求。</summary>
			public void RequestRecycle() => RecycleCount++;
		}

		private sealed class MonsterHarness : IDisposable // 用真实框架状态机驱动怪物身体测试。
		{
			private readonly FsmManager m_Manager = new FsmManager(); // 驱动测试状态机的管理器。
			private readonly IFsm<IMonsterBody> m_Fsm; // 当前测试使用的怪物身体状态机。
			private bool m_Disposed; // 记录驱动器是否已关闭。

			/// <summary>创建假宿主并启动怪物身体状态机。</summary>
			public MonsterHarness(MonsterBodyParams p)
			{
				Body = new FakeMonster(p);
				m_Fsm = m_Manager.CreateFsm<IMonsterBody>(Guid.NewGuid().ToString(), Body,
					new MonsterMoveState(), new MonsterAttackState(), new MonsterRecoveryState(), new MonsterHurtState(),
					new MonsterDeathState());
				m_Fsm.Start<MonsterMoveState>();
			}

			/// <summary>状态机绑定的假怪物宿主。</summary>
			public FakeMonster Body { get; }
			/// <summary>当前身体状态名称。</summary>
			public string State => BodyFsm.CurrentName(m_Fsm);

			/// <summary>推进指定数量的物理帧，并固定宿主在地面上。</summary>
			/// <param name="frames">推进的物理帧数。</param>
			public void Step(int frames)
			{
				for (int i = 0; i < frames; i++)
				{
					BodyFsm.Tick(m_Fsm, Dt);
					Body.Velocity = new Vector2(Body.Velocity.X, 0f);   // 始终站在地面
				}
			}

			/// <summary>逐帧推进至条件成立，最多检查 600 帧。</summary>
			/// <param name="until">返回是否停止推进的状态条件。</param>
			/// <returns>满足条件前推进的帧数；超时返回 -1。</returns>
			public int StepUntil(Func<string, bool> until)
			{
				// 每帧先检查状态，再推进一帧，避免把满足条件的帧多算一次。
				for (int i = 0; i < 600; i++)
				{
					if (until(State))
					{
						return i;
					}

					Step(1);
				}

				return -1;
			}

			/// <summary>关闭状态机管理器；重复调用不会重复关闭。</summary>
			public void Dispose()
			{
				if (m_Disposed)
				{
					return;
				}

				m_Disposed = true;
				m_Manager.Shutdown();
			}
		}
	}
}
