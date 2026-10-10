using System;
using System.Collections.Generic;

namespace GameLogic.Save
{
	/// <summary>
	/// 存档版本 1 的档案快照：一个存档槽的全部持久状态。只含 ID、数量与可变状态，不含配置派生值和运行时引用。
	/// 与运行时 <see cref="GameLogic.Profile.PlayerProfile"/> 的映射只在 <see cref="ProfileMapper"/> 一处。
	/// </summary>
	[Serializable]
	public sealed class ProfileSaveData
	{
		/// <summary>出战英雄 ID。</summary>
		public int ActiveHeroId { get; set; }

		/// <summary>已拥有英雄的持久数据。</summary>
		public List<HeroSaveData> Heroes { get; set; } = new();

		/// <summary>金币。</summary>
		public int Gold { get; set; }
	}
}
