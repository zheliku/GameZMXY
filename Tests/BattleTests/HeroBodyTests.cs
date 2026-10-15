using GameLogic.Architecture;
using GameLogic.Utility;
using GameLogic.Heroes.Body;
using GameLogic.Input;
using GameLogic.Body;
using GameFramework.Fsm;
using Godot;
using Xunit;
using System;

namespace GameLogic.Battle.Tests;

/// <summary>使用真实框架状态机验证造一普攻时序、按键组合及动作打断。</summary>
public sealed class HeroBodyTests
{
    private static InputSystem Inputs => BattleArchitecture.Instance.GetSystem<InputSystem>();

    /// <summary>原版动画长度与按上一段号选取的输入冷却，均转换为秒。</summary>
    private static HeroBodyParams CreateWukongParameters() => new()
    {
        WalkSpeed = 144,
        RunSpeed = 240,
        RunDoubleTapWindow = .5f,
        JumpSpeed = 480,
        Gravity = 864,
        JumpCountMax = 2,
        HurtTime = 0.2f,
        AttackAnims = ["attack_1", "attack_2", "attack_3", "attack_4", "attack_5"],
        AttackTimes = [8f / 24, 8f / 24, 11f / 24, 20f / 24, 15f / 24],
        AttackInputCooldowns = [8f / 24, 8f / 24, 15f / 24, 18f / 24, 8f / 24],
        AirAttackIndex = 2,
        AirAttackCooldown = 15f / 24,
        ComboResetTime = 1,
    };

    /// <summary>复现连打到第四段松手：过早输入丢弃，收招后不补第五段。</summary>
    [Theory, InlineData(60), InlineData(120)]
    public void FourthHit_EarlyPressThenRelease_DoesNotQueueFifth(int hz)
    {
        using Harness h = new(hz);
        h.ReachFourth();
        h.Tap(attack: true);
        h.Run(1);
        Assert.Equal(4, h.Body.BeginCount);
        Assert.Equal("Ground", h.State);
    }

    /// <summary>第四段冷却结束后再次按下立即出第五段，无需等完整动画收招。</summary>
    [Theory, InlineData(60), InlineData(120)]
    public void FourthHit_FreshAllowedPress_StartsFifthBeforeAnimationEnds(int hz)
    {
        using Harness h = new(hz);
        h.ReachFourth();
        h.Run(15f / 24);
        Assert.Equal("attack_4", h.Body.LastAnim);
        h.Tap(attack: true);
        Assert.Equal("attack_5", h.Body.LastAnim);
        Assert.Equal(5, h.Body.BeginCount);
    }

    /// <summary>长按或在冷却期按下并继续按住都不会自动连击。</summary>
    [Theory, InlineData(60), InlineData(120)]
    public void Attack_HoldOrRejectedHold_NeverAutoRepeats(int hz)
    {
        using Harness h = new(hz);
        h.Frame(attack: true);
        h.Run(1.2f, attack: true);
        Assert.Equal(1, h.Body.BeginCount);
        h.Frame();
        h.Tap(attack: true);
        h.Tap(attack: true); // 新的一次按下仍太早；随后一直按住也不能补出下一段。
        h.Run(1, attack: true);
        Assert.Equal(2, h.Body.BeginCount);
    }

    /// <summary>第五段之后在冷却结束且超时前可重新接首段。</summary>
    [Theory, InlineData(60), InlineData(120)]
    public void Combo_FiveThenOne_UsesIndependentCooldown(int hz)
    {
        using Harness h = new(hz);
        h.ReachFourth();
        h.Run(15f / 24);
        h.Tap(attack: true);
        h.Run(18f / 24);
        h.Tap(attack: true);
        Assert.Equal("attack_1", h.Body.LastAnim);
        Assert.Equal(6, h.Body.BeginCount);
    }

    /// <summary>短间隔继续下一段，超过一秒重置到首段。</summary>
    [Theory, InlineData(60), InlineData(120)]
    public void Combo_ContinuesWithinTimeout_ResetsAfterTimeout(int hz)
    {
        using Harness h = new(hz);
        h.Tap(attack: true);
        h.Run(0.4f);
        h.Tap(attack: true);
        Assert.Equal("attack_2", h.Body.LastAnim);
        h.Run(1.1f);
        h.Tap(attack: true);
        Assert.Equal("attack_1", h.Body.LastAnim);
    }

