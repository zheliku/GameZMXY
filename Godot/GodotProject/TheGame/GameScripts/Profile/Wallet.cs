using System;

namespace GameLogic.Profile;

/// <summary>档案的货币：所有增减都经这里，不会出现负数余额。</summary>
public sealed class Wallet
{
	/// <summary>金币余额。</summary>
	public int Gold { get; private set; }

	/// <summary>余额发生变化。</summary>
	public event Action Changed;

	/// <summary>按存档余额创建钱包。</summary>
	/// <param name="gold">初始金币，负数视为 0。</param>
	public Wallet(int gold)
	{
		Gold = Math.Max(0, gold);
	}

	/// <summary>增加金币，溢出时封顶。</summary>
	/// <param name="amount">非负增加量。</param>
	/// <exception cref="ArgumentOutOfRangeException">增加量为负。</exception>
	public void AddGold(int amount)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(amount);
		if (amount == 0)
		{
			return;
		}

		Gold = (int)Math.Min((long)Gold + amount, int.MaxValue);
		Changed?.Invoke();
	}

	/// <summary>扣除金币；余额不足时不扣除。</summary>
	/// <param name="amount">非负扣除量。</param>
	/// <returns>余额是否足够并已扣除。</returns>
	/// <exception cref="ArgumentOutOfRangeException">扣除量为负。</exception>
	public bool TrySpendGold(int amount)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(amount);
		if (amount > Gold)
		{
			return false;
		}

		if (amount > 0)
		{
			Gold -= amount;
			Changed?.Invoke();
		}

		return true;
	}

	/// <summary>回滚到指定余额（关卡事务回滚时使用）。</summary>
	/// <param name="gold">要恢复的余额，负数视为 0。</param>
	public void Restore(int gold)
	{
		int value = Math.Max(0, gold);
		if (value == Gold)
		{
			return;
		}

		Gold = value;
		Changed?.Invoke();
	}
}
