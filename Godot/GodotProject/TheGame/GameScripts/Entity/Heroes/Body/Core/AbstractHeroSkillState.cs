using GameConfig.Skills;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Input;
using GameLogic.Skills;

namespace GameLogic.Heroes.Body;

/// <summary>具名技能状态共用的身体驱动；具体技能负责自己的进入规则、阶段和移动。</summary>
public abstract class AbstractHeroSkillState : AbstractHeroBodyState
{
    /// <summary>借用模型拥有的执行，只在本状态活动期间有效。</summary>
    protected SkillExecution Execution { get; set; }

    /// <summary>旧状态退出后开始技能；持续阶段重新占用身体时借用原执行。</summary>
    /// <param name="fsm">身体状态机。</param>
    /// <param name="body">英雄表现。</param>
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body) =>
        Execution = body.GetSystem<SkillSystem>().BeginBody((ISkillActor)body, body.ActionData.Time);

    /// <summary>执行完成或释放身体后回到移动状态；后续输入由公共技能系统分发。</summary>
    /// <param name="fsm">身体状态机。</param>
    /// <param name="body">英雄表现。</param>
    /// <param name="dt">游戏秒数。</param>
    protected override void Tick(IFsm<IHeroBody> fsm, IHeroBody body, float dt)
    {
        if (Execution.Completed || !Execution.BlocksBody)
        {
            ChangeToLocomotion(fsm, body);
            return;
        }
        body.GetSystem<InputSystem>().ConsumeAttack(body.ActionData);
        // 同次执行跨阶段或释放身体时，同一物理帧只推进一次游戏时间。
        if (Execution.LastAdvancedAt != body.ActionData.Time)
        {
            if (!Execution.GravitySuspended)
                ApplyGravity(body, dt);
            AdvanceSkill(body, dt);
        }
        body.GetSystem<SkillSystem>().Advance(Execution, (ISkillActor)body, dt, body.ActionData.Time);
        if (Execution.Completed || !Execution.BlocksBody)
            ChangeToLocomotion(fsm, body);
    }

    /// <summary>离开身体状态时释放表现；持续执行与受击中断由系统按执行状态区分。</summary>
    /// <param name="fsm">身体状态机。</param>
    /// <param name="body">英雄表现。</param>
    /// <param name="isShutdown">框架是否关停。</param>
    protected override void Leave(IFsm<IHeroBody> fsm, IHeroBody body, bool isShutdown)
    {
        body.GetSystem<SkillSystem>().ReleaseBody(Execution, (ISkillActor)body);
        Execution = null;
    }

    /// <summary>开始动画技能共用的表现与秒制无敌，不处理具体招式规则。</summary>
    /// <param name="body">本次身体。</param>
    /// <param name="action">共享动作定义。</param>
    protected void BeginAnimation(IHeroBody body, SkillActionConfig action)
    {
        if (action.InvincibleDurationSeconds > 0)
            body.GetSystem<SkillSystem>().GrantInvulnerability(body.ActionData.Time + action.InvincibleDelaySeconds,
                action.InvincibleDurationSeconds);
        ((ISkillActor)body).BeginSkillAnimation(action);
    }

    /// <summary>推进本招的具体流程；独立执行也可通过 SkillExecution 自行推进。</summary>
    /// <param name="body">本次身体。</param>
    /// <param name="dt">本帧游戏秒数。</param>
    protected virtual void AdvanceSkill(IHeroBody body, float dt) { }
}
