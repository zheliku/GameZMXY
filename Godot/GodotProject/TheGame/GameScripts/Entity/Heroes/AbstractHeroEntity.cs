using GameLogic.Config;
using GameLogic.Architecture;
using GameLogic.Heroes.Body;
using GameLogic.Entity;
using GameLogic.Battle;
using GameLogic.Battle.Attributes;
using GameLogic.Battle.Damage;
using GameLogic.Battle.Attacks;
using GameLogic.Input;
using System;
using GameConfig.Battle;
using GameConfig.Hero;
using GameFramework.Entity;
using GameFramework.Fsm;
using GameLogic.Body;
using GameLogic.Heroes.Effects;
using GameLogic.Equipment;
using GameLogic.Profile;
using System.Linq;
using Godot;
using GodotGameFramework;

namespace GameLogic.Heroes
{
    /// <summary>
    /// 英雄表现与交互：输入、身体状态机、动画、武器层及出招；数值状态借用已注册的 HeroModel。
    /// 不持有持久进度（等级、经验、灵魂属于档案）；属性由 HeroSystem 随成长、穿戴和局内来源更新，实体只读取。
    /// </summary>
    public abstract partial class AbstractHeroEntity : AbstractActorEntity, IHeroBody
    {
        // ---- 字段 ----

        [Export] private Sprite2D m_Weapon; // 与身体共用定位及帧号，透明区保留原始遮挡。
        [Export] private HeroAppearanceResource m_Appearance; // 身体、防具与武器独立换装的图集资源。

        [Export] private Node2D m_EffectRoot; // 攻击特效层容器，由动画轨道驱动并随朝向镜像。

        [Export] private int m_HeroId; // 场景绑定的 HeroConfig 主键。

        [Export] private MusouAfterimageNode m_MusouAfterimage; // 英雄自有的残影表现，可按角色场景选配。
        [Export] private HeroLevelUpNode m_LevelUp; // 独立升级动画层，不随身体朝向镜像。

        private IFsm<IHeroBody> m_BodyFsm; // 按物理帧推进的英雄身体状态机。
        private ulong m_MusouIntroEnds; // 本英雄拥有的短暂停顿截止时间；零表示没有拥有暂停。

        /// <summary>借用模型中同类英雄共享的身体参数。</summary>
        private HeroBodyParams BodyParams => this.GetModel<ConfigurationModel>().GetHeroBodyParams(HeroId);


        // ---- 属性 ----

        /// <summary>英雄配置 Id（对应 HeroConfig.Id）。每个英雄场景必须显式填写，缺失时本实体停用。</summary>
        public int HeroId => m_HeroId;

        /// <inheritdoc />
        protected override GameConfig.Entity.EntityId EntityType => Config.EntityId;

        /// <summary>英雄配置</summary>
        public HeroConfig Config => this.GetModel<ConfigurationModel>().Tables.TbHeroConfig.Get(HeroId);

        /// <summary>本英雄独占的输入、跳跃与连段数据，由共享 InputSystem 和身体状态推进。</summary>
        public HeroActionData ActionData { get; } = new();

        /// <inheritdoc />
        public override CombatSide Side => CombatSide.Hero;

        /// <summary>直接读取本局已注册属性模型，实体不缓存、创建或销毁模型。</summary>
        private HeroModel Attributes => this.GetModel<HeroModel>();

        /// <inheritdoc />
        public override int Hp => Attributes.Hp.Value;
        /// <inheritdoc />
        public override int MaxHp => Attributes.MaxHp.Value;
        /// <inheritdoc />
        public override int Level => Attributes.Level.Value;

        /// <summary>由具体英雄给出身体初始状态类型。</summary>
        protected abstract Type InitialBodyStateType { get; }

        // ---- 接口实现 ----

        /// <summary>提供身体状态机使用的共享身体参数。</summary>
        HeroBodyParams IHeroBody.Params => BodyParams;

        /// <summary>无双提高横向走跑速度。</summary>
        float IHeroBody.MoveSpeedMultiplier => this.GetSystem<HeroSystem>().MoveSpeedMultiplier;

        /// <summary>无双提高起跳速度；读取时生效，不改写配置快照。</summary>
        float IHeroBody.JumpSpeedMultiplier => this.GetSystem<HeroSystem>().JumpSpeedMultiplier;

        /// <summary>无双期间免受击硬直与击退，仍正常结算伤害。</summary>
        bool IHeroBody.SuperArmor => this.GetSystem<HeroSystem>().SuperArmor;

