using GameLogic.Battle.Damage;
using System.Collections.Generic;
using GameConfig.Battle;
using GameConfig.Sound;
using Godot;
using GameFramework;

namespace GameLogic.Battle.Attacks
{
	/// <summary>一次出招的池化实例：冻结攻击输入并对目标去重；出招创建，收招归还，命中方不得保留引用。</summary>
	public sealed class AttackInstance : IReference
	{
		/// <summary>来源攻击 Id（AttackConfig.Id；运行时动态攻击为 0）。受击结算不回查表；攻击方用它结算自己的命中收益</summary>
		public int AttackId { get; private set; }

		/// <summary>攻击方属性快照（出招时刻）</summary>
		public CombatantAttributes Attacker { get; private set; }

		/// <summary>本招威力（属性威力与固定威力之和，再乘生成攻击倍率；最终取整由伤害结算负责）。</summary>
		public float Power { get; private set; }

		/// <summary>伤害类型</summary>
		public DamageKind Kind { get; private set; }

		/// <summary>击退（表单位：X&gt;0 = 远离攻击者，Y&lt;0 = 向上；乘 BattleConfig 换算系数得 px/s）</summary>
		public Vector2 Knockback { get; private set; }

		/// <summary>出招方向：1 右 / -1 左（击退方向 = 本值 × Knockback.X）</summary>
		public int Direction { get; private set; }

		/// <summary>命中给受击方累计的受击保护值（旧 HitProtect；0 = 按表默认）</summary>
		public int HitProtect { get; private set; }

		/// <summary>命中音效（None = 无）</summary>
		public SoundId HitSoundId { get; private set; }

		private readonly HashSet<ulong> m_HitTargets = new HashSet<ulong>(); // 本招已命中的目标实例号，防止重复结算。

		/// <summary>从引用池取一个攻击包并装填（各参数语义见同名属性）。</summary>
		/// <param name="attackId">来源攻击编号；动态攻击传 0。</param>
		/// <param name="attacker">出招时的攻击方属性快照。</param>
		/// <param name="power">本招威力。</param>
		/// <param name="kind">伤害类型。</param>
		/// <param name="knockback">击退参数。</param>
		/// <param name="direction">出招方向；非负视为向右，负数视为向左。</param>
		/// <param name="hitProtect">本次命中增加的受击保护值。</param>
		/// <param name="hitSoundId">命中音效编号。</param>
		/// <returns>已装填并由引用池管理的攻击数据包。</returns>
		public static AttackInstance Create(int attackId, in CombatantAttributes attacker, float power, DamageKind kind,
			Vector2 knockback, int direction, int hitProtect, SoundId hitSoundId)
		{
			AttackInstance data = ReferencePool.Acquire<AttackInstance>();
			data.AttackId = attackId;
			data.Attacker = attacker;
			data.Power = power;
			data.Kind = kind;
			data.Knockback = knockback;
			data.Direction = direction >= 0 ? 1 : -1;
			data.HitProtect = hitProtect;
			data.HitSoundId = hitSoundId;
			return data;
		}

		/// <summary>登记命中：目标本招首次命中返回 true（应结算），重复命中返回 false（应忽略）。</summary>
		/// <param name="targetInstanceId">目标节点实例编号。</param>
		/// <returns>目标首次命中时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
		public bool TryRegisterHit(ulong targetInstanceId)
		{
			return m_HitTargets.Add(targetInstanceId);
		}

		/// <summary>归还引用池时清空（IReference）。</summary>
		public void Clear()
		{
			AttackId = 0;
			Attacker = default;
			Power = 0f;
			Kind = DamageKind.Physics;
			Knockback = Vector2.Zero;
			Direction = 1;
			HitProtect = 0;
			HitSoundId = SoundId.None;
			m_HitTargets.Clear();
		}
	}
}
