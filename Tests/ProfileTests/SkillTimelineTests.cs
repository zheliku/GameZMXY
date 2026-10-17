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

namespace GameLogic.Profile.Tests;

/// <summary>通过真实 GGF 技能状态验证正式表的费用、秒制事件、火魔斩分支和取整顺序。</summary>
public sealed class SkillTimelineTests
{
    /// <summary>未学、蓝量不足和地面限制都不开始动作，也不扣蓝。</summary>
    [Fact]
    public void Activation_RequiresLearningManaAndGround()
    {
        var scope = new TestGameScope(1, [(1, 0)], 9999, 20);
        var actor = new Motion(scope);
        var skills = scope.GetSystem<SkillSystem>();
        skills.SelectSkillSet(SkillTestDefinitions.Wukong(), actor);
        Assert.False(skills.TryActivate(SkillId.FlameThrust, actor, 0));
        skills.Learn(SkillId.FlameThrust);
        int mana = scope.Hero.Mp.Value;
        Assert.False(skills.TryActivate(SkillId.FlameThrust, actor, 0));
        Assert.Equal(mana, scope.Hero.Mp.Value);
        actor.OnFloor = true;
        scope.Hero.Mp.Value = 19;
        Assert.False(skills.TryActivate(SkillId.FlameThrust, actor, 0));
        Assert.Equal(19, scope.Hero.Mp.Value);
        scope.Hero.Mp.Value = 20;
        Assert.True(skills.TryActivate(SkillId.FlameThrust, actor, 0));
        Assert.Equal(0, scope.Hero.Mp.Value);
        Assert.False(skills.TryActivate(SkillId.FlameThrust, actor, 0));
    }

    /// <summary>突刺状态独立更新四次攻击编号与三次减速，结束后保留下一段普攻衔接。</summary>
    [Fact]
    public void FlameThrust_OwnsFourHitsAndDeceleration()
    {
        using var skill = new SkillFixture(SkillId.FlameThrust, velocity: new Vector2(120, 0));
        Assert.Equal(360, skill.Actor.Velocity.X);
        Assert.Single(skill.Actor.Attacks);
        skill.Advance(1f / 6);
        Assert.Equal(2, skill.Actor.Attacks.Count);
        Assert.InRange(skill.Actor.Velocity.X, 287.99, 288.01);
        skill.Advance(.125f);
        Assert.Equal(3, skill.Actor.Attacks.Count);
        Assert.InRange(skill.Actor.Velocity.X, 230.39, 230.41);
        skill.Advance(.125f);
        Assert.Equal(4, skill.Actor.Attacks.Count);
        Assert.InRange(skill.Actor.Velocity.X, 184.31, 184.33);
        skill.Advance(.2f);
        Assert.True(skill.Execution.Completed);
        Assert.Equal(0, skill.Actor.ActionData.LastGroundSegment);
    }

    /// <summary>外部取消后不再运行旧招式事件，防止换组或离场前多生成一次判定。</summary>
    [Fact]
    public void CancelledSkill_DoesNotEmitMoreHitEvents()
    {
        using var skill = new SkillFixture(SkillId.FlameThrust);
        Assert.Single(skill.Actor.Attacks);
        skill.System.StopAll(skill.Actor);
        skill.Advance(.3f);
        Assert.Single(skill.Actor.Attacks);
        Assert.Null(skill.System.BodySkill);
    }

    /// <summary>升龙与风暴由各自状态占用第二次跳跃，并设置表中的普攻禁止时间。</summary>
    /// <param name="id">消耗空中跳跃的招式。</param>
    [Theory]
    [InlineData(SkillId.RisingDragon)]
    [InlineData(SkillId.FlameStorm)]
    public void SkillEntry_OwnsAirJumpAndNormalAttackCooldown(SkillId id)
    {
        using var skill = new SkillFixture(id, jumpCount: 1);
        Assert.Equal(2, skill.Actor.ActionData.JumpCount);
        Assert.Equal(-1, skill.Actor.ActionData.LastGroundSegment);
        Assert.Equal(TestTables.Tables.TbSkillActionConfig.Get(id).NormalAttackBlockSeconds,
            skill.Actor.ActionData.AttackReadyAt);
    }

    /// <summary>烈焰闪第三源帧创建拖尾，无敌按游戏秒数计算；30/60/144fps 一致。</summary>
    /// <param name="fps">物理帧率。</param>
    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    public void BlazingFlash_TrailAndInvincibilityAreFrameRateIndependent(int fps)
    {
        using var skill = new SkillFixture(SkillId.BlazingFlash);
        Assert.Empty(skill.Actor.Effects);
        for (int frame = 0; frame < fps; frame++)
            skill.Advance(1f / fps);
        Assert.True(skill.Execution.Completed);
        Assert.Single(skill.Actor.Effects);
        Assert.Equal(SkillEffectId.BlazingFlashTrail, skill.Actor.Effects[0]);
        Assert.False(skill.System.IsInvulnerable(.08));
        Assert.True(skill.System.IsInvulnerable(.09));
        Assert.True(skill.System.IsInvulnerable(.5));
        Assert.False(skill.System.IsInvulnerable(.59));
    }

