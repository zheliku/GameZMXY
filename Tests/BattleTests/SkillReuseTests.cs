using GameLogic.SkillTests;
using System;
using System.Collections.Generic;
using GameConfig.Skills;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Body;
using GameLogic.Config;
using GameLogic.Heroes.Body;
using GameLogic.Heroes.Wukong;
using GameLogic.Input;
using GameLogic.Profile;
using GameLogic.Skills;
using Godot;
using Xunit;

namespace GameLogic.Battle.Tests;

/// <summary>验证输入槽、施放锁和共享身体状态不依赖悟空的具体招式规则。</summary>
public sealed class SkillReuseTests
{
    /// <summary>同一个输入槽可以由不同宿主映射到不同招式，共用状态不能替宿主选择悟空技能。</summary>
    /// <param name="skill">测试宿主为槽 1 选择的招式。</param>
    [Theory]
    [InlineData(SkillId.BlazingFlash)]
    [InlineData(SkillId.FlameStorm)]
    [InlineData(SkillId.FireDemonSlash)]
    public void SharedStates_UseHostSkillBinding(SkillId skill)
    {
        var scope = new SkillScope();
        scope.Skills.GetLearnedSkills(1).Add(skill);
        var hero = new SkillHero(scope) { Slot1Skill = skill };
        scope.GetSystem<SkillSystem>().SelectSkillSet(new SkillSet(1,
            new SkillDefinition(skill, _ => new TestExecution(skill), 1, typeof(TestSkillState))), hero);
        var source = new FrameInput();
        source.Pressed.Add(InputSystem.Skill1);
        new InputSystem(scope, source).Sample(hero, 1f / 60);
        using var manager = new FsmLifetime();
        var fsm = manager.Value.CreateFsm<IHeroBody>(Guid.NewGuid().ToString(), hero,
            new HeroGroundState(), new HeroAirState(), new TestSkillState(), new HeroHurtState(), new HeroDeathState());
        fsm.Start<HeroGroundState>();
        fsm.Tick(1f / 60);
        Assert.IsType<TestSkillState>(fsm.CurrentState);
        Assert.Equal(skill, scope.Skills.BodySkill.Id);
        Assert.Equal(100 - scope.Configuration.Tables.TbSkillConfig.Get(skill).ManaCost,
            scope.GetModel<HeroModel>().Mp.Value);
        Assert.Null(hero.ActionData.DirectSkillSlot);
    }

    /// <summary>输入层只报告槽位，按住或拒绝后不在后续帧补发技能。</summary>
    /// <param name="action">直接技能动作。</param>
    /// <param name="slot">对应槽位。</param>
    [Theory]
    [InlineData(InputSystem.Skill1, 1)]
    [InlineData(InputSystem.Skill2, 2)]
    [InlineData(InputSystem.Skill3, 3)]
    public void DirectSkills_ReportSlotsWithoutBindingToHero(string action, int slot)
    {
        var scope = new SkillScope();
        var hero = new SkillHero(scope);
        var source = new FrameInput();
        var input = new InputSystem(scope, source);
        source.Pressed.Add(action);
        source.Held.Add(action);
        input.Sample(hero, 1f / 60);
        Assert.Equal(slot, hero.ActionData.DirectSkillSlot);
        source.Pressed.Clear();
        input.Sample(hero, 1f / 60);
        Assert.Null(hero.ActionData.DirectSkillSlot);
    }

    /// <summary>暂停重力不隐含输入锁，输入锁也不依赖重力；锁内输入不排队。</summary>
    /// <param name="gravitySuspended">是否暂停重力。</param>
    /// <param name="inputLocked">是否锁定输入。</param>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void InputLock_IsIndependentOfGravity(bool gravitySuspended, bool inputLocked)
    {
        var scope = new SkillScope();
        var hero = new SkillHero(scope);
        scope.Skills.BodySkill = new TestExecution(SkillId.RisingDragon)
        {
            TestGravitySuspended = gravitySuspended,
            TestInputLocked = inputLocked
        };
        var source = new FrameInput();
        var input = new InputSystem(scope, source);
        source.Pressed.UnionWith([InputSystem.Skill1, InputSystem.Musou]);
        source.Held.Add(InputSystem.Attack);
        input.Sample(hero, .1f);
        Assert.InRange(hero.ActionData.Time, .0999, .1001);
        Assert.Equal(inputLocked ? null : (int?)1, hero.ActionData.DirectSkillSlot);
        Assert.Equal(!inputLocked, hero.ActionData.MusouRequested);

        if (!inputLocked)
            return;
        ((TestExecution)scope.Skills.BodySkill).Unlock();
        source.Pressed.Clear();
        input.Sample(hero, .1f);
        Assert.Null(hero.ActionData.DirectSkillSlot);
        Assert.False(input.ConsumeAttack(hero.ActionData));
        source.Held.Clear();
        input.Sample(hero, .1f);
        source.Held.Add(InputSystem.Attack);
        input.Sample(hero, .1f);
        Assert.True(input.ConsumeAttack(hero.ActionData));
    }

