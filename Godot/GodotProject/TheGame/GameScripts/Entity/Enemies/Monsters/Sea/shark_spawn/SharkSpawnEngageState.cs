using System;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Enemies.AI;
using Godot;

namespace GameLogic.Enemies;

/// <summary>
/// 鲨鱼按原点追踪、固定折返间隔和一秒决策节拍选择撞击或旋风；撞击后退避三秒。
/// </summary>
/// <param name="trackingHeight">场景允许追踪的高差，像素。</param>
/// <param name="altitude">读取当前世界高度。</param>
/// <param name="horizontalSpeed">读取当前身体水平速度，退避沿用实际运动方向。</param>
internal sealed class SharkSpawnEngageState(float trackingHeight, Func<float> altitude,
    Func<float> horizontalSpeed) : AbstractEnemyAiState
{
    private float _turnInterval; // 本次显示固定的1～3秒折返间隔。
    private double _turnAt; // 下一次允许改变水平追踪方向的游戏时点。
    private double _retreatUntil; // 从普通撞击起手开始的三秒退避截止时点。
    private int _travelDirection; // 折返节流期间保留的水平移动方向。

    /// <inheritdoc />
    protected internal override void OnInit(IFsm<IEnemyAiAgent> fsm)
    {
        base.OnInit(fsm);
        _turnInterval = 1f + 2f * fsm.Owner.NextRandom();
    }

    /// <inheritdoc />
    protected override void Enter(IEnemyAiAgent agent)
    {
        base.Enter(agent);
        EnemyAiData flight = agent.AiData;
        flight.VerticalIntent = 0;
        flight.DirectVerticalMovement = false;
    }

    /// <inheritdoc />
    protected override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds)
    {
        EnemyAiData flight = agent.AiData;
        double now = agent.AiData.ElapsedSeconds;
        if (agent.IsAttacking)
        {
            return;
        }

        // 退避与招式、受击共用游戏时钟；只升至150px，不继续飞出画面。
        flight.DirectVerticalMovement = now < _retreatUntil;
        if (flight.DirectVerticalMovement)
        {
            int direction = Math.Sign(horizontalSpeed());
            agent.Move(direction == 0 ? _travelDirection : direction);
            flight.VerticalIntent = altitude() > 150f ? -1 : 0;
            return;
        }

        if (!agent.HasTarget)
        {
            flight.VerticalIntent = 0;
            agent.GetSystem<EnemyAiSystem>().Patrol(agent, elapseSeconds);
            _travelDirection = agent.AiData.MoveIntent;
            return;
        }

        if (Math.Abs(agent.TargetOffset.Y) > trackingHeight)
        {
            flight.VerticalIntent = 0;
            agent.GetSystem<EnemyAiSystem>().Patrol(agent, elapseSeconds);
            _travelDirection = agent.AiData.MoveIntent;
            return;
        }

        // 追踪条件与450px攻击定义分开：超过250px或高差50px时追向目标上方60px。
        Vector2 offset = agent.TargetOffset;
        if (!agent.IsInEngageRange)
        {
            if (now >= _turnAt)
            {
                _travelDirection = offset.X < 0f ? -1 : 1;
                _turnAt = now + _turnInterval;
            }
            agent.Move(_travelDirection);
            flight.VerticalIntent = offset.Y > 60f ? 1 : -1;
            return;
        }

        flight.VerticalIntent = 0;
        agent.Move(_travelDirection);
        if (!agent.GetSystem<EnemyAiSystem>().IsDecisionDue(agent.AiData, agent.Config.AttackInterval))
        {
            return;
        }

        // 普攻不受旋风冷却限制；旋风只在未抽中普攻且自身冷却结束时触发。
        if (Chance(agent, agent.Config.AttackDesire))
        {
            if (agent.RequestAttack(0))
            {
                _travelDirection = offset.X < 0f ? -1 : 1;
                _retreatUntil = now + 3f;
            }
        }
        else if (agent.AiData.Cooldowns[1] <= 0f)
        {
            agent.RequestAttack(1);
        }
    }
}
