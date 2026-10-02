using GameFramework;
using GameFramework.Event;

namespace GameLogic.Event
{
	/// <summary>
	/// 英雄死亡事件（扣血到 0 的那一刻发一次，早于死亡动画）。发布方：<see cref="Entity.Heroes.HeroEntity"/>
	/// （OnDied 钩子，同 MonsterEntity 广播 MonsterDiedEventArgs 的模式）；订阅方：LevelDirector（判负）。
	///
	/// 只携带值，按根规范 §9 走 ReferencePool。
	/// </summary>
	public sealed class HeroDiedEventArgs : GameEventArgs
	{
		/// <summary>事件编号</summary>
		public static readonly int EventId = typeof(HeroDiedEventArgs).GetHashCode();

		/// <inheritdoc />
		public override int Id => EventId;

		/// <summary>死亡英雄的实体编号</summary>
		public int EntityId { get; private set; }

		/// <summary>击杀者实体编号（0 = 无实体来源）</summary>
		public int KillerEntityId { get; private set; }

		public static HeroDiedEventArgs Create(int entityId, int killerEntityId)
		{
			HeroDiedEventArgs e = ReferencePool.Acquire<HeroDiedEventArgs>();
			e.EntityId = entityId;
			e.KillerEntityId = killerEntityId;
			return e;
		}

		/// <summary>转发复制（根规范 §5.2）</summary>
		public static HeroDiedEventArgs Create(HeroDiedEventArgs source)
		{
			return Create(source.EntityId, source.KillerEntityId);
		}

		public override void Clear()
		{
			EntityId = 0;
			KillerEntityId = 0;
		}
	}
}