        /// <summary>提供当前重力，无双时应用官方倍率。</summary>
        float IActorBody.Gravity => BodyParams.Gravity * this.GetSystem<HeroSystem>().GravityMultiplier;

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

            if (Config == null)
            {
                Log.Error("[AbstractHeroEntity] HeroConfig 缺失：HeroId={0}，本实体停用（物理与动画不再驱动）", HeroId);
                SetPhysicsProcess(false);
                return;
            }

            this.GetSystem<ConfigurationSystem>().EnsureHeroBodyParams(HeroId, BuildBodyParams);
        }

        /// <summary>显示时绑定流程已经准备好的数据，复位动作表现并创建身体状态机。</summary>
        /// <param name="userData">不传递英雄数据；属性从本局模型读取。</param>
        /// <exception cref="InvalidOperationException">本局属性模型未准备，或不是当前英雄。</exception>
        public override void OnShow(object userData)
        {
            // 显示只装配表现；即使异步迟到，也不重置或修改当前模型。
            if (Attributes == null || Attributes.HeroId != HeroId)
                throw new InvalidOperationException($"英雄 {HeroId} 显示前必须准备本局属性模型。");
            base.OnShow(userData);
            RefreshAppearance();
            m_MusouAfterimage?.Reset();
            m_LevelUp?.Reset();

            // 池复用的实例带着上次的脏事实回来，一律在 OnShow 复位（见 Entity/AGENTS.md 生命周期）
            m_PendingHurt = null;
            Velocity = Vector2.Zero;
            this.GetSystem<InputSystem>().Reset(ActionData);
            SetFacing(1);
            CreateBodyStateMachine();
        }

        /// <summary>隐藏时销毁身体状态机并执行基类清理。</summary>
        /// <param name="isShutdown">是否框架关停；此时节点可能已释放。</param>
        /// <param name="userData">本次隐藏参数。</param>
        public override void OnHide(bool isShutdown, object userData)
        {
            EndMusouIntro(!isShutdown);
            if (!isShutdown && IsInstanceValid(m_MusouAfterimage))
                m_MusouAfterimage.Reset();
            if (!isShutdown && IsInstanceValid(m_LevelUp))
                m_LevelUp.Reset();
            DestroyBodyStateMachine(isShutdown);
            base.OnHide(isShutdown, userData);
        }

        /// <summary>真实时间结束本英雄拥有的无双开启停顿；暂停时游戏时间为零仍能恢复。</summary>
        /// <param name="delta">游戏帧秒数，本计时只使用单调真实时间。</param>
        public override void _Process(double delta)
        {
            if (m_MusouIntroEnds != 0 && Time.GetTicksMsec() >= m_MusouIntroEnds)
                EndMusouIntro(true);
        }

        /// <summary>物理步长：采样输入 → 身体状态机 → 物理。AnimationPlayer（子节点）同帧稍后消费播放请求。</summary>
        /// <param name="delta">本次物理帧秒数。</param>
        public override void _PhysicsProcess(double delta)
        {
            if (!IsShown || Config == null || delta <= 0)
            {
                return;
            }

            float dt = (float)delta;
            bool wasActive = this.GetModel<HeroModel>().WsActive.Value;
            this.GetSystem<HeroSystem>().AdvanceWs(dt);
            if (wasActive && !this.GetModel<HeroModel>().WsActive.Value)
                EndMusou();
            // 引擎采样保留按住及短按边沿，组合闩锁在输入层，连段与冷却由共享 InputSystem 根据本英雄动作数据裁定。
            this.GetSystem<InputSystem>().Sample(this, dt);
            if (ActionData.MusouRequested)
                TryActivateMusou();

            m_BodyFsm.Tick(dt);
            MoveAndSlide();

            // 落地归零跳跃次数。必须带 "Velocity.Y >= 0"：起跳那一帧角色可能还没离开地面，
            // 只判 IsOnFloor() 会把刚用掉的次数立刻清零，变成无限跳。
            if (IsOnFloor() && Velocity.Y >= 0)
            {
                ActionData.JumpCount = 0;
            }
        }

        /// <inheritdoc />
        protected override bool ApplyDamage(int amount) => this.GetSystem<HeroSystem>().Damage(amount);
        /// <inheritdoc />
        protected override void RestoreHealth(int amount) => this.GetSystem<HeroSystem>().Heal(amount);

