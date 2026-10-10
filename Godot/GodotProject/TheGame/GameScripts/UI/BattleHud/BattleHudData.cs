using System;
using GameLogic.Entity.Heroes;
using GameLogic.Level;
using GameLogic.Profile;

namespace GameLogic.UI;

/// <summary>
/// HUD 打开参数：本次显示所需的状态源引用（英雄的战斗运行时、档案中的成长、关卡）。
/// 只携带引用，不管理生命周期或数值；HUD 订阅这些状态源的变更事件并在变化时拉取一致快照。
/// </summary>
public sealed class BattleHudData
{
    /// <summary>本次显示的英雄（生命、魔法、无双）。</summary>
    public HeroEntity Hero { get; }

    /// <summary>该英雄在档案中的成长（等级、经验）。</summary>
    public HeroProgression Progression { get; }

    /// <summary>本次关卡（可前进窗口）。</summary>
    public LevelController Level { get; }

    /// <summary>创建一次 HUD 打开的参数。</summary>
    /// <param name="hero">已显示的英雄。</param>
    /// <param name="progression">该英雄的档案成长。</param>
    /// <param name="level">本次关卡。</param>
    /// <exception cref="ArgumentNullException">任一引用为空。</exception>
    public BattleHudData(HeroEntity hero, HeroProgression progression, LevelController level)
    {
        Hero = hero ?? throw new ArgumentNullException(nameof(hero));
        Progression = progression ?? throw new ArgumentNullException(nameof(progression));
        Level = level ?? throw new ArgumentNullException(nameof(level));
    }
}
