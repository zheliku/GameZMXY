using System;

namespace GameLogic.Battle.Resources;

/// <summary>官方普通模式的命中蓄力规则；不引用实体、配置或随机源。</summary>
public static class MusouHelper
{
    /// <summary>按实际伤害相对出招时基础攻击力的比例计算无双，采用正数四舍五入。</summary>
    /// <param name="damage">结算后的伤害；零伤害不积累。</param>
    /// <param name="basePower">出招时攻击力，不含暴击和招式威力倍率。</param>
    /// <param name="coefficient">普通模式配置系数，官方为 2。</param>
    /// <returns>本次命中增长量；无有效伤害或基础攻击力时为零。</returns>
    public static int HitGain(int damage, int basePower, float coefficient)
    {
        if (damage <= 0 || basePower <= 0 || !float.IsFinite(coefficient) || coefficient <= 0f)
            return 0;
        return (int)Math.Min(int.MaxValue, Math.Floor((double)damage / basePower * coefficient + .5));
    }
}
