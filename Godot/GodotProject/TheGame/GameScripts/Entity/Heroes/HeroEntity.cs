using GameConfig.Battle;
using GameConfig.Hero;
using GameConfig.Sound;
using GameFramework.Entity;
using GameLogic.Battle;
using Godot;
using GodotGameFramework;

namespace GameLogic.Entity.Heroes
{
	/// <summary>
	/// 英雄基类：标准英雄机制（输入、走/跑、跳、普攻连段、受击、死亡、待机小动作），
	/// 所有英雄共用；角色专属行为由具体英雄类覆写钩子（见 <see cref="WukongEntity"/>）。
	///
	/// **分工（2026-09-28 定稿，2026-09-30 审查定稿）**：
	///  * 本类维护**角色事实**，全部**单一事实源**、无镜像转抄：
	///    直接事实（MoveInput/Running/JumpCount/AttackSegment）是输入/物理写入的 [Export] 字段；
	///    事件事实（Hurt/Emoting/Dead）在状态进入/离开处翻转（Dead 在 ActorEntity，置位点在
	///    ReceiveHit 扣血扣到 0）；派生事实（Rising/Airborne）是 [Export] 计算属性（空 setter）；
	///  * 出招链路：输入边沿只做两件事——空闲时登记起手（**下一帧提交** AttackSegment 事实，
	///    给状态机留一次"离开攻击组"的推进时机）、出招中缓存按键（连击缓冲）；
	///    **起手装填 OnAttackBegin、收招推进 OnAttackEnd 由攻击动画的方法轨道调用**；
	///    连段序号每执行一段 +1（旧 hit_count），连段始终 1→2→3→4 循环；
	///    判定盒尺寸/位置/开关是动画**值轨道关键帧**（同旧项目），受击打断出招时由
	///    AnimationMixer 自动还原；
	///  * 该角色的 AnimationTree 每条边都是 advance_expression，读事实/调方法决定转移；
	///    C# 不出现"当前状态"枚举。
	///
	/// 输入采用"边沿查询"不缓存标记：按键在不能生效的时机（两段跳用完、攻击/受击中——
	/// 出招中的按键进连击缓冲，收招时消费）按规则丢弃。
	/// 与旧项目的已知差异：旧项目按住方向即跑，本项目慢走/双击跑——有意的操作手感取舍。
	/// </summary>
	public partial class HeroEntity : ActorEntity
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

		/// <summary>
		/// 受击动画名（全项目标准名，所有角色一致）。受击硬直时长 = 该动画长度，
		/// 所以基类需要这一个标准名；其余动画名一律只出现在各角色的动画库与状态机资源里。
		/// </summary>
		private const string HurtAnimName = "hurt";

		// ---- 场景节点引用（英雄专属层；怪物没有这些层，所以不放 ActorEntity）----

		/// <summary>武器层（场景子节点 m_Weapon，可为空）：与身体层同网格，帧由动画轨道驱动（装备外观）</summary>
		[Export] private Sprite2D m_Weapon;

		/// <summary>
		/// 攻击特效层容器（场景子节点 m_EffectRoot，可为空）：其子节点 m_Effect 是特效的 AnimatedSprite2D，
		/// 属性由动画轨道驱动；容器负责朝向镜像（scale.x = ±1，连带镜像轨道写入的 offset）——
		/// 同旧项目英雄 Action/SpecialEffect。旧项目怪物没有自带特效层（受击/保护特效是 Global.add_* 另生成的节点），
		/// 将来怪物需要打击特效走池化特效实体（红线 6），不在身上挂层。
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

		// ---- 表达式事实面：AnimationTree 的每条边只读这里的成员 ----
		// 命名不带 State/m_ 前缀：它们是 .tres 里的标识符，必须保持可读
		// （根规范 §5.1 的 m_ 约定针对场景绑定的节点引用字段）。
		// 表达式只能看见 [Export] 成员：直接事实/事件事实是导出**字段**（输入/物理/事件写入），
		// 派生事实是导出**计算属性**（getter 实时算，见下方 Rising/Airborne 的空 setter 说明）。
		// 只加"角色自己的属性"，禁止把动画名、状态序号塞进来。

