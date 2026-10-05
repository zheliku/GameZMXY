using System.Collections.Generic;
using GameConfig.Battle;
using GameConfig.Entity;
using GameConfig.Sound;
using GameFramework;
using GameFramework.Entity;
using GameLogic.Battle;
using GameLogic.Config;
using GameLogic.Event;
using Godot;
using GodotGameFramework;

namespace GameLogic.Entity
{
	/// <summary>
	/// 战斗角色基类（根规范 §5.1，抽象）。英雄与怪物都从这里派生。
	/// 职责：IEntity 生命周期样板、血量、动画宿主（AnimationPlayer，显示/隐藏时启停）、
	/// **所有角色都有的**表现层（身体）与判定区、朝向、受击与死亡入口、招式装配、公共事实存储
	/// （攻击段、待生效受击、<see cref="OwnAttacks"/>）。**角色行为事实**（移动输入、跑档、跳跃次数……）
	/// 在子类，英雄专属层（武器层、特效层）在 HeroEntity。
	/// 子类必须给出结算侧别 <see cref="Side"/> 与属性快照 <see cref="GetCombatStats"/>（不给默认值，免得静默算错）。
	/// 朝向翻转的扩展点见 <see cref="OnFacingChanged"/>。
	///
	/// 动画架构（2026-10-01 定稿，A 方案：动画纯数据、代码唯一时钟）：
	///  * 子类场景挂 AnimationPlayer，其动画库**只含表现数据**：帧/特效/判定盒值轨道——**无方法轨道**，
	///    动画从不调用代码；
	///  * "现在在做什么"由子类的**身体状态机**（GF.Fsm，物理帧驱动，见 Entity/Body/）决定，
	///    状态进入/切换时经 <see cref="PlayAnim"/> / <see cref="RestartAnim"/> 请求播放；
	///  * 动作时长 = OnInit 时从动画资源**读长度**（<see cref="GetAnimLength"/>，数据），状态自己计时；
	///    音效由状态钩子（BeginAttack/PlayHurtSound/OnDied）触发，音源 ID 查表；
	///  * 判定盒窗口是动画值轨道（几何 = 数据）；将来动画中途的玩法时点用 Godot 动画标记。
	/// 本类<b>不设"阵营"字段</b>：敌我关系由物理层表达（根规范 §7）。
	/// 子节点引用走 <c>[Export]</c> + <c>m_</c> 前缀，由场景绑定
	/// （.tscn 的节点头必须声明 node_paths=PackedStringArray(...)，否则 NodePath 赋值会被忽略）。
	/// </summary>
	public abstract partial class ActorEntity : CharacterBody2D, IEntity
	{
		#region 框架属性（IEntity）

		/// <summary>实体编号</summary>
		public int Id { get; private set; }

		/// <summary>实体资源名称（PackedScene 路径）</summary>
		public string EntityAssetName { get; private set; }

		/// <summary>实体实例</summary>
		public object Handle => this;

		/// <summary>实体所属的实体组</summary>
		public IEntityGroup EntityGroup { get; private set; }

		#endregion

		// ---- 场景节点引用 ----

		[Export] private Node2D m_Body; // 身体表现层，帧由动画轨道驱动。

		[Export] private AnimationPlayer m_AnimPlayer; // 角色专属动画库，由本类直接驱动。

		[Export] private HurtBox m_HurtBox; // 受击判定区，可为空；HurtBox 持有本实体引用。

		[Export] private Area2D m_HitBox; // 攻击判定区，几何和开关完全由动画值轨道驱动。

		[Export] private Node2D m_HitBoxRoot; // 攻击判定区镜像容器，随朝向翻转动画写入的位置。

		/// <summary>身体层（Sprite2D 或 AnimatedSprite2D）</summary>
		public Node2D Body => m_Body;

		/// <summary>动画播放器</summary>
		public AnimationPlayer AnimPlayer => m_AnimPlayer;

		/// <summary>受击判定区</summary>
		public HurtBox HurtBox => m_HurtBox;

		/// <summary>攻击判定区</summary>
		public Area2D HitBox => m_HitBox;

		/// <summary>攻击判定区容器（朝向镜像；怪物 AI 推导判定盒范围时要加上它的本地位置）</summary>
		public Node2D HitBoxRoot => m_HitBoxRoot;

		/// <summary>最大生命</summary>
		public int MaxHp { get; protected set; }

		/// <summary>当前生命</summary>
		public int Hp { get; protected set; }

