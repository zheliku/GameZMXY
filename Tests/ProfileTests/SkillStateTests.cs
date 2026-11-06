using GameLogic.SkillTests;
using System;
using System.Collections.Generic;
using GameConfig.Skills;
using GameFramework.Fsm;
using GameLogic.Skills;
using GameLogic.Heroes.Wukong;
using GameLogic.Heroes.Body;
using GameLogic.Architecture;
using GameLogic.Body;
using GameLogic.Input;
using Godot;
using Xunit;

namespace GameLogic.Profile.Tests
{
    /// <summary>
    /// 真实 GGF 状态验证动作规则；方法键的时序与场景装配由引擎验证，不模拟另一套时间轴。
    /// </summary>
    public sealed class SkillStateTests
    {
        /// <summary>
        /// 突刺按官方四个攻击编号分别结算，段点同时减速，不把子弹的命中预算套在身体攻击上。
        /// </summary>
        [Fact]
        public void Thrust_ConcreteStateOwnsHitPowerAndDeceleration()
        {
            using var skill = new SkillFixture(SkillId.FlameThrust, velocity: new Vector2(120, 0));
            skill.Scope.Hero.Power.Value = 11;
            var state = Assert.IsType<WukongFlameThrustState>(skill.Fsm.CurrentState);
            state.BeginThrust(skill.Actor, 360, 720);
            Assert.Equal(360, skill.Actor.Velocity.X);
            for (int i = 0; i < 3; i++)
            {
                state.ContinueThrust(skill.Actor, .8f);
            }

            Assert.Equal(4, skill.Actor.Attacks.Count);
            Assert.All(skill.Actor.Attacks, hit => Assert.Equal(SkillHitId.FlameThrust, hit));
            Assert.All(skill.Actor.Powers, power => Assert.Equal(6d, power));
            Assert.InRange(skill.Actor.Velocity.X, 184.31, 184.33);
            state.CompleteAnimation();
            skill.Advance(.01f);
            Assert.Null(skill.Scope.Hero.ActiveSkillId);
            Assert.Equal(0, skill.Actor.ActionData.LastGroundSegment);
        }

        /// <summary>
        /// 静止释放火炎突击时仍按角色朝向前冲，不能把零水平速度误判为向左。
        /// </summary>
        [Fact]
        public void Thrust_UsesFacingWhenReleasedFromStandstill()
        {
            using var skill = new SkillFixture(SkillId.FlameThrust, velocity: Vector2.Zero);
            var state = Assert.IsType<WukongFlameThrustState>(skill.Fsm.CurrentState);

            state.BeginThrust(skill.Actor, 360, 720);

            Assert.Equal(360, skill.Actor.Velocity.X);
        }

        /// <summary>
        /// 取消后旧状态不再推进原生动画。
        /// </summary>
        [Fact]
        public void CancelledSkill_DoesNotAdvanceOldAnimation()
        {
            using var skill = new SkillFixture(SkillId.FlameThrust);
            skill.System.CancelSkill();
            skill.Advance(.1f);
            Assert.Equal(0, skill.Actor.Advances);
            Assert.Null(skill.Scope.Hero.ActiveSkillId);
        }

        /// <summary>
        /// 两招分别消耗空中跳跃并读取冷却数值。
        /// </summary>
        /// <param name="id">技能编号。</param>
        [Theory]
        [InlineData(SkillId.RisingDragon)]
        [InlineData(SkillId.FlameStorm)]
        public void Entry_OwnsJumpConsumptionAndCooldown(SkillId id)
        {
            using var skill = new SkillFixture(id, jumpCount: 1);
            Assert.Equal(2, skill.Actor.ActionData.JumpCount);
            Assert.Equal(id == SkillId.RisingDragon ? 21d / 24d : 10d / 24d,
                skill.Actor.ActionData.AttackReadyAt);
        }

