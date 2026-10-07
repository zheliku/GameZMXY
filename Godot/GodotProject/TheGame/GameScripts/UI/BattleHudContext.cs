using System;
using GameLogic.Entity.Heroes;
using GameLogic.Level;

namespace GameLogic;

/// <summary>HUD 打开参数，只携带本次绑定的英雄与关卡引用，不管理生命周期或数值。</summary>
public sealed class BattleHudContext
{
    /// <summary>本次绑定的英雄，数值由实体直接持有。</summary>
    public HeroEntity Hero { get; }

    /// <summary>本次绑定的关卡，提供前进提示状态。</summary>
    public LevelController Level { get; }

    /// <summary>创建一次 HUD 打开的参数。</summary>
    /// <param name="hero">已显示的英雄。</param>
    /// <param name="level">本次关卡。</param>
    /// <exception cref="ArgumentNullException">英雄或关卡为空。</exception>
    public BattleHudContext(HeroEntity hero, LevelController level)
    {
        Hero = hero ?? throw new ArgumentNullException(nameof(hero));
        Level = level ?? throw new ArgumentNullException(nameof(level));
    }
}
