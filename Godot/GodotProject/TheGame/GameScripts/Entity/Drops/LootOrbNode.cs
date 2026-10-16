using GodotGameFramework;
using GameLogic.Config;
using GameLogic.Architecture;
using System;
using GameConfig.Loot;
using Godot;
using GodotGameFramework.NodePool;

namespace GameLogic.Drops;

/// <summary>GGF 节点池中的飞行奖励：短暂停留、上浮、加速追踪，接触后交给关卡结算。</summary>
public partial class LootOrbNode : AnimatedSprite2D, IPoolable, IController
{
    private LootReward m_Reward; // 接触之前尚未入账的奖励。
    private Action<LootOrbNode, LootReward> m_Finished; // 由关卡拥有的完成回调；先清理再调用。
    private Vector2 m_Start; // 上浮起点。
    private float m_Age; // 本次显示经过的游戏秒数。
    private float m_Rise; // 本颗球的随机上升高度。
    private float m_Speed; // 当前追踪速度，像素/秒。

    /// <summary>借用当前游戏流程的架构入口。</summary>
    /// <returns>所属流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

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
    /// <param name="rise">本颗球的上升高度。</param>
    /// <param name="finished">完成回调；只在当前显示期间调用一次。</param>
    public void Show(Vector2 position, LootReward reward, float rise,
        Action<LootOrbNode, LootReward> finished)
    {
        m_Reward = reward;
        m_Finished = finished;
        m_Start = GlobalPosition = position;
        m_Rise = rise;
        m_Age = 0;
        m_Speed = this.GetModel<ConfigurationModel>().Tables.TbDropConfig.Data.InitialSpeed;
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
        var config = this.GetModel<ConfigurationModel>().Tables.TbDropConfig.Data;
        float dt = delta;
        m_Age += dt;
        if (m_Age < config.Delay)
            return;
        if (m_Age < config.Delay + config.RiseDuration)
        {
            float t = (m_Age - config.Delay) / config.RiseDuration;
            GlobalPosition = m_Start + Vector2.Up * m_Rise * (1f - (1f - t) * (1f - t));
            return;
        }

        // 身体受击盒中心就是可视接触点，英雄跳跃或移动时持续跟随。
        m_Speed = Math.Min(config.MaxSpeed, m_Speed + config.Acceleration * dt);
        float step = m_Speed * dt;
        if (GlobalPosition.DistanceTo(target) <= config.PickupRadius + step)
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
