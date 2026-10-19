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
        var hero = new SkillHero(scope);
        scope.GetSystem<SkillSystem>().SelectSkills(1, new SkillDefinition(skill, typeof(TestSkillState), InputSystem.Skill1));
        var source = scope.Source;
        source.Pressed.Add(InputSystem.Skill1);
        new InputSystem(scope, source).Sample(hero, 1f / 60);
        using var manager = new FsmLifetime();
        var fsm = manager.Value.CreateFsm<IHeroBody>(Guid.NewGuid().ToString(), hero,
            new HeroGroundState(), new HeroAirState(), new TestSkillState(), new HeroHurtState(), new HeroDeathState());
        fsm.Start<HeroGroundState>();
        Tick(fsm, 1f / 60);
        Assert.IsType<TestSkillState>(fsm.CurrentState);
        Assert.Equal(skill, scope.Hero.ActiveSkillId.Value);
        Assert.Equal(100 - scope.Configuration.Tables.TbSkillConfig.Get(skill).ManaCost,
            scope.GetModel<HeroModel>().Mp.Value);
        Assert.False(scope.GetSystem<InputSystem>().ConsumePress(hero.ActionData, InputSystem.Skill1));
    }

    /// <summary>任意动作名使用相同采样与消费，不受三个默认快捷键限制，按住不伪造再次按下。</summary>
    /// <param name="action">任意动作名。</param>
    [Theory]
    [InlineData(InputSystem.Skill1)]
    [InlineData("skill_4")]
    [InlineData("skill_9")]
    [InlineData(InputSystem.InventoryToggle)]
    public void Actions_KeepIdentityAndConsumeOnlyOncePerFrame(string action)
    {
        var scope = new SkillScope();
        var hero = new SkillHero(scope);
        var source = scope.Source;
        var input = scope.GetSystem<InputSystem>();
        source.Pressed.Add(action);
        source.Held.Add(action);
        input.Sample(hero, 1f / 60);
        Assert.True(input.IsActionJustPressed(action));
        Assert.True(input.IsActionPressed(action));
        Assert.False(input.IsActionJustReleased(action));
        Assert.True(input.ConsumePress(hero.ActionData, action));
        Assert.False(input.ConsumePress(hero.ActionData, action));
        source.Pressed.Clear();
        input.Sample(hero, 1f / 60);
        Assert.False(input.IsActionJustPressed(action));
        Assert.True(input.IsActionPressed(action));
        Assert.False(input.ConsumePress(hero.ActionData, action));
    }

    /// <summary>完整登记五个直接动作，第五个可以经正式激活规则进入真实 GGF 技能状态。</summary>
    [Fact]
    public void FifthBinding_ActivatesWithoutChangingSharedInputCode()
    {
        var scope = new SkillScope();
        var hero = new SkillHero(scope);
        SkillId[] ids = [SkillId.RisingDragon, SkillId.FlameThrust, SkillId.BlazingFlash,
            SkillId.FlameStorm, SkillId.FireDemonSlash];
        var definitions = new SkillDefinition[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
            scope.Skills.GetLearnedSkills(1).Add(id);
            definitions[i] = new(id, typeof(TestSkillState), $"skill_{i + 1}");
        }
        var skills = scope.GetSystem<SkillSystem>();
        skills.SelectSkills(1, definitions);
        scope.Source.Pressed.Add("skill_5");
        scope.GetSystem<InputSystem>().Sample(hero, .01f);
        using var manager = new FsmLifetime();
        var fsm = manager.Value.CreateFsm<IHeroBody>(Guid.NewGuid().ToString(), hero,
            new HeroGroundState(), new HeroAirState(), new TestSkillState(), new HeroHurtState(), new HeroDeathState());
        fsm.Start<HeroGroundState>();
        Tick(fsm, .01f);
        Assert.IsType<TestSkillState>(fsm.CurrentState);
        Assert.Equal(SkillId.FireDemonSlash, scope.Hero.ActiveSkillId.Value);
        Assert.Equal(100 - scope.Configuration.Tables.TbSkillConfig.Get(SkillId.FireDemonSlash).ManaCost,
            scope.GetModel<HeroModel>().Mp.Value);
    }

    /// <summary>输入系统不持有全局消费标记，采样第二个角色不会清除第一个角色的消费记录。</summary>
    [Fact]
    public void ButtonConsumption_IsIsolatedBetweenHeroes()
    {
        var scope = new SkillScope();
        var first = new SkillHero(scope);
        var second = new SkillHero(scope);
        var input = scope.GetSystem<InputSystem>();
        scope.Source.Pressed.Add("custom_action");
        input.Sample(first, .01f);
        Assert.True(input.ConsumePress(first.ActionData, "custom_action"));
        input.Sample(second, .01f);
        Assert.False(input.ConsumePress(first.ActionData, "custom_action"));
        Assert.True(input.ConsumePress(second.ActionData, "custom_action"));
    }

    /// <summary>暂停重力不隐含输入锁，输入锁也不依赖重力；锁内输入不排队。</summary>
    /// <param name="inputLocked">是否锁定输入。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InputLock_IsOwnedByHeroInputData(bool inputLocked)
    {
        var scope = new SkillScope();
        var hero = new SkillHero(scope);
        scope.Hero.ActiveSkillId = SkillId.RisingDragon;
        hero.ActionData.InputLocked = inputLocked;
        var source = scope.Source;
        var input = new InputSystem(scope, source);
        source.Pressed.UnionWith([InputSystem.Skill1, InputSystem.Musou]);
        source.Held.Add(InputSystem.Attack);
        input.Sample(hero, .1f);
        Assert.InRange(hero.ActionData.Time, .0999, .1001);
        Assert.True(input.IsActionJustPressed(InputSystem.Skill1));
        Assert.Equal(!inputLocked, hero.ActionData.MusouRequested);

        if (!inputLocked)
            return;
        hero.ActionData.InputLocked = false;
        source.Pressed.Clear();
        input.Sample(hero, .1f);
        Assert.False(scope.GetSystem<InputSystem>().IsActionJustPressed(InputSystem.Skill1));
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
        scope.GetSystem<SkillSystem>().SelectSkills(1, new SkillDefinition(SkillId.RisingDragon, typeof(TestSkillState)));
        Assert.True(scope.GetSystem<SkillSystem>().TryActivate(SkillId.RisingDragon, hero));
        using var manager = new FsmLifetime();
        var fsm = manager.Value.CreateFsm<IHeroBody>(Guid.NewGuid().ToString(), hero,
            new HeroGroundState(), new HeroAirState(), new TestSkillState(), new HeroHurtState(), new HeroDeathState());
        fsm.Start<TestSkillState>();
        hero.ActionData.Time = .01f;
        Tick(fsm, .01f);
        Assert.Equal(1, hero.SkillBegins);
        Assert.Equal(321, hero.Velocity.X);
        Assert.NotNull(scope.Hero.ActiveSkillId);
        hero.PendingHurt = new Vector2(-100, 0);
        hero.ActionData.Time += .01f;
        Tick(fsm, .01f);
        Assert.IsType<HeroHurtState>(fsm.CurrentState);
        Assert.Equal(1, hero.SkillEnds);
        Assert.Null(scope.Hero.ActiveSkillId);
    }

    /// <summary>真实 GGF 先执行普攻离开，再开始技能动画；避免旧攻击清理掉新技能判定。</summary>
    [Fact]
    public void AttackToSkill_EndsNormalAttackBeforeStartingSkill()
    {
        var scope = new SkillScope();
        var hero = new SkillHero(scope) { AllowSkillDuringAttack = true };
        var skills = scope.GetSystem<SkillSystem>();
        scope.Skills.GetLearnedSkills(1).Add(SkillId.RisingDragon);
        skills.SelectSkills(1, new SkillDefinition(SkillId.RisingDragon, typeof(TestSkillState), InputSystem.Skill1));
        hero.ActionData.AttackSegment = 0;
        using var manager = new FsmLifetime();
        var fsm = manager.Value.CreateFsm<IHeroBody>(Guid.NewGuid().ToString(), hero,
            new HeroAttackState(), new TestSkillState(), new HeroGroundState(), new HeroAirState(),
            new HeroHurtState(), new HeroDeathState());
        fsm.Start<HeroAttackState>();
        scope.Source.Pressed.Add(InputSystem.Skill1);
        hero.ActionData.Time = .1;
        Tick(fsm, .1f);
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
        skills.SelectSkills(1, SkillTestDefinitions.Wukong());
        Assert.True(skills.TryActivate(id, hero));
        var data = scope.Hero.ActiveSkillId;
        Assert.Equal(0, hero.SkillBegins);
        using var manager = new FsmLifetime();
        var fsm = manager.Value.CreateFsm<IHeroBody>(Guid.NewGuid().ToString(), hero,
            new HeroGroundState(), new HeroAirState(), new HeroHurtState(), new HeroDeathState(),
            new WukongRisingDragonState(), new WukongFlameThrustState(), new WukongBlazingFlashState(),
            new WukongFlameStormState(), new WukongFireDemonState());
        fsm.Start<HeroGroundState>();
        hero.ActionData.Time = .01;
        Tick(fsm, .01f);
        Assert.IsType(stateType, fsm.CurrentState);
        Assert.Equal(1, hero.SkillBegins);
        ((AbstractHeroSkillState)fsm.CurrentState).CompleteAnimation();
        hero.ActionData.Time += 1d / 60;
        Tick(fsm, 1f / 60);
        Assert.Null(scope.Hero.ActiveSkillId);
        Assert.IsType<HeroGroundState>(fsm.CurrentState);
        Assert.Null(scope.Hero.ActiveSkillId);
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
        skills.SelectSkills(1, SkillTestDefinitions.Wukong());
        hero.ActionData.MoveAxis = 1;
        hero.ActionData.RightHeld = true;
        Assert.True(skills.TryActivate(SkillId.RisingDragon, hero));
        using var manager = new FsmLifetime();
        var fsm = manager.Value.CreateFsm<IHeroBody>(Guid.NewGuid().ToString(), hero,
            new WukongRisingDragonState(), new HeroGroundState(), new HeroAirState(),
            new HeroHurtState(), new HeroDeathState());
        fsm.Start<WukongRisingDragonState>();
        hero.ActionData.Time = .01;
        Tick(fsm, .01f);
        Assert.Equal(hero.Params.WalkSpeed, hero.Velocity.X);
        hero.ActionData.RightHeld = false;
        hero.ActionData.Time += .01;
        Tick(fsm, .01f);
        Assert.Equal(0, hero.Velocity.X);
        hero.ActionData.RightHeld = true;
        hero.ActionData.Time += .01;
        Tick(fsm, .01f);
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
        skills.SelectSkills(1, new SkillDefinition(SkillId.RisingDragon, typeof(TestPreparationState), InputSystem.Skill1));
        Assert.True(skills.TryActivate(SkillId.RisingDragon, hero));
        var data = scope.Hero.ActiveSkillId;
        int mana = scope.GetModel<HeroModel>().Mp.Value;
        using var manager = new FsmLifetime();
        var fsm = manager.Value.CreateFsm<IHeroBody>(Guid.NewGuid().ToString(), hero,
            new TestPreparationState(), new TestReleaseState(), new HeroGroundState(), new HeroAirState(),
            new HeroHurtState(), new HeroDeathState());
        fsm.Start<TestPreparationState>();
        scope.Source.Held.Add(InputSystem.Skill1);
        hero.ActionData.Time = .01;
        Tick(fsm, .01f);
        Assert.IsType<TestPreparationState>(fsm.CurrentState);
        Assert.Equal(mana, scope.GetModel<HeroModel>().Mp.Value);
        scope.Source.Pressed.Add(InputSystem.Skill1);
        hero.ActionData.Time = .02;
        Tick(fsm, .01f);
        Assert.IsType<TestReleaseState>(fsm.CurrentState);
        Assert.Equal(data, scope.Hero.ActiveSkillId);
        Assert.Equal(1, hero.SkillBegins);
        Assert.Equal(0, hero.SkillEnds);
        Assert.Equal(mana, scope.GetModel<HeroModel>().Mp.Value);
        hero.ActionData.Time += .3;
        Tick(fsm, .3f);
        Assert.IsType<HeroGroundState>(fsm.CurrentState);
        Assert.Null(scope.Hero.ActiveSkillId);
        Assert.Equal(1, hero.SkillEnds);
    }

    /// <summary>按正式实体的顺序匹配角色输入，再推进真实 GGF 状态。</summary>
    /// <param name="fsm">本测试状态机。</param>
    /// <param name="dt">游戏秒数。</param>
    private static void Tick(IFsm<IHeroBody> fsm, float dt)
    {
        if (fsm.Owner is SkillHero hero && !hero.Dead &&
            ((AbstractHeroBodyState)fsm.CurrentState).TryGetActionInput(hero, out bool attacking, out float normalProgress, out bool airborne))
            hero.MatchInput(attacking, normalProgress, airborne);
        fsm.Tick(dt);
        if (fsm.CurrentState is AbstractHeroSkillState state)
            state.AdvanceAnimation(fsm, fsm.Owner, dt);
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

        /// <summary>当前测试的输入来源。</summary>
        public FrameInput Source { get; } = new();

        /// <summary>借用正式配置，测试不复制数值。</summary>
        public ConfigurationModel Configuration => BattleArchitecture.Instance.GetModel<ConfigurationModel>();
        /// <summary>本测试的独立施放。</summary>
        public SkillModel Skills { get; } = new([1]);

        /// <summary>本次关卡的施放与数值状态。</summary>
        public HeroModel Hero => m_Hero;

        /// <summary>装配规则系统。</summary>
        public SkillScope()
        {
            m_Input = new(this, Source);
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
        /// <summary>测试角色使用当前状态给出的窗口和模型里的绑定，身体接口不承担输入匹配。</summary>
        /// <param name="attacking">是否处于普攻。</param>
        /// <param name="normalProgress">普攻播放比例。</param>
        /// <param name="airborne">是否处于空中。</param>
        public void MatchInput(bool attacking, float normalProgress, bool airborne)
        {
            if (!attacking || AllowSkillDuringAttack)
                scope.GetSystem<SkillSystem>().TryActivateInput(this, ActionData);
        }
        /// <inheritdoc />
        public void BeginSkillAnimation(string animation, string startMarker = null)
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
        public void AdvanceSkillAnimation(float dt) { }
        /// <inheritdoc />
        public void EndSkillAnimation() => SkillEnds++;
        /// <inheritdoc />
        public Vector2? FindSkillTarget(float range, float inset) => null;
        /// <inheritdoc />
        public void NewSkillAttack(SkillHitId hit, double power, float? knockbackOriginX = null) { }
        /// <inheritdoc />
        public void SpawnSkillEffect(PackedScene scene, Vector2 position, SkillHitId hit = SkillHitId.None, double power = 0, Action completed = null,
            float? knockbackOriginX = null)
        {
            if (FinishLandingEffect && hit == SkillHitId.FireDemonLanding)
                completed?.Invoke();
        }
    }

    /// <summary>由测试宿主登记的具名技能状态，复用公共身体驱动。</summary>
    private sealed class TestSkillState : AbstractHeroSkillState
    {
        /// <inheritdoc />
        protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
        {
            base.Enter(fsm, body);
            ((ISkillActor)body).BeginSkillAnimation(null);
        }

        /// <inheritdoc />
        protected override void UpdateSkill(IHeroBody body, float dt) =>
            body.Velocity = new Vector2(321, body.Velocity.Y);
    }

    /// <summary>示例起手阶段，第二次按下后转交执行给本技能的释放阶段。</summary>
    private sealed class TestPreparationState : AbstractHeroSkillState
    {
        private bool m_Transferring; // 是否正向同一技能的下一阶段转交身体。
        /// <inheritdoc />
        protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
        {
            m_Transferring = false;
            base.Enter(fsm, body);
            ((ISkillActor)body).BeginSkillAnimation(null);
        }
        /// <inheritdoc />
        protected override void Tick(IFsm<IHeroBody> fsm, IHeroBody body, float dt)
        {
            if (body.GetSystem<InputSystem>().ConsumePress(body.ActionData, InputSystem.Skill1))
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
                return;
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
        protected override void UpdateSkill(IHeroBody body, float dt)
        {
            m_Age += dt;
            if (m_Age >= .2f)
                CompleteAnimation();
        }
    }

    /// <summary>提供可重复的按键事实。</summary>
    private sealed class FrameInput : IInputSource
    {
        /// <summary>当前按住的动作。</summary>
        public HashSet<string> Held { get; } = new();
        /// <summary>本帧按下的动作。</summary>
        public HashSet<string> Pressed { get; } = new();
        /// <inheritdoc />
        public bool IsPressed(string action) => Held.Contains(action);
        /// <inheritdoc />
        public bool IsJustPressed(string action) => Pressed.Contains(action);
        /// <inheritdoc />
        public bool IsJustReleased(string action) => false;
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
