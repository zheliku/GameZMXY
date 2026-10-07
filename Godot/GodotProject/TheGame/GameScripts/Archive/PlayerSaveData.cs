using System;

namespace GameLogic.Archive
{
	/// <summary>
	/// 可写入存档的玩家普通数据：只保存整数等普通值，不含可绑定属性、角色节点或 UI 引用。
	/// 流程读取后交给英雄复制数值，保存时从英雄捕获新的普通数据快照。
	/// </summary>
	[Serializable]
	public sealed class PlayerSaveData
	{
		/// <summary>当前等级。</summary>
		public int Level { get; set; } = 1;

		/// <summary>累计经验。</summary>
		public int TotalExperience { get; set; }

		/// <summary>金币。</summary>
		public int Gold { get; set; }
	}
}
