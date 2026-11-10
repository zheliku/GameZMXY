using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Heroes.Body;
using GameLogic.Input;
using GameLogic.Skills;
using Godot;

namespace GameLogic.Heroes.Tangseng;

/// <summary>
/// 水魔爆由原生释放键提交第一段，再消费一次追加输入。
/// </summary>
public sealed class TangsengWaterDemonBurstState : AbstractHeroSkillState
{
    private bool _followUp; // 本次追加输入已消费。

    /// <inheritdoc />
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
    {
        base.Enter(fsm, body);
        Stand(body);
        _followUp = false;
        BeginAnimation(body, "skills/water_burst");
    }

    /// <inheritdoc />
    protected override void UpdateSkill(IHeroBody body, float dt)
    {
        Stand(body);
        var actor = (TangsengEntity)body;
        if (!_followUp && actor.CanReleaseWaterDemon &&
            body.GetSystem<InputSystem>().ConsumePress(body.ActionData, InputSystem.Skill4, body.Slot))
        {
            _followUp = true;
            actor.ReleaseWaterDemonFollowUp();
            ((ISkillActor)body).BeginSkillAnimation("skills/water_burst", "release");
        }
    }

    /// <summary>
    /// 未二次释放时按官方第42帧结束，追加分支跳过该键。
    /// </summary>
    public void Timeout() => CompleteAnimation();
}
