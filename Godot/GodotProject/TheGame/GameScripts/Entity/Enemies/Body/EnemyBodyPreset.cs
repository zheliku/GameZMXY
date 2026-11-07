using System;
using System.Collections.Generic;
using System.Linq;
using GameFramework.Fsm;

namespace GameLogic.Enemies.Body;

/// <summary>
/// 敌人身体状态配方；声明行动入口和状态工厂，每次入场创建独立状态实例。
/// </summary>
public sealed class EnemyBodyPreset
{
    private readonly Func<AbstractEnemyBodyState>[] _stateFactories; // 不保存运行中的状态或计时器。

    /// <summary>
    /// 组合身体状态，允许具体敌人增加状态或替换默认攻击/死亡状态。
    /// </summary>
    /// <param name="movementStateType">行动入口，必须包含在配方中。</param>
    /// <param name="deathStateType">死亡入口，必须包含在配方中。</param>
    /// <param name="stateFactories">每次调用均产生新状态的工厂。</param>
    /// <exception cref="ArgumentNullException">行动入口或工厂集合为空。</exception>
    public EnemyBodyPreset(Type movementStateType, Type deathStateType,
        params Func<AbstractEnemyBodyState>[] stateFactories)
    {
        ArgumentNullException.ThrowIfNull(movementStateType);
        ArgumentNullException.ThrowIfNull(deathStateType);
        ArgumentNullException.ThrowIfNull(stateFactories);
        MovementStateType = movementStateType;
        DeathStateType = deathStateType;
        _stateFactories = stateFactories.ToArray();
    }

    /// <summary>
    /// 入场、受击恢复和收招后使用的行动入口。
    /// </summary>
    public Type MovementStateType { get; }

    /// <summary>
    /// 生命耗尽后进入的状态，可由专属演出或形态转换替换。
    /// </summary>
    public Type DeathStateType { get; }

    /// <summary>
    /// 为当前实例创建状态集，拒绝重复、空状态或缺少行动入口的配方。
    /// </summary>
    /// <returns>当前身体状态机独占的状态。</returns>
    /// <exception cref="InvalidOperationException">状态为空、类型重复或缺少行动入口。</exception>
    public FsmState<IEnemyBody>[] CreateStates()
    {
        HashSet<Type> types = new();
        AbstractEnemyBodyState[] states = _stateFactories.Select(factory => factory?.Invoke()).ToArray();
        foreach (AbstractEnemyBodyState state in states)
        {
            if (state == null || !types.Add(state.GetType()))
            {
                throw new InvalidOperationException("身体预设包含空状态或重复类型。");
            }
        }

        if (!types.Contains(MovementStateType) || !types.Contains(DeathStateType))
        {
            throw new InvalidOperationException("身体预设缺少行动或死亡入口。");
        }

        return states;
    }
}

/// <summary>
/// 标准走/飞/游配方；运动方式和物种分类独立，具体敌人可直接组合其他身体状态。
/// </summary>
public static class EnemyBodyPresets
{
    /// <summary>
    /// 创建重力行走配方，含普通攻击、收招、受击和死亡。
    /// </summary>
    /// <param name="waitForDeathEffect">死亡是否等待原生轨道生成的独立效果。</param>
    /// <returns>入口为行走状态的身体预设。</returns>
    public static EnemyBodyPreset Walk(bool waitForDeathEffect = false)
    {
        return new(typeof(EnemyWalkState), typeof(EnemyDeathState),
            () => new EnemyWalkState(), () => new EnemyAttackState(), () => new EnemyRecoveryState(),
            () => new EnemyHurtState(), () => new EnemyDeathState(waitForDeathEffect));
    }

    /// <summary>
    /// 空中飞行；垂直参数来自具体物种，出招前停止飞行速度。
    /// </summary>
    /// <param name="acceleration">垂直加速度，px/s²。</param>
    /// <param name="speed">垂直速度上限，px/s。</param>
    /// <returns>入口为飞行状态的身体预设。</returns>
    public static EnemyBodyPreset Fly(float acceleration, float speed)
    {
        return new(typeof(EnemyFlyState), typeof(EnemyDeathState), () => new EnemyFlyState(acceleration, speed),
            () => new EnemyAttackState(), () => new EnemyRecoveryState(),
            () => new EnemyHurtState(), () => new EnemyDeathState());
    }

    /// <summary>
    /// 创建自由游动配方；需要时可替换为物种专属身体攻击状态。
    /// </summary>
    /// <param name="verticalAcceleration">垂直加速度，px/s²。</param>
    /// <param name="verticalSpeed">垂直速度上限，px/s。</param>
    /// <param name="directSpeed">直接上/下游阶段的速度，px/s。</param>
    /// <returns>入口为游动状态的身体预设。</returns>
    public static EnemyBodyPreset Swim(float verticalAcceleration, float verticalSpeed, float directSpeed) => Swim<EnemyAttackState>(verticalAcceleration, verticalSpeed, directSpeed);

    /// <summary>
    /// 用物种专属攻击状态组合游动配方，行动和攻击始终指向同一状态类型。
    /// </summary>
    /// <typeparam name="TAttackState">游动期间接受出招请求后进入的身体状态。</typeparam>
    /// <param name="verticalAcceleration">垂直加速度，px/s²。</param>
    /// <param name="verticalSpeed">垂直速度上限，px/s。</param>
    /// <param name="directSpeed">直接上/下游阶段的速度，px/s。</param>
    /// <returns>每次入场创建独立游动和攻击状态的预设。</returns>
    public static EnemyBodyPreset Swim<TAttackState>(float verticalAcceleration, float verticalSpeed, float directSpeed)
        where TAttackState : AbstractEnemyBodyState, new()
    {
        return new(typeof(EnemySwimState), typeof(EnemyDeathState),
            () => new EnemySwimState(verticalAcceleration, verticalSpeed, directSpeed, typeof(TAttackState)),
            () => new TAttackState(), () => new EnemyRecoveryState(),
            () => new EnemyHurtState(), () => new EnemyDeathState());
    }
}