		/// <summary>水平输入：-1 左 / 0 无 / 1 右</summary>
		[Export] public int MoveInput;

		/// <summary>跑步档（同一方向键在 RunDoubleTapWindow 内二次按下进入，松手退出）</summary>
		[Export] public bool Running;

		/// <summary>已用跳跃次数：1=第一段跳 2=第二段跳（落地归零）</summary>
		[Export] public int JumpCount;

		/// <summary>当前普攻段（0 起；-1 = 不在攻击中）。段数与动画来自 AttackConfig。</summary>
		[Export] public int AttackSegment = -1;

		/// <summary>受击硬直中（进入硬直/硬直结束两处翻转；时长 = 受击动画长度）</summary>
		[Export] public bool Hurt;

		/// <summary>待机小动作中（开始/结束/离开待机三处翻转；是否存在由角色的 IdleFlavorAnim 决定）</summary>
		[Export] public bool Emoting;

		// 死亡事实 Dead 在 ActorEntity（唯一置位点在 ReceiveHit 扣血扣到 0）。

		/// <summary>
		/// 正在上升（派生事实，getter 实时计算，无同步写入）。
		/// [Export] 只是为了让 AnimationTree 表达式能读到它——Godot 不允许导出无 setter 的
		/// 只读属性（GD0103），不导出的属性对引擎又不可见，所以 setter 是**空实现**：
		/// 引擎侧（检查器/场景反序列化）对它的一切写入都是无害的无效操作。
		/// </summary>
		[Export] public bool Rising
		{
			get => Velocity.Y < 0f;
			private set { }
		}

		/// <summary>在空中（含起跳那一帧还没离地：已用跳跃次数且正在上升）。空 setter 说明见 Rising。</summary>
		[Export] public bool Airborne
		{
			get => !IsOnFloor() || (JumpCount > 0 && Rising);
			private set { }
		}

		// ---- 配置 ----

		/// <summary>英雄配置 Id（对应 HeroConfig.Id）。每个英雄场景必须显式填写，缺失时本实体停用。</summary>
		[Export] public int HeroId;

		/// <summary>英雄配置</summary>
		public HeroConfig Config { get; private set; }

		/// <summary>
		/// 待机小动作的动画名（角色专属，如悟空的憨笑 idle2）。
		/// 基类默认无（返回空 = 该角色没有待机小动作，Emoting 永不为真）。
		/// </summary>
		protected virtual string IdleFlavorAnim => "";

		/// <summary>最多跳跃次数（地面一段 + 空中N段，来自 HeroConfig.JumpCountMax）</summary>
		public int MaxJumpCount => Config?.JumpCountMax ?? 0;

		/// <summary>是否还有可用的跳跃次数</summary>
		public bool CanJump => JumpCount < MaxJumpCount;

		/// <summary>
		/// 受击请求：命中逻辑（M4）置 true；事实更新时消费（出招期间不打断，收招后进入硬直）。
		/// </summary>
		public bool HurtRequested { get; set; }

		/// <summary>
		/// 当前连段序号（0 起，语义等同旧项目 hit_count）：段末未连击时 +1（末段回 0），
		/// 空中归零。AttackSegment 是"正在播的段"，本值决定"下一段起手用哪段"。
		/// </summary>
		public int ComboIndex { get; private set; }

		// ---- 内部事实（全部通过上面的事实面暴露给状态机）----

		/// <summary>本英雄的普攻连段：AttackConfig 里 OwnerId==自己 的行，按 ComboIndex 排序</summary>
		private AttackConfig[] m_Combo = [];

		/// <summary>连击缓冲：出招中按过普攻键（收招时消费——段末推进连段，旧项目 hit_count 手感的等价实现）</summary>
		private bool m_AttackBuffered;

