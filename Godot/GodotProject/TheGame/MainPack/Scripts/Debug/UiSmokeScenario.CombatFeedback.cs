using GameLogic.FloatingText;
using GameLogic.Battle;
using GameLogic.Architecture;
using GameLogic.Equipment;
using GameLogic.Save;
using GameLogic.Profile;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using GameConfig.Entity;
using GameConfig.Constant;
using GameFramework.Fsm;
using GameLogic.Drops;
using GameLogic.Heroes;
using GameLogic.Level;
using GameLogic.Enemies;
using GameLogic.Enemies.AI;
using GameLogic;
using GameLogic.Input;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;

/// <summary>真实引擎中的单向平台、飞行拾取、暂停和升级画面回归。</summary>
public sealed partial class UiSmokeScenario
{
    private LootOrbNode m_PreviousOrb; // 已回池实例，供下一次击败检查 GGF 实际复用。

    /// <summary>强制构造稀有蓝球夹具，验证实际拾取回蓝、上限、池复用与失效目标取消。</summary>
    /// <param name="flow">测试档案作用域。</param>
    /// <param name="level">正式奖励所有者。</param>
    /// <param name="hero">正式出战英雄。</param>
    /// <returns>真实节点飞行与资源变化验证。</returns>
    private async Task CheckManaPickupAsync(ProcedureOwner flow, LevelViewController level, AbstractHeroEntity hero)
    {
        var owned = SmokeInspection.ReadField<HashSet<LootOrbNode>>(level, "m_LootOrbs");
        GF.Fsm.GetSystem<HeroSystem>().TrySpendMp(GF.Fsm.GetModel<HeroModel>().Mp.Value - 2);
        LootOrbNode first = SpawnManaOrb(level, hero.GlobalPosition);
        Check(GF.Fsm.GetModel<HeroModel>().Mp.Value == 2, "蓝球出生时已直接回蓝");
        Vector2 pausedPosition = first.GlobalPosition;
        GF.Base.PauseGame();
        try
        {
            await WaitFramesAsync(4);
            Check(first.GlobalPosition.IsEqualApprox(pausedPosition) && owned.Contains(first) && GF.Fsm.GetModel<HeroModel>().Mp.Value == 2,
                "暂停时飞行球仍移动或领取");
        }
        finally
        {
            GF.Base.ResumeGame();
        }
        // 飞行开始后改变英雄位置，必须跟随本帧坐标而非出生时的目标点。
        Vector2 originalHeroPosition = hero.GlobalPosition;
        hero.GlobalPosition += new Vector2(-40, 0);
        await WaitUntilAsync(() => owned.Count == 0, "蓝球没有接触英雄", 10000);
        Check(GF.Fsm.GetModel<HeroModel>().Mp.Value == Math.Min(GF.Fsm.GetModel<HeroModel>().MaxMp.Value, 2 + flow.GetSystem<LootSystem>().ManaAmount(GF.Fsm.GetModel<HeroModel>().MaxMp.Value)), "接触后回蓝量不符合配置");

        var floating = SmokeInspection.ReadField<FloatingTextPresenter>(level, "m_FloatingText");
        var texts = SmokeInspection.ReadField<HashSet<FloatingTextNode>>(floating, "m_Texts");
        Check(!texts.Any(x => SmokeInspection.ReadField<object>(x, "m_Kind").ToString() == "Recovery"),
            "蓝量灵魂不应生成恢复飘字");
        hero.GlobalPosition = originalHeroPosition;
        GF.Fsm.GetSystem<HeroSystem>().RestoreMp(GF.Fsm.GetModel<HeroModel>().MaxMp.Value);
        GF.Fsm.GetSystem<HeroSystem>().TrySpendMp(2);
        LootOrbNode reused = SpawnManaOrb(level, hero.GlobalPosition);
        Check(ReferenceEquals(first, reused), "蓝球没有复用同一 GGF 池实例");
        await WaitUntilAsync(() => owned.Count == 0, "复用蓝球没有结束飞行", 10000);
        Check(GF.Fsm.GetModel<HeroModel>().Mp.Value == GF.Fsm.GetModel<HeroModel>().MaxMp.Value, "蓝球超出上限或复用后未正确回蓝");

        // 黄球只增加无双，不能再次增加经验；开启期间也允许拾取补充。
        var battle = ConfigSystem.Instance.Tables.TbBattleConfig.Data;
        GF.Fsm.GetSystem<HeroSystem>().ClearWs();
        GF.Fsm.GetSystem<HeroSystem>().AddWs(GF.Fsm.GetModel<HeroModel>().MaxWs.Value);
        Check(hero.TryActivateMusou(), "黄球回归前未开启无双");
        await WaitUntilAsync(() => !GF.Base.IsGamePaused, "黄球回归的开启停顿未结束", 4000);
        GF.Fsm.GetSystem<HeroSystem>().AdvanceWs(2);
        int musou = GF.Fsm.GetModel<HeroModel>().Ws.Value;
        int experience = flow.GetModel<ProfileModel>().TotalExperience;
        SpawnOrb(level, hero.GlobalPosition, new(LootKind.Musou, ConfigSystem.Instance.Tables.TbBattleConfig.Data.MusouPerOrb));
        await WaitUntilAsync(() => owned.Count == 0, "黄球没有接触英雄", 10000);
        Check(GF.Fsm.GetModel<HeroModel>().Ws.Value == musou + 5 && flow.GetModel<ProfileModel>().TotalExperience == experience,
            "开启期间的黄球没有补充5无双，或错误增加经验");
        hero._PhysicsProcess(battle.WsMax / battle.WsDrainPerSecond + 1f);

        m_ExtraHero = await ShowHeroAsync(flow, flow.GetModel<ProfileModel>().TotalExperience);
        // 两个表现节点借用同一 HeroModel，不能通过击杀额外节点来伪造独立生命。
        // 用明显不同的位置验证球的飞行目标，不改动本关生命状态。
        m_ExtraHero.GlobalPosition = hero.GlobalPosition + new Vector2(400, 0);
        GF.Fsm.GetSystem<HeroSystem>().TrySpendMp(GF.Fsm.GetModel<HeroModel>().Mp.Value - 2);
        var targetProbe = SpawnManaOrb(level, m_ExtraHero.GlobalPosition);
        Vector2 lastPosition = targetProbe.GlobalPosition;
        await WaitUntilAsync(() =>
        {
            if (owned.Contains(targetProbe))
                lastPosition = targetProbe.GlobalPosition;
            return owned.Count == 0;
        }, "飞行球没有跟随关卡所有者的实际英雄", 10000);
        Check(lastPosition.DistanceTo(hero.GlobalPosition) < lastPosition.DistanceTo(m_ExtraHero.GlobalPosition),
            "球飞向了额外显示的英雄，没有飞向本关实际英雄");
        Check(GF.Fsm.GetModel<HeroModel>().Mp.Value == Math.Min(GF.Fsm.GetModel<HeroModel>().MaxMp.Value, 2 + flow.GetSystem<LootSystem>().ManaAmount(GF.Fsm.GetModel<HeroModel>().MaxMp.Value)),
            "球错误跟随了同场景的其他英雄，未向本关英雄发放奖励");
        ReleaseTestEntity(m_ExtraHero);
        m_ExtraHero = null;
        // 留一颗正在等待的球，调用方随后通过正式 Cleanup 验证卸载回收。
        SpawnManaOrb(level, hero.GlobalPosition);
        GD.Print("SMOKE-UI: mana pickup/clamp/pool reuse/scene-owned target selection passed");
    }

