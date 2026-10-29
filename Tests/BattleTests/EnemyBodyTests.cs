using GameLogic.Enemies.Body;
using System;
using System.Collections.Generic;
using System.Linq;
using GameFramework.Fsm;
using GameLogic.Body;
using GameLogic.Config;
using Godot;
using Xunit;

namespace GameLogic.Battle.Tests
{
    /// <summary>
    /// 怪物身体状态机回归：框架真实 FsmManager + 假宿主，按 <c>fsm.Tick(dt)</c> 逐物理帧驱动。
    /// AI 的意图（移动、出招请求）由测试直接写进假宿主，验证"AI 只写意图、身体是唯一权威"的边界；
    /// 动作时长 = 参数里的动画长度快照，状态自己计时（A 方案：动画纯数据）。
    /// </summary>
    public class EnemyBodyTests
    {
        private const float Dt = 1f / 60f; // 单个物理帧的时长。

        /// <summary>
        /// 每种移动预设都经真实FSM出招、受击再回到各自入口，重建不复用旧状态对象。
        /// </summary>
        [Fact]
        public void MovementPresets_AttackAndHurtReturnToSelectedMode_WithIndependentStates()
        {
            EnemyBodyPreset[] presets = [EnemyBodyPresets.Walk(), EnemyBodyPresets.Fly(288f, 96f),
                EnemyBodyPresets.Swim(288f, 96f, 72f)];
            foreach (EnemyBodyPreset preset in presets)
            {
                using EnemyHarness h = new(Params(), preset);
                string movement = h.State;
                h.Body.OnFloor = movement == "Walk";
                h.Body.MoveIntent = 1;
                h.Body.VerticalIntent = 1;
                h.Step(1);
                Assert.Equal(80f, h.Body.Velocity.X);

                h.Body.AttackRequest = 0;
                h.Step(1);
                Assert.Equal("Attack", h.State);
                Assert.InRange(h.StepUntil(state => state == movement), 1, 100);
                Assert.Equal(1, h.Body.EndCount);

                h.Body.PendingHurt = new Vector2(-20f, -40f);
                h.Step(1);
                Assert.Equal("Hurt", h.State);
                Assert.InRange(h.StepUntil(state => state == movement), 1, 30);
                var first = preset.CreateStates();
                var second = preset.CreateStates();
                Assert.Equal(first.Select(state => state.GetType()), second.Select(state => state.GetType()));
                Assert.All(first, state => Assert.DoesNotContain(second, other => ReferenceEquals(state, other)));
            }
        }

        /// <summary>
        /// 可组合预设拒绝缺少入口或重复状态，避免首次切换才报错。
        /// </summary>
        [Fact]
        public void BodyPreset_RejectsMissingRolesAndDuplicateStates()
        {
            EnemyBodyPreset missing = new(typeof(EnemyFlyState), typeof(EnemyDeathState),
                () => new EnemyWalkState(), () => new EnemyDeathState());
            EnemyBodyPreset duplicate = new(typeof(EnemyWalkState), typeof(EnemyDeathState),
                () => new EnemyWalkState(), () => new EnemyWalkState(), () => new EnemyDeathState());
            Assert.Throws<InvalidOperationException>(() => missing.CreateStates());
            Assert.Throws<InvalidOperationException>(() => duplicate.CreateStates());
        }

        /// <summary>
        /// 创建用于状态机测试的怪物参数。
        /// </summary>
        /// <param name="attackTime">测试攻击动作的持续秒数。</param>
        /// <param name="recovery">攻击后的收招硬直秒数。</param>
        /// <param name="hurtTime">测试受击动作的持续秒数。</param>
        /// <param name="deathTime">测试死亡动作的持续秒数。</param>
        /// <returns>本夹具独立使用的敌人身体参数。</returns>
        private static EnemyBodyParams Params(float attackTime = 0.1f, float recovery = 0.3f,
            float hurtTime = 0.2f, float deathTime = 0.5f)
        {
            return new EnemyBodyParams
            {
                MoveSpeed = 80f,
                Gravity = 980f,
                HurtTime = hurtTime,
                DeathTime = deathTime,
                AttackAnims = ["attack_1"],
                AttackTimes = [attackTime],
                AttackRecovery = [recovery],
            };
        }

