using System.Collections.Generic;
using GameConfig.Battle;
using GameConfig.Sound;
using Godot;
using GameFramework;

namespace GameLogic.Battle
{
	/// <summary>
	/// 攻击数据包：一次出招（一段普攻 / 一发子弹 / 一次 Buff 跳伤）结算所需的**全部**数据，自包含。
	///
	/// 设计要点：
	///  * **快照而不是配表引用**：出招那一刻把攻击方属性、掷好的威力、伤害类型、击退、无双值、
	///    命中音效全部拷进来。下游（命中结算、击退、飘字、音效）只认这个包，不回查 AttackConfig——
	///    所以子弹、反伤、Buff 跳伤这类没有配表行（或参数被运行时修改）的攻击也走同一条链路。
	///    快照时机与旧项目一致：旧 get_hit_data 在起手时锁定 HitDic，同一招打中多个目标威力相同。
	///  * **池化（GF 的 ReferencePool）**：每段普攻/每发子弹一个包，属于高频生灭的纯 C# 对象（红线 6）。
	///    非节点不走 NodePool。生命周期 = 一招：出招 <see cref="Create"/>，收招/子弹消失时
	///    <c>ReferencePool.Release</c>。命中结算只读不存，事件里只拷值（见 DamageDealtEventArgs）。
	///  * **一招一目标只结算一次**：<see cref="TryRegisterHit"/> 按目标实例号去重。引擎的 area_entered
	///    本身只在"进入重叠"时触发一次（旧项目完全靠它），这里再兜一层：判定窗口内受击盒若因硬直/无敌
	///    开关过而重新进入，不会被同一招打两次。多段技能要多次命中时，每段各开一个包。
	///  * 向量统一用 Godot.Vector2（2026-09-30 裁决：Battle/ 解除禁引，只触纯值类型，
	///    无引擎运行时依赖），单测引 GodotSharp 即可在纯控制台跑。
	/// </summary>
	public sealed class AttackData : IReference
	{
		/// <summary>来源攻击 Id（AttackConfig.Id；运行时动态攻击为 0）。只作日志/统计，结算不回查表</summary>
		public int AttackId { get; private set; }

		/// <summary>攻击方属性快照（出招时刻）</summary>
		public CombatantStats Attacker { get; private set; }

		/// <summary>本招威力（= 攻击方攻击力 × 掷出的倍率 + 固定攻击力；未取整，取整在结算里按旧顺序做）</summary>
		public float Power { get; private set; }

		/// <summary>伤害类型</summary>
		public DamageKind Kind { get; private set; }

		/// <summary>击退（表单位：X&gt;0 = 远离攻击者，Y&lt;0 = 向上；乘 BattleConfig 换算系数得 px/s）</summary>
		public Vector2 Knockback { get; private set; }

		/// <summary>出招方向：1 右 / -1 左（击退方向 = 本值 × Knockback.X）</summary>
		public int Direction { get; private set; }

		/// <summary>命中后攻击方获得的无双值（已在区间内掷定）</summary>
		public int WsGain { get; private set; }

		/// <summary>命中给受击方累计的受击保护值（旧 HitProtect；0 = 按表默认）</summary>
		public int HitProtect { get; private set; }

		/// <summary>命中音效（None = 无）</summary>
		public SoundId HitSoundId { get; private set; }

		/// <summary>本招已命中的目标（实例号），一招一目标只结算一次</summary>
		private readonly HashSet<ulong> m_HitTargets = new HashSet<ulong>();

		/// <summary>从引用池取一个攻击包并装填（各参数语义见同名属性）。</summary>
		public static AttackData Create(int attackId, in CombatantStats attacker, float power, DamageKind kind,
			Vector2 knockback, int direction, int wsGain, int hitProtect, SoundId hitSoundId)
		{
			AttackData data = ReferencePool.Acquire<AttackData>();
			data.AttackId = attackId;
			data.Attacker = attacker;
			data.Power = power;
			data.Kind = kind;
			data.Knockback = knockback;
			data.Direction = direction >= 0 ? 1 : -1;
			data.WsGain = wsGain;
			data.HitProtect = hitProtect;
			data.HitSoundId = hitSoundId;
			return data;
		}

		/// <summary>登记命中：目标本招首次命中返回 true（应结算），重复命中返回 false（应忽略）。</summary>
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
			WsGain = 0;
			HitProtect = 0;
			HitSoundId = SoundId.None;
			m_HitTargets.Clear();
		}
	}
}
