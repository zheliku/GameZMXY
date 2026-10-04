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
		/// <summary>单个物理帧的时长。</summary>
		private const float Dt = 1f / 60f;

		/// <summary>创建用于状态机测试的英雄参数。</summary>
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

		/// <summary>验证双击方向进入奔跑且松开后退出。</summary>
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

		/// <summary>验证跳跃输入缓冲在窗口内保留、到期后清除。</summary>
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

		/// <summary>验证零时长缓冲只在按下帧有效。</summary>
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

		/// <summary>验证状态机启动后进入地面待机。</summary>
		[Fact]
		public void Start_OnGround_IdlesInGround()
		{
			using HeroHarness h = new HeroHarness(Params());
			h.Step(2);
			Assert.Equal("Ground", h.State);
			Assert.Equal("idle1", h.Body.LastAnim);
		}

		/// <summary>验证四段攻击连段推进并在末段后重新循环。</summary>
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

		/// <summary>验证攻击段在配置时长结束前不会切换。</summary>
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

		/// <summary>验证无连段输入時结束当前攻击并保留下次段号。</summary>
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

		/// <summary>验证每段攻击开始一次且退出攻击时统一结束。</summary>
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

		/// <summary>验证英雄在攻击段完成前延迟处理受击。</summary>
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

		/// <summary>验证受击按配置时长结束并丢弃受击前的缓冲输入。</summary>
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

		/// <summary>验证受击硬直中再次受击会重播动画、音效并更新击退。</summary>
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

		/// <summary>验证受击硬直期间移动输入不会覆盖击退速度。</summary>
		[Fact]
		public void Hurt_KnockbackHeldDuringHurt()
		{
			using HeroHarness h = new HeroHarness(Params());
			h.Body.PendingHurt = new Vector2(-100f, 0f);
			h.Step(5, move: 1);                            // 硬直中按方向键无效
			Assert.Equal("Hurt", h.State);
			Assert.Equal(-100f, h.Body.Velocity.X, 3);
		}

		/// <summary>验证英雄最多起跳两次。</summary>
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

		/// <summary>验证落地时触发仍有效的跳跃缓冲。</summary>
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

		/// <summary>验证空中攻击结束后不会推进连段。</summary>
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

		/// <summary>验证死亡可中断攻击并保持终止状态。</summary>
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

		/// <summary>验证持续待机后播放表情动画并按时返回待机。</summary>
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

		/// <summary>验证移动速度、动画选择与朝向跟随输入。</summary>
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

		/// <summary>为英雄身体状态机提供可控的测试宿主。</summary>
		private sealed class FakeHero : FakeActorBody, IHeroBody
		{
			/// <summary>初始化测试参数与输入对象。</summary>
			public FakeHero(HeroBodyParams p, HeroInput input)
			{
				Params = p;
				Input = input;
			}

			/// <summary>英雄身体状态参数。</summary>
			public HeroBodyParams Params { get; }
			/// <summary>由测试驱动的输入状态。</summary>
			public HeroInput Input { get; }
			/// <summary>宿主报告的落地状态。</summary>
			public bool OnFloor { get; set; } = true;
			/// <summary>当前已使用的跳跃次数。</summary>
			public int JumpCount { get; set; }
			/// <summary>下一次普攻使用的连段索引。</summary>
			public int ComboIndex { get; set; }
			/// <summary>返回固定随机值以保持测试可重复。</summary>
			public float NextRandom() => 0f;
		}

		/// <summary>真实 FsmManager 建身体状态机；每帧：喂输入 → BodyFsm.Tick → 模拟落地物理。</summary>
		private sealed class HeroHarness : IDisposable
		{
			/// <summary>驱动真实框架有限状态机的管理器。</summary>
			private readonly FsmManager m_Manager = new FsmManager();
			/// <summary>当前测试使用的英雄身体状态机。</summary>
			private readonly IFsm<IHeroBody> m_Fsm;

			/// <summary>创建英雄输入、假宿主和身体状态机。</summary>
			public HeroHarness(HeroBodyParams p, float bufferTime = 0f)
			{
				Input = new HeroInput(0.3f, bufferTime);
				Body = new FakeHero(p, Input);
				m_Fsm = m_Manager.CreateFsm<IHeroBody>(Guid.NewGuid().ToString(), Body,
					new HeroGroundState(), new HeroAirState(), new HeroAttackState(), new HeroHurtState(),
					new HeroDeathState());
				m_Fsm.Start<HeroGroundState>();
			}

			/// <summary>状态机绑定的假英雄宿主。</summary>
			public FakeHero Body { get; }
			/// <summary>测试驱动的英雄输入。</summary>
			public HeroInput Input { get; }
			/// <summary>当前身体状态名称。</summary>
			public string State => BodyFsm.CurrentName(m_Fsm);

			/// <summary>一帧：本帧按下 attack/jump（单帧边沿）。</summary>
			public void Press(bool attack = false, bool jump = false)
			{
				Input.Sample(Dt, false, false, false, false, jump, attack);
				TickOnly(1);
			}

			/// <summary>逐帧采样方向输入并推进状态机。</summary>
			/// <param name="frames">推进的物理帧数。</param>
			/// <param name="move">方向输入，-1 为左、1 为右、0 为无输入。</param>
			public void Step(int frames, int move = 0)
			{
				for (int i = 0; i < frames; i++)
				{
					Input.Sample(Dt, move < 0, move > 0, false, false, false, false);
					TickOnly(1);
				}
			}

			/// <summary>按秒数换算物理帧并推进状态机。</summary>
			/// <param name="seconds">推进时长（秒）。</param>
			public void Run(float seconds)
			{
				Step((int)Math.Round(seconds / Dt));
			}

			/// <summary>逐帧推进至条件成立，最多检查 600 帧。</summary>
			/// <param name="until">返回是否停止推进的状态条件。</param>
			/// <returns>满足条件前推进的帧数；超时返回 -1。</returns>
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

			/// <summary>推进状态机与落地模拟，不重新采样输入。</summary>
			/// <param name="frames">推进的物理帧数。</param>
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

			/// <summary>关闭状态机管理器并释放测试状态机。</summary>
			public void Dispose() => m_Manager.Shutdown();
		}
	}
}
