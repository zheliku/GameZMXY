using System;
using System.Collections.Generic;
using GameConfig.Battle;
using GameConfig.Sound;
using GameFramework;
using GameFramework.Event;
using GameLogic.Battle;
using GameLogic.Entity.Heroes;
using GameLogic.Entity.Monsters;
using GameLogic.Entity.Monsters.AI;
using GameLogic.Event;
using Godot;
using GodotGameFramework;

/// <summary>
/// 冒烟场景：怪物 AI（M5 完成标准"猴子巡逻、追击、攻击"+ 2026-09-30 判定盒范围/收招硬直）。
/// 由 <see cref="SmokeTestDriver"/> 在 `-- --smoketest=ai` 时驱动，英雄全程不输入（站桩当目标）。
///
/// 阶段（事件驱动，时间只作超时兜底）：
///  0 视野外   猴子挪到英雄右侧 600px → 应当巡逻（Patrol/Idle），不追击；
///  1 进视野   3s 时挪到右侧 150px → Chase → Attack，出招 attack_1 并打中英雄；
///  2 转身     打中英雄后，下一次出招的当帧把英雄瞬移到猴子身后 → 出招与收招硬直期间朝向不变、出招中身体/动画一直是
///             Attack/attack_1；硬直结束后 1s 内转向英雄；
///  3 平台     英雄钉在猴子前方高 120px 的固定点 AirDuration 秒 → 不出招、AI 进 Hold、在英雄 x 两侧来回踱步；
///  4 落地     放英雄落地 → 猴子回 Attack 并出招；
///  5 丢失     英雄始终保持在猴子视野外（模拟跑得比它快）→ LoseTargetTime 后 AI 回 Patrol/Idle；随后以英雄身份打一下（带击退）→ 重新锁定、CcLocked/Hurt；
///  6 击杀     致命伤 → AI 进 Death、广播 MonsterDiedEventArgs（击杀者 = 英雄）、死亡动画后回收。
/// 另断言：攻击范围由 attack_1 判定盒推导 = 50×50 @(-24,-23) → X[-49,1] Y[-48,2]。
/// </summary>
public sealed class MonsterAiSmokeScenario
{
	private const double InSightAt = 3.0; // 进入视野并开始追击的时间（秒）。
	private const double TimeoutAt = 36.0; // 整体超时，必须在引擎退出前给出结论（秒）。
	private const double TurnAroundWindow = 1.0; // 收招硬直结束后允许转向的时限（秒）。
	private const double AirDuration = 4.0; // 空中阶段持续时间（秒）。
	private const float AirHeight = 120f; // 空中阶段英雄离地高度（像素）。
	private const float AirOffsetX = 30f; // 空中阶段英雄相对猴子的水平偏移（像素）。
	private const float PaceEvidence = 20f; // 认定猴子在英雄两侧踱步所需的最小距离（像素）。
	private const double LandedTimeout = 6.0; // 英雄落地后等待猴子出招的时限（秒）；出手是概率判定，窗口要够覆盖多次掷骰。
	private const float LoseOffset = 420f; // 将英雄放到视野外的水平偏移（像素）。
	private const double LoseTimeout = 4.5; // 等待怪物放弃目标的超时时间（秒）。
	private const double KillDelay = 1.5; // 受控阶段到致命攻击的间隔（秒）。
	private const double EndDelay = 2.5; // 死亡阶段到场景结束的间隔（秒）。
	private const float OutOfSightOffset = 600f; // 初始视野外偏移（像素）。
	private const float InSightOffset = 150f; // 进入怪物视野时的水平偏移（像素）。
	private const float BehindOffset = 60f; // 转身测试中英雄位于怪物身后的水平偏移（像素）。
	private static readonly AiBox ExpectedReach = new AiBox(-49f, 1f, -48f, 2f); // attack_1 判定盒期望范围。
	private const float ReachTolerance = 0.5f; // 判定盒边界允许误差（像素）。

