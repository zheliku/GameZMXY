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
///  2 转身     打中英雄后，下一次出招的当帧把英雄瞬移到猴子身后 → 出招与收招硬直期间朝向不变、动画一直是
///             Attack/attack_1；硬直结束后 1s 内转向英雄；
///  3 空中     英雄钉在猴子正上方 120px 两秒 → 期间不出招，AI 停在 Attack（站在下面等）；
///  4 受控     以英雄身份打猴子一下（带击退）→ AI 进 CcLocked、动画进 Hurt；
///  5 击杀     致命伤 → AI 进 Death、广播 MonsterDiedEventArgs（击杀者 = 英雄）、死亡动画后回收。
/// 另断言：攻击范围由 attack_1 判定盒推导 = 50×50 @(-24,-23) → X[-49,1] Y[-48,2]。
/// </summary>
public sealed class MonsterAiSmokeScenario
{
	private const double InSightAt = 3.0;

	/// <summary>整体超时（引擎 --quit-after 1500 帧 ≈ 25s，必须在此之前给出结论）</summary>
	private const double TimeoutAt = 21.0;

	/// <summary>转身阶段：硬直结束后允许的转向时限</summary>
	private const double TurnAroundWindow = 1.0;

	/// <summary>空中阶段时长与高度</summary>
	private const double AirDuration = 2.0;
	private const float AirHeight = 120f;

	/// <summary>空中阶段英雄相对猴子的水平偏移（在判定盒水平范围内，只差高度）</summary>
	private const float AirOffsetX = 30f;

	/// <summary>受控 → 击杀 → 结束的间隔</summary>
	private const double StaggerDelay = 1.0;
	private const double KillDelay = 1.5;
	private const double EndDelay = 2.5;

	/// <summary>视野外偏移（猴子 SightRange 300）/ 视野内偏移 / 转身时放到身后的距离</summary>
	private const float OutOfSightOffset = 600f;
	private const float InSightOffset = 150f;
	private const float BehindOffset = 60f;

	/// <summary>attack_1 判定盒推导的期望值（原生朝左）与容差</summary>
	private static readonly AiBox ExpectedReach = new AiBox(-49f, 1f, -48f, 2f);
	private const float ReachTolerance = 0.5f;

	private enum Phase
	{
		OutOfSight,
		WaitFirstHit,
		WaitSecondAttack,
		TurnAround,
		Air,
		WaitStagger,
		WaitKill,
		WaitEnd,
		Done,
	}

	private readonly HeroEntity m_Hero;
	private readonly MonsterEntity m_Monster;
	private readonly AnimationTree m_MonsterTree;
	private readonly int m_MonsterId;

	private Phase m_Phase = Phase.OutOfSight;
	private double m_PhaseStart;
	private bool m_PlacedOutOfSight;
	private int m_LastSegment = -1;
	private string m_LastAi = "";
	private string m_LastAnim = "";
	private readonly List<(double Time, string Ai)> m_AiObserved = new();
	private readonly List<(double Time, string Anim)> m_AnimObserved = new();
	private readonly List<(double Time, string Ai)> m_AiBeforeSight = new();
	private readonly List<string> m_Failures = new();
	private int m_HeroHitsByMonster;
	private int m_DiedEvents;
	private int m_DiedKiller = -1;
	private bool m_HiddenAfterDeath;

	// 转身阶段
	private int m_LockedFacing;
	private double m_RecoveredAt = -1;
	private bool m_TurnedAfterRecovery;

	// 空中阶段
	private float m_HeroFloorY;
	private int m_AirAttacks;

	public MonsterAiSmokeScenario(HeroEntity hero, MonsterEntity monster, AnimationTree monsterTree)
	{
		m_Hero = hero;
		m_Monster = monster;
		m_MonsterTree = monsterTree;
		m_MonsterId = monster.Id;
		GF.Event.Subscribe(DamageDealtEventArgs.EventId, OnDamageDealt);
		GF.Event.Subscribe(MonsterDiedEventArgs.EventId, OnMonsterDied);
		CheckReach();
	}

	/// <summary>场景已结束（通过或超时），驱动器据此调用 <see cref="Finish"/>。</summary>
	public bool IsDone => m_Phase == Phase.Done;

	/// <summary>每物理帧推进（t = 相对开始的秒数）。</summary>
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