        /// <summary>
        /// 中心击退属于风暴；基础威力先取整。
        /// </summary>
        [Fact]
        public void Storm_OwnsCenterAndRounding()
        {
            using var skill = new SkillFixture(SkillId.FlameStorm);
            skill.Scope.Hero.Power.Value = 11;
            skill.Actor.SkillPosition = new Vector2(200, 100);
            Assert.IsType<WukongFlameStormState>(skill.Fsm.CurrentState).BeginHit(skill.Actor);
            Assert.Equal(200f, Assert.Single(skill.Actor.AttackOrigins));
            Assert.Equal(13d, Assert.Single(skill.Actor.Powers));
        }

        /// <summary>
        /// 无敌按游戏秒数开始，并不随动画播放结束而提前清掉。
        /// </summary>
        [Fact]
        public void Flash_StartsInvulnerabilityAtDashPhase()
        {
            using var skill = new SkillFixture(SkillId.BlazingFlash);
            skill.Actor.ActionData.Time = .1;
            Assert.IsType<WukongBlazingFlashState>(skill.Fsm.CurrentState).StartDash(skill.Actor, null, 1080);
            Assert.False(skill.System.IsInvulnerable(.09));
            Assert.True(skill.System.IsInvulnerable(.1));
            Assert.True(skill.System.IsInvulnerable(.59));
            Assert.False(skill.System.IsInvulnerable(.61));
            Assert.Equal(1080, skill.Actor.Velocity.X);
        }

        /// <summary>
        /// 官方悟空技能中只有烈焰闪的冲刺阶段提供无敌；其他技能不能继承上一招的无敌语义。
        /// </summary>
        [Theory]
        [InlineData(SkillId.RisingDragon)]
        [InlineData(SkillId.FlameThrust)]
        [InlineData(SkillId.FlameStorm)]
        [InlineData(SkillId.FireDemonSlash)]
        public void OtherWukongSkills_DoNotStartInvulnerability(SkillId id)
        {
            using var skill = new SkillFixture(id);

            Assert.False(skill.System.IsInvulnerable(skill.Actor.ActionData.Time));
        }

        /// <summary>
        /// 无目标时请求原生下降标记；中断必须解除输入锁。
        /// </summary>
        [Fact]
        public void FireDemon_NoTargetAndCancellationRestoreInput()
        {
            using var skill = new SkillFixture(SkillId.FireDemonSlash);
            var state = Assert.IsType<WukongFireDemonState>(skill.Fsm.CurrentState);
            Assert.True(skill.Actor.ActionData.InputLocked);
            Assert.False(skill.System.IsInvulnerable(skill.Actor.ActionData.Time));
            state.SearchTarget(skill.Actor, null, 130, 20);
            skill.Advance(.01f);
            Assert.Equal("descent", skill.Actor.LastMarker);
            skill.System.CancelSkill();
            skill.Advance(.01f);
            Assert.False(skill.Actor.ActionData.InputLocked);
        }

        /// <summary>
        /// 九连斩与本体时钟并行，火魔三个阶段都沿施放朝向击退。
        /// </summary>
        [Fact]
        public void FireDemon_TargetOwnsTeleportAndParallelCombo()
        {
            using var skill = new SkillFixture(SkillId.FireDemonSlash, target: new Vector2(80, 100));
            var state = Assert.IsType<WukongFireDemonState>(skill.Fsm.CurrentState);
            state.SearchTarget(skill.Actor, null, 130, 20);
            Assert.Equal(skill.Actor.ActionData.Time + 40d / 24d, skill.Actor.ActionData.AttackReadyAt);
            skill.Advance(.01f);
            Assert.Null(skill.Actor.LastMarker);
            skill.Advance(1f);
            Assert.Equal(new Vector2(80, 100), skill.Actor.SkillPosition);
            Assert.Equal(2, skill.Actor.Advances);
            Assert.Equal(SkillHitId.FireDemonCombo, Assert.Single(skill.Actor.Effects));
            Assert.Null(Assert.Single(skill.Actor.EffectOrigins));
            skill.Advance(1f);
            Assert.Equal(3, skill.Actor.Advances);
            state.Descend(skill.Actor, null, new Vector2(720, 480));
            state.Recover(skill.Actor, null);
            Assert.Equal(new[] { SkillHitId.FireDemonCombo, SkillHitId.FireDemonDescent, SkillHitId.FireDemonLanding },
                skill.Actor.Effects);
            Assert.All(skill.Actor.EffectOrigins, origin => Assert.Null(origin));
        }

