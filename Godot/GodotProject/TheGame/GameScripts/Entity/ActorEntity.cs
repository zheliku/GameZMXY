using GameFramework.Entity;
using Godot;
using GodotGameFramework;

namespace GameLogic.Entity
{
	/// <summary>
	/// 战斗角色基类（AGENTS 5.1）。英雄与怪物都从这里派生。
	/// 职责：IEntity 生命周期样板、血量、动画层与判定区引用、受击与死亡入口。
	///
	/// 本类<b>不设"阵营"字段</b>：敌我关系由物理层表达（AGENTS 5.4）——
	/// 玩家本体=PlayerBody / 怪物本体=EnemyBody；玩家攻击判定=PlayerHitBox 只扫 EnemyHurtBox，
	/// 怪物攻击判定=EnemyHitBox 只扫 PlayerHurtBox。"谁能打到谁"在物理层就被约束，
	/// 再落一份 Team 字段只会变成第二个真相来源（层配对了、字段写错时两处不一致）。
	/// "这一下是谁打的"由 HitBox 上挂的宿主显式引用给出（AGENTS 5.3）；
	/// "场上还有多少敌人"走实体组查询。三者都不需要阵营字段。
	///
	/// 子节点引用走 <c>[Export]</c> + <c>m_</c> 前缀（AGENTS 4.1 生成器约定），由场景绑定
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

		/// <summary>身体动画层（场景子节点 m_Body）</summary>
		[Export] private AnimatedSprite2D m_Body;

		/// <summary>武器动画层（场景子节点 m_Weapon，可为空）</summary>
		[Export] private AnimatedSprite2D m_Weapon;

		/// <summary>受击判定区（场景子节点 m_HurtBox，可为空）</summary>
		[Export] private Area2D m_HurtBox;

		/// <summary>攻击判定区（场景子节点 m_HitBox，可为空；判定帧由 M4 动画驱动开关）</summary>
		[Export] private Area2D m_HitBox;

		/// <summary>身体动画层</summary>
		public AnimatedSprite2D Body => m_Body;

		/// <summary>武器动画层</summary>
		public AnimatedSprite2D Weapon => m_Weapon;

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
				Log.Error("[ActorEntity] 场景未绑定 m_Body（AnimatedSprite2D）：{0}", entityAssetName);
			}
		}

		/// <summary>实体显示。可变状态每次显示都要重置（池复用会带回上次的脏状态）。</summary>
		public virtual void OnShow(object userData)
		{
			Visible = true;
		}

		/// <summary>实体隐藏。</summary>
		public virtual void OnHide(bool isShutdown, object userData)
		{
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

		/// <summary>实体轮询。角色行为不走这里——由状态机与 _PhysicsProcess 驱动（见 HeroEntity）。</summary>
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
		/// 播放身体与武器两层动画（两层同名同步）。
		/// 动画名来自 SpriteFrames（M1 迁移产物），标准命名见 AGENTS 8.2。
		/// </summary>
		public void PlayAnim(string animName)
		{
			PlayAnimOn(m_Body, animName, Name);
			PlayAnimOn(m_Weapon, animName, Name);
		}

		private static void PlayAnimOn(AnimatedSprite2D layer, string animName, string entityName)
		{
			if (layer?.SpriteFrames == null)
			{
				return;
			}

			if (!layer.SpriteFrames.HasAnimation(animName))
			{
				// 动画名打错时不能无声失败：SpriteFrames 缺失动画是配置/资源问题，必须报出来
				Log.Warning("[ActorEntity] {0} 的 {1} 层缺少动画 {2}", entityName, layer.Name, animName);
				return;
			}

			if (!layer.IsPlaying() || layer.Animation != animName)
			{
				layer.Play(animName);
			}
		}

		/// <summary>
		/// 当前动画是否已播完（身体与武器两层都算）。
		/// 用 IsPlaying() 而非信号回调：状态机每帧轮询本方法，状态切换与动画结束在同一帧内判定，
		/// 避免 await 恢复时状态已被切走而产生的竞态。
		/// </summary>
		public bool IsAnimFinished()
		{
			return (m_Body == null || !m_Body.IsPlaying())
				&& (m_Weapon == null || !m_Weapon.IsPlaying());
		}

		/// <summary>设置朝向（0 表示不变）。翻转两个动画层。</summary>
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

			if (m_Weapon != null)
			{
				m_Weapon.FlipH = mirror;
			}

			// 攻击判定盒随朝向镜像。
			// 不用负 scale 翻转父节点：Godot 对物理节点（CharacterBody2D/Area2D）使用负缩放会有告警且碰撞行为不确定，
			// 所以只改判定盒的本地 X 偏移。
			if (m_HitBox != null)
			{
				m_HitBox.Position = new Vector2(m_HitBoxOffsetX * Facing, m_HitBox.Position.Y);
			}
		}

		/// <summary>受击入口。M4 接入 DamageCalculator 后由命中逻辑调用。</summary>
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
