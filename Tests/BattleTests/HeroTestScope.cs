using GameLogic.Architecture;
using GameLogic.Config;

namespace GameLogic.Battle.Tests;

/// <summary>纯规则测试的独立英雄状态；与游戏使用同一系统，不共享可变数据。</summary>
internal sealed class HeroTestScope : IArchitecture
{
    /// <summary>本测试独占的英雄数值。</summary>
    public HeroModel Hero { get; } = new();

    /// <summary>执行正式英雄业务规则。</summary>
    public HeroSystem System { get; }

    /// <summary>建立最小存活英雄，规则测试不依赖场景。</summary>
    public HeroTestScope()
    {
        System = new(this);
        var buffer = new Luban.ByteBuf();
        for (int i = 0; i < 16; i++)
            buffer.WriteInt(0);
        System.Reset(1, 1, new GameConfig.Attributes.ActorBaseAttributes(buffer));
    }
    /// <inheritdoc />
    public TModel GetModel<TModel>() where TModel : class, IModel =>
        (typeof(TModel) == typeof(HeroModel) ? (IModel)Hero :
            typeof(TModel) == typeof(ConfigurationModel) ? BattleArchitecture.Instance.GetModel<ConfigurationModel>() : null) as TModel;
    /// <inheritdoc />
    public TSystem GetSystem<TSystem>() where TSystem : class, ISystem => System as TSystem;
    /// <inheritdoc />
    public TUtility GetUtility<TUtility>() where TUtility : class, IUtility => null;
}
