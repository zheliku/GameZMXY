using System;
using GameConfig.Loot;
using GameLogic.Equipment;
using GameLogic.Heroes;
using Godot;
using GodotGameFramework.NodePool;

namespace GameLogic.Drops;

/// <summary>地面恢复品或装备：受重力落地、接触拾取、按游戏秒数过期；关卡拥有回收与收益。</summary>
public partial class GroundItemNode : CharacterBody2D, IPoolable
{
    [Export] private Sprite2D m_Icon; // 本次掉落的图标，不在节点内查装备配置。
    private HeroEntity m_Target; // 当前关卡活着的出战英雄。
    private DropConfig m_Config; // 地面存在时间、重力和接触范围。
    private PickupConfig m_Pickup; // 恢复品定义，与装备互斥。
    private EquipmentInstance m_Equipment; // 已抽取实际属性的地面装备。
    private Func<PickupConfig, EquipmentInstance, bool> m_Collect; // 关卡结算；满包返回 false，继续留地。
    private Action<GroundItemNode> m_Finished; // 关卡移除所有权并回池。
    private float m_Age; // 已存在的游戏秒数，暂停时不增加。

    /// <summary>取池时等待本次关卡参数。</summary>
    public void OnGet()
    {
        SetPhysicsProcess(false);
        Velocity = Vector2.Zero;
        m_Age = 0;
    }

    /// <summary>归还时清除全部借用引用与视觉，防止复用重复入账。</summary>
    public void OnRelease()
    {
        SetPhysicsProcess(false);
        m_Target = null;
        m_Config = null;
        m_Pickup = null;
        m_Equipment = null;
        m_Collect = null;
        m_Finished = null;
        m_Icon.Texture = null;
        Velocity = Vector2.Zero;
    }

    /// <summary>显示已确定的恢复品或装备；图标由关卡提供。</summary>
    /// <param name="position">死亡位置附近。</param>
    /// <param name="target">本关英雄。</param>
    /// <param name="config">已校验的地面参数。</param>
    /// <param name="pickup">恢复品，装备时为空。</param>
    /// <param name="equipment">实际装备，恢复品时为空。</param>
    /// <param name="texture">通过 GGF 加载的图标。</param>
    /// <param name="collect">结算回调；满包时拒绝领取。</param>
    /// <param name="finished">过期或拾取后的回收回调。</param>
    public void Show(Vector2 position, HeroEntity target, DropConfig config, PickupConfig pickup,
        EquipmentInstance equipment, Texture2D texture,
        Func<PickupConfig, EquipmentInstance, bool> collect, Action<GroundItemNode> finished)
    {
        if ((pickup == null) == (equipment == null)) throw new ArgumentException("一次地面掉落必须且只能包含一种物品。");
        GlobalPosition = position;
        Velocity = Vector2.Zero;
        m_Age = 0;
        m_Target = target;
        m_Config = config;
        m_Pickup = pickup;
        m_Equipment = equipment;
        m_Icon.Texture = texture;
        m_Icon.Scale = Vector2.One * (pickup?.Id == PickupId.LargePeach ? .9f : .7f);
        m_Collect = collect;
        m_Finished = finished;
        SetPhysicsProcess(true);
    }

    /// <summary>以秒推进落地和过期；只有接触英雄才请求入账，满包不会销毁或重抽。</summary>
    /// <param name="delta">游戏秒数。</param>
    public override void _PhysicsProcess(double delta)
    {
        if (delta <= 0) return;
        if (m_Target is not { IsAlive: true })
        {
            Finish();
            return;
        }

        float dt = (float)delta;
        m_Age += dt;
        if (m_Age >= m_Config.GroundLifetime)
        {
            Finish();
            return;
        }

        Velocity += Vector2.Down * m_Config.GroundGravity * dt;
        MoveAndSlide();
        if (m_Target.HurtBox.GetGlobalBounds().Grow(m_Config.GroundPickupRadius)
                .HasPoint(GlobalPosition + Vector2.Up * 8)
            && m_Collect(m_Pickup, m_Equipment)) Finish();
    }

    /// <summary>先停处理和回调，再交给关卡回收，保证只回收一次。</summary>
    private void Finish()
    {
        SetPhysicsProcess(false);
        var finished = m_Finished;
        m_Finished = null;
        m_Collect = null;
        finished?.Invoke(this);
    }
}
