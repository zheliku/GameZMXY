using GameConfig.Battle;
using GameConfig.Hero;
using GameConfig.Sound;
using GameFramework.Entity;
using GameFramework.Fsm;
using GameLogic.Battle;
using GameLogic.Entity.Body;
using GameLogic.Entity.Heroes.Body;
using GameLogic.Event;
using Godot;
using GodotGameFramework;

namespace GameLogic.Entity.Heroes
{
	/// <summary>
	/// 英雄基类 = 身体状态机的**宿主**（<see cref="IHeroBody"/>）：所有英雄共用；角色专属行为由具体英雄类覆写钩子
	/// （见 <see cref="WukongEntity"/>）。
	///
	/// **分工（2026-10-01 定稿，A 方案：动画纯数据、代码唯一时钟）**：
	///  * "现在在做什么"由身体状态机决定（GF.Fsm&lt;IHeroBody&gt;，物理帧驱动，状态见 Heroes/Body/：
	///    Ground / Air / Attack / Hurt / Death），状态进入/切换时经 PlayAnim/RestartAnim 请求播放；
	///  * 动画资源只含表现数据（帧/特效/判定盒值轨道，无方法轨道），从不调用代码；
	///    动作时长 = OnInit 从动画资源读长度（<see cref="HeroBodyParams"/>），状态自己计时；
	///  * 本类只做宿主该做的事：采样 Godot 输入喂给 <see cref="HeroInput"/>、物理（MoveAndSlide、
	///    落地归零跳跃次数）、结算事实登记（受击挂起、死亡置位）、出招提交/收招（BeginAttack/EndAttack）；
	///  * 每个物理帧：采样输入 → <see cref="BodyFsm.Tick{T}"/> → MoveAndSlide（状态机单入口）。
	/// 与旧项目的已知差异：旧项目按住方向即跑，本项目慢走/双击跑——有意的操作手感取舍。
	/// </summary>
	public partial class HeroEntity : ActorEntity, IHeroBody
	{
		// ---- 输入动作名（与 project.godot 的 InputMap 一一对应，改键只改那里）----

		/// <summary>输入动作名：跳跃（K）</summary>
		public static readonly StringName ActionJump = "jump";

		/// <summary>输入动作名：普攻（J）</summary>
		public static readonly StringName ActionAttack = "attack";

		/// <summary>输入动作名：左移（A）</summary>
		private static readonly StringName ActionMoveLeft = "move_left";

		/// <summary>输入动作名：右移（D）</summary>
		private static readonly StringName ActionMoveRight = "move_right";

		// ---- 场景节点引用（英雄专属层；怪物没有这些层，所以不放 ActorEntity）----

		/// <summary>武器层（场景子节点 m_Weapon，可为空）：与身体层同网格，帧由动画轨道驱动（装备外观）</summary>
		[Export] private Sprite2D m_Weapon;

		/// <summary>
		/// 攻击特效层容器（场景子节点 m_EffectRoot，可为空）：其子节点 m_Effect 是特效的 AnimatedSprite2D，
		/// 属性由动画轨道驱动；容器负责朝向镜像（scale.x = ±1，连带镜像轨道写入的 offset）——
		/// 同旧项目英雄 Action/SpecialEffect。将来怪物需要打击特效走池化特效实体（红线 6），不在身上挂层。
		/// </summary>
		[Export] private Node2D m_EffectRoot;

		/// <summary>武器层</summary>
		public Sprite2D Weapon => m_Weapon;

		/// <summary>攻击特效层容器</summary>
		public Node2D EffectRoot => m_EffectRoot;

		/// <summary>朝向变化：镜像英雄专属的武器层与特效层（身体与判定盒由基类处理）。</summary>
		protected override void OnFacingChanged(int dir)
		{
			bool mirror = dir > 0;
			if (m_Weapon != null)
			{
				m_Weapon.FlipH = mirror;
			}

			// 特效层用父容器负 scale 镜像（同旧项目 Action.scale.x = ±1）：
			// 这样特效节点上由轨道写入的 offset 会一起镜像，特效不会跑到身体另一侧。
			if (m_EffectRoot != null)
			{
				m_EffectRoot.Scale = new Vector2(mirror ? -1 : 1, 1);
			}
		}