    /// <summary>一秒边界仍继续连段，不被物理时间的浮点误差误判为超时。</summary>
    [Theory, InlineData(60), InlineData(120)]
    public void Combo_ExactlyOneSecond_DoesNotReset(int hz)
    {
        using Harness h = new(hz);
        h.Tap(attack: true);
        h.Run(1f - 2f / hz);
        h.Tap(attack: true);
        Assert.Equal("attack_2", h.Body.LastAnim);
    }

    /// <summary>冷却拒绝的尝试也刷新超时起点；仅成功出招推进段号。</summary>
    [Theory, InlineData(60), InlineData(120)]
    public void Combo_RejectedAttemptRefreshesTimeoutWithoutAdvancing(int hz)
    {
        using Harness h = new(hz);
        h.ReachFourth();
        h.Run(0.5f);
        h.Tap(attack: true);
        Assert.Equal(4, h.Body.BeginCount);
        h.Run(0.6f);
        h.Tap(attack: true);
        Assert.Equal("attack_5", h.Body.LastAnim);
    }

    /// <summary>受击立即终止普攻并归还攻击包，无双霸体保留攻击。</summary>
    [Theory, InlineData(false), InlineData(true)]
    public void Hurt_InterruptsAttackUnlessSuperArmor(bool armor)
    {
        using Harness h = new();
        h.Tap(attack: true);
        h.Body.SuperArmor = armor;
        h.Body.PendingHurt = new Vector2(-150, 0);
        h.Frame();
        Assert.Equal(armor ? "Attack" : "Hurt", h.State);
        if (!armor)
        {
            Assert.Equal(1, h.Body.EndCount);
            Assert.Equal(-150, h.Body.Velocity.X);
        }
    }

    /// <summary>硬直期间保持击退，连续受击重新播放并更新速度。</summary>
    [Fact]
    public void Hurt_RehitRestartsAndMovementDoesNotOverwriteKnockback()
    {
        using Harness h = new();
        h.Body.PendingHurt = new Vector2(-150, 0);
        h.Frame();
        h.Frame(move: 1);
        Assert.Equal(-150, h.Body.Velocity.X);
        Assert.Equal(1, h.Body.HurtSounds);
        h.Body.PendingHurt = new Vector2(120, 0);
        h.Frame();
        Assert.Equal(120, h.Body.Velocity.X);
        Assert.Equal(2, h.Body.HurtSounds);
        h.Run(0.25f);
        Assert.Equal("Ground", h.State);
    }

    /// <summary>普攻序列跨受击状态保留，硬直结束后的新输入继续下一段。</summary>
    [Theory, InlineData(60), InlineData(120)]
    public void Combo_SurvivesHurtStateTransition(int hz)
    {
        using Harness h = new(hz);
        h.Tap(attack: true);
        h.Body.PendingHurt = new Vector2(-100, 0);
        h.Frame();
        Assert.Equal("Hurt", h.State);

        h.Run(0.4f);
        h.Tap(attack: true);
        Assert.Equal("attack_2", h.Body.LastAnim);
    }

    /// <summary>不同英雄分别拥有普攻序列，互不共享段号或冷却。</summary>
    [Fact]
    public void Combo_IsOwnedIndependentlyByEachHero()
    {
        using Harness first = new();
        using Harness second = new();
        first.Tap(attack: true);
        first.Run(0.4f);
        first.Tap(attack: true);

        second.Tap(attack: true);
        Assert.Equal("attack_1", second.Body.LastAnim);
        Assert.Equal("attack_2", first.Body.LastAnim);
    }