        /// <summary>
        /// 未提前落地时，第70帧兜底效果只创建一次并直接恢复。
        /// </summary>
        [Fact]
        public void LandingFallback_RecoversImmediatelyAndDoesNotSpawnTwice()
        {
            using var skill = new SkillFixture(SkillId.FireDemonSlash);
            var state = Assert.IsType<WukongFireDemonState>(skill.Fsm.CurrentState);
            state.Recover(skill.Actor, null);
            skill.Advance(.01f);
            skill.Scope.Hero.Mp.Value = 100;
            Assert.Null(skill.Scope.Hero.ActiveSkillId);
            Assert.False(skill.Actor.ActionData.InputLocked);
            Assert.Equal(SkillHitId.FireDemonLanding, Assert.Single(skill.Actor.Effects));
            Assert.Empty(skill.Actor.Completions);
            Assert.True(skill.System.TryActivate(SkillId.FireDemonSlash, skill.Actor));
            skill.Advance(.01f);
            state.Recover(skill.Actor, null);
            skill.Advance(.01f);
            Assert.Null(skill.Scope.Hero.ActiveSkillId);
            Assert.Equal(2, skill.Actor.Effects.Count);
            Assert.Null(skill.Actor.LastMarker);
        }

        /// <summary>
        /// 提前落地冻结身体；独立效果完成后才请求原生恢复标记。
        /// </summary>
        [Fact]
        public void LandingContact_HoldsBodyUntilEffectCompletes()
        {
            using var skill = new SkillFixture(SkillId.FireDemonSlash);
            var state = Assert.IsType<WukongFireDemonState>(skill.Fsm.CurrentState);
            var scene = (PackedScene)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(PackedScene));
            state.OpenLandingWindow(scene);
            skill.Actor.OnFloor = true;
            skill.Advance(.01f);
            Assert.True(skill.Actor.AnimationPaused);
            Assert.Equal(SkillHitId.FireDemonLanding, Assert.Single(skill.Actor.Effects));
            Assert.NotNull(skill.Scope.Hero.ActiveSkillId);
            skill.Advance(.25f);
            Assert.Null(skill.Actor.LastMarker);
            Assert.Single(skill.Actor.Effects);
            skill.Actor.Completions[SkillHitId.FireDemonLanding]();
            skill.Advance(.01f);
            Assert.Equal("recovery", skill.Actor.LastMarker);
            Assert.False(skill.Actor.AnimationPaused);
        }

