using GameConfig.Battle;
using GameConfig.Monster;
using GameFramework.Entity;
using GameLogic.Battle;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;

namespace GameLogic.Entity
{
	/// <summary>
	/// 怪物基类：属性（MonsterConfig）、受击硬直/击退、死亡回收。
	/// 与英雄同一套动画架构：C# 只维护事实面（下方 [Export] 字段），该怪物自己的 AnimationTree
	/// 用 advance_expression 读事实选动画（Entity/AGENTS.md）。
	///
	/// M4 范围：可被打的"沙包"——站立、受击、击退、死亡；攻击/巡逻/追击的**决策**属于 M5 的
	/// GF.Fsm AI（每状态一个类），届时 AI 只写 <see cref="MoveInput"/> / <see cref="AttackSegment"/> 等事实，
	/// 动画与判定帧沿用本类与动画库，不需要改动画侧。
	/// </summary>
	public partial class MonsterEntity : ActorEntity
	{
		/// <summary>受击动画名（全项目标准名）：硬直时长 = 该动画长度</summary>
		private const string HurtAnimName = "hurt";

		/// <summary>死亡动画名（全项目标准名）：死亡到回收的时长 = 该动画长度</summary>
		private const string DeathAnimName = "death";

		// ---- 表达式事实面（AnimationTree 边只读这些字段，命名规则同 HeroEntity）----

		/// <summary>水平移动意图：-1 左 / 0 无 / 1 右（M5 AI 写入）</summary>
		[Export] public int MoveInput;

		/// <summary>当前攻击段（0 起；-1 = 不在攻击中；M5 AI 写入）</summary>
		[Export] public int AttackSegment = -1;

		/// <summary>受击硬直中</summary>
		[Export] public bool Hurt;

		/// <summary>已死亡</summary>
		[Export] public bool Dead;

		// ---- 配置 ----

		/// <summary>怪物配置 Id（对应 MonsterConfig.Id），场景必填</summary>
		[Export] public int MonsterId;

		/// <summary>怪物配置</summary>
		public MonsterConfig Config { get; private set; }

		/// <inheritdoc />
		public override CombatSide Side => CombatSide.Monster;

		/// <inheritdoc />
		protected override Vector2 PopAnchor => new Vector2(0, -50);

		/// <summary>受击硬直剩余（秒）</summary>
		private float m_HurtTime;

		/// <summary>受击硬直时长（hurt 动画长度，OnInit 缓存）</summary>
		private float m_HurtLen;

		/// <summary>死亡到回收的剩余时间（秒；&lt;0 = 未进入死亡）</summary>
		private float m_DeathTime = -1f;

		/// <summary>死亡动画长度（OnInit 缓存）</summary>
		private float m_DeathLen;

		/// <summary>已请求隐藏（防止死亡计时结束后重复 HideEntity）</summary>
		private bool m_HideRequested;

		/// <summary>已显示可驱动（同 HeroEntity.m_Active）</summary>
		private bool m_Active;

		public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance,
			object userData)
		{
			base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);

			Config = ConfigSystem.Instance.Tables.TbMonsterConfig.GetOrDefault(MonsterId);
			if (Config == null)
			{
				Log.Error("[MonsterEntity] MonsterConfig 缺失：MonsterId={0}，本实体停用", MonsterId);
				SetPhysicsProcess(false);
				return;
			}