    /// <summary>即使复用升龙动作数据，共用状态也只执行宿主自己的移动与起手规则。</summary>
    [Fact]
    public void SharedSkillState_UsesHostMovementAndReleasesInterruptedExecution()
    {
        var scope = new SkillScope();
        var hero = new SkillHero(scope);
        scope.Skills.GetLearnedSkills(1).Add(SkillId.RisingDragon);
        scope.GetSystem<SkillSystem>().SelectSkillSet(new SkillSet(1,
            new SkillDefinition(SkillId.RisingDragon, _ => new TestExecution(SkillId.RisingDragon), BodyState: typeof(TestSkillState))), hero);
        Assert.True(scope.GetSystem<SkillSystem>().TryActivate(SkillId.RisingDragon, hero, 0));
        using var manager = new FsmLifetime();
        var fsm = manager.Value.CreateFsm<IHeroBody>(Guid.NewGuid().ToString(), hero,
            new HeroGroundState(), new HeroAirState(), new TestSkillState(), new HeroHurtState(), new HeroDeathState());
        fsm.Start<TestSkillState>();
        hero.ActionData.Time = .01f;
        fsm.Tick(.01f);
        Assert.Equal(1, hero.SkillBegins);
        Assert.Equal(321, hero.Velocity.X);
        Assert.NotNull(scope.Skills.BodySkill);
        hero.PendingHurt = new Vector2(-100, 0);
        hero.ActionData.Time += .01f;
        fsm.Tick(.01f);
        Assert.IsType<HeroHurtState>(fsm.CurrentState);
        Assert.Equal(1, hero.SkillEnds);
        Assert.Null(scope.Skills.BodySkill);
    }

    /// <summary>真实 GGF 先执行普攻离开，再开始技能动画；避免旧攻击清理掉新技能判定。</summary>
    [Fact]
    public void AttackToSkill_EndsNormalAttackBeforeStartingSkill()
    {
        var scope = new SkillScope();
        var hero = new SkillHero(scope) { Slot1Skill = SkillId.RisingDragon, AllowSkillDuringAttack = true };
        var skills = scope.GetSystem<SkillSystem>();
        scope.Skills.GetLearnedSkills(1).Add(SkillId.RisingDragon);
        skills.SelectSkillSet(new SkillSet(1, new SkillDefinition(SkillId.RisingDragon,
            _ => new TestExecution(SkillId.RisingDragon), 1, typeof(TestSkillState))), hero);
        hero.ActionData.AttackSegment = 0;
        using var manager = new FsmLifetime();
        var fsm = manager.Value.CreateFsm<IHeroBody>(Guid.NewGuid().ToString(), hero,
            new HeroAttackState(), new TestSkillState(), new HeroGroundState(), new HeroAirState(),
            new HeroHurtState(), new HeroDeathState());
        fsm.Start<HeroAttackState>();
        hero.ActionData.DirectSkillSlot = 1;
        hero.ActionData.Time = .1;
        fsm.Tick(.1f);
        Assert.IsType<TestSkillState>(fsm.CurrentState);
        Assert.Equal(new[] { "end_normal", "begin_skill" }, hero.Operations);
        Assert.Equal(1, hero.SkillBegins);
    }

