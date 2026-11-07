using GameFramework.Fsm;
using GameLogic.Heroes.Body;
using Godot;

namespace GameLogic.Heroes.Tangseng;

/// <summary>
/// 玄冰阵在第15源帧提交独立判定，并安排已有分身协同施法。
/// </summary>
public sealed class TangsengDarkIceState : AbstractHeroSkillState
{
    private bool _hit; // 本次原生出手键已提交判定。

    /// <inheritdoc />
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
    {
        base.Enter(fsm, body);
        GravitySuspended = true;
        body.Velocity = Vector2.Zero;
        _hit = false;
        BeginAnimation(body, "skills/dark_ice");
        ((TangsengEntity)body).PlayShadowDarkIce();
    }

    /// <summary>
    /// 从本体原生方法键提交本体效果，分身由自身时间轴独立出手。
    /// </summary>
    /// <param name="body">唐僧身体。</param>
    /// <param name="effectScene">原生轨道引用的玄冰阵场景。</param>
    public void BeginHit(IHeroBody body, PackedScene effectScene)
    {
        if (_hit)
        {
            return;
        }

        _hit = true;
        var actor = (TangsengEntity)body;
        actor.SpawnDarkIceEffect(effectScene, actor.GlobalPosition, actor.Facing);
    }
}
