using GameConfig.Skills;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Heroes.Body;
using GameLogic.Input;
using GameLogic.Skills;
using Godot;

namespace GameLogic.Heroes.Tangseng;

/// <summary>
/// 冰龙波在原生蓄力姿态等待松键，再按蓄力结果发射独立法弹。
/// </summary>
public sealed class TangsengIceDragonState : AbstractHeroSkillState
{
    private const float ChargeSeconds = 30f / 24f; // 官方30源帧蓄力上限。
    private bool _releasing; // 松键后进入释放片段。
    private bool _effectReleased; // 本次释放方法键已经提交法弹。
    private bool _fullCharge; // 本次已达到满蓄上限。
    private float _chargeTime; // 按游戏秒累计，暂停不增长。
    private int _moveAxis; // 空中沿用普攻起手方向，松键后本次不再恢复横移。

    /// <summary>
    /// 本次是否达到官方30源帧蓄力上限。
    /// </summary>
    public bool FullCharge => _fullCharge;

    /// <inheritdoc />
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
    {
        base.Enter(fsm, body);
        _releasing = _effectReleased = _fullCharge = false;
        _chargeTime = 0f;
        _moveAxis = body.ActionData.MoveAxis;
        if (!IsAirborne(body))
        {
            Stand(body);
        }
        BeginAnimation(body, "skills/ice_dragon");
        ((ISkillActor)body).PauseSkillAnimation();
    }

    /// <inheritdoc />
    protected override void UpdateSkill(IHeroBody body, float dt)
    {
        InputSystem input = body.GetSystem<InputSystem>();
        if (!input.IsDirectionHeld(body.ActionData, _moveAxis) ||
            input.WasDirectionReleased(body.ActionData, _moveAxis))
        {
            _moveAxis = 0;
        }
        float speed = body.ActionData.Running ? body.Params.RunSpeed : body.Params.WalkSpeed;
        body.Velocity = new Vector2(IsAirborne(body) ? _moveAxis * speed * body.MoveSpeedMultiplier : 0,
            body.Velocity.Y);
        if (_releasing)
        {
            return;
        }

        if (input.IsActionPressed(InputSystem.Attack))
        {
            _chargeTime = Mathf.Min(_chargeTime + dt, ChargeSeconds);
            ((TangsengEntity)body).UpdateIceDragonCharge(_chargeTime / ChargeSeconds);
            return;
        }

        _fullCharge = _chargeTime >= ChargeSeconds - 0.0001f;
        _releasing = true;
        ((TangsengEntity)body).UpdateIceDragonCharge(0);
        BeginAnimation(body, "skills/ice_dragon");
    }

    /// <inheritdoc />
    protected override void Leave(IFsm<IHeroBody> fsm, IHeroBody body, bool isShutdown)
    {
        if (!isShutdown)
        {
            ((TangsengEntity)body).UpdateIceDragonCharge(0);
        }
        base.Leave(fsm, body, isShutdown);
    }

    /// <summary>
    /// 释放方法键按本次蓄力结果扣费并提交唯一法弹。
    /// </summary>
    /// <param name="body">唐僧身体。</param>
    /// <param name="normalScene">未满蓄力的普通法弹。</param>
    /// <param name="chargedScene">满蓄力的冰龙法弹。</param>
    public void Release(IHeroBody body, PackedScene normalScene, PackedScene chargedScene)
    {
        if (_effectReleased)
        {
            return;
        }

        _effectReleased = true;
        if (_fullCharge && !body.GetSystem<SkillSystem>().TrySpendActivatedMana(body.HeroId, SkillId.IceDragonWave))
        {
            body.GetSystem<SkillSystem>().CancelSkill(body.HeroId);
            return;
        }

        ((TangsengEntity)body).SpawnIceDragonBolt(_fullCharge ? chargedScene : normalScene, _fullCharge);
    }
}