    /// <summary>只在测试侧通过反射把蓝球挂入正式所有权，避免为随机测试增加生产接口。</summary>
    /// <param name="level">实际结算所有者。</param>
    /// <param name="position">出生位置；不绑定目标英雄。</param>
    /// <returns>正在飞行的池实例。</returns>
    private static LootOrbNode SpawnManaOrb(LevelViewController level, Vector2 position)
        => SpawnOrb(level, position, new(LootKind.Mana, 0));

    /// <summary>在测试中生成指定球并接入当前关卡实际的拾取回调。</summary>
    /// <param name="level">实际奖励所有者。</param>
    /// <param name="position">出生位置；实际目标由关卡所有者决定。</param>
    /// <param name="reward">受测奖励，不向生产代码增加观测入口。</param>
    /// <returns>真实池实例。</returns>
    private static LootOrbNode SpawnOrb(LevelViewController level, Vector2 position, LootReward reward)
    {
        var orb = GodotGameFramework.NodePool.NodePool.Instance.Get<LootOrbNode>(ResourcesCollectionConstant.Drops_loot_orb, SmokeInspection.Scene(level));
        SmokeInspection.ReadField<HashSet<LootOrbNode>>(level, "m_LootOrbs").Add(orb);
        MethodInfo finish = typeof(LevelViewController).GetMethod("OnLootFinished", BindingFlags.Instance | BindingFlags.NonPublic)!;
        orb.Show(position + new Vector2(180, -30), reward,
            (node, reward) => finish.Invoke(level, [node, reward]));
        return orb;
    }