		/// <summary>已按下普攻、待下一帧提交的起手请求（见 UpdateAttack：一帧延迟保证状态机先离攻击组）</summary>
		private bool m_PendingAttack;

		/// <summary>本套连段起手是否站在地面（可否推进连段的前提）</summary>
		private bool m_Chainable;

		/// <summary>受击硬直剩余时间（秒）</summary>
		private float m_HurtTime;

		/// <summary>待生效的击退速度（受击登记时写入，硬直开始时施加）</summary>
		private Vector2 m_PendingKnockback;

		/// <summary>受击硬直时长（受击动画长度，OnInit 缓存）</summary>
		private float m_HurtLen;

		/// <summary>待机小动作剩余时间（秒）</summary>
		private float m_EmoteTime;

		/// <summary>本次静止站立已累计的秒数（离开待机即归零；憨笑按它排期）</summary>
		private float m_IdleTime;

		/// <summary>下一次憨笑需要连续静止的秒数（每次排期时在 IdleEmoteDelay 区间内掷出）</summary>
		private float m_NextEmoteDelay;

		/// <summary>待机小动作时长（小动作动画长度，OnInit 缓存；0 = 该角色没有）</summary>
		private float m_EmoteLen;

		/// <summary>物理累计时间（单调时钟，双击判定用）</summary>
		private double m_PhysTime;

		/// <summary>上一次点击的方向（-1/1），与 m_LastTapTime 一起用于双击判定</summary>
		private int m_LastTapDirection;
		private double m_LastTapTime;

