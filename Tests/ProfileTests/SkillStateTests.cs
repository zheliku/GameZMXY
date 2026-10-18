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

/// <summary>真实 GGF 状态验证动作规则；方法键的时序与场景装配由引擎验证，不模拟另一套时间轴。</summary>
public sealed class SkillStateTests
{
    /// <summary>同一技能的四段判定按具体方法更新，速度和取整不进入公共解释器。</summary>
    [Fact]
    public void Thrust_ConcreteStateOwnsHitPowerAndDeceleration()
    {
        using var skill = new SkillFixture(SkillId.FlameThrust, velocity: new Vector2(120, 0));
        skill.Scope.Hero.Power.Value = 11;
        var state = Assert.IsType<WukongFlameThrustState>(skill.Fsm.CurrentState);
        state.BeginThrust(skill.Actor, 360, 720);
        Assert.Equal(360, skill.Actor.Velocity.X);
        for (int i = 0; i < 3; i++)
            state.ContinueThrust(skill.Actor, .8f);
        Assert.Equal(4, skill.Actor.Attacks.Count);
        Assert.All(skill.Actor.Powers, power => Assert.Equal(6d, power));
        Assert.InRange(skill.Actor.Velocity.X, 184.31, 184.33);
        state.CompleteAnimation();
        skill.Advance(.01f);
        Assert.Null(skill.Scope.Skills.ActiveSkillId);
        Assert.Equal(0, skill.Actor.ActionData.LastGroundSegment);
    }

    /// <summary>取消后旧状态不再推进原生动画。</summary>
    [Fact]
    public void CancelledSkill_DoesNotAdvanceOldAnimation()
    {
        using var skill = new SkillFixture(SkillId.FlameThrust);
        skill.System.CancelSkill();
        skill.Advance(.1f);
        Assert.Equal(0, skill.Actor.Advances);
        Assert.Null(skill.Scope.Skills.ActiveSkillId);
    }

    /// <summary>两招分别消耗空中跳跃并读取冷却数值。</summary>
    /// <param name="id">技能编号。</param>
    [Theory]
    [InlineData(SkillId.RisingDragon)]
    [InlineData(SkillId.FlameStorm)]
    public void Entry_OwnsJumpConsumptionAndCooldown(SkillId id)
    {
        using var skill = new SkillFixture(id, jumpCount: 1);
        Assert.Equal(2, skill.Actor.ActionData.JumpCount);
        Assert.Equal(TestTables.Tables.TbSkillConfig.Get(id).NormalAttackBlockSeconds,
            skill.Actor.ActionData.AttackReadyAt);
    }

    /// <summary>中心击退属于风暴；基础威力先取整。</summary>
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

    /// <summary>无敌按游戏秒数开始，并不随动画播放结束而提前清掉。</summary>
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

    /// <summary>无目标时请求原生下降标记；中断必须解除输入锁。</summary>
    [Fact]
    public void FireDemon_NoTargetAndCancellationRestoreInput()
    {
        using var skill = new SkillFixture(SkillId.FireDemonSlash);
        var state = Assert.IsType<WukongFireDemonState>(skill.Fsm.CurrentState);
        Assert.True(skill.Actor.ActionData.InputLocked);
        state.SearchTarget(skill.Actor, null, 130, 20, 40d / 24);
        skill.Advance(.01f);
        Assert.Equal("descent", skill.Actor.LastMarker);
        skill.System.CancelSkill();
        skill.Advance(.01f);
        Assert.False(skill.Actor.ActionData.InputLocked);
    }

    /// <summary>九连斩期间停留本体，特效保持独立，击退中心取瞬移目标。</summary>
    [Fact]
    public void FireDemon_TargetOwnsTeleportAndComboPause()
    {
        using var skill = new SkillFixture(SkillId.FireDemonSlash, target: new Vector2(80, 100));
        var state = Assert.IsType<WukongFireDemonState>(skill.Fsm.CurrentState);
        state.SearchTarget(skill.Actor, null, 130, 20, 40d / 24);
        skill.Advance(1f);
        Assert.Equal(new Vector2(80, 100), skill.Actor.SkillPosition);
        Assert.Equal(0, skill.Actor.Advances);
        Assert.Equal(SkillHitId.FireDemonCombo, Assert.Single(skill.Actor.Effects));
        Assert.Equal(80f, Assert.Single(skill.Actor.EffectOrigins));
        skill.Advance(1f);
        Assert.Equal(1, skill.Actor.Advances);
    }

