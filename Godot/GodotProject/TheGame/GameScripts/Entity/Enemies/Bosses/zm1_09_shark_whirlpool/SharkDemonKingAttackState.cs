using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies;

/// <summary>
/// 鲨魔王在起手时选择高低生命动作，按所选原生动作的完成标记收招。
/// </summary>
internal sealed class SharkDemonKingAttackState : AbstractEnemyBodyState
{
    private static readonly string[] s_highAnimations = ["attack_1", "attack_2", "attack_3"]; // 高生命阶段的三招。
    private static readonly string[] s_lowAnimations = ["attack_1_low", "attack_2_low", "attack_3_low"]; // 低生命阶段的三招。
    private int _index; // 本次起手固定的攻击下标。
    private float _left; // 本次所选动作的剩余秒数。

    /// <inheritdoc />
    protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
    {
        _index = fsm.GetData<VarInt32>(AttackIndexKey).Value;
        fsm.RemoveData(AttackIndexKey);
        body.MoveIntent = 0;
        body.BeginAttack(_index);
        string[] animations = ((SharkDemonKingEntity)body).LowPhase ? s_lowAnimations : s_highAnimations;
        Animation animation = ((SharkDemonKingEntity)body).AnimPlayer.GetAnimation(animations[_index]);
        _left = (float)(animation.HasMarker("complete") ? animation.GetMarkerTime("complete") : animation.Length);
        body.RestartAnim(animations[_index]);
    }

    /// <inheritdoc />
    protected override void Leave(IFsm<IEnemyBody> fsm, IEnemyBody body, bool isShutdown)
    {
        body.MoveIntent = 0;
        body.EndAttack();
    }

    /// <inheritdoc />
    protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
    {
        ApplyGravity(body, dt);
        Stand(body);
        if (!Elapsed(_left))
        {
            _left -= dt;
            return;
        }
        float recovery = body.Params.AttackRecovery[_index];
        if (recovery > 0f)
        {
            fsm.SetData(RecoveryDurationKey, (VarSingle)recovery);
            ChangeState<EnemyRecoveryState>(fsm);
        }
        else
        {
            ChangeState(fsm, body.MovementStateType);
        }
    }
}
