using GameConfig.Monster;
using GameFramework;
using GameFramework.Event;
using Godot;

namespace GameLogic.Event
{
	/// <summary>
	/// 怪物死亡事件（扣血到 0 的那一刻发一次，早于死亡动画与实体回收）。
	/// 发布方：MonsterEntity；关卡计数、经验/掉落、击杀统计等外部玩法系统可按需订阅。
	///
	/// 只携带值（实体随后会被回收复用，订阅者不得持有实体引用），按根规范 §9 走 ReferencePool。
	/// </summary>
	public sealed class MonsterDiedEventArgs : GameEventArgs
	{
		/// <summary>事件编号</summary>
		public static readonly int EventId = typeof(MonsterDiedEventArgs).GetHashCode();

		/// <inheritdoc />
		public override int Id => EventId;

		/// <summary>死亡怪物的实体编号</summary>
		public int EntityId { get; private set; }

		/// <summary>MonsterConfig.Id（经验/掉落查表用）</summary>
		public int MonsterId { get; private set; }

		/// <summary>阶级（Boss 死亡触发结算等）</summary>
		public MonsterRank Rank { get; private set; }

		/// <summary>击杀者实体编号（0 = 无实体来源）</summary>
		public int KillerEntityId { get; private set; }

		/// <summary>死亡位置（世界坐标，掉落物出生点）</summary>
		public Vector2 Position { get; private set; }

		/// <summary>从引用池创建并填充一次怪物死亡事件。</summary>
		/// <param name="entityId">怪物实体编号。</param>
		/// <param name="monsterId">怪物配置编号。</param>
		/// <param name="rank">怪物阶级。</param>
		/// <param name="killerEntityId">击杀者实体编号；无实体来源时为 0。</param>
		/// <param name="position">死亡世界坐标。</param>
		/// <returns>由引用池持有、仅在本次事件分发期间有效的参数。</returns>
		public static MonsterDiedEventArgs Create(int entityId, int monsterId, MonsterRank rank, int killerEntityId,
			Vector2 position)
		{
			MonsterDiedEventArgs e = ReferencePool.Acquire<MonsterDiedEventArgs>();
			e.EntityId = entityId;
			e.MonsterId = monsterId;
			e.Rank = rank;
			e.KillerEntityId = killerEntityId;
			e.Position = position;
			return e;
		}

		/// <summary>清除本次事件数据，供引用池复用。</summary>
		public override void Clear()
		{
			EntityId = 0;
			MonsterId = 0;
			Rank = MonsterRank.Normal;
			KillerEntityId = 0;
			Position = Vector2.Zero;
		}
	}
}