        /// <summary>
        /// 验证移动意图控制怪物速度、动画和朝向。
        /// </summary>
        [Fact]
        public void Move_FollowsIntent_AndPicksAnim()
        {
            using EnemyHarness h = new EnemyHarness(Params());
            h.Step(1);
            Assert.Equal("Walk", h.State);
            Assert.Equal("idle", h.Body.LastAnim);

            h.Body.MoveIntent = 1;
            h.Step(1);
            Assert.Equal("run", h.Body.LastAnim);
            Assert.Equal(80f, h.Body.Velocity.X, 3);
            Assert.Equal(1, h.Body.Facing);
        }

        /// <summary>
        /// 空中请求作废，落地只执行新的请求。
        /// </summary>
        [Fact]
        public void Airborne_DropsAttackRequest_UntilGrounded()
        {
            using EnemyHarness h = new(Params());
            h.Body.OnFloor = false;
            h.Body.AttackRequest = 0;
            h.Step(1);
            Assert.Equal("Walk", h.State);
            Assert.Equal(0, h.Body.BeginCount);
            Assert.True(h.Body.Velocity.Y > 0);
            h.Body.OnFloor = true;
            h.Step(1);
            Assert.Equal("Walk", h.State); // 空中的旧请求不会在落地时突然释放。
            h.Body.AttackRequest = 0;
            h.Step(1);
            Assert.Equal("Attack", h.State);
        }

        /// <summary>
        /// 验证怪物攻击、收招硬直及其后的移动状态。
        /// </summary>
        [Fact]
        public void Attack_Request_Recovery_ThenMove()
        {
            using EnemyHarness h = new EnemyHarness(Params());
            h.Body.MoveIntent = 1;
            h.Body.AttackRequest = 0;
            h.Step(1);
            Assert.Equal("Attack", h.State);
            Assert.Equal("attack_1", h.Body.LastAnim);
            Assert.Equal(0, h.Body.AttackSegment);
            Assert.Equal(1, h.Body.BeginCount);
            Assert.Equal(0f, h.Body.Velocity.X, 3);       // 出招定身

            h.StepUntil(s => s == "Recovery");            // 招式时长走完 → 收招硬直
            Assert.Equal(-1, h.Body.AttackSegment);
            Assert.Equal(1, h.Body.EndCount);

            h.Body.MoveIntent = 1;
            h.Body.AttackRequest = 0;                      // 硬直中的请求不被消费
            h.Step(15);                                    // 0.3s 内
            Assert.Equal("Recovery", h.State);
            h.StepUntil(s => s == "Attack");               // 硬直结束当帧 Move 消费请求
            Assert.Equal("Attack", h.State);
        }

        /// <summary>
        /// 验证零收招时长的攻击直接返回移动状态。
        /// </summary>
        [Fact]
        public void Attack_ZeroRecovery_ReturnsToMove()
        {
            using EnemyHarness h = new EnemyHarness(Params(recovery: 0f));
            h.Body.AttackRequest = 0;
            h.Step(1);
            h.StepUntil(s => s == "Walk");
            Assert.Equal("Walk", h.State);
        }

        /// <summary>
        /// 切换数据传递实际请求的招式和收招时间，不能固定使用第一招。
        /// </summary>
        [Fact]
        public void Attack_SecondRequestUsesItsAnimationAndRecovery()
        {
            EnemyBodyParams parameters = Params() with
            {
                AttackAnims = ["attack_1", "attack_2"],
                AttackTimes = [0.1f, 0.2f],
                AttackRecovery = [0.3f, 0.5f],
            };
            using EnemyHarness h = new(parameters);
            h.Body.AttackRequest = 1;
            h.Step(1);
            Assert.Equal("attack_2", h.Body.LastAnim);
            Assert.Equal(1, h.Body.AttackSegment);

            h.StepUntil(state => state == "Recovery");
            h.Step(24);
            Assert.Equal("Recovery", h.State);
            h.StepUntil(state => state == "Walk");
            Assert.Equal("Walk", h.State);
        }

        /// <summary>
        /// 验证受击会中断攻击并清除未处理的攻击请求。
        /// </summary>
        [Fact]
        public void Hurt_InterruptsAttack_AndDropsRequest()
        {
            using EnemyHarness h = new EnemyHarness(Params());
            h.Body.AttackRequest = 0;
            h.Step(1);
            Assert.Equal("Attack", h.State);

            h.Body.PendingHurt = new Vector2(120f, 0f);
            h.Body.AttackRequest = 0;
            h.Step(1);
            Assert.Equal("Hurt", h.State);
            Assert.Equal("hurt", h.Body.LastAnim);
            Assert.Equal(-1, h.Body.AttackSegment);
            Assert.Equal(1, h.Body.EndCount);
            Assert.Equal(120f, h.Body.Velocity.X, 3);

            h.Step(10);                                    // 硬直时长内不结束
            Assert.Equal("Hurt", h.State);
            h.StepUntil(s => s == "Walk");
            Assert.Equal("Walk", h.State);
            Assert.Equal(-1, h.Body.AttackRequest);        // 硬直中的请求已作废
        }