	private enum Phase // AI 烟测的阶段状态。
	{
		/// <summary>怪物位于英雄视野外。</summary>
		OutOfSight,
		/// <summary>等待怪物首次命中英雄。</summary>
		WaitFirstHit,
		/// <summary>等待下一次攻击开始。</summary>
		WaitSecondAttack,
		/// <summary>验证出招和收招期间的朝向锁定。</summary>
		TurnAround,
		/// <summary>验证英雄处于空中时的守候行为。</summary>
		Air,
		/// <summary>等待英雄落地并验证恢复攻击。</summary>
		WaitLanded,
		/// <summary>验证目标丢失后的巡逻恢复。</summary>
		Lose,
		/// <summary>等待致命攻击。</summary>
		WaitKill,
		/// <summary>等待死亡动画和实体回收。</summary>
		WaitEnd,
		/// <summary>场景已完成。</summary>
		Done,
	}

	private readonly HeroEntity m_Hero; // 受测英雄实体。
	private readonly MonsterEntity m_Monster; // 受测怪物实体。
	private readonly int m_MonsterId; // 受测怪物实体编号。
	private Phase m_Phase = Phase.OutOfSight; // 当前场景阶段。
	private double m_PhaseStart; // 当前阶段开始时间（秒）。
	private bool m_PlacedOutOfSight; // 是否已将怪物放到视野外。
	private int m_LastSegment = -1; // 上一帧攻击段编号。
	private string m_LastAi = ""; // 上一次观测到的 AI 状态名。
	private string m_LastAnim = ""; // 上一次观测到的动画路径。
	private readonly List<(double Time, string Ai)> m_AiObserved = new(); // 按时间记录 AI 状态序列。
	private readonly List<(double Time, string Anim)> m_AnimObserved = new(); // 按时间记录动画序列。
	private readonly List<(double Time, string Ai)> m_AiBeforeSight = new(); // 进入视野前记录的 AI 状态序列。
	private readonly List<string> m_Failures = new(); // 断言失败原因列表。
	private int m_HeroHitsByMonster; // 怪物命中英雄的次数。
	private int m_DiedEvents; // 收到怪物死亡事件的次数。
	private int m_DiedKiller = -1; // 死亡事件报告的击杀者编号。
	private bool m_HiddenAfterDeath; // 是否已观察到死亡后的实体回收。

	// 转身阶段
	private int m_LockedFacing; // 转身测试中锁定的朝向。
	private double m_RecoveredAt = -1; // 收招硬直结束时间（秒）。
	private bool m_TurnedAfterRecovery; // 是否在收招后成功转向英雄。

	// 空中（平台）阶段
	private float m_HeroFloorY; // 英雄落地时的地面 Y 坐标。
	private float m_AirX; // 平台阶段固定的英雄 X 坐标。
	private float m_AirMinX; // 平台阶段怪物走位的最小 X 坐标。
	private float m_AirMaxX; // 平台阶段怪物走位的最大 X 坐标。
	private bool m_SawHold; // 是否观察到守候状态。
	private int m_AirAttacks; // 英雄在空中时怪物出招次数。
	private bool m_StruckAfterLanding; // 英雄落地后怪物是否已出招。

	// 丢失目标阶段
	private bool m_LostTarget; // 是否观察到怪物丢失目标并恢复巡逻。

	/// <summary>创建 AI 烟测场景并订阅战斗事件。</summary>
	/// <param name="hero">受测英雄实体。</param>
	/// <param name="monster">受测怪物实体。</param>
	public MonsterAiSmokeScenario(HeroEntity hero, MonsterEntity monster)
	{
		m_Hero = hero;
		m_Monster = monster;
		m_MonsterId = monster.Id;
		GF.Event.Subscribe(DamageDealtEventArgs.EventId, OnDamageDealt);
		GF.Event.Subscribe(MonsterDiedEventArgs.EventId, OnMonsterDied);
		CheckReach();
	}

	/// <summary>场景已结束（通过或超时），驱动器据此调用 <see cref="Finish"/>。</summary>
	public bool IsDone => m_Phase == Phase.Done;

