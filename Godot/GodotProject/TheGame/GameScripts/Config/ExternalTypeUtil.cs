using Godot;

namespace GameLogic.Config
{
	/// <summary>
	/// Luban TypeMapper 的外部类型构造函数（Configs/GameConfig/Defines/external_types.xml 引用）。
	/// 把配置侧占位 bean（vector2/vector2i）转换成 Godot 引擎类型。
	/// </summary>
	public static class ExternalTypeUtil
	{
		/// <summary>配置 vector2 → Godot.Vector2</summary>
		public static Vector2 NewVector2(GameConfig.vector2 v)
		{
			return new Vector2(v.X, v.Y);
		}

		/// <summary>配置 vector2i → Godot.Vector2I</summary>
		public static Vector2I NewVector2I(GameConfig.vector2i v)
		{
			return new Vector2I(v.X, v.Y);
		}
	}
}