			case Phase.WaitStagger:
				if (t - m_PhaseStart >= StaggerDelay)
				{
					HitMonster(1f, new Vector2(3, 0));
					Enter(Phase.WaitKill, t);
				}

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
	public List<string> Finish()
	{
		GF.Event.Unsubscribe(DamageDealtEventArgs.EventId, OnDamageDealt);
		GF.Event.Unsubscribe(MonsterDiedEventArgs.EventId, OnMonsterDied);

		List<string> failures = new(m_Failures);
		if (!m_AiBeforeSight.Exists(o => o.Ai == "Patrol"))
		{
			failures.Add("视野外期间没有观察到 Patrol");
		}

		if (m_AiBeforeSight.Exists(o => o.Ai is "Chase" or "Attack"))
		{
			failures.Add("视野外期间不应追击/攻击（索敌范围或目标锁定有误）");
		}

		RequireInOrder(failures, m_AiObserved.ConvertAll(o => o.Ai), "AI", "Chase", "Attack", "CcLocked", "Death");
		RequireInOrder(failures, m_AnimObserved.ConvertAll(o => o.Anim), "猴子动画", "Ground/run", "Attack/attack_1",
			"Hurt", "Death");

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

	private void Enter(Phase phase, double t)
	{
		m_Phase = phase;
		m_PhaseStart = t;
		GD.Print($"SMOKE-AI[{t:F2}] 阶段 → {phase}");
	}

	/// <summary>
	/// 转身：出招中与收招硬直中朝向必须保持、出招中动画必须在攻击组；硬直结束后限时内转向英雄。
	/// </summary>
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

			if (m_Monster.AttackSegment >= 0 && !CurrentMonsterAnim().StartsWith("Attack"))
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

	/// <summary>空中阶段开始：等猴子空闲再钉住英雄（避免把上一招的收尾算进来）。</summary>
	private void StartAir(double t)
	{
		m_HeroFloorY = m_Hero.GlobalPosition.Y;
		m_Hero.Heal(m_Hero.MaxHp);
		Enter(Phase.Air, t);
	}

	private void UpdateAir(double t, bool attackStarted)
	{
		// 每帧把英雄钉在猴子正上方（本驱动器在实体物理之后处理：猴子下一帧读到的就是这个位置）
		float x = m_Monster.GlobalPosition.X - m_Monster.Facing * AirOffsetX;
		m_Hero.GlobalPosition = new Vector2(x, m_HeroFloorY - AirHeight);
		m_Hero.Velocity = Vector2.Zero;

		if (attackStarted && t - m_PhaseStart > 0.1)
		{
			m_AirAttacks++;
			GD.Print($"SMOKE-AI[{t:F2}] 英雄在头顶时猴子出招了（seg={m_Monster.AttackSegment}）");
		}

		if (t - m_PhaseStart < AirDuration)
		{
			return;
		}

		if (m_Monster.AiStateName != "Attack")
		{
			m_Failures.Add($"英雄在头顶时猴子应站在下面等（Attack），实际 {m_Monster.AiStateName}");
		}

		// 放英雄落回地面，进入受控阶段
		m_Hero.GlobalPosition = new Vector2(x, m_HeroFloorY);
		Enter(Phase.WaitStagger, t);
	}

	/// <summary>判定盒推导结果（OnInit 时算好）：猴子 attack_1 的 50×50 @(-24,-23)。</summary>
	private void CheckReach()
	{
		AiBox reach = m_Monster.GetAttackReach(0);
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

	private string CurrentMonsterAnim()
	{
		return SmokeTestDriver.CurrentStatePath(m_MonsterTree);
	}

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
				+ $"move={m_Monster.MoveInput} seg={m_Monster.AttackSegment} hurt={m_Monster.Hurt} dead={m_Monster.Dead})");
		}

		string anim = CurrentMonsterAnim();
		if (anim.Length > 0 && anim != m_LastAnim)
		{
			m_LastAnim = anim;
			m_AnimObserved.Add((t, anim));
			GD.Print($"SMOKE-AI[{t:F2}] 猴子动画 {anim}  (facing={m_Monster.Facing} 硬直={m_Monster.InRecovery})");
		}
	}

	private void Teleport(float offset)
	{
		Vector2 pos = new Vector2(m_Hero.GlobalPosition.X + offset, m_Monster.GlobalPosition.Y);
		m_Monster.GlobalPosition = pos;
		m_Monster.Velocity = Vector2.Zero;
		GD.Print($"SMOKE-AI: 猴子挪到 x={pos.X:F0}（英雄 x={m_Hero.GlobalPosition.X:F0}）");
	}

	/// <summary>以英雄为攻击方走真实结算链路（ReceiveHit → OnHurt 锁定仇恨/硬直/死亡）。</summary>
	private void HitMonster(float power, Vector2 knockback)
	{
		AttackData attack = AttackData.Create(0, default, power, DamageKind.Real, knockback, 1, 0, 0, SoundId.None);
		m_Monster.ReceiveHit(attack, m_Hero.Id);
		ReferencePool.Release(attack);
	}

	private void OnDamageDealt(object sender, GameEventArgs args)
	{
		if (args is DamageDealtEventArgs e && e.TargetEntityId == m_Hero.Id && e.AttackerEntityId == m_MonsterId &&
		    !e.IsMiss)
		{
			m_HeroHitsByMonster++;
		}
	}

	private void OnMonsterDied(object sender, GameEventArgs args)
	{
		if (args is MonsterDiedEventArgs e && e.EntityId == m_MonsterId)
		{
			m_DiedEvents++;
			m_DiedKiller = e.KillerEntityId;
		}
	}

	/// <summary>序列中必须按给定顺序（允许间隔其它项）依次出现 expected。</summary>
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
