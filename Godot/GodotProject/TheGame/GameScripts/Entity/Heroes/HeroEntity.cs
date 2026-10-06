using GameConfig.Battle;
using GameConfig.Hero;
using GameConfig.Sound;
using GameFramework.Entity;
using GameFramework.Fsm;
using GameLogic.Battle;
using GameLogic.Entity.Body;
using GameLogic.Entity.Heroes.Body;
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

		private static readonly StringName ActionMoveLeft = "move_left"; // 输入动作名：左移。

		private static readonly StringName ActionMoveRight = "move_right"; // 输入动作名：右移。

		// ---- 场景节点引用（英雄专属层；怪物没有这些层，所以不放 ActorEntity）----

		[Export] private Sprite2D m_Weapon; // 武器表现层，与身体层同网格并由动画轨道驱动。

		[Export] private Node2D m_EffectRoot; // 攻击特效层容器，由动画轨道驱动并随朝向镜像。

		/// <summary>武器层</summary>
		public Sprite2D Weapon => m_Weapon;

		/// <summary>攻击特效层容器</summary>
		public Node2D EffectRoot => m_EffectRoot;

		/// <summary>朝向变化：镜像英雄专属的武器层与特效层（身体与判定盒由基类处理）。</summary>
		/// <summary>镜像英雄专属武器层与特效层。</summary>
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

		private HeroBodyParams m_BodyParams; // 从英雄配置和动画库构建的身体参数快照。
		private IFsm<IHeroBody> m_BodyFsm; // 按物理帧推进的英雄身体状态机。

		/// <summary>读取英雄配置、招式和输入参数，并初始化实体状态。</summary>
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

		/// <summary>显示时复位英雄战斗事实、输入，并创建身体状态机。</summary>
		public override void OnShow(object userData)
		{
			base.OnShow(userData);

			// 池复用的实例带着上次的脏事实回来，一律在 OnShow 复位（见 Entity/AGENTS.md 生命周期）
			Hp = MaxHp;
			WsValue = 0;
			ComboIndex = 0;
			JumpCount = 0;
			m_PendingHurt = null;
			Velocity = Vector2.Zero;
			Input?.Reset();
			SetFacing(1);
			CreateBody();
		}

		/// <summary>隐藏时销毁身体状态机并执行基类清理。</summary>
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
		/// 英雄等级。当前战斗固定 1 级；成长与存档尚未接入。
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

		/// <summary>无双值（旧 WSValue）：普攻命中按 AttackConfig.WsGain 区间累计。</summary>
		public int WsValue { get; private set; }

		/// <summary>无双值上限（旧项目默认 100）。</summary>
		public int WsMax { get; private set; }

		/// <summary>
		/// 命中收益（英雄专属）：按本招 AttackConfig.WsGain 掷定无双值并累计。
		/// 收益规则属于英雄，不进攻击包、不进 ActorEntity（怪物没有无双值）。
		/// </summary>
		protected override void OnHitLanded(AttackData attack, DamageResult result)
		{
			if (ConfigSystem.Instance.Tables.TbAttackConfig.GetOrDefault(attack.AttackId) is { } config)
			{
				int gain = GD.RandRange(config.WsGain.X, Mathf.Max(config.WsGain.X, config.WsGain.Y));
				WsValue = Mathf.Clamp(WsValue + gain, 0, WsMax);
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
			if (!Dead && m_BodyParams.HurtTime > 0f)
			{
				m_PendingHurt = knockback;
			}
		}

		// ---- 身体状态机 ----

		private void CreateBody() // 每次显示重建身体状态机，从 Ground 状态起步。
		{
			// 已有配置或状态机时不重复创建。
			if (Config == null || m_BodyFsm != null)
			{
				return;
			}

			m_BodyFsm = GF.Fsm.CreateFsm<IHeroBody>($"HeroBody_{Id}", this,
				new HeroGroundState(), new HeroAirState(), new HeroAttackState(), new HeroHurtState(),
				new HeroDeathState());
			m_BodyFsm.Start<HeroGroundState>();
		}

		private void DestroyBody(bool isShutdown) // 销毁身体状态机，关停阶段仅清除本地引用。
		{
			if (m_BodyFsm != null && !isShutdown && !m_BodyFsm.IsDestroyed)
			{
				GF.Fsm.DestroyFsm(m_BodyFsm);
			}

			m_BodyFsm = null;
		}

		private HeroBodyParams BuildBodyParams() // 构建身体状态机所需的配置、动画名和时长快照。
		{
			// 先读取每段普攻动画及其时长，缺失动画直接记录配置错误。
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

			// 汇总移动、跳跃、受击和待机动作参数，形成不可变快照。
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

		/// <summary>提供身体状态机使用的参数快照。</summary>
		HeroBodyParams IHeroBody.Params => m_BodyParams;
		/// <summary>提供当前物理接地状态。</summary>
		bool IHeroBody.OnFloor => IsOnFloor();

		/// <summary>转发身体状态机对跳跃次数的读写。</summary>
		int IHeroBody.JumpCount
		{
			get => JumpCount;
			set => JumpCount = value;
		}

		/// <summary>转发身体状态机对连段序号的读写。</summary>
		int IHeroBody.ComboIndex
		{
			get => ComboIndex;
			set => ComboIndex = value;
		}

		/// <summary>向身体状态机提供 Godot 随机值。</summary>
		float IHeroBody.NextRandom() => GD.Randf();

		/// <summary>提供英雄重力参数。</summary>
		float IActorBody.Gravity => m_BodyParams.Gravity;

		/// <summary>提交攻击段并装填攻击数据和起手音效。</summary>
		void IActorBody.BeginAttack(int segment)
		{
			AttackSegment = segment;
			ArmAttack(OwnAttacks[segment]);
			PlaySound(OwnAttacks[segment].SoundId);   // 起手音：音源查表，与旧 add_music 同点（第 0 帧）
		}

		/// <summary>结束攻击段并归还攻击数据。</summary>
		void IActorBody.EndAttack()
		{
			AttackSegment = -1;
			ReleaseAttack();
		}

		/// <summary>播放英雄配置的受击音效。</summary>
		void IActorBody.PlayHurtSound() => PlaySound(Config.HurtSoundId);

		/// <summary>播放英雄配置的死亡音效。</summary>
		void IActorBody.OnDied() => PlaySound(Config.DeathSoundId);
	}
}
