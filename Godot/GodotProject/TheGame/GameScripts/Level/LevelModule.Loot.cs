using System;
using System.Collections.Generic;
using GameConfig.Monster;
using GameLogic.Drops;
using Godot;
using GodotGameFramework.NodePool;

namespace GameLogic.Level;

/// <summary>关卡拥有的飞行掉落；拾取才入账，死亡、放弃和卸载时回收未领取的球。</summary>
public partial class LevelModule
{
    private const string LootOrbScene = "res://TheGame/Entitys/LootOrb.tscn"; // GGF 节点池注册的可复用场景。
    private readonly HashSet<LootOrbNode> m_LootOrbs = new(); // 仅本关当前在场的球，不用全池清理影响其他作用域。
    private readonly RandomNumberGenerator m_LootRandom = new(); // 本关掉落采样器；纯规则通过委托取值。
    private bool m_ExitRequested; // W 已确认，等待尚在飞行的球接触后提交检查点。

    /// <summary>复制后的死亡数据在当前关卡生成奖励，不保留池化事件参数。</summary>
    /// <param name="monster">基础掉落配置。</param>
    /// <param name="position">怪物死亡位置。</param>
    /// <param name="experienceReward">本次配方确定的经验。</param>
    private void OnMonsterDefeated(MonsterConfig monster, Vector2 position, int experienceReward)
    {
        if (Outcome != LevelOutcome.Running || !m_Player.IsAlive) return;
        m_DefeatedMonsterCount++;
        var config = ConfigSystem.Instance.Tables.TbDropConfig.Data;
        try
        {
            foreach (LootReward reward in LootHelper.Roll(monster, experienceReward, config, m_LootRandom.Randf))
            {
                var orb = NodePool.Instance.Get<LootOrbNode>(LootOrbScene, this)
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
                case LootKind.Experience: AwardExperience(reward.Amount); break;
                case LootKind.Mana:
                    m_Player.Vitals.RestoreMp(LootHelper.ManaAmount(m_Player.Vitals.MaxMp,
                        ConfigSystem.Instance.Tables.TbDropConfig.Data));
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
        m_ExitRequested = false;
    }
}