    /// <summary>同一组共享身体状态支持不同普攻段数、动画名及空中攻击下标。</summary>
    /// <param name="segmentCount">测试英雄配置的普攻段数。</param>
    /// <param name="airAttackIndex">测试英雄指定的空中攻击下标。</param>
    /// <param name="hz">物理帧率。</param>
    [Theory, InlineData(1, 0, 60), InlineData(1, 0, 120), InlineData(3, 1, 60), InlineData(3, 1, 120)]
    public void SharedStates_UseEachHeroConfiguredAttackSequence(int segmentCount, int airAttackIndex, int hz)
    {
        string[] animations = new string[segmentCount];
        float[] durations = new float[segmentCount];
        float[] cooldowns = new float[segmentCount];
        for (int i = 0; i < segmentCount; i++)
        {
            animations[i] = $"custom_attack_{i}";
            durations[i] = 0.1f;
            cooldowns[i] = 0.2f;
        }

        HeroBodyParams parameters = CreateWukongParameters() with
        {
            AttackAnims = animations,
            AttackTimes = durations,
            AttackInputCooldowns = cooldowns,
            AirAttackIndex = airAttackIndex,
            AirAttackCooldown = 0.35f,
        };
        using Harness h = new(hz, parameters);

        // 多打一段验证按本英雄的段数循环，而非固定为悟空五段。
        for (int i = 0; i <= segmentCount; i++)
        {
            h.Tap(attack: true);
            Assert.Equal(animations[i % segmentCount], h.Body.LastAnim);
            h.Run(0.25f);
        }

        // 空中攻击也使用角色参数，不固定为悟空第三段下劈。
        h.Body.OnFloor = false;
        h.Body.Velocity = new(0, 100);
        h.Tap(attack: true);
        Assert.Equal(animations[airAttackIndex], h.Body.LastAnim);
        Assert.Equal(segmentCount + 2, h.Body.BeginCount);
    }

    /// <summary>无双解除硬直，横向和起跳倍率即时生效。</summary>
    [Fact]
    public void Musou_ReleasesHurtAndAppliesMotionMultipliers()
    {
        using Harness h = new();
        h.Body.MoveSpeedMultiplier = 1.5f;
        h.Body.JumpSpeedMultiplier = 1.5f;
        h.Frame(move: 1);
        Assert.Equal(216, h.Body.Velocity.X);
        h.Body.PendingHurt = new Vector2(-150, 0);
        h.Frame();
        Assert.Equal("Hurt", h.State);
        h.Body.SuperArmor = true;
        h.Frame();
        Assert.Equal("Ground", h.State);
        h.Tap(jump: true);
        Assert.True(h.Body.Velocity.Y < -680);
    }

    /// <summary>攻击和方向同帧按下时朝向先更新，起手后地面定身。</summary>
    [Fact]
    public void Attack_InitialDirectionUpdatesFacing()
    {
        using Harness h = new();
        h.Frame(move: -1, attack: true);
        Assert.Equal(-1, h.Body.Facing);
        Assert.Equal(0, h.Body.Velocity.X);
    }

    /// <summary>死亡始终优先于输入和受击，且终态不再出招。</summary>
    [Fact]
    public void Death_AlwaysInterruptsAndRemainsTerminal()
    {
        using Harness h = new();
        h.Tap(attack: true);
        h.Body.Dead = true;
        h.Body.PendingHurt = new Vector2(-150, 0);
        h.Frame();
        Assert.Equal("Death", h.State);
        Assert.Equal(1, h.Body.DiedCount);
        h.Tap(attack: true);
        h.Tap(jump: true);
        Assert.Equal("Death", h.State);
        Assert.Equal(1, h.Body.BeginCount);
    }

    /// <summary>J＋K 原版不出招也不起跳；未学习 W＋J 时同样不回退普攻。</summary>
    [Theory, InlineData(false), InlineData(true)]
    public void KeyCombinations_DoNotFallThroughToNormalAttack(bool up)
    {
        using Harness h = new();
        h.Frame(attack: true, jump: !up, up: up);
        Assert.Equal("Ground", h.State);
        Assert.Equal(0, h.Body.BeginCount);
        Assert.Equal(0, h.Body.ActionData.JumpCount);
    }

    /// <summary>S＋J 可以普攻，S＋K 仅在单向平台执行下落，不变成跳跃。</summary>
    [Theory, InlineData(false), InlineData(true)]
    public void DownKeyCombinations_AttackOrPlatformDrop(bool platform)
    {
        using Harness h = new();
        h.Body.CanDrop = platform;
        h.Frame(down: true, jump: true);
        Assert.Equal(platform ? 1 : 0, h.Body.DropCount);
        Assert.Equal(platform ? "Air" : "Ground", h.State);
        h.Frame();
        h.Frame(down: true, attack: true);
        Assert.Equal(platform ? "attack_3" : "attack_1", h.Body.LastAnim);
    }

    /// <summary>跳跃次数耗尽的输入被消费，不会在落地时补跳。</summary>
    [Fact]
    public void Jump_ExhaustedRequestDoesNotSurviveLanding()
    {
        using Harness h = new();
        h.Body.OnFloor = false;
        h.Body.ActionData.JumpCount = 2;
        h.Body.Velocity = new(0, 100);
        h.Tap(jump: true);
        h.Body.OnFloor = true;
        h.Body.ActionData.JumpCount = 0;
        h.Body.Velocity = Vector2.Zero;
        h.Frame();
        Assert.Equal("Ground", h.State);
        Assert.Equal(0, h.Body.ActionData.JumpCount);
    }