		// ---- 配置 ----

		/// <summary>英雄配置 Id（对应 HeroConfig.Id）。每个英雄场景必须显式填写，缺失时本实体停用。</summary>
		[Export] public int HeroId;

		/// <summary>英雄配置</summary>
		public HeroConfig Config { get; private set; }

		/// <summary>
		/// 待机小动作的动画名（角色专属，如悟空的憨笑 idle2）。
		/// 基类默认无（返回空 = 该角色没有待机小动作）。
		/// </summary>
		protected virtual string IdleFlavorAnim => "";

		// ---- 身体事实（身体状态机经 IHeroBody 读写；外部只读，供 HUD/调试观测）----

		/// <summary>输入层</summary>
		public HeroInput Input { get; private set; }

		/// <summary>已用跳跃次数：1=第一段跳 2=第二段跳（落地归零）</summary>
		public int JumpCount { get; private set; }

		/// <summary>连段序号（下一次起手用哪段，语义同旧 hit_count）</summary>
		public int ComboIndex { get; private set; }

		/// <summary>当前身体状态名（调试/冒烟观测：Ground / Air / Attack / Hurt / Death）</summary>
		public string BodyStateName => BodyFsm.CurrentName(m_BodyFsm);

		private HeroBodyParams m_BodyParams;
		private IFsm<IHeroBody> m_BodyFsm;

		public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance,
			object userData)
		{
			base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);

			Config = ConfigSystem.Instance.Tables.TbHeroConfig.Get(HeroId);
			if (Config == null)
			{
				Log.Error("[HeroEntity] HeroConfig 缺失：HeroId={0}，本实体停用（物理与动画不再驱动）", HeroId);
				SetPhysicsProcess(false);
				return;
			}