	/// <summary>每物理帧推进（t = 相对开始的秒数）。</summary>
	/// <param name="t">相对场景开始时间（秒）。</param>
	public void Update(double t)
	{
		if (t >= TimeoutAt && m_Phase != Phase.Done)
		{
			m_Failures.Add($"超时：停在阶段 {m_Phase}");
			m_Phase = Phase.Done;
			return;
		}

		int segment = m_Monster.IsShown ? m_Monster.AttackSegment : -1;
		bool attackStarted = segment >= 0 && m_LastSegment < 0;
		m_LastSegment = segment;

		switch (m_Phase)
		{
			case Phase.OutOfSight:
				if (!m_PlacedOutOfSight)
				{
					Teleport(OutOfSightOffset);
					m_PlacedOutOfSight = true;
				}

				if (t >= InSightAt)
				{
					Teleport(InSightOffset);
					Enter(Phase.WaitFirstHit, t);
				}

				break;

			case Phase.WaitFirstHit:
				if (m_HeroHitsByMonster > 0)
				{
					m_Hero.Heal(m_Hero.Vitals.MaxHp);   // 防止后续阶段英雄被打死导致目标失效
					Enter(Phase.WaitSecondAttack, t);
				}

				break;

			case Phase.WaitSecondAttack:
				if (attackStarted)
				{
					// 出招当帧把英雄放到猴子身后：朝向必须锁到收招硬直结束
					m_LockedFacing = m_Monster.Facing;
					float behind = m_Monster.GlobalPosition.X - m_LockedFacing * BehindOffset;
					m_Hero.GlobalPosition = new Vector2(behind, m_Hero.GlobalPosition.Y);
					m_Hero.Velocity = Vector2.Zero;
					GD.Print($"SMOKE-AI[{t:F2}] 转身测试：猴子出招朝向 {m_LockedFacing}，英雄瞬移到身后 x={behind:F0}");
					Enter(Phase.TurnAround, t);
				}

				break;

			case Phase.TurnAround:
				UpdateTurnAround(t);
				break;

			case Phase.Air:
				UpdateAir(t, attackStarted);
				break;

			case Phase.WaitLanded:
				// 看"正在出招"而不是起手沿：落地当帧就可能已提交出招（起手沿落在 Air 阶段的最后一帧）
				if (!m_StruckAfterLanding && segment >= 0)
				{
					GD.Print($"SMOKE-AI[{t:F2}] 英雄落地后 {t - m_PhaseStart:F2}s 猴子出招");
					m_StruckAfterLanding = true;
				}

				if (m_StruckAfterLanding || t - m_PhaseStart > LandedTimeout)
				{
					m_Hero.Heal(m_Hero.Vitals.MaxHp);
					Enter(Phase.Lose, t);
				}

				break;

			case Phase.Lose:
				UpdateLose(t);
				break;

			case Phase.WaitKill:
				if (t - m_PhaseStart >= KillDelay)
				{
					HitMonster(m_Monster.Vitals.MaxHp * 10f, Vector2.Zero);
					Enter(Phase.WaitEnd, t);
				}

				break;

			case Phase.WaitEnd:
				if (t - m_PhaseStart >= EndDelay)
				{
					Enter(Phase.Done, t);
				}

				break;
		}

		Sample(t);
	}

