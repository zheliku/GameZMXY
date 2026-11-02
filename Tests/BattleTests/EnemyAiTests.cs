using System;
using System.Runtime.CompilerServices;
using System.Linq;
using GameConfig.Enemy;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Enemies.AI;
using GameLogic.Enemies.Body;
using GameLogic.Enemies;
using Godot;
using Xunit;

namespace GameLogic.Battle.Tests
{
    /// <summary>
    /// 真实GGF状态机的AI规则回归；原生范围、死亡和复用由引擎验证。
    /// </summary>
    public class EnemyAiTests
    {
        /// <summary>
        /// 双招小怪每次节拍优先冷却技能，首领近远三招共享规则；保持各自配置与冷却独立。
        /// </summary>
        [Fact]
        public void SharedEngageRulesPreserveSkillPriority()
        {
            using (var dual = new Harness(0, _ => new(typeof(EngageState), typeof(EngageState),
                () => new EngageState(cooldownAttackIndex: 1)), 2))
            {
                dual.Agent.HasTarget = true;
                dual.Agent.TargetOffset = Vector2.Zero;
                dual.Step(1);
                Assert.Equal(1, dual.Agent.AiData.AttackRequest);
                dual.Agent.IsAttacking = false;
                dual.Agent.AiData.Cooldowns[1] = 5;
                dual.Step(1);
                Assert.Equal(0, dual.Agent.AiData.AttackRequest);
            }
            using var boss = new Harness(preset: _ => new(typeof(DistanceSkillEngageState), typeof(DistanceSkillEngageState),
                () => new DistanceSkillEngageState()), attackCount: 3);
            boss.Agent.HasTarget = true;
            boss.Agent.TargetOffset = new(200, 0);
            boss.Step(.01f);
            Assert.Equal(2, boss.Agent.AiData.AttackRequest);
            boss.Agent.IsAttacking = false;
            boss.Agent.AiData.Cooldowns[2] = 10;
            boss.Step(.01f);
            Assert.Equal(1, boss.Agent.AiData.MoveIntent);
            boss.Agent.TargetOffset = Vector2.Zero;
            boss.Step(1);
            Assert.Equal(1, boss.Agent.AiData.AttackRequest);
            boss.Agent.IsAttacking = false;
            boss.Agent.AiData.Cooldowns[1] = 10;
            boss.Step(1);
            Assert.Equal(0, boss.Agent.AiData.AttackRequest);
        }

        /// <summary>
        /// 原生场景注入的两段距离决定牛魔选招，不需要构造具体实体。
        /// </summary>
        /// <param name="distance">目标水平距离。</param>
        /// <param name="expected">期望的招式下标。</param>
        [Theory]
        [InlineData(70, 4)]
        [InlineData(90, 3)]
        [InlineData(150, 1)]
        public void BullUsesInjectedDistanceBands(float distance, int expected)
        {
            using var h = new Harness(preset: _ => new(typeof(BullDemonKingEngageState), typeof(BullDemonKingEngageState),
                () => new BullDemonKingEngageState(80, 100)), attackCount: 5);
            h.Agent.HasTarget = true;
            h.Agent.EngageBounds = new(-200, -200, 400, 400);
            h.Agent.TargetOffset = new(distance, 0);
            h.Step(.01f);
            Assert.Equal(expected, h.Agent.AiData.AttackRequest);
        }

        /// <summary>
        /// 龙王近身水柱与中距离冰系分支使用显式距离；同时可用时保留原随机选择。
        /// </summary>
        [Fact]
        public void DragonUsesInjectedColumnDistanceAndIndependentCooldowns()
        {
            using var h = new Harness(preset: _ => new(typeof(DragonKingEngageState), typeof(DragonKingEngageState),
                () => new DragonKingEngageState(100, 100)), attackCount: 4);
            h.Agent.HasTarget = true;
            h.Agent.EngageBounds = new(-450, -200, 900, 400);
            h.Agent.TargetOffset = new(80, 0);
            h.Step(.01f);
            Assert.Equal(3, h.Agent.AiData.AttackRequest);
            h.Agent.IsAttacking = false;
            h.Agent.TargetOffset = new(200, 0);
            h.Step(.01f);
            Assert.Equal(1, h.Agent.AiData.AttackRequest);
            h.Agent.IsAttacking = false;
            h.Agent.AiData.Cooldowns[1] = 10;
            h.Step(.01f);
            Assert.Equal(2, h.Agent.AiData.AttackRequest);
        }