		/// <summary>
		/// 死亡事实。**单一事实源**：只在 ReceiveHit 扣血扣到 0 的那一刻置位、OnShow 复位；
		/// 身体状态机在下一个物理帧据此进入死亡状态（死亡优先级最高）。
		/// </summary>
		public bool Dead { get; protected set; }

		/// <summary>朝向：1 右 / -1 左。素材原始朝左，见 SetFacing 注释。</summary>
		public int Facing { get; private set; } = 1;

		/// <summary>结算侧别（选 BattleConfig 常数组；子类必须固定返回）</summary>
		public abstract CombatSide Side { get; }

		/// <summary>
		/// 已显示、可驱动：OnShow 置真、OnHide 置假。子类 _PhysicsProcess 靠它提前返回（实体隐藏/回收后
		/// 节点仍在树上，不挡会继续跑物理）；AI 等外部持有者靠它判断引用是否还有效（配合 IsInstanceValid）。
		/// （2026-09-30 自 HeroEntity/MonsterEntity 各自的 m_Active 下沉合并。）
		/// </summary>
		public bool IsShown { get; private set; }

		/// <summary>可作为目标/可交互：节点有效、已显示、未死亡（外部持有引用时用它判断是否还能用）。</summary>
		public bool IsAlive => IsInstanceValid(this) && IsShown && !Dead;

		/// <summary>
		/// 飘字锚点（世界坐标）：受击盒顶边中点——受击盒本来就按素材身形摆放，头顶位置随之而来，
		/// 不需要每个角色再写一个偏移常数。没有受击盒时退到实体原点。
		/// </summary>
		public Vector2 HeadPosition
		{
			get
			{
				if (m_HurtBox == null)
				{
					return GlobalPosition;
				}

				Rect2 bounds = m_HurtBox.GetGlobalBounds();
				return new Vector2(bounds.GetCenter().X, bounds.Position.Y);
			}
		}

		private AttackData m_ActiveAttack; // 当前招式的攻击包；出招装填、收招归还，null 表示未出招。

		private string m_RequestedAnim; // 最近一次请求播放的动画名；同名请求不重播，OnShow 时清空。

		private readonly Dictionary<string, StringName> m_AnimNames = new(); // 动画名到 StringName 的缓存，避免重复转换。

		// ---- 公共身体事实（英雄与怪物共用，宿主钩子与状态经 IActorBody 读写）----

		/// <summary>本角色的招式（AttackConfig 里 OwnerId==自己，按 ComboIndex 排序；子类 OnInit 装填）</summary>
		protected AttackConfig[] OwnAttacks = [];

		/// <summary>正在播的攻击段（0 起；-1 = 不在出招）：BeginAttack/EndAttack 维护，AI/冒烟只读</summary>
		public int AttackSegment { get; protected set; } = -1;

		/// <summary>待生效的受击（值 = 击退速度；null = 无）：OnHurt 登记（子类按门槛），身体状态生效</summary>
		protected Vector2? m_PendingHurt;

		/// <summary>有待生效的受击</summary>
		public bool HasPendingHurt => m_PendingHurt.HasValue;

		/// <summary>取出待生效受击的击退速度（取出即清除）。</summary>
		public Vector2 TakePendingHurt()
		{
			Vector2 knockback = m_PendingHurt ?? Vector2.Zero;
			m_PendingHurt = null;
			return knockback;
		}

		/// <summary>实体初始化。isNewInstance 为 true 时做一次性初始化（见 Entity/AGENTS.md 生命周期）。</summary>
		public virtual void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance,
			object userData)
		{
			Id = entityId;
			EntityAssetName = entityAssetName;
			Name = entityAssetName == null ? "ActorEntity" : $"Entity_{entityId}";
			EntityGroup = entityGroup;

			if (isNewInstance && m_HitBox != null)
			{
				// 命中回调只连一次（判定盒几何/开关全在动画轨道上，代码不再初始化形状）
				m_HitBox.AreaEntered += OnHitBoxAreaEntered;
			}

			if (m_Body == null)
			{
				Log.Error("[ActorEntity] 场景未绑定 m_Body（Sprite2D / AnimatedSprite2D）：{0}", entityAssetName);
			}

		if (m_AnimPlayer != null)
		{
			// 与实体物理同频推进：实体（父节点）先跑身体状态机并提出播放请求，AnimationPlayer（子节点）同帧消费。
			m_AnimPlayer.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Physics;
		}
		else
		{
			Log.Error("[ActorEntity] 场景未绑定 m_AnimPlayer（AnimationPlayer）：{0}", entityAssetName);
		}
		}

