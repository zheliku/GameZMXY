using System;
using GameConfig.Loot;
using GameLogic.Heroes;
using Godot;
using GodotGameFramework.NodePool;

namespace GameLogic.Drops;

/// <summary>GGF 节点池中的飞行奖励：短暂停留、上浮、加速追踪，接触后交给关卡结算。</summary>
public partial class LootOrbNode : AnimatedSprite2D, IPoolable
{
    private HeroEntity m_Target; // 本次显示的英雄，不跨关卡持有。
    private DropConfig m_Config; // 已校验的共享飞行参数。
    private LootReward m_Reward; // 接触之前尚未入账的奖励。
    private Action<LootOrbNode, LootReward, bool> m_Finished; // 由关卡拥有的完成回调；先清理再调用。
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

    /// <summary>池归还时清除目标和回调，不让复用实例向旧会话入账。</summary>
    public void OnRelease()
    {
        SetProcess(false);
        Stop();
        m_Target = null;
        m_Config = null;
        m_Finished = null;
        m_Reward = default;
    }

    /// <summary>开始一次飞行；回调负责回池，只有接触活着的英雄时 collected 才为 true。</summary>
    /// <param name="position">怪物死亡位置附近的出生点。</param>
    /// <param name="target">本关出战英雄。</param>
    /// <param name="reward">尚未领取的奖励。</param>
    /// <param name="config">飞行配置。</param>
    /// <param name="rise">本颗球的上升高度。</param>
    /// <param name="finished">完成回调；只在当前显示期间调用一次。</param>
    public void Show(Vector2 position, HeroEntity target, LootReward reward, DropConfig config, float rise,
        Action<LootOrbNode, LootReward, bool> finished)
    {
        m_Target = target;
        m_Config = config;
        m_Reward = reward;
        m_Finished = finished;
        m_Start = GlobalPosition = position;
        m_Rise = rise;
        m_Age = 0;
        m_Speed = config.InitialSpeed;
        Frame = 0;
        Play(reward.Kind switch
        {
            LootKind.Soul => LootAnims.Soul,
            LootKind.Musou => LootAnims.Musou,
            LootKind.Mana => LootAnims.Mana,
            _ => throw new ArgumentOutOfRangeException(nameof(reward))
        });
        SetProcess(true);
    }

    /// <summary>使用游戏时间推进；暂停时不移动，跨帧距离检测防止高速越过英雄。</summary>
    /// <param name="delta">游戏帧秒数。</param>
    public override void _Process(double delta)
    {
        if (m_Target is not { IsAlive: true })
        {
            Finish(false);
            return;
        }
        float dt = (float)delta;
        m_Age += dt;
        if (m_Age < m_Config.Delay)
            return;
        if (m_Age < m_Config.Delay + m_Config.RiseDuration)
        {
            float t = (m_Age - m_Config.Delay) / m_Config.RiseDuration;
            GlobalPosition = m_Start + Vector2.Up * m_Rise * (1f - (1f - t) * (1f - t));
            return;
        }

        // 身体受击盒中心就是可视接触点，英雄跳跃或移动时持续跟随。
        Vector2 target = m_Target.HurtBox.GetGlobalBounds().GetCenter();
        m_Speed = Math.Min(m_Config.MaxSpeed, m_Speed + m_Config.Acceleration * dt);
        float step = m_Speed * dt;
        if (GlobalPosition.DistanceTo(target) <= m_Config.PickupRadius + step)
        {
            GlobalPosition = target;
            Finish(true);
            return;
        }
        GlobalPosition = GlobalPosition.MoveToward(target, step);
    }

    /// <summary>先停止和清除完成回调，再通知关卡，防止结算重入与重复回收。</summary>
    /// <param name="collected">是否已接触活着的目标。</param>
    private void Finish(bool collected)
    {
        SetProcess(false);
        var finished = m_Finished;
        m_Finished = null;
        finished?.Invoke(this, m_Reward, collected);
    }
}