        /// <summary>
        /// 凤凰飞行和落地事实必须实时读取；离地未转飞行时不误出招，阶段改变后按空中规则执行。
        /// </summary>
        [Fact]
        public void GarudaReadsLivePhaseWithoutEntityReference()
        {
            bool flying = false;
            bool onFloor = false;
            using var h = new Harness(preset: _ => new(typeof(PengDemonKingEngageState), typeof(PengDemonKingEngageState),
                () => new PengDemonKingEngageState(300, 100, () => flying, () => onFloor)), attackCount: 4);
            h.Agent.HasTarget = true;
            h.Agent.TargetOffset = new(200, 0);
            h.Step(1);
            Assert.Equal(0, h.Agent.RequestCount);
            flying = true;
            h.Step(1);
            Assert.Equal(0, h.Agent.AiData.AttackRequest);
            h.Agent.IsAttacking = false;
            flying = false;
            onFloor = true;
            h.Agent.TargetOffset = new(50, 0);
            h.Step(1);
            Assert.Equal(3, h.Agent.AiData.AttackRequest);
        }

        /// <summary>
        /// 两种飞行小怪仅悬停高度不同，退避沿同一时钟并读取当前高度，不复制节点状态。
        /// </summary>
        /// <param name="pursuitHeight">物种追踪高度。</param>
        /// <param name="vertical">追踪阶段预期垂直意图。</param>
        [Theory]
        [InlineData(60, 1)]
        [InlineData(90, -1)]
        public void FlyingPursuitPreservesHeightAndRetreat(float pursuitHeight, int vertical)
        {
            float altitude = 300;
            using var h = new Harness(preset: _ => new(typeof(FlyingPursuitState), typeof(FlyingPursuitState),
                () => new FlyingPursuitState(pursuitHeight, 300, () => altitude)));
            h.Agent.HasTarget = true;
            h.Agent.TargetOffset = new(200, 75);
            h.Step(.01f);
            Assert.Equal(vertical, h.Agent.AiData.VerticalIntent);
            h.Agent.TargetOffset = Vector2.Zero;
            h.Step(1);
            Assert.Equal(1, h.Agent.RequestCount);
            h.Agent.IsAttacking = false;
            h.Step(.01f);
            Assert.True(h.Agent.AiData.DirectVerticalMovement);
            Assert.Equal(-1, h.Agent.AiData.VerticalIntent);
            altitude = 100;
            h.Step(.01f);
            Assert.Equal(0, h.Agent.AiData.VerticalIntent);
            h.Agent.HasTarget = false;
            h.Step(3);
            Assert.False(h.Agent.AiData.DirectVerticalMovement);
        }

        /// <summary>
        /// 飞行鲨鱼退避沿实际速度，虾兵沿注入的退避内界；两者均可纯规则验证。
        /// </summary>
        [Fact]
        public void SeaMovementUsesInjectedMotionAndRetreatRange()
        {
            using (var shark = new Harness(preset: _ => new(typeof(SharkSpawnEngageState), typeof(SharkSpawnEngageState),
                () => new SharkSpawnEngageState(300, () => 200, () => -480)), attackCount: 2))
            {
                shark.Agent.HasTarget = true;
                shark.Agent.TargetOffset = Vector2.Zero;
                shark.Step(1);
                shark.Agent.IsAttacking = false;
                shark.Step(.01f);
                Assert.Equal(-1, shark.Agent.AiData.MoveIntent);
                Assert.Equal(-1, shark.Agent.AiData.VerticalIntent);
                Assert.True(shark.Agent.AiData.DirectVerticalMovement);
            }
            using var shrimp = new Harness(preset: _ => new(typeof(ShrimpDemonEngageState), typeof(ShrimpDemonEngageState),
                () => new ShrimpDemonEngageState(150)));
            shrimp.Agent.HasTarget = true;
            shrimp.Agent.TargetOffset = new(100, 0);
            shrimp.Step(.1f);
            Assert.Equal(-1, shrimp.Agent.AiData.MoveIntent);
            shrimp.Agent.TargetOffset = new(300, 0);
            shrimp.Step(.1f);
            Assert.Equal(1, shrimp.Agent.AiData.MoveIntent);
        }

