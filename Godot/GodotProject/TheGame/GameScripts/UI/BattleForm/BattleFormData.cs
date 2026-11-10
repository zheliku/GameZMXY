using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.Heroes;
using GameLogic.Level;

namespace GameLogic.UI;

/// <summary>
/// 本次 HUD 的表现对象；玩家成长和关卡状态由共享 Model 提供。
/// </summary>
public sealed class BattleFormData
{
    /// <summary>
    /// 借用本次显示必需的实体与场景，不管理其生命周期。
    /// </summary>
    /// <param name="heroes">本关按席位排列、已经显示的1～2名英雄。</param>
    /// <param name="viewController">本场景表现控制器。</param>
    /// <exception cref="ArgumentNullException">必需表现对象为空。</exception>
    /// <exception cref="ArgumentException">英雄数量不在1～2人或包含空实体。</exception>
    public BattleFormData(IReadOnlyList<AbstractHeroEntity> heroes, LevelViewController viewController)
    {
        ArgumentNullException.ThrowIfNull(heroes);
        if (heroes.Count is < 1 or > 2 || heroes.Any(hero => hero == null))
        {
            throw new ArgumentException("HUD 必须接收1～2名非空英雄。", nameof(heroes));
        }
        Heroes = heroes;
        ViewController = viewController ?? throw new ArgumentNullException(nameof(viewController));
    }

    /// <summary>
    /// 本局参战英雄，按席位排列；借用流程拥有的集合。
    /// </summary>
    public IReadOnlyList<AbstractHeroEntity> Heroes { get; }

    /// <summary>
    /// 本场景拥有的守关对手展示来源。
    /// </summary>
    public LevelViewController ViewController { get; }
}