    /// <summary>五招分别进入自己登记的 GGF 状态，完成后回移动状态，模型执行和表现各清理一次。</summary>
    /// <param name="id">悟空招式。</param>
    /// <param name="stateType">招式自己的状态类型。</param>
    [Theory]
    [InlineData(SkillId.RisingDragon, typeof(WukongRisingDragonState))]
    [InlineData(SkillId.FlameThrust, typeof(WukongFlameThrustState))]
    [InlineData(SkillId.BlazingFlash, typeof(WukongBlazingFlashState))]
    [InlineData(SkillId.FlameStorm, typeof(WukongFlameStormState))]
    [InlineData(SkillId.FireDemonSlash, typeof(WukongFireDemonState))]
    public void WukongSkills_UseSeparateGgfStatesAndFinish(SkillId id, Type stateType)
    {
        var scope = new SkillScope();
        var hero = new SkillHero(scope) { FinishLandingEffect = true };
        var skills = scope.GetSystem<SkillSystem>();
        scope.Skills.GetLearnedSkills(1).Add(id);
        skills.SelectSkillSet(SkillTestDefinitions.Wukong(), hero);
        Assert.True(skills.TryActivate(id, hero, 0));
        var execution = skills.BodySkill;
        Assert.Equal(0, hero.SkillBegins);
        using var manager = new FsmLifetime();
        var fsm = manager.Value.CreateFsm<IHeroBody>(Guid.NewGuid().ToString(), hero,
            new HeroGroundState(), new HeroAirState(), new HeroHurtState(), new HeroDeathState(),
            new WukongRisingDragonState(), new WukongFlameThrustState(), new WukongBlazingFlashState(),
            new WukongFlameStormState(), new WukongFireDemonState());
        fsm.Start<HeroGroundState>();
        hero.ActionData.Time = .01;
        fsm.Tick(.01f);
        Assert.IsType(stateType, fsm.CurrentState);
        Assert.Equal(1, hero.SkillBegins);
        for (int frame = 1; frame <= 600 && !execution.Completed; frame++)
        {
            hero.ActionData.Time += 1d / 60;
            fsm.Tick(1f / 60);
        }
        Assert.True(execution.Completed);
        Assert.IsType<HeroGroundState>(fsm.CurrentState);
        Assert.Null(skills.BodySkill);
        Assert.Equal(1, hero.SkillEnds);
    }

    /// <summary>升龙自己的状态保留起手方向，松开再按也不会在本次招式内恢复横移。</summary>
    [Fact]
    public void RisingDragon_StopsStartingMovementOnRelease()
    {
        var scope = new SkillScope();
        var hero = new SkillHero(scope);
        scope.Skills.GetLearnedSkills(1).Add(SkillId.RisingDragon);
        var skills = scope.GetSystem<SkillSystem>();
        skills.SelectSkillSet(SkillTestDefinitions.Wukong(), hero);
        hero.ActionData.MoveAxis = 1;
        hero.ActionData.RightHeld = true;
        Assert.True(skills.TryActivate(SkillId.RisingDragon, hero, 0));
        using var manager = new FsmLifetime();
        var fsm = manager.Value.CreateFsm<IHeroBody>(Guid.NewGuid().ToString(), hero,
            new WukongRisingDragonState(), new HeroGroundState(), new HeroAirState(),
            new HeroHurtState(), new HeroDeathState());
        fsm.Start<WukongRisingDragonState>();
        hero.ActionData.Time = .01;
        fsm.Tick(.01f);
        Assert.Equal(hero.Params.WalkSpeed, hero.Velocity.X);
        hero.ActionData.RightHeld = false;
        hero.ActionData.Time += .01;
        fsm.Tick(.01f);
        Assert.Equal(0, hero.Velocity.X);
        hero.ActionData.RightHeld = true;
        hero.ActionData.Time += .01;
        fsm.Tick(.01f);
        Assert.Equal(0, hero.Velocity.X);
    }

    /// <summary>多阶段技能可以用自己的 GGF 状态转换，转交同一次执行，最终退出才释放身体。</summary>
    [Fact]
    public void SkillPhaseTransition_PreservesExecutionAndChargesOnlyOnce()
    {
        var scope = new SkillScope();
        var hero = new SkillHero(scope);
        var skills = scope.GetSystem<SkillSystem>();
        scope.Skills.GetLearnedSkills(1).Add(SkillId.RisingDragon);
        skills.SelectSkillSet(new SkillSet(1, new SkillDefinition(SkillId.RisingDragon,
            _ => new TestExecution(SkillId.RisingDragon), 1, typeof(TestPreparationState))), hero);
        Assert.True(skills.TryActivate(SkillId.RisingDragon, hero, 0));
        var execution = skills.BodySkill;
        int mana = scope.GetModel<HeroModel>().Mp.Value;
        using var manager = new FsmLifetime();
        var fsm = manager.Value.CreateFsm<IHeroBody>(Guid.NewGuid().ToString(), hero,
            new TestPreparationState(), new TestReleaseState(), new HeroGroundState(), new HeroAirState(),
            new HeroHurtState(), new HeroDeathState());
        fsm.Start<TestPreparationState>();
        hero.ActionData.SetSkillButton(1, new(true, true, false));
        skills.HandleInput(hero, hero.ActionData);
        hero.ActionData.Time = .01;
        fsm.Tick(.01f);
        Assert.IsType<TestReleaseState>(fsm.CurrentState);
        Assert.Same(execution, skills.BodySkill);
        Assert.Equal(1, hero.SkillBegins);
        Assert.Equal(0, hero.SkillEnds);
        Assert.Equal(mana, scope.GetModel<HeroModel>().Mp.Value);
        hero.ActionData.Time += .3;
        fsm.Tick(.3f);
        Assert.IsType<HeroGroundState>(fsm.CurrentState);
        Assert.Null(skills.BodySkill);
        Assert.Equal(1, hero.SkillEnds);
    }