		/// <summary>输入边沿（每物理帧刷新，当帧有效）</summary>
		private bool m_JumpPressed;
		private bool m_AttackPressed;

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
			// 普攻连段：段序号 i（0 起）对应动画 attack_<i+1>（各角色状态机按同一约定生成攻击节点）
			m_Combo = LoadOwnAttacks(Config.EntityId);
			CacheAnimLens();
		}

		public override void OnShow(object userData)
		{
			base.OnShow(userData);

			// 池复用的实例带着上次的脏事实回来，一律在 OnShow 复位（见 Entity/AGENTS.md 生命周期）。
			// 状态机不需要手动归位：事实复位后（血量回满、计时器归零……），表达式边会自行把树
			// 从任意状态（含死亡）拉回地面——"death → 地面"边就是为此存在的。
			Hp = MaxHp;
			ComboIndex = 0;
			JumpCount = 0;
			MoveInput = 0;
			Running = false;
			HurtRequested = false;
			AttackSegment = -1;
			m_AttackBuffered = false;
			m_PendingAttack = false;
			Hurt = false;
			Emoting = false;
			Dead = false;
			m_LastTapDirection = 0;
			m_LastTapTime = 0;
			m_PhysTime = 0;
			m_Chainable = false;
			m_HurtTime = 0f;
			m_EmoteTime = 0f;
			m_IdleTime = 0f;
			m_PendingKnockback = Vector2.Zero;
			Velocity = Vector2.Zero;

			if (Config != null)
			{
				m_NextEmoteDelay = NextEmoteDelay();
			}

			SetFacing(1);
		}

		/// <summary>
		/// 物理步长：读输入 → 事实更新 → 物理。本帧的物理结果由 AnimationTree（子节点，
		/// 同帧稍后推进）经表达式读到——事实是计算属性，无需单独的"同步"步骤。
		/// </summary>
		public override void _PhysicsProcess(double delta)
		{
			if (!IsShown || Config == null)
			{
				return;
			}

			float dt = (float)delta;
			m_PhysTime += dt;
			ReadInput();

			UpdateHurt(dt);
			UpdateAttack();
			UpdateJump();
			UpdateEmote(dt);
			UpdateLocomotion(dt);
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

		/// <summary>
		/// 受击表现：登记硬直请求，击退速度在硬直生效时施加。
		/// 与旧项目的**有意差异**：旧英雄受击会立刻顶掉出招动画（BaseHero.gd state_behit 同帧播放），
		/// 本项目选择"出招不打断、收招后进硬直"——连段不被单次受击清空，手感取舍，见 UpdateHurt。
		/// </summary>
		protected override void OnHurt(AttackData attack, DamageResult result, Vector2 knockback, int attackerEntityId)
		{
			base.OnHurt(attack, result, knockback, attackerEntityId);
			if (Dead)
			{
				return;   // Dead 已由 ReceiveHit 置位
			}

			HurtRequested = true;
			m_PendingKnockback = knockback;
		}

		/// <summary>
		/// 方法轨道回调（旧 add_music）：普攻起手音。音源按当前段查 AttackConfig.SoundId
		/// （触发时机在动画轨道上，音源在表里——数据各归其位）。
		/// </summary>
		public void OnAttackSwingSound()
		{
			if (AttackSegment >= 0 && AttackSegment < m_Combo.Length)
			{
				Log.Debug("[Hero] 段{0}起手音 {1}", AttackSegment + 1, m_Combo[AttackSegment].SoundId);
				PlaySound(m_Combo[AttackSegment].SoundId);
			}
		}

		/// <summary>方法轨道回调（旧 add_music(7)）：死亡语音，音源查 HeroConfig.DeathSoundId。</summary>
		public void OnDeathVoice()
		{
			PlaySound(Config?.DeathSoundId ?? SoundId.None);
		}

		// ---- 事实更新 ----

		/// <summary>受击事实：消费命中请求（出招期间不打断，收招后再进入硬直）并计时。</summary>
		private void UpdateHurt(float dt)
		{
			if (m_HurtTime > 0)
			{
				m_HurtTime = Mathf.Max(0f, m_HurtTime - dt);
				if (m_HurtTime <= 0f)
				{
					Hurt = false;
				}
			}

			if (AttackSegment >= 0)
			{
				return;
			}

			if (HurtRequested && !Dead && m_HurtLen > 0)
			{
				HurtRequested = false;
				m_PendingAttack = false;   // 受击硬直开始：起手请求随按键语义一并丢弃
				m_HurtTime = m_HurtLen;
				Hurt = true;
				Velocity = m_PendingKnockback;
				m_PendingKnockback = Vector2.Zero;
				PlaySound(Config.HurtSoundId);   // 受害者自己的受击语音（旧 BaseHero.gd:592 按 self 选音）
			}
		}

		/// <summary>
		/// 普攻段事实。**段的起止时序归动画**：起手只写事实（状态机进攻击组），装填在
		/// OnAttackBegin、收招推进在 OnAttackEnd——两者由攻击动画的方法轨道调用（旧项目
		/// frame() 回调的同构）。这里只做输入边沿：空闲起手、出招中缓存连击按键。
		/// </summary>
		private void UpdateAttack()
		{
			// 死亡打断出招：动画被切走不会再走到 OnAttackEnd，这里显式收掉（同旧项目死亡打断普攻）
			if (Dead && AttackSegment >= 0)
			{
				AttackSegment = -1;
				ReleaseAttack();
				return;
			}

			if (AttackSegment >= 0)
			{
				// 出招中：缓存连击按键，收招（动画末帧的 OnAttackEnd）时消费
				if (m_AttackPressed)
				{
					m_AttackBuffered = true;
				}

				return;
			}

			// 提交上一帧的起手请求。**必须延一帧**：OnAttackEnd 在动画推进内把 AttackSegment
			// 归 -1，状态机要到下一次推进才真正离开攻击组；若在归位后的同一帧就把新段写回 >=0，
			// 出组条件（AttackSegment < 0）永远等不到一次成立的推进——树卡死在攻击组里
			// （组内只有 +1 链边，没有 4→2 的跳转）。
			if (m_PendingAttack)
			{
				m_PendingAttack = false;
				AttackSegment = Mathf.Clamp(ComboIndex, 0, m_Combo.Length - 1);
				m_Chainable = IsOnFloor();
				Log.Debug("[Hero] 攻击段{0} 起手", AttackSegment + 1);
				return;
			}

			// 空中连段归零（旧 BaseHero.gd:270 role1 在空中时 hit_count = 0）
			if (!IsOnFloor())
			{
				ComboIndex = 0;
			}

			// 起手登记：攻击/受击中按键丢弃（受击会把 m_PendingAttack 清掉）；硬直与死亡由条件表达
			if (m_AttackPressed && !Hurt && !Dead && m_Combo.Length > 0)
			{
				m_PendingAttack = true;
			}
		}

		/// <summary>
		/// 【动画方法轨道回调】收招（动画末帧）：连击缓冲推进下一段，否则收招归位。
		/// 连段序号语义同旧 hit_count（Role1.gd do_normalhit_1..4 末尾 hit_count=1/2/3/0）：
		/// **每执行一段就 +1（hit4 打完归 0）**、跨按键保持——所以连段始终 1→2→3→4→1→…按序循环；
		/// 此前按"收招才 +1"实现，整套连打后序号错位成 2→3→4，已对齐旧项目修正。
		/// </summary>
		public override void OnAttackEnd()
		{
			int finished = AttackSegment;
			ComboIndex = (finished + 1) % m_Combo.Length;
			bool chain = m_AttackBuffered && m_Chainable && finished >= 0 && finished < m_Combo.Length - 1;
			m_AttackBuffered = false;
			if (chain)
			{
				AttackSegment = finished + 1;
				Log.Debug("[Hero] 连击 → 段{0}", AttackSegment + 1);
			}
			else
			{
				AttackSegment = -1;
				Log.Debug("[Hero] 收招 连段序号={0}", ComboIndex);
			}

			base.OnAttackEnd();
		}

		/// <summary>动画调 OnAttackBegin 时解析"正在播的段"对应的攻击配置。</summary>
		protected override AttackConfig GetAttackConfig()
		{
			return m_Combo.Length == 0 ? null : m_Combo[Mathf.Clamp(AttackSegment, 0, m_Combo.Length - 1)];
		}

		/// <summary>跳跃事实：次数与纵向速度。攻击/受击中按键丢弃；两段用完后 CanJump 自然拒绝。</summary>
		private void UpdateJump()
		{
			if (!m_JumpPressed || !CanJump || AttackSegment >= 0 || Hurt || Dead)
			{
				return;
			}

			JumpCount++;
			ComboIndex = 0;
			Velocity = new Vector2(Velocity.X, -Config.JumpSpeed);
			Log.Debug("[Hero] 起跳 JumpCount={0} vy={1:F0}", JumpCount, Velocity.Y);
		}

		/// <summary>
		/// 待机小动作事实：站在地面、无输入、不在攻击/受击/死亡时**开始累计静止时间**，
		/// 连续静止满 m_NextEmoteDelay 才播放；离开待机（跑动/起跳/挨打）即归零重计。
		/// 憨笑播完后重新掷下一次间隔，继续在同一次静止里累计。
		/// （修复 2026-09-30：旧实现按全局时钟排期，"落地/停步的瞬间恰逢排期到点"会立即憨笑。）
		/// </summary>
		private void UpdateEmote(float dt)
		{
			bool idle = IsOnFloor() && MoveInput == 0 && AttackSegment < 0 && !Hurt && !Dead && m_EmoteLen > 0;

			if (!idle)
			{
				m_EmoteTime = 0f;
				m_IdleTime = 0f;
				Emoting = false;
				return;
			}

			if (m_EmoteTime > 0)
			{
				m_EmoteTime = Mathf.Max(0f, m_EmoteTime - dt);
				if (m_EmoteTime <= 0f)
				{
					Emoting = false;
				}

				return;
			}

			m_IdleTime += dt;
			if (m_IdleTime >= m_NextEmoteDelay)
			{
				m_EmoteTime = m_EmoteLen;
				Emoting = true;
				m_IdleTime = 0f;
				m_NextEmoteDelay = NextEmoteDelay();
			}
		}

		/// <summary>
		/// 移动与物理：重力每帧施加（速度为 0 时 MoveAndSlide 不做运动检测，IsOnFloor 会闪断，
		/// 始终向下压住地面才稳定）；死亡/受击定身；地面出招定身、空中出招保留动量
		/// （旧项目 is_can_move_attack_in_sky 的等价实现）。
		/// </summary>
		private void UpdateLocomotion(float dt)
		{
			Velocity += new Vector2(0, Config.Gravity * dt);

			if (Dead)
			{
				Velocity = new Vector2(0, Velocity.Y);
			}
			else if (Hurt)
			{
				// 硬直期间保持击退速度（受击生效时写入），硬直结束回到输入控制——
				// 与旧项目一致：击退只在 hurt 动画期间生效，之后由移动逻辑接管。
			}
			else if (AttackSegment >= 0)
			{
				if (IsOnFloor())
				{
					Velocity = new Vector2(0, Velocity.Y);
				}
			}
			else
			{
				float speed = Running ? Config.RunSpeed : Config.WalkSpeed;
				Velocity = new Vector2(MoveInput * speed, Velocity.Y);
				if (MoveInput != 0)
				{
					SetFacing(MoveInput);
				}
			}

			MoveAndSlide();

			// 落地归零跳跃次数。必须带 "Velocity.Y >= 0"：起跳那一帧角色可能还没离开地面，
			// 只判 IsOnFloor() 会把刚用掉的次数立刻清零，变成无限跳。
			if (IsOnFloor() && Velocity.Y >= 0)
			{
				JumpCount = 0;
			}
		}

		private void ReadInput()
		{
			int move = 0;
			if (Input.IsActionPressed(ActionMoveLeft))
			{
				move -= 1;
			}

			if (Input.IsActionPressed(ActionMoveRight))
			{
				move += 1;
			}

			MoveInput = move;

			// 双击进入跑步档：原版是"0.3 秒内连点两次方向键后按住"进入跑步
			if (Input.IsActionJustPressed(ActionMoveLeft))
			{
				RegisterDirectionTap(-1);
			}

			if (Input.IsActionJustPressed(ActionMoveRight))
			{
				RegisterDirectionTap(1);
			}

			if (move == 0)
			{
				Running = false;
			}

			m_JumpPressed = Input.IsActionJustPressed(ActionJump);
			m_AttackPressed = Input.IsActionJustPressed(ActionAttack);
		}

		/// <summary>
		/// 方向键点击登记：同一方向在 Config.RunDoubleTapWindow 秒内二次按下 → 进入跑步档。
		/// 窗口时长来自配置表（根规范 §4.5：调参数值进表，不写裸字面量）。
		/// </summary>
		private void RegisterDirectionTap(int dir)
		{
			Running = dir == m_LastTapDirection && m_PhysTime - m_LastTapTime <= Config.RunDoubleTapWindow;
			m_LastTapDirection = dir;
			m_LastTapTime = m_PhysTime;
		}

		/// <summary>
		/// 缓存动画时长类事实：受击硬直时长、待机小动作时长。
		/// 动画名来自全项目标准名（hurt）或角色覆写（IdleFlavorAnim）；基类不出现角色专属动画名。
		/// （攻击段时长不再缓存——出招时序由动画自身的方法轨道回调，见 UpdateAttack。）
		/// </summary>
		private void CacheAnimLens()
		{
			m_HurtLen = GetAnimLength(HurtAnimName);
			m_EmoteLen = IdleFlavorAnim == "" ? 0f : GetAnimLength(IdleFlavorAnim);
		}

		/// <summary>待机小动作的随机排期间隔（HeroConfig.IdleEmoteDelay 区间内取随机）。</summary>
		private float NextEmoteDelay()
		{
			return (float)GD.RandRange(Config.IdleEmoteDelay.X, Config.IdleEmoteDelay.Y);
		}
	}
}
