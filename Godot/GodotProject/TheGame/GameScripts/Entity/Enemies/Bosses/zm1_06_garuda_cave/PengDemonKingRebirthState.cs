using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;

namespace GameLogic.Enemies;

/// <summary>
/// 复生开始即补满本次配方生命；原生动画结束恢复行动，受击仍可正常打断。
/// </summary>
internal sealed class PengDemonKingRebirthState : PengDemonKingBodyState
{
    private float _left; // 原生30帧复生动作的剩余时间。

    /// <inheritdoc />
    protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
    {
        var actor = (PengDemonKingEntity)body;
        actor.BeginRebirth();
        Animation animation = actor.AnimPlayer.GetAnimation(PengDemonKingAnims.Rebirth);
        _left = (float)(animation.HasMarker("complete") ? animation.GetMarkerTime("complete") : animation.Length);
        body.RestartAnim(PengDemonKingAnims.Rebirth);
    }

    /// <inheritdoc />
    protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
    {
        body.TakeAttackRequest();
        ApplyGravity(body, dt);
        Stand(body);
        if (Elapsed(_left))
        {
            ChangeState<PengDemonKingMoveState>(fsm);
            return;
        }
        _left -= dt;
    }
}
