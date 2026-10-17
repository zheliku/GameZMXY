using System;
using GameConfig.Skills;
using GameLogic.Input;
using GameLogic.Heroes.Body;
using GameLogic.Skills;
using Godot;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>通过正式系统验证非悟空技能所需的二次输入、蓄力、持续阶段和武器技能组；不冒充角色的完整招式实现。</summary>
public sealed class SkillExecutionTests
{
    /// <summary>没有动画表的唐僧技能也能登记；按住不触发二段，第二次边沿接续原执行且不重复扣蓝。</summary>
    [Fact]
    public void SecondPress_ContinuesSameExecutionAndChargesOnlyOnce()
    {
        var scope = Ready();
        var actor = new Actor();
        var execution = new TwoStageExecution();
        var system = scope.GetSystem<SkillSystem>();
        system.SelectSkillSet(new SkillSet(2, new SkillDefinition(SkillId.WaterDemonBurst, _ => execution, 3, typeof(TestSkillState))), actor);
        Assert.Equal(SkillLearnResult.Learned, system.Learn(SkillId.WaterDemonBurst));
        scope.Hero.Mp.Value = 200;
        Activate(system, SkillId.WaterDemonBurst, actor, 0);
        int afterFirst = scope.Hero.Mp.Value;
        Assert.Equal(200 - TestTables.Tables.TbSkillConfig.Get(SkillId.WaterDemonBurst).ManaCost, afterFirst);
        var data = new HeroActionData();
        data.SetSkillButton(3, new(false, true, false));
        system.HandleInput(actor, data);
        Assert.Equal(1, execution.Stage);
        data.SetSkillButton(3, new(false, false, true));
        system.HandleInput(actor, data);
        data.SetSkillButton(3, new(true, true, false));
        system.HandleInput(actor, data);
        Assert.Equal(2, execution.Stage);
        Assert.Same(execution, system.BodySkill);
        Assert.Equal(afterFirst, scope.Hero.Mp.Value);
        Assert.False(system.TryActivate(SkillId.WaterDemonBurst, actor, 1));
    }

    /// <summary>持续技能释放身体后仍推进，可与另一技能并存，最后一次普通物理帧不重复计时。</summary>
    [Fact]
    public void PersistentPhase_ReleasesBodyAndAdvancesOncePerFrame()
    {
        var scope = Ready();
        var actor = new Actor();
        var persistent = new PersistentExecution(SkillId.WaterClone);
        var second = new TwoStageExecution();
        var system = scope.GetSystem<SkillSystem>();
        system.SelectSkillSet(new SkillSet(2, new SkillDefinition(SkillId.WaterClone, _ => persistent, 1, typeof(TestSkillState)),
            new SkillDefinition(SkillId.WaterDemonBurst, _ => second, 3, typeof(TestSkillState))), actor);
        scope.Skills.GetLearnedSkills(2).Add(SkillId.WaterClone);
        scope.Skills.GetLearnedSkills(2).Add(SkillId.WaterDemonBurst);
        scope.Hero.Mp.Value = 200;
        Activate(system, SkillId.WaterClone, actor, 0);
        system.Advance(persistent, actor, .1f, .1);
        Assert.Null(system.BodySkill);
        system.ReleaseBody(persistent, actor);
        system.AdvanceBackground(actor, .1f, .1);
        Assert.InRange(persistent.Age, .0999, .1001);
        Activate(system, SkillId.WaterDemonBurst, actor, .1);
        int mana = scope.Hero.Mp.Value;
        var input = new HeroActionData { DirectSkillSlot = 1 };
        input.SetSkillButton(1, new(true, true, false));
        system.HandleInput(actor, input);
        Assert.Same(second, system.BodySkill);
        Assert.False(persistent.BlocksBody);
        Assert.Equal(mana, scope.Hero.Mp.Value);
        Assert.Null(input.DirectSkillSlot);
        system.AdvanceBackground(actor, .1f, .2);
        Assert.InRange(persistent.Age, .1999, .2001);
        Assert.True(system.IsActive(persistent));
        Assert.True(system.IsActive(second));
        Assert.Equal(0, persistent.Stops);
        system.StopAll(actor);
        Assert.False(system.IsActive(persistent));
        Assert.False(system.IsActive(second));
        Assert.Equal(1, persistent.Stops);
        system.StopAll(actor);
        Assert.Equal(1, persistent.Stops);
    }

