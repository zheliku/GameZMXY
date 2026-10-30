using System;
using System.Runtime.CompilerServices;
using GameConfig.Enemy;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Enemies.AI;
using GameLogic.Enemies.Body;
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
            public Harness(int desire = 100)
            {
                Agent = new(desire);
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
            public EnemyAiData AiData { get; } = new(1);

            /// <inheritdoc />
            public EnemyAiPreset AiPreset => EnemyAiPresets.Patrol;

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
            public FakeAgent(int desire)
            {
                Attacks = [CreateAttack()];
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