		/// <summary>
		/// 实体显示：启用动画播放器。可变状态每次显示都要重置（池复用会带回上次的脏状态）——子类在 base 之后复位
		/// 自己的事实并重建身体状态机，初始状态进入时请求的动画会把播放器从上次的任意动画（含死亡）拉回来。
		/// </summary>
		public virtual void OnShow(object userData)
		{
			Visible = true;
			IsShown = true;
			Dead = false;
			m_RequestedAnim = null;
			if (m_AnimPlayer != null)
			{
				m_AnimPlayer.Active = true;
			}
		}

		// ---- 动画播放与动画事件（身体状态机经宿主接口调用）----

		/// <summary>
		/// 请求播放动画：本帧已请求（或正在播）同名动画时不打断；否则 Play（从头播）。
		/// 同名不重播的判定看"最近请求"，不看播放器状态——非循环动画播完停在末帧时也算"在播"，
		/// 逐帧请求 idle/jump 这类动画不会因此重播。参数是 string：身体状态是纯 C#，不构造 Godot 的 StringName。
		/// </summary>
		public void PlayAnim(string anim)
		{
			if (m_AnimPlayer == null || string.IsNullOrEmpty(anim))
			{
				return;
			}

			if (anim == m_RequestedAnim)
			{
				return;
			}

			m_RequestedAnim = anim;
			m_AnimPlayer.Play(AnimName(anim));
		}

		/// <summary>从第 0 帧重播动画（攻击段、受击、死亡等必须从头播、即使同名也要重来的动作）。</summary>
		public void RestartAnim(string anim)
		{
			if (m_AnimPlayer == null || string.IsNullOrEmpty(anim))
			{
				return;
			}

			m_RequestedAnim = anim;
			m_AnimPlayer.Stop();
			m_AnimPlayer.Play(AnimName(anim));
		}

		private StringName AnimName(string anim) // 将动画名转换为缓存的 StringName，避免播放请求分配。
		{
			// 首次遇到动画名时建立缓存，之后直接复用。
			if (!m_AnimNames.TryGetValue(anim, out StringName name))
			{
				name = new StringName(anim);
				m_AnimNames.Add(anim, name);
			}

			return name;
		}

		/// <summary>当前（或最近一次）播放的动画名（调试/冒烟观测；从未播过为空串）。</summary>
		public string CurrentAnim => m_AnimPlayer == null ? "" : m_AnimPlayer.AssignedAnimation.ToString();

		/// <summary>动画库是否有该动画。</summary>
		protected bool HasAnim(string anim)
		{
			return m_AnimPlayer != null && m_AnimPlayer.HasAnimation(anim);
		}

		/// <summary>
		/// 实体隐藏：停动画播放器、归还攻击包。关停阶段（isShutdown=true）子节点可能已被引擎释放——
		/// 框架的 Shutdown 在场景树析构之后才补调 OnHide，此时读节点会抛 ObjectDisposedException，所以不碰节点。
		/// </summary>
		public virtual void OnHide(bool isShutdown, object userData)
		{
			IsShown = false;
			ReleaseAttack();

			if (isShutdown || !IsInstanceValid(this))
			{
				return;
			}

			if (m_AnimPlayer != null && IsInstanceValid(m_AnimPlayer))
			{
				m_AnimPlayer.Active = false;
			}

			Visible = false;
		}

		/// <summary>实体回收（下一帧执行，节点不销毁）。</summary>
		public virtual void OnRecycle()
		{
			Id = 0;
			EntityAssetName = null;
			Name = "ActorEntity (Recycled)";
			Visible = false;
			Velocity = Vector2.Zero;
		}

		/// <summary>实体轮询。角色行为不走这里——由子类 _PhysicsProcess 里的身体状态机驱动。</summary>
		public virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
		{
		}

		/// <summary>附加子实体。</summary>
		public virtual void OnAttached(IEntity childEntity, object userData)
		{
		}

		/// <summary>解除子实体。</summary>
		public virtual void OnDetached(IEntity childEntity, object userData)
		{
		}

		/// <summary>被附加到父实体。</summary>
		public virtual void OnAttachTo(IEntity parentEntity, object userData)
		{
		}

		/// <summary>从父实体解除。</summary>
		public virtual void OnDetachFrom(IEntity parentEntity, object userData)
		{
		}

		/// <summary>
		/// 按 SoundId 播放一次性音效：查 SoundConfig 拿路径与组，直接走框架的统一入口
		/// `GF.Sound.PlaySound(资源, 组名)`。代码里不出现资源路径（根规范 §4.5），路径与组都在表里。
		/// </summary>
		public void PlaySound(SoundId id)
		{
			if (id == SoundId.None)
			{
				return;
			}

			SoundConfig cfg = ConfigSystem.Instance.Tables.TbSoundConfig.GetOrDefault((int)id);
			if (cfg == null)
			{
				Log.Error("[ActorEntity] SoundConfig 缺失行：SoundId={0}", id);
				return;
			}

			GF.Sound.PlaySound(cfg.Path, cfg.Group);
		}

