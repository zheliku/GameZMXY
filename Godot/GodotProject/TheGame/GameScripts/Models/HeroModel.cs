using System;
using System.Collections.Generic;
using GameLogic.Architecture;

namespace GameLogic.Battle;

/// <summary>
/// 本局参战英雄状态的唯一容器；个人数值与时钟由各自 HeroState 持有。
/// </summary>
public sealed class HeroModel : AbstractModel
{
    private readonly Dictionary<int, HeroState> _heroes = new(); // 本局参战英雄的数值与状态。

    /// <summary>
    /// 本局全部参战英雄的只读视图。
    /// </summary>
    public IReadOnlyDictionary<int, HeroState> Heroes => _heroes;

    /// <summary>
    /// 读取指定参战英雄的本局状态。
    /// </summary>
    /// <param name="heroId">已经建立的参战英雄编号。</param>
    /// <returns>该英雄唯一的本局状态。</returns>
    /// <exception cref="KeyNotFoundException">该英雄尚未建立本局状态。</exception>
    public HeroState GetState(int heroId)
    {
        return _heroes[heroId];
    }

    /// <summary>
    /// 为参战英雄建立本局状态；同一英雄不得重复建立。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <returns>新建的本局状态。</returns>
    /// <exception cref="ArgumentException">该英雄已建立状态。</exception>
    internal HeroState CreateState(int heroId)
    {
        HeroState state = new(heroId);
        _heroes.Add(heroId, state);
        return state;
    }

    /// <summary>
    /// 清空本局全部英雄状态；系统先结束订阅与状态效果。
    /// </summary>
    internal void ClearStates()
    {
        _heroes.Clear();
    }
}
