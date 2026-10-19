using System;
using GameConfig.Skills;
using GameLogic.Heroes.Body;
using GameLogic.Input;
using GameLogic.Skills;
using Godot;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>验证模型登记所有权、激活原子性、施放身份与原生输入查询；招式行为由真实 GGF 状态测试覆盖。</summary>
public sealed class SkillSystemTests
{
    /// <summary>角色提供的登记数组由模型复制；无效替换不能取消当前施放或修改旧登记。</summary>
    [Fact]
    public void Registration_IsOwnedByModelAndInvalidReplacementIsAtomic()
    {
        var scope = Ready();
        var system = scope.GetSystem<SkillSystem>();
        var first = new SkillDefinition(SkillId.WaterClone, typeof(TestSkillState), "skill_5");
        SkillDefinition[] definitions = [first];
        system.SelectSkills(2, definitions);
        Assert.True(system.TryActivate(SkillId.WaterClone, new Actor()));
        var data = scope.Hero.ActiveSkillId;
        definitions[0] = new SkillDefinition(SkillId.WaterDemonBurst, typeof(TestSkillState), "skill_5");
        Assert.Same(first, scope.Hero.SkillDefinitions[SkillId.WaterClone]);

        Assert.Throws<ArgumentException>(() => system.SelectSkills(2,
            first, new SkillDefinition(SkillId.WaterDemonBurst, typeof(TestSkillState), "skill_5")));
        Assert.Throws<ArgumentException>(() => system.SelectSkills(1, first));
        Assert.Throws<ArgumentException>(() => system.SelectSkills(2,
            new SkillDefinition(SkillId.RisingDragon, typeof(TestSkillState))));
        Assert.Equal(data, scope.Hero.ActiveSkillId);
        Assert.Single(scope.Hero.SkillDefinitions);
    }

    /// <summary>错误状态在登记边界被拒绝，不等到扣费或切换身体时才失败。</summary>
    /// <param name="state">错误的状态类型。</param>
    [Theory]
    [InlineData(null)]
    [InlineData(typeof(string))]
    [InlineData(typeof(AbstractHeroSkillState))]
    public void Registration_RejectsInvalidBodyStateBeforeSpendingMana(Type state)
    {
        var scope = Ready();
        var system = scope.GetSystem<SkillSystem>();
        int mana = scope.Hero.Mp.Value;
        Assert.Throws<ArgumentException>(() => system.SelectSkills(2,
            new SkillDefinition(SkillId.WaterClone, state, InputSystem.Skill1)));
        Assert.Null(scope.Hero.ActiveSkillId);
        Assert.Empty(scope.Hero.SkillDefinitions);
        Assert.Equal(mana, scope.Hero.Mp.Value);
    }

    /// <summary>起手条件或身体占用拒绝激活时不扣蓝；激活只登记数据，不提前触碰动画。</summary>
    [Fact]
    public void Activation_ChecksConditionsAndBodyOccupancyBeforeCharging()
    {
        var scope = Ready();
        var system = scope.GetSystem<SkillSystem>();
        var actor = new Actor { OnFloor = false };
        system.SelectSkills(2,
            new SkillDefinition(SkillId.WaterClone, typeof(TestSkillState), CanStart: body => body.OnFloor));
        int mana = scope.Hero.Mp.Value;
        Assert.False(system.TryActivate(SkillId.WaterClone, actor));
        Assert.Equal(mana, scope.Hero.Mp.Value);
        actor.OnFloor = true;
        Assert.True(system.TryActivate(SkillId.WaterClone, actor));
        int cost = TestTables.Tables.TbSkillConfig.Get(SkillId.WaterClone).ManaCost;
        Assert.Equal(mana - cost, scope.Hero.Mp.Value);
        Assert.False(system.TryActivate(SkillId.WaterClone, actor));
        Assert.Equal(mana - cost, scope.Hero.Mp.Value);
        Assert.Equal(0, actor.AnimationBegins);
    }

