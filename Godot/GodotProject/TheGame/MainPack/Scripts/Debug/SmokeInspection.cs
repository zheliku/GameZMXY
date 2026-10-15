using GameLogic.Architecture;
using GameLogic.Level;
using GameLogic.Monsters.Body;
using GameLogic.Monsters.AI;
using GameLogic.Heroes.Body;
using System;
using System.Reflection;
using GameFramework.Fsm;
using GameFramework.Procedure;
using GameLogic.Procedure.FlowData;
using GameLogic.Entity;
using GameLogic.Heroes;
using GameLogic.Monsters;
using GameLogic;

/// <summary>开发测试侧读取内部事实与设置沙包，不要求玩法代码提供测试接口。</summary>
internal static class SmokeInspection
{
    /// <summary>沿继承链读取字段；字段变更时直接让测试失败。</summary>
    /// <typeparam name="T">字段的实际类型。</typeparam>
    /// <param name="instance">受测实例。</param>
    /// <param name="name">字段名。</param>
    /// <returns>字段当前值。</returns>
    /// <exception cref="ArgumentNullException">实例为空。</exception>
    /// <exception cref="MissingFieldException">字段不存在。</exception>
    public static T ReadField<T>(object instance, string name)
    {
        ArgumentNullException.ThrowIfNull(instance);
        // 私有字段不会随继承反射返回，逐层定位声明者。
        for (Type type = instance.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null)
            {
                return (T)field.GetValue(instance);
            }
        }

        throw new MissingFieldException(instance.GetType().FullName, name);
    }

    /// <summary>读取 C# 事件的实际处理器数，验证关闭退订及复用时没有重复订阅。</summary>
    /// <param name="source">事件所属的状态源。</param>
    /// <param name="eventName">字段式事件名称。</param>
    /// <param name="listener">接收事件的窗口或对象。</param>
    /// <returns>该对象在事件上的处理器数量。</returns>
    public static int CountEventHandlers(object source, string eventName, object listener)
    {
        var handlers = ReadField<Delegate>(source, eventName);
        int count = 0;
        if (handlers != null)
            foreach (var handler in handlers.GetInvocationList())
                if (ReferenceEquals(handler.Target, listener))
                    count++;
        return count;
    }

    /// <summary>读取英雄或怪物状态机的真实当前状态，并格式化日志名称。</summary>
    /// <param name="actor">受测英雄或怪物。</param>
    /// <returns>身体状态名；状态机未运行时为空。</returns>
    public static string BodyStateName(AbstractActorEntity actor)
    {
        object state = actor is AbstractHeroEntity
            ? ReadField<IFsm<IHeroBody>>(actor, "m_BodyFsm")?.CurrentState
            : ReadField<IFsm<IMonsterBody>>(actor, "m_BodyFsm")?.CurrentState;
        return StateName(state);
    }

    /// <summary>读取怪物 AI 状态机的真实当前状态。</summary>
    /// <param name="monster">受测怪物。</param>
    /// <returns>AI 状态名；状态机未运行时为空。</returns>
    public static string AiStateName(AbstractMonsterEntity monster) =>
        StateName(ReadField<IFsm<IMonsterAiAgent>>(monster, "m_AiFsm")?.CurrentState);

    /// <summary>读取怪物是否处于收招状态。</summary>
    /// <param name="monster">受测怪物。</param>
    /// <returns>身体状态机是否正在收招。</returns>
    public static bool InRecovery(AbstractMonsterEntity monster) =>
        ReadField<IFsm<IMonsterBody>>(monster, "m_BodyFsm")?.CurrentState is MonsterRecoveryState;

    /// <summary>读取关卡阶段所有者，未找到关卡时返回空。</summary>
    /// <param name="level">受测关卡。</param>
    /// <returns>正式逻辑使用的阶段序列。</returns>
    public static LevelStageSequence Sequence(LevelViewController level) =>
        level == null ? null : Model(level).Sequence;

    /// <summary>读取真实场景控制器，不向生产代码添加测试 getter。</summary>
    /// <param name="level">受测关卡控制器。</param>
    /// <returns>对应场景根节点。</returns>
    public static LevelViewController Scene(LevelViewController level) => level;

    /// <summary>借用测试对象归属的本局模型。</summary>
    /// <param name="level">受测控制器。</param>
    /// <returns>真实注册模型；控制器为空时为空。</returns>
    public static LevelModel Model(LevelViewController level) => level?.GetModel<LevelModel>();

    /// <summary>借用测试对象归属的业务模块。</summary>
    /// <param name="level">受测控制器。</param>
    /// <returns>真实注册模块。</returns>
    public static LevelSystem GetLevelSystem(LevelViewController level) => level.GetSystem<LevelSystem>();

    /// <summary>读取正式流程拥有的本次运行，流程不在关卡时返回空。</summary>
    /// <param name="procedure">当前关卡流程；允许为空。</param>
    /// <returns>本次关卡运行。</returns>
    public static LevelViewController Level(ProcedureLevel procedure) =>
        procedure == null ? null : ReadField<LevelViewController>(procedure, "m_LevelViewController");

    /// <summary>读取正式流程持有状态的 FSM，流程不在关卡时返回空。</summary>
    /// <param name="procedure">当前关卡流程；允许为空。</param>
    /// <returns>持有档案状态与模块的流程 FSM。</returns>
    public static IFsm<IProcedureManager> Flow(ProcedureLevel procedure) =>
        procedure == null ? null : ReadField<IFsm<IProcedureManager>>(procedure, "m_Owner");

    /// <summary>仅在测试中停止怪物 AI 并清除待消费意图，身体动作仍按正式逻辑执行。</summary>
    /// <param name="monster">作为沙包的怪物；允许为空。</param>
    /// <exception cref="MissingMethodException">正式 AI 清理方法已更名或删除。</exception>
    public static void FreezeAi(AbstractMonsterEntity monster)
    {
        if (monster == null)
        {
            return;
        }

        // 复用正式隐藏时的 AI 清理，不在实体中保留测试开关与额外状态。
        MethodInfo destroy = typeof(AbstractMonsterEntity).GetMethod("DestroyAiStateMachine", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(AbstractMonsterEntity).FullName, "DestroyAiStateMachine");
        destroy.Invoke(monster, [false]);
        IMonsterBody body = monster;
        body.MoveIntent = 0;
        body.TakeAttackRequest();
    }

    /// <summary>将状态类型名转换为测试日志沿用的短名称。</summary>
    /// <param name="state">真实状态实例；允许为空。</param>
    /// <returns>移除宿主前缀和 State 后缀的名称。</returns>
    private static string StateName(object state)
    {
        string name = state?.GetType().Name ?? string.Empty;
        if (name.StartsWith("Hero", StringComparison.Ordinal))
        {
            name = name["Hero".Length..];
        }
        else if (name.StartsWith("Monster", StringComparison.Ordinal))
        {
            name = name["Monster".Length..];
        }

        return name.EndsWith("State", StringComparison.Ordinal) ? name[..^"State".Length] : name;
    }
}
