using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Input;
using Godot;

namespace GameLogic.Heroes.Body;

/// <summary>
/// 造一普攻：按键当场检查冷却，允许立即换段，无有效输入则正常收招。
/// </summary>
public sealed class HeroAttackState : AbstractHeroBodyState
{
    private int _moveAxis; // 起手时已有的移动方向，攻击中不接受新的方向。
    private double _endsAt; // 本段动画收招的游戏时刻，单位秒。

    /// <summary>
    /// 开始已裁定的攻击段。
    /// </summary>
    /// <param name="fsm">所属身体状态机。</param>
    /// <param name="body">本次攻击的身体。</param>
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
    {
        _moveAxis = body.ActionData.MoveAxis;
        body.SetFacing(_moveAxis);
        StartSegment(body);
    }

    /// <summary>
    /// 收招、受击、死亡或隐藏时归还攻击包。
    /// </summary>
    /// <param name="fsm">所属身体状态机。</param>
    /// <param name="body">本次攻击的身体。</param>
    /// <param name="isShutdown">框架是否关停。</param>
    protected override void Leave(IFsm<IHeroBody> fsm, IHeroBody body, bool isShutdown) => body.EndAttack();

    /// <summary>
    /// 消费普攻，地面定身，空中只保留起手方向并允许松键停止，按动画时长收招。
    /// </summary>
    /// <param name="fsm">所属身体状态机。</param>
    /// <param name="body">本次攻击的身体。</param>
    /// <param name="dt">游戏物理帧秒数。</param>
    protected override void Tick(IFsm<IHeroBody> fsm, IHeroBody body, float dt)
    {
        if (TryStartSkill(fsm, body))
        {
            return;
        }
        // 原版松键清除 isLeft/isRight；攻击中再按方向不会重新置位，须等收招。
        if (!body.GetSystem<InputSystem>().IsDirectionHeld(body.ActionData, _moveAxis) ||
            body.GetSystem<InputSystem>().WasDirectionReleased(body.ActionData, _moveAxis))
        {
            _moveAxis = 0;
        }

        if (body.ActionData.AttackRequested)
        {
            body.ActionData.AttackRequested = false;
            StartSegment(body);
        }

        ApplyGravity(body, dt);
        int axis = IsAirborne(body) ? _moveAxis : 0;
        float speed = body.ActionData.Running ? body.Params.RunSpeed : body.Params.WalkSpeed;
        body.Velocity = new Vector2(axis * speed * body.MoveSpeedMultiplier, body.Velocity.Y);
        if (body.ActionData.Time + TimeEpsilon >= _endsAt)
        {
            ChangeToLocomotion(fsm, body);
        }
    }

    /// <inheritdoc />
    public override bool TryGetActionInput(IHeroBody body, out bool attacking, out bool airborne)
    {
        attacking = true;
        airborne = IsAirborne(body);
        return true;
    }

    /// <summary>
    /// 装填攻击包并从零播放本次动作，空中使用指定下劈。
    /// </summary>
    /// <param name="body">英雄身体。</param>
    private void StartSegment(IHeroBody body)
    {
        int segment = body.ActionData.AttackSegment;
        _endsAt = body.ActionData.Time + body.Params.AttackTimes[segment];
        body.BeginAttack(segment);
        body.RestartAnim(body.Params.AttackAnims[segment]);
        if (!IsAirborne(body))
        {
            body.Velocity = new Vector2(0f, body.Velocity.Y);
        }
    }
}
