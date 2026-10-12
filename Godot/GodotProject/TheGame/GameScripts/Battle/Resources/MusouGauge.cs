using System;

namespace GameLogic.Battle.Resources;

/// <summary>英雄无双：伤害比例蓄力，未满时定时衰减，满值保留；开启后耗尽，不写入存档。</summary>
public sealed class MusouGauge
{
	private const double TickTolerance = 1e-5; // 浮点秒数累计的点数容差。
	private double m_DrainProgress; // 开启消耗尚不足一点的累计量。
	private float m_DrainPerSecond; // 开启后额外消耗的点数/秒。
	private double m_DecayProgress; // 自然衰减尚不足一点的累计量；命中不重置。
	private float m_DecayPerSecond; // 未满时自然衰减的点数/秒。

	/// <summary>当前处于无双状态。</summary>
	public bool IsActive { get; private set; }

	/// <summary>当前无双值。</summary>
	public int Value { get; private set; }

	/// <summary>无双上限（BattleConfig.WsMax）。</summary>
	public int Max { get; private set; }

	/// <summary>已蓄满。</summary>
	public bool IsFull => !IsActive && Max > 0 && Value >= Max;

	/// <summary>数值与上限更新或重置完成后同步通知外部观察者。</summary>
	/// <remarks>订阅方读取 Value、Max、IsFull 与 IsActive；一次推进完成后通知。</remarks>
	public event Action Changed;

	/// <summary>设置上限并清零（实体显示时调用）。</summary>
	/// <param name="max">无双上限，下限 0。</param>
	/// <param name="decayPerSecond">自然衰减速度，有限正点数/秒。</param>
	/// <param name="drainPerSecond">开启后额外消耗速度，有限正点数/秒。</param>
	/// <exception cref="ArgumentOutOfRangeException">衰减参数非法。</exception>
	public void Reset(int max, float decayPerSecond, float drainPerSecond)
	{
		if (!float.IsFinite(decayPerSecond) || decayPerSecond <= 0f
			|| !float.IsFinite(drainPerSecond) || drainPerSecond <= 0f)
			throw new ArgumentOutOfRangeException(nameof(decayPerSecond), "无双每秒衰减和消耗必须为有限正数。");
		Max = Math.Max(0, max);
		m_DecayPerSecond = decayPerSecond;
		m_DecayProgress = 0;
		m_DrainPerSecond = drainPerSecond;
		m_DrainProgress = 0;
		Value = 0;
		IsActive = false;
		Changed?.Invoke();
	}

	/// <summary>按官方规则累计：正在开启或本次加值会超过上限时整次不增加。</summary>
	/// <param name="amount">增加量；非正数不处理，满值保持。</param>
	public void Add(int amount)
	{
		if (!IsActive) AddFromPickup(amount);
	}

	/// <summary>黄球增加无双；开启期间也可补充，超过上限仍整次忽略。</summary>
	/// <param name="amount">本颗黄球的配置值，通常为 5。</param>
	public void AddFromPickup(int amount)
	{
		if (amount <= 0 || amount > Max - Value) return;
		Value += amount;
		Changed?.Invoke();
	}

	/// <summary>蓄满时开启无双；激活期间不再次开启，也不累计命中收益。</summary>
	/// <returns>是否已进入无双状态。</returns>
	public bool TryActivate()
	{
		if (!IsFull)
		{
			return false;
		}

		IsActive = true;
		m_DrainProgress = 0;
		Changed?.Invoke();
		return true;
	}

	/// <summary>推进未满时的自然衰减或开启后的耗尽；满值保留，暂停时宿主不推进。</summary>
	/// <param name="delta">本次物理步长秒数。</param>
	public void Advance(float delta)
	{
		if (!float.IsFinite(delta) || delta <= 0f)
		{
			return;
		}

		if (IsFull || m_DecayPerSecond <= 0f) return;
		int before = Value;
		bool wasActive = IsActive;
		// 官方满值不推进自然衰减；首次开启扣点后，普通衰减恢复。
		double decayDelta = delta;
		if (IsActive)
		{
			if (Value == Max) decayDelta = Math.Max(0, delta - (1 - m_DrainProgress) / m_DrainPerSecond);
			m_DrainProgress += delta * (double)m_DrainPerSecond;
			double ticks = Math.Floor(m_DrainProgress + TickTolerance);
			m_DrainProgress = Math.Max(0, m_DrainProgress - ticks);
			Value = (int)Math.Max(0, Value - ticks);
		}
		m_DecayProgress += decayDelta * m_DecayPerSecond;
		double decayTicks = Math.Floor(m_DecayProgress + TickTolerance);
		m_DecayProgress = Math.Max(0, m_DecayProgress - decayTicks);
		Value = (int)Math.Max(0, Value - decayTicks);
		if (Value == 0) IsActive = false;
		if (before != Value || wasActive != IsActive)
		{
			Changed?.Invoke();
		}
	}

	/// <summary>立即结束并清空进度（死亡或隐藏），不影响无双上限。</summary>
	public void Clear()
	{
		bool changed = Value != 0 || IsActive;
		Value = 0;
		IsActive = false;
		m_DrainProgress = 0;
		m_DecayProgress = 0;
		if (changed) Changed?.Invoke();
	}
}
