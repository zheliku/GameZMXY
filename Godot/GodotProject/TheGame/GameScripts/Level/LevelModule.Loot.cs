using System;
using System.Collections.Generic;
using GameConfig.Monster;
using GameConfig.Equipment;
using GameConfig.Loot;
using GameLogic.Equipment;
using GodotGameFramework;
using GameConfig.Constant;
using GameLogic.Drops;
using Godot;
using GodotGameFramework.NodePool;
using GameLogic.UI;

namespace GameLogic.Level;

/// <summary>关卡拥有的飞行球和地面物品；拾取才入账，死亡、放弃和卸载时回收未领取物。</summary>
public partial class LevelModule
{
    private readonly HashSet<GroundItemNode> m_GroundItems = new(); // 本关地面物品；未拾取不会阻止出口。
    private readonly HashSet<LootOrbNode> m_LootOrbs = new(); // 仅本关当前在场的球，不用全池清理影响其他作用域。
    private readonly HashSet<RecoveryPopNode> m_RecoveryPops = new(); // 本关拾取恢复提示，关卡退出时统一回收。
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
        if (Outcome != LevelOutcome.Running || !m_Player.IsAlive) return;
        m_DefeatedMonsterCount++;
        AwardExperience(experienceReward);
        var config = ConfigSystem.Instance.Tables.TbDropConfig.Data;
        try
        {
            var tables = ConfigSystem.Instance.Tables;
            var selection = LootHelper.RollGround(tables.TbPickupConfig.DataList, monster.EquipmentDropChance,
                tables.TbEquipmentDropPoolConfig.GetOrDefault(equipmentPoolId), m_LootRandom.Randf);
            if (selection.Pickup != PickupId.None)
                ShowGroundItem(position + new Vector2(-18, 0), tables.TbPickupConfig.Get(selection.Pickup), null);
            if (selection.Equipment != EquipmentId.None)
                ShowGroundItem(position + new Vector2(18, 0), null,
                    m_Equipment.Roll(Guid.NewGuid(), selection.Equipment, m_LootRandom.Randf));
            foreach (LootReward reward in LootHelper.Roll(monster, config, m_LootRandom.Randf))
            {
                var orb = NodePool.Instance.Get<LootOrbNode>(ResourcesCollectionConstant.Entitys_LootOrb, this)
                          ?? throw new InvalidOperationException("飞行掉落节点池未注册或资源无法显示。");
                m_LootOrbs.Add(orb);
                Vector2 offset = new(m_LootRandom.RandfRange(-config.ScatterRadius, config.ScatterRadius),
                    m_LootRandom.RandfRange(-config.ScatterRadius, config.ScatterRadius));
                orb.Show(position + offset, m_Player, reward, config,
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
    /// <param name="pickup">恢复品或空。</param>
    /// <param name="equipment">装备或空。</param>
    private void ShowGroundItem(Vector2 position, PickupConfig pickup, EquipmentInstance equipment)
    {
        var tables = ConfigSystem.Instance.Tables;
        var definition = equipment == null ? null : tables.TbEquipmentConfig.Get(equipment.EquipmentId);
        var texture = GF.Resource.LoadAsset<Texture2D>(pickup?.IconPath ?? definition.GroundIconPath)
                      ?? throw new InvalidOperationException("地面物品图标加载失败。");
        var item = NodePool.Instance.Get<GroundItemNode>(ResourcesCollectionConstant.Entitys_ground_item, this)
                   ?? throw new InvalidOperationException("地面物品节点池未注册。");
        m_GroundItems.Add(item);
        item.Show(position, m_Player, tables.TbDropConfig.Data, pickup, equipment, texture,
            TryCollectGroundItem, OnGroundItemFinished);
    }

    /// <summary>接触时立即恢复或领取装备；满包保留地面物品，结局后拒绝入账。</summary>
    /// <param name="pickup">接触的恢复品。</param>
    /// <param name="equipment">接触的实际装备。</param>
    /// <returns>是否完成领取。</returns>
    private bool TryCollectGroundItem(PickupConfig pickup, EquipmentInstance equipment)
    {
        if (Outcome != LevelOutcome.Running || !m_Player.IsAlive) return false;
        if (equipment != null) return m_Profile.Inventory.TryAdd(equipment);
        if (pickup.Effect == PickupEffect.Health) m_Player.Vitals.Heal(pickup.RestoreAmount);
        else m_Player.Vitals.RestoreMp(pickup.RestoreAmount);
        ShowRecovery(pickup.RestoreAmount, pickup.Effect == PickupEffect.Mana);
        return true;
    }

    /// <summary>在英雄头顶显示恢复量；只由实际领取路径调用。</summary>
    /// <param name="amount">恢复品或蓝球的配置量。</param>
    /// <param name="mana">蓝色魔法恢复，false 为绿色生命恢复。</param>
    private void ShowRecovery(int amount, bool mana)
    {
        if (amount <= 0) return;
        var pop = NodePool.Instance.Get<RecoveryPopNode>(ResourcesCollectionConstant.Effects_recovery_number, this)
                  ?? throw new InvalidOperationException("恢复数字节点池未注册。");
        m_RecoveryPops.Add(pop);
        pop.Show(m_Player.GlobalPosition, amount, mana, OnRecoveryFinished);
    }

    /// <summary>恢复提示结束，只归还本关仍拥有的实例。</summary>
    /// <param name="pop">结束显示的节点。</param>
    private void OnRecoveryFinished(RecoveryPopNode pop)
    {
        if (m_RecoveryPops.Remove(pop)) NodePool.Instance.Release(pop);
    }

    /// <summary>拾取或过期后只回收本关拥有的实例。</summary>
    /// <param name="item">已经停止处理的节点。</param>
    private void OnGroundItemFinished(GroundItemNode item)
    {
        if (m_GroundItems.Remove(item)) NodePool.Instance.Release(item);
    }

    /// <summary>只接受本关仍拥有的球，先归还实例，再在一次接触中发放对应奖励。</summary>
    /// <param name="orb">结束飞行的池实例。</param>
    /// <param name="reward">本次收益快照。</param>
    /// <param name="collected">是否已实际接触活着的英雄。</param>
    private void OnLootFinished(LootOrbNode orb, LootReward reward, bool collected)
    {
        if (!m_LootOrbs.Remove(orb)) return;
        NodePool.Instance.Release(orb);
        if (collected && Outcome == LevelOutcome.Running && m_Player.IsAlive)
        {
            switch (reward.Kind)
            {
                case LootKind.Soul: m_Profile.Wallet.AddSoul(reward.Amount); break;
                case LootKind.Musou: m_Player.Musou.AddFromPickup(reward.Amount); break;
                case LootKind.Mana:
                    int amount = LootHelper.ManaAmount(m_Player.Vitals.MaxMp,
                        ConfigSystem.Instance.Tables.TbDropConfig.Data);
                    m_Player.Vitals.RestoreMp(amount);
                    ShowRecovery(amount, true);
                    break;
            }
        }

        if (m_ExitRequested && m_LootOrbs.Count == 0 && Outcome == LevelOutcome.Running) OnExitEntered();
    }

    /// <summary>逆序结束关卡时清除本关在场奖励；回收不触发拾取，不写档案。</summary>
    private void ClearLoot()
    {
        foreach (LootOrbNode orb in m_LootOrbs) NodePool.Instance.Release(orb);
        m_LootOrbs.Clear();
        foreach (var item in m_GroundItems) NodePool.Instance.Release(item);
        m_GroundItems.Clear();
        foreach (var pop in m_RecoveryPops) NodePool.Instance.Release(pop);
        m_RecoveryPops.Clear();
        m_ExitRequested = false;
    }
}
