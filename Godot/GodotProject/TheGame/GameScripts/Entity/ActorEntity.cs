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

		/// <summary>
		/// 特效层容器（场景子节点 m_EffectRoot，可为空）：其子节点 m_Effect 是棍气等特效的
		/// AnimatedSprite2D。
		/// 为什么多一层容器：旧项目把特效的位移写在节点 offset 上、朝向镜像写在父节点
		/// `Action.scale.x = ±1`（镜像会连带翻转 offset）。这里用同构做法——
		/// m_EffectRoot 负责朝向（scale.x = ±1），m_Effect 的属性交给 AnimationPlayer 轨道驱动，
		/// 两者互不覆盖。
		/// </summary>
		[Export] private Node2D m_EffectRoot;

		/// <summary>
		/// 特效层动画播放器（场景子节点 m_EffectPlayer，可为空）。
		/// 旧项目用 AnimationPlayer 的四类属性轨道驱动特效层（animation/frame/offset/scale），
		/// 迁移后轨道路径为 m_EffectRoot/m_Effect:*，数据见 wukong_effect_library.tres。
		/// </summary>
		[Export] private AnimationPlayer m_EffectPlayer;

		/// <summary>受击判定区（场景子节点 m_HurtBox，可为空）</summary>
		[Export] private Area2D m_HurtBox;

		/// <summary>攻击判定区（场景子节点 m_HitBox，可为空；判定帧由 M4 动画驱动开关）</summary>
		[Export] private Area2D m_HitBox;

		/// <summary>身体动画层</summary>
		public AnimatedSprite2D Body => m_Body;

		/// <summary>武器动画层</summary>
		public AnimatedSprite2D Weapon => m_Weapon;

		/// <summary>特效层容器（负责朝向镜像）</summary>
		public Node2D EffectRoot => m_EffectRoot;

		/// <summary>特效层动画播放器</summary>
		public AnimationPlayer EffectPlayer => m_EffectPlayer;

		/// <summary>
		/// 按 SoundId 播放一次性音效：查 SoundConfig 拿路径与组，直接走框架的统一入口
		/// `GF.Sound.PlaySound(资源, 组名)`（SoundComponent.cs:153；组 = SoundGroupRes 注册的
		/// Music/SFX/UI，各自映射到 Godot 总线，带音量/静音/代理池/优先级抢占）。
		/// 代码里不出现资源路径（红线 5），路径与组都在表里。
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
		/// 播放身体、武器、特效三层动画（三层同名同步）。
		/// 动画名来自 SpriteFrames（M1 迁移产物），标准命名见 AGENTS 8.2。
		/// </summary>
		public void PlayAnim(string animName)
		{
			PlayAnimOn(m_Body, animName, Name);
			PlayAnimOn(m_Weapon, animName, Name);
			PlayEffectAnim(animName);
		}

		/// <summary>
		/// 特效层播放（棍气等）：特效层只有攻击类动画（attack_1..4），
		/// 其余动画名没有特效，落到空白动画 empty——与旧项目一致（旧项目把非攻击动画的
		/// SpecialEffect 切到空白 "wait"）。
		/// 用 AnimationPlayer 而非直接播 SpriteFrames：特效的 frame/offset/scale 是原项目
		/// 用轨道手调的演出数据（含"末尾空白帧收招"），必须原样由轨道驱动。
		/// </summary>
		private void PlayEffectAnim(string animName)
		{
			if (m_EffectPlayer == null)
			{
				return;
			}

			StringName name = m_EffectPlayer.HasAnimation(animName) ? animName : EffectEmptyAnim;
			if (!m_EffectPlayer.IsPlaying() || m_EffectPlayer.CurrentAnimation != name)
			{
				m_EffectPlayer.Play(name);
			}
		}

		/// <summary>特效层的空白动画名（wukong_effect_library.tres 内置）</summary>
		private const string EffectEmptyAnim = "empty";

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
		/// 当前动画是否已播完（只看身体与武器两层）。
		/// 用 IsPlaying() 而非信号回调：状态机每帧轮询本方法，状态切换与动画结束在同一帧内判定，
		/// 避免 await 恢复时状态已被切走而产生的竞态。
		/// 特效层不参与判定：它由独立的 AnimationPlayer 驱动（不是 IsPlaying 语义），
		/// 且攻击特效与身体动画等长，不影响状态时长。
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

			// 特效层用父容器负 scale 镜像（同旧项目 Action.scale.x = ±1）：
			// 这样特效节点上由轨道写入的 offset 会一起镜像，特效不会跑到身体另一侧。
			// 负 scale 只用在 Node2D 容器上（非物理节点），不影响碰撞。
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
