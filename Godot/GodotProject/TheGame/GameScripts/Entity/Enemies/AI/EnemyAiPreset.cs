using System;
using System.Collections.Generic;
using System.Linq;
using GameFramework.Fsm;

namespace GameLogic.Enemies.AI;

/// <summary>可复用的只读 AI 状态配方；工厂为每台状态机创建独立状态，不共享计时器。</summary>
public sealed class EnemyAiPreset
{
    private readonly Func<AbstractEnemyAiState>[] m_StateFactories; // 私有复制的状态工厂集合。

    /// <summary>无目标时进入的状态，也是默认初始状态。</summary>
    public Type IdleStateType { get; }

    /// <summary>发现目标或解除控制后进入的交战状态。</summary>
    public Type EngageStateType { get; }

    /// <summary>声明状态集合与入口；可在具体怪物中组合专属状态，而不修改通用系统。</summary>
    /// <param name="idleStateType">集合中的无目标入口。</param>
    /// <param name="engageStateType">集合中的交战入口。</param>
    /// <param name="stateFactories">每次调用都创建新状态实例的工厂；包括死亡和受控状态。</param>
    /// <exception cref="ArgumentNullException">入口或集合为空。</exception>
    public EnemyAiPreset(Type idleStateType, Type engageStateType,
        params Func<AbstractEnemyAiState>[] stateFactories)
    {
        ArgumentNullException.ThrowIfNull(idleStateType);
        ArgumentNullException.ThrowIfNull(engageStateType);
        ArgumentNullException.ThrowIfNull(stateFactories);
        IdleStateType = idleStateType;
        EngageStateType = engageStateType;
        m_StateFactories = stateFactories.ToArray();
    }

    /// <summary>为一个怪物创建完整状态集合，创建前不保存任何运行时状态。</summary>
    /// <returns>由该怪物状态机独占的状态实例。</returns>
    /// <exception cref="InvalidOperationException">状态为空、类型重复或缺少入口及公共打断状态。</exception>
    public FsmState<IEnemyAiAgent>[] CreateStates()
    {
        HashSet<Type> types = new();
        FsmState<IEnemyAiAgent>[] states = new FsmState<IEnemyAiAgent>[m_StateFactories.Length];
        for (int i = 0; i < states.Length; i++)
        {
            var state = m_StateFactories[i]?.Invoke();
            if (state == null || !types.Add(state.GetType()))
                throw new InvalidOperationException("AI 预设包含空状态或重复状态类型。");
            states[i] = state;
        }
        // 通用打断与预设入口都必须存在，避免运行中才因切换失败。
        if (!types.Contains(IdleStateType) || !types.Contains(EngageStateType) ||
            !types.Contains(typeof(CcLockedState)) || !types.Contains(typeof(DeathState)))
            throw new InvalidOperationException("AI 预设缺少入口、受控或死亡状态。");
        return states;
    }
}
