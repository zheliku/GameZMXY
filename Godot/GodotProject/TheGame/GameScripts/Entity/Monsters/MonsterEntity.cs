using System.Linq;
using System;
using GameConfig.Battle;
using GameConfig.Entity;
using GameConfig.Monster;
using GameFramework.Entity;
using GameFramework.Fsm;
using GameLogic.Battle;
using GameLogic.Entity.Body;
using GameLogic.Entity.Monsters.AI;
using GameLogic.Entity.Monsters.Body;
using GameLogic.Event;
using Godot;
using GodotGameFramework.Entity;
using GodotGameFramework;

namespace GameLogic.Entity.Monsters
{
	/// <summary>持有怪物配置、感知及身体和 AI 状态机，具体行为由直接派生的怪物声明。</summary>
	public abstract partial class MonsterEntity : ActorEntity, IMonsterAiAgent, IMonsterBody
	{
		// ---- 字段 ----

		[Export] private Area2D m_Detector; // 索敌区；为空时只会在受击后反击。

		[Export] private EntityId m_MonsterEntityId; // 场景用可读枚举绑定唯一怪物配置行。

		private MonsterAiParams m_AiParams; // 从 MonsterConfig 复制的 AI 参数快照。

		private MonsterBodyParams m_BodyParams; // 从配置和攻击动画构建的身体状态参数快照。

		/// <summary>按框架帧驱动的怪物 AI 状态机（行为类别按自己的状态类型查询）。</summary>
		protected IFsm<IMonsterAiAgent> m_AiFsm;

		/// <summary>按物理帧推进的怪物身体状态机（行为类别按自己的状态类型查询受击/收招硬直）。</summary>
		protected IFsm<IMonsterBody> m_BodyFsm;

		private ActorEntity m_Target; // 当前索敌目标；失效或超时后清除。

		private Vector2 m_Home; // 本次显示时记录的巡逻起点。

		private int m_LastAttackerId; // 最近一次造成有效受击的实体编号。

		private int m_AttackRequest = -1; // 待身体状态机提交的攻击下标，-1 表示无请求。

		private int m_MoveIntent; // AI 写入、身体状态机消费的水平移动意图。

		private bool m_RecycleRequested; // 死亡动画结束后等待物理帧回收的标记。

		private float m_OutOfSightTime; // 目标持续在视野外的秒数。

		// ---- 属性 ----

		/// <summary>怪物配置</summary>
		public MonsterConfig Config { get; private set; }

		/// <inheritdoc />
		public override CombatSide Side => CombatSide.Monster;

		/// <summary>
		/// 霸体：受击照常扣血飘字，但不硬直、不击退、不打断出招（MonsterConfig.SuperArmor）。
		/// 子类可覆写做条件霸体（Boss 出招期间、低血量……）。
		/// </summary>
		protected virtual bool IsSuperArmor => Config is { SuperArmor: true };

		/// <summary>攻击集，供 AI 判断范围与加权选招。</summary>
		public MonsterAttackBook Attacks { get; private set; } = MonsterAttackBook.Empty;

		/// <summary>收招硬直中，由具体怪物提供，AI 据此拒绝新攻击请求。</summary>
		protected abstract bool InRecovery { get; }

		/// <summary>受击硬直中（由行为类别提供；AI 的受控判定同源）</summary>
		protected abstract bool InHurt { get; }

		/// <summary>已请求攻击、出招或处于收招硬直。</summary>
		private bool IsBusy => AttackSegment >= 0 || m_AttackRequest >= 0 || InRecovery;

		/// <summary>当前是否允许 AI 自主行动。</summary>
		private bool CanAct => !Dead && !InHurt;

		/// <summary>目标受击盒相对本实体原点的范围。</summary>
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

		/// <summary>身体初始状态类型（**行为类别必须给出**）。</summary>
		protected abstract Type InitialBodyStateType { get; }