    /// <summary>跳跃需松开再按才使用第二次；第三次不会起跳。</summary>
    [Fact]
    public void Jump_HoldDoesNotDoubleJump_ReleaseRearms()
    {
        using Harness h = new();
        h.Frame(jump: true);
        h.Body.OnFloor = false;
        h.Run(0.1f, jump: true);
        Assert.Equal(1, h.Body.ActionData.JumpCount);
        h.Tap(jump: true);
        Assert.Equal(2, h.Body.ActionData.JumpCount);
        h.Tap(jump: true);
        Assert.Equal(2, h.Body.ActionData.JumpCount);
    }

    /// <summary>攻击中按跳并松开会取消；持续按住则在收招后起跳一次。</summary>
    [Theory, InlineData(false), InlineData(true)]
    public void Jump_DuringAttack_RequiresStillHeldAtRecovery(bool held)
    {
        using Harness h = new();
        h.Tap(attack: true);
        h.Frame(jump: true);
        h.Run(0.4f, jump: held);
        Assert.Equal(held ? 1 : 0, h.Body.ActionData.JumpCount);
    }

    /// <summary>受击期间未消费的普攻只在仍按住时等待硬直结束，松开便取消。</summary>
    [Theory, InlineData(false), InlineData(true)]
    public void Attack_DuringHurt_RequiresStillHeldAtRecovery(bool held)
    {
        using Harness h = new();
        h.Body.PendingHurt = new Vector2(-100, 0);
        h.Frame(attack: true);
        h.Run(0.4f, attack: held);
        Assert.Equal(held ? 1 : 0, h.Body.BeginCount);
    }

    /// <summary>纯跳跃不清空连段；空中下劈清空地面序列。</summary>
    [Theory, InlineData(false), InlineData(true)]
    public void Air_OnlyAttackResetsGroundCombo(bool attack)
    {
        using Harness h = new();
        h.Tap(attack: true);
        h.Run(0.4f);
        h.Tap(jump: true);
        h.Body.OnFloor = false;
        if (attack)
        {
            h.Tap(attack: true);
            Assert.Equal("attack_3", h.Body.LastAnim);
            h.Run(0.65f);
        }
        h.Body.OnFloor = true;
        h.Body.ActionData.JumpCount = 0;
        h.Body.Velocity = Vector2.Zero;
        h.Tap(attack: true);
        Assert.Equal(attack ? "attack_1" : "attack_2", h.Body.LastAnim);
    }

    /// <summary>空中普攻保留起手方向，反向键不转身，松开起手方向即停。</summary>
    [Fact]
    public void AirAttack_LocksFacingAndExistingDirection_ReleaseStops()
    {
        using Harness h = new();
        h.Body.OnFloor = false;
        h.Frame(move: 1);
        h.Frame(move: 1, attack: true);
        h.Frame(move: 1, leftAlso: true);
        Assert.Equal(144, h.Body.Velocity.X);
        Assert.Equal(1, h.Body.Facing);
        h.Frame(move: -1);
        Assert.Equal(0, h.Body.Velocity.X);
        Assert.Equal(1, h.Body.Facing);
    }

    /// <summary>空中攻击中松开起手方向后再按回也不能恢复移动，收招后方向输入才接管。</summary>
    [Theory, InlineData(60), InlineData(120)]
    public void AirAttack_ReleaseThenRepressWaitsUntilRecovery(int hz)
    {
        using Harness h = new(hz);
        h.Body.OnFloor = false;
        h.Frame(move: 1, attack: true);
        h.Frame();
        h.Frame(move: 1);
        Assert.Equal("Attack", h.State);
        Assert.Equal(0, h.Body.Velocity.X);
        for (int i = 0; i < hz; i++)
            h.Frame(move: 1);
        Assert.Equal("Air", h.State);
        Assert.Equal(144, h.Body.Velocity.X);
    }

