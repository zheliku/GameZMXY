using GameConfig.Battle;
using GameConfig.Monster;
using GameFramework.Entity;
using GameFramework.Fsm;
using GameLogic.Battle;
using GameLogic.Entity.Monsters.AI;
using GameLogic.Event;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;

namespace GameLogic.Entity.Monsters
{
	/// <summary>
	/// 怪物基类 = **身体层**（抽象）：属性（MonsterConfig）、受击硬直/击退、死亡回收、感知、AI 宿主。
	/// 与英雄同一套动画架构：C# 只维护事实面（下方 [Export] 字段，事件事实在状态进入/离开处翻转），
	/// 该怪物自己的 AnimationTree 用 advance_expression 读事实选动画（Entity/AGENTS.md）。
	///
	/// **大脑由每种怪显式选择**：子类必须覆写 <see cref="CreateBrain"/>，从 <see cref="MonsterBrains"/> 取一套原型，
	/// 需要时再 Bind 替换角色 / AddExtra 追加状态（参考 tModLoader aiStyle、Unity Game Kit 每怪一个 Behaviour：
	/// 身体共用、大脑按原型复用、差异写在自己的类里）。
	///
	/// **AI 决策 = GF.Fsm**（<see cref="MonsterAiState"/> 每状态一个类，持有者为本类实现的
	/// <see cref="IMonsterAiAgent"/>）。三层分工：
	///  * AI 状态机只写**意图**：<see cref="IMonsterAiAgent.Move"/> → MoveInput、
	///    <see cref="IMonsterAiAgent.Face"/> → 朝向、<see cref="IMonsterAiAgent.RequestAttack"/> → 出招请求；
	///  * 本类把意图提交为**事实**（出招请求在安全时机才写 AttackSegment，见 <see cref="CommitPendingAttack"/>），
	///    并负责物理、受击、死亡、收招硬直；
	///  * AnimationTree 只读事实选动画——AI 状态名与动画状态互不耦合，改 AI 不动动画图。
	/// 状态机随实体显示创建、隐藏销毁（池复用时每次都是全新的 AI）。
	///
	/// **出招节奏**：请求 → 下一安全帧提交（转向目标、锁定整招朝向）→ 动画播完 OnAttackEnd →
	/// **收招硬直** AttackConfig.AiRecovery 秒（原地不动、不转身、不出招）→ AI 恢复决策。
	/// 受击仍会打断出招与硬直（旧项目手感）；霸体（SuperArmor）不打断。
	///
	/// **够不够得着**：每招的范围由攻击动画判定盒推导（<see cref="AttackReachReader"/>，唯一真相源在动画），
	/// 与目标受击盒求交（含高度）——目标跳到头顶时不会在下面空挥。远程招（无判定盒）按 AttackConfig.AiRange。
	///
	/// 感知：场景子节点 m_Detector（Area2D，Detector 层，扫 PlayerBody）宽度按 SightRange 设置，
	/// 无目标时查询重叠体锁定目标；被打时直接锁定攻击方（旧 _on_hurt_box_area_entered: has_target = true）。
	/// 目标一经锁定不因离开视野而丢失（同旧项目 has_target 只置不清），目标死亡/回收才失效。
	/// </summary>
	public abstract partial class MonsterEntity : ActorEntity, IMonsterAiAgent
	{
		/// <summary>受击动画名（全项目标准名）：硬直时长 = 该动画长度</summary>
		private const string HurtAnimName = "hurt";

		/// <summary>死亡动画名（全项目标准名）：死亡到回收的时长 = 该动画长度</summary>
		private const string DeathAnimName = "death";

		/// <summary>
		/// 出招请求提交前，攻击段须已归位（AttackSegment &lt; 0）的物理帧数。
		/// OnAttackEnd 在动画推进内把段归 -1，状态机要到下一次推进才离开攻击组；若紧接着的物理帧
		/// 就写回新段，单段攻击组会停在已播完的 attack_1 上卡死（同 HeroEntity.UpdateAttack 的一帧延迟）。
		/// </summary>
		private const int AttackRecommitFrames = 1;

		// ---- 表达式事实面（AnimationTree 边只读这些成员，命名规则同 HeroEntity）----

		/// <summary>水平移动意图：-1 左 / 0 无 / 1 右（AI 写入）</summary>
		[Export] public int MoveInput;