    /// <summary>取消清除激活编号与无敌区间，之后可以重新激活同一技能。</summary>
    [Fact]
    public void CancelAndReactivate_ClearsOccupancyAndInvulnerability()
    {
        var scope = Ready();
        var system = scope.GetSystem<SkillSystem>();
        var actor = new Actor();
        system.SelectSkills(2, new SkillDefinition(SkillId.WaterClone, typeof(TestSkillState)));
        Assert.True(system.TryActivate(SkillId.WaterClone, actor));
        system.GrantInvulnerability(0, 100);
        system.CancelSkill();
        Assert.Null(scope.Hero.ActiveSkillId);
        Assert.False(system.IsInvulnerable(1));
        Assert.True(system.TryActivate(SkillId.WaterClone, actor));
        Assert.Equal(SkillId.WaterClone, scope.Hero.ActiveSkillId);
    }

    /// <summary>同一档案重试时技能书保留，旧本局登记、施放和无敌不能泄漏到新模型。</summary>
    [Fact]
    public void NewRun_PreservesLearningAndDropsAllCastState()
    {
        var scope = new TestGameScope(1, [(1, 0)], 100, 25);
        var system = scope.GetSystem<SkillSystem>();
        Assert.Equal(SkillLearnResult.Learned, system.Learn(SkillId.RisingDragon));
        Assert.True(system.TryActivate(SkillId.RisingDragon, new Actor()));
        system.GrantInvulnerability(0, 100);
        var previousHero = scope.Hero;
        var learned = scope.Skills.GetLearnedSkills(1);
        scope.RestartHero();
        Assert.NotSame(previousHero, scope.Hero);
        Assert.Same(learned, scope.Skills.GetLearnedSkills(1));
        Assert.True(system.IsLearned(SkillId.RisingDragon));
        Assert.False(system.IsAvailable(SkillId.RisingDragon));
        Assert.Null(scope.Hero.ActiveSkillId);
        Assert.Null(system.BodyStateType);
        Assert.False(system.IsInvulnerable(1));
        Assert.Empty(scope.Hero.SkillDefinitions);
        system.SelectSkills(1, GameLogic.SkillTests.SkillTestDefinitions.Wukong());
        Assert.True(system.TryActivate(SkillId.RisingDragon, new Actor()));
        Assert.Equal(SkillId.RisingDragon, scope.Hero.ActiveSkillId);
    }

    /// <summary>正常收招只释放本技能占用，不能结束别的技能；秒制无敌不随动画提前结束。</summary>
    [Fact]
    public void EndSkill_ReleasesMatchingOccupancyWithoutCancellingTimedInvulnerability()
    {
        var scope = Ready();
        var system = scope.GetSystem<SkillSystem>();
        system.SelectSkills(2, new SkillDefinition(SkillId.WaterClone, typeof(TestSkillState)));
        Assert.True(system.TryActivate(SkillId.WaterClone, new Actor()));
        system.GrantInvulnerability(0, 10);
        system.EndSkill(SkillId.WaterDemonBurst);
        Assert.Equal(SkillId.WaterClone, scope.Hero.ActiveSkillId);
        system.EndSkill(SkillId.WaterClone);
        Assert.Null(scope.Hero.ActiveSkillId);
        Assert.True(system.IsInvulnerable(1));
        system.CancelSkill();
        Assert.False(system.IsInvulnerable(1));
    }

    /// <summary>保持相同登记不打断施放；切换角色的技能组才取消旧数据，学习记录仍保留。</summary>
    [Fact]
    public void SkillGroupChange_CancelsOldCastAndPreservesLearning()
    {
        var scope = Ready();
        var system = scope.GetSystem<SkillSystem>();
        var actor = new Actor();
        var first = new SkillDefinition(SkillId.WaterClone, typeof(TestSkillState), InputSystem.Skill1);
        system.SelectSkills(2, first);
        Assert.True(system.TryActivate(SkillId.WaterClone, actor));
        var data = scope.Hero.ActiveSkillId;
        system.SelectSkills(2, first);
        Assert.Equal(data, scope.Hero.ActiveSkillId);
        system.SelectSkills(2, new SkillDefinition(SkillId.WaterDemonBurst, typeof(TestSkillState), InputSystem.Skill1));
        Assert.Null(scope.Hero.ActiveSkillId);
        Assert.True(system.IsLearned(SkillId.WaterClone));
        Assert.False(system.IsAvailable(SkillId.WaterClone));
        Assert.True(system.TryActivate(SkillId.WaterDemonBurst, actor));
    }

