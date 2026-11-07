using GameFramework.Fsm;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies;

/// <summary>
/// 兽形冲撞忽略目标距离，抵达相机两侧后停步，再向反方向冲刺。
/// </summary>
internal sealed class BullDemonChargePatrolState : AbstractEnemyAiState
{
    private int _direction; // 当前一轮冲撞方向，首次固定朝左。
    private float _restLeft; // 首轮边界停一秒，之后每轮停三秒。
    private bool _resting; // 已抵达边界，不能随目标移动立即重新起步。

    /// <inheritdoc />
    protected override void Enter(IEnemyAiAgent agent)
    {
        _direction = -1;
        _restLeft = 1f;
        _resting = false;
        agent.Move(_direction);
    }

    /// <inheritdoc />
    protected override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds)
    {
        if (!_resting && (_direction < 0 && agent.PatrolOffsetX < agent.PatrolBounds.X ||
            _direction > 0 && agent.PatrolOffsetX > agent.PatrolBounds.Y))
        {
            _resting = true;
            agent.Move(0);
        }
        if (!_resting)
        {
            return;
        }

        _restLeft -= elapseSeconds;
        if (_restLeft > 0f)
        {
            return;
        }

        _restLeft = 3f;
        _resting = false;
        _direction = -_direction;
        agent.Move(_direction);
    }
}
