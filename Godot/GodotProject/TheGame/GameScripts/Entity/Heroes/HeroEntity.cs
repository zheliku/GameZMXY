using System;
using GameConfig.Battle;
using GameConfig.Hero;
using GameFramework.Entity;
using GameFramework.Fsm;
using GameLogic.Battle;
using GameLogic.Battle.Stats;
using GameLogic.Entity.Body;
using GameLogic.Entity.Heroes.Body;
using GameLogic.Profile;
using Godot;
using GodotGameFramework;

namespace GameLogic.Entity.Heroes
{
	/// <summary>
	/// 英雄的表现与战斗运行时：输入、身体状态机、动画/武器层、出招，以及本次显示期间的属性汇总、生命魔法与无双。
	/// 不持有持久进度（等级、经验、金币属于档案）；显示时由 <see cref="HeroLoadout"/> 注入属性，升级后由关卡运行重新注入。
	/// </summary>
	public abstract partial class HeroEntity : ActorEntity, IHeroBody
	{
		// ---- 字段 ----

		/// <summary>输入动作名：跳跃（K）</summary>
		public static readonly StringName ActionJump = "jump";

		/// <summary>输入动作名：普攻（J）</summary>
		public static readonly StringName ActionAttack = "attack";

		private static readonly StringName ActionMoveLeft = "move_left"; // 输入动作名：左移。

		private static readonly StringName ActionMoveRight = "move_right"; // 输入动作名：右移。

		[Export] private Sprite2D m_Weapon; // 武器表现层，与身体层同网格并由动画轨道驱动。

		[Export] private Node2D m_EffectRoot; // 攻击特效层容器，由动画轨道驱动并随朝向镜像。

		[Export] private int m_HeroId; // 场景绑定的 HeroConfig 主键。

		private HeroBodyParams m_BodyParams; // 从英雄配置和动画库构建的身体参数快照。

		private IFsm<IHeroBody> m_BodyFsm; // 按物理帧推进的英雄身体状态机。

		private HeroLoadout m_Loadout; // 最近一次应用的出战装配；用于升级时移除旧的持久修正来源。

		// ---- 属性 ----

		/// <summary>武器层</summary>
		public Sprite2D Weapon => m_Weapon;

		/// <summary>攻击特效层容器</summary>
		public Node2D EffectRoot => m_EffectRoot;

		/// <summary>英雄配置 Id（对应 HeroConfig.Id）。每个英雄场景必须显式填写，缺失时本实体停用。</summary>
		public int HeroId => m_HeroId;

		/// <summary>英雄配置</summary>
		public HeroConfig Config { get; private set; }

		/// <summary>
		/// 待机小动作的动画名（角色专属，如悟空的憨笑 idle2）。
		/// 基类默认无（返回空 = 该角色没有待机小动作）。
		/// </summary>
		protected virtual string IdleFlavorAnim => "";

		/// <summary>输入层</summary>
		public HeroInput Input { get; private set; }

		/// <summary>已用跳跃次数：1=第一段跳 2=第二段跳（落地归零）</summary>
		public int JumpCount { get; private set; }

		/// <summary>连段序号（下一次起手用哪段，语义同旧 hit_count）</summary>
		public int ComboIndex { get; private set; }

		/// <summary>当前身体状态名（调试/冒烟观测：Ground / Air / Attack / Hurt / Death）</summary>
		public string BodyStateName => BodyFsm.CurrentName(m_BodyFsm);

		/// <inheritdoc />
		public override CombatSide Side => CombatSide.Hero;

		/// <summary>无双进度（本次显示期间有效，不写入存档）：普攻命中按 AttackConfig.WsGain 区间累计。</summary>
		public MusouGauge Musou { get; } = new();

		/// <summary>由具体英雄给出身体初始状态类型。</summary>
		protected abstract Type InitialBodyStateType { get; }

		// ---- 生命周期 ----

		/// <summary>读取英雄配置、招式和输入参数，并初始化实体状态。</summary>
		/// <param name="entityId">本次运行实体编号。</param>
		/// <param name="entityAssetName">实体资源路径。</param>
		/// <param name="entityGroup">所属实体组。</param>
		/// <param name="isNewInstance">是否首次创建池实例。</param>
		/// <param name="userData">本次初始化参数。</param>
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

