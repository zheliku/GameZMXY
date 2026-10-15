using System;
using GameLogic.Heroes;

namespace GameLogic.UI;

/// <summary>本次个人资料要观察的英雄；共享玩家、关卡与配置由窗口直接读取。</summary>
public sealed class InventoryFormData
{
    /// <summary>借用本关出战英雄，窗口不拥有实体。</summary>
    public AbstractHeroEntity Hero { get; }

    /// <summary>指定本次显示的英雄。</summary>
    /// <param name="hero">已经显示的出战英雄。</param>
    /// <exception cref="ArgumentNullException">英雄为空。</exception>
    public InventoryFormData(AbstractHeroEntity hero) => Hero = hero ?? throw new ArgumentNullException(nameof(hero));
}