        /// <summary>
        /// 巡逻半秒起步，边界来自场景且允许左右不对称。
        /// </summary>
        [Theory]
        [InlineData(-361, 1)]
        [InlineData(401, -1)]
        /// <param name="offset">相对巡逻中心的水平偏移像素。</param>
        /// <param name="direction">期待的方向符号。</param>
        public void PatrolUsesSceneBounds(float offset, int direction)
        {
            using var h = new Harness();
            h.Agent.PatrolOffsetX = offset;
            h.Agent.PatrolBounds = new(-360, 400);
            h.Step(.49f);
            Assert.Equal(0, h.Agent.AiData.MoveIntent);
            h.Step(.02f);
            Assert.Equal(direction, h.Agent.AiData.MoveIntent);
            Assert.IsType<PatrolState>(h.Fsm.CurrentState);
        }

        /// <summary>
        /// 范围外追击，范围内按共同节拍出招；忙碌期间不排第二招。
        /// </summary>
        [Fact]
        public void EngageUsesSceneRangeAndCadence()
        {
            using var h = new Harness();
            h.Agent.HasTarget = true;
            h.Agent.TargetOffset = new(200, 0);
            h.Step(.01f);
            Assert.IsType<EngageState>(h.Fsm.CurrentState);
            h.Step(.48f);
            Assert.Equal(1, h.Agent.AiData.MoveIntent);
            h.Agent.TargetOffset = new(80, 0);
            h.Step(.49f);
            Assert.Equal(0, h.Agent.RequestCount);
            h.Step(.03f);
            Assert.Equal(1, h.Agent.RequestCount);
            h.Step(1);
            Assert.Equal(1, h.Agent.RequestCount);
            h.Agent.IsAttacking = false;
            h.Agent.AiData.AttackRequest = -1;
            h.Step(1);
            Assert.Equal(2, h.Agent.RequestCount);
        }

        /// <summary>
        /// 高度超限只改变决策，目标仍保留。
        /// </summary>
        [Fact]
        public void HighTargetPatrols()
        {
            using var h = new Harness();
            h.Agent.HasTarget = true;
            h.Agent.TargetOffset = new(200, 161);
            h.Agent.PatrolOffsetX = 500;
            h.Step(.01f);
            h.Step(.5f);
            Assert.Equal(-1, h.Agent.AiData.MoveIntent);
            Assert.True(h.Agent.HasTarget);
            Assert.Equal(0, h.Agent.RequestCount);
        }

        /// <summary>
        /// 受控清理意图但不换状态，不重置时钟，不增加恢复延迟。
        /// </summary>
        [Fact]
        public void ControlPreservesStateAndClock()
        {
            using var h = new Harness();
            h.Agent.HasTarget = true;
            h.Agent.TargetOffset = new(200, 0);
            h.Step(.01f);
            var state = h.Fsm.CurrentState;
            h.Agent.IsCcLocked = true;
            h.Agent.AiData.AttackRequest = 0;
            h.Step(.99f);
            Assert.Same(state, h.Fsm.CurrentState);
            Assert.Equal(-1, h.Agent.AiData.AttackRequest);
            Assert.Equal(0, h.Agent.AiData.MoveIntent);
            Assert.InRange(h.Agent.AiData.ElapsedSeconds, .999, 1.001);
            h.Agent.IsCcLocked = false;
            h.Step(.01f);
            Assert.Equal(1, h.Agent.AiData.MoveIntent);
        }

        /// <summary>
        /// 失效目标回巡逻，死亡防线停止请求和移动。
        /// </summary>
        [Fact]
        public void LostTargetAndDeathStopIntent()
        {
            using var h = new Harness();
            h.Agent.HasTarget = true;
            h.Step(.01f);
            h.Agent.HasTarget = false;
            h.Step(.01f);
            Assert.IsType<PatrolState>(h.Fsm.CurrentState);
            h.Agent.IsDead = true;
            h.Agent.Move(1);
            h.Agent.AiData.AttackRequest = 0;
            h.Step(.6f);
            Assert.Equal(0, h.Agent.AiData.MoveIntent);
            Assert.Equal(-1, h.Agent.AiData.AttackRequest);
            Assert.Equal(0, h.Agent.RequestCount);
        }

