using System;

namespace GameLogic.Profile;

/// <summary>
/// 档案中一名英雄的持久记录。现阶段只有等级成长；技能书、装备栏、丹药等以后作为同级成员加入，
/// 由 <see cref="HeroStatBuilder"/> 统一换算为属性修正。
/// </summary>
public sealed class HeroRecord
{
	/// <summary>英雄 ID（HeroConfig.Id）。</summary>
	public int HeroId { get; }

	/// <summary>等级成长。</summary>
	public HeroProgression Progression { get; }

	/// <summary>创建英雄记录。</summary>
	/// <param name="heroId">英雄 ID。</param>
	/// <param name="progression">等级成长。</param>
	/// <exception cref="ArgumentNullException">成长为空。</exception>
	public HeroRecord(int heroId, HeroProgression progression)
	{
		HeroId = heroId;
		Progression = progression ?? throw new ArgumentNullException(nameof(progression));
	}
}
