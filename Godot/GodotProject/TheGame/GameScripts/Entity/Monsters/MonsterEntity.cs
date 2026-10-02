using GameConfig.Battle;
using GameConfig.Monster;
using GameConfig.Sound;
using GameFramework.Entity;
using GameFramework.Fsm;
using GameLogic.Battle;
using GameLogic.Entity.Body;
using GameLogic.Entity.Monsters.AI;
using GameLogic.Entity.Monsters.Body;
using GameLogic.Event;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;

namespace GameLogic.Entity.Monsters
{
	/// <summary>
	/// 怪物基类（抽象）= 两台状态机的**宿主**：属性（MonsterConfig）、结算、感知、物理。
	/// 每种怪必须覆写 <see cref="CreateBrain"/> 从 <see cref="MonsterBrains"/> 选一套大脑原型。
	///
	/// 三层分工（2026-10-01 定稿；同 Unreal 的 AIController / Character / AnimBP）：
	///  * **AI 状态机**（GF.Fsm&lt;<see cref="IMonsterAiAgent"/>&gt;，框架帧）只写**意图**：Move / Face / RequestAttack；
	///  * **身体状态机**（GF.Fsm&lt;<see cref="IMonsterBody"/>&gt;，物理帧，见 Monsters/Body/）是受击、死亡、出招、
	///    收招硬直的唯一权威，把意图变成动作与物理，并请求播放动画；它不知道 AI 的存在；
	///  * **AnimationPlayer 只播放**：动画资源是纯表现数据（帧/特效/判定盒值轨道，无方法轨道），
	///    从不调用代码——动作时长 = OnInit 读动画长度，状态自己计时（A 方案：代码唯一时钟）。
	/// AI 经本类只读身体事实（<see cref="IMonsterAiAgent.IsCcLocked"/> = 身体在 Hurt、IsAttacking = 已请求/出招/收招硬直）。
	///
	/// 出招节奏：AI 请求 → 身体 Move 状态在下一物理帧提交（进入出招状态：转向目标、计冷却、装填攻击包、起手音）→
	/// 招式时长（= 动画长度）走完 → 收招硬直 AttackConfig.AiRecovery → Move。受击打断出招与硬直（旧项目手感），霸体不登记受击。
	/// 够不够得着：每招范围由攻击动画判定盒推导（<see cref="AttackReachReader"/>），与目标受击盒求交（含高度）。
	/// 感知：m_Detector（Detector 层扫 PlayerBody，宽 2×SightRange）无目标时锁敌；被打直接锁定攻击方；
	/// 目标死亡/回收即失效，水平距离持续超出 SightRange 达 LoseTargetTime 秒即丢失（0 = 保留旧项目"只置不清"）。
	/// </summary>
	public abstract partial class MonsterEntity : ActorEntity, IMonsterAiAgent, IMonsterBody
	{
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
		private MonsterBodyParams m_BodyParams;

		/// <summary>招式书（AI 选招与冷却；调试观测可读 <see cref="MonsterAttackBook.ReachOf"/>）</summary>
		public MonsterAttackBook Attacks { get; private set; } = MonsterAttackBook.Empty;

		#endregion

		#region 运行时状态

		/// <summary>AI 开关（默认开；关闭 = 沙包，调试用；每次 OnShow 复位为开）</summary>
		public bool AiEnabled { get; private set; } = true;

		/// <summary>当前 AI 状态名（调试/冒烟观测；无 AI 为空串）</summary>
		public string AiStateName => (m_AiFsm?.CurrentState as MonsterAiState)?.StateName ?? "";

		/// <summary>当前身体状态名（调试/冒烟观测：Move / Attack / Recovery / Hurt / Death）</summary>
		public string BodyStateName => BodyFsm.CurrentName(m_BodyFsm);

		/// <summary>移动意图（AI 写入，身体执行；调试观测）</summary>
		public int MoveIntent { get; private set; }

		/// <summary>收招硬直中（调试/冒烟观测）</summary>
		public bool InRecovery => BodyFsm.IsIn<IMonsterBody, MonsterRecoveryState>(m_BodyFsm);

		/// <summary>受击硬直中（调试/冒烟观测；AI 的受控判定同源）</summary>
		public bool InHurt => BodyFsm.IsIn<IMonsterBody, MonsterHurtState>(m_BodyFsm);

		/// <summary>出招中：已请求待提交 / 攻击段在播 / 收招硬直</summary>
		private bool IsBusy => AttackSegment >= 0 || m_AttackRequest >= 0 || InRecovery;

		/// <summary>能自主行动（未受控、未死亡）</summary>
		private bool CanAct => !Dead && !InHurt;

		private IFsm<IMonsterAiAgent> m_AiFsm;
		private IFsm<IMonsterBody> m_BodyFsm;
		private MonsterAiStateSet m_StateSet;
		private ActorEntity m_Target;
		private Vector2 m_Home;
		private int m_LastAttackerId;
		private int m_AttackRequest = -1;
		private bool m_RecycleRequested;