        /// <summary>
        /// 冷却归各实例，接受请求时保留官方同一步末的24Hz扣减。
        /// </summary>
        [Fact]
        public void CooldownsAreIndependent()
        {
            using var first = new Harness();
            using var second = new Harness();
            first.System.MarkUsed(first.Agent.AiData, first.Agent.Attacks, 0, () => .5f);
            Assert.InRange(first.Agent.AiData.Cooldowns[0], 3.9582f, 3.9584f);
            Assert.Equal(0, second.Agent.AiData.Cooldowns[0]);
            first.Step(4);
            Assert.Equal(0, first.Agent.AiData.Cooldowns[0]);
        }

        /// <summary>
        /// 重置借用当前难度配置，初始与施放冷却分别采样，且不影响其他实例。
        /// </summary>
        [Fact]
        public void ResetUsesSelectedAttackConfiguration()
        {
            using var first = new Harness();
            using var second = new Harness();
            var attack = (GameConfig.Battle.AttackConfig)RuntimeHelpers.GetUninitializedObject(typeof(GameConfig.Battle.AttackConfig));
            typeof(GameConfig.Battle.AttackConfig).GetField("AiInitCooldown")!.SetValue(attack, new Vector2(2, 6));
            typeof(GameConfig.Battle.AttackConfig).GetField("AiCooldown")!.SetValue(attack, new Vector2(8, 12));
            GameConfig.Battle.AttackConfig[] attacks = [attack];
            first.System.Reset(first.Agent.AiData, attacks, () => .25f);
            Assert.Equal(3, first.Agent.AiData.Cooldowns[0]);
            Assert.Equal(0, second.Agent.AiData.Cooldowns[0]);
            first.System.MarkUsed(first.Agent.AiData, attacks, 0, () => .75f);
            Assert.InRange(first.Agent.AiData.Cooldowns[0], 10.9582f, 10.9584f);
            first.System.Reset(first.Agent.AiData, second.Agent.Attacks, () => .5f);
            Assert.Equal(0, first.Agent.AiData.Cooldowns[0]);
            Assert.Equal(new Vector2(2, 6), attack.AiInitCooldown);
            Assert.Equal(new Vector2(8, 12), attack.AiCooldown);
        }

        /// <summary>
        /// 共用计数周期换算成游戏秒数，跨源周期不依赖60/120Hz。
        /// </summary>
        [Theory]
        [InlineData(60)]
        [InlineData(120)]
        /// <param name="hz">每秒模拟步数。</param>
        public void ClockWrapUsesGameSeconds(int hz)
        {
            using var h = new Harness();
            int count = 0;
            for (int i = 0; i < hz * 11; i++)
            {
                h.System.AdvanceCooldowns(h.Agent.AiData, 1f / hz);
                if (h.System.IsDecisionDue(h.Agent.AiData, 1))
                {
                    count++;
                }
            }
            Assert.Equal(11, count);
        }

        /// <summary>
        /// 概率独立于节拍，零欲望时不出招。
        /// </summary>
        [Fact]
        public void ZeroDesireDoesNotAttack()
        {
            using var h = new Harness(0);
            h.Agent.HasTarget = true;
            h.Step(.01f);
            h.Step(1);
            Assert.Equal(0, h.Agent.RequestCount);
        }

        /// <summary>
        /// 预设独立创建状态，只验证真实入口而无需空死亡/受控状态。
        /// </summary>
        [Fact]
        public void PresetOwnsFactoriesAndValidatesEntrypoints()
        {
            var first = EnemyAiPresets.Patrol.CreateStates();
            var second = EnemyAiPresets.Patrol.CreateStates();
            Assert.Equal(2, first.Length);
            Assert.NotSame(first[0], second[0]);
            var missing = new EnemyAiPreset(typeof(PatrolState), typeof(EngageState), () => new PatrolState());
            Assert.Throws<InvalidOperationException>(() => missing.CreateStates());
        }

        /// <summary>
        /// 本测试拥有的真实框架管理器。
        /// </summary>
        private sealed class Harness : IDisposable
        {
            private readonly FsmManager m_Manager = new(); // 每个测试独立驱动。

            /// <inheritdoc />
            public FakeAgent Agent { get; }