        /// <summary>
        /// 验证受击会中断收招硬直。
        /// </summary>
        [Fact]
        public void Hurt_InterruptsRecovery()
        {
            using EnemyHarness h = new EnemyHarness(Params(recovery: 1f));
            h.Body.AttackRequest = 0;
            h.Step(1);
            h.StepUntil(s => s == "Recovery");
            Assert.Equal("Recovery", h.State);

            h.Body.PendingHurt = new Vector2(-50f, 0f);
            h.Step(1);
            Assert.Equal("Hurt", h.State);
        }

        /// <summary>
        /// 官方受击结束直接恢复移动，不能因资源中存在备用姿势而增加硬直阶段。
        /// </summary>
        /// <param name="frequency">本次物理推进频率，单位为Hz。</param>
        [Theory]
        [InlineData(24)]
        [InlineData(60)]
        [InlineData(120)]
        public void Hurt_SourceRecoveryHasNoExtraPhase(int frequency)
        {
            using EnemyHarness h = new(Params(hurtTime: .5f));
            h.Body.PendingHurt = new Vector2(24f, 0f);
            h.Step(1, 1f / frequency);
            Assert.Equal("hurt", h.Body.LastAnim);
            h.Step(frequency / 2 - 1, 1f / frequency);
            Assert.Equal("Hurt", h.State);
            h.Step(2, 1f / frequency);
            Assert.Equal("Walk", h.State);
        }

        /// <summary>
        /// 官方受击横速按秒指数衰减，不能因物理帧率或怪物体型改变。
        /// </summary>
        /// <param name="frequency">物理帧率。</param>
        [Theory]
        [InlineData(24)]
        [InlineData(60)]
        [InlineData(120)]
        public void Hurt_DampingMatchesSourceAcrossPhysicsRates(int frequency)
        {
            var config = BattleArchitecture.Instance.GetModel<ConfigurationModel>().Tables.TbBattleConfig.Data;
            using EnemyHarness h = new(Params(hurtTime: 2) with
            {
                MoveSpeed = 72f,
                KnockbackRetentionPerSecond = config.EnemyKnockbackRetentionPerSecond,
            });
            h.Body.PendingHurt = new Vector2(35 * 24, -2 * 24);
            h.Step(frequency / 4, 1f / frequency);
            Assert.Equal(840 * MathF.Pow(.8f, 6), h.Body.Velocity.X, .01f);
            h.Step(frequency / 2, 1f / frequency);
            Assert.Equal(72f, h.Body.Velocity.X, .01f);

            // 小于移动速度的轻击不被衰减，也不能被强行抬高到移动速度。
            h.Body.PendingHurt = new Vector2(-2 * 24, -22 * 24);
            h.Step(1, 1f / frequency);
            Assert.Equal(-48f, h.Body.Velocity.X);
        }

        /// <summary>
        /// 官方先位移后加重力；秒制物理在各帧率重现同一升龙抛物线。
        /// </summary>
        /// <param name="frequency">物理帧率。</param>
        [Theory]
        [InlineData(24)]
        [InlineData(60)]
        [InlineData(120)]
        public void Hurt_LaunchPreservesOfficialDisplacement(int frequency)
        {
            using EnemyHarness h = new(Params(hurtTime: .5f) with { Gravity = 864 });
            h.Body.OnFloor = false;
            h.Body.PendingHurt = new Vector2(48, -528);
            float y = 0, minimumY = 0, dt = 1f / frequency;
            for (int step = 1; step <= frequency; step++)
            {
                h.Step(1, dt);
                y += h.Body.Velocity.Y * dt;
                minimumY = Math.Min(minimumY, y);
                double t = (double)step / frequency;
                double sourceY = -528 * t + 864 * t * (t - 1d / 24) / 2;
                Assert.Equal(sourceY, y, .002);
            }
            Assert.InRange(-minimumY, 172.4, 172.6);
        }