    /// <summary>一帧内松开并按回方向仍停止空中攻击横移，不能因最终按住状态漏掉松键。</summary>
    [Theory, InlineData(60), InlineData(120)]
    public void AirAttack_ReleaseAndRepressWithinFrameStopsMovement(int hz)
    {
        using Harness h = new(hz);
        h.Body.OnFloor = false;
        h.Frame(move: 1, attack: true);
        h.Frame(move: 1, rightReleased: true);
        Assert.Equal("Attack", h.State);
        Assert.Equal(0, h.Body.Velocity.X);
        h.Frame(move: 1);
        Assert.Equal(0, h.Body.Velocity.X);
    }

    /// <summary>双击窗口为半秒，松方向、受击和地面普攻取消奔跑。</summary>
    [Fact]
    public void Run_HalfSecondWindow_ReleaseAndNormalAttackStop()
    {
        using Harness h = new();
        h.Frame(move: 1);
        h.Run(0.4f);
        h.Frame(move: 1);
        Assert.True(h.Input.Running);
        Assert.Equal(240, h.Body.Velocity.X);
        h.Frame(move: 1, attack: true);
        Assert.False(h.Input.Running);
        Assert.Equal(0, h.Body.Velocity.X);
    }

    /// <summary>方向点击间插入动作键会打断双击识别；相反方向同时按住采用最后按下者。</summary>
    [Fact]
    public void Direction_OtherKeyBreaksDoubleTap_LastPressedWins()
    {
        HeroActionData input = new();
        Sample(input, 0.5f, 0.01f, false, true, false, true, false, false);
        Sample(input, 0.5f, 0.01f, false, false, false, false, false, true);
        Sample(input, 0.5f, 0.01f, false, true, false, true, false, false);
        Assert.False(input.Running);
        Sample(input, 0.5f, 0.01f, true, true, true, false, false, false);
        Assert.Equal(-1, input.MoveAxis);
    }

    /// <summary>半秒边界不奔跑；松开 S 能重新允许仍按住的 J，符合原版共享闩锁。</summary>
    [Fact]
    public void Input_StrictDoubleTapBoundary_AndSharedActionLatch()
    {
        HeroActionData input = new();
        Sample(input, 0.5f, 0.25f, false, true, false, true, false, false);
        Sample(input, 0.5f, 0.25f, false, false, false, false, false, false);
        Sample(input, 0.5f, 0.25f, false, true, false, true, false, false);
        Assert.False(input.Running);
        Sample(input, 0.5f, 0.01f, false, false, false, false, false, true);
        Assert.True(Inputs.ConsumeAttack(input));
        Sample(input, 0.5f, 0.01f, false, false, false, false, false, true, downHeld: true);
        Assert.False(Inputs.ConsumeAttack(input));
        Sample(input, 0.5f, 0.01f, false, false, false, false, false, true);
        Assert.True(Inputs.ConsumeAttack(input));
    }

    /// <summary>动作键一帧内松开并重按会重置共享闩锁，之后继续长按不重复消费。</summary>
    [Fact]
    public void Input_ReleaseAndRepressWithinFrameRearmsAction()
    {
        HeroActionData input = new();
        Sample(input, 0.5f, 0.01f, false, false, false, false, false, true);
        Assert.True(Inputs.ConsumeAttack(input));
        Sample(input, 0.5f, 0.01f, false, false, false, false, false, true, attackPressed: true, actionReleased: true);
        Assert.True(Inputs.ConsumeAttack(input));
        Sample(input, 0.5f, 0.01f, false, false, false, false, false, true);
        Assert.False(Inputs.ConsumeAttack(input));
    }

    /// <summary>完整待机循环能被移动和起跳立即打断。</summary>
    [Fact]
    public void Idle_WalkAndJumpInterruptImmediately()
    {
        using Harness h = new();
        h.Run(12);
        Assert.Equal("idle1", h.Body.LastAnim);
        h.Frame(move: -1);
        Assert.Equal("walk", h.Body.LastAnim);
        Assert.Equal(-144, h.Body.Velocity.X);
        h.Frame();
        Assert.Equal("idle1", h.Body.LastAnim);
        h.Tap(jump: true);
        Assert.Equal("jump", h.Body.LastAnim);
    }

