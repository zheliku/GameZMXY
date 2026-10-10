using System;
using System.IO;
using GameConfig;
using Luban;

namespace GameLogic.Profile.Tests
{
	/// <summary>读取与游戏相同的 Luban 产物（测试输出目录 Data/），只加载被访问到的表。</summary>
	internal static class TestTables
	{
		/// <summary>全部配置表（懒加载，与游戏 ConfigSystem 同一读取方式）。</summary>
		public static readonly Tables Tables = new(name =>
			new ByteBuf(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", name + ".bytes"))));

		/// <summary>经过启动校验的经验曲线。</summary>
		public static readonly ExperienceCurve Curve = new(Tables.TbHeroLevelConfig.DataList);
	}
}
