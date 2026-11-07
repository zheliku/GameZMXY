using System;
using GameConfig.Loot;
using Godot;
using GodotGameFramework;
using GodotGameFramework.NodePool;

namespace GameLogic.Drops;

/// <summary>
/// GGF 节点池中的飞行奖励：短暂停留、上浮、加速追踪，接触后交给关卡结算。
/// </summary>
public partial class LootOrbNode : AnimatedSprite2D, IPoolable
{
    [Export] private float _scatterRadius = 15f; // 出生位置的可见散布半径，像素。
    [Export] private float _delay = 20f / 24f; // 起飞前等待的游戏秒数。
    [Export] private float _riseDuration = 1f; // 上浮动画的游戏秒数。
    [Export] private Vector2 _riseDistance = new(30, 50); // 上浮高度的随机范围，像素。
    [Export] private float _initialSpeed = 120f; // 追踪开始时的速度，像素/秒。
    [Export] private float _acceleration = 1152f; // 追踪加速度，像素/秒²。
    [Export] private float _maxSpeed = 480f; // 追踪最高速度，像素/秒。
    [Export] private float _pickupRadius = 10f; // 可视接触距离，像素。
    private readonly RandomNumberGenerator _random = new(); // 飞行表现独有的散布采样器，不影响掉落概率。
    private LootReward _reward; // 接触之前尚未入账的奖励。
    private Action<LootOrbNode, LootReward> _finished; // 由关卡拥有的完成回调；先清理再调用。
    private Vector2 _start; // 上浮起点。
    private float _age; // 本次显示经过的游戏秒数。
    private float _rise; // 本颗球的随机上升高度。
    private float _speed; // 当前追踪速度，像素/秒。

    /// <summary>
    /// 池取出时停止上次驱动，等待所属关卡提供本次参数。
    /// </summary>
    public void OnGet()
    {
        SetProcess(false);
        _age = 0;
        Modulate = Colors.White;
    }

    /// <summary>
    /// 池归还时清除奖励和回调，不让复用实例向旧会话入账。
    /// </summary>
    public void OnRelease()
    {
        SetProcess(false);
        Stop();
        _finished = null;
        _reward = default;
    }

    /// <summary>
    /// 开始一次飞行；回调负责回池，只有接触当前存活英雄才通知领取。
    /// </summary>
    /// <param name="position">怪物死亡位置附近的出生点。</param>
    /// <param name="reward">尚未领取的奖励。</param>
    /// <param name="finished">完成回调；只在当前显示期间调用一次。</param>
    public void Show(Vector2 position, LootReward reward,
        Action<LootOrbNode, LootReward> finished)
    {
        _reward = reward;
        _finished = finished;
        _random.Randomize();
        _start = GlobalPosition = position + new Vector2(
            _random.RandfRange(-_scatterRadius, _scatterRadius),
            _random.RandfRange(-_scatterRadius, _scatterRadius));
        _rise = _random.RandfRange(_riseDistance.X, _riseDistance.Y);
        _age = 0;
        _speed = _initialSpeed;
        Frame = 0;
        Play(reward.Kind switch
        {
            LootKind.Soul => LootAnims.Soul,
            LootKind.Musou => LootAnims.Musou,
            LootKind.Mana => LootAnims.Mana,
            LootKind.Health => LootAnims.Health,
            _ => throw new ArgumentOutOfRangeException(nameof(reward))
        });
        SetProcess(false);
    }

    /// <summary>
    /// 由本关控制器提供当前目标坐标并推进飞行；节点不持有英雄。
    /// </summary>
    /// <param name="delta">本帧游戏秒数；暂停时不推进或结算。</param>
    /// <param name="target">本帧存活英雄的受击盒中心。</param>
    public void Tick(float delta, Vector2 target)
    {
        if (delta <= 0 || _finished == null)
        {
            return;
        }

        float dt = delta;
        _age += dt;
        if (_age < _delay)
        {
            return;
        }

        if (_age < _delay + _riseDuration)
        {
            float t = (_age - _delay) / _riseDuration;
            GlobalPosition = _start + Vector2.Up * _rise * (1f - (1f - t) * (1f - t));
            return;
        }

        // 身体受击盒中心就是可视接触点，英雄跳跃或移动时持续跟随。
        _speed = Math.Min(_maxSpeed, _speed + _acceleration * dt);
        float step = _speed * dt;
        if (GlobalPosition.DistanceTo(target) <= _pickupRadius + step)
        {
            GlobalPosition = target;
            Finish();
            return;
        }
        GlobalPosition = GlobalPosition.MoveToward(target, step);
    }

    /// <summary>
    /// 先停止和清除完成回调，再通知关卡，防止结算重入与重复回收。
    /// </summary>
    private void Finish()
    {
        SetProcess(false);
        Action<LootOrbNode, LootReward> finished = _finished;
        _finished = null;
        finished?.Invoke(this, _reward);
    }
}