    /// <summary>火魔斩无目标时直接下冲，落地等待特效结束后才恢复重力和输入。</summary>
    [Fact]
    public void FireDemon_MissingTargetSkipsComboAndFinishesThroughLanding()
    {
        using var skill = new SkillFixture(SkillId.FireDemonSlash);
        skill.Advance(.3f);
        Assert.True(skill.Actor.AnimationTime >= 46d / 24);
        Assert.DoesNotContain(SkillEffectId.FireDemonCombo, skill.Actor.Effects);
        Assert.Contains(SkillEffectId.FireDemonDescent, skill.Actor.Effects);
        skill.Actor.OnFloor = true;
        skill.Advance(.1f);
        Assert.Contains(SkillEffectId.FireDemonLanding, skill.Actor.Effects);
        Assert.False(skill.Execution.Completed);
        double frozen = skill.Actor.AnimationTime;
        skill.Advance(5);
        Assert.Equal(frozen, skill.Actor.AnimationTime);
        skill.Actor.Completions[SkillEffectId.FireDemonLanding]();
        skill.Advance(.01f);
        Assert.True(skill.Execution.Completed);
        Assert.False(skill.Execution.GravitySuspended);
        Assert.False(skill.Execution.InputLocked);
    }

    /// <summary>寻敌成功后定位、生成九连斩并冻结，同帧特效事件不能丢失。</summary>
    /// <param name="fps">物理帧率。</param>
    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    public void FireDemon_TargetFreezesAtTheComboFrame(int fps)
    {
        using var skill = new SkillFixture(SkillId.FireDemonSlash, target: new Vector2(80, 100));
        for (int frame = 0; frame < fps; frame++)
            skill.Advance(1f / fps);
        Assert.Equal(skill.Actor.Target.Value, skill.Actor.SkillPosition);
        Assert.Contains(SkillEffectId.FireDemonCombo, skill.Actor.Effects);
        Assert.InRange(skill.Actor.AnimationTime, .2499, .2501);
        Assert.DoesNotContain(SkillEffectId.FireDemonDescent, skill.Actor.Effects);
        Assert.True(skill.Execution.GravitySuspended);
        Assert.True(skill.Execution.InputLocked);
        for (int frame = 0; frame < 3 * fps; frame++)
            skill.Advance(1f / fps);
        Assert.Contains(SkillEffectId.FireDemonDescent, skill.Actor.Effects);
    }

    /// <summary>复用同一个 GGF 状态实例再次起手后，旧落地回调只能操作旧游标。</summary>
    [Fact]
    public void LandingCallback_CannotSeekTheNextExecution()
    {
        using var skill = new SkillFixture(SkillId.FireDemonSlash);
        skill.Advance(.3f);
        skill.Actor.OnFloor = true;
        skill.Advance(.1f);
        var oldCompletion = skill.Actor.Completions[SkillEffectId.FireDemonLanding];
        skill.System.StopAll(skill.Actor);
        skill.Advance(.01f);
        skill.Scope.Hero.Mp.Value = 100;
        Assert.True(skill.System.TryActivate(SkillId.FireDemonSlash, skill.Actor, skill.Actor.ActionData.Time));
        skill.Advance(.01f);
        double newTime = skill.Actor.AnimationTime;
        oldCompletion();
        skill.Advance(.01f);
        Assert.InRange(skill.Actor.AnimationTime, newTime, newTime + .011);
        Assert.NotNull(skill.System.BodySkill);
        Assert.False(skill.System.BodySkill.Completed);
    }

    /// <summary>原版不同招式的取整位置不同，无双最后作用。</summary>
    /// <param name="id">命中配置。</param>
    /// <param name="power">出招攻击力。</param>
    /// <param name="multiplier">无双倍率。</param>
    /// <param name="expected">预期伤害威力。</param>
    [Theory]
    [InlineData(SkillHitId.FlameThrust, 11, 1, 6)]
    [InlineData(SkillHitId.FlameThrust, 11, 1.5f, 9)]
    [InlineData(SkillHitId.BlazingFlash, 11, 1.5f, 150)]
    [InlineData(SkillHitId.FlameStorm, 11, 1.5f, 19)]
    [InlineData(SkillHitId.FireDemonLanding, 11, 1.5f, 49)]
    public void Power_UsesOfficialRoundingOrder(SkillHitId id, float power, float multiplier, int expected) =>
        Assert.Equal(expected, SkillSystem.CalculatePower(TestTables.Tables.TbSkillHitConfig.Get(id), power, multiplier));

    /// <summary>真实框架状态机夹具；只替换引擎物理与动画节点。</summary>
    private sealed class SkillFixture : IDisposable
    {
        private readonly FsmManager m_Manager = new(); // 本测试拥有的真实 GGF 状态机管理器。
        private readonly IFsm<IHeroBody> m_Fsm; // 按物理帧驱动的正式状态集合。
        /// <summary>独立模型作用域。</summary>
        public TestGameScope Scope { get; } = new(1, [(1, 0)], 9999, 20);
        /// <summary>角色表现替身。</summary>
        public Motion Actor { get; }
        /// <summary>真实技能系统。</summary>
        public SkillSystem System => Scope.GetSystem<SkillSystem>();
        /// <summary>借用本次开始的执行身份。</summary>
        public SkillExecution Execution { get; }

