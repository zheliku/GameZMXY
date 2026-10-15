using System;
using System.IO;
using GameConfig;
using GameLogic.Architecture;
using GameLogic.Config;
using Luban;

namespace GameLogic.Battle.Tests;

/// <summary>纯规则测试的显式配置入口，不创建全局游戏或 Godot 节点。</summary>
internal sealed class BattleArchitecture : IArchitecture
{
    /// <summary>本测试程序集共享的只读配置来源。</summary>
    public static BattleArchitecture Instance { get; } = new();
    private readonly ConfigurationModel m_Configuration = new(new Tables(name =>
        new ByteBuf(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", name + ".bytes"))))); // 读取真实表产物。
    private readonly GameLogic.Input.InputSystem m_Input; // 所有测试宿主借用同一无状态规则入口。
    private readonly GameLogic.Monsters.AI.MonsterAiSystem m_Ai; // 共享规则；每个测试怪物的数据独立。

    /// <summary>装配规则入口，保持与游戏相同的共享系统身份。</summary>
    private BattleArchitecture()
    {
        m_Input = new(this);
        m_Ai = new(this);
    }

    /// <inheritdoc />
    public TModel GetModel<TModel>() where TModel : class, IModel =>
        typeof(TModel) == typeof(ConfigurationModel) ? (TModel)(object)m_Configuration
        : null;

    /// <inheritdoc />
    public TSystem GetSystem<TSystem>() where TSystem : class, ISystem =>
        typeof(TSystem) == typeof(GameLogic.Input.InputSystem) ? (TSystem)(object)m_Input
        : typeof(TSystem) == typeof(GameLogic.Monsters.AI.MonsterAiSystem) ? (TSystem)(object)m_Ai
        : null;
    /// <inheritdoc />
    public TUtility GetUtility<TUtility>() where TUtility : class, IUtility => null;
}
