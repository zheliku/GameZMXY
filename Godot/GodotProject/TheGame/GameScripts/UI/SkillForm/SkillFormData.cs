using System;
using GameConfig.Skills;
using GameLogic.Level;
using GameLogic.Profile;
using GameLogic.Skills;

namespace GameLogic.UI;

/// <summary>技能面板借用配置目录、钱包与关卡；本轮没有持久学习状态。</summary>
public sealed class SkillFormData
{
    /// <summary>正式配置的只读目录。</summary>
    public SkillCatalog Catalog { get; }
    /// <summary>本关钱包，仅观察灵魂余额。</summary>
    public Wallet Wallet { get; }
    /// <summary>所属关卡，结束时关闭窗口。</summary>
    public LevelModule Level { get; }
    /// <summary>当前出战角色，也是本窗口唯一允许查看的角色目录。</summary>
    public SkillHero Hero { get; }

    /// <summary>借用当前关卡作用域，不复制档案状态。</summary>
    /// <param name="catalog">正式技能目录。</param>
    /// <param name="wallet">当前档案钱包。</param>
    /// <param name="level">正在运行的关卡。</param>
    /// <param name="hero">当前出战角色。</param>
    /// <exception cref="ArgumentNullException">任一状态源为空。</exception>
    public SkillFormData(SkillCatalog catalog, Wallet wallet, LevelModule level, SkillHero hero)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        Level = level ?? throw new ArgumentNullException(nameof(level));
        Hero = hero;
    }
}
