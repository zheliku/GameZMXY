using System;
using System.Collections.Generic;
using GameFramework.Fsm;
using GameLogic.Entity.Body;
using GameLogic.Entity.Heroes.Body;
using Godot;
using Xunit;

namespace GameLogic.Battle.Tests
{
	/// <summary>
	/// 英雄身体状态机回归：用框架真实 FsmManager + 假宿主，按 <see cref="BodyFsm.Tick{T}"/> 逐物理帧驱动
	/// （同时验证"GF.Fsm 按物理帧驱动"这一做法本身）。动作时长 = 参数里的动画长度快照，状态自己计时——
	/// 测试里没有动画，时长由 <see cref="HeroBodyParams.AttackTimes"/> 等参数直接给出（A 方案：动画纯数据）。
	/// </summary>
	public class HeroBodyTests
	{
		private const float Dt = 1f / 60f;

		private static HeroBodyParams Params(float attackTime = 0.1f, float hurtTime = 0.2f, float emoteTime = 0.5f)
		{
			return new HeroBodyParams
			{
				WalkSpeed = 120f,
				RunSpeed = 240f,
				JumpSpeed = 540f,
				Gravity = 980f,
				JumpCountMax = 2,
				HurtTime = hurtTime,
				EmoteAnim = "idle2",
				EmoteTime = emoteTime,
				EmoteDelayMin = 1f,
				EmoteDelayMax = 1f,
				AttackAnims = ["attack_1", "attack_2", "attack_3", "attack_4"],
				AttackTimes = [attackTime, attackTime, attackTime, attackTime],
			};
		}

		// ---------------------------------------------------------------- 输入层

		[Fact]
		public void Input_DoubleTap_EntersRun_ReleaseExits()
		{
			HeroInput input = new HeroInput(0.3f, 0f);
			input.Sample(Dt, false, true, false, true, false, false);
			Assert.False(input.Running);
			input.Sample(Dt, false, false, false, false, false, false);
			input.Sample(Dt, false, true, false, true, false, false);
			Assert.True(input.Running);
			input.Sample(Dt, false, false, false, false, false, false);
			Assert.False(input.Running);
		}

		[Fact]
		public void Input_Buffer_HoldsWithinWindow_ExpiresAfter()
		{
			HeroInput input = new HeroInput(0.3f, 0.09f);
			input.Sample(Dt, false, false, false, false, true, false);   // 按下那帧：age = 0
			for (int i = 0; i < 5; i++)
			{
				input.Sample(Dt, false, false, false, false, false, false);
			}

			Assert.True(input.JumpBuffered);   // 又过 5 帧：age = 0.083s ≤ 0.09s

			input.Sample(Dt, false, false, false, false, false, false);
			Assert.False(input.JumpBuffered);  // 第 6 帧：age = 0.1s > 0.09s
		}

		[Fact]
		public void Input_ZeroBuffer_OnlyPressFrame()
		{
			HeroInput input = new HeroInput(0.3f, 0f);
			input.Sample(Dt, false, false, false, false, false, true);
			Assert.True(input.AttackBuffered);
			input.Sample(Dt, false, false, false, false, false, false);
			Assert.False(input.AttackBuffered);
		}

		// ---------------------------------------------------------------- 状态流转

		[Fact]
		public void Start_OnGround_IdlesInGround()
		{
			using HeroHarness h = new HeroHarness(Params());
			h.Step(2);
			Assert.Equal("Ground", h.State);
			Assert.Equal("idle1", h.Body.LastAnim);
		}