			m_OwnAttacks = LoadOwnAttacks(Config.EntityId);
			m_BodyParams = BuildBodyParams();
			Input = new HeroInput(Config.RunDoubleTapWindow, Config.InputBufferTime);
		}

		/// <summary>显示时按出战装配重建属性、补满生命魔法、清零无双，复位战斗事实与输入并创建身体状态机。</summary>
		/// <param name="userData">出战装配 <see cref="HeroLoadout"/>（由档案构建），不能为空。</param>
		/// <exception cref="ArgumentException">参数不是本英雄的出战装配。</exception>
		public override void OnShow(object userData)
		{
			base.OnShow(userData);

			if (Config == null)
			{
				return;
			}

			// 显示参数只接受出战装配：属性来源唯一，测试场地也必须经档案构建。
			if (userData is not HeroLoadout loadout || loadout.HeroId != HeroId)
			{
				throw new ArgumentException($"英雄 {HeroId} 的显示参数必须是同一英雄的 HeroLoadout。", nameof(userData));
			}

			// 池实例可能带着上次的修正来源，先清空再按装配重建。
			Stats.Clear();
			m_Loadout = null;
			ApplyLoadout(loadout, refill: true);
			Musou.Reset(ConfigSystem.Instance.Tables.TbBattleConfig.Data.WsMax);

			// 池复用的实例带着上次的脏事实回来，一律在 OnShow 复位（见 Entity/AGENTS.md 生命周期）
			ComboIndex = 0;
			JumpCount = 0;
			m_PendingHurt = null;
			Velocity = Vector2.Zero;
			Input?.Reset();
			SetFacing(1);
			CreateBody();
		}

		/// <summary>隐藏时销毁身体状态机并执行基类清理。</summary>
		/// <param name="isShutdown">是否框架关停；此时节点可能已释放。</param>
		/// <param name="userData">本次隐藏参数。</param>
		public override void OnHide(bool isShutdown, object userData)
		{
			DestroyBody(isShutdown);
			base.OnHide(isShutdown, userData);
		}

		/// <summary>物理步长：采样输入 → 身体状态机 → 物理。AnimationPlayer（子节点）同帧稍后消费播放请求。</summary>
		/// <param name="delta">本次物理帧秒数。</param>
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

		// ---- 业务入口 ----

		/// <summary>
		/// 应用出战装配：替换成长基础值与持久修正（不触碰 Buff 等局内来源），再同步生命魔法上限。
		/// 显示时由 OnShow 调用；升级、换装后由关卡运行调用。
		/// </summary>
		/// <param name="loadout">由档案构建的出战装配。</param>
		/// <param name="refill">是否补满生命与魔法（显示、升级）；否则把当前值钳到新上限。</param>
		/// <exception cref="ArgumentNullException">装配为空。</exception>
		/// <exception cref="ArgumentException">装配属于其他英雄。</exception>
		public void ApplyLoadout(HeroLoadout loadout, bool refill)
		{
			ArgumentNullException.ThrowIfNull(loadout);
			if (loadout.HeroId != HeroId)
			{
				throw new ArgumentException($"装配属于英雄 {loadout.HeroId}，不能应用到英雄 {HeroId}。", nameof(loadout));
			}

			// 先移除上一次装配的持久来源再登记新来源，最后同步资源上限。
			loadout.ApplyTo(Stats, m_Loadout);
			m_Loadout = loadout;
			Level = loadout.Level;
			SyncVitalsToStats(refill);
		}

		// ---- 内部方法与扩展点 ----

		/// <summary>镜像英雄专属武器层与特效层。</summary>
		/// <param name="dir">朝向：1 右，-1 左。</param>
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

		/// <summary>
		/// 命中收益（英雄专属）：按本招 AttackConfig.WsGain 掷定无双值并累计。
		/// 收益规则属于英雄，不进攻击包、不进 ActorEntity（怪物没有无双值）。
		/// </summary>
		/// <param name="attack">本次攻击快照，仅在回调内使用。</param>
		/// <param name="result">目标已完成的伤害结算结果。</param>
		protected override void OnHitLanded(AttackData attack, DamageResult result)
		{
			if (ConfigSystem.Instance.Tables.TbAttackConfig.GetOrDefault(attack.AttackId) is { } config)
			{
				Musou.Add(GD.RandRange(config.WsGain.X, Mathf.Max(config.WsGain.X, config.WsGain.Y)));
			}
		}

