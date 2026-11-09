using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Skills;

namespace GameLogic.Battle.Tests;

/// <summary>
/// 纯规则测试的独立英雄状态；与游戏使用同一系统，不共享可变数据。
/// </summary>
internal sealed class HeroTestScope : IArchitecture
{
    /// <summary>
    /// 本测试独占的英雄数值。
    /// </summary>
    public HeroState Hero => _heroes.GetState(1);
    private readonly HeroModel _heroes = new(); // 本测试的本局状态容器。
    private readonly SkillModel _skills; // 与本测试英雄集合对应的独立技能记录。

    /// <summary>
    /// 执行正式英雄业务规则。
    /// </summary>
    public HeroSystem System { get; }
    private readonly HeroStatusSystem _heroStatusSystem; // 与数值规则共享本测试英雄集合的状态效果系统。

    /// <summary>
    /// 建立最小存活英雄，规则测试不依赖场景。
    /// </summary>
    /// <param name="twoHeroes">是否同时建立两个英雄，供共享系统的隔离规则验证。</param>
    public HeroTestScope(bool twoHeroes = false)
    {
        // 按用例人数建立独立状态和技能书，复用同一套正式规则系统。
        _skills = new(twoHeroes ? [1, 2] : [1]);
        _heroes.CreateState(1);
        if (twoHeroes)
        {
            _heroes.CreateState(2);
        }
        _heroStatusSystem = new(this);
        ((ICanInit)_heroStatusSystem).Init();
        System = new(this);
        var buffer = new Luban.ByteBuf();
        for (int i = 0; i < 16; i++)
        {
            if (i is 5 or 6 or 9 or 10)
            {
                buffer.WriteFloat(0);
            }
            else
            {
                buffer.WriteInt(0);
            }
        }

        // 两人可以共享只读基础值，资源与运行计时仍写入各自的 HeroState。
        var attributes = new GameConfig.Attributes.ActorBaseAttributes(buffer);
        foreach (HeroState state in _heroes.Heroes.Values)
        {
            System.Reset(state.HeroId, 1, attributes);
        }
    }

    /// <inheritdoc />
    public TModel GetModel<TModel>() where TModel : class, IModel
    {
        return (typeof(TModel) == typeof(HeroModel) ? (IModel)_heroes :
            typeof(TModel) == typeof(SkillModel) ? _skills :
            typeof(TModel) == typeof(ConfigurationModel) ? BattleArchitecture.Instance.GetModel<ConfigurationModel>() : null) as TModel;
    }

    /// <inheritdoc />
    public TSystem GetSystem<TSystem>() where TSystem : class, ISystem => (typeof(TSystem) == typeof(HeroStatusSystem) ? (ISystem)_heroStatusSystem : System) as TSystem;

    /// <inheritdoc />
    public TUtility GetUtility<TUtility>() where TUtility : class, IUtility => null;
}