		/// <summary>设置朝向（0 表示不变）。翻转通用表现层（身体/判定盒容器），最后调钩子（子类镜像自己的附加层）。</summary>
		public void SetFacing(int dir)
		{
			if (dir == 0)
			{
				return;
			}

			Facing = dir > 0 ? 1 : -1;

			// 素材原始朝向是"左"：旧项目向右移动时把节点 scale.x 置 -1（BaseHero.gd:381/387），
			// 所以这里"面朝右 = 翻转"，面朝左保持原样。
			bool mirror = Facing > 0;
			if (m_Body is Sprite2D sprite)
			{
				// 网格身体层 offset 恒为 0，FlipH 即可（FlipH 不镜像 offset，已实测）
				sprite.FlipH = mirror;
			}
			else if (m_Body != null)
			{
				// 轨道驱动 offset 的身体层（怪物）：负 scale 让 offset 一起镜像（同旧 MonsterDir.scale.x = ±1）。
				// m_Body 是纯显示节点，负缩放没有物理节点的问题。
				m_Body.Scale = new Vector2(mirror ? -1 : 1, 1);
			}

			// 判定盒用父容器负 scale 镜像（同旧 base_damagebox.scale.x = ±1）：动画轨道写的是原生朝左坐标，
			// 容器一翻，判定盒位置跟着镜像——代码不碰几何。
			if (m_HitBoxRoot != null)
			{
				m_HitBoxRoot.Scale = new Vector2(mirror ? -1 : 1, 1);
			}

			OnFacingChanged(dir);
		}

		// ---- 攻击判定（M4）----

		/// <summary>
		/// 出招装填：快照攻击方属性、掷威力倍率，装填攻击包（只装受击方结算要用的数据）。
		/// 由子类宿主钩子 BeginAttack（攻击状态进入时）调用，上一招未收（连段直接推进）时先归还上一招的包。
		/// **时序与几何**：判定窗口与判定盒尺寸/位置全部是动画值轨道的关键帧（同旧项目 keyframe
		/// shape/position/disabled）——C# 只负责"这一招的数值事实"。
		/// </summary>
		protected void ArmAttack(AttackConfig attack)
		{
			ReleaseAttack();
			if (attack == null)
			{
				return;
			}

			CombatantStats stats = GetCombatStats();
			float power = stats.Power * Mathf.Lerp(attack.PowerScale.X, attack.PowerScale.Y, GD.Randf()) + attack.FlatPower;
			m_ActiveAttack = AttackData.Create(attack.Id, stats, power, attack.DamageKind, attack.Knockback, Facing,
				attack.HitProtect, attack.HitSoundId);
		}

		/// <summary>
		/// 收招（安全释放）：归还攻击包（纯 C#，状态机销毁期间也可调用）。由子类宿主钩子 EndAttack
		/// （攻击状态离开时：收招、受击打断、死亡、实体隐藏）调用。
		/// 判定盒不需要代码去关：库内每个动画都带齐判定盒值轨道（轨道完备性规则），
		/// 切到任何动画首帧就写回安全值。
		/// </summary>
		public void ReleaseAttack()
		{
			if (m_ActiveAttack != null)
			{
				ReferencePool.Release(m_ActiveAttack);
				m_ActiveAttack = null;
			}
		}

		private void OnHitBoxAreaEntered(Area2D area) // 将攻击判定交给受击方，并保证同一招不重复命中目标。
		{
			// 只处理有效攻击包和受击盒，过滤判定窗口外或无关区域。
			if (m_ActiveAttack == null || area is not HurtBox hurtBox)
			{
				return;
			}

			// 过滤自身、死亡目标和无效实体，避免产生无意义结算。
			ActorEntity target = hurtBox.OwnerEntity;
			if (target == null || target == this || target.Dead)
			{
				return;
			}

			// AttackData 负责一招一目标去重。
			if (!m_ActiveAttack.TryRegisterHit(target.GetInstanceId()))
			{
				return;
			}

			// 命中后播放音效，并将有效命中收益交给具体角色。
			DamageResult result = target.ReceiveHit(m_ActiveAttack, Id);
			if (!result.IsMiss)
			{
				PlaySound(m_ActiveAttack.HitSoundId);
				OnHitLanded(m_ActiveAttack, result);
			}
		}