	/// <summary>收尾断言，返回失败原因列表（空 = 通过）。</summary>
	/// <returns>断言失败原因；全部通过时为空列表。</returns>
	public List<string> Finish()
	{
		GF.Event.Unsubscribe(DamageDealtEventArgs.EventId, OnDamageDealt);
		GF.Event.Unsubscribe(MonsterDiedEventArgs.EventId, OnMonsterDied);

		List<string> failures = new(m_Failures);
		if (!m_AiBeforeSight.Exists(o => o.Ai is "Wander" or "Pause"))
		{
			failures.Add("视野外期间没有观察到 Wander/Pause");
		}

		if (m_AiBeforeSight.Exists(o => o.Ai is "WalkToTarget" or "StandAndStrike"))
		{
			failures.Add("视野外期间不应追击/攻击（索敌范围或目标锁定有误）");
		}

		RequireInOrder(failures, m_AiObserved.ConvertAll(o => o.Ai), "AI", "WalkToTarget", "StandAndStrike",
			"PaceBelowTarget", "StandAndStrike", "Wander", "CcLocked", "Death");
		RequireInOrder(failures, m_AnimObserved.ConvertAll(o => o.Anim), "猴子身体/动画", "Move/run", "Attack/attack_1",
			"Recovery/idle", "Hurt/hurt", "Death/death");

		if (m_HeroHitsByMonster == 0)
		{
			failures.Add("猴子出招期间没有打中英雄（判定盒/物理层/出招链路）");
		}

		if (!m_TurnedAfterRecovery)
		{
			failures.Add("转身测试未完成：收招硬直结束后猴子没有转向身后的英雄");
		}

		if (m_AirAttacks > 0)
		{
			failures.Add($"英雄在头顶 {AirHeight}px 期间猴子出招了 {m_AirAttacks} 次（高度判定失效）");
		}

		if (!m_StruckAfterLanding)
		{
			failures.Add($"英雄落地 {LandedTimeout}s 内猴子没有出招（Hold → Attack 未恢复）");
		}

		if (!m_LostTarget)
		{
			failures.Add("英雄离开视野后猴子没有丢失目标回到巡逻");
		}

		if (m_DiedEvents != 1)
		{
			failures.Add($"MonsterDiedEventArgs 应恰好广播 1 次，实际 {m_DiedEvents}");
		}
		else if (m_DiedKiller != m_Hero.Id)
		{
			failures.Add($"死亡事件击杀者应为英雄 {m_Hero.Id}，实际 {m_DiedKiller}");
		}

		if (!m_HiddenAfterDeath)
		{
			failures.Add("死亡动画后猴子没有被回收（HideEntity）");
		}

		GD.Print($"SMOKE-AI: AI 序列 {string.Join(" → ", m_AiObserved.ConvertAll(o => $"{o.Ai}@{o.Time:F2}"))}");
		GD.Print($"SMOKE-AI: 动画序列 {string.Join(" → ", m_AnimObserved.ConvertAll(o => $"{o.Anim}@{o.Time:F2}"))}");
		GD.Print($"SMOKE-AI: 猴子命中英雄 {m_HeroHitsByMonster} 次，英雄 HP {m_Hero.Vitals.Hp}/{m_Hero.Vitals.MaxHp}");
		return failures;
	}

	private void Enter(Phase phase, double t) // 切换场景阶段并记录阶段开始时间。
	{
		m_Phase = phase;
		m_PhaseStart = t;
		GD.Print($"SMOKE-AI[{t:F2}] 阶段 → {phase}");
	}

	private void UpdateTurnAround(double t) // 校验出招和收招硬直期间锁定朝向，结束后恢复追踪。
	{
		// 忙碌期间只允许攻击动画和原朝向；恢复后等待 AI 转向英雄。
		bool busy = m_Monster.AttackSegment >= 0 || m_Monster.InRecovery;
		if (busy)
		{
			if (m_Monster.Facing != m_LockedFacing)
			{
				m_Failures.Add($"出招/收招硬直期间转身了（t={t:F2} seg={m_Monster.AttackSegment} 硬直={m_Monster.InRecovery}）");
				Enter(Phase.Air, t);
				return;
			}

			if (m_Monster.AttackSegment >= 0 && !CurrentMonsterAnim().StartsWith("Attack/attack_"))
			{
				m_Failures.Add($"出招期间动画被切走：{CurrentMonsterAnim()}（t={t:F2}）");
			}

			return;
		}

		if (m_RecoveredAt < 0)
		{
			m_RecoveredAt = t;
			GD.Print($"SMOKE-AI[{t:F2}] 收招硬直结束（出招开始后 {t - m_PhaseStart:F2}s）");
		}

		if (m_Monster.Facing == -m_LockedFacing)
		{
			m_TurnedAfterRecovery = true;
			GD.Print($"SMOKE-AI[{t:F2}] 硬直结束后 {t - m_RecoveredAt:F2}s 转向英雄");
			StartAir(t);
		}
		else if (t - m_RecoveredAt > TurnAroundWindow)
		{
			m_Failures.Add($"收招硬直结束 {TurnAroundWindow}s 后仍未转向身后的英雄");
			StartAir(t);
		}
	}

	private void StartAir(double t) // 将英雄固定到怪物前方的空中测试点并开始平台阶段。
	{
		m_HeroFloorY = m_Hero.GlobalPosition.Y;
		m_AirX = m_Monster.GlobalPosition.X - m_Monster.Facing * AirOffsetX;
		m_AirMinX = m_AirMaxX = m_Monster.GlobalPosition.X;
		m_Hero.Heal(m_Hero.Vitals.MaxHp);
		Enter(Phase.Air, t);
	}

