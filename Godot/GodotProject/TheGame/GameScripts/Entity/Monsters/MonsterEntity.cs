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
	/// 怪物基类 = **身体层**（抽象）：属性（MonsterConfig）、受击/死亡、出招节奏、感知、AI 宿主。
	/// 每种怪必须覆写 <see cref="CreateBrain"/> 从 <see cref="MonsterBrains"/> 选一套大脑原型。
	///
	/// 三层分工（同英雄的动画架构）：
	///  * AI（GF.Fsm&lt;<see cref="IMonsterAiAgent"/>&gt;）只写**意图**：Move / Face / RequestAttack；
	///  * 本类把意图提交为**事实**（[Export] 事实面），并负责物理、受击、死亡、收招硬直；
	///  * 该怪物自己的 AnimationTree 用 advance_expression 只读事实选动画——AI 状态与动画状态互不耦合。
	///
	/// 出招节奏：请求 → 下一安全帧提交（转向目标、锁定朝向）→ 动画 OnAttackEnd → 收招硬直 AttackConfig.AiRecovery
	/// → AI 恢复决策。受击打断出招与硬直（旧项目手感），霸体不打断。
	/// 够不够得着：每招范围由攻击动画判定盒推导（<see cref="AttackReachReader"/>），与目标受击盒求交（含高度）。
	/// 感知：m_Detector（Detector 层扫 PlayerBody，宽 2×SightRange）无目标时锁敌；被打直接锁定攻击方；
	/// 目标死亡/回收即失效，水平距离持续超出 SightRange 达 LoseTargetTime 秒即丢失（旧项目只置不清，0 = 保留旧行为）。
	/// </summary>
	public abstract partial class MonsterEntity : ActorEntity, IMonsterAiAgent
	{
		/// <summary>受击动画名（全项目标准名）：硬直时长 = 该动画长度</summary>
		private const string HurtAnimName = "hurt";

		/// <summary>死亡动画名（全项目标准名）：死亡到回收的时长 = 该动画长度</summary>
		private const string DeathAnimName = "death";

		/// <summary>
		/// 出招请求提交前，攻击段须已归位的物理帧数：OnAttackEnd 在动画推进内把段归 -1，状态机要到下一次推进才离开
		/// 攻击组；紧接着的物理帧就写回新段，单段攻击组会卡在已播完的 attack_1（同 HeroEntity 的一帧延迟）。
		/// </summary>
		private const int AttackRecommitFrames = 1;

		#region 表达式事实面（AnimationTree 边只读这些成员）

		/// <summary>水平移动意图：-1 左 / 0 无 / 1 右（AI 写入）</summary>
		[Export] public int MoveInput;

		/// <summary>当前攻击段（0 起；-1 = 不在攻击中），段序号 i ↔ 动画 attack_(i+1)</summary>
		[Export] public int AttackSegment = -1;

		/// <summary>受击硬直中（进入硬直 / 硬直结束 / 死亡三处翻转）</summary>
		[Export] public bool Hurt;

		// 死亡事实 Dead 在 ActorEntity（唯一置位点在 ReceiveHit 扣血扣到 0）。

		#endregion

		#region 场景与配置

		/// <summary>索敌区（场景子节点 m_Detector；可为空 = 只会被打后反击）</summary>
		[Export] private Area2D m_Detector;

		/// <summary>怪物配置 Id（MonsterConfig.Id），场景必填</summary>
		[Export] public int MonsterId;

		/// <summary>怪物配置</summary>
		public MonsterConfig Config { get; private set; }

		/// <inheritdoc />
		public override CombatSide Side => CombatSide.Monster;

		/// <summary>
		/// 霸体：受击照常扣血飘字，但不硬直、不击退、不打断出招（MonsterConfig.SuperArmor）。
		/// 子类可覆写做条件霸体（Boss 出招期间、低血量……）。
		/// </summary>
		protected virtual bool IsSuperArmor => Config != null && Config.SuperArmor;

		/// <summary>
		/// 【每种怪必须实现】选大脑：返回一套**全新**状态集（每次显示调用；GF.Fsm 状态实例不共享）。
		/// 标准写法 <c>return MonsterBrains.Brawler();</c>，要改某个环节就 <c>.Bind(角色, 行为)</c> 换槽。返回 null = 不建 AI。
		/// </summary>
		protected abstract MonsterAiStateSet CreateBrain();

		private MonsterAiParams m_AiParams;
		private AttackConfig[] m_Attacks = [];

		/// <summary>招式书（AI 选招与冷却；调试观测可读 <see cref="MonsterAttackBook.ReachOf"/>）</summary>
		public MonsterAttackBook Attacks { get; private set; } = MonsterAttackBook.Empty;
		private float m_HurtLen;
		private float m_DeathLen;

		#endregion

		#region 运行时状态

		/// <summary>AI 开关（默认开；关闭 = 沙包，调试用；每次 OnShow 复位为开）</summary>
		public bool AiEnabled { get; private set; } = true;

		/// <summary>当前 AI 状态名（调试/冒烟观测；无 AI 为空串）</summary>
		public string AiStateName => (m_Fsm?.CurrentState as MonsterAiState)?.StateName ?? "";

		/// <summary>收招硬直中（调试/冒烟观测）</summary>
		public bool InRecovery => m_RecoveryTime > 0f;

		/// <summary>出招中：已请求待提交 / 攻击段在播 / 收招硬直</summary>
		private bool IsBusy => AttackSegment >= 0 || m_PendingAttack >= 0 || m_RecoveryTime > 0f;

		/// <summary>能自主行动（未受控、未死亡）</summary>
		private bool CanAct => !Hurt && !Dead;

		private IFsm<IMonsterAiAgent> m_Fsm;
		private MonsterAiStateSet m_StateSet;
		private ActorEntity m_Target;
		private Vector2 m_Home;
		private int m_LastAttackerId;
		private int m_PendingAttack = -1;
		private int m_FramesSinceAttack;
		private float m_HurtTime;
		private float m_RecoveryTime;

		/// <summary>目标持续在视野外的秒数（超过 LoseTargetTime 即放弃）</summary>
		private float m_OutOfSightTime;

		/// <summary>死亡到回收的剩余秒（&lt;0 = 未死亡；归 0 即已请求回收，停在 0 不再触发）</summary>
		private float m_DeathTime = -1f;

		#endregion

		#region 生命周期

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
			m_AiParams = new MonsterAiParams
			{
				AttackDesire = Config.AttackDesire,
				AttackInterval = Config.AttackInterval,
				PatrolInterval = Config.PatrolInterval,
				PatrolIdleChance = Config.PatrolIdleChance,
				PatrolRadius = Config.PatrolRadius,
				CalmTime = Config.BehitCalmTime,
				AttackRangeSlack = Config.AttackRangeSlack,
				PaceRange = Config.PaceRange,
			};
			LoadAttacks();
			if (isNewInstance)
			{
				ConfigureDetector();
			}
		}

		public override void OnShow(object userData)
		{
			base.OnShow(userData);

			// 池复用带回脏状态：全部复位
			Hp = MaxHp;
			Dead = Hurt = false;
			MoveInput = 0;
			AttackSegment = m_PendingAttack = -1;
			m_FramesSinceAttack = AttackRecommitFrames;
			m_HurtTime = m_RecoveryTime = 0f;
			m_DeathTime = -1f;
			SetTarget(null);
			m_LastAttackerId = 0;
			Velocity = Vector2.Zero;
			Attacks.Reset(GD.Randf);

			// ShowEntity 的 userData 约定为出生点 Vector2
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
			SetTarget(null);
			base.OnHide(isShutdown, userData);
		}

		public override void _PhysicsProcess(double delta)
		{
			if (!IsShown || Config == null)
			{
				return;
			}

			float dt = (float)delta;
			if (TickDown(ref m_HurtTime, dt))
			{
				Hurt = false;
			}

			TickDown(ref m_RecoveryTime, dt);
			if (TickDown(ref m_DeathTime, dt))
			{
				GF.Entity.HideEntitySafe(this);
			}

			Attacks.Tick(dt);
			UpdateTarget(dt);
			CommitPendingAttack();
			UpdateLocomotion(dt);
		}

		/// <summary>开关 AI（调试用）。关闭：销毁状态机、清意图与请求（在播的招照常收招）；开启：从初始状态重建。</summary>
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
				return;
			}

			DestroyAi(false);
			MoveInput = 0;
			m_PendingAttack = -1;
		}

		#endregion

		#region 战斗：结算、受击、死亡

		/// <summary>结算快照：全部取 MonsterConfig（怪物无成长）。</summary>
		protected override CombatantStats GetCombatStats()
		{
			return Config == null
				? default
				: new CombatantStats(CombatSide.Monster, Config.Level, 0, Config.Def, Config.Mdef, Config.Crit,
					Config.Miss, Config.Lucky, Config.Toughness, Config.Htarget, Config.CritReduce, Config.Ar, Config.Sp);
		}

		/// <summary>
		/// 受击：记击杀者、锁定攻击方；死亡进入死亡流程；否则（非霸体、招式有击退）打断出招进入硬直
		/// （旧 BaseMonster state_hurt：击退 [0,0] 的招式不硬直）。硬直中再受击重置计时。
		/// </summary>
		protected override void OnHurt(AttackData attack, DamageResult result, Vector2 knockback, int attackerEntityId)
		{
			if (attackerEntityId != 0)
			{
				m_LastAttackerId = attackerEntityId;
			}

			// 基类"面向攻击者"：霸体出招中不转身（会打乱出招朝向）
			if (!(IsSuperArmor && AttackSegment >= 0))
			{
				base.OnHurt(attack, result, knockback, attackerEntityId);
			}

			if (Dead)
			{
				EnterDeath();
				return;
			}

			// 被打即锁定攻击方（仅当它是有效的角色实体）
			if (attackerEntityId != Id && GF.Entity.GetEntity(attackerEntityId) is ActorEntity { IsAlive: true } attacker)
			{
				SetTarget(attacker);
			}

			if (IsSuperArmor || knockback == Vector2.Zero || m_HurtLen <= 0f)
			{
				return;
			}

			InterruptAttack();
			m_HurtTime = m_HurtLen;
			Hurt = true;
			Velocity = knockback;
			PlaySound(Config.HurtSoundId);
		}

		/// <summary>死亡：打断出招、关受击盒（尸体不再挨打）、广播死亡事件，死亡动画播完回收。</summary>
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
			m_DeathTime = Mathf.Max(m_DeathLen, Mathf.Epsilon);
			PlaySound(Config.DeathSoundId);
			GF.Event.Fire(this, MonsterDiedEventArgs.Create(Id, Config.Id, Config.Rank, m_LastAttackerId, GlobalPosition));
		}

		private void SetHurtBoxEnabled(bool enabled)
		{
			// deferred：可能在物理回调内调用
			HurtBox?.SetDeferred(Area2D.PropertyName.Monitorable, enabled);
		}

		#endregion

		#region 出招

		/// <summary>装配招式（AttackConfig 里 OwnerId==自己，按 ComboIndex），每招范围从它的攻击动画判定盒推导。</summary>
		private void LoadAttacks()
		{
			m_Attacks = LoadOwnAttacks(Config.EntityId);
			if (m_Attacks.Length == 0)
			{
				Log.Warning("[MonsterEntity] {0} 没有任何招式（AttackConfig.OwnerId={1}），AI 不会出招", Config.NameCn, Config.EntityId);
			}

			MonsterAttackSpec[] specs = new MonsterAttackSpec[m_Attacks.Length];
			for (int i = 0; i < specs.Length; i++)
			{
				AttackConfig a = m_Attacks[i];
				specs[i] = new MonsterAttackSpec
				{
					Index = i,
					Priority = a.AiPriority,
					Weight = a.AiWeight,
					Reach = AttackReachReader.Read(this, a.Animation),
					Range = (a.AiRange.X, a.AiRange.Y),
					Cooldown = (a.AiCooldown.X, a.AiCooldown.Y),
					InitCooldown = (a.AiInitCooldown.X, a.AiInitCooldown.Y),
				};
				if (a.AiWeight > 0 && specs[i].Reach.IsEmpty && a.AiRange.Y <= 0f)
				{
					Log.Warning("[MonsterEntity] {0} 的招式 {1}（动画 {2}）推导不出判定盒、且 AiRange 为 0,0：AI 不会使用这招",
						Config.NameCn, a.Id, a.Animation);
				}
			}

			Attacks = new MonsterAttackBook(specs);
		}

		/// <summary>动画调 OnAttackBegin 时解析"正在播的段"对应的攻击配置。</summary>
		protected override AttackConfig GetAttackConfig()
		{
			return AttackSegment >= 0 && AttackSegment < m_Attacks.Length ? m_Attacks[AttackSegment] : null;
		}

		/// <summary>请求 → AttackSegment 事实：空闲、可行动且攻击段已归位满 AttackRecommitFrames 帧才提交。</summary>
		private void CommitPendingAttack()
		{
			if (AttackSegment >= 0)
			{
				return;
			}

			bool ready = m_FramesSinceAttack++ >= AttackRecommitFrames;
			if (m_PendingAttack < 0 || !ready || !CanAct || m_RecoveryTime > 0f)
			{
				return;
			}

			// 出招瞬间转向目标（旧 attack_target），之后整招 + 收招硬直朝向锁定
			SetFacing(System.Math.Sign(TargetBox.CenterX));
			AttackSegment = m_PendingAttack;
			m_PendingAttack = -1;
			MoveInput = 0;
			Attacks.MarkUsed(AttackSegment, GD.Randf());
			Log.Debug("[Monster] {0} 出招 段{1}", Id, AttackSegment + 1);
		}

		/// <summary>【动画方法轨道回调】收招：按本招 AiRecovery 进入收招硬直、归位攻击段。</summary>
		public override void OnAttackEnd()
		{
			if (GetAttackConfig() is { } attack)
			{
				m_RecoveryTime = Mathf.Max(0f, attack.AiRecovery);
			}

			ResetSegment();
			base.OnAttackEnd();
		}

		/// <summary>打断出招（受击/死亡）：清请求与收招硬直、归位攻击段、归还攻击包——动画被切走不会再走到 OnAttackEnd。</summary>
		private void InterruptAttack()
		{
			m_PendingAttack = -1;
			m_RecoveryTime = 0f;
			if (AttackSegment >= 0)
			{
				ResetSegment();
			}

			ReleaseAttack();
		}

		private void ResetSegment()
		{
			AttackSegment = -1;
			MoveInput = 0;
			m_FramesSinceAttack = 0;
		}

		#endregion

		#region 移动与感知

		/// <summary>重力常驻；受控保持击退速度；死亡/出招/收招硬直定身；其余按移动意图并随之转向。</summary>
		private void UpdateLocomotion(float dt)
		{
			Velocity += new Vector2(0, Config.Gravity * dt);
			if (!Hurt)
			{
				int move = Dead || IsBusy ? 0 : MoveInput;
				Velocity = new Vector2(move * Config.MoveSpeed, Velocity.Y);
				SetFacing(move);
			}

			MoveAndSlide();
		}

		/// <summary>
		/// 目标维护：失效（死亡/回收）即清；水平距离持续超出 SightRange 达 LoseTargetTime 秒即放弃（AI 随之回 Patrol 槽）；
		/// 无目标时查询索敌区重叠体（mask 只含 PlayerBody，层即敌我关系）。
		/// </summary>
		private void UpdateTarget(float dt)
		{
			if (m_Target is { IsAlive: true })
			{
				bool outOfSight = Mathf.Abs(m_Target.GlobalPosition.X - GlobalPosition.X) > Config.SightRange;
				m_OutOfSightTime = outOfSight ? m_OutOfSightTime + dt : 0f;
				if (Config.LoseTargetTime <= 0f || m_OutOfSightTime < Config.LoseTargetTime)
				{
					return;
				}

				Log.Debug("[Monster] {0} 丢失目标 {1}", Id, m_Target.Id);
			}

			SetTarget(null);
			if (Dead || m_Detector == null)
			{
				return;
			}

			foreach (Node2D body in m_Detector.GetOverlappingBodies())
			{
				if (body is ActorEntity { IsAlive: true } actor && actor != this)
				{
					SetTarget(actor);
					Log.Debug("[Monster] {0} 发现目标 {1}", Id, actor.Id);
					return;
				}
			}
		}

		private void SetTarget(ActorEntity target)
		{
			m_Target = target;
			m_OutOfSightTime = 0f;
		}

		/// <summary>目标受击盒相对自己原点（无受击盒退化为点盒；无目标/目标失效为空盒）。</summary>
		private AiBox TargetBox
		{
			get
			{
				if (m_Target is not { IsAlive: true } target)
				{
					return default;
				}

				Vector2 o = GlobalPosition;
				if (target.HurtBox == null)
				{
					return AiBox.Point(target.GlobalPosition.X - o.X, target.GlobalPosition.Y - o.Y);
				}

				Rect2 r = target.HurtBox.GetGlobalBounds();
				return new AiBox(r.Position.X - o.X, r.End.X - o.X, r.Position.Y - o.Y, r.End.Y - o.Y);
			}
		}

		/// <summary>索敌区宽度 = 2 × SightRange（高度由场景形状决定）；形状复制一份再改，避免实例间互改共享子资源。</summary>
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

		#endregion

		#region AI 宿主（IMonsterAiAgent：AI 只经这里读感知、写意图）

		private void CreateAi()
		{
			if (!AiEnabled || m_Fsm != null || Config == null || !IsShown)
			{
				return;
			}

			m_StateSet = CreateBrain();
			if (m_StateSet == null)
			{
				Log.Error("[MonsterEntity] {0} 的 CreateBrain() 返回 null，本只怪不建 AI", Config.NameCn);
				return;
			}

			m_Fsm = GF.Fsm.CreateFsm<IMonsterAiAgent>($"MonsterAI_{Id}", this, m_StateSet.ToArray());
			m_Fsm.Start(m_StateSet.Resolve(m_StateSet.InitialRole));
		}

		/// <summary>销毁状态机（关停阶段框架统一销毁，这里只丢引用）。</summary>
		private void DestroyAi(bool isShutdown)
		{
			if (m_Fsm != null && !isShutdown && !m_Fsm.IsDestroyed)
			{
				GF.Fsm.DestroyFsm(m_Fsm);
			}

			m_Fsm = null;
			m_StateSet = null;
		}

		bool IMonsterAiAgent.IsDead => Dead;
		bool IMonsterAiAgent.IsCcLocked => Hurt;
		bool IMonsterAiAgent.IsAttacking => IsBusy;
		AiBox IMonsterAiAgent.TargetBox => TargetBox;
		float IMonsterAiAgent.HomeDeltaX => GlobalPosition.X - m_Home.X;
		MonsterAiParams IMonsterAiAgent.Params => m_AiParams;
		MonsterAiStateSet IMonsterAiAgent.States => m_StateSet;

		float IMonsterAiAgent.NextRandom() => GD.Randf();

		void IMonsterAiAgent.Move(int dir) => MoveInput = Mathf.Clamp(dir, -1, 1);

		void IMonsterAiAgent.Face(int dir)
		{
			if (CanAct && !IsBusy)
			{
				SetFacing(dir);
			}
		}

		bool IMonsterAiAgent.RequestAttack(int index)
		{
			if (index < 0 || index >= m_Attacks.Length || IsBusy || !CanAct)
			{
				return false;
			}

			m_PendingAttack = index;
			MoveInput = 0;
			return true;
		}

		#endregion
	}
}