    /// <summary>持续阶段再次输入可以重新占用身体；其他身体技能占用期间不得抢占。</summary>
    [Fact]
    public void RepeatedInput_ReacquiresAvailableBody()
    {
        var scope = Ready();
        var actor = new Actor();
        var execution = new PersistentExecution(SkillId.WaterClone);
        var system = scope.GetSystem<SkillSystem>();
        system.SelectSkillSet(new SkillSet(2, new SkillDefinition(SkillId.WaterClone, _ => execution, 1, typeof(TestSkillState))), actor);
        scope.Skills.GetLearnedSkills(2).Add(SkillId.WaterClone);
        scope.Hero.Mp.Value = 200;
        Activate(system, SkillId.WaterClone, actor, 0);
        system.Advance(execution, actor, .1f, .1);
        Assert.Null(system.BodySkill);
        int mana = scope.Hero.Mp.Value;
        var data = new HeroActionData();
        data.SetSkillButton(1, new(true, true, false));
        system.HandleInput(actor, data);
        Assert.Same(execution, system.BodySkill);
        Assert.Equal(mana, scope.Hero.Mp.Value);
    }

    /// <summary>蓄力与持续效果按秒推进，松手事实可完成阶段；公共系统不限定动画表或固定帧率。</summary>
    /// <param name="fps">测试帧率。</param>
    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    public void ChargeAndRelease_UseGameSeconds(int fps)
    {
        var scope = Ready();
        var actor = new Actor();
        var execution = new ChargeExecution();
        var system = scope.GetSystem<SkillSystem>();
        system.SelectSkillSet(new SkillSet(2, new SkillDefinition(SkillId.IceDragonWave, _ => execution, 1, typeof(TestSkillState))), actor);
        scope.Skills.GetLearnedSkills(2).Add(SkillId.IceDragonWave);
        scope.Hero.Mp.Value = 200;
        Activate(system, SkillId.IceDragonWave, actor, 0);
        var data = new HeroActionData();
        data.SetSkillButton(1, new(false, true, false));
        for (int i = 0; i < fps; i++)
        {
            system.HandleInput(actor, data);
            system.Advance(execution, actor, 1f / fps, (i + 1d) / fps);
        }
        Assert.InRange(execution.Age, .9999, 1.0001);
        Assert.True(system.IsActive(execution));
        data.SetSkillButton(1, new(false, false, true));
        system.HandleInput(actor, data);
        Assert.True(execution.Completed);
        Assert.False(system.IsActive(execution));
        Assert.Null(system.BodySkill);
        Assert.Equal(1, execution.Stops);
    }

    /// <summary>武器换组改变技能与槽位，清理旧持续阶段、身体占用和无敌，学习记录仍属于同一英雄。</summary>
    [Fact]
    public void WeaponSkillSetSwitch_CleansOldExecutionAndChangesBinding()
    {
        var scope = Ready();
        var actor = new Actor();
        var old = new PersistentExecution(SkillId.WaterClone);
        var system = scope.GetSystem<SkillSystem>();
        var first = new SkillSet(2, new SkillDefinition(SkillId.WaterClone, _ => old, 1, typeof(TestSkillState)));
        var second = new SkillSet(2, new SkillDefinition(SkillId.WaterDemonBurst, _ => new TwoStageExecution(), 1, typeof(TestSkillState)));
        scope.Skills.GetLearnedSkills(2).Add(SkillId.WaterClone);
        scope.Skills.GetLearnedSkills(2).Add(SkillId.WaterDemonBurst);
        scope.Hero.Mp.Value = 200;
        system.SelectSkillSet(first, actor);
        Activate(system, SkillId.WaterClone, actor, 0);
        system.GrantInvulnerability(0, 100);
        system.SelectSkillSet(first, actor);
        Assert.True(system.IsActive(old));
        system.SelectSkillSet(second, actor);
        Assert.True(old.Completed);
        Assert.Equal(1, old.Stops);
        Assert.False(system.IsActive(old));
        Assert.False(system.IsInvulnerable(1));
        Assert.False(system.IsAvailable(SkillId.WaterClone));
        Assert.Equal(SkillId.WaterDemonBurst, scope.Skills.SelectedSet.GetSkill(1));
        Assert.Null(scope.Skills.SelectedSet.GetSkill(3));
        Assert.True(system.IsLearned(SkillId.WaterClone));
        Activate(system, SkillId.WaterDemonBurst, actor, 1);
    }

