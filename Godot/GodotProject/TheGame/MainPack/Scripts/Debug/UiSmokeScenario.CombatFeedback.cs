using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using GameConfig.Entity;
using GameFramework.Fsm;
using GameLogic.Drops;
using GameLogic.Heroes;
using GameLogic.Level;
using GameLogic.Monsters;
using GameLogic.Monsters.AI;
using GameLogic;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;

/// <summary>真实引擎中的单向平台、飞行拾取、暂停和升级画面回归。</summary>
public sealed partial class UiSmokeScenario
{
    private LootOrbNode m_PreviousOrb; // 已回池实例，供下一次击败检查 GGF 实际复用。

    /// <summary>强制构造稀有蓝球夹具，验证实际拾取回蓝、上限、池复用与失效目标取消。</summary>
    /// <param name="context">测试档案作用域。</param>
    /// <param name="level">正式奖励所有者。</param>
    /// <param name="hero">正式出战英雄。</param>
    /// <returns>真实节点飞行与资源变化验证。</returns>
    private async Task CheckManaPickupAsync(GameContext context, LevelModule level, HeroEntity hero)
    {
        var owned = SmokeInspection.ReadField<HashSet<LootOrbNode>>(level, "m_LootOrbs");
        hero.Vitals.TrySpendMp(hero.Vitals.Mp - 2);
        LootOrbNode first = SpawnManaOrb(level, hero);
        Check(hero.Vitals.Mp == 2, "蓝球出生时已直接回蓝");
        await WaitUntilAsync(() => owned.Count == 0, "蓝球没有接触英雄", 10000);
        Check(hero.Vitals.Mp == Math.Min(hero.Vitals.MaxMp, 2 + LootHelper.ManaAmount(hero.Vitals.MaxMp,
            ConfigSystem.Instance.Tables.TbDropConfig.Data)), "接触后回蓝量不符合配置");

        hero.Vitals.RestoreMp(hero.Vitals.MaxMp);
        hero.Vitals.TrySpendMp(2);
        LootOrbNode reused = SpawnManaOrb(level, hero);
        Check(ReferenceEquals(first, reused), "蓝球没有复用同一 GGF 池实例");
        await WaitUntilAsync(() => owned.Count == 0, "复用蓝球没有结束飞行", 10000);
        Check(hero.Vitals.Mp == hero.Vitals.MaxMp, "蓝球超出上限或复用后未正确回蓝");

        m_ExtraHero = await ShowHeroAsync(context, context.Profile.ActiveHero);
        m_ExtraHero.GlobalPosition = hero.GlobalPosition;
        int before = hero.Vitals.Mp;
        SpawnManaOrb(level, m_ExtraHero);
        KillHero(m_ExtraHero);
        await WaitUntilAsync(() => owned.Count == 0, "死亡目标的飞行球没有回池");
        Check(hero.Vitals.Mp == before, "死亡目标的球向其他英雄发放了奖励");
        ReleaseTestEntity(m_ExtraHero);
        m_ExtraHero = null;
        // 留一颗正在等待的球，调用方随后通过正式 Cleanup 验证卸载回收。
        SpawnManaOrb(level, hero);
        GD.Print("SMOKE-UI: mana pickup/clamp/pool reuse/dead target cancellation passed");
    }