			MaxHp = Config.BaseHp;
			Hp = MaxHp;
			WsMax = ConfigSystem.Instance.Tables.TbBattleConfig.Data.WsMax;
			OwnAttacks = LoadOwnAttacks(Config.EntityId);
			m_BodyParams = BuildBodyParams();
			Input = new HeroInput(Config.RunDoubleTapWindow, Config.InputBufferTime);
		}

		public override void OnShow(object userData)
		{
			base.OnShow(userData);

			// 池复用的实例带着上次的脏事实回来，一律在 OnShow 复位（见 Entity/AGENTS.md 生命周期）
			Hp = MaxHp;
			WsValue = 0;
			ComboIndex = 0;
			JumpCount = 0;
			m_PendingHurt = null;
			m_LastAttackerId = 0;
			Velocity = Vector2.Zero;
			Input?.Reset();
			SetFacing(1);
			CreateBody();

			// 进场广播初始状态（HUD 先开、英雄后生成，订阅顺序由流程保证）
			FireVitals();
		}

		public override void OnHide(bool isShutdown, object userData)
		{
			DestroyBody(isShutdown);
			base.OnHide(isShutdown, userData);
		}

		/// <summary>物理步长：采样输入 → 身体状态机 → 物理。AnimationPlayer（子节点）同帧稍后消费播放请求。</summary>
		public override void _PhysicsProcess(double delta)
		{
			if (!IsShown || Config == null)
			{
				return;
			}

			float dt = (float)delta;
			Input.Sample(dt,
				Godot.Input.IsActionPressed(ActionMoveLeft), Godot.Input.IsActionPressed(ActionMoveRight),
				Godot.Input.IsActionJustPressed(ActionMoveLeft), Godot.Input.IsActionJustPressed(ActionMoveRight),
				Godot.Input.IsActionJustPressed(ActionJump), Godot.Input.IsActionJustPressed(ActionAttack));

			BodyFsm.Tick(m_BodyFsm, dt);
			MoveAndSlide();

			// 落地归零跳跃次数。必须带 "Velocity.Y >= 0"：起跳那一帧角色可能还没离开地面，
			// 只判 IsOnFloor() 会把刚用掉的次数立刻清零，变成无限跳。
			if (IsOnFloor() && Velocity.Y >= 0)
			{
				JumpCount = 0;
			}
		}

		/// <inheritdoc />
		public override CombatSide Side => CombatSide.Hero;

		/// <summary>
		/// 英雄等级。M4 固定 1 级（HeroLevelConfig 第一行）；M6 接存档后由 GF.Archive 写入。
		/// 攻防成长 = HeroConfig.Base* + (Level-1) × Grow*。
		/// </summary>
		public int Level { get; private set; } = 1;

		/// <summary>结算快照：按等级算成长后的攻防，其余战斗属性直接取 HeroConfig。</summary>
		protected override CombatantStats GetCombatStats()
		{
			if (Config == null)
			{
				return default;
			}

			int grow = Level - 1;
			return new CombatantStats(CombatSide.Hero, Level,
				Config.BasePower + grow * Config.GrowPower,
				Config.BaseDef + grow * Config.GrowDef,
				Config.BaseMdef + grow * Config.GrowMdef,
				Config.Crit, Config.Miss, Config.Lucky, Config.Toughness, Config.Htarget, Config.CritReduce,
				Config.Ar, Config.Sp);
		}

		/// <summary>无双值（旧 WSValue）：普攻命中按 AttackConfig.WsGain 区间累计，上限 <see cref="WsMax"/>。消耗随无双技能系统加入。</summary>
		public int WsValue { get; private set; }

		/// <summary>无双值上限（BattleConfig.WsMax；HUD 无双条满值）。</summary>
		public int WsMax { get; private set; }

		/// <summary>最近伤害来源实体编号（死亡事件的击杀者；0 = 无实体来源）</summary>
		private int m_LastAttackerId;

		/// <summary>
		/// 命中收益（英雄专属）：按本招 AttackConfig.WsGain 掷定无双值并累计（上限 <see cref="WsMax"/>）。
		/// 收益规则属于英雄，不进攻击包、不进 ActorEntity（怪物没有无双值）。
		/// </summary>
		protected override void OnHitLanded(AttackData attack, DamageResult result)
		{
			if (ConfigSystem.Instance.Tables.TbAttackConfig.GetOrDefault(attack.AttackId) is { } config)
			{
				WsValue = Mathf.Min(WsValue + GD.RandRange(config.WsGain.X, Mathf.Max(config.WsGain.X, config.WsGain.Y)),
					WsMax);
				FireVitals();
			}
		}

		/// <summary>
		/// 受击：只登记事实（击退速度），何时生效由身体状态决定——
		/// 与旧项目的**有意差异**：旧英雄受击会立刻顶掉出招动画，本项目"出招不打断、收招后进硬直"
		/// （连段不被单次受击清空，见 HeroAttackState）。死亡由 ReceiveHit 置位 Dead，身体状态机下一帧进入死亡。
		/// 没有 hurt 动画（时长无从谈起）时不登记。
		/// </summary>
		protected override void OnHurt(AttackData attack, DamageResult result, Vector2 knockback, int attackerEntityId)
		{
			base.OnHurt(attack, result, knockback, attackerEntityId);
			m_LastAttackerId = attackerEntityId;
			if (!Dead && m_BodyParams.HurtTime > 0f)
			{
				m_PendingHurt = knockback;
			}

			FireVitals();
		}

		// ---- 身体状态机 ----

		/// <summary>每次显示重建身体状态机（池复用 = 全新状态，从 Ground 起步）。</summary>
		private void CreateBody()
		{
			if (Config == null || m_BodyFsm != null)
			{
				return;
			}

			m_BodyFsm = GF.Fsm.CreateFsm<IHeroBody>($"HeroBody_{Id}", this,
				new HeroGroundState(), new HeroAirState(), new HeroAttackState(), new HeroHurtState(),
				new HeroDeathState());
			m_BodyFsm.Start<HeroGroundState>();
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

		/// <summary>
		/// 配置快照：表数值 + 动作时长（= 动画长度，OnInit 读动画资源——动画纯数据、代码唯一时钟）
		/// + 普攻段动画名（AttackConfig.Animation）。动画名只来自全项目标准名（HeroAnims）、
		/// 角色覆写（IdleFlavorAnim）与表——基类不出现角色专属动画名。
		/// </summary>
		private HeroBodyParams BuildBodyParams()
		{
			string[] attackAnims = new string[OwnAttacks.Length];
			float[] attackTimes = new float[OwnAttacks.Length];
			for (int i = 0; i < OwnAttacks.Length; i++)
			{
				attackAnims[i] = OwnAttacks[i].Animation;
				attackTimes[i] = GetAnimLength(attackAnims[i]);
				if (string.IsNullOrEmpty(attackAnims[i]))
				{
					Log.Error("[HeroEntity] AttackConfig {0} 的 Animation 列为空（连段第 {1} 段无法播放）",
						OwnAttacks[i].Id, i + 1);
				}
			}

			string emote = IdleFlavorAnim;
			return new HeroBodyParams
			{
				WalkSpeed = Config.WalkSpeed,
				RunSpeed = Config.RunSpeed,
				JumpSpeed = Config.JumpSpeed,
				Gravity = Config.Gravity,
				JumpCountMax = Config.JumpCountMax,
				HurtTime = GetAnimLength(HeroAnims.Hurt),
				EmoteAnim = emote,
				EmoteTime = string.IsNullOrEmpty(emote) ? 0f : GetAnimLength(emote),
				EmoteDelayMin = Config.IdleEmoteDelay.X,
				EmoteDelayMax = Config.IdleEmoteDelay.Y,
				AttackAnims = attackAnims,
				AttackTimes = attackTimes,
			};
		}

		// ---- IHeroBody（英雄专属成员；公共成员由 ActorEntity 提供/钩子在此实现）----

		HeroBodyParams IHeroBody.Params => m_BodyParams;
		bool IHeroBody.OnFloor => IsOnFloor();

		int IHeroBody.JumpCount
		{
			get => JumpCount;
			set => JumpCount = value;
		}

		int IHeroBody.ComboIndex
		{
			get => ComboIndex;
			set => ComboIndex = value;
		}

		float IHeroBody.NextRandom() => GD.Randf();

		float IActorBody.Gravity => m_BodyParams.Gravity;

		void IActorBody.BeginAttack(int segment)
		{
			AttackSegment = segment;
			ArmAttack(OwnAttacks[segment]);
			PlaySound(OwnAttacks[segment].SoundId);   // 起手音：音源查表，与旧 add_music 同点（第 0 帧）
		}

		void IActorBody.EndAttack()
		{
			AttackSegment = -1;
			ReleaseAttack();
		}

		void IActorBody.PlayHurtSound() => PlaySound(Config.HurtSoundId);

		/// <summary>死亡副作用：播死亡音、广播死亡事件（同 MonsterEntity 的 MonsterDiedEventArgs 模式）。</summary>
		void IActorBody.OnDied()
		{
			PlaySound(Config.DeathSoundId);
			GF.Event.Fire(this, HeroDiedEventArgs.Create(Id, m_LastAttackerId));
		}

		/// <summary>恢复生命后广播状态（HUD 血条）。</summary>
		public override void Heal(int value)
		{
			base.Heal(value);
			if (!Dead && value > 0)
			{
				FireVitals();
			}
		}

		/// <summary>广播生命/无双/等级快照（HUD 订阅 HeroVitalsChangedEventArgs；只携带值）。</summary>
		private void FireVitals()
		{
			GF.Event.Fire(this, HeroVitalsChangedEventArgs.Create(Id, Hp, MaxHp, Level, WsValue, WsMax));
		}
	}
}