    /// <summary>错误英雄的技能组在改变任何旧执行前失败。</summary>
    [Fact]
    public void SkillSetOwnership_IsValidatedBeforeReplacingCurrentSet()
    {
        var scope = Ready();
        var actor = new Actor();
        var system = scope.GetSystem<SkillSystem>();
        var valid = new SkillSet(2, new SkillDefinition(SkillId.WaterDemonBurst, _ => new TwoStageExecution(), 3, typeof(TestSkillState)));
        system.SelectSkillSet(valid, actor);
        Assert.Throws<ArgumentException>(() => system.SelectSkillSet(new SkillSet(1,
            new SkillDefinition(SkillId.RisingDragon, _ => new TwoStageExecution())), actor));
        Assert.Same(valid, scope.Skills.SelectedSet);
    }

    /// <summary>二次输入结束执行时，同一边沿不能重新成为新激活请求并重复扣费。</summary>
    [Fact]
    public void FollowupInput_IsConsumedEvenWhenItCompletesTheExecution()
    {
        var scope = Ready();
        var actor = new Actor();
        var system = scope.GetSystem<SkillSystem>();
        var execution = new FinishOnPressExecution();
        system.SelectSkillSet(new SkillSet(2, new SkillDefinition(SkillId.IceDragonWave,
            _ => execution, 1, typeof(TestSkillState))), actor);
        scope.Skills.GetLearnedSkills(2).Add(SkillId.IceDragonWave);
        scope.Hero.Mp.Value = 200;
        Activate(system, SkillId.IceDragonWave, actor, 0);
        int mana = scope.Hero.Mp.Value;
        var input = new HeroActionData { DirectSkillSlot = 1 };
        input.SetSkillButton(1, new(true, true, false));
        system.HandleInput(actor, input);
        Assert.True(execution.Completed);
        Assert.Null(input.DirectSkillSlot);
        Assert.Null(system.BodySkill);
        Assert.Equal(mana, scope.Hero.Mp.Value);
    }

    /// <summary>缺少身体状态登记在扣费前报告装配错误，不回退到公共万能技能状态。</summary>
    [Fact]
    public void BodySkill_RequiresStateRegistrationBeforeSpendingMana()
    {
        var scope = Ready();
        var actor = new Actor();
        var system = scope.GetSystem<SkillSystem>();
        system.SelectSkillSet(new SkillSet(2, new SkillDefinition(SkillId.WaterDemonBurst,
            _ => new TwoStageExecution(), 3)), actor);
        scope.Skills.GetLearnedSkills(2).Add(SkillId.WaterDemonBurst);
        scope.Hero.Mp.Value = 200;
        Assert.Throws<InvalidOperationException>(() => system.TryActivate(SkillId.WaterDemonBurst, actor, 0));
        Assert.Equal(200, scope.Hero.Mp.Value);
        Assert.Null(system.BodySkill);
    }

    /// <summary>模拟 GGF 激活后进入身体状态；不让技能在旧状态退出前起手。</summary>
    /// <param name="system">正式技能系统。</param>
    /// <param name="id">技能编号。</param>
    /// <param name="actor">表现替身。</param>
    /// <param name="now">游戏时刻。</param>
    private static void Activate(SkillSystem system, SkillId id, ISkillActor actor, double now)
    {
        Assert.True(system.TryActivate(id, actor, now));
        if (system.BodySkill != null)
            system.BeginBody(actor, now);
    }

    /// <summary>创建当前已有唐僧目录的独立测试作用域，不新增虚假的八戒或沙僧数值。</summary>
    /// <returns>正式模型和系统构成的作用域。</returns>
    private static TestGameScope Ready() => new(2, [(2, 0)], 999999, 20, initializeHero: false);

