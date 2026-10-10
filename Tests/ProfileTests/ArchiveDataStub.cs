namespace GodotGameFramework.Archive
{
	/// <summary>
	/// 测试侧替身：框架 <c>ArchiveData</c> 与 <c>ArchiveSystem</c> 同文件，后者依赖 Godot 资源加载，不链进纯 C# 单测。
	/// 字段与框架定义一致（只有 UnitId），保证 GameData 的 JSON 形状与游戏相同。
	/// </summary>
	public class ArchiveData
	{
		/// <summary>存档槽单位 ID。</summary>
		public long UnitId;
	}

	/// <summary>测试侧替身：框架存档目录基类（只有 UnitId）。</summary>
	public class ArchiveCatalogue
	{
		/// <summary>存档槽单位 ID。</summary>
		public long UnitId;
	}
}
