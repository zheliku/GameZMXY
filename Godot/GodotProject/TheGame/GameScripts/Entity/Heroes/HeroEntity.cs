using System.Collections.Generic;
using GameConfig.Battle;
using GameConfig.Hero;
using GameFramework.Entity;
using GameFramework.Fsm;
using Godot;
using GodotGameFramework;

namespace GameLogic.Entity
{
	/// <summary>
	/// 英雄实体公共部分：读取 HeroConfig、采集输入、创建并驱动状态机（AGENTS 5.1 / 5.2）。
	///
	/// 两条时间轴的分工（重要）：
	///  * 状态机（GF.Fsm）由框架在帧步长驱动 —— 负责状态切换与动画播放；
	///  * _PhysicsProcess 在物理步长驱动 —— 负责重力、水平移动与 MoveAndSlide。
	/// 状态只表达"想怎么动"（MoveStyle + MoveInput），物理负责换算成速度。
	///
	/// 输入采用"边沿查询"（状态里直接 Input.IsActionJustPressed），不缓存标记：
	/// 按键在不能生效的状态（两段跳用完、攻击/受击期间）当帧丢弃，不会残留到落地或收招之后再触发。
	/// 与旧项目的已知差异：旧项目是 is_action_pressed 电平触发（按住 J 会随冷却自动连段），
	/// 本项目逐次点按出段——有意的操作手感取舍，如需"按住自动连段"再改为电平查询。
	/// </summary>
	public partial class HeroEntity : ActorEntity
	{
		/// <summary>
		/// 最多跳跃次数（地面一段 + 空中N段，来自 HeroConfig.JumpCountMax）。
		/// 旧项目 jump() 判据是 jump_count &lt; 2。
		/// </summary>
		public int MaxJumpCount => Config?.JumpCountMax ?? 0;

		/// <summary>输入动作名：跳跃（K）</summary>
		public static readonly StringName ActionJump = "jump";

		/// <summary>输入动作名：普攻（J）</summary>
		public static readonly StringName ActionAttack = "attack";

		/// <summary>英雄配置 Id（对应 HeroConfig.Id）</summary>
		[Export]
		public int HeroId = 1;

		/// <summary>英雄配置</summary>
		public HeroConfig Config { get; private set; }

		/// <summary>状态机</summary>
		public IFsm<HeroEntity> Fsm { get; private set; }

		/// <summary>水平输入：-1 左 / 0 无 / 1 右</summary>
		public int MoveInput { get; private set; }

		/// <summary>
		/// 是否允许 _PhysicsProcess 用输入改写横向速度。
		/// false = 完全不碰横向速度（出招 / 受击 / 死亡期间）：
		/// 旧项目出招时不调 NorMalMove，只有"站在地面且出招中"会在 BaseHero.gd:505 把 velocity.x 清零一次，
		/// 空中出招因此保留原有动量。
		/// </summary>
		public bool HorizontalControl { get; set; } = true;

		/// <summary>
		/// 是否处于跑步档（快走）。规则：同一方向键在 RunDoubleTapWindow 内二次按下进入，松手退出。
		/// 窗口时长来自配置，见 RegisterDirectionTap。
		/// </summary>
		public bool IsRunning { get; private set; }

		/// <summary>已使用的跳跃次数：0=站在地面；1=刚起跳（还剩一次二段跳）；2=两段用完。落地归零。</summary>
		public int JumpCount { get; private set; }

		/// <summary>是否还有可用的跳跃次数</summary>
		public bool CanJump => JumpCount < MaxJumpCount;

		/// <summary>受击请求：M4 命中逻辑置 true；状态机用 ConsumeHurt() 取走并清空</summary>
		public bool HurtRequested { get; set; }

		/// <summary>
		/// 当前要播放的连段序号（0 起）。语义等同旧项目的 hit_count：
		/// 每段动画**播完后** +1（最后一段后回到 0），而不是"再按一次才 +1"。
		/// 空中归零（旧项目 role1 的 `if not is_on_floor(): hit_count = 0`）。
		/// </summary>
		public int ComboIndex { get; private set; }

