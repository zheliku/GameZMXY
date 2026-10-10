using System;

namespace GameLogic.Profile;

/// <summary>
/// 一名英雄的等级成长：累计经验是唯一事实，等级、本级经验与上限都由经验曲线派生，不会出现不一致的中间态。
/// 属于档案（跨关卡保留并写入存档）；关卡内获得的经验直接写入这里。
/// </summary>
public sealed class HeroProgression
{
	private readonly ExperienceCurve m_Curve; // 已验证的共享经验曲线。

	/// <summary>累计经验。</summary>
	public int TotalExperience { get; private set; }

	/// <summary>当前等级。</summary>
	public int Level { get; private set; }

	/// <summary>本级已获得经验。</summary>
	public int Experience { get; private set; }

	/// <summary>升下一级所需经验；满级为 0。</summary>
	public int MaxExperience { get; private set; }

	/// <summary>已达到经验表最高等级。</summary>
	public bool IsMaxLevel => Level >= m_Curve.MaxLevel;

	/// <summary>累计经验或等级发生变化；订阅方一次读取全部派生值。</summary>
	public event Action Changed;

	/// <summary>按累计经验创建成长状态。</summary>
	/// <param name="curve">共享经验曲线。</param>
	/// <param name="totalExperience">累计经验，超出范围时钳到首末门槛。</param>
	/// <exception cref="ArgumentNullException">经验曲线为空。</exception>
	public HeroProgression(ExperienceCurve curve, int totalExperience)
	{
		m_Curve = curve ?? throw new ArgumentNullException(nameof(curve));
		Apply(totalExperience);
	}

	/// <summary>获得经验，可一次跨多级，满级后不再增加。</summary>
	/// <param name="amount">非负经验值。</param>
	/// <returns>本次提升的等级数；未升级为 0。</returns>
	/// <exception cref="ArgumentOutOfRangeException">经验为负。</exception>
	public int AddExperience(int amount)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(amount);
		int total = m_Curve.Add(TotalExperience, amount);
		if (total == TotalExperience)
		{
			return 0;
		}

		int previousLevel = Level;
		Apply(total);
		Changed?.Invoke();
		return Level - previousLevel;
	}

	/// <summary>回滚到指定累计经验（关卡事务回滚时使用）。</summary>
	/// <param name="totalExperience">要恢复的累计经验。</param>
	public void Restore(int totalExperience)
	{
		if (totalExperience == TotalExperience)
		{
			return;
		}

		Apply(totalExperience);
		Changed?.Invoke();
	}

	/// <summary>写入累计经验并刷新全部派生值。</summary>
	/// <param name="totalExperience">累计经验。</param>
	private void Apply(int totalExperience)
	{
		TotalExperience = Math.Clamp(totalExperience, 0, m_Curve.MaxTotalExperience);
		(Level, Experience, MaxExperience) = m_Curve.Evaluate(TotalExperience);
	}
}