        /// <summary>在成长模型确认升级并更新属性后显示提示；普通进关不触发。</summary>
        public void PlayLevelUp() => m_LevelUp?.ShowLevelUp();

        /// <summary>只释放本次开启拥有的暂停；隐藏时对称清理，关停时只清字段。</summary>
        /// <param name="resume">是否仍可访问框架恢复游戏。</param>
        private void EndMusouIntro(bool resume)
        {
            if (m_MusouIntroEnds == 0)
                return;
            m_MusouIntroEnds = 0;
            if (resume && GF.Base.IsGamePaused)
                GF.Base.ResumeGame();
        }

        // ---- 业务入口 ----

        /// <summary>存活且蓄满时开启无双，使出招与运动读取加成并开始角色残影。</summary>
        /// <returns>本次是否成功开启；按住按键、未满值、已开启或已死亡均不重复开启。</returns>
        public bool TryActivateMusou()
        {
            BattleConfig battle = this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.Data;
            if (!IsShown || Dead || GF.Base.IsGamePaused || !this.GetSystem<HeroSystem>().TryActivateWs())
                return false;
            m_PendingHurt = null;
            m_MusouAfterimage?.SetEmitting(true);
            m_MusouIntroEnds = Time.GetTicksMsec() + (ulong)(battle.WsIntroPauseDuration * 1000f);
            GF.Base.PauseGame();
            return true;
        }

        /// <summary>移除局内加成并结束残影采样，不影响成长或装备来源。</summary>
        private void EndMusou()
        {
            EndMusouIntro(true);
            this.GetSystem<HeroSystem>().ClearWs();
            m_MusouAfterimage?.SetEmitting(false);
        }

        /// <summary>读取当前穿戴，仅刷新身体和武器视觉，不修改属性。</summary>
        public void RefreshAppearance()
        {
            var equipment = this.GetModel<ProfileModel>();
            ApplyAppearance(equipment.Weapon != null, equipment.Armor != null);
        }

        /// <inheritdoc />
        protected override CombatantAttributes CaptureCombatAttributes() => Attributes.CaptureCombatAttributes(Id);

        /// <summary>按装配独立选择身体与武器外观。</summary>
        /// <param name="hasWeapon">是否穿戴武器。</param>
        /// <param name="hasArmor">是否穿戴防具。</param>
        private void ApplyAppearance(bool hasWeapon, bool hasArmor) =>
            m_Appearance.ApplyToSprites((Sprite2D)Body, m_Weapon, hasWeapon, hasArmor);

        // ---- 内部方法与扩展点 ----

        /// <summary>同步镜像英雄武器及特效，身体层由公共实体镜像。</summary>
        /// <param name="dir">朝向：1 右，-1 左。</param>
        protected override void OnFacingChanged(int dir)
        {
            bool mirror = dir > 0;
            m_Weapon.FlipH = mirror;
            // 特效层用父容器负 scale 镜像（同旧项目 Action.scale.x = ±1）：
            // 这样特效节点上由轨道写入的 offset 会一起镜像，特效不会跑到身体另一侧。
            if (m_EffectRoot != null)
            {
                m_EffectRoot.Scale = new Vector2(mirror ? -1 : 1, 1);
            }
        }

        /// <summary>
        /// 命中收益（英雄专属）：按实际伤害相对出招时攻击力计算官方无双增长。
        /// 收益规则属于英雄，不进攻击包、不进 AbstractActorEntity（怪物没有无双值）。
        /// </summary>
        /// <param name="attack">本次攻击快照，仅在回调内使用。</param>
        /// <param name="result">目标已完成的伤害结算结果。</param>
        protected override void OnHitLanded(AttackInstance attack, DamageResult result)
        {
            this.GetSystem<HeroSystem>().RecordHit(result.Damage, attack.Attacker.Power);
        }

        /// <summary>
        /// 受击：只登记事实（击退速度），何时生效由身体状态决定——
        /// 普通受击立即打断动作，无双霸体不登记硬直；连段与冷却由英雄持有，受击不清空。
        /// 死亡由 ReceiveHit 置位 Dead，身体状态机下一帧进入死亡。
        /// 没有 hurt 动画（时长无从谈起）时不登记。
        /// </summary>
        /// <param name="attack">本次攻击快照，不得保留池化引用。</param>
        /// <param name="result">已完成的伤害结算结果。</param>
        /// <param name="knockback">击退速度，单位像素每秒。</param>
        /// <param name="attackerEntityId">攻击方运行编号；无来源时为零。</param>
        protected override void OnHurt(AttackInstance attack, DamageResult result, Vector2 knockback,
            int attackerEntityId)
        {
            if (Dead)
                EndMusou();
            if (!Dead && !this.GetModel<HeroModel>().WsActive.Value && BodyParams.HurtTime > 0f)
            {
                m_PendingHurt = knockback;
            }
        }

