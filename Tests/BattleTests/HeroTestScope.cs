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
    private readonly SkillModel _skills = new([1]); // 与正式英雄装配相同的独立技能记录。

    /// <summary>
    /// 执行正式英雄业务规则。
    /// </summary>
    public HeroSystem System { get; }
    private readonly HeroStatusSystem _heroStatusSystem;

    /// <summary>
    /// 建立最小存活英雄，规则测试不依赖场景。
    /// </summary>
    public HeroTestScope()
    {
        _heroes.CreateState(1);
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

        System.Reset(1, 1, new GameConfig.Attributes.ActorBaseAttributes(buffer));
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