    /// <summary>本组测试只提供推进时间轴必需的模型与系统。</summary>
    private sealed class SkillScope : IArchitecture
    {
        private readonly HeroModel m_Hero = new(); // 本测试的数值来源。
        private readonly ProfileModel m_Profile = new(1, [(1, 0)],
            new ExperienceCurve(BattleArchitecture.Instance.GetModel<ConfigurationModel>().Tables.TbHeroLevelConfig.DataList), 20); // 独立出战身份。
        private readonly InputSystem m_Input; // 共用的正式输入规则。
        private readonly SkillSystem m_System; // 共用的正式时间轴规则。
        private readonly HeroSystem m_HeroSystem; // 扣蓝使用正式业务入口。

        /// <summary>借用正式配置，测试不复制数值。</summary>
        public ConfigurationModel Configuration => BattleArchitecture.Instance.GetModel<ConfigurationModel>();
        /// <summary>本测试的独立施放。</summary>
        public SkillModel Skills { get; } = new([1]);

        /// <summary>装配规则系统。</summary>
        public SkillScope()
        {
            m_Input = new(this);
            m_System = new(this);
            m_HeroSystem = new(this);
            m_Hero.Mp.Value = 100;
        }

        /// <inheritdoc />
        public TModel GetModel<TModel>() where TModel : class, IModel =>
            (typeof(TModel) == typeof(ConfigurationModel) ? (IModel)Configuration :
                typeof(TModel) == typeof(SkillModel) ? Skills :
                typeof(TModel) == typeof(ProfileModel) ? m_Profile : m_Hero) as TModel;
        /// <inheritdoc />
        public TSystem GetSystem<TSystem>() where TSystem : class, ISystem =>
            (typeof(TSystem) == typeof(InputSystem) ? (ISystem)m_Input :
                typeof(TSystem) == typeof(HeroSystem) ? m_HeroSystem : m_System) as TSystem;
        /// <inheritdoc />
        public TUtility GetUtility<TUtility>() where TUtility : class, IUtility => null;
    }

    /// <summary>独立角色策略替身，刻意使用不同于悟空的技能移动。</summary>
    private sealed class SkillHero(SkillScope scope) : AbstractFakeActorBody, IHeroBody, ISkillActor
    {
        /// <inheritdoc />
        public HeroBodyParams Params { get; } = new()
        {
            WalkSpeed = 144,
            RunSpeed = 240,
            HurtTime = .2f,
            AttackTimes = [1],
            AttackAnims = ["attack_1"]
        };
        /// <inheritdoc />
        public HeroActionData ActionData { get; } = new();
        /// <inheritdoc />
        public Vector2 SkillPosition { get; set; }
        /// <inheritdoc />
        int ISkillActor.Facing => Facing;
        /// <summary>技能起手次数。</summary>
        public int SkillBegins { get; private set; }
        /// <summary>技能表现释放次数。</summary>
        public int SkillEnds { get; private set; }
        /// <summary>测试宿主自行决定槽 1 的招式。</summary>
        public SkillId? Slot1Skill { get; init; }
        /// <summary>测试专用的普攻取消策略。</summary>
        public bool AllowSkillDuringAttack { get; init; }
        /// <summary>测试是否自动完成落地特效回调。</summary>
        public bool FinishLandingEffect { get; init; }
        /// <summary>记录生命周期顺序。</summary>
        public List<string> Operations { get; } = new();
        /// <inheritdoc />
        IArchitecture IBelongToArchitecture.GetArchitecture() => scope;
        /// <inheritdoc />
        public bool TryDropThroughPlatform() => false;
        /// <inheritdoc />
        public void ProcessSkillInput(bool attacking, float normalRatio, bool airborne)
        {
            SkillId? requested = (!attacking || AllowSkillDuringAttack) && ActionData.DirectSkillSlot == 1 ? Slot1Skill : null;
            ActionData.DirectSkillSlot = null;
            if (requested is { } id)
                scope.GetSystem<SkillSystem>().TryActivate(id, this, ActionData.Time);
        }
        /// <inheritdoc />
        public bool TryStartAttack(bool attacking, float normalRatio, bool airborne) => false;
        /// <inheritdoc />
        public void BeginSkillAnimation(SkillActionConfig action)
        {
            Operations.Add("begin_skill");
            SkillBegins++;
        }
        /// <inheritdoc />
        void IActorBody.EndAttack()
        {
            Operations.Add("end_normal");
            EndAttack();
        }
        /// <inheritdoc />
        public void SetSkillAnimationTime(double seconds) { }
        /// <inheritdoc />
        public void EndSkillAnimation() => SkillEnds++;
        /// <inheritdoc />
        public Vector2? FindSkillTarget(float range, float inset) => null;
        /// <inheritdoc />
        public void NewSkillAttack(SkillHitId hit) { }
        /// <inheritdoc />
        public void SpawnSkillEffect(SkillEffectId effect, SkillExecution execution, Vector2 position, Action completed = null)
        {
            if (FinishLandingEffect && effect == SkillEffectId.FireDemonLanding)
                completed?.Invoke();
        }
    }

