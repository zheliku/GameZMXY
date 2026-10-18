using GodotGameFramework;
using System;
using GameConfig.Loot;
using Godot;
using GodotGameFramework.NodePool;

namespace GameLogic.Drops;

/// <summary>GGF 节点池中的飞行奖励：短暂停留、上浮、加速追踪，接触后交给关卡结算。</summary>
public partial class LootOrbNode : AnimatedSprite2D, IPoolable
{
    [Export] private float m_ScatterRadius = 15f; // 出生位置的可见散布半径，像素。
    [Export] private float m_Delay = 20f / 24f; // 起飞前等待的游戏秒数。
    [Export] private float m_RiseDuration = 1f; // 上浮动画的游戏秒数。
    [Export] private Vector2 m_RiseDistance = new(30, 50); // 上浮高度的随机范围，像素。
    [Export] private float m_InitialSpeed = 120f; // 追踪开始时的速度，像素/秒。
    [Export] private float m_Acceleration = 1152f; // 追踪加速度，像素/秒²。
    [Export] private float m_MaxSpeed = 480f; // 追踪最高速度，像素/秒。
    [Export] private float m_PickupRadius = 10f; // 可视接触距离，像素。
    private readonly RandomNumberGenerator m_Random = new(); // 飞行表现独有的散布采样器，不影响掉落概率。
    private LootReward m_Reward; // 接触之前尚未入账的奖励。
    private Action<LootOrbNode, LootReward> m_Finished; // 由关卡拥有的完成回调；先清理再调用。
    private Vector2 m_Start; // 上浮起点。
    private float m_Age; // 本次显示经过的游戏秒数。
    private float m_Rise; // 本颗球的随机上升高度。
    private float m_Speed; // 当前追踪速度，像素/秒。

    /// <summary>池取出时停止上次驱动，等待所属关卡提供本次参数。</summary>
    public void OnGet()
    {
        SetProcess(false);
        m_Age = 0;
        Modulate = Colors.White;
    }

    /// <summary>池归还时清除奖励和回调，不让复用实例向旧会话入账。</summary>
    public void OnRelease()
    {
        SetProcess(false);
        Stop();
        m_Finished = null;
        m_Reward = default;
    }

    /// <summary>开始一次飞行；回调负责回池，只有接触当前存活英雄才通知领取。</summary>
    /// <param name="position">怪物死亡位置附近的出生点。</param>
    /// <param name="reward">尚未领取的奖励。</param>
    /// <param name="finished">完成回调；只在当前显示期间调用一次。</param>
    public void Show(Vector2 position, LootReward reward,
        Action<LootOrbNode, LootReward> finished)
    {
        m_Reward = reward;
        m_Finished = finished;
        m_Random.Randomize();
        m_Start = GlobalPosition = position + new Vector2(
            m_Random.RandfRange(-m_ScatterRadius, m_ScatterRadius),
            m_Random.RandfRange(-m_ScatterRadius, m_ScatterRadius));
        m_Rise = m_Random.RandfRange(m_RiseDistance.X, m_RiseDistance.Y);
        m_Age = 0;
        m_Speed = m_InitialSpeed;
        Frame = 0;
        Play(reward.Kind switch
        {
            LootKind.Soul => LootAnims.Soul,
            LootKind.Musou => LootAnims.Musou,
            LootKind.Mana => LootAnims.Mana,
            _ => throw new ArgumentOutOfRangeException(nameof(reward))
        });
        SetProcess(false);
    }

    /// <summary>由本关控制器提供当前目标坐标并推进飞行；节点不持有英雄。</summary>
    /// <param name="delta">本帧游戏秒数；暂停时不推进或结算。</param>
    /// <param name="target">本帧存活英雄的受击盒中心。</param>
    public void Tick(float delta, Vector2 target)
    {
        if (delta <= 0 || m_Finished == null)
            return;
        float dt = delta;
        m_Age += dt;
        if (m_Age < m_Delay)
            return;
        if (m_Age < m_Delay + m_RiseDuration)
        {
            float t = (m_Age - m_Delay) / m_RiseDuration;
            GlobalPosition = m_Start + Vector2.Up * m_Rise * (1f - (1f - t) * (1f - t));
            return;
        }

        // 身体受击盒中心就是可视接触点，英雄跳跃或移动时持续跟随。
        m_Speed = Math.Min(m_MaxSpeed, m_Speed + m_Acceleration * dt);
        float step = m_Speed * dt;
        if (GlobalPosition.DistanceTo(target) <= m_PickupRadius + step)
        {
            GlobalPosition = target;
            Finish();
            return;
        }
        GlobalPosition = GlobalPosition.MoveToward(target, step);
    }

    /// <summary>先停止和清除完成回调，再通知关卡，防止结算重入与重复回收。</summary>
    private void Finish()
    {
        SetProcess(false);
        var finished = m_Finished;
        m_Finished = null;
        finished?.Invoke(this, m_Reward);
    }
}
