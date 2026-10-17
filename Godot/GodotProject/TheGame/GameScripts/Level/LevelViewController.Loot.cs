using GameLogic.Battle;
using GameLogic.Architecture;
using GameLogic.Config;
using System;
using System.Collections.Generic;
using GameConfig.Monster;
using GameConfig.Equipment;
using GameConfig.Loot;
using GameLogic.Equipment;
using GameConfig.Constant;
using GameLogic.Drops;
using GameLogic.Heroes;
using Godot;
using GodotGameFramework.NodePool;
using GameLogic.FloatingText;

namespace GameLogic.Level;

/// <summary>关卡拥有的飞行球和地面物品；拾取才入账，死亡、放弃和卸载时回收未领取物。</summary>
public partial class LevelViewController
{
    private readonly HashSet<GroundItemNode> m_GroundItems = new(); // 本关地面物品；未拾取不会阻止出口。
    private readonly HashSet<LootOrbNode> m_LootOrbs = new(); // 仅本关当前在场的球，不用全池清理影响其他作用域。
    private readonly List<LootOrbNode> m_LootTickBuffer = new(); // 帧内快照，允许接触回调回池或结束整局。
    private readonly List<GroundItemNode> m_GroundTickBuffer = new(); // 接触检查期间允许关卡结束并清空掉落集合。
    private readonly RandomNumberGenerator m_LootRandom = new(); // 本关掉落采样器；纯规则通过委托取值。
    private bool m_ExitRequested; // W 已确认，等待尚在飞行的球接触后提交检查点。

    /// <summary>复制后的死亡数据在当前关卡生成奖励，不保留池化事件参数。</summary>
    /// <param name="monster">基础掉落配置。</param>
    /// <param name="position">怪物死亡位置。</param>
    /// <param name="experienceReward">本次配方确定的经验。</param>
    /// <param name="equipmentPoolId">本次配方确定的装备池。</param>
    private void OnMonsterDefeated(MonsterConfig monster, Vector2 position, int experienceReward,
        EquipmentDropPoolId equipmentPoolId)
    {
        if (LevelModel.Outcome.Value != LevelOutcome.Running || !m_Player.IsAlive)
            return;
        if (this.GetSystem<LevelSystem>().RecordDefeat(experienceReward))
        {
            m_Player.PlayLevelUp();
        }

        var config = this.GetModel<ConfigurationModel>().Tables.TbDropConfig.Data;
        try
        {
            var selection = this.GetSystem<LootSystem>()
                .RollGround(monster.EquipmentDropChance, equipmentPoolId, m_LootRandom.Randf);
            if (selection.Pickup != PickupId.None)
                ShowGroundItem(position + new Vector2(-18, 0), selection.Pickup, null);
            if (selection.Equipment != EquipmentId.None)
                ShowGroundItem(position + new Vector2(18, 0), PickupId.None,
                    this.GetSystem<EquipmentSystem>().Roll(Guid.NewGuid(), selection.Equipment, m_LootRandom.Randf));
            foreach (LootReward reward in this.GetSystem<LootSystem>().Roll(monster, m_LootRandom.Randf))
            {
                var orb = NodePool.Instance.Get<LootOrbNode>(ResourcesCollectionConstant.Entitys_LootOrb, this)
                          ?? throw new InvalidOperationException("飞行掉落节点池未注册或资源无法显示。");
                m_LootOrbs.Add(orb);
                Vector2 offset = new(m_LootRandom.RandfRange(-config.ScatterRadius, config.ScatterRadius),
                    m_LootRandom.RandfRange(-config.ScatterRadius, config.ScatterRadius));
                orb.Show(position + offset, reward,
                    m_LootRandom.RandfRange(config.RiseDistance.X, config.RiseDistance.Y), OnLootFinished);
            }
        }
        catch (Exception error)
        {
            OnSpawnFailed(error);
        }
    }

    /// <summary>创建关卡拥有的地面物品，实例属性在落地之前已经确定。</summary>
    /// <param name="position">死亡位置附近。</param>
    /// <param name="pickupId">恢复品或空。</param>
    /// <param name="data">装备或空。</param>
    private void ShowGroundItem(Vector2 position, PickupId pickupId, EquipmentData data)
    {
        var item = NodePool.Instance.Get<GroundItemNode>(ResourcesCollectionConstant.Entitys_ground_item, this)
                   ?? throw new InvalidOperationException("地面物品节点池未注册。");
        m_GroundItems.Add(item);
        item.Show(position, pickupId, data,
            TryCollectGroundItem, OnGroundItemFinished);
    }

