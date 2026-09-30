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
	/// 战斗角色基类（根规范 §5.1）。英雄与怪物都从这里派生。
	/// 职责：IEntity 生命周期样板、血量、动画宿主（AnimationPlayer + AnimationTree）、
	/// **通用**表现层（身体/特效）与判定区、朝向、受击与死亡入口。**本类不含任何角色事实**
	/// （移动输入、跑档、攻击段、跳跃次数……都在子类），也不含英雄专属层（武器层在 HeroEntity）。
	/// 朝向翻转的扩展点见 <see cref="OnFacingChanged"/>。
	///
	/// 动画架构（迁移决策 2026-09-28）：
	///  * 子类场景挂 AnimationPlayer（承载该角色自己的动画库：帧/特效/音效轨道）+
	///    AnimationTree（该角色自己的状态机资源）；
	///  * **状态机不设参数、不设脉冲**：子类把"角色属性"（见 HeroEntity 的表达式事实面字段）
	///    每物理帧同步一次，该角色状态机的每条边用 advance_expression 判断这些属性决定转移；
	///  * 基类只把 AnimationTree 的表达式基对象指向本节点（见 <see cref="OnInit"/>），
	///    不解释任何事实，也不出现任何角色专属动画名。
	/// 本类<b>不设"阵营"字段</b>：敌我关系由物理层表达（根规范 §7）。
	/// 子节点引用走 <c>[Export]</c> + <c>m_</c> 前缀，由场景绑定
	/// （.tscn 的节点头必须声明 node_paths=PackedStringArray(...)，否则 NodePath 赋值会被忽略）。
	/// </summary>
	public partial class ActorEntity : CharacterBody2D, IEntity
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

		/// <summary>
		/// 身体层（场景子节点 m_Body），帧由动画轨道驱动。两种形态：
		/// Sprite2D 均匀网格（英雄：身体/武器同网格）或 AnimatedSprite2D（怪物：每动作一张图集，
		/// 轨道设 animation/frame/offset，同旧 mr_ani）。朝向镜像规则见 <see cref="SetFacing"/>。
		/// </summary>
		[Export] private Node2D m_Body;

		/// <summary>
		/// 特效层容器（场景子节点 m_EffectRoot，可为空）：其子节点 m_Effect 是特效的
		/// AnimatedSprite2D。容器负责朝向镜像（scale.x = ±1，连带镜像轨道写入的 offset），
		/// m_Effect 的属性由动画轨道驱动——与旧项目 Action/SpecialEffect 同构。
		/// </summary>
		[Export] private Node2D m_EffectRoot;

		/// <summary>动画播放器（场景子节点 m_AnimPlayer）：承载该角色自己的动画库，由 m_AnimTree 驱动</summary>
		[Export] private AnimationPlayer m_AnimPlayer;

		/// <summary>动画状态机（场景子节点 m_AnimTree）：该角色自己的嵌套状态机资源</summary>
		[Export] private AnimationTree m_AnimTree;

		/// <summary>受击判定区（场景子节点 m_HurtBox，可为空；脚本 HurtBox 持有本实体引用）</summary>
		[Export] private HurtBox m_HurtBox;

		/// <summary>
		/// 攻击判定区（场景子节点 m_HitBox，可为空）。**几何与开关完全由动画值轨道驱动**
		/// （同旧项目：旧动画 keyframe HitBox 的 shape/position/disabled）——代码不碰判定盒几何。
		/// 朝向镜像由容器 <see cref="m_HitBoxRoot"/> 承担（同旧 base_damagebox 的 scale.x 翻转），
		/// 动画里写的位置是"素材原生朝向"坐标（前方 = 负 X）。
		/// </summary>
		[Export] private Area2D m_HitBox;

		/// <summary>
		/// 攻击判定区容器（场景子节点 m_HitBoxRoot，可为空）：scale.x = ±1 随朝向翻转，
		/// 镜像 m_HitBox 连同动画轨道写入的位置——与 m_EffectRoot 同构（同旧 base_damagebox）。
		/// </summary>
		[Export] private Node2D m_HitBoxRoot;

		/// <summary>身体层（Sprite2D 或 AnimatedSprite2D）</summary>
		public Node2D Body => m_Body;

		/// <summary>特效层容器（负责朝向镜像）</summary>
		public Node2D EffectRoot => m_EffectRoot;

		/// <summary>动画播放器</summary>
		public AnimationPlayer AnimPlayer => m_AnimPlayer;

		/// <summary>动画状态机</summary>
		public AnimationTree AnimTree => m_AnimTree;

		/// <summary>受击判定区</summary>
		public HurtBox HurtBox => m_HurtBox;

		/// <summary>攻击判定区</summary>
		public Area2D HitBox => m_HitBox;

		/// <summary>最大生命</summary>
		public int MaxHp { get; protected set; }

		/// <summary>当前生命</summary>
		public int Hp { get; protected set; }

		/// <summary>
		/// 死亡事实（[Export]：表达式读它，如 P_DEATH = "Dead"）。**单一事实源**：
		/// 只在 ReceiveHit 扣血扣到 0 的那一刻置位、OnShow 复位，C# 与表达式统一用它，别名叫法不保留。
		/// </summary>
		[Export] public bool Dead;

		/// <summary>朝向：1 右 / -1 左。素材原始朝左，见 SetFacing 注释。</summary>
		public int Facing { get; private set; } = 1;

		/// <summary>结算侧别（选 BattleConfig 常数组；子类固定返回）</summary>
		public virtual CombatSide Side => CombatSide.Hero;

		/// <summary>
		/// 已显示、可驱动：OnShow 置真、OnHide 置假。子类 _PhysicsProcess 靠它提前返回（实体隐藏/回收后
		/// 节点仍在树上，不挡会继续跑物理）；AI 等外部持有者靠它判断引用是否还有效（配合 IsInstanceValid）。
		/// （2026-09-30 自 HeroEntity/MonsterEntity 各自的 m_Active 下沉合并。）
		/// </summary>
		public bool IsShown { get; private set; }

		/// <summary>
		/// 飘字锚点相对实体原点的偏移（头顶）。子类按素材高度覆写；属于表现层布局常数，不参与玩法计算。
		/// </summary>
		protected virtual Vector2 PopAnchor => new Vector2(0, -60);

		/// <summary>当前招式的攻击包（出招装填、收招归还；null = 不在出招中）</summary>
		private AttackData m_ActiveAttack;

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
				Log.Error("[ActorEntity] 场景未绑定 m_Body（Sprite2D）：{0}", entityAssetName);
			}

			if (m_AnimTree != null)
			{
				// 表达式以本节点为基对象（子类的事实字段从此刻起可被表达式读取）；
				// 与实体物理同频推进：实体先刷新事实、AnimationTree（子节点）同帧消费。
				m_AnimTree.AdvanceExpressionBaseNode = m_AnimTree.GetPathTo(this);
				m_AnimTree.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Physics;
			}
			else
			{
				Log.Error("[ActorEntity] 场景未绑定 m_AnimTree（AnimationTree）：{0}", entityAssetName);
			}

			if (m_AnimPlayer == null)
			{
				Log.Error("[ActorEntity] 场景未绑定 m_AnimPlayer（AnimationPlayer）：{0}", entityAssetName);
			}
		}

		/// <summary>实体显示。可变状态每次显示都要重置（池复用会带回上次的脏状态）。</summary>
		public virtual void OnShow(object userData)
		{
			Visible = true;
			IsShown = true;
		}

		/// <summary>实体隐藏。关停阶段（isShutdown=true）场景树可能已析构，不再碰节点。</summary>
		public virtual void OnHide(bool isShutdown, object userData)
		{
			IsShown = false;

			// 攻击包是纯 C# 池对象，关停时也要归还（不碰节点）
			if (m_ActiveAttack != null)
			{
				ReferencePool.Release(m_ActiveAttack);
				m_ActiveAttack = null;
			}

			if (isShutdown || !IsInstanceValid(this))
			{
				return;
			}

			ReleaseAttack();
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

		/// <summary>实体轮询。角色行为不走这里——由子类的 _PhysicsProcess 驱动。</summary>
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

			SoundConfig cfg = SoundConfigQuery.Get(id);
			if (cfg == null)
			{
				Log.Error("[ActorEntity] SoundConfig 缺失行：SoundId={0}", id);
				return;
			}

			GF.Sound.PlaySound(cfg.Path, cfg.Group);
		}

		/// <summary>设置朝向（0 表示不变）。翻转通用表现层（身体/特效/判定盒容器），最后调钩子。</summary>
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

			// 特效层用父容器负 scale 镜像（同旧项目 Action.scale.x = ±1）：
			// 这样特效节点上由轨道写入的 offset 会一起镜像，特效不会跑到身体另一侧。
			if (m_EffectRoot != null)
			{
				m_EffectRoot.Scale = new Vector2(mirror ? -1 : 1, 1);
			}

			// 判定盒同理（同旧 base_damagebox.scale.x = ±1）：动画轨道写的是原生朝左坐标，
			// 容器一翻，判定盒位置跟着镜像——代码不碰几何。
			if (m_HitBoxRoot != null)
			{
				m_HitBoxRoot.Scale = new Vector2(mirror ? -1 : 1, 1);
			}

			OnFacingChanged(dir);
		}

		// ---- 攻击判定（M4）----

		/// <summary>
		/// 【动画方法轨道回调】出招起手（attack_N 动画第 0 帧调用）：按 <see cref="GetAttackConfig"/>
		/// 拿到本段配置，快照攻击方属性、掷威力倍率与无双值，装填攻击包。
		/// **时序与几何都归动画**：判定窗口与判定盒尺寸/位置全部是动画值轨道的关键帧
		/// （同旧项目 keyframe shape/position/disabled）——C# 只负责"这一招的数值事实"。
		/// 上一招未收（连段直接推进）时先归还上一招的包。
		/// </summary>
		public virtual void OnAttackBegin()
		{
			ReleaseAttack();
			AttackConfig attack = GetAttackConfig();
			if (attack == null)
			{
				return;
			}

			CombatantStats stats = GetCombatStats();
			float scale = Mathf.Lerp(attack.PowerScale.X, attack.PowerScale.Y, GD.Randf());
			float power = stats.Power * scale + attack.FlatPower;
			int wsGain = attack.WsGain.X >= attack.WsGain.Y
				? attack.WsGain.X
				: GD.RandRange(attack.WsGain.X, attack.WsGain.Y);

			m_ActiveAttack = AttackData.Create(attack.Id, stats, power, attack.DamageKind,
				attack.Knockback, Facing, wsGain, attack.HitProtect, attack.HitSoundId);
		}

		/// <summary>
		/// 【动画方法轨道回调】收招（attack_N 动画末帧调用）：归还攻击包、关判定（兜底——
		/// 正常收招时 disabled 轨道已经先关了）。子类在此推进自己的连段事实（HeroEntity 连段推进、
		/// MonsterEntity 归位 AttackSegment）后调 base。
		/// </summary>
		public virtual void OnAttackEnd()
		{
			ReleaseAttack();
		}

		/// <summary>
		/// 当前段的攻击配置（动画调 OnAttackBegin 时由子类按"正在播的段"解析）。
		/// </summary>
		protected virtual AttackConfig GetAttackConfig()
		{
			return null;
		}

		/// <summary>
		/// 强制收招（安全释放）：归还攻击包。**不是动画回调**——供受击打断、死亡、
		/// OnHide 等打断路径使用；这些路径不会再走到 OnAttackEnd（动画被切走），必须显式释放。
		/// 判定盒不需要代码去关：离开攻击动画时 AnimationMixer 会把 disabled/shape/position
		/// 轨道捕获的初始值还原（值轨道自带的自愈，方法轨道没有——这也是几何走值轨道的原因之一）。
		/// </summary>
		protected void ReleaseAttack()
		{
			if (m_ActiveAttack != null)
			{
				ReferencePool.Release(m_ActiveAttack);
				m_ActiveAttack = null;
			}
		}

		/// <summary>
		/// 判定盒扫到受击盒：交给受击方结算。一招一目标只结算一次（AttackData 去重）；
		/// 判定帧外（无攻击包）或自己打到自己一律忽略。
		/// </summary>
		private void OnHitBoxAreaEntered(Area2D area)
		{
			if (m_ActiveAttack == null || area is not HurtBox hurtBox)
			{
				return;
			}

			ActorEntity target = hurtBox.OwnerEntity;
			if (target == null || target == this || target.Dead)
			{
				return;
			}

			if (!m_ActiveAttack.TryRegisterHit(target.GetInstanceId()))
			{
				return;
			}

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
				result.Damage, result.IsMiss, result.IsCrit, result.Kind, GlobalPosition + PopAnchor, Hp));
			return result;
		}

		/// <summary>结算用属性快照（子类按自己的配置/等级填）。</summary>
		protected virtual CombatantStats GetCombatStats()
		{
			return default;
		}

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

		/// <summary>命中他人钩子（未闪避时）：子类用于无双值累计等攻击方收益。</summary>
		protected virtual void OnHitLanded(AttackData attack, DamageResult result)
		{
		}

		/// <summary>
		/// 朝向变化钩子：基类翻转通用表现层（身体/特效）与判定盒之后调用。
		/// 子类用它镜像自己额外的附加层（如英雄专属的武器层），不要重写翻转规则本身。
		/// </summary>
		protected virtual void OnFacingChanged(int dir)
		{
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
