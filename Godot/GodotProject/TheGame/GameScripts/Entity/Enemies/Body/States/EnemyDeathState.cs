using GameFramework.Fsm;
using GameLogic.Body;
using GameLogic.Enemies.Body;
using Godot;

namespace GameLogic.Enemies.Body;

/// <summary>
/// 死亡（终态）：进入时由宿主做副作用（<see cref="IEnemyBody.BeginDeath"/>：普通怪物保留受击体、首领关闭受击盒、广播
/// EnemyDiedEventArgs），作废出招请求；普通尸体保留抛飞并等待动画/落地，效果死亡固定身体并等待独立效果完成。
/// 池复用由OnShow重建整台状态机。
/// </summary>
/// <param name="waitForEffect">是否固定身体并等待独立死亡效果；默认使用普通尸体运动和动画时长。</param>
public sealed class EnemyDeathState(bool waitForEffect = false) : AbstractEnemyBodyState
{
    private float _left; // 死亡动画结束前的剩余时间（秒）。

    private bool _recycled; // 是否已经向宿主发送回收请求。
    private bool _starting; // 新的致死/尸体击退需要一次积分校正。

    /// <summary>
    /// 死亡状态不再响应受击中断。
    /// </summary>
    protected override bool HurtInterrupts => false;

    /// <summary>
    /// 清除待处理请求，执行死亡副作用并播放死亡动画。
    /// </summary>
    /// <param name="fsm">当前宿主拥有的状态机。</param>
    /// <param name="body">当前状态借用的身体宿主。</param>
    protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
    {
        body.MoveIntent = 0;
        body.TakeAttackRequest();
        _starting = !waitForEffect && body.HasPendingHurt;
        if (_starting)
        {
            body.Velocity = body.TakePendingHurt();
        }
        if (waitForEffect)
        {
            body.TakePendingHurt();
            body.Velocity = Vector2.Zero;
        }

        body.BeginDeath();
        _left = waitForEffect ? float.PositiveInfinity : body.Params.DeathTime;
        _recycled = false;
        body.RestartAnim(EnemyAnims.Death);
    }

    /// <summary>
    /// 维持尸体抛飞与原生循环；到达允许销毁的完成帧后只回收一次。
    /// </summary>
    /// <param name="fsm">当前宿主拥有的状态机。</param>
    /// <param name="body">当前状态借用的身体宿主。</param>
    /// <param name="dt">本次物理推进的秒数。</param>
    protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
    {
        if (waitForEffect)
        {
            body.Velocity = Vector2.Zero;
        }
        else
        {
            if (body.HasPendingHurt)
            {
                body.Velocity = body.TakePendingHurt();
                _starting = true;
            }
            ApplyHitMotion(body, dt, _starting);
            _starting = false;
        }

        if (_recycled)
        {
            return;
        }

        if (Elapsed(_left))
        {
            if (!waitForEffect && !body.OnFloor && body.Params.DeathAirLoopTime.HasValue)
            {
                // Flash keeps the corpse animation in its post-impact loop until the body lands.
                body.RestartAnim(EnemyAnims.Death, EnemyAnims.DeathAirLoopMarker);
                _left = body.Params.DeathTime - body.Params.DeathAirLoopTime.Value;
                return;
            }

            _recycled = true;
            body.RequestRecycle();
            return;
        }

        _left -= dt;
    }

    /// <summary>
    /// 独立死亡效果只登记完成；回收仍由下一身体帧提交，不能在动画回调中销毁状态机。
    /// </summary>
    public void CompleteEffect()
    {
        _left = 0;
    }
}