		/// <summary>目标持续在视野外的秒数（超过 LoseTargetTime 即放弃）</summary>
		private float m_OutOfSightTime;

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
			m_BodyParams = BuildBodyParams();
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
			Dead = false;
			MoveIntent = 0;
			AttackSegment = m_AttackRequest = -1;
			m_PendingHurt = null;
			m_RecycleRequested = false;
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

			CreateBody();
			AiEnabled = true;
			CreateAi();
		}

		public override void OnHide(bool isShutdown, object userData)
		{
			DestroyAi(isShutdown);
			DestroyBody(isShutdown);
			SetTarget(null);
			base.OnHide(isShutdown, userData);
		}

		/// <summary>物理步长：感知 → 身体状态机 → 物理 →（死亡时长走完）回收。AI 在框架帧写意图，这里只消费。</summary>
		public override void _PhysicsProcess(double delta)
		{
			if (!IsShown || Config == null)
			{
				return;
			}

			float dt = (float)delta;
			Attacks.Tick(dt);
			UpdateTarget(dt);
			BodyFsm.Tick(m_BodyFsm, dt);
			MoveAndSlide();

			if (m_RecycleRequested)
			{
				m_RecycleRequested = false;
				GF.Entity.HideEntitySafe(this);
			}
		}

		/// <summary>开关 AI（调试用）。关闭：销毁 AI 状态机、清意图与请求（在播的招照常收招）；开启：从初始状态重建。</summary>
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
			MoveIntent = 0;
			m_AttackRequest = -1;
		}

		#endregion

		#region 战斗：结算、受击、死亡

		/// <summary>结算快照：全部取 MonsterConfig（怪物无成长）。</summary>
		protected override CombatantStats GetCombatStats()
		{
			return Config == null
				? default
				: new CombatantStats(CombatSide.Monster, Config.Level, 0, Config.Def, Config.Mdef, Config.Crit,
					Config.Miss, Config.Lucky, Config.Toughness, Config.Htarget, Config.CritReduce, Config.Ar,
					Config.Sp);
		}

		/// <summary>
		/// 受击：记击杀者、锁定攻击方；非霸体、招式有击退且动画库有 hurt 动画（硬直时长无从谈起就不登记）时
		/// 登记受击，身体状态机下一物理帧打断出招进入硬直（旧 BaseMonster state_hurt：击退 [0,0] 的招式不硬直）。
		/// 死亡由 ReceiveHit 置位 Dead，身体状态机进入死亡。
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
				return;
			}

			// 被打即锁定攻击方（仅当它是有效的角色实体）
			if (attackerEntityId != Id &&
			    GF.Entity.GetEntity(attackerEntityId) is ActorEntity { IsAlive: true } attacker)
			{
				SetTarget(attacker);
			}

			if (IsSuperArmor || knockback == Vector2.Zero || m_BodyParams.HurtTime <= 0f)
			{
				return;
			}

			m_PendingHurt = knockback;
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
			OwnAttacks = LoadOwnAttacks(Config.EntityId);
			if (OwnAttacks.Length == 0)
			{
				Log.Warning("[MonsterEntity] {0} 没有任何招式（AttackConfig.OwnerId={1}），AI 不会出招", Config.NameCn,
					Config.EntityId);
			}

			MonsterAttackSpec[] specs = new MonsterAttackSpec[OwnAttacks.Length];
			for (int i = 0; i < specs.Length; i++)
			{
				AttackConfig a = OwnAttacks[i];
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

		/// <summary>配置快照：表数值 + 每招动画名、时长（= 动画长度）与收招硬直。</summary>
		private MonsterBodyParams BuildBodyParams()
		{
			string[] anims = new string[OwnAttacks.Length];
			float[] times = new float[OwnAttacks.Length];
			float[] recovery = new float[OwnAttacks.Length];
			for (int i = 0; i < OwnAttacks.Length; i++)
			{
				anims[i] = OwnAttacks[i].Animation;
				times[i] = GetAnimLength(anims[i]);
				recovery[i] = Mathf.Max(0f, OwnAttacks[i].AiRecovery);
			}

			return new MonsterBodyParams
			{
				MoveSpeed = Config.MoveSpeed,
				Gravity = Config.Gravity,
				HurtTime = GetAnimLength(MonsterAnims.Hurt),
				DeathTime = GetAnimLength(MonsterAnims.Death),
				AttackAnims = anims,
				AttackTimes = times,
				AttackRecovery = recovery,
			};
		}

		/// <summary>出招提交的副作用：转向目标、计入冷却、装填攻击包、播起手音（音源查 AttackConfig.SoundId）。</summary>
		private void BeginAttackIndex(int index)
		{
			SetFacing(System.Math.Sign(TargetBox.CenterX));
			Attacks.MarkUsed(index, GD.Randf());
			AttackSegment = index;
			ArmAttack(OwnAttacks[index]);
			PlaySound(OwnAttacks[index].SoundId);
			Log.Debug("[Monster] {0} 出招 段{1}", Id, index + 1);
		}

		/// <summary>收招/打断：归还攻击包、攻击段归 -1。</summary>
		private void EndAttackIndex()
		{
			AttackSegment = -1;
			ReleaseAttack();
		}

		#endregion

		#region 感知

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

		#region 身体状态机宿主（IMonsterBody：身体状态只经这里读写；公共成员由 ActorEntity 提供）

		private void CreateBody()
		{
			if (Config == null || m_BodyFsm != null)
			{
				return;
			}

			m_BodyFsm = GF.Fsm.CreateFsm<IMonsterBody>($"MonsterBody_{Id}", this,
				new MonsterMoveState(), new MonsterAttackState(), new MonsterRecoveryState(), new MonsterHurtState(),
				new MonsterDeathState());
			m_BodyFsm.Start<MonsterMoveState>();
		}

		/// <summary>销毁身体状态机（关停阶段框架统一销毁，这里只丢引用）。</summary>
		private void DestroyBody(bool isShutdown)
		{
			if (m_BodyFsm != null && !isShutdown && !m_BodyFsm.IsDestroyed)
			{
				GF.Fsm.DestroyFsm(m_BodyFsm);
			}

			m_BodyFsm = null;
		}

		MonsterBodyParams IMonsterBody.Params => m_BodyParams;

		int IMonsterBody.MoveIntent
		{
			get => MoveIntent;
			set => MoveIntent = value;
		}

		int IMonsterBody.TakeAttackRequest()
		{
			int request = m_AttackRequest;
			m_AttackRequest = -1;
			return request;
		}

		float IActorBody.Gravity => m_BodyParams.Gravity;

		void IActorBody.BeginAttack(int index) => BeginAttackIndex(index);

		void IActorBody.EndAttack() => EndAttackIndex();

		void IActorBody.PlayHurtSound() => PlaySound(Config.HurtSoundId);

		/// <summary>死亡副作用：关受击盒（尸体不再挨打）、播死亡音、广播死亡事件。</summary>
		void IActorBody.OnDied()
		{
			SetHurtBoxEnabled(false);
			PlaySound(Config.DeathSoundId);
			GF.Event.Fire(this,
				MonsterDiedEventArgs.Create(Id, Config.Id, Config.Rank, m_LastAttackerId, GlobalPosition));
		}

		void IMonsterBody.RequestRecycle() => m_RecycleRequested = true;

		#endregion

		#region AI 宿主（IMonsterAiAgent：AI 只经这里读感知与身体事实、写意图）

		private void CreateAi()
		{
			if (!AiEnabled || m_AiFsm != null || Config == null || !IsShown)
			{
				return;
			}

			m_StateSet = CreateBrain();
			if (m_StateSet == null)
			{
				Log.Error("[MonsterEntity] {0} 的 CreateBrain() 返回 null，本只怪不建 AI", Config.NameCn);
				return;
			}

			m_AiFsm = GF.Fsm.CreateFsm<IMonsterAiAgent>($"MonsterAI_{Id}", this, m_StateSet.ToArray());
			m_AiFsm.Start(m_StateSet.Resolve(m_StateSet.InitialRole));
		}

		/// <summary>销毁 AI 状态机（关停阶段框架统一销毁，这里只丢引用）。</summary>
		private void DestroyAi(bool isShutdown)
		{
			if (m_AiFsm != null && !isShutdown && !m_AiFsm.IsDestroyed)
			{
				GF.Fsm.DestroyFsm(m_AiFsm);
			}

			m_AiFsm = null;
			m_StateSet = null;
		}

		bool IMonsterAiAgent.IsDead => Dead;
		bool IMonsterAiAgent.IsCcLocked => InHurt;
		bool IMonsterAiAgent.IsAttacking => IsBusy;
		AiBox IMonsterAiAgent.TargetBox => TargetBox;
		float IMonsterAiAgent.HomeDeltaX => GlobalPosition.X - m_Home.X;
		MonsterAiParams IMonsterAiAgent.Params => m_AiParams;
		MonsterAiStateSet IMonsterAiAgent.States => m_StateSet;

		float IMonsterAiAgent.NextRandom() => GD.Randf();

		void IMonsterAiAgent.Move(int dir) => MoveIntent = Mathf.Clamp(dir, -1, 1);

		void IMonsterAiAgent.Face(int dir)
		{
			if (CanAct && !IsBusy)
			{
				SetFacing(dir);
			}
		}

		bool IMonsterAiAgent.RequestAttack(int index)
		{
			if (index < 0 || index >= OwnAttacks.Length || IsBusy || !CanAct)
			{
				return false;
			}

			m_AttackRequest = index;
			MoveIntent = 0;
			return true;
		}

		#endregion
	}
}
