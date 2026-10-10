using System;

namespace GameLogic.Battle.Stats;

/// <summary>英雄无双：命中蓄力，满值开启后按持续时间耗尽；只存在于关卡运行期，不写入存档。</summary>
public sealed class MusouGauge
{
	private float m_Duration; // 本次无双持续秒数，用于把剩余时长映射回进度。

	/// <summary>无双状态剩余秒数；零表示尚未开启或已经结束。</summary>
	public float RemainingSeconds { get; private set; }

	/// <summary>当前处于无双状态。</summary>
	public bool IsActive => RemainingSeconds > 0f;
	/// <summary>当前无双值。</summary>
	public int Value { get; private set; }

	/// <summary>无双上限（BattleConfig.WsMax）。</summary>
	public int Max { get; private set; }

	/// <summary>已蓄满。</summary>
	public bool IsFull => !IsActive && Max > 0 && Value >= Max;

	/// <summary>数值与上限更新或重置完成后同步通知外部观察者。</summary>
	/// <remarks>订阅方读取 Value、Max、IsFull 与 IsActive；状态切换也会通知，剩余时长的逐帧变化不单独通知。</remarks>
	public event Action Changed;

	/// <summary>设置上限并清零（实体显示时调用）。</summary>
	/// <param name="max">无双上限，下限 0。</param>
	public void Reset(int max)
	{
		Max = Math.Max(0, max);
		Value = 0;
		RemainingSeconds = 0f;
		m_Duration = 0f;
		Changed?.Invoke();
	}

	/// <summary>累计无双值，不超过上限。</summary>
	/// <param name="amount">增加量；非正数不处理。</param>
	public void Add(int amount)
	{
		if (amount <= 0 || Value >= Max || IsActive)
		{
			return;
		}

		Value += Math.Min(amount, Max - Value);
		Changed?.Invoke();
	}

	/// <summary>消耗全部无双值（释放无双技）。</summary>
	/// <returns>是否已蓄满并已清零。</returns>
	public bool TryConsume()
	{
		if (!IsFull)
		{
			return false;
		}

		Value = 0;
		Changed?.Invoke();
		return true;
	}

	/// <summary>蓄满时开启无双；激活期间不再次开启，也不累计命中收益。</summary>
	/// <param name="duration">配置提供的持续秒数，必须是有限正数。</param>
	/// <returns>是否已进入无双状态。</returns>
	public bool TryActivate(float duration)
	{
		if (!IsFull || !float.IsFinite(duration) || duration <= 0f)
		{
			return false;
		}

		m_Duration = duration;
		RemainingSeconds = duration;
		Changed?.Invoke();
		return true;
	}

	/// <summary>推进无双时间并同步进度；暂停时宿主不推进。</summary>
	/// <param name="delta">本次物理步长秒数。</param>
	public void Advance(float delta)
	{
		if (!IsActive || !float.IsFinite(delta) || delta <= 0f)
		{
			return;
		}

		RemainingSeconds = Math.Max(0f, RemainingSeconds - delta);
		int value = (int)Math.Ceiling(Max * (double)RemainingSeconds / m_Duration);
		if (Value != value || !IsActive)
		{
			Value = value;
			Changed?.Invoke();
		}
	}

	/// <summary>立即结束并清空进度（死亡或隐藏），不影响无双上限。</summary>
	public void Clear()
	{
		if (Value == 0 && !IsActive)
		{
			return;
		}

		Value = 0;
		RemainingSeconds = 0f;
		m_Duration = 0f;
		Changed?.Invoke();
	}
}