            /// <inheritdoc />
            public IFsm<IEnemyAiAgent> Fsm { get; }

            /// <inheritdoc />
            public EnemyAiSystem System => BattleArchitecture.Instance.GetSystem<EnemyAiSystem>();

            /// <inheritdoc />
            public Harness(int desire = 100, Func<FakeAgent, EnemyAiPreset> preset = null, int attackCount = 1)
            {
                Agent = new(desire, attackCount);
                if (preset != null)
                {
                    Agent.AiPreset = preset(Agent);
                }
                Fsm = m_Manager.CreateFsm<IEnemyAiAgent>(Guid.NewGuid().ToString(), Agent, Agent.AiPreset.CreateStates());
                Fsm.Start(Agent.AiPreset.IdleStateType);
            }

            /// <inheritdoc />
            public void Step(float seconds)
            {
                System.AdvanceCooldowns(Agent.AiData, seconds);
                m_Manager.Update(seconds, seconds);
            }

            /// <inheritdoc />
            public void Dispose() { m_Manager.Shutdown(); }
        }

        /// <summary>
        /// 确定性的感知事实，不构造Godot节点。
        /// </summary>
        private sealed class FakeAgent : IEnemyAiAgent
        {
            /// <inheritdoc />
            public bool IsDead { get; set; }

            /// <inheritdoc />
            public bool IsCcLocked { get; set; }

            /// <inheritdoc />
            public bool IsAttacking { get; set; }

            /// <inheritdoc />
            public bool HasTarget { get; set; }

            /// <inheritdoc />
            public Vector2 PatrolBounds { get; set; } = new(-400, 400);

            /// <inheritdoc />
            public Rect2 EngageBounds { get; set; } = new(-80, -160, 160, 320);

            /// <inheritdoc />
            public Vector2 TargetOffset { get; set; }

            /// <inheritdoc />
            public bool IsInEngageRange => Math.Abs(TargetOffset.X) <= 80 && Math.Abs(TargetOffset.Y) <= 160;

            /// <inheritdoc />
            public float PatrolOffsetX { get; set; }

            /// <inheritdoc />
            public EnemyConfig Config { get; }

            /// <inheritdoc />
            public EnemyAiData AiData { get; }

            /// <inheritdoc />
            public EnemyAiPreset AiPreset { get; set; } = EnemyAiPresets.Patrol;

            /// <inheritdoc />
            public System.Collections.Generic.IReadOnlyList<GameConfig.Battle.AttackConfig> Attacks { get; }

            /// <summary>
            /// 为测试创建既有配置类型，反射仅存在于测试夹具。
            /// </summary>
            /// <returns>四秒冷却的固定配置。</returns>
            private static GameConfig.Battle.AttackConfig CreateAttack()
            {
                var data = (GameConfig.Battle.AttackConfig)RuntimeHelpers.GetUninitializedObject(typeof(GameConfig.Battle.AttackConfig));
                typeof(GameConfig.Battle.AttackConfig).GetField("AiCooldown")!.SetValue(data, new Vector2(4, 4));
                return data;
            }

            /// <inheritdoc />
            public int RequestCount { get; private set; }

            /// <inheritdoc />
            public FakeAgent(int desire, int attackCount)
            {
                AiData = new(attackCount);
                Attacks = Enumerable.Range(0, attackCount).Select(_ => CreateAttack()).ToArray();
                Config = (EnemyConfig)RuntimeHelpers.GetUninitializedObject(typeof(EnemyConfig));
                typeof(EnemyConfig).GetField("AttackDesire")!.SetValue(Config, desire);
                typeof(EnemyConfig).GetField("AttackInterval")!.SetValue(Config, 1f);
            }

            /// <inheritdoc />
            IArchitecture IBelongToArchitecture.GetArchitecture() => BattleArchitecture.Instance;

            /// <inheritdoc />
            public float NextRandom() => .25f;

            /// <inheritdoc />
            public void Move(int dir) { AiData.MoveIntent = dir; }

            /// <inheritdoc />
            public void Face(int dir) { }

            /// <inheritdoc />
            public bool RequestAttack(int index)
            {
                if (IsAttacking || IsDead || IsCcLocked)
                {
                    return false;
                }

                AiData.AttackRequest = index;
                IsAttacking = true;
                RequestCount++;
                return true;
            }
        }
    }
}