		/// <summary>
		/// 受击结算：属性快照 + 攻击包交 DamageCalculator，扣血、击退、广播 DamageDealtEventArgs。
		/// 公式不写在这里（GameScripts/AGENTS.md：命中回调不写公式）。
		/// </summary>
		public DamageResult ReceiveHit(AttackData attack, int attackerEntityId)
		{
			if (Dead)
			{
				return DamageResult.Missed(attack.Kind);
			}

			BattleConfig config = ConfigSystem.Instance.Tables.TbBattleConfig.Data;
			CombatantStats defender = GetCombatStats();
			float missRoll = GD.Randf();
			float critRoll = GD.Randf();
			DamageResult result = DamageCalculator.Calculate(config, attack, defender, missRoll, critRoll);

			if (!result.IsMiss)
			{
				Hp = Mathf.Max(0, Hp - result.Damage);
				if (Hp <= 0)
				{
					Dead = true;   // 死亡事实唯一置位点
				}

				OnHurt(attack, result, DamageCalculator.KnockbackVelocity(config, attack, Side), attackerEntityId);
			}

			GF.Event.Fire(this, DamageDealtEventArgs.Create(attackerEntityId, Id, Side == CombatSide.Hero,
				result.Damage, result.IsMiss, result.IsCrit, result.Kind, HeadPosition, Hp));
			return result;
		}

		/// <summary>结算用属性快照（子类必须按自己的配置/等级填）。</summary>
		protected abstract CombatantStats GetCombatStats();

		/// <summary>
		/// 受击表现钩子（已扣血、未闪避时调用）：子类决定硬直/击退如何生效。
		/// 基类只做"面向攻击者"：击退方向与攻击方出招方向相同，受击方转身面对攻击方。
		/// </summary>
		/// <param name="knockback">击退速度 px/s（已按侧别换算，方向已含攻击方朝向）</param>
		/// <param name="attackerEntityId">攻击方实体编号（0 = 无实体来源，如 Buff/调试伤害）；怪物用它锁定仇恨</param>
		protected virtual void OnHurt(AttackData attack, DamageResult result, Vector2 knockback, int attackerEntityId)
		{
			SetFacing(-attack.Direction);
		}

		/// <summary>
		/// 命中他人钩子（未闪避时）：攻击方收益的扩展点——英雄累计无双值（AttackConfig.WsGain）、怪物将来的吸血/叠层等。
		/// 基类不实现任何收益：收益规则属于具体角色，不进攻击包、不进基类。
		/// </summary>
		protected virtual void OnHitLanded(AttackData attack, DamageResult result)
		{
		}

		/// <summary>
		/// 朝向变化钩子：基类翻转通用表现层（身体）与判定盒之后调用。
		/// 子类用它镜像自己额外的附加层（如英雄专属的武器层/特效层），不要重写翻转规则本身。
		/// </summary>
		protected virtual void OnFacingChanged(int dir)
		{
		}

		/// <summary>动画长度（秒）：动作时长的数据源（OnInit 读进参数快照，状态计时）；
		/// 动画库缺少该动画时告警并按 0 处理（状态会立即结束，问题可见）。</summary>
		protected float GetAnimLength(string animName)
		{
			if (m_AnimPlayer == null || string.IsNullOrEmpty(animName) || !m_AnimPlayer.HasAnimation(animName))
			{
				Log.Warning("[ActorEntity] {0} 的动画库缺少 {1}，相关时长按 0 处理", Name, animName);
				return 0f;
			}

			return (float)m_AnimPlayer.GetAnimation(animName).Length;
		}

		/// <summary>
		/// 装配某角色的招式：AttackConfig 里 OwnerId == owner 的行，按 ComboIndex 排序
		/// （下标 = 攻击段序号；英雄连段与怪物招式同一份数据、同一约定）。
		/// </summary>
		protected static AttackConfig[] LoadOwnAttacks(EntityId owner)
		{
			List<AttackConfig> attacks = new List<AttackConfig>();
			foreach (AttackConfig attack in ConfigSystem.Instance.Tables.TbAttackConfig.DataList)
			{
				if (attack.OwnerId == owner)
				{
					attacks.Add(attack);
				}
			}

			attacks.Sort((a, b) => a.ComboIndex.CompareTo(b.ComboIndex));
			return [.. attacks];
		}

		/// <summary>恢复生命。</summary>
		public virtual void Heal(int value)
		{
			if (Dead || value <= 0)
			{
				return;
			}

			Hp = Mathf.Min(MaxHp, Hp + value);
		}
	}
}
