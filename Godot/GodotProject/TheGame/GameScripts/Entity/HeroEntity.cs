using System.Collections.Generic;
using GameConfig.Battle;
using GameConfig.Hero;
using GameConfig.Sound;
using GameFramework.Entity;
using Godot;
using GodotGameFramework;

namespace GameLogic.Entity
{
	/// <summary>
	/// 英雄基类：标准英雄机制（输入、走/跑、跳、普攻连段、受击、死亡、待机小动作），
	/// 所有英雄共用；角色专属行为由具体英雄类覆写钩子（见 <see cref="WukongEntity"/>）。
	///
	/// **分工（2026-09-28 定稿）**：
	///  * 本类维护**角色属性**（下方"表达式事实面"字段）——输入、跑档、是否在空中、
	///    跳了几段、普攻第几段、受击/小动作/死亡是否进行中，全部是角色自己的事实；
	///  * 该角色的 AnimationTree（如 wukong_animation_tree.tres）**每条边都是
	///    advance_expression**，读这些属性决定转移；图上没有布尔参数、没有脉冲，
	///    C# 不出现任何"第几个状态/当前状态枚举"，也不判断"现在该播哪个动画"；
	///  * 帧序（_PhysicsProcess，父节点先于 AnimationTree 子节点）：读输入 → 事实计时
	///    → 物理（重力/移动/MoveAndSlide）→ 刷新表达式事实面 → AnimationTree 同帧消费。
	///
	/// 派生事实（Hurt / Emoting / Dead / Airborne / Rising）由计时器与物理状态在
	/// <see cref="SyncAnimFacts"/> 里一次算出；直接事实（MoveInput / Running / JumpCount /
	/// AttackSegment）就是本类自己的属性。
	///
	/// 输入采用"边沿查询"不缓存标记：按键在不能生效的时机（两段跳用完、攻击/受击中）当帧丢弃。
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

		// ---- 场景节点引用（英雄专属层）----

		/// <summary>武器层（场景子节点 m_Weapon，可为空）：与身体层同网格，帧由动画轨道驱动</summary>
		[Export] private Sprite2D m_Weapon;

		/// <summary>武器层。属于英雄（装备外观），怪物没有这一层，所以不放 ActorEntity</summary>
		public Sprite2D Weapon => m_Weapon;

		/// <summary>朝向变化：镜像英雄专属的武器层（身体/特效层与判定盒由基类处理）。</summary>
		protected override void OnFacingChanged(int dir)
		{
			if (m_Weapon != null)
			{
				m_Weapon.FlipH = dir > 0;
			}
		}

		// ---- 表达式事实面：AnimationTree 的每条边只读这里的字段 ----
		// 命名不带 State/m_ 前缀：它们是 .tres 里的标识符，必须保持可读
		// （AGENTS 5.1 的 m_ 约定针对场景绑定的节点引用字段）。
		// 只加"角色自己的属性"，禁止把动画名、状态序号塞进来。

		/// <summary>水平输入：-1 左 / 0 无 / 1 右</summary>
		[Export] public int MoveInput;

		/// <summary>跑步档（同一方向键在 RunDoubleTapWindow 内二次按下进入，松手退出）</summary>
		[Export] public bool Running;

		/// <summary>在空中（含起跳那一帧：已用跳跃次数且正在上升）</summary>
		[Export] public bool Airborne;

		/// <summary>正在上升（velocity.y &lt; 0）</summary>
		[Export] public bool Rising;

		/// <summary>已用跳跃次数：1=第一段跳 2=第二段跳（落地归零）</summary>
		[Export] public int JumpCount;

		/// <summary>当前普攻段（0 起；-1 = 不在攻击中）。段数与时序来自 AttackConfig。</summary>
		[Export] public int AttackSegment = -1;

		/// <summary>受击硬直中（时长 = 受击动画长度）</summary>
		[Export] public bool Hurt;

		/// <summary>待机小动作中（是否存在与时长由角色的 <see cref="IdleFlavorAnim"/> 决定）</summary>
		[Export] public bool Emoting;

		/// <summary>已死亡</summary>
		[Export] public bool Dead;

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

