using System;

namespace GameLogic.Profile;

/// <summary>档案的货币：所有增减都经这里，不会出现负数余额。</summary>
public sealed class Wallet
{
    /// <summary>灵魂余额。</summary>
    public int Soul { get; private set; }

    /// <summary>余额实际变化后通知一次，界面重新读取完整余额。</summary>
    public event Action Changed;

    /// <summary>按存档余额创建钱包。</summary>
    /// <param name="soul">初始灵魂，负数视为 0。</param>
    public Wallet(int soul)
    {
        Soul = Math.Max(0, soul);
    }

    /// <summary>增加灵魂，溢出时封顶。</summary>
    /// <param name="amount">非负增加量。</param>
    /// <exception cref="ArgumentOutOfRangeException">增加量为负。</exception>
    public void AddSoul(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        SetSoul((int)Math.Min((long)Soul + amount, int.MaxValue));
    }

    /// <summary>扣除灵魂；余额不足时不扣除。</summary>
    /// <param name="amount">非负扣除量。</param>
    /// <returns>余额是否足够并已扣除。</returns>
    /// <exception cref="ArgumentOutOfRangeException">扣除量为负。</exception>
    public bool TrySpendSoul(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (amount > Soul)
        {
            return false;
        }

        SetSoul(Soul - amount);

        return true;
    }

    /// <summary>回滚到指定余额（关卡事务回滚时使用）。</summary>
    /// <param name="soul">要恢复的余额，负数视为 0。</param>
    public void Restore(int soul)
    {
        SetSoul(Math.Max(0, soul));
    }

    /// <summary>提交完整余额后发出聚合变更，无变化不重复通知。</summary>
    /// <param name="soul">已校验的非负余额。</param>
    private void SetSoul(int soul)
    {
        if (soul == Soul)
            return;
        Soul = soul;
        Changed?.Invoke();
    }
}
