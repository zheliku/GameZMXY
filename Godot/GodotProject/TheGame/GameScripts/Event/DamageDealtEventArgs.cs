using GameConfig.Battle;
using GameFramework;
using GameFramework.Event;
using Godot;

namespace GameLogic.Event
{
	/// <summary>
	/// 命中结算完成事件（每次命中一条，含闪避）。发布方：受击实体（ActorEntity.ReceiveHit）；
	/// 订阅方：飘字（DamagePopManager）、后续 HUD/连击数/统计。
	///
	/// 只携带**值**：不引用 AttackData（攻击包随招式归还，事件分发时可能已被复用）、
	/// 不引用实体节点（实体可能在同帧死亡回收）——订阅者需要的全部信息在这里拷一份。
	/// 按根规范 §9：Create() 取自 ReferencePool，分发后由事件池回收，订阅者不得持有本对象。
	/// </summary>
	public sealed class DamageDealtEventArgs : GameEventArgs
	{
		/// <summary>事件编号</summary>
		public static readonly int EventId = typeof(DamageDealtEventArgs).GetHashCode();

		/// <inheritdoc />
		public override int Id => EventId;

		/// <summary>攻击方实体编号（0 = 无实体来源）</summary>
		public int AttackerEntityId { get; private set; }

		/// <summary>受击方实体编号</summary>
		public int TargetEntityId { get; private set; }

		/// <summary>受击方是否为英雄（飘字按此选"英雄受伤/怪物受伤"样式）</summary>
		public bool TargetIsHero { get; private set; }

		/// <summary>最终伤害（闪避为 0）</summary>
		public int Damage { get; private set; }

		/// <summary>是否闪避</summary>
		public bool IsMiss { get; private set; }

		/// <summary>是否暴击</summary>
		public bool IsCrit { get; private set; }

		/// <summary>伤害类型</summary>
		public DamageKind Kind { get; private set; }

		/// <summary>飘字锚点（世界坐标，受击方头顶）</summary>
		public Vector2 PopPosition { get; private set; }

		/// <summary>受击方剩余生命</summary>
		public int TargetHp { get; private set; }

		/// <summary>从引用池创建并填充一次命中事件。</summary>
		/// <param name="attackerEntityId">攻击方实体编号；无实体来源时为 0。</param>
		/// <param name="targetEntityId">受击方实体编号。</param>
		/// <param name="targetIsHero">受击方是否为英雄。</param>
		/// <param name="damage">最终伤害。</param>
		/// <param name="isMiss">是否闪避。</param>
		/// <param name="isCrit">是否暴击。</param>
		/// <param name="kind">伤害类型。</param>
		/// <param name="popPosition">飘字世界坐标。</param>
		/// <param name="targetHp">结算后的剩余生命。</param>
		/// <returns>由引用池持有、仅在本次事件分发期间有效的参数。</returns>
		public static DamageDealtEventArgs Create(int attackerEntityId, int targetEntityId, bool targetIsHero,
			int damage, bool isMiss, bool isCrit, DamageKind kind, Vector2 popPosition, int targetHp)
		{
			DamageDealtEventArgs e = ReferencePool.Acquire<DamageDealtEventArgs>();
			e.AttackerEntityId = attackerEntityId;
			e.TargetEntityId = targetEntityId;
			e.TargetIsHero = targetIsHero;
			e.Damage = damage;
			e.IsMiss = isMiss;
			e.IsCrit = isCrit;
			e.Kind = kind;
			e.PopPosition = popPosition;
			e.TargetHp = targetHp;
			return e;
		}

		/// <summary>清除本次事件数据，供引用池复用。</summary>
		public override void Clear()
		{
			AttackerEntityId = 0;
			TargetEntityId = 0;
			TargetIsHero = false;
			Damage = 0;
			IsMiss = false;
			IsCrit = false;
			Kind = DamageKind.Physics;
			PopPosition = Vector2.Zero;
			TargetHp = 0;
		}
	}
}