    /// <summary>由测试宿主登记的具名技能状态，复用公共身体驱动。</summary>
    private sealed class TestSkillState : AbstractHeroSkillState { }

    /// <summary>示例起手阶段，第二次按下后转交执行给本技能的释放阶段。</summary>
    private sealed class TestPreparationState : AbstractHeroSkillState
    {
        private bool m_Transferring; // 是否正向同一技能的下一阶段转交身体。
        /// <inheritdoc />
        protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
        {
            m_Transferring = false;
            base.Enter(fsm, body);
        }
        /// <inheritdoc />
        protected override void Tick(IFsm<IHeroBody> fsm, IHeroBody body, float dt)
        {
            if (body.ActionData.GetSkillButton(1).Pressed)
            {
                m_Transferring = true;
                ChangeState<TestReleaseState>(fsm);
                return;
            }
            base.Tick(fsm, body, dt);
        }
        /// <inheritdoc />
        protected override void Leave(IFsm<IHeroBody> fsm, IHeroBody body, bool isShutdown)
        {
            if (m_Transferring)
                Execution = null;
            else
                base.Leave(fsm, body, isShutdown);
        }
    }

    /// <summary>示例释放阶段，使用原执行身份，完成后按公共生命周期退出。</summary>
    private sealed class TestReleaseState : AbstractHeroSkillState
    {
        private float m_Age; // 本阶段已经推进的游戏秒数。
        /// <inheritdoc />
        protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
        {
            m_Age = 0;
            base.Enter(fsm, body);
        }
        /// <inheritdoc />
        protected override void AdvanceSkill(IHeroBody body, float dt)
        {
            m_Age += dt;
            Execution.Completed = m_Age >= .2f;
        }
    }

    /// <summary>测试自己的技能行为，共用状态不预设悟空移动或第二跳规则。</summary>
    private sealed class TestExecution(SkillId id) : SkillExecution(id)
    {
        /// <summary>设置测试输入锁。</summary>
        public bool TestInputLocked { init => InputLocked = value; }
        /// <summary>设置测试重力策略。</summary>
        public bool TestGravitySuspended { init => GravitySuspended = value; }
        /// <summary>解除测试锁。</summary>
        public void Unlock() => InputLocked = false;
        /// <inheritdoc />
        public override void Start(ISkillActor actor, double now) => actor.BeginSkillAnimation(null);
        /// <inheritdoc />
        public override void Advance(ISkillActor actor, float dt) => actor.Velocity = new Vector2(321, actor.Velocity.Y);
    }

    /// <summary>提供可重复的按键事实。</summary>
    private sealed class FrameInput : IInputSource
    {
        /// <summary>当前按住的动作。</summary>
        public HashSet<string> Held { get; } = new();
        /// <summary>本帧按下的动作。</summary>
        public HashSet<string> Pressed { get; } = new();
        /// <inheritdoc />
        public bool IsHeld(string action) => Held.Contains(action);
        /// <inheritdoc />
        public bool WasPressed(string action) => Pressed.Contains(action);
        /// <inheritdoc />
        public bool WasReleased(string action) => false;
    }

    /// <summary>即使断言失败也归还真实框架状态。</summary>
    private sealed class FsmLifetime : IDisposable
    {
        /// <summary>本测试独占的状态机管理器。</summary>
        public FsmManager Value { get; } = new();
        /// <inheritdoc />
        public void Dispose() => Value.Shutdown();
    }
}
