using System;

namespace GameLogic.Battle.Stats;

/// <summary>英雄的无双进度：命中累计、满值后供无双技消耗；只存在于关卡运行期，不写入存档。</summary>
public sealed class MusouGauge
{
	/// <summary>当前无双值。</summary>
	public int Value { get; private set; }

	/// <summary>无双上限（BattleConfig.WsMax）。</summary>
	public int Max { get; private set; }

	/// <summary>已蓄满。</summary>
	public bool IsFull => Max > 0 && Value >= Max;

	/// <summary>数值或上限发生变化。</summary>
	public event Action Changed;

	/// <summary>设置上限并清零（实体显示时调用）。</summary>
	/// <param name="max">无双上限，下限 0。</param>
	public void Reset(int max)
	{
		Max = Math.Max(0, max);
		Value = 0;
		Changed?.Invoke();
	}

	/// <summary>累计无双值，不超过上限。</summary>
	/// <param name="amount">增加量；非正数不处理。</param>
	public void Add(int amount)
	{
		if (amount <= 0 || Value >= Max)
		{
			return;
		}

		Value = Math.Min(Max, Value + amount);
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
}