		[Fact]
		public void Combo_ChainsOneToFour_ThenLoops()
		{
			using HeroHarness h = new HeroHarness(Params());
			h.Step(1);
			h.Press(attack: true);
			Assert.Equal("Attack", h.State);
			Assert.Equal("attack_1", h.Body.LastAnim);

			for (int seg = 2; seg <= 4; seg++)
			{
				h.Press(attack: true);                       // 出招中按普攻 = 连击缓冲
				int frames = h.StepUntil(s => s == "Attack" && h.Body.LastAnim == $"attack_{seg}");
				Assert.True(frames < 12, $"段 {seg} 没有接上（等了 {frames} 帧）");
				Assert.Equal($"attack_{seg}", h.Body.LastAnim);
				Assert.Equal(seg - 1, h.Body.AttackSegment);
			}

			h.Press(attack: true);
			h.StepUntil(s => s == "Ground");                 // 末段时长走完：不再推进
			Assert.Equal("Ground", h.State);
			Assert.Equal(-1, h.Body.AttackSegment);
			Assert.Equal(0, h.Body.ComboIndex);

			h.Step(1);
			h.Press(attack: true);
			Assert.Equal("attack_1", h.Body.LastAnim);
		}

		[Fact]
		public void Combo_SegmentLastsAttackTime()
		{
			// 段时长 = AttackTimes[0]（0.1s ≈ 6 帧，含进入帧）：时长没走完不切下一段
			using HeroHarness h = new HeroHarness(Params(attackTime: 0.1f));
			h.Step(1);
			h.Press(attack: true);
			h.Press(attack: true);   // 连击缓冲
			h.Step(4);               // 共 5 帧 < 0.1s
			Assert.Equal("Attack", h.State);
			Assert.Equal("attack_1", h.Body.LastAnim);
		}

		[Fact]
		public void Combo_WithoutChainInput_EndsAndNextPressContinuesSequence()
		{
			using HeroHarness h = new HeroHarness(Params());
			h.Step(1);
			h.Press(attack: true);
			h.StepUntil(s => s == "Ground");
			Assert.Equal("Ground", h.State);
			Assert.Equal(1, h.Body.ComboIndex);

			h.Step(1);
			h.Press(attack: true);
			Assert.Equal("attack_2", h.Body.LastAnim);   // 同旧 hit_count：跨按键保持
		}

		[Fact]
		public void Attack_BeginEndAttack_PairedOnEverySegment()
		{
			using HeroHarness h = new HeroHarness(Params());
			h.Step(1);
			h.Press(attack: true);
			Assert.Equal(1, h.Body.BeginCount);
			h.Press(attack: true);
			h.StepUntil(s => h.Body.LastAnim == "attack_2");
			h.Press(attack: true);
			h.StepUntil(s => h.Body.LastAnim == "attack_3");
			h.Press(attack: true);
			h.StepUntil(s => h.Body.LastAnim == "attack_4");
			h.StepUntil(s => s == "Ground");
			// 四段提交四次（连段推进不离开状态，收招离开时补一次归还）
			Assert.Equal(4, h.Body.BeginCount);
			Assert.Equal(1, h.Body.EndCount);
			Assert.Equal(-1, h.Body.AttackSegment);
		}

		[Fact]
		public void Hurt_DuringAttack_IsDeferredUntilAttackTime()
		{
			using HeroHarness h = new HeroHarness(Params());
			h.Step(1);
			h.Press(attack: true);
			h.Body.PendingHurt = new Vector2(-150f, 0f);
			h.Step(3);
			Assert.Equal("Attack", h.State);    // 出招不被打断

			h.StepUntil(s => s == "Hurt");      // 段时长走完收招 → 经 Ground 的打断进 Hurt
			Assert.Equal("Hurt", h.State);
			Assert.Equal("hurt", h.Body.LastAnim);
			Assert.Equal(-150f, h.Body.Velocity.X, 3);
			Assert.Equal(1, h.Body.HurtSounds);
		}

		[Fact]
		public void Hurt_LastsHurtTime_AndDropsBufferedInput()
		{
			using HeroHarness h = new HeroHarness(Params());
			h.Input.Sample(Dt, false, false, false, false, false, true);   // 硬直前按的普攻
			h.Body.PendingHurt = new Vector2(-100f, 0f);
			h.TickOnly(1);
			Assert.Equal("Hurt", h.State);

			h.Step(10);                         // 0.2s 内不结束
			Assert.Equal("Hurt", h.State);
			h.StepUntil(s => s == "Ground");
			Assert.Equal("Ground", h.State);
			Assert.Equal(-1, h.Body.AttackSegment);   // 硬直前的按键没带出来
		}