    /// <summary>在真实妖猴身体和播放器上验证重新进入追击不会插入持棒待机帧。</summary>
    /// <param name="hero">静止的真实目标。</param>
    /// <returns>两次追击进入的引擎回归。</returns>
    private async Task CheckDemonChaseAsync(AbstractHeroEntity hero)
    {
        m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.DemonMonkey,
            new EnemySpawn(hero.GlobalPosition + new Vector2(300, -40)));
        await WaitUntilAsync(() => m_Monster.OnFloor, "妖猴追击夹具没有落地");
        typeof(AbstractEnemyEntity).GetField("m_Target", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(m_Monster, hero);
        var fsm = SmokeInspection.ReadField<IFsm<IEnemyAiAgent>>(m_Monster, "m_AiFsm");
        MethodInfo change = fsm.GetType().GetMethod("ChangeState", BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(Type)], null)!;
        foreach (float direction in new[] { -1f, 1f })
        {
            hero.GlobalPosition = m_Monster.GlobalPosition + new Vector2(direction * 300, 0);
            change.Invoke(fsm, [typeof(StandAndStrikeState)]);
            change.Invoke(fsm, [typeof(WalkToTargetState)]);
            Check(m_Monster.AiData.MoveIntent == (int)direction,
                "进入追击当帧仍写入零移动意图");
            await WaitFramesAsync(2);
            Check(m_Monster.AnimPlayer.CurrentAnimation == "run" && Math.Sign(m_Monster.Velocity.X) == (int)direction,
                $"追击进入后播放了持棒待机姿势：{m_Monster.AnimPlayer.CurrentAnimation}");
        }