		/// <summary>
		/// 受击：只登记事实（击退速度），何时生效由身体状态决定——
		/// 与旧项目的**有意差异**：旧英雄受击会立刻顶掉出招动画，本项目"出招不打断、收招后进硬直"
		/// （连段不被单次受击清空，见 HeroAttackState）。死亡由 ReceiveHit 置位 Dead，身体状态机下一帧进入死亡。
		/// 没有 hurt 动画（时长无从谈起）时不登记。
		/// </summary>
		/// <param name="attack">本次攻击快照，不得保留池化引用。</param>
		/// <param name="result">已完成的伤害结算结果。</param>
		/// <param name="knockback">击退速度，单位像素每秒。</param>
		/// <param name="attackerEntityId">攻击方运行编号；无来源时为零。</param>
		protected override void OnHurt(AttackData attack, DamageResult result, Vector2 knockback, int attackerEntityId)
		{
			base.OnHurt(attack, result, knockback, attackerEntityId);
			if (!Dead && m_BodyParams.HurtTime > 0f)
			{
				m_PendingHurt = knockback;
			}
		}

		/// <summary>由具体英雄给出自己的身体状态集合。</summary>
		/// <returns>本英雄使用的全部身体状态。</returns>
		protected abstract FsmState<IHeroBody>[] CreateBodyStates();

		/// <summary>每次显示重建身体状态机，从初始状态起步。</summary>
		private void CreateBody()
		{
			// 已有配置或状态机时不重复创建。
			if (Config == null || m_BodyFsm != null)
			{
				return;
			}

			m_BodyFsm = GF.Fsm.CreateFsm($"HeroBody_{Id}", this, CreateBodyStates());
			m_BodyFsm.Start(InitialBodyStateType);
		}

		/// <summary>销毁身体状态机，关停阶段仅清除本地引用。</summary>
		/// <param name="isShutdown">是否框架关停，关停时不再次访问状态机服务。</param>
		private void DestroyBody(bool isShutdown)
		{
			if (m_BodyFsm != null && !isShutdown && !m_BodyFsm.IsDestroyed)
			{
				GF.Fsm.DestroyFsm(m_BodyFsm);
			}

			m_BodyFsm = null;
		}

		/// <summary>构建身体状态机所需的配置、动画名和时长快照（扩展点：远程角色可改写移动/攻击参数）。</summary>
		/// <returns>身体状态机使用的参数快照。</returns>
		protected virtual HeroBodyParams BuildBodyParams()
		{
			// 先读取每段普攻动画及其时长，缺失动画直接记录配置错误。
			string[] attackAnims = new string[m_OwnAttacks.Length];
			float[] attackTimes = new float[m_OwnAttacks.Length];
			for (int i = 0; i < m_OwnAttacks.Length; i++)
			{
				attackAnims[i] = m_OwnAttacks[i].Animation;
				attackTimes[i] = GetAnimLength(attackAnims[i]);
				if (string.IsNullOrEmpty(attackAnims[i]))
				{
					Log.Error("[HeroEntity] AttackConfig {0} 的 Animation 列为空（连段第 {1} 段无法播放）",
						m_OwnAttacks[i].Id, i + 1);
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

		// ---- 接口实现 ----

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
		/// <returns>范围为零到一的随机值。</returns>
		/// <returns>零到一之间的随机值。</returns>
		float IHeroBody.NextRandom() => GD.Randf();

		/// <summary>提供英雄重力参数。</summary>
		float IActorBody.Gravity => m_BodyParams.Gravity;

		/// <summary>提交攻击段并装填攻击数据和起手音效。</summary>
		/// <param name="segment">英雄连段下标。</param>
		void IActorBody.BeginAttack(int segment)
		{
			AttackSegment = segment;
			ArmAttack(m_OwnAttacks[segment]);
			PlaySound(m_OwnAttacks[segment].SoundId);   // 起手音：音源查表，与旧 add_music 同点（第 0 帧）
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
