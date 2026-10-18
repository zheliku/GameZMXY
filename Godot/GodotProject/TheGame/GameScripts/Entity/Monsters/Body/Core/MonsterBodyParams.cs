using Godot;
using System.Collections.Generic;

namespace GameLogic.Monsters.Body
{
    /// <summary>同类实体共享的只读身体参数；配置和动画派生数据由 ConfigurationModel 集中持有。</summary>
    public sealed record MonsterBodyParams
    {
        /// <summary>移动速度 px/s（MonsterConfig.MoveSpeed）</summary>
        public float MoveSpeed { get; init; }

        /// <summary>重力 px/s²（MonsterConfig.Gravity）</summary>
        public float Gravity { get; init; }

        /// <summary>受击硬直秒（= hurt 动画长度）</summary>
        public float HurtTime { get; init; }

        /// <summary>受击横速每秒保留比例；只在超过本怪物移动速度时减速。</summary>
        public float KnockbackRetentionPerSecond { get; init; } = 1f;

        /// <summary>死亡到回收的秒数（= death 动画长度）</summary>
        public float DeathTime { get; init; }

        /// <summary>每招的攻击动画名（下标 = 招式序号，AttackConfig.Animation 按 ComboIndex 排序）</summary>
        public IReadOnlyList<string> AttackAnims { get; init; }

        /// <summary>每招的时长秒（= 招式动画长度，OnInit 读动画资源；状态计时）</summary>
        public IReadOnlyList<float> AttackTimes { get; init; }

        /// <summary>每招的收招硬直秒（AttackConfig.AiRecovery；下标同上）——表数值，与动画无关</summary>
        public IReadOnlyList<float> AttackRecovery { get; init; }

        /// <summary>AI 共用的每招范围、权重与冷却配置；逐个怪物的剩余冷却不放在这里。</summary>
        public IReadOnlyList<MonsterAttackParams> Attacks { get; init; } = System.Array.Empty<MonsterAttackParams>();

        /// <summary>招式数</summary>
        public int AttackCount => AttackAnims?.Count ?? 0;
    }

    /// <summary>
    /// 一招的 AI 用法：优先级、权重、释放距离与攻击动画判定盒。
    /// </summary>
    public readonly struct MonsterAttackParams
    {
        /// <summary>招式下标（= AttackSegment）</summary>
        public int Index { get; init; }

        /// <summary>抽选权重（≤0 = AI 不用这招）</summary>
        public int Weight { get; init; }

        /// <summary>0 = 普攻；&gt;0 = 可在追击/守候时释放的优先招（大者优先）</summary>
        public int Priority { get; init; }

        /// <summary>近战攻击动画判定盒的并集（素材原生朝左）。</summary>
        public Rect2 Reach { get; init; }

        /// <summary>无近战判定盒招式的水平释放距离区间（远程/冲锋）</summary>
        public (float Min, float Max) Range { get; init; }

        /// <summary>释放后冷却区间</summary>
        public (float Min, float Max) Cooldown { get; init; }

        /// <summary>出生时初始冷却区间</summary>
        public (float Min, float Max) InitCooldown { get; init; }

        /// <summary>该招式是否为普通攻击（优先级为 0）。</summary>
        public bool IsBasic => Priority == 0;
    }
}