	private void UpdateAir(double t, bool attackStarted) // 校验目标在空中时的守候与踱步，结束后恢复攻击。
	{
		// 每帧锁定英雄位置并收集怪物在目标两侧的移动证据。
		// 每帧重设位置（本驱动器在实体物理之后处理：猴子下一帧读到的就是这个位置）
		m_Hero.GlobalPosition = new Vector2(m_AirX, m_HeroFloorY - AirHeight);
		m_Hero.Velocity = Vector2.Zero;

		float mx = m_Monster.GlobalPosition.X;
		m_AirMinX = Mathf.Min(m_AirMinX, mx);
		m_AirMaxX = Mathf.Max(m_AirMaxX, mx);
		m_SawHold |= m_Monster.AiStateName == "PaceBelowTarget";
		if (attackStarted && t - m_PhaseStart > 0.1)
		{
			m_AirAttacks++;
			GD.Print($"SMOKE-AI[{t:F2}] 英雄在头顶时猴子出招了（seg={m_Monster.AttackSegment}）");
		}

		if (t - m_PhaseStart < AirDuration)
		{
			return;
		}

		GD.Print($"SMOKE-AI[{t:F2}] 守候踱步范围 x∈[{m_AirMinX:F0},{m_AirMaxX:F0}]（英雄 x={m_AirX:F0}）");
		if (!m_SawHold)
		{
			m_Failures.Add($"英雄在头顶时猴子应进入 PaceBelowTarget，实际 {m_Monster.AiStateName}");
		}

		if (!(m_AirMinX < m_AirX - PaceEvidence && m_AirMaxX > m_AirX + PaceEvidence))
		{
			m_Failures.Add($"守候时没有在英雄两侧来回踱步：x∈[{m_AirMinX:F0},{m_AirMaxX:F0}]，英雄 x={m_AirX:F0}");
		}

		m_Hero.GlobalPosition = new Vector2(m_AirX, m_HeroFloorY);
		Enter(Phase.WaitLanded, t);
	}

	private void UpdateLose(double t) // 将英雄保持在视野外，校验怪物放弃目标并恢复巡逻。
	{
		// 持续保持目标超出视野，直到 AI 恢复巡逻或超过配置的等待上限。
		m_Hero.GlobalPosition = new Vector2(m_Monster.GlobalPosition.X + LoseOffset, m_HeroFloorY);
		m_Hero.Velocity = Vector2.Zero;
		if (m_Monster.AiStateName is "Wander" or "Pause")
		{
			// 确认丢失目标后重新把英雄放回视野并施加受击，推进到击杀阶段。
			GD.Print($"SMOKE-AI[{t:F2}] 丢失目标，{t - m_PhaseStart:F2}s 后回到 {m_Monster.AiStateName}");
			m_LostTarget = true;
			m_Hero.GlobalPosition = new Vector2(m_Monster.GlobalPosition.X + InSightOffset, m_HeroFloorY);
			HitMonster(1f, new Vector2(3, 0));   // 以英雄身份打一下：重新锁定 + 受控
			Enter(Phase.WaitKill, t);
		}
		else if (t - m_PhaseStart > LoseTimeout)
		{
			// 超时仍未恢复巡逻时记为失败，但继续推进到收尾阶段。
			m_Failures.Add($"英雄离开视野 {LoseTimeout}s 后猴子仍在 {m_Monster.AiStateName}（没有丢失目标）");
			Enter(Phase.WaitKill, t);
		}
	}

	private void CheckReach() // 校验 attack_1 判定盒推导出的范围。
	{
		AiBox reach = m_Monster.Attacks.ReachOf(0);
		GD.Print($"SMOKE-AI: attack_1 判定盒推导范围 {reach}");
		if (reach.IsEmpty ||
		    Math.Abs(reach.Left - ExpectedReach.Left) > ReachTolerance ||
		    Math.Abs(reach.Right - ExpectedReach.Right) > ReachTolerance ||
		    Math.Abs(reach.Top - ExpectedReach.Top) > ReachTolerance ||
		    Math.Abs(reach.Bottom - ExpectedReach.Bottom) > ReachTolerance)
		{
			m_Failures.Add($"attack_1 判定盒推导范围 {reach} ≠ 期望 {ExpectedReach}");
		}
	}

