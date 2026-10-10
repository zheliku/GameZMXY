using System;

namespace GameLogic.Battle.Stats;

/// <summary>
/// 角色的生命与魔法资源：当前值与上限一起维护，任何修改都在一次调用内完成并只通知一次。
/// 上限来自 <see cref="StatSheet"/> 的最终值，由持有者在属性变化后调用 <see cref="SetMaximums"/> 同步。
/// 纯 C#；死亡判定以 <see cref="Damage"/> 返回值为唯一入口。
/// </summary>
public sealed class Vitals
{
	/// <summary>当前生命。</summary>
	public int Hp { get; private set; }

	/// <summary>生命上限。</summary>
	public int MaxHp { get; private set; }

	/// <summary>当前魔法。</summary>
	public int Mp { get; private set; }

	/// <summary>魔法上限。</summary>
	public int MaxMp { get; private set; }

	/// <summary>生命已归零。</summary>
	public bool IsDepleted => Hp <= 0;

	/// <summary>任一数值发生变化；订阅方一次读取全部数值，不会看到中间态。</summary>
	public event Action Changed;

	/// <summary>同步上限；补满或把当前值钳到新上限。</summary>
	/// <param name="maxHp">新的生命上限，下限 1。</param>
	/// <param name="maxMp">新的魔法上限，下限 0。</param>
	/// <param name="refill">是否补满生命与魔法（进关、升级）。</param>
	public void SetMaximums(int maxHp, int maxMp, bool refill)
	{
		// 生命上限至少为 1，避免显示期间被判为死亡。
		MaxHp = Math.Max(1, maxHp);
		MaxMp = Math.Max(0, maxMp);
		Hp = refill ? MaxHp : Math.Min(Hp, MaxHp);
		Mp = refill ? MaxMp : Math.Min(Mp, MaxMp);
		Changed?.Invoke();
	}

	/// <summary>扣除生命。</summary>
	/// <param name="amount">伤害值；非正数不处理。</param>
	/// <returns>本次扣除是否使生命归零（只在归零的那一次返回真）。</returns>
	public bool Damage(int amount)
	{
		if (amount <= 0 || IsDepleted)
		{
			return false;
		}

		Hp = Math.Max(0, Hp - amount);
		Changed?.Invoke();
		return IsDepleted;
	}

	/// <summary>恢复生命，不超过上限；已归零时不复活。</summary>
	/// <param name="amount">恢复量；非正数不处理。</param>
	public void Heal(int amount)
	{
		if (amount <= 0 || IsDepleted || Hp >= MaxHp)
		{
			return;
		}

		Hp = Math.Min(MaxHp, Hp + amount);
		Changed?.Invoke();
	}

	/// <summary>消耗魔法；不足时不扣除。</summary>
	/// <param name="amount">消耗量；非正数视为成功且不扣除。</param>
	/// <returns>魔法是否足够并已扣除。</returns>
	public bool TrySpendMp(int amount)
	{
		if (amount <= 0)
		{
			return true;
		}

		if (Mp < amount)
		{
			return false;
		}

		Mp -= amount;
		Changed?.Invoke();
		return true;
	}

	/// <summary>恢复魔法，不超过上限。</summary>
	/// <param name="amount">恢复量；非正数不处理。</param>
	public void RestoreMp(int amount)
	{
		if (amount <= 0 || Mp >= MaxMp)
		{
			return;
		}

		Mp = Math.Min(MaxMp, Mp + amount);
		Changed?.Invoke();
	}
}