        var body = m_Monster.GetNode<AnimatedSprite2D>("m_Body");
        foreach (float direction in new[] { -1f, 1f })
        {
            // 平台目标：每个实际绘制帧都必须继续移动，不能短暂进入站定攻击。
            hero.GlobalPosition = m_Monster.GlobalPosition + new Vector2(direction * 30, -160);
            change.Invoke(fsm, [typeof(WalkToTargetState)]);
            for (int frame = 0; frame < 8; frame++)
            {
                await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
                await m_Driver.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Check(fsm.CurrentState is PaceBelowTargetState && m_Monster.AttackSegment < 0 &&
                    m_Monster.AiData.MoveIntent != 0 && body.Animation == "run",
                    $"平台下移动闪出待机帧：AI={fsm.CurrentState.GetType().Name}，身体={body.Animation}");
            }

            // 移动目标：在追击/站定的边缘连续往返，逐渲染帧观测真实 Sprite，而非等两帧后才检查。
            change.Invoke(fsm, [typeof(WalkToTargetState)]);
            for (int frame = 0; frame < 16; frame++)
            {
                await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.ProcessFrame);
                float distance = fsm.CurrentState is WalkToTargetState ? 40f : 140f;
                hero.GlobalPosition = m_Monster.GlobalPosition + new Vector2(direction * distance, 0);
                await m_Driver.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Check(m_Monster.AiData.MoveIntent == (int)direction &&
                    body.Animation == "run" && m_Monster.AttackSegment < 0,
                    $"范围边缘追击闪出待机帧：AI={fsm.CurrentState.GetType().Name}，身体={body.Animation}");
            }
        }
        ReleaseTestEntity(m_Monster);
        m_Monster = null;
        GD.Print("SMOKE-UI: 妖猴左右追击、平台踱步及范围边缘切换逐渲染帧保持 run，无瞬间举棒");
    }

    /// <summary>用真实 CharacterBody2D 分别穿过平台底部并落在平台上。</summary>
    /// <param name="level">平台所在关卡。</param>
    /// <param name="flow">用于创建独立测试英雄的档案作用域。</param>
    /// <returns>三个物理探针及英雄下穿完成的任务。</returns>
    private async Task CheckPlatformsAsync(LevelViewController level, ProcedureOwner flow)
    {
        foreach (StaticBody2D platform in SmokeInspection.Scene(level).GetNode("World/Platforms").GetChildren())
        {
            var probe = new CharacterBody2D { CollisionLayer = 0, CollisionMask = 8 };
            probe.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(20, 20) } });
            SmokeInspection.Scene(level).AddChild(probe);
            try
            {
                var surface = platform.GetNode<CollisionShape2D>("Surface");
                Rect2 bounds = surface.GlobalTransform * surface.Shape.GetRect();
                float centerX = bounds.GetCenter().X;
                float surfaceTop = bounds.Position.Y;
                probe.GlobalPosition = new Vector2(centerX, surfaceTop + 60);
                probe.Velocity = new Vector2(0, -500);
                bool above = false;
                for (int frame = 0; frame < 150; frame++)
                {
                    await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
                    probe.Velocity += new Vector2(0, 900f / Engine.PhysicsTicksPerSecond);
                    probe.MoveAndSlide();
                    above |= probe.GlobalPosition.Y < surfaceTop - 20;
                    if (above && probe.IsOnFloor())
                        break;
                }
                float landingY = surfaceTop - 10f;
                Check(above && probe.IsOnFloor() && Math.Abs(probe.GlobalPosition.Y - landingY) < 1.1f,
                    $"平台 {platform.Name} 无法从下方穿过并落到上方：{probe.GlobalPosition}");
            }
            finally
            {
                probe.QueueFree();
            }
        }
        m_ExtraHero = await ShowHeroAsync(flow, flow.GetModel<ProfileModel>().TotalExperience);
        AbstractHeroEntity hero = m_ExtraHero;
        try
        {
            StaticBody2D platform = SmokeInspection.Scene(level).GetNode("World/Platforms").GetChildren().OfType<StaticBody2D>().First();
            CollisionShape2D bodyShape = hero.GetNode<CollisionShape2D>("CollisionShape2D");
            var platformSurface = platform.GetNode<CollisionShape2D>("Surface");
            Rect2 platformBounds = platformSurface.GlobalTransform * platformSurface.Shape.GetRect();
            float feetOffset = bodyShape.Position.Y + bodyShape.Shape.GetRect().End.Y;
            float surfaceTop = platformBounds.Position.Y;
            float centerX = platformBounds.GetCenter().X;
            hero.GlobalPosition = new Vector2(centerX, surfaceTop - feetOffset - 40f);
            hero.Velocity = Vector2.Zero;
            hero.SetPhysicsProcess(true);
            // 等两次帧信号，确保至少完成一次移位后的物理更新。
            for (int frame = 0; frame < 2; frame++)
                await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
            for (int frame = 0; frame < 120 && !hero.IsOnFloor(); frame++)
                await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(hero.IsOnFloor() && Enumerable.Range(0, hero.GetSlideCollisionCount()).Any(index =>
                ReferenceEquals(hero.GetSlideCollision(index).GetCollider(), platform)), "英雄未落在可穿平台");
            float before = hero.GlobalPosition.Y;
            Input.ActionPress(InputSystem.MoveDown);
            Input.ActionPress(InputSystem.Jump);
            for (int frame = 0; frame < 20; frame++)
                await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(hero.GlobalPosition.Y > before + hero.Config.PlatformDropDistance && hero.Velocity.Y >= 0,
                $"S+K 未从单向平台向下离开，或误触起跳：before={before}, after={hero.GlobalPosition.Y}, vy={hero.Velocity.Y}, floor={hero.IsOnFloor()}");
        }
        finally
        {
            Input.ActionRelease(InputSystem.MoveDown);
            Input.ActionRelease(InputSystem.Jump);
            ReleaseTestEntity(m_ExtraHero);
            m_ExtraHero = null;
        }
        GD.Print("SMOKE-UI: three official one-way platforms and hero S+K drop passed real physics");
    }

    /// <summary>观测真正的在场球：未接触不入账，暂停不前进，恢复后追踪移动中的英雄。</summary>
    /// <param name="level">奖励所有者。</param>
    /// <param name="hero">本关英雄。</param>
    /// <returns>飞行中行为检查任务。</returns>
    private async Task CheckFlyingLootAsync(LevelViewController level, AbstractHeroEntity hero)
    {
        var orbs = SmokeInspection.ReadField<HashSet<LootOrbNode>>(level, "m_LootOrbs");
        Check(orbs.Count is >= 2 and <= 10, "怪物死亡掉落数量超出官方范围");
        if (m_PreviousOrb != null)
            Check(orbs.Contains(m_PreviousOrb), "下一次击败未复用已归还的飞行球");
        m_PreviousOrb = orbs.First();
        var positions = orbs.ToDictionary(x => x, x => x.GlobalPosition);
        GF.Base.PauseGame();
        try
        {
            await m_Driver.ToSignal(m_Driver.GetTree().CreateTimer(.3, true, false, true), SceneTreeTimer.SignalName.Timeout);
            Check(positions.All(x => x.Key.GlobalPosition.IsEqualApprox(x.Value)), "暂停时飞行球仍在运动");
        }
        finally
        {
            GF.Base.ResumeGame();
        }

        // 跟踪目标换位置，后续调用方等待真正接触后的档案变化。
        hero.GlobalPosition += new Vector2(-40, 0);
        foreach (AbstractEnemyEntity monster in m_Driver.GetTree().Root.FindChildren("*", "CharacterBody2D", true, false).OfType<AbstractEnemyEntity>())
            SmokeInspection.FreezeAi(monster);
        await CaptureCombatAsync("flying_loot");
    }

    /// <summary>有渲染器时保留实际游戏截图，headless 时只执行状态与物理断言。</summary>
    /// <param name="name">证据文件名称。</param>
    /// <returns>渲染帧保存任务。</returns>
    private async Task CaptureCombatAsync(string name)
    {
        if (DisplayServer.GetName() == "headless")
            return;
        await m_Driver.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute("res://.godot/validation");
        Check(m_Driver.GetViewport().GetTexture().GetImage().SavePng($"res://.godot/validation/{name}.png") == Error.Ok,
            "战斗反馈截图保存失败");
    }
}
