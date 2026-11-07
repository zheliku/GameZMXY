using System;
using System.IO;
using GameConfig;
using GameLogic.Architecture;
using GameLogic.Config;
using Luban;

namespace GameLogic.Battle.Tests;

/// <summary>
/// 纯规则测试的显式配置入口，不创建全局游戏或 Godot 节点。
/// </summary>
internal sealed class BattleArchitecture : IArchitecture
{
    private readonly ConfigurationModel _configuration = new(new Tables(name =>
                        new ByteBuf(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", name + ".bytes"))))); // 读取真实表产物。
    private readonly GameLogic.Input.InputSystem _input; // 所有测试宿主借用同一无状态规则入口。
    private readonly GameLogic.Enemies.AI.EnemyAiSystem _ai; // 共享规则；每个测试怪物的数据独立。

    /// <summary>
    /// 本测试程序集共享的只读配置来源。
    /// </summary>
    public static BattleArchitecture Instance { get; } = new();

    /// <summary>
    /// 装配规则入口，保持与游戏相同的共享系统身份。
    /// </summary>
    private BattleArchitecture()
    {
        _input = new(this);
        _ai = new(this);
    }

    /// <inheritdoc />
    public TModel GetModel<TModel>() where TModel : class, IModel => typeof(TModel) == typeof(ConfigurationModel) ? (TModel)(object)_configuration : null;

    /// <inheritdoc />
    public TSystem GetSystem<TSystem>() where TSystem : class, ISystem
    {
        return typeof(TSystem) == typeof(GameLogic.Input.InputSystem) ? (TSystem)(object)_input :
            typeof(TSystem) == typeof(GameLogic.Enemies.AI.EnemyAiSystem) ? (TSystem)(object)_ai : null;
    }

    /// <inheritdoc />
    public TUtility GetUtility<TUtility>() where TUtility : class, IUtility => null;
}
