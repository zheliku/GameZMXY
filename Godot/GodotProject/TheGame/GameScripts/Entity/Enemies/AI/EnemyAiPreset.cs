using System;
using System.Collections.Generic;
using System.Linq;
using GameFramework.Fsm;

namespace GameLogic.Enemies.AI;

/// <summary>
/// 可复用的只读 AI 状态配方；工厂为每台状态机创建独立状态，不共享计时器。
/// </summary>
public sealed class EnemyAiPreset
{
    private readonly Func<AbstractEnemyAiState>[] _stateFactories; // 私有复制的状态工厂集合。

    /// <summary>
    /// 声明状态集合与入口；可在具体怪物中组合专属状态，而不修改通用系统。
    /// </summary>
    /// <param name="idleStateType">集合中的无目标入口。</param>
    /// <param name="engageStateType">集合中的交战入口。</param>
    /// <param name="stateFactories">每次调用都创建新状态实例；身体死亡和受控不另建AI状态。</param>
    /// <exception cref="ArgumentNullException">入口或集合为空。</exception>
    public EnemyAiPreset(Type idleStateType, Type engageStateType,
        params Func<AbstractEnemyAiState>[] stateFactories)
    {
        ArgumentNullException.ThrowIfNull(idleStateType);
        ArgumentNullException.ThrowIfNull(engageStateType);
        ArgumentNullException.ThrowIfNull(stateFactories);
        IdleStateType = idleStateType;
        EngageStateType = engageStateType;
        _stateFactories = stateFactories.ToArray();
    }

    /// <summary>
    /// 无目标时进入的状态，也是默认初始状态。
    /// </summary>
    public Type IdleStateType { get; }

    /// <summary>
    /// 发现目标时进入的交战状态；受控暂停后继续原状态。
    /// </summary>
    public Type EngageStateType { get; }

    /// <summary>
    /// 为一个怪物创建完整状态集合，创建前不保存任何运行时状态。
    /// </summary>
    /// <returns>由该怪物状态机独占的状态实例。</returns>
    /// <exception cref="InvalidOperationException">状态为空、类型重复或缺少巡逻/交战入口。</exception>
    public FsmState<IEnemyAiAgent>[] CreateStates()
    {
        HashSet<Type> types = new();
        var states = new FsmState<IEnemyAiAgent>[_stateFactories.Length];
        for (int i = 0; i < states.Length; i++)
        {
            AbstractEnemyAiState state = _stateFactories[i]?.Invoke();
            if (state == null || !types.Add(state.GetType()))
            {
                throw new InvalidOperationException("AI 预设包含空状态或重复状态类型。");
            }

            states[i] = state;
        }
        // 只验证真正会进入的状态，不为死亡/受控标记建立空状态。
        if (!types.Contains(IdleStateType) || !types.Contains(EngageStateType))
        {
            throw new InvalidOperationException("AI 预设缺少巡逻或交战入口。");
        }

        return states;
    }
}