		/// <summary>当前段对应的攻击配置（M4 起用于伤害包）</summary>
		public AttackConfig CurrentAttack =>
			m_Combo.Length == 0 ? null : m_Combo[Mathf.Clamp(ComboIndex, 0, m_Combo.Length - 1)];

		// ---- 内部事实（全部通过上面的事实面暴露给状态机）----

		/// <summary>本英雄的普攻连段：AttackConfig 里 OwnerId==自己 的行，按 ComboIndex 排序</summary>
		private AttackConfig[] m_Combo = [];

		/// <summary>每段的最短停留时长 = max(AttackConfig.Interval, 动画时长)，LoadCombo 缓存</summary>
		private float[] m_SegLens = [];

		/// <summary>本段已播时间</summary>
		private float m_SegTime;

		/// <summary>本套连段起手是否站在地面（可否推进连段的前提）</summary>
		private bool m_Chainable;

		/// <summary>受击硬直剩余时间（秒）</summary>
		private float m_HurtTime;

		/// <summary>受击硬直时长（受击动画长度，OnInit 缓存）</summary>
		private float m_HurtLen;

		/// <summary>待机小动作剩余时间（秒）</summary>
		private float m_EmoteTime;

		/// <summary>下一次待机小动作的物理时刻</summary>
		private double m_NextEmoteAt;

		/// <summary>待机小动作时长（小动作动画长度，OnInit 缓存；0 = 该角色没有）</summary>
		private float m_EmoteLen;

		/// <summary>物理累计时间（单调时钟，双击判定/憨笑排期共用）</summary>
		private double m_PhysTime;

		/// <summary>上一次点击的方向（-1/1），与 m_LastTapTime 一起用于双击判定</summary>
		private int m_LastTapDirection;
		private double m_LastTapTime;

		/// <summary>输入边沿（每物理帧刷新，当帧有效）</summary>
		private bool m_JumpPressed;
		private bool m_AttackPressed;

