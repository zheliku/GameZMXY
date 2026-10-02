using GameFramework;
using GameFramework.Event;

namespace GameLogic.Event
{
	/// <summary>
	/// 英雄状态变化事件（生命/无双/等级）。发布方：<see cref="Entity.Heroes.HeroEntity"/>
	/// （OnShow 复位、受击扣血、命中涨无双、回血）；订阅方：HUD 血条/无双条。
	///
	/// 只携带值（实体随后可能被回收，订阅者不得持有实体引用），按根规范 §9 走 ReferencePool。
	/// </summary>
	public sealed class HeroVitalsChangedEventArgs : GameEventArgs
	{
		/// <summary>事件编号</summary>
		public static readonly int EventId = typeof(HeroVitalsChangedEventArgs).GetHashCode();

		/// <inheritdoc />
		public override int Id => EventId;

		/// <summary>英雄实体编号</summary>
		public int EntityId { get; private set; }

		/// <summary>当前生命</summary>
		public int Hp { get; private set; }

		/// <summary>最大生命</summary>
		public int MaxHp { get; private set; }

		/// <summary>等级</summary>
		public int Level { get; private set; }

		/// <summary>无双值</summary>
		public int WsValue { get; private set; }

		/// <summary>无双值上限（BattleConfig.WsMax）</summary>
		public int WsMax { get; private set; }

		public static HeroVitalsChangedEventArgs Create(int entityId, int hp, int maxHp, int level,
			int wsValue, int wsMax)
		{
			HeroVitalsChangedEventArgs e = ReferencePool.Acquire<HeroVitalsChangedEventArgs>();
			e.EntityId = entityId;
			e.Hp = hp;
			e.MaxHp = maxHp;
			e.Level = level;
			e.WsValue = wsValue;
			e.WsMax = wsMax;
			return e;
		}

		/// <summary>转发复制（根规范 §5.2）</summary>
		public static HeroVitalsChangedEventArgs Create(HeroVitalsChangedEventArgs source)
		{
			return Create(source.EntityId, source.Hp, source.MaxHp, source.Level, source.WsValue, source.WsMax);
		}

		public override void Clear()
		{
			EntityId = 0;
			Hp = 0;
			MaxHp = 0;
			Level = 0;
			WsValue = 0;
			WsMax = 0;
		}
	}
}
