using System;
using GameLogic.Heroes;
using GameLogic.Level;
using GameLogic.Profile;

namespace GameLogic.UI;

/// <summary>背包只借用本关英雄与档案；持久状态不复制到窗口。</summary>
public sealed class InventoryFormData
{
    /// <summary>当前战斗实体，提供最终属性与生命魔法。</summary>
    public HeroEntity Hero { get; }
    /// <summary>角色成长和钱包所属档案。</summary>
    public PlayerProfile Profile { get; }
    /// <summary>所属关卡，结束时关闭窗口。</summary>
    public LevelModule Level { get; }

    /// <summary>绑定本次查看对象。</summary>
    /// <param name="hero">当前出战英雄。</param>
    /// <param name="profile">当前存档槽的档案。</param>
    /// <param name="level">当前关卡。</param>
    /// <exception cref="ArgumentNullException">任一引用为空。</exception>
    public InventoryFormData(HeroEntity hero, PlayerProfile profile, LevelModule level)
    {
        Hero = hero ?? throw new ArgumentNullException(nameof(hero));
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Level = level ?? throw new ArgumentNullException(nameof(level));
    }
}