		[Fact]
		public void Hurt_Rehit_ReplaysHurtAnimAndKnockback()
		{
			using HeroHarness h = new HeroHarness(Params());
			h.Body.PendingHurt = new Vector2(-100f, 0f);
			h.Step(1);
			int plays = h.Body.Anims.Count;
			int sounds = h.Body.HurtSounds;
			h.Step(3);
			Assert.Equal(plays, h.Body.Anims.Count);           // 硬直中不重播
			Assert.Equal(sounds, h.Body.HurtSounds);

			h.Body.PendingHurt = new Vector2(120f, 0f);
			h.Step(1);
			Assert.Equal(plays + 1, h.Body.Anims.Count);       // 再受击：重播
			Assert.Equal(sounds + 1, h.Body.HurtSounds);
			Assert.Equal(120f, h.Body.Velocity.X, 3);
			Assert.Equal("hurt", h.Body.LastAnim);
		}

		[Fact]
		public void Hurt_KnockbackHeldDuringHurt()
		{
			using HeroHarness h = new HeroHarness(Params());
			h.Body.PendingHurt = new Vector2(-100f, 0f);
			h.Step(5, move: 1);                            // 硬直中按方向键无效
			Assert.Equal("Hurt", h.State);
			Assert.Equal(-100f, h.Body.Velocity.X, 3);
		}

		[Fact]
		public void Jump_Twice_ThenRefused()
		{
			using HeroHarness h = new HeroHarness(Params());
			h.Step(1);
			h.Press(jump: true);
			Assert.Equal("Air", h.State);
			Assert.Equal(1, h.Body.JumpCount);
			Assert.Equal("jump", h.Body.LastAnim);

			h.Step(10);
			h.Press(jump: true);
			Assert.Equal(2, h.Body.JumpCount);
			Assert.Equal("jump_2", h.Body.LastAnim);

			h.Step(10);
			h.Press(jump: true);
			Assert.Equal(2, h.Body.JumpCount);
		}

		[Fact]
		public void Jump_Buffered_FiresOnLanding()
		{
			using HeroHarness h = new HeroHarness(Params(), bufferTime: 0.1f);
			h.Body.JumpCount = 2;                          // 二段跳已用完，正在下落
			h.Body.OnFloor = false;
			h.Body.Velocity = new Vector2(0f, 300f);
			h.Step(1);
			Assert.Equal("Air", h.State);

			h.Press(jump: true);                           // 落地前 2 帧按跳：被拒绝但进缓冲
			Assert.Equal(2, h.Body.JumpCount);
			h.Step(1);
			h.Body.OnFloor = true;
			h.Body.JumpCount = 0;                          // 宿主在落地后归零
			h.Body.Velocity = new Vector2(0f, 0f);
			h.Step(1);
			Assert.Equal("Air", h.State);                  // 缓冲生效：落地当帧起跳
			Assert.Equal(1, h.Body.JumpCount);
		}

		[Fact]
		public void AirAttack_DoesNotChain()
		{
			using HeroHarness h = new HeroHarness(Params());
			h.Body.OnFloor = false;
			h.Body.Velocity = new Vector2(0f, 50f);
			h.Step(1);
			h.Press(attack: true);
			Assert.Equal("Attack", h.State);
			h.Press(attack: true);
			h.StepUntil(s => s != "Attack");
			Assert.Equal("Air", h.State);                  // 空中起手：不推进连段
		}

		[Fact]
		public void Death_TakesPriority_EvenDuringAttack()
		{
			using HeroHarness h = new HeroHarness(Params());
			h.Step(1);
			h.Press(attack: true);
			h.Body.PendingHurt = new Vector2(-100f, 0f);
			h.Body.Dead = true;
			h.Step(1);
			Assert.Equal("Death", h.State);
			Assert.Equal("death", h.Body.LastAnim);
			Assert.Equal(1, h.Body.DiedCount);
			Assert.Equal(-1, h.Body.AttackSegment);
			Assert.True(h.Body.EndCount >= 1);

			h.Press(attack: true, jump: true);
			h.Step(5);
			Assert.Equal("Death", h.State);
		}

