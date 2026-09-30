using System;
using System.Collections.Generic;
using GameConfig.Battle;
using GameConfig.Sound;
using GameFramework;
using GameFramework.Event;
using GameLogic.Battle;
using GameLogic.Entity;
using GameLogic.Event;
using Godot;
using GodotGameFramework;

/// <summary>
/// 冒烟场景：怪物 AI（M5 完成标准"猴子巡逻、追击、攻击"）。由 <see cref="SmokeTestDriver"/> 在
/// `-- --smoketest=ai` 时驱动，英雄全程不输入（站桩当目标）。
///
/// 时间轴（相对找到英雄的时刻）：
///  0.0  猴子挪到视野外（英雄右侧 600px）→ 应当巡逻（Patrol/Idle），不追击；
///  3.0  猴子挪进视野（英雄右侧 150px）→ 应当 Chase → Attack，出招 attack_1 并打中英雄；
///  7.0  以英雄身份打猴子一下（带击退）→ AI 应进 CcLocked、动画进 Hurt；
///  8.5  以英雄身份打出致命伤 → AI 进 Death、广播 MonsterDiedEventArgs（击杀者 = 英雄）、
///       死亡动画播完后实体回收。
/// 断言的是 AI 状态名（GF.Fsm 当前状态）与猴子 AnimationTree 实际播放的节点两条链路。
/// </summary>
public sealed class MonsterAiSmokeScenario
{
	private const double OutOfSightAt = 0.0;
	private const double InSightAt = 3.0;
	private const double StaggerAt = 7.0;
	private const double KillAt = 8.5;
	public const double EndTime = 11.0;

	/// <summary>视野外偏移（猴子 SightRange 300）/ 视野内偏移</summary>
	private const float OutOfSightOffset = 600f;
	private const float InSightOffset = 150f;

	private readonly HeroEntity m_Hero;
	private readonly MonsterEntity m_Monster;
	private readonly AnimationTree m_MonsterTree;
	private readonly int m_MonsterId;

	private int m_Step;
	private string m_LastAi = "";
	private string m_LastAnim = "";
	private readonly List<(double Time, string Ai)> m_AiObserved = new();
	private readonly List<(double Time, string Anim)> m_AnimObserved = new();
	private readonly List<(double Time, string Ai)> m_AiBeforeSight = new();
	private int m_HeroHitsByMonster;
	private int m_DiedEvents;
	private int m_DiedKiller = -1;
	private bool m_HiddenAfterDeath;

	public MonsterAiSmokeScenario(HeroEntity hero, MonsterEntity monster, AnimationTree monsterTree)
	{
		m_Hero = hero;
		m_Monster = monster;
		m_MonsterTree = monsterTree;
		m_MonsterId = monster.Id;
		GF.Event.Subscribe(DamageDealtEventArgs.EventId, OnDamageDealt);
		GF.Event.Subscribe(MonsterDiedEventArgs.EventId, OnMonsterDied);
	}

	/// <summary>每物理帧推进（t = 相对开始的秒数）。</summary>
	public void Update(double t)
	{
		if (m_Step == 0 && t >= OutOfSightAt)
		{
			Teleport(OutOfSightOffset);
			m_Step++;
		}
		else if (m_Step == 1 && t >= InSightAt)
		{
			Teleport(InSightOffset);
			m_Step++;
		}
		else if (m_Step == 2 && t >= StaggerAt)
		{
			HitMonster(1f, new Vector2(3, 0));
			m_Step++;
		}
		else if (m_Step == 3 && t >= KillAt)
		{
			HitMonster(m_Monster.MaxHp * 10f, Vector2.Zero);
			m_Step++;
		}

		Sample(t);
	}

	/// <summary>收尾断言，返回失败原因列表（空 = 通过）。</summary>
	public List<string> Finish()
	{
		GF.Event.Unsubscribe(DamageDealtEventArgs.EventId, OnDamageDealt);
		GF.Event.Unsubscribe(MonsterDiedEventArgs.EventId, OnMonsterDied);

		List<string> failures = new();
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

		string anim = SmokeTestDriver.CurrentStatePath(m_MonsterTree);
		if (anim.Length > 0 && anim != m_LastAnim)
		{
			m_LastAnim = anim;
			m_AnimObserved.Add((t, anim));
			GD.Print($"SMOKE-AI[{t:F2}] 猴子动画 {anim}");
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