		/// <summary>AI 初始状态类型（**行为类别必须给出**）。</summary>
		protected abstract Type InitialAiStateType { get; }

		// ---- 生命周期 ----

		/// <summary>读取基础实体信息与怪物配置，并初始化攻击和身体参数。</summary>
		/// <param name="entityId">本次运行实体编号。</param>
		/// <param name="entityAssetName">实体资源路径。</param>
		/// <param name="entityGroup">所属实体组。</param>
		/// <param name="isNewInstance">是否首次创建池实例。</param>
		/// <param name="userData">本次初始化参数。</param>
		public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance,
			object userData)
		{
			base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);

			// 场景用可读枚举名绑定数值行；MonsterConfig.EntityId 必须与之一一对应。
			MonsterConfig[] matches = ConfigSystem.Instance.Tables.TbMonsterConfig.DataList
				.Where(x => x.EntityId == m_MonsterEntityId)
				.ToArray();
			if (matches.Length != 1)
			{
				throw new InvalidOperationException(
					$"MonsterConfig 必须唯一：{m_MonsterEntityId}，命中行数={matches.Length}");
			}

			Config = matches[0];
			m_AiParams = BuildAiParams();
			LoadAttacks();
			m_BodyParams = BuildBodyParams();
			if (isNewInstance)
			{
				ConfigureDetector();
			}
		}

		/// <summary>显示时复位池化状态并重建身体与 AI 状态机。</summary>
		/// <param name="userData">本次显示参数，由具体实体解释。</param>
		public override void OnShow(object userData)
		{
			base.OnShow(userData);

			// 池复用带回脏状态：属性按配置重建（怪物无成长，基础值即配置值），生命补满。
			Stats.Clear();
			Stats.SetBase(Config.Stats);
			Level = Config.Level;
			SyncVitalsToStats(refill: true);
			m_MoveIntent = 0;
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
			CreateAi();
		}

		/// <summary>隐藏时销毁状态机、清空目标并执行基类清理。</summary>
		/// <param name="isShutdown">是否框架关停；此时节点可能已释放。</param>
		/// <param name="userData">本次隐藏参数。</param>
		public override void OnHide(bool isShutdown, object userData)
		{
			DestroyAi(isShutdown);
			DestroyBody(isShutdown);
			SetTarget(null);
			base.OnHide(isShutdown, userData);
		}

		/// <summary>AI 计时与感知跟随框架帧；身体状态机和物理按固定物理帧推进。</summary>
		/// <param name="elapseSeconds">本帧游戏秒数。</param>
		/// <param name="realElapseSeconds">本帧实际秒数。</param>
		public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
		{
			base.OnUpdate(elapseSeconds, realElapseSeconds);
			if (!IsShown || Config == null)
			{
				return;
			}

			Attacks.Tick(elapseSeconds);
			UpdateTarget(elapseSeconds);
		}

		/// <summary>物理步长：身体状态机 → 物理 →（死亡时长走完）回收；本帧消费 AI 意图。</summary>
		/// <param name="delta">本次物理帧秒数。</param>
		public override void _PhysicsProcess(double delta)
		{
			if (!IsShown || Config == null)
			{
				return;
			}

			float dt = (float)delta;
			m_BodyFsm.Tick(dt);
			MoveAndSlide();

			if (m_RecycleRequested)
			{
				m_RecycleRequested = false;
				GF.Entity.HideEntitySafe(this);
			}
		}

		// ---- 内部方法与扩展点 ----

		/// <summary>
		/// 受击：记击杀者、锁定攻击方；非霸体、招式有击退且动画库有 hurt 动画（硬直时长无从谈起就不登记）时
		/// 登记受击，身体状态机下一物理帧打断出招进入硬直（旧 BaseMonster state_hurt：击退 [0,0] 的招式不硬直）。
		/// 死亡由 ReceiveHit 置位 Dead，身体状态机进入死亡。
		/// </summary>
		/// <param name="attack">本次攻击快照，不得保留池化引用。</param>
		/// <param name="result">已完成的伤害结算结果。</param>
		/// <param name="knockback">击退速度，单位像素每秒。</param>
		/// <param name="attackerEntityId">攻击方运行编号；无来源时为零。</param>
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

		/// <summary>延迟切换受击盒可监测性，允许在物理回调中安全调用。</summary>
		/// <param name="enabled">是否允许受击盒被检测。</param>
		private void SetHurtBoxEnabled(bool enabled)
		{
			// deferred：可能在物理回调内调用
			HurtBox?.SetDeferred(Area2D.PropertyName.Monitorable, enabled);
		}

		/// <summary>装配招式并从攻击动画判定盒推导每招范围。</summary>
		private void LoadAttacks()
		{
			// 先按配置装载本怪物的招式，再为 AI 建立可查询的规格。
			m_OwnAttacks = LoadOwnAttacks(Config.EntityId);
			if (m_OwnAttacks.Length == 0)
			{
				Log.Warning("[MonsterEntity] {0} 没有任何招式（AttackConfig.OwnerId={1}），AI 不会出招", Config.NameCn,
					Config.EntityId);
			}

			// 将攻击配置转换为 AI 使用的范围、权重和冷却快照；判定盒/射程由行为类别补充。
			MonsterAttackSpec[] specs = new MonsterAttackSpec[m_OwnAttacks.Length];
			for (int i = 0; i < specs.Length; i++)
			{
				specs[i] = BuildAttackSpec(i, m_OwnAttacks[i]);
			}

			Attacks = new MonsterAttackBook(specs);
		}

		/// <summary>把一条 AttackConfig 转成 AI 用法快照（扩展点：近战类别补判定盒范围，远程类别补射程）。</summary>
		/// <param name="index">招式下标（= AttackSegment）。</param>
		/// <param name="attack">该招的配置行。</param>
		/// <returns>AI 抽选与释放判定使用的规格。</returns>
		protected virtual MonsterAttackSpec BuildAttackSpec(int index, AttackConfig attack)
		{
			return new MonsterAttackSpec
			{
				Index = index,
				Weight = attack.AiWeight,
				Priority = attack.AiPriority,
				Range = (attack.AiRange.X, attack.AiRange.Y),
				Cooldown = (attack.AiCooldown.X, attack.AiCooldown.Y),
				InitCooldown = (attack.AiInitCooldown.X, attack.AiInitCooldown.Y),
			};
		}

		/// <summary>构建 AI 参数快照（**行为类别必须给出**：索敌、巡逻与出手欲望等数值来源）。</summary>
		/// <returns>AI 状态机使用的参数快照。</returns>
		protected abstract MonsterAiParams BuildAiParams();

		/// <summary>构建身体状态机所需的配置与动画时长快照（扩展点：飞行/远程怪在子类替换参数）。</summary>
		/// <returns>身体状态机使用的参数快照。</returns>
		protected virtual MonsterBodyParams BuildBodyParams()
		{
			// 读取每段攻击动画和收招硬直，供状态机按物理时间推进。
			string[] anims = new string[m_OwnAttacks.Length];
			float[] times = new float[m_OwnAttacks.Length];
			float[] recovery = new float[m_OwnAttacks.Length];
			for (int i = 0; i < m_OwnAttacks.Length; i++)
			{
				anims[i] = m_OwnAttacks[i].Animation;
				times[i] = GetAnimLength(anims[i]);
				recovery[i] = Mathf.Max(0f, m_OwnAttacks[i].AiRecovery);
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

		/// <summary>提交出招：转向目标、装填攻击包并播放起手音。</summary>
		/// <param name="index">本怪物攻击配置数组中的下标。</param>
		private void BeginAttackIndex(int index)
		{
			// 出招开始时固定朝向并记录冷却，随后装填本段结算数据。
			SetFacing(Math.Sign(TargetBox.CenterX));
			Attacks.MarkUsed(index, GD.Randf);
			AttackSegment = index;
			ArmAttack(m_OwnAttacks[index]);
			PlaySound(m_OwnAttacks[index].SoundId);
			Log.Debug("[Monster] {0} 出招 段{1}", Id, index + 1);
		}

		/// <summary>结束或打断出招并归还攻击包。</summary>
		private void EndAttackIndex()
		{
			AttackSegment = -1;
			ReleaseAttack();
		}

		/// <summary>维护目标有效性、视野超时和索敌区发现。</summary>
		/// <param name="dt">本帧游戏秒数。</param>
		private void UpdateTarget(float dt)
		{
			// 已有目标先检查存活与视野超时，保留仍在范围内的目标。
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

			// 当前目标失效后清除，再从索敌区选择第一个有效角色。
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

		/// <summary>设置当前目标并清零视野外计时。</summary>
		/// <param name="target">本次目标；空值清除索敌。</param>
		private void SetTarget(ActorEntity target)
		{
			m_Target = target;
			m_OutOfSightTime = 0f;
		}

		/// <summary>按视野范围复制并调整索敌区矩形，避免共享资源互改。</summary>
		private void ConfigureDetector()
		{
			if (m_Detector == null)
			{
				return;
			}

			// 只调整本实体私有的 Shape 副本，避免改动 PackedScene 共享资源影响其他实例。
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

		/// <summary>身体状态集合（**行为类别必须给出**：地面近战是移动/攻击/收招/受击/死亡，飞行、远程各不相同）。</summary>
		/// <returns>本怪物使用的全部身体状态。</returns>
		protected abstract FsmState<IMonsterBody>[] CreateBodyStates();

		/// <summary>创建并启动怪物身体状态机；已有状态机时保持复用。</summary>
		private void CreateBody()
		{
			if (Config == null || m_BodyFsm != null)
			{
				return;
			}

			m_BodyFsm = GF.Fsm.CreateFsm<IMonsterBody>($"MonsterBody_{Id}", this, CreateBodyStates());
			m_BodyFsm.Start(InitialBodyStateType);
		}

		/// <summary>销毁身体状态机，关停阶段仅清除本地引用。</summary>
		/// <param name="isShutdown">是否框架关停，关停时不访问状态机服务。</param>
		private void DestroyBody(bool isShutdown)
		{
			if (m_BodyFsm != null && !isShutdown && !m_BodyFsm.IsDestroyed)
			{
				GF.Fsm.DestroyFsm(m_BodyFsm);
			}

			m_BodyFsm = null;
		}

		/// <summary>AI 状态集合（**行为类别必须给出**：地面近战是巡逻/追击/站定出招，远程、飞行各不相同）。</summary>
		/// <returns>本怪物使用的全部 AI 状态。</returns>
		protected abstract FsmState<IMonsterAiAgent>[] CreateAiStates();

		/// <summary>创建并启动怪物 AI 状态机。</summary>
		private void CreateAi()
		{
			if (m_AiFsm != null || Config == null || !IsShown)
			{
				return;
			}

			m_AiFsm = GF.Fsm.CreateFsm($"MonsterAI_{Id}", this, CreateAiStates());
			m_AiFsm.Start(InitialAiStateType);
		}

		/// <summary>销毁 AI 状态机，关停阶段仅清除本地引用。</summary>
		/// <param name="isShutdown">是否框架关停，关停时不访问状态机服务。</param>
		private void DestroyAi(bool isShutdown)
		{
			if (m_AiFsm != null && !isShutdown && !m_AiFsm.IsDestroyed)
			{
				GF.Fsm.DestroyFsm(m_AiFsm);
			}

			m_AiFsm = null;
		}

		// ---- 接口实现 ----

		/// <summary>向身体状态机提供怪物身体参数快照。</summary>
		MonsterBodyParams IMonsterBody.Params => m_BodyParams;

		/// <summary>转发 AI 写入的移动意图读写。</summary>
		int IMonsterBody.MoveIntent
		{
			get => m_MoveIntent;
			set => m_MoveIntent = value;
		}

		/// <summary>取出并清除待提交的攻击请求。</summary>
		/// <returns>待提交下标；没有请求时为 -1。</returns>
		int IMonsterBody.TakeAttackRequest()
		{
			int request = m_AttackRequest;
			m_AttackRequest = -1;
			return request;
		}

		/// <summary>提供怪物身体状态使用的重力参数。</summary>
		float IActorBody.Gravity => m_BodyParams.Gravity;

		/// <summary>提交怪物招式并装填攻击包。</summary>
		/// <param name="index">要提交的怪物攻击下标。</param>
		void IActorBody.BeginAttack(int index) => BeginAttackIndex(index);

		/// <summary>结束怪物招式并归还攻击包。</summary>
		void IActorBody.EndAttack() => EndAttackIndex();

		/// <summary>播放配置指定的怪物受击音效。</summary>
		void IActorBody.PlayHurtSound() => PlaySound(Config.HurtSoundId);

		/// <summary>死亡副作用：关受击盒（尸体不再挨打）、播死亡音、广播死亡事件。</summary>
		void IActorBody.OnDied()
		{
			SetHurtBoxEnabled(false);
			PlaySound(Config.DeathSoundId);
			GF.Event.Fire(this,
				MonsterDiedEventArgs.Create(Id, Config.Id, Config.Rank, m_LastAttackerId, GlobalPosition));
		}

		/// <summary>请求在当前物理帧完成后安全回收实体。</summary>
		void IMonsterBody.RequestRecycle() => m_RecycleRequested = true;

		/// <summary>向 AI 提供实体死亡事实。</summary>
		bool IMonsterAiAgent.IsDead => Dead;

		/// <summary>向 AI 提供当前受击受控事实。</summary>
		bool IMonsterAiAgent.IsCcLocked => InHurt;

		/// <summary>向 AI 提供攻击或收招忙碌事实。</summary>
		bool IMonsterAiAgent.IsAttacking => IsBusy;

		/// <summary>提供目标受击盒相对怪物原点的范围。</summary>
		AiBox IMonsterAiAgent.TargetBox => TargetBox;

		/// <summary>提供怪物相对出生点的水平偏移。</summary>
		float IMonsterAiAgent.HomeDeltaX => GlobalPosition.X - m_Home.X;

		/// <summary>向 AI 提供怪物参数快照。</summary>
		MonsterAiParams IMonsterAiAgent.Params => m_AiParams;

		/// <summary>由 Godot 随机源提供 AI 随机值。</summary>
		/// <returns>零到一之间的随机值。</returns>
		float IMonsterAiAgent.NextRandom() => GD.Randf();

		/// <summary>设置并限制怪物的水平移动意图。</summary>
		/// <param name="dir">水平意图：负值左，正值右，零停。</param>
		void IMonsterAiAgent.Move(int dir) => m_MoveIntent = Mathf.Clamp(dir, -1, 1);

		/// <summary>仅在可行动且未忙碌时按目标方向调整朝向。</summary>
		/// <param name="dir">期望朝向：正值右，负值左。</param>
		void IMonsterAiAgent.Face(int dir)
		{
			if (CanAct && !IsBusy)
			{
				SetFacing(dir);
			}
		}

		/// <summary>校验攻击请求并登记供身体状态机下一物理帧消费。</summary>
		/// <param name="index">希望提交的攻击配置下标。</param>
		/// <returns>下标和当前状态是否允许该请求。</returns>
		bool IMonsterAiAgent.RequestAttack(int index)
		{
			if (index < 0 || index >= m_OwnAttacks.Length || IsBusy || !CanAct)
			{
				return false;
			}

			m_AttackRequest = index;
			m_MoveIntent = 0;
			return true;
		}

	}
}
