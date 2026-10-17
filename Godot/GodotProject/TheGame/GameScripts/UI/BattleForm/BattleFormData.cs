using System;
using GameLogic.Heroes;
using GameLogic.Level;

namespace GameLogic.UI;

/// <summary>本次 HUD 的表现对象；玩家成长和关卡状态由共享 Model 提供。</summary>
public sealed class BattleFormData
{
    /// <summary>本次观察的英雄战斗运行时。</summary>
    public AbstractHeroEntity Hero { get; }

    /// <summary>本场景拥有的守关对手展示来源。</summary>
    public LevelViewController ViewController { get; }

    /// <summary>借用本次显示必需的实体与场景，不管理其生命周期。</summary>
    /// <param name="hero">本关已经显示的英雄。</param>
    /// <param name="viewController">本场景表现控制器。</param>
    /// <exception cref="ArgumentNullException">必需表现对象为空。</exception>
    public BattleFormData(AbstractHeroEntity hero, LevelViewController viewController)
    {
        Hero = hero ?? throw new ArgumentNullException(nameof(hero));
        ViewController = viewController ?? throw new ArgumentNullException(nameof(viewController));
    }
}