		/// <summary>当前攻击段（0 起；-1 = 不在攻击中）。由 AI 出招请求提交而来，段序号 i ↔ 动画 attack_(i+1)</summary>
		[Export] public int AttackSegment = -1;

		/// <summary>受击硬直中（进入硬直/硬直结束/死亡三处翻转）</summary>
		[Export] public bool Hurt;

		// 死亡事实 Dead 在 ActorEntity（唯一置位点在 ReceiveHit 扣血扣到 0）。

		// ---- 场景节点引用 ----

		/// <summary>索敌区（场景子节点 m_Detector，Detector 层、mask=PlayerBody；可为空 = 只会被打后反击）</summary>
		[Export] private Area2D m_Detector;

		// ---- 配置 ----

		/// <summary>怪物配置 Id（对应 MonsterConfig.Id），场景必填</summary>
		[Export] public int MonsterId;

		/// <summary>怪物配置</summary>
		public MonsterConfig Config { get; private set; }

		/// <inheritdoc />
		public override CombatSide Side => CombatSide.Monster;

		/// <summary>
		/// AI 开关（默认开）。关闭时销毁状态机、清空意图，怪物退化为 M4 的沙包——
		/// 供冒烟测试等调试工具使用；每次 OnShow 复位为开。
		/// </summary>
		public bool AiEnabled { get; private set; } = true;

		/// <summary>当前 AI 状态名（调试/冒烟测试观测用；无 AI 时为空串）</summary>
		public string AiStateName => (m_Fsm?.CurrentState as MonsterAiState)?.StateName ?? "";

		/// <summary>当前锁定的目标（无效时为 null）</summary>
		public ActorEntity Target => IsTargetValid(m_Target) ? m_Target : null;

		/// <summary>收招硬直中（调试/冒烟观测用）</summary>
		public bool InRecovery => m_RecoveryTime > 0f;

		/// <summary>受击硬直剩余（秒）</summary>
		private float m_HurtTime;

		/// <summary>受击硬直时长（hurt 动画长度，OnInit 缓存）</summary>
		private float m_HurtLen;

		/// <summary>死亡到回收的剩余时间（秒；&lt;0 = 未进入死亡）</summary>
		private float m_DeathTime = -1f;

		/// <summary>死亡动画长度（OnInit 缓存）</summary>
		private float m_DeathLen;

		/// <summary>收招硬直剩余（秒；OnAttackEnd 按本招 AttackConfig.AiRecovery 写入）</summary>
		private float m_RecoveryTime;

		/// <summary>本怪物的招式：AttackConfig 里 OwnerId==自己 的行，按 ComboIndex 排序（下标 = AttackSegment）</summary>
		private AttackConfig[] m_Attacks = [];

		/// <summary>技能书（招式的 AI 用法 + 冷却；每实例一份，冷却互不影响）</summary>
		private MonsterSkillBook m_Skills = MonsterSkillBook.Empty;

		/// <summary>当前状态机的状态集（显示期间存在）</summary>
		private MonsterAiStateSet m_StateSet;

		/// <summary>最后一次命中自己的攻击方（死亡事件的击杀者）</summary>
		private int m_LastAttackerId;

		/// <summary>已请求隐藏（防止死亡计时结束后重复 HideEntity）</summary>
		private bool m_HideRequested;

		/// <summary>AI 状态机（显示期间存在）</summary>
		private IFsm<IMonsterAiAgent> m_Fsm;

		/// <summary>AI 决策参数快照（OnInit 自配置拷入）</summary>
		private MonsterAiParams m_AiParams;

		/// <summary>锁定的目标</summary>
		private ActorEntity m_Target;

		/// <summary>出生点（巡逻半径的圆心）</summary>
		private Vector2 m_Home;

		/// <summary>待提交的出招请求（招式下标；-1 = 无）</summary>
		private int m_PendingAttack = -1;

		/// <summary>攻击段归位后已经过的物理帧数（见 AttackRecommitFrames）</summary>
		private int m_FramesSinceAttack;

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
			m_AiParams = new MonsterAiParams(Config.AttackDesire, Config.AttackInterval, Config.PatrolInterval,
				Config.PatrolIdleChance, Config.PatrolRadius, Config.BehitCalmTime, Config.AttackRangeSlack);
			LoadAttacks();
			if (isNewInstance)
			{
				ConfigureDetector();
			}
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
			m_RecoveryTime = 0f;
			m_HideRequested = false;
			m_Target = null;
			m_LastAttackerId = 0;
			m_PendingAttack = -1;
			m_FramesSinceAttack = AttackRecommitFrames;
			Velocity = Vector2.Zero;
			m_Skills.Reset(GD.Randf);

