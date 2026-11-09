using System;
using System.Collections.Generic;
using GameConfig.Constant;
using GameConfig.Equipment;
using GameConfig.Loot;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Config;
using GameLogic.Drops;
using GameLogic.Equipment;
using GameLogic.Event.Combat;
using GameLogic.FloatingText;
using GameLogic.Heroes;
using Godot;
using GodotGameFramework;
using GodotGameFramework.NodePool;

namespace GameLogic.Level;

/// <summary>
/// 关卡拥有的飞行球和地面物品；拾取才入账，死亡、放弃和卸载时回收未领取物。
/// </summary>
public partial class LevelViewController
{
    private readonly HashSet<GroundItemNode> _groundItems = new(); // 本关地面物品；未拾取不会阻止出口。
    private readonly HashSet<LootOrbNode> _lootOrbs = new(); // 仅本关当前在场的球，不用全池清理影响其他作用域。
    private readonly List<LootOrbNode> _lootTickBuffer = new(); // 帧内快照，允许接触回调回池或结束整局。
    private readonly List<GroundItemNode> _groundTickBuffer = new(); // 接触检查期间允许关卡结束并清空掉落集合。
    private readonly RandomNumberGenerator _lootRandom = new(); // 本关掉落采样器；纯规则通过委托取值。
    private bool _exitRequested; // W 已确认，等待尚在飞行的球接触后提交检查点。

    /// <summary>
    /// 同步消费死亡快照，在当前关卡生成奖励，不保留池化事件参数。
    /// </summary>
    /// <param name="data">仅在本次同步回调内有效的死亡奖励快照，不保存池化参数。</param>
    /// <param name="spawnPointId">实际死亡实体归属的配方生成点。</param>
    private void OnEnemyDefeated(EnemyDiedEventArgs data, string spawnPointId)
    {
        if (LevelModel.Outcome.Value != LevelOutcome.Running || !_player.IsAlive)
        {
            return;
        }

        // 当前会话只生成一个英雄；外部或已离场的来源不参与本局奖励。
        var killer = GF.Entity.GetEntity(data.KillerEntityId) as AbstractHeroEntity;
        if (!ReferenceEquals(killer, _player))
        {
            killer = null;
        }

        if (this.GetSystem<LevelSystem>().RecordDefeat(killer?.HeroId ?? 0, data.ExperienceReward))
        {
            killer.PlayLevelUp();
        }

        try
        {
            GroundLootSelection selection = this.GetSystem<LootSystem>()
                .RollGround(data.EquipmentDropChanceOverride ?? data.Config.EquipmentDropChance,
                    data.EquipmentDropPoolIdOverride ?? data.Config.EquipmentDropPoolId, _lootRandom.Randf);
            if (selection.Pickup != PickupId.None)
            {
                ShowGroundItem(data.Position + new Vector2(-18, 0), selection.Pickup, null);
            }

            if (selection.Equipment != EquipmentId.None)
            {
                ShowGroundItem(data.Position + new Vector2(18, 0), PickupId.None,
                                this.GetSystem<EquipmentSystem>().Roll(Guid.NewGuid(), selection.Equipment, _lootRandom.Randf));
            }

            // 无英雄归属时仍保留地面掉落，只跳过飞行球，与官方 dropAura 的早退边界一致。
            if (killer != null)
            {
                HeroState hero = this.GetModel<HeroModel>().GetState(killer.HeroId);
                foreach (LootReward reward in this.GetSystem<LootSystem>().Roll(data.Config,
                    data.SoulPerOrbOverride ?? data.Config.SoulPerOrb, hero.MaxHp.Value, hero.MaxMp.Value, _lootRandom.Randf))
                {
                    LootOrbNode orb = NodePool.Instance.RentNode<LootOrbNode>(ResourcesCollectionConstant.Drops_loot_orb, this)
                              ?? throw new InvalidOperationException("飞行掉落节点池未注册或资源无法显示。");
                    _lootOrbs.Add(orb);
                    orb.Show(data.Position, reward, OnLootFinished);
                }
            }
            // 先完成掉落登记，再允许玩家使用目标出口。
            if (data.EncounterComplete && this.GetSystem<LevelSystem>().TryOpenBatCaveExit(spawnPointId))
            {
                _gates.ReleaseRegion(LevelModel.Sequence.Current.StageOrder);
                _exit.Enable();
            }
        }
        catch (Exception error)
        {
            OnSpawnFailed(error);
        }
    }