		/// <summary>当前段对应的攻击配置（动画名 + 本段最短停留 Interval；M4 起用于伤害包）</summary>
		public AttackConfig CurrentAttack =>
			m_Combo.Length == 0 ? null : m_Combo[Mathf.Clamp(ComboIndex, 0, m_Combo.Length - 1)];

		/// <summary>输入动作名：左移（A）。与 project.godot 的 InputMap 一一对应，改键只改那里。</summary>
		private static readonly StringName ActionMoveLeft = "move_left";

		/// <summary>输入动作名：右移（D）</summary>
		private static readonly StringName ActionMoveRight = "move_right";

		/// <summary>本英雄的普攻连段，已按 ComboIndex 排序；由 HeroAttackConfig + AttackConfig 装配</summary>
		private AttackConfig[] m_Combo = System.Array.Empty<AttackConfig>();

		/// <summary>上一次点击的方向（-1/1），与 m_LastTapTime 一起用于双击判定</summary>
		private int m_LastTapDirection;

		/// <summary>上一次点击方向的时刻（秒，取单调时钟 Time.GetTicksMsec）</summary>
		private double m_LastTapTime;

		/// <summary>
		/// 是否处于"已显示、可驱动"状态：OnShow 置真、OnHide 置假。
		/// _PhysicsProcess 提前返回靠它——实体隐藏/回收后节点仍在树上，不挡会继续跑物理。
		/// </summary>
		private bool m_Active;

		public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
		{
			base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);

			Config = ConfigSystem.Instance.Tables.TbHeroConfig.Get(HeroId);
			if (Config == null)
			{
				Log.Error("[HeroEntity] HeroConfig 缺失：HeroId={0}", HeroId);
				return;
			}

			MaxHp = Config.BaseHp;
			Hp = MaxHp;
			LoadCombo();
		}

		public override void OnShow(object userData)
		{
			base.OnShow(userData);

			// 池复用的实例带着上次的脏状态回来，可变状态一律在 OnShow 重置（AGENTS 4.2）
			Hp = MaxHp;
			ComboIndex = 0;
			JumpCount = 0;
			MoveInput = 0;
			HorizontalControl = true;
			IsRunning = false;
			m_LastTapDirection = 0;
			m_LastTapTime = 0;
			HurtRequested = false;
			Velocity = Vector2.Zero;

			// 状态机随"显示"创建、随"隐藏"销毁：保证复用实例总是从 Idle 干净起步
			Fsm = GF.Fsm.CreateFsm(this,
				new HeroIdleState(),
				new HeroWalkState(),
				new HeroRunState(),
				new HeroJumpState(),
				new HeroFallState(),
				new HeroAttackState(),
				new HeroHurtState(),
				new HeroDeathState());
			Fsm.Start<HeroIdleState>();

			m_Active = true;
			SetFacing(1);
		}

		public override void OnHide(bool isShutdown, object userData)
		{
			m_Active = false;

			if (Fsm != null)
			{
				GF.Fsm.DestroyFsm(Fsm);
				Fsm = null;
			}

			base.OnHide(isShutdown, userData);
		}

		/// <summary>
		/// 物理步长：输入采集 + 重力 + 水平移动 + 落地归零跳跃次数。
		/// 状态机不在这里推进（它由框架驱动），状态只通过 Motion / TryJump 表达意图。
		/// </summary>
		public override void _PhysicsProcess(double delta)
		{
			if (!m_Active || Config == null)
			{
				return;
			}

			float dt = (float)delta;
			ReadInput();

			// 重力每帧都施加（而不是"仅离地时"）：
			// 速度为 0 时 MoveAndSlide 不做运动检测，IsOnFloor() 可能闪断成 false，
			// 会让站地面被误判成空中（连段被重置、状态乱跳）。
			// 始终向下压住地面，IsOnFloor() 才稳定。
			Velocity += new Vector2(0, Config.Gravity * dt);

			if (HorizontalControl)
			{
				float speed = IsRunning ? Config.RunSpeed : Config.WalkSpeed;
				Velocity = new Vector2(MoveInput * speed, Velocity.Y);
			}

			MoveAndSlide();

			// 落地归零跳跃次数（旧项目：is_on_floor() and velocity.y == 0 → jump_count = 0）。
			// 必须带 "Velocity.Y >= 0"：起跳那一帧角色可能还没离开地面，
			// 只判 IsOnFloor() 会把刚用掉的次数立刻清零，变成无限跳。
			if (IsOnFloor() && Velocity.Y >= 0)
			{
				JumpCount = 0;
			}
		}

