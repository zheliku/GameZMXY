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
	/// <summary>进入视野并开始追击的时间（秒）。</summary>
	private const double InSightAt = 3.0;

	/// <summary>整体超时（引擎需带 --quit-after 2400 帧 ≈ 40s，必须在此之前给出结论）</summary>
	private const double TimeoutAt = 36.0;

	/// <summary>转身阶段：硬直结束后允许的转向时限</summary>
	private const double TurnAroundWindow = 1.0;

	/// <summary>空中阶段时长（秒）。</summary>
	private const double AirDuration = 4.0;
	/// <summary>空中阶段英雄离地高度（像素）。</summary>
	private const float AirHeight = 120f;

	/// <summary>空中阶段英雄相对猴子的水平偏移（在判定盒水平范围内，只差高度）</summary>
	private const float AirOffsetX = 30f;

	/// <summary>踱步证据：猴子在英雄 x 两侧都走出至少这么远，才算"来回踱步"</summary>
	private const float PaceEvidence = 20f;

	/// <summary>英雄落地后等猴子出招的时限</summary>
	private const double LandedTimeout = 2.5;

	/// <summary>丢失目标：英雄保持在猴子右侧这么远（SightRange 300 之外），等待时限（LoseTargetTime 3s + 余量）</summary>
	private const float LoseOffset = 420f;
	/// <summary>等待怪物放弃目标的超时时间（秒）。</summary>
	private const double LoseTimeout = 4.5;

	/// <summary>受控阶段到致命攻击的间隔（秒）。</summary>
	private const double KillDelay = 1.5;
	/// <summary>死亡阶段到场景结束的间隔（秒）。</summary>
	private const double EndDelay = 2.5;

	/// <summary>视野外偏移（猴子 SightRange 300）/ 视野内偏移 / 转身时放到身后的距离</summary>
	private const float OutOfSightOffset = 600f;
	/// <summary>英雄进入怪物视野时的水平偏移（像素）。</summary>
	private const float InSightOffset = 150f;
	/// <summary>转身测试中英雄位于怪物身后的水平偏移（像素）。</summary>
	private const float BehindOffset = 60f;

	/// <summary>attack_1 判定盒推导的期望值（原生朝左）。</summary>
	private static readonly AiBox ExpectedReach = new AiBox(-49f, 1f, -48f, 2f);
	/// <summary>判定盒边界的允许误差（像素）。</summary>
	private const float ReachTolerance = 0.5f;

	/// <summary>AI 烟测当前阶段。</summary>
	private enum Phase
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

	/// <summary>受测英雄实体。</summary>
	private readonly HeroEntity m_Hero;
	/// <summary>受测怪物实体。</summary>
	private readonly MonsterEntity m_Monster;
	/// <summary>受测怪物实体编号。</summary>
	private readonly int m_MonsterId;

	/// <summary>当前场景阶段。</summary>
	private Phase m_Phase = Phase.OutOfSight;
	/// <summary>当前阶段开始时间（秒）。</summary>
	private double m_PhaseStart;
	/// <summary>是否已将怪物放到视野外。</summary>
	private bool m_PlacedOutOfSight;
	/// <summary>上一帧攻击段编号。</summary>
	private int m_LastSegment = -1;
	/// <summary>上一次观测到的 AI 状态名。</summary>
	private string m_LastAi = "";
	/// <summary>上一次观测到的动画路径。</summary>
	private string m_LastAnim = "";
	/// <summary>按时间记录的 AI 状态序列。</summary>
	private readonly List<(double Time, string Ai)> m_AiObserved = new();
	/// <summary>按时间记录的动画序列。</summary>
	private readonly List<(double Time, string Anim)> m_AnimObserved = new();
	/// <summary>进入视野前记录的 AI 状态序列。</summary>
	private readonly List<(double Time, string Ai)> m_AiBeforeSight = new();
	/// <summary>断言失败原因列表。</summary>
	private readonly List<string> m_Failures = new();
	/// <summary>怪物命中英雄的次数。</summary>
	private int m_HeroHitsByMonster;
	/// <summary>收到怪物死亡事件的次数。</summary>
	private int m_DiedEvents;
	/// <summary>死亡事件报告的击杀者编号。</summary>
	private int m_DiedKiller = -1;
	/// <summary>是否已观察到死亡后的实体回收。</summary>
	private bool m_HiddenAfterDeath;

	// 转身阶段
	/// <summary>转身测试中锁定的朝向。</summary>
	private int m_LockedFacing;
	/// <summary>收招硬直结束时间（秒）。</summary>
	private double m_RecoveredAt = -1;
	/// <summary>是否在收招后成功转向英雄。</summary>
	private bool m_TurnedAfterRecovery;

	// 空中（平台）阶段
	/// <summary>英雄落地时的地面 Y 坐标。</summary>
	private float m_HeroFloorY;
	/// <summary>平台阶段固定的英雄 X 坐标。</summary>
	private float m_AirX;
	/// <summary>平台阶段怪物走位的最小 X 坐标。</summary>
	private float m_AirMinX;
	/// <summary>平台阶段怪物走位的最大 X 坐标。</summary>
	private float m_AirMaxX;
	/// <summary>是否观察到守候状态。</summary>
	private bool m_SawHold;
	/// <summary>英雄在空中时怪物出招次数。</summary>
	private int m_AirAttacks;
	/// <summary>英雄落地后怪物是否已出招。</summary>
	private bool m_StruckAfterLanding;

	// 丢失目标阶段
	/// <summary>是否观察到怪物丢失目标并恢复巡逻。</summary>
	private bool m_LostTarget;

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
					m_Hero.Heal(m_Hero.MaxHp);   // 防止后续阶段英雄被打死导致目标失效
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
					m_Hero.Heal(m_Hero.MaxHp);
					Enter(Phase.Lose, t);
				}

				break;

			case Phase.Lose:
				UpdateLose(t);
				break;

			case Phase.WaitKill:
				if (t - m_PhaseStart >= KillDelay)
				{
					HitMonster(m_Monster.MaxHp * 10f, Vector2.Zero);
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
		GD.Print($"SMOKE-AI: 猴子命中英雄 {m_HeroHitsByMonster} 次，英雄 HP {m_Hero.Hp}/{m_Hero.MaxHp}");
		return failures;
	}

	/// <summary>切换场景阶段并记录阶段开始时间。</summary>
	/// <param name="phase">目标阶段。</param>
	/// <param name="t">相对场景开始时间（秒）。</param>
	private void Enter(Phase phase, double t)
	{
		m_Phase = phase;
		m_PhaseStart = t;
		GD.Print($"SMOKE-AI[{t:F2}] 阶段 → {phase}");
	}

	/// <summary>
	/// 转身：出招中与收招硬直中朝向必须保持、出招中动画必须在攻击组；硬直结束后限时内转向英雄。
	/// </summary>
	/// <param name="t">相对场景开始时间（秒）。</param>
	private void UpdateTurnAround(double t)
	{
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

	/// <summary>空中阶段开始：把英雄钉在猴子前方 AirOffsetX、高 AirHeight 的固定点（模拟站在平台上）。</summary>
	/// <param name="t">相对场景开始时间（秒）。</param>
	private void StartAir(double t)
	{
		m_HeroFloorY = m_Hero.GlobalPosition.Y;
		m_AirX = m_Monster.GlobalPosition.X - m_Monster.Facing * AirOffsetX;
		m_AirMinX = m_AirMaxX = m_Monster.GlobalPosition.X;
		m_Hero.Heal(m_Hero.MaxHp);
		Enter(Phase.Air, t);
	}

	/// <summary>
	/// 空中（平台）：期间不出招、AI 进 Hold；猴子以英雄 x 为中心来回踱步——左右两侧都到过、且没走出 PaceRange + 滞回。
	/// 结束后放英雄落地：猴子应回到 Attack 并出招。
	/// </summary>
	/// <param name="t">相对场景开始时间（秒）。</param>
	/// <param name="attackStarted">当前帧是否刚开始出招。</param>
	private void UpdateAir(double t, bool attackStarted)
	{
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

	/// <summary>
	/// 丢失目标：英雄每帧保持在猴子右侧 LoseOffset（SightRange 外，模拟英雄跑得比猴子快；场地右侧够长），
	/// LoseTargetTime 后猴子应放弃目标回到 Wander/Pause。
	/// </summary>
	/// <param name="t">相对场景开始时间（秒）。</param>
	private void UpdateLose(double t)
	{
		m_Hero.GlobalPosition = new Vector2(m_Monster.GlobalPosition.X + LoseOffset, m_HeroFloorY);
		m_Hero.Velocity = Vector2.Zero;
		if (m_Monster.AiStateName is "Wander" or "Pause")
		{
			GD.Print($"SMOKE-AI[{t:F2}] 丢失目标，{t - m_PhaseStart:F2}s 后回到 {m_Monster.AiStateName}");
			m_LostTarget = true;
			m_Hero.GlobalPosition = new Vector2(m_Monster.GlobalPosition.X + InSightOffset, m_HeroFloorY);
			HitMonster(1f, new Vector2(3, 0));   // 以英雄身份打一下：重新锁定 + 受控
			Enter(Phase.WaitKill, t);
		}
		else if (t - m_PhaseStart > LoseTimeout)
		{
			m_Failures.Add($"英雄离开视野 {LoseTimeout}s 后猴子仍在 {m_Monster.AiStateName}（没有丢失目标）");
			Enter(Phase.WaitKill, t);
		}
	}

	/// <summary>判定盒推导结果（OnInit 时算好）：猴子 attack_1 的 50×50 @(-24,-23)。</summary>
	private void CheckReach()
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

	/// <summary>猴子观测点 `身体状态/树当前节点`（如 Attack/attack_1、Recovery/idle）。</summary>
	/// <returns>身体状态名与动画路径。</returns>
	private string CurrentMonsterAnim()
	{
		return SmokeTestDriver.ObservePath(m_Monster.BodyStateName, m_Monster.CurrentAnim);
	}

	/// <summary>采样当前 AI 与动画状态，并记录状态变化。</summary>
	/// <param name="t">相对场景开始时间（秒）。</param>
	private void Sample(double t)
	{
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

	/// <summary>将怪物传送到英雄水平位置的指定偏移处。</summary>
	/// <param name="offset">相对英雄的水平偏移（像素）。</param>
	private void Teleport(float offset)
	{
		Vector2 pos = new Vector2(m_Hero.GlobalPosition.X + offset, m_Monster.GlobalPosition.Y);
		m_Monster.GlobalPosition = pos;
		m_Monster.Velocity = Vector2.Zero;
		GD.Print($"SMOKE-AI: 猴子挪到 x={pos.X:F0}（英雄 x={m_Hero.GlobalPosition.X:F0}）");
	}

	/// <summary>以英雄为攻击方对怪物执行一次真实伤害结算。</summary>
	/// <param name="power">攻击威力。</param>
	/// <param name="knockback">击退向量。</param>
	private void HitMonster(float power, Vector2 knockback)
	{
		AttackData attack = AttackData.Create(0, default, power, DamageKind.Real, knockback, 1, 0, SoundId.None);
		m_Monster.ReceiveHit(attack, m_Hero.Id);
		ReferencePool.Release(attack);
	}

	/// <summary>记录怪物命中英雄的伤害事件。</summary>
	/// <param name="sender">事件发送者。</param>
	/// <param name="args">伤害事件参数。</param>
	private void OnDamageDealt(object sender, GameEventArgs args)
	{
		if (args is DamageDealtEventArgs e && e.TargetEntityId == m_Hero.Id && e.AttackerEntityId == m_MonsterId &&
		    !e.IsMiss)
		{
			m_HeroHitsByMonster++;
		}
	}

	/// <summary>记录受测怪物死亡事件及击杀者。</summary>
	/// <param name="sender">事件发送者。</param>
	/// <param name="args">怪物死亡事件参数。</param>
	private void OnMonsterDied(object sender, GameEventArgs args)
	{
		if (args is MonsterDiedEventArgs e && e.EntityId == m_MonsterId)
		{
			m_DiedEvents++;
			m_DiedKiller = e.KillerEntityId;
		}
	}

	/// <summary>检查观测序列是否按指定顺序包含所有期望项。</summary>
	/// <param name="failures">用于追加失败原因的列表。</param>
	/// <param name="seq">实际观测序列。</param>
	/// <param name="label">序列名称。</param>
	/// <param name="expected">期望出现的顺序。</param>
	private static void RequireInOrder(List<string> failures, List<string> seq, string label, params string[] expected)
	{
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