    /// <summary>同帧短按与重新按住均保留三个查询事实，消费只影响本角色的按下边沿。</summary>
    /// <param name="held">本帧最终是否按住。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InputQueries_PreserveBothEdgesAndIndependentConsumption(bool held)
    {
        var scope = Ready();
        var input = scope.GetSystem<InputSystem>();
        const string action = "custom_skill_5";
        scope.Input.Actions[action] = (true, held, true);
        var firstData = new HeroActionData();
        var secondData = new HeroActionData();
        Assert.True(input.IsActionJustPressed(action));
        Assert.True(input.IsActionJustReleased(action));
        Assert.Equal(held, input.IsActionPressed(action));
        Assert.True(input.ConsumePress(firstData, action));
        Assert.False(input.ConsumePress(firstData, action));
        Assert.True(input.ConsumePress(secondData, action));
        Assert.True(input.IsActionJustReleased(action));
        Assert.Equal(held, input.IsActionPressed(action));
    }

    /// <summary>当前技能占用期间拒绝的新按下仍消费，取消后同帧不能补发一次扣费。</summary>
    [Fact]
    public void RejectedPress_IsNotReplayedAfterCancellation()
    {
        var scope = Ready();
        var system = scope.GetSystem<SkillSystem>();
        var actor = new Actor();
        system.SelectSkills(2, new SkillDefinition(SkillId.WaterClone, typeof(TestSkillState), InputSystem.Skill1));
        Assert.True(system.TryActivate(SkillId.WaterClone, actor));
        int mana = scope.Hero.Mp.Value;
        var data = new HeroActionData();
        scope.Input.Actions[InputSystem.Skill1] = (true, true, false);
        Assert.False(system.TryActivateInput(actor, data));
        system.CancelSkill();
        Assert.False(system.TryActivateInput(actor, data));
        Assert.Equal(mana, scope.Hero.Mp.Value);
    }

    /// <summary>装配唐僧登记所需的目录与蓝量；不假称其正式招式已实现。</summary>
    /// <returns>独立的规则测试作用域。</returns>
    private static TestGameScope Ready()
    {
        var scope = new TestGameScope(2, [(2, 0)], 99999, 20, initializeHero: false);
        scope.Skills.GetLearnedSkills(2).Add(SkillId.WaterClone);
        scope.Skills.GetLearnedSkills(2).Add(SkillId.WaterDemonBurst);
        scope.Hero.Mp.Value = 200;
        return scope;
    }

    /// <summary>只用于验证登记契约，不替代具体角色技能实现。</summary>
    private sealed class TestSkillState : AbstractHeroSkillState { }

    /// <summary>只替代节点能力，激活和费用使用正式系统。</summary>
    private sealed class Actor : ISkillActor
    {
        /// <inheritdoc />
        public Vector2 SkillPosition { get; set; }
        /// <inheritdoc />
        public Vector2 Velocity { get; set; }
        /// <inheritdoc />
        public bool OnFloor { get; set; } = true;
        /// <inheritdoc />
        public int Facing => 1;
        /// <summary>用于核对激活尚未开始表现。</summary>
        public int AnimationBegins { get; private set; }
        /// <inheritdoc />
        public void BeginSkillAnimation(string animation, string startMarker = null) => AnimationBegins++;
        /// <inheritdoc />
        public void AdvanceSkillAnimation(float dt) { }
        /// <inheritdoc />
        public void EndSkillAnimation() { }
        /// <inheritdoc />
        public Vector2? FindSkillTarget(float range, float inset) => null;
        /// <inheritdoc />
        public void NewSkillAttack(SkillHitId hit, double power, float? knockbackOriginX = null) { }
        /// <inheritdoc />
        public void SpawnSkillEffect(PackedScene scene, Vector2 position, SkillHitId hit = SkillHitId.None, double power = 0, Action completed = null,
            float? knockbackOriginX = null)
        { }
    }
}