    /// <summary>只在测试侧通过反射把蓝球挂入正式所有权，避免为随机测试增加生产接口。</summary>
    /// <param name="level">实际结算所有者。</param>
    /// <param name="target">飞行目标。</param>
    /// <returns>正在飞行的池实例。</returns>
    private static LootOrbNode SpawnManaOrb(LevelModule level, HeroEntity target)
    {
        var orb = GodotGameFramework.NodePool.NodePool.Instance.Get<LootOrbNode>("res://TheGame/Entitys/LootOrb.tscn", level);
        SmokeInspection.ReadField<HashSet<LootOrbNode>>(level, "m_LootOrbs").Add(orb);
        MethodInfo finish = typeof(LevelModule).GetMethod("OnLootFinished", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var config = ConfigSystem.Instance.Tables.TbDropConfig.Data;
        orb.Show(target.GlobalPosition + new Vector2(180, -30), target, new(LootKind.Mana, 0), config,
            config.RiseDistance.X, (node, reward, collected) => finish.Invoke(level, [node, reward, collected]));
        return orb;
    }

    /// <summary>在真实妖猴身体和播放器上验证重新进入追击不会插入持棒待机帧。</summary>
    /// <param name="hero">静止的真实目标。</param>
    /// <returns>两次追击进入的引擎回归。</returns>
    private async Task CheckDemonChaseAsync(HeroEntity hero)
    {
        m_Monster = (MonsterEntity)await GF.Entity.ShowEntityAsync(EntityId.DemonMonkey,
            new MonsterSpawn(hero.GlobalPosition + new Vector2(300, -40)));
        await WaitUntilAsync(() => m_Monster.OnFloor, "妖猴追击夹具没有落地");
        typeof(MonsterEntity).GetField("m_Target", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(m_Monster, hero);
        var fsm = SmokeInspection.ReadField<IFsm<IMonsterAiAgent>>(m_Monster, "m_AiFsm");
        MethodInfo change = fsm.GetType().GetMethod("ChangeState", BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(Type)], null)!;
        foreach (float direction in new[] { -1f, 1f })
        {
            hero.GlobalPosition = m_Monster.GlobalPosition + new Vector2(direction * 300, 0);
            change.Invoke(fsm, [typeof(StandAndStrikeState)]);
            change.Invoke(fsm, [typeof(WalkToTargetState)]);
            Check(SmokeInspection.ReadField<int>(m_Monster, "m_MoveIntent") == (int)direction,
                "进入追击当帧仍写入零移动意图");
            await WaitFramesAsync(2);
            Check(m_Monster.AnimPlayer.CurrentAnimation == "run" && Math.Sign(m_Monster.Velocity.X) == (int)direction,
                $"追击进入后播放了持棒待机姿势：{m_Monster.AnimPlayer.CurrentAnimation}");
        }
        ReleaseTestEntity(m_Monster);
        m_Monster = null;
        GD.Print("SMOKE-UI: demon chase re-entry keeps walking animation");
    }

    /// <summary>用真实 CharacterBody2D 分别穿过平台底部并落在平台上。</summary>
    /// <param name="level">平台所在关卡。</param>
    /// <returns>三个物理探针完成的任务。</returns>
    private async Task CheckPlatformsAsync(LevelModule level)
    {
        foreach (StaticBody2D platform in level.GetNode("World/Platforms").GetChildren())
        {
            var probe = new CharacterBody2D { CollisionLayer = 0, CollisionMask = 8 };
            probe.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(20, 20) } });
            level.AddChild(probe);
            try
            {
                probe.GlobalPosition = platform.GlobalPosition + new Vector2(0, 60);
                probe.Velocity = new Vector2(0, -500);
                bool above = false;
                for (int frame = 0; frame < 150; frame++)
                {
                    await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
                    probe.Velocity += new Vector2(0, 900f / Engine.PhysicsTicksPerSecond);
                    probe.MoveAndSlide();
                    above |= probe.GlobalPosition.Y < platform.GlobalPosition.Y - 20;
                    if (above && probe.IsOnFloor()) break;
                }
                Check(above && probe.IsOnFloor() && Math.Abs(probe.GlobalPosition.Y - (platform.GlobalPosition.Y - 20)) < 1.1f,
                    $"平台 {platform.Name} 无法从下方穿过并落到上方：{probe.GlobalPosition}");
            }
            finally { probe.QueueFree(); }
        }
        GD.Print("SMOKE-UI: three official one-way platforms passed real physics");
    }

    /// <summary>观测真正的在场球：未接触不入账，暂停不前进，恢复后追踪移动中的英雄。</summary>
    /// <param name="level">奖励所有者。</param>
    /// <param name="hero">本关英雄。</param>
    /// <returns>飞行中行为检查任务。</returns>
    private async Task CheckFlyingLootAsync(LevelModule level, HeroEntity hero)
    {
        var orbs = SmokeInspection.ReadField<HashSet<LootOrbNode>>(level, "m_LootOrbs");
        Check(orbs.Count >= 3, "怪物死亡未产生池化飞行球");
        if (m_PreviousOrb != null) Check(orbs.Contains(m_PreviousOrb), "下一次击败未复用已归还的飞行球");
        m_PreviousOrb = orbs.First();
        var positions = orbs.ToDictionary(x => x, x => x.GlobalPosition);
        GF.Base.PauseGame();
        try
        {
            await m_Driver.ToSignal(m_Driver.GetTree().CreateTimer(.3, true, false, true), SceneTreeTimer.SignalName.Timeout);
            Check(positions.All(x => x.Key.GlobalPosition.IsEqualApprox(x.Value)), "暂停时飞行球仍在运动");
        }
        finally { GF.Base.ResumeGame(); }

        // 跟踪目标换位置，后续调用方等待真正接触后的档案变化。
        hero.GlobalPosition += new Vector2(-40, 0);
        foreach (MonsterEntity monster in m_Driver.GetTree().Root.FindChildren("*", "CharacterBody2D", true, false).OfType<MonsterEntity>())
            SmokeInspection.FreezeAi(monster);
        await CaptureCombatAsync("flying_loot");
    }

    /// <summary>有渲染器时保留实际游戏截图，headless 时只执行状态与物理断言。</summary>
    /// <param name="name">证据文件名称。</param>
    /// <returns>渲染帧保存任务。</returns>
    private async Task CaptureCombatAsync(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await m_Driver.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute("res://.godot/validation");
        Check(m_Driver.GetViewport().GetTexture().GetImage().SavePng($"res://.godot/validation/{name}.png") == Error.Ok,
            "战斗反馈截图保存失败");
    }
}