        /// <summary>
        /// 取消后的落地效果即使迟到，也不得推动复用后的同一技能状态。
        /// </summary>
        [Fact]
        public void LandingCompletion_FromPreviousCastDoesNotAffectReuse()
        {
            using var skill = new SkillFixture(SkillId.FireDemonSlash);
            var state = Assert.IsType<WukongFireDemonState>(skill.Fsm.CurrentState);
            var scene = (PackedScene)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(PackedScene));
            state.OpenLandingWindow(scene);
            skill.Actor.OnFloor = true;
            skill.Advance(.01f);
            Action oldCompletion = skill.Actor.Completions[SkillHitId.FireDemonLanding];
            skill.System.CancelSkill();
            skill.Advance(.01f);
            skill.Scope.Hero.Mp.Value = 100;
            Assert.True(skill.System.TryActivate(SkillId.FireDemonSlash, skill.Actor));
            skill.Advance(.01f);
            oldCompletion();
            skill.Advance(.01f);
            Assert.Null(skill.Actor.LastMarker);
            Assert.NotNull(skill.Scope.Hero.ActiveSkillId);
        }

        /// <summary>
        /// 真实框架状态机夹具；只替换引擎物理与动画节点。
        /// </summary>
        private sealed class SkillFixture : IDisposable
        {
            private readonly FsmManager m_Manager = new(); // 本测试拥有的真实 GGF 状态机管理器。
            public readonly IFsm<IHeroBody> Fsm; // 按物理帧驱动的正式状态集合。

            /// <summary>
            /// 独立模型作用域。
            /// </summary>
            public TestGameScope Scope { get; } = new(1, [(1, 0)], 9999, 20);

            /// <summary>
            /// 角色表现替身。
            /// </summary>
            public Motion Actor { get; }

            /// <summary>
            /// 真实技能系统。
            /// </summary>
            public SkillSystem System
            {
                get
                {
                    return Scope.GetSystem<SkillSystem>();
                }
            }

            /// <summary>
            /// 装配并直接进入指定技能自己的状态。
            /// </summary>
            /// <param name="id">悟空招式。</param>
            /// <param name="target">可选寻敌结果。</param>
            /// <param name="velocity">可选起手速度。</param>
            /// <param name="jumpCount">起手前已使用的跳跃次数。</param>
            public SkillFixture(SkillId id, Vector2? target = null, Vector2? velocity = null, int jumpCount = 0)
            {
                Actor = new Motion(Scope) { Target = target, OnFloor = true, Velocity = velocity ?? Vector2.Zero };
                Actor.ActionData.JumpCount = jumpCount;
                System.SelectSkills(1, SkillTestDefinitions.Wukong());
                System.Learn(id);
                Scope.Hero.Mp.Value = 100;
                Assert.True(System.TryActivate(id, Actor));
                Actor.OnFloor = false;
                Fsm = m_Manager.CreateFsm<IHeroBody>(Guid.NewGuid().ToString(), Actor,
                    new HeroGroundState(), new HeroAirState(), new HeroHurtState(), new HeroDeathState(),
                    new WukongRisingDragonState(), new WukongFlameThrustState(), new WukongBlazingFlashState(),
                    new WukongFlameStormState(), new WukongFireDemonState());
                Fsm.Start(System.BodyStateType);
            }

            /// <summary>
            /// 推进一帧真实身体状态。
            /// </summary>
            /// <param name="dt">游戏秒数。</param>
            public void Advance(float dt)
            {
                Actor.ActionData.Time += dt;
                Fsm.Tick(dt);
                if (Fsm.CurrentState is AbstractHeroSkillState state)
                {
                    state.AdvanceAnimation(Fsm, Actor, dt);
                }
            }

            /// <inheritdoc />
            public void Dispose()
            {
                m_Manager.Shutdown();
            }
        }

        /// <summary>
        /// 只记录物理与特效请求的测试表现替身。
        /// </summary>
        /// <param name="scope">本测试替身借用的独立模型与系统作用域。</param>
        private sealed class Motion(TestGameScope scope) : ISkillActor, IHeroBody
        {
            /// <inheritdoc />
            public bool IsAnimationComplete(string animation) => true;

            /// <inheritdoc />
            IArchitecture IBelongToArchitecture.GetArchitecture()
            {
                return scope;
            }

            /// <inheritdoc />
            public HeroActionData ActionData { get; } = new();

            /// <inheritdoc />
            public HeroBodyParams Params { get; } = new();

            /// <inheritdoc />
            public bool Dead
            {
                get
                {
                    return false;
                }
            }

            /// <inheritdoc />
            public float Gravity
            {
                get
                {
                    return 980;
                }
            }

            /// <inheritdoc />
            public int AttackSegment
            {
                get
                {
                    return -1;
                }
            }

            /// <inheritdoc />
            public bool HasPendingHurt
            {
                get
                {
                    return false;
                }
            }

            /// <inheritdoc />
            public bool TryDropThroughPlatform()
            {
                return false;
            }

            /// <inheritdoc />
            public Vector2 TakePendingHurt()
            {
                return Vector2.Zero;
            }

            /// <inheritdoc />
            public void BeginAttack(int index) { }

            /// <inheritdoc />
            public void EndAttack() { }

            /// <inheritdoc />
            public void SetFacing(int direction) { }

            /// <inheritdoc />
            public void PlayAnim(string animation) { }

            /// <inheritdoc />
            public void RestartAnim(string animation, string startMarker = null) { }

            /// <inheritdoc />
            public void BeginSkillAnimation(string animation, string startMarker = null)
            {
                LastMarker = startMarker;
                AnimationPaused = false;
            }

            /// <inheritdoc />
            public void AdvanceSkillAnimation(float dt)
            {
                Advances++;
            }

            /// <summary>
            /// 夹具保留本体暂停事实，不创建引擎播放器。
            /// </summary>
            public void PauseSkillAnimation()
            {
                AnimationPaused = true;
            }

            /// <summary>
            /// 夹具中的原生暂停事实。
            /// </summary>
            public bool AnimationPaused { get; private set; }

            /// <inheritdoc />
            public void EndSkillAnimation() { }

            /// <inheritdoc />
            public Vector2 SkillPosition { get; set; }

            /// <inheritdoc />
            public Vector2 Velocity { get; set; }

            /// <inheritdoc />
            public bool OnFloor { get; set; }

            /// <inheritdoc />
            public int Facing
            {
                get
                {
                    return 1;
                }
            }

            /// <summary>
            /// 本招产生的独立命中编号请求。
            /// </summary>
            public List<SkillHitId> Attacks { get; } = new();

            /// <summary>
            /// 按实际创建顺序记录技能攻击包的威力。
            /// </summary>
            public List<double> Powers { get; } = new();

            /// <summary>
            /// 具体状态传入的击退中心；为空表示沿出招朝向。
            /// </summary>
            public List<float?> AttackOrigins { get; } = new();

            /// <summary>
            /// 最近应用到角色动画的秒数。
            /// </summary>
            public int Advances { get; private set; }

            /// <summary>
            /// 测试动画播放器最近一次跳转的阶段标记。
            /// </summary>
            public string LastMarker { get; private set; }

            /// <summary>
            /// 测试可延后触发的独立效果完成通知。
            /// </summary>
            public Dictionary<SkillHitId, Action> Completions { get; } = new();

            /// <summary>
            /// 测试提供的首个有效目标。
            /// </summary>
            public Vector2? Target { get; init; }

            /// <summary>
            /// 已请求的特效。
            /// </summary>
            public List<SkillHitId> Effects { get; } = new();

            /// <summary>
            /// 具体招式提供的特效击退中心；空值沿出招朝向。
            /// </summary>
            public List<float?> EffectOrigins { get; } = new();

            /// <inheritdoc />
            public Vector2? FindSkillTarget(float range, float inset)
            {
                return Target;
            }

            /// <inheritdoc />
            public void NewSkillAttack(SkillHitId hit, double power, float? knockbackOriginX = null)
            {
                Attacks.Add(hit);
                Powers.Add(power);
                AttackOrigins.Add(knockbackOriginX);
            }

            /// <inheritdoc />
            public void PlaySkillEffect(PackedScene scene, Vector2 position, SkillHitId hit = SkillHitId.None, double power = 0, Action completed = null,
                float? knockbackOriginX = null)
            {
                Effects.Add(hit);
                EffectOrigins.Add(knockbackOriginX);
                if (completed != null)
                {
                    Completions[hit] = completed;
                }
            }
        }
    }
}
