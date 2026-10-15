using GameLogic.Utility;
using Godot;
using GameLogic.Architecture;
using GameLogic.Monsters.Body;
using GameFramework.Fsm;
using System;

namespace GameLogic.Monsters.AI
{
    /// <summary>
    /// 普通怪 AI 规则入口：处理攻击范围、加权选招与独立冷却；状态集合由怪物声明的预设提供。
    /// 普攻站位由判定盒决定；优先招可使用远程区间与逐招冷却。
    /// </summary>
    public sealed class MonsterAiSystem(IArchitecture architecture) : AbstractSystem(architecture)
    {
        /// <summary>
        /// 近身招"够得着"所需的最小水平重叠深度 px（工程常数）：判定盒只擦边时物理重叠不稳定，吃进几像素再出招。
        /// </summary>
        public const float ReachMargin = 4f;

        /// <summary>重置本怪物的意图、请求及招式初始冷却。</summary>
        /// <param name="data">本怪物拥有的 AI 数据。</param>
        /// <param name="parameters">同类怪物共享的攻击参数。</param>
        /// <param name="random">本怪物的随机源。</param>
        public void Reset(MonsterAiData data, MonsterBodyParams parameters, Func<float> random)
        {
            data.MoveIntent = 0;
            data.AttackRequest = -1;
            for (int i = 0; i < parameters.Attacks.Count; i++)
            {
                data.Cooldowns[i] = Roll(parameters.Attacks[i].InitCooldown, random);
            }
        }

        /// <summary>推进本怪物招式冷却并截断到零。</summary>
        /// <param name="data">本怪物拥有的 AI 数据。</param>
        /// <param name="elapseSeconds">本帧游戏秒数。</param>
        public void AdvanceCooldowns(MonsterAiData data, float elapseSeconds)
        {
            for (int i = 0; i < data.Cooldowns.Length; i++)
            {
                data.Cooldowns[i] = Math.Max(0f, data.Cooldowns[i] - elapseSeconds);
            }
        }

        /// <summary>标记招式已使用，并按其冷却区间重新计时。</summary>
        /// <param name="data">本怪物拥有的 AI 数据。</param>
        /// <param name="parameters">共享攻击参数。</param>
        /// <param name="index">已使用的攻击下标。</param>
        /// <param name="random">随机源。</param>
        public void MarkUsed(MonsterAiData data, MonsterBodyParams parameters, int index, Func<float> random)
        {
            int i = Find(parameters, index);
            if (i >= 0)
            {
                data.Cooldowns[i] = Roll(parameters.Attacks[i].Cooldown, random);
            }
        }

        /// <summary>
        /// 有效普攻中最近一招的水平间隙，不检查高度和冷却。
        /// </summary>
        /// <param name="parameters">同类怪物共享的攻击参数。</param>
        /// <param name="target">相对怪物原点的目标受击盒。</param>
        /// <param name="dir">面向目标的方向，左为 -1，右为 1。</param>
        /// <returns>水平间隙（像素）；不大于 -ReachMargin 时够得着，无攻击或目标时为正无穷。</returns>
        public float GetBasicAttackHorizontalGap(MonsterBodyParams parameters, Rect2 target, int dir)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < parameters.Attacks.Count && target.HasArea(); i++)
            {
                if (parameters.Attacks[i].IsBasic && parameters.Attacks[i].Weight > 0 && HasReachOrRange(parameters.Attacks[i]))
                {
                    best = Math.Min(best, GapX(parameters.Attacks[i], target, dir));
                }
            }