    /// <summary>测试技能自行登记的身体状态，不在正式公共层新增默认技能状态。</summary>
    private sealed class TestSkillState : AbstractHeroSkillState { }

    /// <summary>验证二次按下可结束本次执行，不把它重新解释为新起手。</summary>
    private sealed class FinishOnPressExecution() : SkillExecution(SkillId.IceDragonWave)
    {
        /// <inheritdoc />
        public override void Start(ISkillActor actor, double now) { }
        /// <inheritdoc />
        public override void Advance(ISkillActor actor, float dt) { }
        /// <inheritdoc />
        public override void HandleInput(ISkillActor actor, SkillButtonInput input, bool bodyAvailable) =>
            Completed = input.Pressed;
    }

    /// <summary>测试二次输入所需的阶段转换，不依赖动作配置或悟空事件枚举。</summary>
    private sealed class TwoStageExecution() : SkillExecution(SkillId.WaterDemonBurst)
    {
        /// <summary>同一次执行的当前阶段。</summary>
        public int Stage { get; private set; }
        /// <inheritdoc />
        public override void Start(ISkillActor actor, double now) => Stage = 1;
        /// <inheritdoc />
        public override void Advance(ISkillActor actor, float dt) { }
        /// <inheritdoc />
        public override void HandleInput(ISkillActor actor, SkillButtonInput input, bool bodyAvailable)
        {
            if (input.Pressed && Stage == 1)
                Stage = 2;
        }
    }

    /// <summary>测试定时持续与再次发动，代表分身或反伤等持续行为需要的生命周期。</summary>
    private sealed class PersistentExecution(SkillId id) : SkillExecution(id)
    {
        /// <summary>已推进游戏秒数。</summary>
        public double Age { get; private set; }
        /// <summary>停止规则的调用次数。</summary>
        public int Stops { get; private set; }
        /// <inheritdoc />
        public override void Start(ISkillActor actor, double now) { }
        /// <inheritdoc />
        public override void Advance(ISkillActor actor, float dt)
        {
            Age += dt;
            BlocksBody = false;
            Completed = Age >= 2;
        }
        /// <inheritdoc />
        public override void HandleInput(ISkillActor actor, SkillButtonInput input, bool bodyAvailable)
        {
            if (input.Pressed && bodyAvailable)
                BlocksBody = true;
        }
        /// <inheritdoc />
        public override void Stop(ISkillActor actor) => Stops++;
    }

    /// <summary>测试蓄力后松手结束，结束时只清理一次。</summary>
    private sealed class ChargeExecution() : SkillExecution(SkillId.IceDragonWave)
    {
        /// <summary>已推进游戏秒数。</summary>
        public double Age { get; private set; }
        /// <summary>结束次数。</summary>
        public int Stops { get; private set; }
        /// <inheritdoc />
        public override void Start(ISkillActor actor, double now) { }
        /// <inheritdoc />
        public override void Advance(ISkillActor actor, float dt) => Age += dt;
        /// <inheritdoc />
        public override void HandleInput(ISkillActor actor, SkillButtonInput input, bool bodyAvailable) => Completed = input.Released;
        /// <inheritdoc />
        public override void Stop(ISkillActor actor) => Stops++;
    }

    /// <summary>仅提供正式执行契约需要的表现能力，不依赖引擎节点。</summary>
    private sealed class Actor : ISkillActor
    {
        /// <inheritdoc />
        public Vector2 SkillPosition { get; set; }
        /// <inheritdoc />
        public Vector2 Velocity { get; set; }
        /// <inheritdoc />
        public bool OnFloor => true;
        /// <inheritdoc />
        public int Facing => 1;
        /// <inheritdoc />
        public void BeginSkillAnimation(SkillActionConfig action) { }
        /// <inheritdoc />
        public void SetSkillAnimationTime(double seconds) { }
        /// <inheritdoc />
        public void EndSkillAnimation() { }
        /// <inheritdoc />
        public Vector2? FindSkillTarget(float range, float inset) => null;
        /// <inheritdoc />
        public void NewSkillAttack(SkillHitId hit) { }
        /// <inheritdoc />
        public void SpawnSkillEffect(SkillEffectId effect, SkillExecution execution, Vector2 position, Action completed = null) { }
    }
}