        /// <summary>装配并直接进入指定技能自己的状态。</summary>
        /// <param name="id">悟空招式。</param>
        /// <param name="target">可选寻敌结果。</param>
        /// <param name="velocity">可选起手速度。</param>
        /// <param name="jumpCount">起手前已使用的跳跃次数。</param>
        public SkillFixture(SkillId id, Vector2? target = null, Vector2? velocity = null, int jumpCount = 0)
        {
            Actor = new Motion(Scope) { Target = target, OnFloor = true, Velocity = velocity ?? Vector2.Zero };
            Actor.ActionData.JumpCount = jumpCount;
            System.SelectSkillSet(SkillTestDefinitions.Wukong(), Actor);
            System.Learn(id);
            Scope.Hero.Mp.Value = 100;
            Assert.True(System.TryActivate(id, Actor, 0));
            Execution = System.BodySkill;
            Actor.OnFloor = false;
            m_Fsm = m_Manager.CreateFsm<IHeroBody>(Guid.NewGuid().ToString(), Actor,
                new HeroGroundState(), new HeroAirState(), new HeroHurtState(), new HeroDeathState(),
                new WukongRisingDragonState(), new WukongFlameThrustState(), new WukongBlazingFlashState(),
                new WukongFlameStormState(), new WukongFireDemonState());
            m_Fsm.Start(System.BodyStateType);
        }

        /// <summary>推进一帧真实身体状态。</summary>
        /// <param name="dt">游戏秒数。</param>
        public void Advance(float dt)
        {
            Actor.ActionData.Time += dt;
            m_Fsm.Tick(dt);
        }
        /// <inheritdoc />
        public void Dispose() => m_Manager.Shutdown();
    }

    /// <summary>只记录物理与特效请求的测试表现替身。</summary>
    private sealed class Motion(TestGameScope scope) : ISkillActor, IHeroBody
    {
        /// <inheritdoc />
        IArchitecture IBelongToArchitecture.GetArchitecture() => scope;
        /// <inheritdoc />
        public HeroActionData ActionData { get; } = new();
        /// <inheritdoc />
        public HeroBodyParams Params { get; } = new();
        /// <inheritdoc />
        public bool Dead => false;
        /// <inheritdoc />
        public float Gravity => 980;
        /// <inheritdoc />
        public int AttackSegment => -1;
        /// <inheritdoc />
        public bool HasPendingHurt => false;
        /// <inheritdoc />
        public bool TryStartAttack(bool attacking, float normalRatio, bool airborne) => false;
        /// <inheritdoc />
        public void ProcessSkillInput(bool attacking, float normalRatio, bool airborne) { }
        /// <inheritdoc />
        public bool TryDropThroughPlatform() => false;
        /// <inheritdoc />
        public Vector2 TakePendingHurt() => Vector2.Zero;
        /// <inheritdoc />
        public void BeginAttack(int index) { }
        /// <inheritdoc />
        public void EndAttack() { }
        /// <inheritdoc />
        public void SetFacing(int direction) { }
        /// <inheritdoc />
        public void PlayAnim(string animation) { }
        /// <inheritdoc />
        public void RestartAnim(string animation) { }
        /// <inheritdoc />
        public void PlayHurtSound() { }
        /// <inheritdoc />
        public void OnDied() { }
        /// <inheritdoc />
        public void BeginSkillAnimation(SkillActionConfig action) { }
        /// <inheritdoc />
        public void SetSkillAnimationTime(double seconds) => AnimationTime = seconds;
        /// <inheritdoc />
        public void EndSkillAnimation() { }
        /// <inheritdoc />
        public Vector2 SkillPosition { get; set; }
        /// <inheritdoc />
        public Vector2 Velocity { get; set; }
        /// <inheritdoc />
        public bool OnFloor { get; set; }
        /// <inheritdoc />
        public int Facing => 1;
        /// <summary>本招产生的独立命中编号请求。</summary>
        public List<SkillHitId> Attacks { get; } = new();
        /// <summary>最近应用到角色动画的秒数。</summary>
        public double AnimationTime { get; private set; }
        /// <summary>测试可延后触发的独立效果完成通知。</summary>
        public Dictionary<SkillEffectId, Action> Completions { get; } = new();
        /// <summary>测试提供的首个有效目标。</summary>
        public Vector2? Target { get; init; }
        /// <summary>已请求的特效。</summary>
        public List<SkillEffectId> Effects { get; } = new();
        /// <inheritdoc />
        public Vector2? FindSkillTarget(float range, float inset) => Target;
        /// <inheritdoc />
        public void NewSkillAttack(SkillHitId hit) => Attacks.Add(hit);
        /// <inheritdoc />
        public void SpawnSkillEffect(SkillEffectId effect, SkillExecution execution, Vector2 position, Action completed = null)
        {
            Effects.Add(effect);
            if (completed != null)
                Completions[effect] = completed;
        }
    }
}
