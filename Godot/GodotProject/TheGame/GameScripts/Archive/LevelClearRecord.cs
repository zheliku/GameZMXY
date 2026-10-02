using System;

namespace GameLogic.Archive
{
	/// <summary>
	/// 单个关卡的通关记录（<see cref="GameData.LevelClearRecords"/> 列表元素；纯可序列化数据，根规范 §9）。
	/// </summary>
	[Serializable]
	public class LevelClearRecord
	{
		/// <summary>关卡ID（LevelConfig.Id）</summary>
		public int LevelId;

		/// <summary>最好用时（秒；取历次通关最小值）</summary>
		public double BestSeconds;

		/// <summary>通关次数</summary>
		public int ClearCount;
	}
}
