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
		private const float Dt = 1f / 60f;

		private static MonsterBodyParams Params(float attackTime = 0.1f, float recovery = 0.3f,
			float hurtTime = 0.2f, float deathTime = 0.5f)
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

		[Fact]
		public void Attack_ZeroRecovery_ReturnsToMove()
		{
			using MonsterHarness h = new MonsterHarness(Params(recovery: 0f));
			h.Body.AttackRequest = 0;
			h.Step(1);
			h.StepUntil(s => s == "Move");
			Assert.Equal("Move", h.State);
		}

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

		private sealed class FakeMonster : FakeActorBody, IMonsterBody
		{
			public FakeMonster(MonsterBodyParams p)
			{
				Params = p;
			}

			public MonsterBodyParams Params { get; }
			public int MoveIntent { get; set; }
			public int AttackRequest = -1;
			public int RecycleCount;

			public int TakeAttackRequest()
			{
				int r = AttackRequest;
				AttackRequest = -1;
				return r;
			}

			public void RequestRecycle() => RecycleCount++;
		}

		private sealed class MonsterHarness : IDisposable
		{
			private readonly FsmManager m_Manager = new FsmManager();
			private readonly IFsm<IMonsterBody> m_Fsm;
			private bool m_Disposed;

			public MonsterHarness(MonsterBodyParams p)
			{
				Body = new FakeMonster(p);
				m_Fsm = m_Manager.CreateFsm<IMonsterBody>(Guid.NewGuid().ToString(), Body,
					new MonsterMoveState(), new MonsterAttackState(), new MonsterRecoveryState(), new MonsterHurtState(),
					new MonsterDeathState());
				m_Fsm.Start<MonsterMoveState>();
			}

			public FakeMonster Body { get; }
			public string State => BodyFsm.CurrentName(m_Fsm);

			public void Step(int frames)
			{
				for (int i = 0; i < frames; i++)
				{
					BodyFsm.Tick(m_Fsm, Dt);
					Body.Velocity = new Vector2(Body.Velocity.X, 0f);   // 始终站在地面
				}
			}

			/// <summary>推进直到条件满足（每帧检查状态），返回消耗的帧数；超过 600 帧返回 -1。</summary>
			public int StepUntil(Func<string, bool> until)
			{
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