    /// <summary>状态复用后旧落地通知不能跳转新施放。</summary>
    [Fact]
    public void LandingCallback_CannotChangeNextExecution()
    {
        using var skill = new SkillFixture(SkillId.FireDemonSlash);
        var state = Assert.IsType<WukongFireDemonState>(skill.Fsm.CurrentState);
        typeof(WukongFireDemonState).GetMethod("SpawnLanding", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).Invoke(state, [skill.Actor, null, true]);
        Action oldCompletion = skill.Actor.Completions[SkillHitId.FireDemonLanding];
        skill.System.CancelSkill();
        skill.Advance(.01f);
        skill.Scope.Hero.Mp.Value = 100;
        Assert.True(skill.System.TryActivate(SkillId.FireDemonSlash, skill.Actor));
        skill.Advance(.01f);
        var current = skill.Scope.Skills.ActiveSkillId;
        oldCompletion();
        skill.Advance(.01f);
        Assert.Equal(current, skill.Scope.Skills.ActiveSkillId);
        Assert.Null(skill.Actor.LastMarker);
    }

    /// <summary>真实框架状态机夹具；只替换引擎物理与动画节点。</summary>
    private sealed class SkillFixture : IDisposable
    {
        private readonly FsmManager m_Manager = new(); // 本测试拥有的真实 GGF 状态机管理器。
        public readonly IFsm<IHeroBody> Fsm; // 按物理帧驱动的正式状态集合。
        /// <summary>独立模型作用域。</summary>
        public TestGameScope Scope { get; } = new(1, [(1, 0)], 9999, 20);
        /// <summary>角色表现替身。</summary>
        public Motion Actor { get; }
        /// <summary>真实技能系统。</summary>
        public SkillSystem System => Scope.GetSystem<SkillSystem>();

        /// <summary>装配并直接进入指定技能自己的状态。</summary>
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

        /// <summary>推进一帧真实身体状态。</summary>
        /// <param name="dt">游戏秒数。</param>
        public void Advance(float dt)
        {
            Actor.ActionData.Time += dt;
            Fsm.Tick(dt);
            if (Fsm.CurrentState is AbstractHeroSkillState state)
                state.AdvanceAnimation(Fsm, Actor, dt);
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
        public void BeginSkillAnimation(string animation, string startMarker = null) => LastMarker = startMarker;
        /// <inheritdoc />
        public void AdvanceSkillAnimation(float dt) => Advances++;
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
        public List<double> Powers { get; } = new();
        /// <summary>具体状态传入的击退中心；为空表示沿出招朝向。</summary>
        public List<float?> AttackOrigins { get; } = new();
        /// <summary>最近应用到角色动画的秒数。</summary>
        public int Advances { get; private set; }
        public string LastMarker { get; private set; }
        /// <summary>测试可延后触发的独立效果完成通知。</summary>
        public Dictionary<SkillHitId, Action> Completions { get; } = new();
        /// <summary>测试提供的首个有效目标。</summary>
        public Vector2? Target { get; init; }
        /// <summary>已请求的特效。</summary>
        public List<SkillHitId> Effects { get; } = new();
        /// <summary>具体招式提供的特效击退中心；空值沿出招朝向。</summary>
        public List<float?> EffectOrigins { get; } = new();
        /// <inheritdoc />
        public Vector2? FindSkillTarget(float range, float inset) => Target;
        /// <inheritdoc />
        public void NewSkillAttack(SkillHitId hit, double power, float? knockbackOriginX = null)
        {
            Attacks.Add(hit);
            Powers.Add(power);
            AttackOrigins.Add(knockbackOriginX);
        }
        /// <inheritdoc />
        public void SpawnSkillEffect(PackedScene scene, Vector2 position, SkillHitId hit = SkillHitId.None, double power = 0, Action completed = null,
            float? knockbackOriginX = null)
        {
            Effects.Add(hit);
            EffectOrigins.Add(knockbackOriginX);
            if (completed != null)
                Completions[hit] = completed;
        }
    }
}