    /// <summary>共用输入系统时，英雄各自的闩锁、连段、跳数与重置互不影响。</summary>
    [Fact]
    public void SharedInputSystem_KeepsHeroActionDataIndependent()
    {
        FrameInput source = new();
        source.Held.Add(InputSystem.Attack);
        InputSystem system = new(BattleArchitecture.Instance, source);
        var parameters = CreateWukongParameters();
        HeroActionData first = new(), second = new();
        system.Sample(new Hero(parameters, first), .01f);
        Assert.True(system.TryStartAttack(first, parameters, false, false));
        first.JumpCount = 2;
        Assert.Equal(-1, second.AttackSegment);
        Assert.Equal(0, second.JumpCount);
        Assert.Equal(0, second.Time);
        system.Sample(new Hero(parameters, second), .01f);
        Assert.True(system.TryStartAttack(second, parameters, false, false));
        system.Reset(second);
        Assert.Equal(0, first.AttackSegment);
        Assert.Equal(2, first.JumpCount);
        Assert.False(system.ConsumeAttack(first));
        Assert.Equal(-1, second.AttackSegment);
    }

    /// <summary>脚本输入源使测试走实际 Sample 入口，不暴露生产测试接口。</summary>
    private sealed class FrameInput : IInputSource
    {
        /// <summary>当前按住的动作集合。</summary>
        public System.Collections.Generic.HashSet<string> Held { get; } = new();
        /// <summary>本帧按下边沿。</summary>
        public System.Collections.Generic.HashSet<string> Pressed { get; } = new();
        /// <summary>本帧松开边沿。</summary>
        public System.Collections.Generic.HashSet<string> Released { get; } = new();
        /// <inheritdoc />
        public bool IsHeld(string action) => Held.Contains(action);
        /// <inheritdoc />
        public bool WasPressed(string action) => Pressed.Contains(action);
        /// <inheritdoc />
        public bool WasReleased(string action) => Released.Contains(action);
    }

    /// <summary>把测试用的帧事实装入输入源，再调用与游戏相同的二参数采样入口。</summary>
    private static void Sample(HeroActionData data, float doubleTapWindow, float dt,
        bool leftHeld, bool rightHeld, bool leftPressed, bool rightPressed, bool jumpHeld, bool attackHeld,
        bool musouPressed = false, bool downHeld = false, bool upHeld = false, bool jumpPressed = false,
        bool attackPressed = false, bool actionReleased = false, bool leftReleased = false, bool rightReleased = false)
    {
        FrameInput source = new();
        if (leftHeld)
            source.Held.Add(InputSystem.MoveLeft);
        if (rightHeld)
            source.Held.Add(InputSystem.MoveRight);
        if (jumpHeld)
            source.Held.Add(InputSystem.Jump);
        if (attackHeld)
            source.Held.Add(InputSystem.Attack);
        if (downHeld)
            source.Held.Add(InputSystem.MoveDown);
        if (upHeld)
            source.Held.Add(InputSystem.Interact);
        if (leftPressed)
            source.Pressed.Add(InputSystem.MoveLeft);
        if (rightPressed)
            source.Pressed.Add(InputSystem.MoveRight);
        if (jumpPressed)
            source.Pressed.Add(InputSystem.Jump);
        if (attackPressed)
            source.Pressed.Add(InputSystem.Attack);
        if (musouPressed)
            source.Pressed.Add(InputSystem.Musou);
        if (actionReleased)
            source.Released.Add(InputSystem.Attack);
        if (leftReleased)
            source.Released.Add(InputSystem.MoveLeft);
        if (rightReleased)
            source.Released.Add(InputSystem.MoveRight);
        InputSystem system = new(BattleArchitecture.Instance, source);
        system.Sample(new Hero(CreateWukongParameters() with { RunDoubleTapWindow = doubleTapWindow }, data), dt);
    }

    /// <summary>为真实状态机提供可控的物理宿主。</summary>
    private sealed class Hero : AbstractFakeActorBody, IHeroBody
    {
        IArchitecture IBelongToArchitecture.GetArchitecture() => BattleArchitecture.Instance;
        /// <summary>使用同一共享身体定义创建本英雄拥有的普攻序列。</summary>
        /// <param name="parameters">本测试英雄的身体参数。</param>
        public Hero(HeroBodyParams parameters, HeroActionData actions = null)
        {
            Params = parameters;
            ActionData = actions ?? new();
        }

