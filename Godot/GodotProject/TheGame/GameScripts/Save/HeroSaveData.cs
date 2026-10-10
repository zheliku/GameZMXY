using System;

namespace GameLogic.Save
{
	/// <summary>存档中一名英雄的持久数据：只含 ID 与可变状态，派生值（等级、属性）读档时重算。</summary>
	[Serializable]
	public sealed class HeroSaveData
	{
		/// <summary>英雄 ID（HeroConfig.Id）。</summary>
		public int HeroId { get; set; }

		/// <summary>累计经验（等级由经验曲线派生，不单独保存）。</summary>
		public int TotalExperience { get; set; }
	}
}