		[Fact]
		public void Emote_PlaysAfterContinuousIdle_EndsAfterEmoteTime()
		{
			using HeroHarness h = new HeroHarness(Params(emoteTime: 0.5f));
			h.Run(1.1f);                                   // 连续静止满 1s（EmoteDelayMin=Max=1）
			Assert.Equal("idle2", h.Body.LastAnim);

			h.Step(10);                                    // 0.5s 内一直在播
			Assert.Equal("idle2", h.Body.LastAnim);
			h.StepUntil(s => h.Body.LastAnim == "idle1");  // 播完回待机
			Assert.Equal("idle1", h.Body.LastAnim);

			h.Run(1.1f);
			h.Step(1, move: 1);                            // 走动一下：静止计时归零
			h.Run(0.9f);
			Assert.Equal("idle1", h.Body.LastAnim);
		}

		[Fact]
		public void Locomotion_WalkRunAndFacing()
		{
			using HeroHarness h = new HeroHarness(Params());
			h.Step(1, move: 1);
			Assert.Equal("walk", h.Body.LastAnim);
			Assert.Equal(120f, h.Body.Velocity.X, 3);
			Assert.Equal(1, h.Body.Facing);

			h.Step(1, move: -1);
			Assert.Equal(-120f, h.Body.Velocity.X, 3);
			Assert.Equal(-1, h.Body.Facing);
		}

		// ---------------------------------------------------------------- 假宿主与驱动

		private sealed class FakeHero : FakeActorBody, IHeroBody
		{
			public FakeHero(HeroBodyParams p, HeroInput input)
			{
				Params = p;
				Input = input;
			}

			public HeroBodyParams Params { get; }
			public HeroInput Input { get; }
			public bool OnFloor { get; set; } = true;
			public int JumpCount { get; set; }
			public int ComboIndex { get; set; }
			public float NextRandom() => 0f;
		}

		/// <summary>真实 FsmManager 建身体状态机；每帧：喂输入 → BodyFsm.Tick → 模拟落地物理。</summary>
		private sealed class HeroHarness : IDisposable
		{
			private readonly FsmManager m_Manager = new FsmManager();
			private readonly IFsm<IHeroBody> m_Fsm;

			public HeroHarness(HeroBodyParams p, float bufferTime = 0f)
			{
				Input = new HeroInput(0.3f, bufferTime);
				Body = new FakeHero(p, Input);
				m_Fsm = m_Manager.CreateFsm<IHeroBody>(Guid.NewGuid().ToString(), Body,
					new HeroGroundState(), new HeroAirState(), new HeroAttackState(), new HeroHurtState(),
					new HeroDeathState());
				m_Fsm.Start<HeroGroundState>();
			}

			public FakeHero Body { get; }
			public HeroInput Input { get; }
			public string State => BodyFsm.CurrentName(m_Fsm);

			/// <summary>一帧：本帧按下 attack/jump（单帧边沿）。</summary>
			public void Press(bool attack = false, bool jump = false)
			{
				Input.Sample(Dt, false, false, false, false, jump, attack);
				TickOnly(1);
			}

			public void Step(int frames, int move = 0)
			{
				for (int i = 0; i < frames; i++)
				{
					Input.Sample(Dt, move < 0, move > 0, false, false, false, false);
					TickOnly(1);
				}
			}

			public void Run(float seconds)
			{
				Step((int)Math.Round(seconds / Dt));
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

			/// <summary>只推进状态机与模拟物理（不重采样输入）。</summary>
			public void TickOnly(int frames)
			{
				for (int i = 0; i < frames; i++)
				{
					BodyFsm.Tick(m_Fsm, Dt);
					if (Body.OnFloor && Body.Velocity.Y > 0f)
					{
						Body.Velocity = new Vector2(Body.Velocity.X, 0f);
					}
				}
			}

			public void Dispose() => m_Manager.Shutdown();
		}
	}
}
