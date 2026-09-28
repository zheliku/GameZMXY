using GameConfig.Entity;
using GameConfig.Sound;
using GameFramework.Entity;
using GameLogic.Config;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Sound;

namespace GameLogic.Entity
{
	/// <summary>
	/// 战斗角色基类（AGENTS 5.1）。英雄与怪物都从这里派生。
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
	/// 本类<b>不设"阵营"字段</b>：敌我关系由物理层表达（AGENTS 5.4）。
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

		/// <summary>身体层（场景子节点 m_Body）：Sprite2D 均匀网格，帧由动画轨道驱动</summary>
		[Export] private Sprite2D m_Body;

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

		/// <summary>受击判定区（场景子节点 m_HurtBox，可为空）</summary>
		[Export] private Area2D m_HurtBox;

		/// <summary>攻击判定区（场景子节点 m_HitBox，可为空；判定帧由 M4 动画驱动开关）</summary>
		[Export] private Area2D m_HitBox;

		/// <summary>身体层</summary>
		public Sprite2D Body => m_Body;

		/// <summary>特效层容器（负责朝向镜像）</summary>
		public Node2D EffectRoot => m_EffectRoot;

		/// <summary>动画播放器</summary>
		public AnimationPlayer AnimPlayer => m_AnimPlayer;

		/// <summary>动画状态机</summary>
		public AnimationTree AnimTree => m_AnimTree;

		/// <summary>受击判定区</summary>
		public Area2D HurtBox => m_HurtBox;

		/// <summary>攻击判定区</summary>
		public Area2D HitBox => m_HitBox;

		/// <summary>最大生命</summary>
		public int MaxHp { get; protected set; }

		/// <summary>当前生命</summary>
		public int Hp { get; protected set; }

		/// <summary>是否已死亡</summary>
		public bool IsDead => Hp <= 0;

		/// <summary>朝向：1 右 / -1 左。素材原始朝左，见 SetFacing 注释。</summary>
		public int Facing { get; private set; } = 1;

		/// <summary>攻击判定盒相对宿主的横向偏移绝对值（从场景读取，镜像朝向时用）</summary>
		private float m_HitBoxOffsetX;

		/// <summary>实体初始化。isNewInstance 为 true 时做一次性初始化（AGENTS 4.2）。</summary>
		public virtual void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance,
			object userData)
		{
			Id = entityId;
			EntityAssetName = entityAssetName;
			Name = entityAssetName == null ? "ActorEntity" : $"Entity_{entityId}";
			EntityGroup = entityGroup;

			if (isNewInstance)
			{
				// 判定盒的横向偏移由场景给出，这里记下绝对值，供 SetFacing 按朝向镜像
				m_HitBoxOffsetX = m_HitBox != null ? Mathf.Abs(m_HitBox.Position.X) : 0f;
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
		}

		/// <summary>实体隐藏。关停阶段（isShutdown=true）场景树可能已析构，不再碰节点。</summary>
		public virtual void OnHide(bool isShutdown, object userData)
		{
			if (isShutdown || !IsInstanceValid(this))
			{
				return;
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
		/// `GF.Sound.PlaySound(资源, 组名)`。代码里不出现资源路径（红线 5），路径与组都在表里。
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

		/// <summary>设置朝向（0 表示不变）。翻转通用表现层（身体/特效）与判定盒，最后调钩子。</summary>
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
			if (m_Body != null)
			{
				m_Body.FlipH = mirror;
			}

			// 特效层用父容器负 scale 镜像（同旧项目 Action.scale.x = ±1）：
			// 这样特效节点上由轨道写入的 offset 会一起镜像，特效不会跑到身体另一侧。
			if (m_EffectRoot != null)
			{
				m_EffectRoot.Scale = new Vector2(mirror ? -1 : 1, 1);
			}

			// 攻击判定盒随朝向镜像。
			// 不用负 scale 翻转父节点：Godot 对物理节点（CharacterBody2D/Area2D）使用负缩放会有告警且碰撞行为不确定，
			// 所以只改判定盒的本地 X 偏移。
			if (m_HitBox != null)
			{
				m_HitBox.Position = new Vector2(m_HitBoxOffsetX * Facing, m_HitBox.Position.Y);
			}

			OnFacingChanged(dir);
		}

		/// <summary>
		/// 朝向变化钩子：基类翻转通用表现层（身体/特效）与判定盒之后调用。
		/// 子类用它镜像自己额外的附加层（如英雄专属的武器层），不要重写翻转规则本身。
		/// </summary>
		protected virtual void OnFacingChanged(int dir)
		{
		}

		/// <summary>受击入口。M4 接入 DamageCalculator 后由命中逻辑调用；子类可叠加硬直/语音等表现。</summary>
		public virtual void TakeDamage(int damage, int attackerFacing)
		{
			if (IsDead)
			{
				return;
			}

			Hp = Mathf.Max(0, Hp - damage);
			SetFacing(-attackerFacing);
		}

		/// <summary>恢复生命。</summary>
		public virtual void Heal(int value)
		{
			if (IsDead || value <= 0)
			{
				return;
			}

			Hp = Mathf.Min(MaxHp, Hp + value);
		}
	}
}
