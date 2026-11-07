using GameLogic.Effects;
using Godot;

namespace GameLogic.Enemies;

/// <summary>
/// 虾兵魔法弹：按源加速并在1000px距离内飞行，被玩家攻击碰到后停伤并淡出一秒。
/// </summary>
public partial class ShrimpProjectileEffectNode : SkillEffectNode
{
    private const float InitialSpeed = 96f; // 源setSpeed(4)按24fps换算。
    private const float Acceleration = 230.4f; // 源每帧增加0.4，换算为像素每秒平方。
    private const float RightSpeedLimit = 172.8f; // 源speed<7后最后一次增加到7.2；向左保留原符号判断。
    private const float MaxDistance = 1000f; // 官方弹体飞行距离。
    private const float FadeDuration = 1f; // 官方TweenMax一秒消散。

    [Export] private Area2D _cancelArea; // 只监听玩家攻击层，不充当角色受击体。
    private float _speed; // 当前绝对速度。
    private float _distance; // 本次显示已经飞过的距离。
    private float _fadeLeft; // 大于零表示已被击散，期间不再运动或伤害。

    /// <inheritdoc />
    public override void _Ready()
    {
        base._Ready();
        _cancelArea.AreaEntered += OnPlayerAttackEntered;
    }

    /// <inheritdoc />
    public override void OnGet()
    {
        base.OnGet();
        _speed = InitialSpeed;
        _distance = 0f;
        _fadeLeft = 0f;
        Modulate = Colors.White;
    }

    /// <inheritdoc />
    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_fadeLeft > 0f)
        {
            _fadeLeft = Mathf.Max(0f, _fadeLeft - dt);
            float alpha = _fadeLeft / FadeDuration;
            Modulate = new Color(1f, 1f, 1f, alpha * alpha);
            if (_fadeLeft <= 0f)
            {
                RequestFinish();
            }
        }
        else
        {
            int direction = Scale.X > 0f ? -1 : 1;
            float next = _speed + Acceleration * dt;
            if (direction > 0)
            {
                next = Mathf.Min(next, RightSpeedLimit);
            }

            float distance = (_speed + next) * .5f * dt;
            Position += Vector2.Right * direction * distance;
            _distance += distance;
            _speed = next;
            if (_distance >= MaxDistance)
            {
                RequestFinish();
            }

            foreach (Area2D area in _cancelArea.GetOverlappingAreas())
            {
                OnPlayerAttackEntered(area);
            }
        }
        base._PhysicsProcess(delta);
    }

    /// <summary>
    /// 玩家攻击碰到弹体时清除伤害，只保留原地消散。
    /// </summary>
    /// <param name="area">进入取消区的玩家攻击区域。</param>
    private void OnPlayerAttackEntered(Area2D area)
    {
        if (_fadeLeft > 0f)
        {
            return;
        }

        StopAttack();
        _fadeLeft = FadeDuration;
    }
}
