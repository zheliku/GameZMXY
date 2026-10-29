using Godot;
using System.Collections.Generic;

namespace GameLogic.Enemies.Body
{
    /// <summary>
    /// 敌人状态使用的只读身体参数；基础参数归 ConfigurationModel，专属参数由具体实体持有。
    /// </summary>
    public sealed record EnemyBodyParams
    {
        /// <summary>
        /// 移动速度 px/s（EnemyConfig.MoveSpeed）
        /// </summary>
        public float MoveSpeed { get; init; }

        /// <summary>
        /// 重力 px/s²（EnemyConfig.Gravity）
        /// </summary>
        public float Gravity { get; init; }

        /// <summary>
        /// 受击硬直秒，取hurt的complete标记；没有标记时使用片段长度。
        /// </summary>
        public float HurtTime { get; init; }

        /// <summary>
        /// 受击横速每秒保留比例；只在超过本怪物移动速度时减速。
        /// </summary>
        public float KnockbackRetentionPerSecond { get; init; } = 1f;

        /// <summary>
        /// 死亡片段的完成检查时点，取complete标记或片段长度。
        /// </summary>
        public float DeathTime { get; init; }

        /// <summary>
        /// 空中尸体循环起点秒数；没有该源分支时为空，按完成时点回收。
        /// </summary>
        public float? DeathAirLoopTime { get; init; }

        /// <summary>
        /// 实体声明的攻击动画名，下标与攻击配置数组的招式序号一致。
        /// </summary>
        public IReadOnlyList<string> AttackAnims { get; init; }

        /// <summary>
        /// 每招实际收招秒数，OnInit读取complete标记或片段长度。
        /// </summary>
        public IReadOnlyList<float> AttackTimes { get; init; }

        /// <summary>
        /// 每招的收招硬直秒（AttackConfig.AiRecovery；下标同上）——表数值，与动画无关
        /// </summary>
        public IReadOnlyList<float> AttackRecovery { get; init; }

        /// <summary>
        /// AI 共用的每招范围、权重与冷却配置；逐个怪物的剩余冷却不放在这里。
        /// </summary>
        public IReadOnlyList<EnemyAttackParams> Attacks { get; init; } = System.Array.Empty<EnemyAttackParams>();

        /// <summary>
        /// 招式数
        /// </summary>
        public int AttackCount
        {
            get
            {
                return AttackAnims?.Count ?? 0;
            }
        }
    }

    /// <summary>
    /// 一招的 AI 用法：优先级、权重、释放距离与攻击动画判定盒。
    /// </summary>
    public readonly struct EnemyAttackParams
    {
        /// <summary>
        /// 招式下标（= AttackSegment）
        /// </summary>
        public int Index { get; init; }

        /// <summary>
        /// 抽选权重（≤0 = AI 不用这招）
        /// </summary>
        public int Weight { get; init; }

        /// <summary>
        /// 0 = 普攻；&gt;0 = 可在追击/守候时释放的优先招（大者优先）
        /// </summary>
        public int Priority { get; init; }

        /// <summary>
        /// 近战攻击动画判定盒的并集（素材原生朝左）。
        /// </summary>
        public Rect2 Reach { get; init; }

        /// <summary>
        /// 无近战判定盒招式的水平释放距离区间（远程/冲锋）
        /// </summary>
        public (float Min, float Max) Range { get; init; }

        /// <summary>
        /// 释放后冷却区间
        /// </summary>
        public (float Min, float Max) Cooldown { get; init; }

        /// <summary>
        /// 出生时初始冷却区间
        /// </summary>
        public (float Min, float Max) InitCooldown { get; init; }

        /// <summary>
        /// 该招式是否为普通攻击（优先级为 0）。
        /// </summary>
        public bool IsBasic
        {
            get
            {
                return Priority == 0;
            }
        }
    }
}