		/// <summary>取走受击请求（读 + 清空，避免同一次请求被多个状态重复响应）</summary>
		public bool ConsumeHurt()
		{
			if (!HurtRequested)
			{
				return false;
			}

			HurtRequested = false;
			return true;
		}

		/// <summary>
		/// 起跳。次数用完返回 false（动画由 HeroJumpState 按 JumpCount 播放）。
		/// 两段跳都用同一个 JumpSpeed 重置纵向速度——与旧项目一致（jump() 里 velocity.y = jump_power）。
		/// </summary>
		public bool TryJump()
		{
			if (!CanJump)
			{
				Log.Debug("[Hero] 起跳被拒 JumpCount={0}", JumpCount);
				return false;
			}

			JumpCount++;
			// 旧项目：role1 在空中时 hit_count 归零 —— 起跳即视为空中，连段从头开始
			ComboIndex = 0;
			Velocity = new Vector2(Velocity.X, -Config.JumpSpeed);
			Log.Debug("[Hero] 起跳 #{0} vy={1:F0}", JumpCount, Velocity.Y);
			return true;
		}

		/// <summary>
		/// 连段推进：序号 +1，超过最后一段回到 0。
		/// 对应旧项目每段 `await animation_finished` 之后的 `hit_count = N`（最后一段写 0）。
		/// </summary>
		public void AdvanceCombo()
		{
			if (m_Combo.Length == 0)
			{
				return;
			}

			ComboIndex = (ComboIndex + 1) % m_Combo.Length;
		}

		/// <summary>
		/// 连段归零。旧项目 role1 在空中时 hit_count = 0。
		/// 触发点：起跳（TryJump）与进入下落（HeroFallState）。
		/// 刻意不用"每帧 IsOnFloor() 轮询"——那正是"连按 J 偶尔重播同一段"的根因：
		/// 站地面静止时 MoveAndSlide 无运动检测，IsOnFloor() 会闪断，把地面误判成空中。
		/// </summary>
		public void ResetCombo()
		{
			ComboIndex = 0;
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
				IsRunning = false;
			}
		}

		/// <summary>
		/// 方向键点击登记：同一方向在 Config.RunDoubleTapWindow 秒内二次按下 → 进入跑步档。
		/// 窗口时长来自配置表（AGENTS 3.5：代码里不得出现数值字面量）。
		/// </summary>
		private void RegisterDirectionTap(int dir)
		{
			double now = Time.GetTicksMsec() / 1000.0;
			IsRunning = dir == m_LastTapDirection && now - m_LastTapTime <= Config.RunDoubleTapWindow;
			m_LastTapDirection = dir;
			m_LastTapTime = now;
		}

		/// <summary>按 HeroAttackConfig 的 ComboIndex 顺序装配连段（数据驱动，无硬编码分支）</summary>
		private void LoadCombo()
		{
			List<HeroAttackConfig> links = new List<HeroAttackConfig>();
			foreach (HeroAttackConfig link in ConfigSystem.Instance.Tables.TbHeroAttackConfig.DataList)
			{
				if (link.HeroId == HeroId)
				{
					links.Add(link);
				}
			}

			links.Sort((a, b) => a.ComboIndex.CompareTo(b.ComboIndex));

			List<AttackConfig> combo = new List<AttackConfig>();
			foreach (HeroAttackConfig link in links)
			{
				AttackConfig attack = ConfigSystem.Instance.Tables.TbAttackConfig.Get(link.AttackId);
				if (attack == null)
				{
					Log.Error("[HeroEntity] HeroAttackConfig 引用了不存在的攻击：AttackId={0}", link.AttackId);
					continue;
				}

				combo.Add(attack);
			}

			m_Combo = combo.ToArray();
		}
	}
}
