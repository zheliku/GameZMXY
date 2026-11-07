using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;

namespace GameLogic.Enemies;

/// <summary>
/// 最终形态的身体状态拥有心脏显隐、火柱轮次和入场，不创建没有选招职责的AI。
/// </summary>
internal sealed class BullDemonHeartState : AbstractEnemyBodyState
{
    private HeartPhase _heartPhase; // 当前心脏显隐阶段。
    private float _heartLeft; // 隐藏或完整显现的随机停留秒数。
    private float _introLeft; // 原生背景进入剩余时长。
    private float _fireLeft; // 下一轮火柱前的随机秒数。
    private int _fireCount; // 依次2、4、6根，再从2根开始。

    /// <inheritdoc />
    protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
    {
        var owner = (BullDemonHeartEntity)body;
        body.MoveIntent = 0;
        body.Velocity = Vector2.Zero;
        body.RestartAnim(EnemyAnims.Idle);
        owner.BeginPresentation();
        _introLeft = owner.IntroDuration;
        _heartPhase = HeartPhase.Hidden;
        _heartLeft = GD.RandRange(5, 9);
        _fireLeft = GD.RandRange(2, 6);
        _fireCount = 2;
    }

    /// <inheritdoc />
    protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
    {
        var owner = (BullDemonHeartEntity)body;
        body.Velocity = Vector2.Zero;
        if (_introLeft > 0)
        {
            _introLeft -= dt;
            if (_introLeft <= 0)
            {
                owner.StartHands();
            }
        }
        _fireLeft -= dt;
        if (_fireLeft <= 0)
        {
            owner.SpawnFire(_fireCount);
            _fireCount = _fireCount == 6 ? 2 : _fireCount + 2;
            _fireLeft = GD.RandRange(2, 6);
        }
        if (_heartPhase is HeartPhase.Showing or HeartPhase.Hiding)
        {
            if (owner.HeartFading)
            {
                return;
            }

            _heartLeft = _heartPhase == HeartPhase.Showing ? GD.RandRange(2, 5) : GD.RandRange(5, 11);
            _heartPhase = _heartPhase == HeartPhase.Showing ? HeartPhase.Shown : HeartPhase.Hidden;
            return;
        }
        _heartLeft -= dt;
        if (_heartLeft <= 0)
        {
            bool show = _heartPhase == HeartPhase.Hidden;
            owner.FadeHeart(show);
            _heartPhase = show ? HeartPhase.Showing : HeartPhase.Hiding;
        }
    }

    /// <summary>
    /// 每个阶段只维护自己的下一次转换。
    /// </summary>
    private enum HeartPhase
    {
        Hidden, Showing, Shown, Hiding
    }
}