			// 位置由生成方通过 userData 传入（ShowEntity 的 userData 约定为出生点 Vector2）
			if (userData is Vector2 spawn)
			{
				GlobalPosition = spawn;
			}

			m_Home = GlobalPosition;

			SetHurtBoxEnabled(true);
			SetFacing(-1);

			AiEnabled = true;
			CreateAi();
		}

		public override void OnHide(bool isShutdown, object userData)
		{
			DestroyAi(isShutdown);
			m_Target = null;
			base.OnHide(isShutdown, userData);
		}

		public override void _PhysicsProcess(double delta)
		{
			if (!IsShown || Config == null)
			{
				return;
			}

			float dt = (float)delta;
			UpdateHurt(dt);
			UpdateDeath(dt);
			UpdateRecovery(dt);
			UpdateTarget();
			m_Skills.Tick(dt);
			CommitPendingAttack();
			UpdateLocomotion(dt);
		}

		/// <summary>阶级（MonsterConfig.Rank）</summary>
		public MonsterRank Rank => Config?.Rank ?? MonsterRank.Normal;

		/// <summary>
		/// 霸体：受击照常扣血与飘字，但不硬直、不击退、不打断出招（MonsterConfig.SuperArmor；
		/// 精英/首领常用）。子类可覆写做条件霸体（如 Boss 出招期间霸体、低血量霸体）。
		/// </summary>
		protected virtual bool IsSuperArmor => Config != null && Config.SuperArmor;

		/// <summary>
		/// 【每种怪必须实现】选这只怪的"大脑"：返回一套**全新**状态集（每次显示都会调用；GF.Fsm 约束状态实例不共享）。
		/// 标准写法：<c>return MonsterBrains.GroundMelee();</c>；特殊怪在返回前 Bind 替换角色 / AddExtra 追加状态。
		/// 返回 null = 不建 AI（记错误日志，怪物退化为沙包）。
		/// </summary>
		protected abstract MonsterAiStateSet CreateBrain();

		/// <summary>第 index 招由动画判定盒推导出的范围（原生朝左；调试/冒烟观测用）。越界返回空盒。</summary>
		public AiBox GetAttackReach(int index)
		{
			return index >= 0 && index < m_Skills.Count ? m_Skills[index].Reach : default;
		}