        /// <summary>身体动作参数。</summary>
        public HeroBodyParams Params { get; }
        /// <summary>输入采样器。</summary>
        public HeroActionData ActionData { get; }
        /// <summary>测试注入的无双霸体。</summary>
        public bool SuperArmor { get; set; }
        /// <summary>测试注入的横向速度倍率。</summary>
        public float MoveSpeedMultiplier { get; set; } = 1f;
        /// <summary>测试注入的起跳速度倍率。</summary>
        public float JumpSpeedMultiplier { get; set; } = 1f;
        /// <summary>是否站在单向平台。</summary>
        public bool CanDrop;
        /// <summary>实际穿平台次数。</summary>
        public int DropCount;
        /// <summary>仅可穿平台允许 S＋K。</summary>
        /// <returns>是否执行平台下落。</returns>
        public bool TryDropThroughPlatform()
        {
            if (!CanDrop || !OnFloor)
                return false;
            DropCount++;
            ActionData.JumpCount = 1;
            OnFloor = false;
            return true;
        }
    }

    /// <summary>按指定物理频率采样并推进真实 GF 状态机。</summary>
    private sealed class Harness : IDisposable
    {
        private readonly FsmManager m_Manager = new(); // 测试独占的框架管理器。
        private readonly IFsm<IHeroBody> m_Fsm; // 真实英雄身体状态机。
        private readonly float m_Dt; // 每物理帧秒数。
        private bool m_Left, m_Right; // 上次方向采样，用于边沿。
        /// <summary>创建测试状态机。</summary>
        /// <param name="hz">物理频率。</param>
        /// <param name="parameters">可选的其他英雄参数；省略时使用悟空参数。</param>
        public Harness(int hz = 60, HeroBodyParams? parameters = null)
        {
            m_Dt = 1f / hz;
            Body = new Hero(parameters ?? CreateWukongParameters());
            m_Fsm = m_Manager.CreateFsm<IHeroBody>(Guid.NewGuid().ToString(), Body,
                new HeroGroundState(), new HeroAirState(), new HeroAttackState(), new HeroHurtState(), new HeroDeathState());
            m_Fsm.Start<HeroGroundState>();
        }
        /// <summary>测试身体。</summary>
        public Hero Body { get; }
        /// <summary>身体使用的输入。</summary>
        public HeroActionData Input => Body.ActionData;
        /// <summary>直接观测现有 IFsm，不新增生产 API。</summary>
        public string State => m_Fsm.CurrentState.GetType().Name[4..^5];
        /// <summary>采样并推进一帧。</summary>
        /// <param name="move">方向。</param>
        /// <param name="attack">按住普攻。</param>
        /// <param name="jump">按住跳跃。</param>
        /// <param name="down">按住下方向。</param>
        /// <param name="up">按住上方向。</param>
        /// <param name="leftAlso">同时按住左方向。</param>
        /// <param name="rightReleased">本帧右方向曾松开，可能已经按回。</param>
        public void Frame(int move = 0, bool attack = false, bool jump = false, bool down = false, bool up = false, bool leftAlso = false, bool rightReleased = false)
        {
            bool left = move < 0 || leftAlso, right = move > 0;
            Sample(Input, 0.5f, m_Dt, left, right, left && !m_Left, right && !m_Right, jump, attack, downHeld: down, upHeld: up,
                rightReleased: rightReleased);
            m_Left = left;
            m_Right = right;
            m_Fsm.Tick(m_Dt);
            if (Body.OnFloor && Body.Velocity.Y > 0)
                Body.Velocity = new(Body.Velocity.X, 0);
        }
        /// <summary>先释放再按下，确保物理边沿与松键闩锁。</summary>
        /// <param name="attack">普攻。</param>
        /// <param name="jump">跳跃。</param>
        public void Tap(bool attack = false, bool jump = false)
        {
            Frame();
            Frame(attack: attack, jump: jump);
        }
        /// <summary>按秒数推进，并可保持动作键。</summary>
        /// <param name="seconds">推进秒数。</param>
        /// <param name="attack">是否保持普攻。</param>
        /// <param name="jump">是否保持跳跃。</param>
        public void Run(float seconds, bool attack = false, bool jump = false)
        {
            for (int i = 0; i < (int)MathF.Ceiling(seconds / m_Dt); i++)
                Frame(attack: attack, jump: jump);
        }
        /// <summary>按原版前三级冷却进入第四段。</summary>
        public void ReachFourth()
        {
            Tap(attack: true);
            for (int i = 0; i < 3; i++)
            {
                Run(8f / 24);
                Tap(attack: true);
            }
            Assert.Equal("attack_4", Body.LastAnim);
        }
        /// <summary>释放测试独占状态机。</summary>
        public void Dispose() => m_Manager.Shutdown();
    }
}