    /// <summary>接触时立即恢复或领取装备；满包保留地面物品，结局后拒绝入账。</summary>
    /// <param name="hero">物品检测到的实际接触者；必须属于本会话。</param>
    /// <param name="pickupId">接触的恢复品。</param>
    /// <param name="data">接触的实际装备。</param>
    /// <returns>是否完成领取。</returns>
    private bool TryCollectGroundItem(AbstractHeroEntity hero, PickupId pickupId, EquipmentData data)
    {
        if (LevelModel.Outcome.Value != LevelOutcome.Running || !ReferenceEquals(hero, m_Player) || !hero.IsAlive)
            return false;
        if (data != null)
            return this.GetSystem<LevelSystem>().TryCollectEquipment(data);
        var pickup = this.GetModel<ConfigurationModel>().Tables.TbPickupConfig.Get(pickupId);
        if (pickup.Effect == PickupEffect.Health)
            this.GetSystem<HeroSystem>().Heal(pickup.RestoreAmount);
        else
            this.GetSystem<HeroSystem>().RestoreMp(pickup.RestoreAmount);
        m_FloatingText.ShowRecovery(m_Player.GlobalPosition, pickup.RestoreAmount,
            pickup.Effect == PickupEffect.Mana ? RecoveryKind.Mana : RecoveryKind.Health);
        return true;
    }

    /// <summary>拾取或过期后只回收本关拥有的实例。</summary>
    /// <param name="item">已经停止处理的节点。</param>
    private void OnGroundItemFinished(GroundItemNode item)
    {
        if (m_GroundItems.Remove(item))
            NodePool.Instance.Release(item);
    }

    /// <summary>只接受本关仍拥有的球，先归还实例，再在一次接触中发放对应奖励。</summary>
    /// <param name="orb">结束飞行的池实例。</param>
    /// <param name="reward">本次收益快照。</param>
    private void OnLootFinished(LootOrbNode orb, LootReward reward)
    {
        if (!m_LootOrbs.Remove(orb))
            return;
        NodePool.Instance.Release(orb);
        if (LevelModel.Outcome.Value == LevelOutcome.Running && m_Player.IsAlive)
        {
            switch (reward.Kind)
            {
                case LootKind.Soul:
                    this.GetSystem<LevelSystem>().CollectSoul(reward.Amount);
                    break;
                case LootKind.Musou:
                    this.GetSystem<HeroSystem>().AddWsFromPickup(reward.Amount);
                    break;
                case LootKind.Mana:
                    int amount = this.GetSystem<LootSystem>().ManaAmount(this.GetModel<HeroModel>().MaxMp.Value);
                    this.GetSystem<HeroSystem>().RestoreMp(amount);
                    break;
            }
        }

        if (m_ExitRequested && m_LootOrbs.Count == 0 && LevelModel.Outcome.Value == LevelOutcome.Running)
            OnExitEntered();
    }

    /// <summary>仅由本关所有者取英雄坐标，逐球推进；接触回调可以清空集合。</summary>
    /// <param name="delta">本帧游戏秒数。</param>
    private void TickLoot(float delta)
    {
        if (delta <= 0 || !IsInstanceValid(m_Player) || !m_Player.IsAlive)
            return;
        m_GroundTickBuffer.AddRange(m_GroundItems);
        try
        {
            foreach (var item in m_GroundTickBuffer)
                if (m_GroundItems.Contains(item))
                    item.TryCollect(m_Player);
        }
        finally
        {
            m_GroundTickBuffer.Clear();
        }
        if (m_LootOrbs.Count == 0)
            return;
        Vector2 target = m_Player.HurtBox.GetGlobalBounds().GetCenter();
        m_LootTickBuffer.AddRange(m_LootOrbs);
        try
        {
            foreach (var orb in m_LootTickBuffer)
            {
                if (m_LootOrbs.Contains(orb))
                    orb.Tick(delta, target);
            }
        }
        finally
        {
            m_LootTickBuffer.Clear();
        }
    }

    /// <summary>逆序结束关卡时清除本关在场奖励；回收不触发拾取，不写档案。</summary>
    private void ClearLoot()
    {
        foreach (LootOrbNode orb in m_LootOrbs)
            NodePool.Instance.Release(orb);
        m_LootOrbs.Clear();
        foreach (var item in m_GroundItems)
            NodePool.Instance.Release(item);
        m_GroundItems.Clear();
        m_ExitRequested = false;
    }
}