		/// <summary>
		/// 开关 AI（调试用）。关闭：销毁状态机、清意图与出招请求（已在播的招照常收招）；
		/// 开启：显示中则重建状态机，从初始状态开始。
		/// </summary>
		public void SetAiEnabled(bool enabled)
		{
			if (AiEnabled == enabled)
			{
				return;
			}

			AiEnabled = enabled;
			if (enabled)
			{
				CreateAi();
			}
			else
			{
				DestroyAi(false);
				MoveInput = 0;
				m_PendingAttack = -1;
			}
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
		/// 受击：锁定攻击方为目标；进入硬直并施加击退（旧 BaseMonster state_hurt：击退 [0,0] 的招式不硬直不击退）；
		/// 攻击中/收招硬直中被打断（旧项目怪物受击会打断出招，与英雄"出招不打断"相反）——
		/// 动画被切走不会再走到 OnAttackEnd，必须在这里显式 ReleaseAttack。
		/// </summary>
		protected override void OnHurt(AttackData attack, DamageResult result, Vector2 knockback, int attackerEntityId)
		{
			if (attackerEntityId != 0)
			{
				m_LastAttackerId = attackerEntityId;
			}

			// 霸体出招中不转身（基类的"面向攻击者"会打乱出招朝向）
			if (!(IsSuperArmor && AttackSegment >= 0))
			{
				base.OnHurt(attack, result, knockback, attackerEntityId);
			}

			if (Dead)
			{
				EnterDeath();
				return;
			}

			LockAttacker(attackerEntityId);

			if (IsSuperArmor || knockback == Vector2.Zero || m_HurtLen <= 0f)
			{
				return;
			}

			// 硬直中再次受击：重置计时（状态机停在 Hurt 不重播，见 build_huaguoshan_monkey_anim_tree.gd）
			InterruptAttack();
			m_HurtTime = m_HurtLen;
			Hurt = true;
			Velocity = knockback;
			PlaySound(Config.HurtSoundId);
		}

		private void UpdateHurt(float dt)
		{
			if (m_HurtTime > 0f)
			{
				m_HurtTime = Mathf.Max(0f, m_HurtTime - dt);
				if (m_HurtTime <= 0f)
				{
					Hurt = false;
				}
			}
		}

		private void UpdateRecovery(float dt)
		{
			if (m_RecoveryTime > 0f)
			{
				m_RecoveryTime = Mathf.Max(0f, m_RecoveryTime - dt);
			}
		}

		/// <summary>
		/// 【动画方法轨道回调】收招（attack_N 末帧）：按本招 AiRecovery 进入收招硬直，归位攻击事实，开始"归位帧"计数。
		/// </summary>
		public override void OnAttackEnd()
		{
			if (AttackSegment >= 0 && AttackSegment < m_Attacks.Length)
			{
				m_RecoveryTime = Mathf.Max(0f, m_Attacks[AttackSegment].AiRecovery);
			}

			AttackSegment = -1;
			MoveInput = 0;
			m_FramesSinceAttack = 0;
			base.OnAttackEnd();
		}

		/// <summary>动画调 OnAttackBegin 时解析"正在播的段"对应的攻击配置。</summary>
		protected override AttackConfig GetAttackConfig()
		{
			if (AttackSegment < 0 || AttackSegment >= m_Attacks.Length)
			{
				return null;
			}

			return m_Attacks[AttackSegment];
		}

		/// <summary>死亡：关受击盒（尸体不再挨打）、计时播完死亡动画后回收实体。</summary>
		private void EnterDeath()
		{
			if (m_DeathTime >= 0f)
			{
				return;
			}

			InterruptAttack();
			m_HurtTime = 0f;
			Hurt = false;
			MoveInput = 0;
			SetHurtBoxEnabled(false);
			m_DeathTime = m_DeathLen;
			PlaySound(Config.DeathSoundId);
			GF.Event.Fire(this, MonsterDiedEventArgs.Create(Id, Config.Id, Config.Rank, m_LastAttackerId, GlobalPosition));
		}

		/// <summary>打断出招：清请求与收招硬直、归位攻击段、归还攻击包（受击/死亡路径，动画不会再走到 OnAttackEnd）。</summary>
		private void InterruptAttack()
		{
			m_PendingAttack = -1;
			m_RecoveryTime = 0f;
			if (AttackSegment >= 0)
			{
				AttackSegment = -1;
				m_FramesSinceAttack = 0;
			}

			ReleaseAttack();
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

		/// <summary>
		/// 出招请求 → AttackSegment 事实。只在空闲（未出招、未硬直、未受控、未死亡）且攻击段已归位满
		/// <see cref="AttackRecommitFrames"/> 帧时提交；受击/死亡会把请求清掉。
		/// </summary>
		private void CommitPendingAttack()
		{
			if (AttackSegment >= 0)
			{
				return;
			}

			bool ready = m_FramesSinceAttack >= AttackRecommitFrames;
			m_FramesSinceAttack++;
			if (m_PendingAttack < 0 || !ready || Hurt || Dead || m_RecoveryTime > 0f)
			{
				return;
			}

			// 出招瞬间转向目标（旧 attack_target / to_hero），之后整招 + 收招硬直朝向锁定
			if (Target is { } target)
			{
				SetFacing(MonsterAiRules.Sign(target.GlobalPosition.X - GlobalPosition.X));
			}

			AttackSegment = m_PendingAttack;
			m_PendingAttack = -1;
			MoveInput = 0;
			m_Skills.MarkUsed(AttackSegment, GD.Randf());
			Log.Debug("[Monster] {0} 出招 段{1}", Id, AttackSegment + 1);
		}

		/// <summary>移动：重力常驻；硬直中保持击退速度，死亡/出招/收招硬直定身，其余按移动意图。</summary>
		private void UpdateLocomotion(float dt)
		{
			Velocity += new Vector2(0, Config.Gravity * dt);

			if (Dead || (!Hurt && (AttackSegment >= 0 || m_RecoveryTime > 0f)))
			{
				Velocity = new Vector2(0, Velocity.Y);
			}
			else if (!Hurt)
			{
				Velocity = new Vector2(MoveInput * Config.MoveSpeed, Velocity.Y);
				if (MoveInput != 0)
				{
					SetFacing(MoveInput);
				}
			}

			MoveAndSlide();
		}

		// ---- 感知 ----

		/// <summary>目标维护：失效即清；无目标时查询索敌区的重叠体（mask 只含 PlayerBody，层即敌我关系）。</summary>
		private void UpdateTarget()
		{
			if (m_Target != null && !IsTargetValid(m_Target))
			{
				m_Target = null;
			}

			if (m_Target != null || Dead || m_Detector == null || !m_Detector.HasOverlappingBodies())
			{
				return;
			}

			foreach (Node2D body in m_Detector.GetOverlappingBodies())
			{
				if (body is ActorEntity actor && actor != this && IsTargetValid(actor))
				{
					m_Target = actor;
					Log.Debug("[Monster] {0} 发现目标 {1}", Id, actor.Id);
					return;
				}
			}
		}

		/// <summary>被打：锁定攻击方（仅当它是有效的角色实体）。</summary>
		private void LockAttacker(int attackerEntityId)
		{
			if (attackerEntityId == 0 || attackerEntityId == Id)
			{
				return;
			}

			if (GF.Entity.GetEntity(attackerEntityId) is ActorEntity attacker && IsTargetValid(attacker))
			{
				m_Target = attacker;
			}
		}

		private static bool IsTargetValid(ActorEntity actor)
		{
			return actor != null && GodotObject.IsInstanceValid(actor) && actor.IsShown && !actor.Dead;
		}

		/// <summary>目标受击盒相对自己原点的矩形（无受击盒退化为原点点盒；无目标为空盒）。</summary>
		private AiBox GetTargetBox()
		{
			if (Target is not { } target)
			{
				return default;
			}

			if (target.HurtBox == null)
			{
				Vector2 delta = target.GlobalPosition - GlobalPosition;
				return AiBox.Point(delta.X, delta.Y);
			}

			Rect2 bounds = target.HurtBox.GetGlobalBounds();
			Vector2 min = bounds.Position - GlobalPosition;
			Vector2 max = bounds.End - GlobalPosition;
			return new AiBox(min.X, max.X, min.Y, max.Y);
		}

		/// <summary>
		/// 索敌区宽度 = 2 × SightRange（只看水平距离，同旧 abs(dx) &lt; mysee；高度由场景形状决定）。
		/// 形状资源复制一份再改，避免同场景的不同配置实例互相改写共享子资源。
		/// </summary>
		private void ConfigureDetector()
		{
			if (m_Detector == null)
			{
				return;
			}

			foreach (Node child in m_Detector.GetChildren())
			{
				if (child is CollisionShape2D { Shape: RectangleShape2D rect } shapeNode)
				{
					RectangleShape2D own = (RectangleShape2D)rect.Duplicate();
					own.Size = new Vector2(Config.SightRange * 2f, rect.Size.Y);
					shapeNode.Shape = own;
					return;
				}
			}

			Log.Warning("[MonsterEntity] m_Detector 缺少 RectangleShape2D 形状，索敌失效：{0}", Name);
		}

		// ---- AI 生命周期 ----

		private void CreateAi()
		{
			if (!AiEnabled || m_Fsm != null || Config == null || !IsShown)
			{
				return;
			}

			// 每次显示都新建一套状态实例（GF.Fsm 约束：状态实例不跨状态机共享；池复用也从初始状态重新开始）
			MonsterAiStateSet states = CreateBrain();
			if (states == null)
			{
				Log.Error("[MonsterEntity] {0} 的 CreateBrain() 返回 null，本只怪不建 AI", Config.NameCn);
				return;
			}

			m_StateSet = states;
			m_Fsm = GF.Fsm.CreateFsm<IMonsterAiAgent>($"MonsterAI_{Id}", this, m_StateSet.ToArray());
			m_Fsm.Start(m_StateSet.Resolve(m_StateSet.InitialRole));
		}

		/// <summary>销毁状态机。关停阶段框架会统一销毁全部状态机，这里只丢弃引用。</summary>
		private void DestroyAi(bool isShutdown)
		{
			if (m_Fsm == null)
			{
				return;
			}

			if (!isShutdown && !m_Fsm.IsDestroyed)
			{
				GF.Fsm.DestroyFsm(m_Fsm);
			}

			m_Fsm = null;
			m_StateSet = null;
		}

		// ---- IMonsterAiAgent（AI 状态只经这里读感知、写意图）----

		bool IMonsterAiAgent.IsDead => Dead;

		bool IMonsterAiAgent.IsCcLocked => Hurt;

		bool IMonsterAiAgent.IsAttacking => AttackSegment >= 0 || m_PendingAttack >= 0 || m_RecoveryTime > 0f;

		bool IMonsterAiAgent.HasTarget => Target != null;

		AiBox IMonsterAiAgent.TargetBox => GetTargetBox();

		float IMonsterAiAgent.TargetDeltaX => Target != null ? GetTargetBox().CenterX : 0f;

		float IMonsterAiAgent.HomeDeltaX => GlobalPosition.X - m_Home.X;

		MonsterAiParams IMonsterAiAgent.Params => m_AiParams;

		MonsterSkillBook IMonsterAiAgent.Skills => m_Skills;

		MonsterAiStateSet IMonsterAiAgent.States => m_StateSet;

		float IMonsterAiAgent.NextRandom()
		{
			return GD.Randf();
		}

		void IMonsterAiAgent.Move(int dir)
		{
			MoveInput = Mathf.Clamp(dir, -1, 1);
		}

		void IMonsterAiAgent.Face(int dir)
		{
			if (!Hurt && !Dead && AttackSegment < 0 && m_RecoveryTime <= 0f)
			{
				SetFacing(dir);
			}
		}

		bool IMonsterAiAgent.RequestAttack(int index)
		{
			if (index < 0 || index >= m_Attacks.Length || AttackSegment >= 0 || m_PendingAttack >= 0 ||
			    m_RecoveryTime > 0f || Hurt || Dead)
			{
				return false;
			}

			m_PendingAttack = index;
			MoveInput = 0;
			return true;
		}

		// ---- 配置装配 ----

		/// <summary>
		/// 装配招式：AttackConfig 里 OwnerId==自己 的行，按 ComboIndex 排序（同 HeroEntity.LoadCombo）；
		/// 每招的够得着范围从它的攻击动画（AttackConfig.Animation）判定盒推导。
		/// </summary>
		private void LoadAttacks()
		{
			m_Attacks = LoadOwnAttacks(Config.EntityId);

			MonsterSkillSpec[] specs = new MonsterSkillSpec[m_Attacks.Length];
			for (int i = 0; i < m_Attacks.Length; i++)
			{
				AttackConfig a = m_Attacks[i];
				AiBox reach = AttackReachReader.Read(this, a.Animation);
				specs[i] = new MonsterSkillSpec(i, a.AiPriority, a.AiWeight, a.AiRange.X, a.AiRange.Y,
					a.AiCooldown.X, a.AiCooldown.Y, a.AiInitCooldown.X, a.AiInitCooldown.Y, reach);
				if (a.AiWeight > 0 && reach.IsEmpty && a.AiRange.Y <= 0f)
				{
					// 既没有判定盒（动画里从未启用判定），也没填远程距离：AI 永远判定"够不着"
					Log.Warning("[MonsterEntity] {0} 的招式 {1}（动画 {2}）推导不出判定盒范围，且 AiRange 为 0,0：AI 不会使用这招",
						Config.NameCn, a.Id, a.Animation);
				}
				else
				{
					Log.Debug("[MonsterEntity] {0} 招式 {1} 范围 {2}", Config.NameCn, a.Id, reach);
				}
			}

			m_Skills = new MonsterSkillBook(specs);

			if (m_Attacks.Length == 0)
			{
				Log.Warning("[MonsterEntity] {0} 没有任何招式（AttackConfig.OwnerId={1}），AI 不会出招",
					Config.NameCn, Config.EntityId);
			}
		}

		/// <summary>受击盒开关（deferred：可能在物理回调内调用）。</summary>
		private void SetHurtBoxEnabled(bool enabled)
		{
			if (HurtBox != null)
			{
				HurtBox.SetDeferred(Area2D.PropertyName.Monitorable, enabled);
			}
		}
	}
}
