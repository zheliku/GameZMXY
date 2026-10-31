using Godot;
using GameLogic.Architecture;
using GameConfig.Battle;
using System.Collections.Generic;
using System;

namespace GameLogic.Enemies.AI
{
    /// <summary>
    /// 敌人 AI 共用时钟、官方游荡与独立冷却；各物种状态拥有原点距离和选招规则。
    /// </summary>
    /// <param name="architecture">借用的所属作用域；模型和服务由作用域持有。</param>
    public sealed class EnemyAiSystem(IArchitecture architecture) : AbstractSystem(architecture)
    {
        /// <summary>
        /// 重置本怪物的意图、请求及招式初始冷却。
        /// </summary>
        /// <param name="data">本怪物拥有的 AI 数据。</param>
        /// <param name="attacks">借用实体当前的招式配置，不复制冷却区间。</param>
        /// <param name="random">本怪物的随机源。</param>
        public void Reset(EnemyAiData data, IReadOnlyList<AttackConfig> attacks, Func<float> random)
        {
            data.ElapsedSeconds = 0;
            data.PreviousElapsedSeconds = 0;
            data.ClockOrigin = 0;
            data.PatrolStopTime = 3;
            data.MoveIntent = 0;
            data.AttackRequest = -1;
            for (int i = 0; i < attacks.Count; i++)
            {
                data.Cooldowns[i] = Roll(attacks[i].AiInitCooldown, random);
            }
        }

        /// <summary>
        /// 推进本怪物招式冷却并截断到零。
        /// </summary>
        /// <param name="data">本怪物拥有的 AI 数据。</param>
        /// <param name="elapseSeconds">本帧游戏秒数。</param>
        public void AdvanceCooldowns(EnemyAiData data, float elapseSeconds)
        {
            data.PreviousElapsedSeconds = data.ElapsedSeconds;
            data.ElapsedSeconds += elapseSeconds;
            for (int i = 0; i < data.Cooldowns.Length; i++)
            {
                float remaining = data.Cooldowns[i] - elapseSeconds;
                data.Cooldowns[i] = remaining <= .00001f ? 0f : remaining;
            }
        }

        /// <summary>
        /// 检测官方共用计数周期中的决策节拍；242个源帧只在此换算成游戏秒数。
        /// </summary>
        /// <param name="data">本实例持续推进的时钟。</param>
        /// <param name="period">该物种的决策间隔，游戏秒。</param>
        /// <returns>本次更新是否跨过决策时点。</returns>
        public bool IsDecisionDue(EnemyAiData data, double period)
        {
            const double clockPeriod = 242d / 24d;
            double age = (data.ElapsedSeconds - data.ClockOrigin) % clockPeriod;
            double previous = Math.Max(0, data.PreviousElapsedSeconds - data.ClockOrigin) % clockPeriod;
            return age + 1e-6 < previous ||
                (int)((age + 1e-6) / period) != (int)((previous + 1e-6) / period);
        }

        /// <summary>
        /// BaseMonster.normalWalk 的共同游荡：半秒起步、按场景巡逻边界选择折返、三或四秒停步并重启周期。
        /// </summary>
        /// <param name="agent">本实例的时钟、随机源和移动意图。</param>
        /// <param name="elapseSeconds">本次游戏秒数，概率按源24Hz换算，不随渲染帧率变化。</param>
        public void Patrol(IEnemyAiAgent agent, float elapseSeconds)
        {
            const double clockPeriod = 242d / 24d;
            EnemyAiData data = agent.AiData;
            double age = (data.ElapsedSeconds - data.ClockOrigin) % clockPeriod;
            double previous = Math.Max(0, data.PreviousElapsedSeconds - data.ClockOrigin) % clockPeriod;
            if (previous < .5 && age + 1e-6 >= .5)
            {
                agent.Move(agent.PatrolOffsetX < agent.PatrolBounds.X ? 1 : agent.PatrolOffsetX > agent.PatrolBounds.Y ? -1 :
                    agent.NextRandom() < .5f ? -1 : 1);
                data.PatrolStopTime = agent.NextRandom() < .5f ? 3 : 4;
            }
            if (age < data.PatrolStopTime)
            {
                return;
            }
            if (agent.NextRandom() < 1 - Math.Pow(.5, elapseSeconds * 24))
            {
                agent.Move(0);
            }

            double resetChance = Math.Clamp((age - data.PatrolStopTime) / 1.25, 0, 1);
            if (agent.NextRandom() < 1 - Math.Pow(1 - resetChance, elapseSeconds * 24))
            {
                data.ClockOrigin = data.ElapsedSeconds;
            }
        }

        /// <summary>
        /// 标记招式已使用，并按其冷却区间重新计时。
        /// </summary>
        /// <param name="data">本怪物拥有的 AI 数据。</param>
        /// <param name="attacks">借用实体当前的招式配置，包含具体难度选档。</param>
        /// <param name="index">已使用的攻击下标。</param>
        /// <param name="random">随机源。</param>
        public void MarkUsed(EnemyAiData data, IReadOnlyList<AttackConfig> attacks, int index, Func<float> random)
        {
            int i = index;
            if ((uint)i < attacks.Count)
            {
                // 官方先在决策中赋值skillCD，再在同一源步末执行countCD；保留这一次24Hz扣减。
                // 否则渲染帧的相位差会让刚好四秒的冷却错过下一决策点，多等待整整一秒。
                data.Cooldowns[i] = Math.Max(0f, Roll(attacks[i].AiCooldown, random) - 1f / 24f);
            }
        }

        /// <summary>
        /// 按区间配置采样冷却，定值区间不消费随机源。
        /// </summary>
        /// <param name="range">以秒表示的区间。</param>
        /// <param name="random">本怪物的随机源。</param>
        /// <returns>按区间采样的冷却秒数。</returns>
        private static float Roll(Vector2 range, Func<float> random)
        {
            return Mathf.Lerp(range.X, range.Y, range.Y > range.X ? random() : 0f);
        }
    }
}
