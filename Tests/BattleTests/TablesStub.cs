namespace GameConfig
{
	/// <summary>
	/// 测试侧占位：生成的 BattleConfig / TbBattleConfig 的 ResolveRef(Tables) 需要这个类型。
	/// 真正的 Tables.cs 聚合了全部表（含引用 Godot 类型的表），不链进纯 C# 单测。
	/// BattleConfig 没有外键，ResolveRef 是空实现，占位类不会被实际使用。
	/// </summary>
	public partial class Tables
	{
	}
}