			MaxHp = Config.Hp;
			m_HurtLen = GetAnimLength(HurtAnimName);
			m_DeathLen = GetAnimLength(DeathAnimName);
		}

		public override void OnShow(object userData)
		{
			base.OnShow(userData);

			Hp = MaxHp;
			MoveInput = 0;
			AttackSegment = -1;
			Hurt = false;
			Dead = false;
			m_HurtTime = 0f;
			m_DeathTime = -1f;
			m_HideRequested = false;
			Velocity = Vector2.Zero;

			// 位置由生成方通过 userData 传入（ShowEntity 的 userData 约定为出生点 Vector2）
			if (userData is Vector2 spawn)
			{
				GlobalPosition = spawn;
			}

			SetHurtBoxEnabled(true);
			if (AnimTree != null)
			{
				AnimTree.Active = true;
			}

			m_Active = true;
			SetFacing(-1);
		}

		public override void OnHide(bool isShutdown, object userData)
		{
			m_Active = false;
			if (!isShutdown && AnimTree != null && GodotObject.IsInstanceValid(AnimTree))
			{
				AnimTree.Active = false;
			}

			base.OnHide(isShutdown, userData);
		}

		public override void _PhysicsProcess(double delta)
		{
			if (!m_Active || Config == null)
			{
				return;
			}

			float dt = (float)delta;
			UpdateHurt(dt);
			UpdateDeath(dt);
			UpdateLocomotion(dt);
			SyncAnimFacts();
		}

		/// <summary>结算快照：全部取 MonsterConfig（怪物无成长，等级即表内等级）。</summary>
		protected override CombatantStats GetCombatStats()
		{
			if (Config == null)
			{
				return default;
			}

			return new CombatantStats(CombatSide.Monster, Config.Level, 0, Config.Def, Config.Mdef, Config.Crit,
				Config.Miss, Config.Lucky, Config.Toughness, Config.Htarget, Config.CritReduce, Config.Ar, Config.Sp);
		}

		/// <summary>
		/// 受击：进入硬直并施加击退（旧 BaseMonster state_hurt：击退 [0,0] 的招式不硬直不击退）；
		/// 攻击中被打断（旧项目怪物受击会打断出招，与英雄"出招不打断"相反）。
		/// </summary>
		protected override void OnHurt(AttackData attack, DamageResult result, Vector2 knockback)
		{
			base.OnHurt(attack, result, knockback);
			if (IsDead)
			{
				EnterDeath();
				return;
			}

			if (knockback == Vector2.Zero || m_HurtLen <= 0f)
			{
				return;
			}

			// 硬直中再次受击：重置计时（状态机停在 Hurt 不重播，见 build_huaguoshan_monkey_anim_tree.gd）
			AttackSegment = -1;
			EndAttack();
			m_HurtTime = m_HurtLen;
			Velocity = knockback;
		}

		private void UpdateHurt(float dt)
		{
			if (m_HurtTime > 0f)
			{
				m_HurtTime = Mathf.Max(0f, m_HurtTime - dt);
			}
		}

		/// <summary>死亡：关受击盒（尸体不再挨打）、计时播完死亡动画后回收实体。</summary>
		private void EnterDeath()
		{
			if (m_DeathTime >= 0f)
			{
				return;
			}

			AttackSegment = -1;
			m_HurtTime = 0f;
			EndAttack();
			SetHurtBoxEnabled(false);
			m_DeathTime = m_DeathLen;
		}

		private void UpdateDeath(float dt)
		{
			if (m_DeathTime < 0f || m_HideRequested)
			{
				return;
			}

			m_DeathTime -= dt;
			if (m_DeathTime <= 0f)
			{
				m_HideRequested = true;
				GF.Entity.HideEntitySafe(this);
			}
		}

		/// <summary>移动：重力常驻；硬直中保持击退速度并在地面衰减到 0，死亡定身。</summary>
		private void UpdateLocomotion(float dt)
		{
			Velocity += new Vector2(0, Config.Gravity * dt);

			if (IsDead)
			{
				Velocity = new Vector2(0, Velocity.Y);
			}
			else if (m_HurtTime <= 0f)
			{
				Velocity = new Vector2(MoveInput * Config.MoveSpeed, Velocity.Y);
				if (MoveInput != 0)
				{
					SetFacing(MoveInput);
				}
			}

			MoveAndSlide();
		}

		private void SyncAnimFacts()
		{
			Hurt = m_HurtTime > 0f;
			Dead = IsDead;
		}

		/// <summary>受击盒开关（deferred：可能在物理回调内调用）。</summary>
		private void SetHurtBoxEnabled(bool enabled)
		{
			if (HurtBox != null)
			{
				HurtBox.SetDeferred(Area2D.PropertyName.Monitorable, enabled);
			}
		}

		private float GetAnimLength(string animName)
		{
			if (AnimPlayer == null || !AnimPlayer.HasAnimation(animName))
			{
				Log.Warning("[MonsterEntity] 动画库缺少 {0}，相关时长按 0 处理", animName);
				return 0f;
			}

			return (float)AnimPlayer.GetAnimation(animName).Length;
		}
	}
}
