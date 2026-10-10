using System;
using System.Reflection;
using GameFramework.Fsm;
using GameLogic.Entity;
using GameLogic.Entity.Heroes;
using GameLogic.Entity.Heroes.Body;
using GameLogic.Entity.Monsters;
using GameLogic.Entity.Monsters.AI;
using GameLogic.Entity.Monsters.Body;
using GameLogic.Level;
using GameLogic.Session;

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

    /// <summary>读取英雄或怪物状态机的真实当前状态，并格式化日志名称。</summary>
    /// <param name="actor">受测英雄或怪物。</param>
    /// <returns>身体状态名；状态机未运行时为空。</returns>
    public static string BodyStateName(ActorEntity actor)
    {
        object state = actor is HeroEntity
            ? ReadField<IFsm<IHeroBody>>(actor, "m_BodyFsm")?.CurrentState
            : ReadField<IFsm<IMonsterBody>>(actor, "m_BodyFsm")?.CurrentState;
        return StateName(state);
    }

    /// <summary>读取怪物 AI 状态机的真实当前状态。</summary>
    /// <param name="monster">受测怪物。</param>
    /// <returns>AI 状态名；状态机未运行时为空。</returns>
    public static string AiStateName(MonsterEntity monster) =>
        StateName(ReadField<IFsm<IMonsterAiAgent>>(monster, "m_AiFsm")?.CurrentState);

    /// <summary>读取怪物是否处于收招状态。</summary>
    /// <param name="monster">受测怪物。</param>
    /// <returns>身体状态机是否正在收招。</returns>
    public static bool InRecovery(MonsterEntity monster) =>
        ReadField<IFsm<IMonsterBody>>(monster, "m_BodyFsm")?.CurrentState is MonsterRecoveryState;

    /// <summary>读取关卡阶段所有者，未找到关卡时返回空。</summary>
    /// <param name="level">受测关卡。</param>
    /// <returns>正式逻辑使用的阶段序列。</returns>
    public static LevelStageSequence Sequence(LevelController level) =>
        level == null ? null : ReadField<LevelStageSequence>(level, "m_Sequence");

    /// <summary>读取正式流程拥有的本次运行，流程不在关卡时返回空。</summary>
    /// <param name="procedure">当前关卡流程；允许为空。</param>
    /// <returns>本次关卡运行。</returns>
    public static LevelRun Run(ProcedureLevel procedure) =>
        procedure == null ? null : ReadField<LevelRun>(procedure, "m_Run");

    /// <summary>读取正式流程已注入的档案作用域，流程不在关卡时返回空。</summary>
    /// <param name="procedure">当前关卡流程；允许为空。</param>
    /// <returns>当前档案作用域。</returns>
    public static GameContext Context(ProcedureLevel procedure) =>
        procedure == null ? null : ReadField<GameContext>(procedure, "m_Context");

    /// <summary>仅在测试中停止怪物 AI 并清除待消费意图，身体动作仍按正式逻辑执行。</summary>
    /// <param name="monster">作为沙包的怪物；允许为空。</param>
    /// <exception cref="MissingMethodException">正式 AI 清理方法已更名或删除。</exception>
    public static void FreezeAi(MonsterEntity monster)
    {
        if (monster == null)
        {
            return;
        }

        // 复用正式隐藏时的 AI 清理，不在实体中保留测试开关与额外状态。
        MethodInfo destroy = typeof(MonsterEntity).GetMethod("DestroyAi", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(MonsterEntity).FullName, "DestroyAi");
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
