using System;

namespace GameLogic.Save
{
	/// <summary>
	/// 存档版本 0 的玩家数据（重构前格式），只用于读取并迁移旧存档，不再写入。
	/// 字段不设初始值：缺字段即视为 0，迁移按"等级不倒退、累计经验不丢失"规则合并。
	/// </summary>
	[Serializable]
	public sealed class PlayerSaveDataV0
	{
		/// <summary>旧格式独立保存的等级。</summary>
		public int Level { get; set; }

		/// <summary>累计经验。</summary>
		public int TotalExperience { get; set; }

		/// <summary>金币。</summary>
		public int Gold { get; set; }
	}
}
