using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;

namespace GameLogic.Enemies;

/// <summary>
/// 前两形态先结算死亡，再完成原生演出和后继登记，最后安全归还旧实体。
/// </summary>
internal sealed class BullDemonTransformState : AbstractEnemyBodyState
{
    private bool _animationCompleted; // 原生死亡片段与可选BossDead均已完成。
    private bool _requested; // 本次状态只提交一次后继显示。
    private bool _transformed; // 原服务完成所有权登记后才允许回收旧形态。

    /// <inheritdoc />
    protected override bool HurtInterrupts => false;

    /// <inheritdoc />
    protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
    {
        _animationCompleted = false;
        _requested = false;
        _transformed = false;
        body.MoveIntent = 0;
        body.TakeAttackRequest();
        body.TakePendingHurt();
        body.Velocity = Vector2.Zero;
        body.BeginDeath();
        body.RestartAnim(EnemyAnims.Death);
    }

    /// <inheritdoc />
    protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
    {
        body.Velocity = Vector2.Zero;
        if (_animationCompleted && !_requested)
        {
            _requested = true;
            ((AbstractBullDemonFormEntity)body).BeginTransformation(() => _transformed = true);
        }
        if (_transformed)
        {
            body.RequestRecycle();
        }
    }

    /// <summary>
    /// 原生方法键或独立死亡效果只登记完成，不在动画遍历中变身或销毁状态机。
    /// </summary>
    internal void CompleteAnimation() => _animationCompleted = true;
}
