using GameConfig.Battle;

namespace GameLogic.Battle.Damage;

/// <summary>
/// 一次命中的结算结果（值类型，调用方按值持有，无归还义务）。
/// </summary>
public readonly struct DamageResult
{
    /// <summary>
    /// 最终伤害（闪避时为 0；未命中闪避时也可能因减伤截断为 0）
    /// </summary>
    public readonly int Damage;

    /// <summary>
    /// 本次命中结算出的攻击方无双收益；是否接受增长由英雄系统的当前状态决定。
    /// </summary>
    public readonly int AttackerWsGain;

    /// <summary>
    /// 是否被闪避
    /// </summary>
    public readonly bool IsMiss;

    /// <summary>
    /// 是否暴击
    /// </summary>
    public readonly bool IsCrit;

    /// <summary>
    /// 伤害类型（飘字按它选数字样式）
    /// </summary>
    public readonly DamageKind Kind;

    /// <summary>
    /// 创建一次命中的结算结果。
    /// </summary>
    /// <param name="damage">最终伤害值。</param>
    /// <param name="isMiss">是否被闪避。</param>
    /// <param name="isCrit">是否暴击。</param>
    /// <param name="kind">伤害类型。</param>
    /// <param name="attackerWsGain">战斗系统结算的无双收益；持续扣血等非命中事实为零。</param>
    public DamageResult(int damage, bool isMiss, bool isCrit, DamageKind kind, int attackerWsGain = 0)
    {
        Damage = damage;
        AttackerWsGain = attackerWsGain;
        IsMiss = isMiss;
        IsCrit = isCrit;
        Kind = kind;
    }

    /// <summary>
    /// 创建指定伤害类型的闪避结果。
    /// </summary>
    /// <param name="kind">本次攻击的伤害类型。</param>
    /// <returns>伤害为 0、标记闪避且不暴击的结果。</returns>
    public static DamageResult Missed(DamageKind kind)
    {
        return new(0, true, false, kind);
    }
}
