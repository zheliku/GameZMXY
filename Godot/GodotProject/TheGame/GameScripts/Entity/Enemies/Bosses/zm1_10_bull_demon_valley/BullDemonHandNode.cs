using System;
using GameLogic.Entity;
using Godot;

namespace GameLogic.Enemies;

/// <summary>
/// 最终形态的一只巨掌拥有游荡、加速下压与落地停留，命中编号归首领共享。
/// </summary>
public partial class BullDemonHandNode : Node2D
{
    [Export] private Area2D _hitBox; // 巨掌少量基础矩形判定，不含场景背景。
    private Action<AbstractActorEntity> _hit; // 借用本次首领的共享命中入口。
    private Action _resetAttack; // 任一手掌复位时更新父攻击编号。
    private Motion _motion; // 当前运动阶段。
    private float _left; // 当前游荡或落地停留剩余秒数。
    private float _directionLeft; // 下一次随机折返前秒数。
    private int _direction; // 0向左、1向右、2停留，与源规则一致。
    private float _downSpeed; // 本轮下压速度，单位px/s。
    private bool _firstDescent; // 首轮初速3px/源帧，随后2px/源帧。

    /// <summary>
    /// 节点随首领显示，未开始时保持停止。
    /// </summary>
    public override void _Ready() => SetPhysicsProcess(false);

    /// <summary>
    /// 重置一只手掌的原点和本次首领回调。
    /// </summary>
    /// <param name="x">相对首领的初始横坐标。</param>
    /// <param name="hit">本次共享命中入口。</param>
    /// <param name="resetAttack">更新共享父攻击编号。</param>
    public void Start(float x, Action<AbstractActorEntity> hit, Action resetAttack)
    {
        _hit = hit;
        _resetAttack = resetAttack;
        Position = new Vector2(x, 18);
        _motion = Motion.Wandering;
        _left = GD.RandRange(2, 4);
        _directionLeft = 1f;
        _direction = 0;
        _firstDescent = true;
        Visible = true;
        SetPhysicsProcess(true);
    }

    /// <summary>
    /// 首领死亡或离场立即停止判定和局部运动，不保留上次回调。
    /// </summary>
    public void Stop()
    {
        SetPhysicsProcess(false);
        _hit = null;
        _resetAttack = null;
        Visible = false;
    }

    /// <summary>
    /// 秒制还原游荡和加速下压，只有下压阶段进行共享编号的伤害判定。
    /// </summary>
    /// <param name="delta">暂停时不推进的游戏秒数。</param>
    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_hit == null || dt <= 0)
        {
            return;
        }

        if (_motion == Motion.Wandering)
        {
            float stepX = 0;
            if (_direction == 0)
            {
                stepX = -72 * dt;
            }
            else if (_direction == 1)
            {
                stepX = 72 * dt;
            }
            Position += new Vector2(stepX, 0);
            if (Position.X < -400)
            {
                _direction = 1;
            }
            else if (Position.X > 400)
            {
                _direction = 0;
            }

            _directionLeft -= dt;
            if (_directionLeft <= 0)
            {
                _direction = (_direction + (GD.Randf() > .5f ? 2 : 1)) % 3;
                _directionLeft = GD.RandRange(1, 2);
            }
            _left -= dt;
            if (_left <= 0)
            {
                _motion = Motion.Descending;
                _downSpeed = (_firstDescent ? 3f : 2f) * 24f - 6f;
            }
            return;
        }
        if (_motion == Motion.Descending)
        {
            Position += new Vector2(0, _downSpeed * dt + 144f * dt * dt);
            _downSpeed += 288f * dt;
            foreach (Area2D area in _hitBox.GetOverlappingAreas())
            {
                if (area is HurtBoxNode { OwnerEntity: { IsAlive: true } target })
                {
                    _hit(target);
                }
            }

            if (Position.Y >= 144)
            {
                _motion = Motion.Impact;
                _left = 1f;
            }
            return;
        }
        _left -= dt;
        if (_left <= 0)
        {
            Position = new Vector2(Position.X, 18);
            _firstDescent = false;
            _motion = Motion.Wandering;
            _left = GD.RandRange(3, 6);
            _resetAttack();
        }
    }

    /// <summary>
    /// 互斥运动阶段，避免多个移动标志竞争。
    /// </summary>
    private enum Motion
    {
        Wandering, Descending, Impact
    }
}