	private string CurrentMonsterAnim() // 返回猴子身体状态与当前动画树节点的组合路径。
	{
		return SmokeTestDriver.ObservePath(m_Monster.BodyStateName, m_Monster.CurrentAnim);
	}

	private void Sample(double t) // 记录 AI、动画变化及死亡后的实体回收。
	{
		// 先捕获死亡回收，再在实体仍可见时采样状态序列。
		if (!m_HiddenAfterDeath && m_DiedEvents > 0 && !m_Monster.IsShown)
		{
			m_HiddenAfterDeath = true;
			GD.Print($"SMOKE-AI[{t:F2}] 猴子已回收");
		}

		if (!m_Monster.IsShown)
		{
			return;
		}

		string ai = m_Monster.AiStateName;
		if (ai.Length > 0 && ai != m_LastAi)
		{
			m_LastAi = ai;
			m_AiObserved.Add((t, ai));
			if (t < InSightAt)
			{
				m_AiBeforeSight.Add((t, ai));
			}

			GD.Print($"SMOKE-AI[{t:F2}] AI {ai}  (dx={m_Hero.GlobalPosition.X - m_Monster.GlobalPosition.X:F0} "
				+ $"move={m_Monster.MoveIntent} body={m_Monster.BodyStateName} seg={m_Monster.AttackSegment} dead={m_Monster.Dead})");
		}

		string anim = CurrentMonsterAnim();
		if (anim.Length > 0 && anim != m_LastAnim)
		{
			m_LastAnim = anim;
			m_AnimObserved.Add((t, anim));
			GD.Print($"SMOKE-AI[{t:F2}] 猴子动画 {anim}  (facing={m_Monster.Facing} 硬直={m_Monster.InRecovery})");
		}
	}

	private void Teleport(float offset) // 将怪物传送到英雄水平位置的指定偏移处。
	{
		Vector2 pos = new Vector2(m_Hero.GlobalPosition.X + offset, m_Monster.GlobalPosition.Y);
		m_Monster.GlobalPosition = pos;
		m_Monster.Velocity = Vector2.Zero;
		GD.Print($"SMOKE-AI: 猴子挪到 x={pos.X:F0}（英雄 x={m_Hero.GlobalPosition.X:F0}）");
	}

	private void HitMonster(float power, Vector2 knockback) // 以英雄为攻击方对怪物执行一次真实伤害结算。
	{
		// 攻击数据只在结算期间持有，完成后立即归还引用池。
		AttackData attack = AttackData.Create(0, default, power, DamageKind.Real, knockback, 1, 0, SoundId.None);
		m_Monster.ReceiveHit(attack, m_Hero.Id);
		ReferencePool.Release(attack);
	}

	private void OnDamageDealt(object sender, GameEventArgs args) // 记录受测怪物命中英雄的伤害事件。
	{
		if (args is DamageDealtEventArgs e && e.TargetEntityId == m_Hero.Id && e.AttackerEntityId == m_MonsterId &&
		    !e.IsMiss)
		{
			m_HeroHitsByMonster++;
		}
	}

	private void OnMonsterDied(object sender, GameEventArgs args) // 记录受测怪物死亡事件及击杀者。
	{
		if (args is MonsterDiedEventArgs e && e.EntityId == m_MonsterId)
		{
			m_DiedEvents++;
			m_DiedKiller = e.KillerEntityId;
		}
	}

	private static void RequireInOrder(List<string> failures, List<string> seq, string label, params string[] expected) // 校验观测序列按期望顺序包含所有状态。
	{
		// 每个期望项只能从上一个匹配位置之后查找，保证顺序约束有效。
		int from = 0;
		foreach (string item in expected)
		{
			int index = seq.FindIndex(from, s => s == item);
			if (index < 0)
			{
				failures.Add($"{label}序列在 {string.Join("→", expected)} 中缺少（或顺序错误）{item}");
				return;
			}

			from = index + 1;
		}
	}
}