            return best;
        }

        /// <summary>检查是否有有效普攻够得着目标，包含高度，不检查冷却。</summary>
        /// <param name="parameters">同类怪物共享的攻击参数。</param>
        /// <param name="target">相对怪物原点的目标受击盒。</param>
        /// <param name="dir">面向目标的方向，左为 -1，右为 1。</param>
        /// <returns>存在满足范围的普攻时为 true。</returns>
        public bool IsBasicAttackInReach(MonsterBodyParams parameters, Rect2 target, int dir)
        {
            foreach (MonsterAttackParams spec in parameters.Attacks)
            {
                if (spec.IsBasic && spec.Weight > 0 && HasReachOrRange(spec) && InReach(spec, target, dir))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>返回当前目标可用的最高优先级，没招可用时返回 -1。</summary>
        /// <param name="data">本怪物独占的冷却数据。</param>
        /// <param name="parameters">同类怪物共享的攻击参数。</param>
        /// <param name="target">相对怪物原点的目标受击盒。</param>
        /// <param name="dir">面向目标的方向，左为 -1，右为 1。</param>
        /// <returns>冷却结束且满足范围的最高正优先级；没有候选时为 -1。</returns>
        public int GetHighestUsableAttackPriority(MonsterAiData data, MonsterBodyParams parameters, Rect2 target, int dir)
        {
            int best = 0;
            for (int i = 0; i < parameters.Attacks.Count; i++)
            {
                if (parameters.Attacks[i].Priority > best && IsUsable(data, parameters, i, target, dir))
                {
                    best = parameters.Attacks[i].Priority;
                }
            }

            return best == 0 ? -1 : best;
        }

        /// <summary>按给定优先级和随机值加权选择招式。</summary>
        /// <param name="data">本怪物独占的冷却数据。</param>
        /// <param name="parameters">同类怪物共享的攻击参数。</param>
        /// <param name="target">相对怪物原点的目标受击盒。</param>
        /// <param name="dir">面向目标的方向，左为 -1，右为 1。</param>
        /// <param name="priority">大于零的特殊招式优先级。</param>
        /// <param name="roll">零到一之间的随机落点。</param>
        /// <returns>可用招式下标；优先级无效或没有候选时为 -1。</returns>
        public int SelectAttackByPriority(MonsterAiData data, MonsterBodyParams parameters, Rect2 target, int dir, int priority, float roll)
        {
            return priority <= 0 ? -1 : PickWeighted(data, parameters, target, dir, roll, priority);
        }

        /// <summary>从可用普攻按权重抽取；无候选不取随机值。</summary>
        /// <param name="data">本怪物独占的冷却数据。</param>
        /// <param name="parameters">同类怪物共享的攻击参数。</param>
        /// <param name="target">相对怪物原点的目标受击盒。</param>
        /// <param name="dir">面向目标的方向，左为 -1，右为 1。</param>
        /// <param name="random">返回零到一之间数值的随机源。</param>
        /// <returns>可用普攻下标；没有候选时为 -1。</returns>
        public int SelectBasicAttack(MonsterAiData data, MonsterBodyParams parameters, Rect2 target, int dir, Func<float> random)
        {
            return PickWeighted(data, parameters, target, dir, random, 0);
        }

        /// <summary>从指定优先级的可用招式中按权重选择。</summary>
        /// <param name="data">本怪物独占的冷却数据。</param>
        /// <param name="parameters">同类怪物共享的攻击参数。</param>
        /// <param name="target">目标相对怪物的受击盒。</param>
        /// <param name="dir">面向目标的方向，左 -1、右 1。</param>
        /// <param name="roll">零到一的随机值。</param>
        /// <param name="priority">要选择的招式优先级。</param>
        /// <returns>攻击参数下标或选定的招式下标；没有候选时为 -1。</returns>
        private int PickWeighted(MonsterAiData data, MonsterBodyParams parameters, Rect2 target, int dir, float roll, int priority)
        {
            // 先计算候选总权重，避免没有候选时消费随机值。
            int total = 0;
            for (int i = 0; i < parameters.Attacks.Count; i++)
            {
                total += parameters.Attacks[i].Priority == priority && IsUsable(data, parameters, i, target, dir)
                    ? parameters.Attacks[i].Weight
                    : 0;
            }

            if (total == 0)
            {
                return -1;
            }

            // 按随机落点扣减权重，落入的最后一个候选即为选择结果。
            float pick = roll * total;
            int last = -1;
            for (int i = 0; i < parameters.Attacks.Count; i++)
            {
                if (parameters.Attacks[i].Priority != priority || !IsUsable(data, parameters, i, target, dir))
                {
                    continue;
                }

                last = parameters.Attacks[i].Index;
                pick -= parameters.Attacks[i].Weight;
                if (pick < 0f)
                {
                    break;
                }
            }

            return last; // roll 恰为 1 的浮点边界落到最后一个候选
        }

        /// <summary>确认有候选后取一次随机值并执行加权选择。</summary>
        /// <param name="data">本怪物独占的冷却数据。</param>
        /// <param name="parameters">同类怪物共享的攻击参数。</param>
        /// <param name="target">目标相对怪物的受击盒。</param>
        /// <param name="dir">面向目标的方向，左 -1、右 1。</param>
        /// <param name="random">本怪物的随机源。</param>
        /// <param name="priority">要选择的招式优先级。</param>
        /// <returns>攻击参数下标或选定的招式下标；没有候选时为 -1。</returns>
        private int PickWeighted(MonsterAiData data, MonsterBodyParams parameters, Rect2 target, int dir, Func<float> random, int priority)
        {
            // 先确认候选存在，保证空候选不调用随机源。
            for (int i = 0; i < parameters.Attacks.Count; i++)
            {
                if (parameters.Attacks[i].Priority == priority && IsUsable(data, parameters, i, target, dir))
                {
                    return PickWeighted(data, parameters, target, dir, random(), priority);
                }
            }

            return -1;
        }

        /// <summary>判断招式是否有权重、冷却结束且当前可命中。</summary>
        /// <param name="data">本怪物独占的冷却数据。</param>
        /// <param name="parameters">同类怪物共享的攻击参数。</param>
        /// <param name="i">攻击参数列表下标。</param>
        /// <param name="target">目标相对怪物的受击盒。</param>
        /// <param name="dir">面向目标的方向，左 -1、右 1。</param>
        /// <returns>是否满足攻击条件。</returns>
        private bool IsUsable(MonsterAiData data, MonsterBodyParams parameters, int i, Rect2 target, int dir)
        {
            return parameters.Attacks[i].Weight > 0 && data.Cooldowns[i] <= 0f && HasReachOrRange(parameters.Attacks[i]) &&
                   InReach(parameters.Attacks[i], target, dir);
        }

        /// <summary>判断招式是否配置了判定盒或远程范围。</summary>
        /// <param name="spec">一招的共享攻击参数。</param>
        /// <returns>是否满足攻击条件。</returns>
        private static bool HasReachOrRange(MonsterAttackParams spec)
        {
            return spec.Reach.HasArea() || spec.Range.Max > 0f;
        }

        /// <summary>判断目标是否同时满足水平重叠和近战高度条件。</summary>
        /// <param name="spec">一招的共享攻击参数。</param>
        /// <param name="target">目标相对怪物的受击盒。</param>
        /// <param name="dir">面向目标的方向，左 -1、右 1。</param>
        /// <returns>是否满足攻击条件。</returns>
        private static bool InReach(MonsterAttackParams spec, Rect2 target, int dir)
        {
            return target.HasArea() && GapX(spec, target, dir) <= -ReachMargin &&
                   (!spec.Reach.HasArea() || spec.Reach.Facing(dir).GapY(target) < 0f);
        }

        /// <summary>计算近战盒间隙或远程范围距离。</summary>
        /// <param name="spec">一招的共享攻击参数。</param>
        /// <param name="target">目标相对怪物的受击盒。</param>
        /// <param name="dir">面向目标的方向，左 -1、右 1。</param>
        /// <returns>攻击参数下标或选定的招式下标；没有候选时为 -1。</returns>
        private static float GapX(MonsterAttackParams spec, Rect2 target, int dir)
        {
            if (spec.Reach.HasArea())
            {
                return spec.Reach.Facing(dir).GapX(target);
            }

            float distance = Math.Abs(target.GetCenter().X);
            return distance < spec.Range.Min ? spec.Range.Min - distance :
                distance > spec.Range.Max ? distance - spec.Range.Max : float.NegativeInfinity;
        }

        /// <summary>按外部攻击下标查找规格数组位置。</summary>
        /// <param name="parameters">同类怪物共享的攻击参数。</param>
        /// <param name="index">实体攻击下标。</param>
        /// <returns>攻击参数下标或选定的招式下标；没有候选时为 -1。</returns>
        private int Find(MonsterBodyParams parameters, int index)
        {
            for (int i = 0; i < parameters.Attacks.Count; i++)
                if (parameters.Attacks[i].Index == index)
                    return i;
            return -1;
        }

        /// <summary>按区间配置采样冷却，定值区间不消费随机源。</summary>
        /// <param name="range">以秒表示的区间。</param>
        /// <param name="random">本怪物的随机源。</param>
        /// <returns>水平间隙像素，或按区间采样的冷却秒数。</returns>
        private static float Roll((float Min, float Max) range, Func<float> random)
        {
            return range.Sample(range.Max > range.Min ? random() : 0f);
        }

    }
}