        /// <summary>
        /// 验证宿主不登记受击时霸体攻击不会被中断。
        /// </summary>
        [Fact]
        public void SuperArmor_HostNotRegisteringHurt_KeepsAttack()
        {
            // 霸体由宿主"不登记受击"表达：身体状态机看不到受击，出招照常走完（0.1s → Recovery）
            using EnemyHarness h = new EnemyHarness(Params());
            h.Body.AttackRequest = 0;
            h.Step(1);
            h.Step(10);
            Assert.Equal("Recovery", h.State);   // 没被打断，正常收招
        }

        /// <summary>
        /// 验证死亡优先处理、一次性副作用及延时回收。
        /// </summary>
        [Fact]
        public void Death_Priority_SideEffectsOnce_RecycleAfterDeathTime()
        {
            using EnemyHarness h = new EnemyHarness(Params());
            h.Body.AttackRequest = 0;
            h.Step(1);
            h.Body.Dead = true;
            h.Body.PendingHurt = new Vector2(10f, 0f);
            h.Step(1);
            Assert.Equal("Death", h.State);
            Assert.Equal("death", h.Body.LastAnim);
            Assert.Equal(1, h.Body.DiedCount);
            Assert.Equal(-1, h.Body.AttackSegment);

            h.Step(28);                                    // deathTime 0.5s 内不回收
            Assert.Equal("Death", h.State);
            Assert.Equal(0, h.Body.RecycleCount);
            h.StepUntil(s => h.Body.RecycleCount > 0);     // 死亡时长走完才回收
            Assert.Equal(1, h.Body.RecycleCount);
            h.Step(30);
            Assert.Equal(1, h.Body.RecycleCount);          // 只请求一次
            Assert.Equal(1, h.Body.DiedCount);
        }

        /// <summary>
        /// 死亡保留致死击退，使用与活体受击相同的秒制抛飞轨迹。
        /// </summary>
        [Fact]
        public void Death_PreservesLethalKnockbackMotion()
        {
            var parameters = Params(hurtTime: 2, deathTime: 2) with
            {
                Gravity = 864,
                MoveSpeed = 72,
                KnockbackRetentionPerSecond = MathF.Pow(.8f, 24),
            };
            using EnemyHarness hurt = new(parameters);
            using EnemyHarness dead = new(parameters);
            hurt.Body.OnFloor = dead.Body.OnFloor = false;
            hurt.Body.PendingHurt = dead.Body.PendingHurt = new Vector2(840, -528);
            dead.Body.Dead = true;
            for (int frame = 0; frame < 20; frame++)
            {
                hurt.Step(1);
                dead.Step(1);
                Assert.Equal(hurt.Body.Velocity.X, dead.Body.Velocity.X, .001f);
                Assert.Equal(hurt.Body.Velocity.Y, dead.Body.Velocity.Y, .001f);
            }
        }

        /// <summary>
        /// 官方空中尸体在第9帧起的原生分段循环，落地后才在末帧销毁。
        /// </summary>
        [Fact]
        public void Death_AirLoopWaitsForGroundContact()
        {
            using EnemyHarness h = new(Params(deathTime: 14f / 24) with { DeathAirLoopTime = 8f / 24 });
            h.Body.OnFloor = false;
            h.Body.Dead = true;
            h.Step(90);
            Assert.Equal("Death", h.State);
            Assert.Equal(0, h.Body.RecycleCount);
            Assert.Equal(EnemyAnims.DeathAirLoopMarker, h.Body.LastStartMarker);
            Assert.Equal(1, h.Body.DiedCount);

            h.Body.OnFloor = true;
            Assert.InRange(h.StepUntil(_ => h.Body.RecycleCount > 0), 1, 17);
            Assert.Equal(1, h.Body.RecycleCount);
        }

        /// <summary>
        /// 验证状态机关闭时会结束进行中的攻击。
        /// </summary>
        [Fact]
        public void Shutdown_DuringAttack_ReleasesAttack()
        {
            EnemyHarness h = new EnemyHarness(Params());
            h.Body.AttackRequest = 0;
            h.Step(1);
            h.Dispose();
            Assert.Equal(1, h.Body.EndCount);
        }

        // ---------------------------------------------------------------- 假宿主与驱动

        /// <summary>
        /// 为三种移动状态提供可控测试意图。
        /// </summary>
        private sealed class FakeEnemy : AbstractFakeActorBody, IEnemyFreeMoveBody
        {
            /// <inheritdoc />
            float IActorBody.Gravity
            {
                get
                {
                    return Params.Gravity;
                }
            }
            /// <summary>
            /// 待身体状态机消费的攻击段索引。
            /// </summary>
            public int AttackRequest = -1;
            /// <summary>
            /// 请求回收的次数。
            /// </summary>
            public int RecycleCount;