		/// <summary>
		/// 是否处于"已显示、可驱动"状态：OnShow 置真、OnHide 置假。
		/// _PhysicsProcess 提前返回靠它——实体隐藏/回收后节点仍在树上，不挡会继续跑物理。
		/// </summary>
		private bool m_Active;

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
			LoadCombo();
			CacheAnimLens();
		}

		public override void OnShow(object userData)
		{
			base.OnShow(userData);

			// 池复用的实例带着上次的脏事实回来，一律在 OnShow 复位（AGENTS 4.2）。
			// 状态机不需要手动归位：事实复位后（Dead=false、Hurt=false…），
			// 表达式边会自行把树从任意状态（含死亡）拉回地面——"death → 地面"边就是为此存在的。
			Hp = MaxHp;
			ComboIndex = 0;
			JumpCount = 0;
			MoveInput = 0;
			Running = false;
			HurtRequested = false;
			Hurt = false;
			Emoting = false;
			Dead = false;
			Airborne = false;
			Rising = false;
			AttackSegment = -1;
			m_LastTapDirection = 0;
			m_LastTapTime = 0;
			m_PhysTime = 0;
			m_SegTime = 0f;
			m_Chainable = false;
			m_HurtTime = 0f;
			m_EmoteTime = 0f;
			Velocity = Vector2.Zero;

			if (Config != null)
			{
				m_NextEmoteAt = GD.RandRange(Config.IdleEmoteDelay.X, Config.IdleEmoteDelay.Y);
			}

			if (AnimTree != null)
			{
				AnimTree.Active = true;
			}

			m_Active = true;
			SetFacing(1);
		}

		public override void OnHide(bool isShutdown, object userData)
		{
			m_Active = false;

			// 关停阶段（isShutdown=true）子节点可能已被引擎释放——框架的 Shutdown 在
			// 场景树析构之后才补调 OnHide，此时读 AnimTree 会抛 ObjectDisposedException。
			// 关停时也没什么可停的（树随场景一起销毁），直接跳过。
			if (!isShutdown && AnimTree != null && GodotObject.IsInstanceValid(AnimTree))
			{
				AnimTree.Active = false;
			}

			base.OnHide(isShutdown, userData);
		}

		/// <summary>
		/// 物理步长：读输入 → 事实更新 → 物理 → 刷新表达式事实面。
		/// 本帧刷新出的事实由 AnimationTree（子节点）同帧经表达式消费。
		/// </summary>
		public override void _PhysicsProcess(double delta)
		{
			if (!m_Active || Config == null)
			{
				return;
			}

			float dt = (float)delta;
			m_PhysTime += dt;
			ReadInput();

			UpdateHurt(dt);
			UpdateAttack(dt);
			UpdateJump();
			UpdateEmote(dt);
			UpdateLocomotion(dt);

			SyncAnimFacts();
		}

		/// <summary>
		/// 受击入口：M4 命中逻辑调用。扣血 + 转向由基类完成；这里只登记命中请求，
		/// 由 <see cref="UpdateHurt"/> 决定生效时机（出招期间不打断，收招后进硬直——旧项目规则）。
		/// </summary>
		public override void TakeDamage(int damage, int attackerFacing)
		{
			base.TakeDamage(damage, attackerFacing);
			if (IsDead)
			{
				return;
			}

			HurtRequested = true;
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

		/// <summary>
		/// 刷新表达式事实面。派生的三项在这里算：硬直/小动作看剩余时间，死亡看血量；
		/// Airborne 把"起跳那一帧还没离地"也算作空中（否则会闪一帧地面动画）。
		/// 必须在 UpdateLocomotion 之后调用（速度与 IsOnFloor 才是本帧结果）。
		/// </summary>
		private void SyncAnimFacts()
		{
			Hurt = m_HurtTime > 0f;
			Emoting = m_EmoteTime > 0f;
			Dead = IsDead;
			Rising = Velocity.Y < 0f;
			Airborne = !IsOnFloor() || (JumpCount > 0 && Rising);
		}

		// ---- 事实更新 ----

		/// <summary>受击事实：消费命中请求（出招期间不打断，收招后再进入硬直）并计时。</summary>
		private void UpdateHurt(float dt)
		{
			if (m_HurtTime > 0)
			{
				m_HurtTime = Mathf.Max(0f, m_HurtTime - dt);
			}

			if (AttackSegment >= 0)
			{
				return;
			}

			if (HurtRequested && !IsDead && m_HurtLen > 0)
			{
				HurtRequested = false;
				m_HurtTime = m_HurtLen;
				PlaySound(Config.HurtSoundId);   // 受害者自己的受击语音（旧 BaseHero.gd:592 按 self 选音）
			}
		}

		/// <summary>
		/// 普攻段事实：起手（段末帧按键可连击推进）、收招（连段序号推进或归零）。
		/// 段最短停留 = max(AttackConfig.Interval, 动画时长)，Interval 是配置表秒数。
		/// </summary>
		private void UpdateAttack(float dt)
		{
			if (AttackSegment >= 0)
			{
				m_SegTime += dt;
				if (m_SegTime < m_SegLens[AttackSegment])
				{
					return;
				}

				// 段末：按键且可连 → 下一段；否则收招（推进连段序号，空中起手则归零）
				if (m_AttackPressed && m_Chainable && AttackSegment < m_Combo.Length - 1)
				{
					AttackSegment++;
					m_SegTime = 0f;
					Log.Debug("[Hero] 连击 → 段{0}", AttackSegment + 1);
				}
				else
				{
					ComboIndex = m_Chainable ? (ComboIndex + 1) % m_Combo.Length : 0;
					Log.Debug("[Hero] 收招 连段序号={0}", ComboIndex);
					AttackSegment = -1;
				}
			}
			else
			{
				// 空中连段归零（旧项目 role1 在空中时 hit_count = 0）
				if (!IsOnFloor())
				{
					ComboIndex = 0;
				}

				// 起手：攻击/受击中按键丢弃；硬直与死亡事实由 UpdateHurt/IsDead 表达
				if (m_AttackPressed && m_HurtTime <= 0 && !IsDead && m_Combo.Length > 0)
				{
					AttackSegment = Mathf.Clamp(ComboIndex, 0, m_Combo.Length - 1);
					m_SegTime = 0f;
					m_Chainable = IsOnFloor();
					Log.Debug("[Hero] 攻击段{0} 起手", AttackSegment + 1);
				}
			}
		}

		/// <summary>跳跃事实：次数与纵向速度。攻击/受击中按键丢弃；两段用完后 CanJump 自然拒绝。</summary>
		private void UpdateJump()
		{
			if (!m_JumpPressed || !CanJump || AttackSegment >= 0 || m_HurtTime > 0 || IsDead)
			{
				return;
			}

			JumpCount++;
			ComboIndex = 0;
			Velocity = new Vector2(Velocity.X, -Config.JumpSpeed);
			Log.Debug("[Hero] 起跳 JumpCount={0} vy={1:F0}", JumpCount, Velocity.Y);
		}

		/// <summary>
		/// 待机小动作事实：站在地面、无输入、不在攻击/受击/死亡时，按 IdleEmoteDelay 随机排期，
		/// 时长为小动作动画本身（<see cref="IdleFlavorAnim"/>，角色专属）。
		/// </summary>
		private void UpdateEmote(float dt)
		{
			bool idle = IsOnFloor() && MoveInput == 0 && AttackSegment < 0 && m_HurtTime <= 0 && !IsDead && m_EmoteLen > 0;

			if (!idle)
			{
				m_EmoteTime = 0f;
				return;
			}

			if (m_EmoteTime > 0)
			{
				m_EmoteTime = Mathf.Max(0f, m_EmoteTime - dt);
			}
			else if (m_PhysTime >= m_NextEmoteAt)
			{
				m_EmoteTime = m_EmoteLen;
				m_NextEmoteAt = m_PhysTime + m_EmoteLen + NextEmoteDelay();
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

			if (IsDead || m_HurtTime > 0)
			{
				Velocity = new Vector2(0, Velocity.Y);
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
		/// 窗口时长来自配置表（AGENTS 3.5：代码里不得出现数值字面量）。
		/// </summary>
		private void RegisterDirectionTap(int dir)
		{
			Running = dir == m_LastTapDirection && m_PhysTime - m_LastTapTime <= Config.RunDoubleTapWindow;
			m_LastTapDirection = dir;
			m_LastTapTime = m_PhysTime;
		}

		/// <summary>
		/// 装配本英雄的普攻连段：取 AttackConfig 里 `OwnerId == 自己` 的行，按 ComboIndex 排序；
		/// 段序号 i（0 起）对应动画 `attack_<i+1>`（各角色状态机按同一约定生成攻击节点）。
		/// 同时缓存每段时长 max(Interval, 动画长度)。怪物 AI（M5）用同一份数据：
		/// 过滤 OwnerId + 按 AiWeight 抽招。
		/// </summary>
		private void LoadCombo()
		{
			List<AttackConfig> combo = new List<AttackConfig>();
			foreach (AttackConfig attack in ConfigSystem.Instance.Tables.TbAttackConfig.DataList)
			{
				if (attack.OwnerId == Config.EntityId)
				{
					combo.Add(attack);
				}
			}

			combo.Sort((a, b) => a.ComboIndex.CompareTo(b.ComboIndex));
			m_Combo = [.. combo];
		}

		/// <summary>
		/// 缓存动画时长类事实：每段连段时长、受击硬直时长、待机小动作时长。
		/// 动画名来自配置表（AttackConfig.Animation）、全项目标准名（hurt）或角色覆写
		/// （IdleFlavorAnim）；基类不出现角色专属动画名。
		/// </summary>
		private void CacheAnimLens()
		{
			m_SegLens = new float[m_Combo.Length];
			for (int i = 0; i < m_Combo.Length; i++)
			{
				m_SegLens[i] = Mathf.Max(m_Combo[i].Interval, GetAnimLength(m_Combo[i].Animation));
			}

			m_HurtLen = GetAnimLength(HurtAnimName);
			m_EmoteLen = IdleFlavorAnim == "" ? 0f : GetAnimLength(IdleFlavorAnim);
		}

		private float GetAnimLength(string animName)
		{
			if (AnimPlayer == null || animName == null || AnimPlayer.HasAnimation(animName) == false)
			{
				Log.Warning("[HeroEntity] 动画库缺少 {0}，相关时长按 0 处理", animName);
				return 0f;
			}

			return (float)AnimPlayer.GetAnimation(animName).Length;
		}

		/// <summary>待机小动作的随机排期间隔（HeroConfig.IdleEmoteDelay 区间内取随机）。</summary>
		private double NextEmoteDelay()
		{
			return GD.RandRange(Config.IdleEmoteDelay.X, Config.IdleEmoteDelay.Y);
		}
	}
}