        /// <summary>由具体英雄给出自己的身体状态集合。</summary>
        /// <returns>本英雄使用的全部身体状态。</returns>
        protected abstract FsmState<IHeroBody>[] CreateBodyStates();

        /// <summary>每次显示重建身体状态机，从初始状态起步。</summary>
        private void CreateBodyStateMachine()
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
        private void DestroyBodyStateMachine(bool isShutdown)
        {
            if (m_BodyFsm != null && !isShutdown && !m_BodyFsm.IsDestroyed)
            {
                GF.Fsm.DestroyFsm(m_BodyFsm);
            }

            m_BodyFsm = null;
        }

        /// <summary>构建身体状态机所需的配置、动画名和时长定义（扩展点：远程角色可改写移动/攻击参数）。</summary>
        /// <returns>身体状态机使用的共享身体参数。</returns>
        protected virtual HeroBodyParams BuildBodyParams()
        {
            // 先读取每段普攻动画及其时长，缺失动画直接记录配置错误。
            string[] attackAnims = new string[OwnAttacks.Count];
            float[] attackTimes = new float[OwnAttacks.Count];
            for (int i = 0; i < OwnAttacks.Count; i++)
            {
                attackAnims[i] = OwnAttacks[i].Animation;
                attackTimes[i] = GetAnimLength(attackAnims[i]);
                if (string.IsNullOrEmpty(attackAnims[i]))
                {
                    Log.Error("[AbstractHeroEntity] AttackConfig {0} 的 Animation 列为空（连段第 {1} 段无法播放）",
                        OwnAttacks[i].Id, i + 1);
                }
            }

            // 汇总移动、跳跃与受击参数；待机时序由完整循环动画负责。
            return new HeroBodyParams
            {
                WalkSpeed = Config.WalkSpeed,
                RunDoubleTapWindow = Config.RunDoubleTapWindow,
                RunSpeed = Config.RunSpeed,
                JumpSpeed = Config.JumpSpeed,
                Gravity = Config.Gravity,
                JumpCountMax = Config.JumpCountMax,
                HurtTime = GetAnimLength(HeroAnims.Hurt),
                AttackAnims = Array.AsReadOnly(attackAnims),
                AttackTimes = Array.AsReadOnly(attackTimes),
                AttackInputCooldowns = Array.AsReadOnly(Config.AttackInputCooldowns),
                AirAttackCooldown = Config.AirAttackCooldown,
                ComboResetTime = Config.ComboResetTime,
            };
        }

        /// <summary>S＋K 仅从单向平台向下离开，不穿越普通地面。</summary>
        /// <returns>是否执行平台下落。</returns>
        bool IHeroBody.TryDropThroughPlatform()
        {
            if (!IsOnFloor())
                return false;
            uint platformMask = LayerMask.LayerToMask2D("Platform");
            for (int i = 0; i < GetSlideCollisionCount(); i++)
            {
                KinematicCollision2D collision = GetSlideCollision(i);
                if (collision.GetNormal().Dot(Vector2.Up) < Mathf.Cos(FloorMaxAngle) ||
                    collision.GetCollider() is not CollisionObject2D collider || (collider.CollisionLayer & platformMask) == 0)
                    continue;
                // 原版从平台向下移 10px；单向碰撞不再阻挡已位于平台下方的脚底。
                Position += Vector2.Down * Config.PlatformDropDistance;
                ActionData.JumpCount = 1;
                return true;
            }
            return false;
        }

        /// <summary>提交攻击段并装填攻击数据和起手音效。</summary>
        /// <param name="segment">英雄连段下标。</param>
        void IActorBody.BeginAttack(int segment)
        {
            AttackSegment = segment;
            // 官方加成乘在整招威力上，包含随机固定加值，避免只放大基础攻击。
            PrepareAttack(OwnAttacks[segment], this.GetSystem<HeroSystem>().AttackPowerMultiplier);
            PlaySound(OwnAttacks[segment].SoundId); // 起手音：音源查表，与旧 add_music 同点（第 0 帧）
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