            /// <summary>
            /// 怪物身体状态参数。
            /// </summary>
            public EnemyBodyParams Params { get; }

            /// <inheritdoc />
            public Type MovementStateType { get; }

            /// <inheritdoc />
            public int VerticalIntent { get; set; }

            /// <inheritdoc />
            public bool DirectVerticalMovement { get; set; }

            /// <inheritdoc />
            public Type DeathStateType
            {
                get
                {
                    return typeof(EnemyDeathState);
                }
            }

            /// <summary>
            /// AI 写入的移动方向意图。
            /// </summary>
            public int MoveIntent { get; set; }

            /// <summary>
            /// 初始化怪物身体状态参数。
            /// </summary>
            /// <param name="p">本夹具借用的敌人身体状态参数。</param>
            /// <param name="movementStateType">本测试使用的同级移动状态类型。</param>
            public FakeEnemy(EnemyBodyParams p, Type movementStateType)
            {
                Params = p;
                MovementStateType = movementStateType;
            }

            /// <summary>
            /// 取出并清除待处理的攻击请求。
            /// </summary>
            /// <returns>待处理的招式编号；没有请求时为-1。</returns>
            public int TakeAttackRequest()
            {
                int r = AttackRequest;
                AttackRequest = -1;
                return r;
            }

            /// <summary>
            /// 记录一次实体回收请求。
            /// </summary>
            public void RequestRecycle()
            {
                RecycleCount++;
            }
        }

        /// <summary>
        /// 用真实框架状态机驱动怪物身体测试。
        /// </summary>
        private sealed class EnemyHarness : IDisposable
        {
            private readonly FsmManager m_Manager = new FsmManager(); // 驱动测试状态机的管理器。
            private readonly IFsm<IEnemyBody> m_Fsm; // 当前测试使用的怪物身体状态机。
            private bool m_Disposed; // 记录驱动器是否已关闭。

            /// <summary>
            /// 状态机绑定的假怪物宿主。
            /// </summary>
            public FakeEnemy Body { get; }

            /// <summary>
            /// 当前身体状态名称。
            /// </summary>
            public string State
            {
                get
                {
                    return m_Fsm.CurrentState.GetType().Name["Enemy".Length..^"State".Length];
                }
            }

            /// <summary>
            /// 创建假宿主并启动怪物身体状态机。
            /// </summary>
            /// <param name="p">本夹具借用的敌人身体状态参数。</param>
            /// <param name="preset">身体状态预设；未指定时使用Walk预设。</param>
            public EnemyHarness(EnemyBodyParams p, EnemyBodyPreset preset = null)
            {
                preset ??= EnemyBodyPresets.Walk();
                Body = new FakeEnemy(p, preset.MovementStateType);
                m_Fsm = m_Manager.CreateFsm<IEnemyBody>(Guid.NewGuid().ToString(), Body, preset.CreateStates());
                m_Fsm.Start(preset.MovementStateType);
            }

            /// <summary>
            /// 推进指定数量的物理帧，并固定宿主在地面上。
            /// </summary>
            /// <param name="frames">推进的物理帧数。</param>
            /// <param name="dt">每步游戏秒数。</param>
            public void Step(int frames, float dt = Dt)
            {
                for (int i = 0; i < frames; i++)
                {
                    m_Fsm.Tick(dt);
                    if (Body.OnFloor)
                    {
                        Body.Velocity = new Vector2(Body.Velocity.X, 0f);
                    }
                }
            }

            /// <summary>
            /// 逐帧推进至条件成立，最多检查 600 帧。
            /// </summary>
            /// <param name="until">返回是否停止推进的状态条件。</param>
            /// <returns>满足条件前推进的帧数；超时返回 -1。</returns>
            public int StepUntil(Func<string, bool> until)
            {
                // 每帧先检查状态，再推进一帧，避免把满足条件的帧多算一次。
                for (int i = 0; i < 600; i++)
                {
                    if (until(State))
                    {
                        return i;
                    }

                    Step(1);
                }

                return -1;
            }

            /// <summary>
            /// 关闭状态机管理器；重复调用不会重复关闭。
            /// </summary>
            public void Dispose()
            {
                if (m_Disposed)
                {
                    return;
                }

                m_Disposed = true;
                m_Manager.Shutdown();
            }
        }
    }
}
