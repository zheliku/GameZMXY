using System;
using GameConfig;
using GameLogic.Profile;

namespace GameLogic.Session
{
	/// <summary>
	/// 档案作用域（一个存档槽从读档到回到标题期间）的根对象：持有玩家档案与档案级共享规则。
	/// 由 ProcedureLoadProfile 创建并写入流程状态机数据，后续流程按 <see cref="DataKey"/> 读取；
	/// 不是全局单例，只经流程数据与构造参数显式传递。
	/// </summary>
	public sealed class GameContext
	{
		/// <summary>流程状态机数据中保存本对象的键。</summary>
		public const string DataKey = "GameContext";

		/// <summary>当前档案（唯一的持久状态所有者）。</summary>
		public PlayerProfile Profile { get; }

		/// <summary>共享经验曲线（启动期已校验）。</summary>
		public ExperienceCurve Curve { get; }

		/// <summary>英雄出战装配规则。</summary>
		public HeroStatBuilder StatBuilder { get; }

		/// <summary>存档读写入口。</summary>
		public GameLogic.Save.SaveService Save { get; }

		/// <summary>创建档案作用域。</summary>
		/// <param name="profile">已读档或新建的档案。</param>
		/// <param name="curve">共享经验曲线。</param>
		/// <param name="tables">配置总表（构建档案级规则）。</param>
		/// <param name="save">存档读写入口。</param>
		/// <exception cref="ArgumentNullException">任一参数为空。</exception>
		public GameContext(PlayerProfile profile, ExperienceCurve curve, Tables tables, GameLogic.Save.SaveService save)
		{
			ArgumentNullException.ThrowIfNull(tables);
			Profile = profile ?? throw new ArgumentNullException(nameof(profile));
			Curve = curve ?? throw new ArgumentNullException(nameof(curve));
			Save = save ?? throw new ArgumentNullException(nameof(save));
			StatBuilder = new HeroStatBuilder(tables.TbHeroGrowthConfig);
		}
	}
}