    /// <summary>
    /// 创建关卡拥有的地面物品，实例属性在落地之前已经确定。
    /// </summary>
    /// <param name="position">死亡位置附近。</param>
    /// <param name="pickupId">恢复品或空。</param>
    /// <param name="data">装备或空。</param>
    private void ShowGroundItem(Vector2 position, PickupId pickupId, EquipmentData data)
    {
        GroundItemNode item = NodePool.Instance.RentNode<GroundItemNode>(ResourcesCollectionConstant.Drops_ground_item, this)
                   ?? throw new InvalidOperationException("地面物品节点池未注册。");
        _groundItems.Add(item);
        item.Show(position, pickupId, data,
            TryCollectGroundItem, OnGroundItemFinished);
    }

    /// <summary>
    /// 接触时立即恢复或领取装备；满包保留地面物品，结局后拒绝入账。
    /// </summary>
    /// <param name="hero">物品检测到的实际接触者；必须属于本会话。</param>
    /// <param name="pickupId">接触的恢复品。</param>
    /// <param name="data">接触的实际装备。</param>
    /// <returns>是否完成领取。</returns>
    private bool TryCollectGroundItem(AbstractHeroEntity hero, PickupId pickupId, EquipmentData data)
    {
        if (LevelModel.Outcome.Value != LevelOutcome.Running || !ReferenceEquals(hero, _player) || !hero.IsAlive)
        {
            return false;
        }

        if (data != null)
        {
            return this.GetSystem<LevelSystem>().TryCollectEquipment(_player.HeroId, data);
        }

        if (!this.GetSystem<LevelSystem>().TryCollectPickup(_player.HeroId, pickupId))
        {
            return false;
        }

        PickupConfig pickup = this.GetModel<ConfigurationModel>().Tables.TbPickupConfig.Get(pickupId);
        _floatingText.ShowRecovery(_player.GlobalPosition, pickup.RestoreAmount,
            pickup.Effect == PickupEffect.Mana ? RecoveryKind.Mana : RecoveryKind.Health);
        return true;
    }

    /// <summary>
    /// 显示本关英雄已完成的主动治疗，本体与分身复用同一恢复数字。
    /// </summary>
    /// <param name="amount">官方技能的恢复量，满血时也保留反馈。</param>
    private void OnHeroHealthRestored(int amount) => _floatingText.ShowRecovery(_player.GlobalPosition, amount, RecoveryKind.Health);

    /// <summary>
    /// 拾取或过期后只回收本关拥有的实例。
    /// </summary>
    /// <param name="item">已经停止处理的节点。</param>
    private void OnGroundItemFinished(GroundItemNode item)
    {
        if (_groundItems.Remove(item))
        {
            NodePool.Instance.Release(item);
        }
    }

    /// <summary>
    /// 只接受本关仍拥有的球，先归还实例，再在一次接触中发放对应奖励。
    /// </summary>
    /// <param name="orb">结束飞行的池实例。</param>
    /// <param name="reward">本次收益快照。</param>
    private void OnLootFinished(LootOrbNode orb, LootReward reward)
    {
        if (!_lootOrbs.Remove(orb))
        {
            return;
        }

        NodePool.Instance.Release(orb);
        this.GetSystem<LevelSystem>().CollectLoot(_player.HeroId, reward);

        if (_exitRequested && _lootOrbs.Count == 0 && LevelModel.Outcome.Value == LevelOutcome.Running)
        {
            OnExitEntered();
        }
    }

    /// <summary>
    /// 仅由本关所有者取英雄坐标，逐球推进；接触回调可以清空集合。
    /// </summary>
    /// <param name="delta">本帧游戏秒数。</param>
    private void TickLoot(float delta)
    {
        if (delta <= 0 || !IsInstanceValid(_player) || !_player.IsAlive)
        {
            return;
        }

        _groundTickBuffer.AddRange(_groundItems);
        try
        {
            foreach (GroundItemNode item in _groundTickBuffer)
            {
                if (_groundItems.Contains(item))
                {
                    item.TryCollect(_player);
                }
            }
        }
        finally
        {
            _groundTickBuffer.Clear();
        }
        if (_lootOrbs.Count == 0)
        {
            return;
        }

        Vector2 target = _player.HurtBox.GetGlobalBounds().GetCenter();
        _lootTickBuffer.AddRange(_lootOrbs);
        try
        {
            foreach (LootOrbNode orb in _lootTickBuffer)
            {
                if (_lootOrbs.Contains(orb))
                {
                    orb.Tick(delta, target);
                }
            }
        }
        finally
        {
            _lootTickBuffer.Clear();
        }
    }

    /// <summary>
    /// 逆序结束关卡时清除本关在场奖励；回收不触发拾取，不写档案。
    /// </summary>
    private void ClearLoot()
    {
        foreach (LootOrbNode orb in _lootOrbs)
        {
            NodePool.Instance.Release(orb);
        }

        _lootOrbs.Clear();
        foreach (GroundItemNode item in _groundItems)
        {
            NodePool.Instance.Release(item);
        }

        _groundItems.Clear();
        _exitRequested = false;
    }
}
