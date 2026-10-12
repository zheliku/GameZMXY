using GameLogic.Level;
using GameLogic.Battle.Attacks;
using GameLogic.Event.Combat;
using GameLogic.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using GameLogic.Save;
using GameLogic.UI;
using GameLogic.UI.Widgets;
using GameConfig.Battle;
using GameConfig.Sound;
using GameConfig.Constant;
using GameFramework;
using GameLogic.Heroes;
using GameLogic.Monsters;
using GameLogic;
using Godot;
using GodotGameFramework;

/// <summary>通过真实移动、碰撞、守关怪和出口输入验证五波清场、自动保存与返回地图。</summary>
public sealed class LevelSmokeScenario
{
    private enum Step // 单段战斗内的物理约束验证顺序。
    {
        RightWall,
        LeftWall,
        Kill,
    }

    private readonly HeroEntity m_Hero; // 通过游戏流程实际显示的玩家。
    private readonly LevelModule m_Level; // 受测关卡会话。
    private readonly LevelCameraNode m_Camera; // 受测实际取景。
    private readonly LevelGatesNode m_Gates; // 受测阶段区域。
    private readonly HashSet<int> m_Seen = new(); // 所有实际显示过的运行实体 ID。
    private readonly List<string> m_Failures = new(); // 累计回归失败信息。
    private float m_LastCenter; // 上一帧实际相机中心。
    private float m_LastPlayerX; // 上一帧玩家横坐标。
    private int m_StageOrder; // 上一次观测到的开战阶段。
    private int m_BlockedFrames; // 玩家在边界持续停住的物理帧数。
    private Step m_Step; // 当前战斗物理验证步骤。
    private double m_Time; // 整体游戏时间，超时用于报告卡住的位置。
    private double m_BattleTime; // 当前阶段开战后的游戏时间。
    private bool m_GoHintSeen; // 是否在行进阶段观察到前进提示。
    private int m_TravelStageOrder; // 当前观测过行进起点的阶段。
    private bool m_ExpectCenteredArrival; // 从新取景中心后方正常走入时，抵达应保持中央死区。
    private readonly int m_StartExperience; // 进关时档案中的累计经验。
    private readonly int m_StartSoul; // 进关时的钱包，通关后按真实随机奖励核对写盘。
    private bool m_FirstSpawnEntered; // 首波保留真实 AI，直到观测到它从场外走入画面。
    private readonly GameContext m_Context; // 本次受测档案与存档入口。
    private ulong m_ExitRequestedAt; // 近处 W 按下到地图实际可见的实际时刻。
    private bool m_ExitLatencyRecorded; // 延迟只采样一次，不包含随后验证读盘的时间。
    private bool m_ExitHadPendingLoot; // W 时还有球在飞行；等待真实拾取与额外关卡延迟分别观测。
    private Task m_PlatformCapture; // 第三阶段的实际画面证据。
    private int m_ExitStep; // 远处按 W、接近、出口交互的验证顺序。
    private bool m_SawFeatured; // 是否实际展示过大猩猩入场和生命条。
    private bool m_WaitingForMap; // 通关后停止访问即将卸载的场景。
    private Task<SaveLoadResult> m_Reload; // 返回地图后重新读盘验证保存结果。
    private Task m_ExitCapture; // 有渲染器时先保存出口提示画面再按 W。
    private int m_ExpectedTotalExperience; // 通关保存应保留的累计经验。
    private int m_ExpectedSoul; // 已实际拾取、应由 v4 检查点保存的灵魂。

    /// <summary>创建正式关卡的物理回归场景。</summary>
    /// <param name="hero">正式流程显示的英雄。</param>
    /// <param name="level">受测关卡。</param>
    public LevelSmokeScenario(HeroEntity hero, LevelModule level)
    {
        m_Hero = hero;
        m_Level = level;
        m_Context = SmokeInspection.Context(GF.Procedure.CurrentProcedure as ProcedureLevel);
        m_StartExperience = m_Context.Profile.ActiveHero.TotalExperience;
        m_StartSoul = m_Context.Profile.Wallet.Soul;
        m_Camera = level.GetNode<LevelCameraNode>("Camera2D");
        m_Gates = level.GetNode<LevelGatesNode>("World/StageGates");
        m_LastCenter = m_Camera.GlobalPosition.X;
        m_LastPlayerX = hero.GlobalPosition.X;
    }

    /// <summary>物理回归已完成或遇到失败。</summary>
    public bool IsDone { get; private set; }

    /// <summary>完成时的失败原因列表。</summary>
    public IReadOnlyList<string> Failures => m_Failures;

    /// <summary>在相机和实体物理更新之后观测，驱动真实输入和测试攻击；完成后不再访问已可能被重开流程卸载的关卡。</summary>
    /// <param name="delta">物理帧间隔秒数。</param>
    public void Update(double delta)
    {
        if (IsDone)
        {
            return;
        }

        m_Time += delta;
        if (m_WaitingForMap)
        {
            CheckReturnAndReload();
            return;
        }
        float center = m_Camera.GlobalPosition.X;
        if (Math.Abs(center - m_LastCenter) > 600f * delta + 0.1f)
        {
            Fail($"相机帧位移超限：{m_LastCenter} → {center}，阶段 {SmokeInspection.Sequence(m_Level).Current.StageOrder}");
        }

        if (Math.Abs(center - m_Camera.GetScreenCenterPosition().X) > 0.1f)
        {
            Fail("脚本相机中心与实际显示中心不一致");
        }

        m_LastCenter = center;
        MonsterEntity[] monsters = m_Hero.GetTree().Root.FindChildren("*", "CharacterBody2D", true, false)
            .OfType<MonsterEntity>().Where(x => x.IsShown && !x.Dead).ToArray();
        foreach (MonsterEntity monster in monsters)
        {
            if (!m_FirstSpawnEntered && SmokeInspection.Sequence(m_Level).Current.StageOrder == 1 &&
                monster.GlobalPosition.X < m_Camera.ViewRight - 20f)
            {
                m_FirstSpawnEntered = true;
                GD.Print($"SMOKE-LEVEL: offscreen spawn entered, monster={monster.GlobalPosition.X:0.0}, viewRight={m_Camera.ViewRight:0.0}");
            }
            if (m_FirstSpawnEntered) SmokeInspection.FreezeAi(monster);
            if (m_Seen.Add(monster.Id))
            {
                if (monster is GorillaEntity)
                {
                    m_SawFeatured = true;
                    var banner = SmokeInspection.ReadField<EncounterHealthBar>(
                        GF.UI.GetUIForm(ResourcesCollectionConstant.UIs_BattleHud), "m_EncounterHealthBar");
                    if (!ReferenceEquals(m_Level.FeaturedOpponent, monster) || monster.Vitals.MaxHp != 900 ||
                        !banner.Visible || banner.HasNode("Entrance") ||
                        SmokeInspection.ReadField<Label>(banner, "m_Name").Text != monster.Config.NameCn)
                        Fail("大猩猩配置或守关血条未绑定，或仍残留入场弹窗");
                }
                // 在地形上方横穿当前右门，验证怪物可进入，玩家仍被同一道边界阻挡。
                float right = m_Gates.BoundsOf(SmokeInspection.Sequence(m_Level).Current.StageOrder).Right;
                Transform2D from = new(0f, new Vector2(right + 60f, 100f));
                if (monster.TestMove(from, new Vector2(-120f, 0f)) || !m_Hero.TestMove(from, new Vector2(-120f, 0f)))
                    Fail("阶段门未正确区分玩家阻挡与怪物场外入场");
            }
        }

        if (SmokeInspection.Sequence(m_Level).Phase == LevelStagePhase.Travelling)
        {
            int order = SmokeInspection.Sequence(m_Level).Current.StageOrder;
            if (m_TravelStageOrder != order)
            {
                m_TravelStageOrder = order;
                float arrivalCenter = m_Gates.BoundsOf(order).Right - m_Camera.ViewSize.X * 0.5f;
                // 重叠短过渡可能从新中心前方出发；此时只检查连续追近与边界，不要求瞬间倒退居中。
                m_ExpectCenteredArrival = m_Hero.GlobalPosition.X <= arrivalCenter - 40f;
            }
            if (monsters.Length > 0)
            {
                Fail("相机抵达之前生成了下一阶段敌人");
            }

            // 清波后到下一场开战之间，关卡应处于可前进窗口（HUD 据此显示 Go）。
            if (m_Level.TravelAvailable)
            {
                m_GoHintSeen = true;
            }

            Move("move_right");
            // 用户保留的斜坡含竖直端面，烟测用真实跳跃跨越，不修改场景地形。
            if (m_Hero.IsOnWall() && m_Hero.IsOnFloor())
            {
                Input.ActionPress(InputActions.Jump);
            }
            else
            {
                Input.ActionRelease(InputActions.Jump);
            }
        }
        else if (SmokeInspection.Sequence(m_Level).Phase == LevelStagePhase.Fighting)
        {
            if (m_Level.TravelAvailable)
            {
                Fail("开战后仍处于可前进窗口");
            }

            DriveBattle(delta, monsters);
        }
        else if (SmokeInspection.Sequence(m_Level).Phase == LevelStagePhase.AwaitingExit)
        {
            DriveExit();
        }
        else if (SmokeInspection.Sequence(m_Level).Phase == LevelStagePhase.Completed)
        {
            if (m_PlatformCapture?.IsFaulted == true) { Fail(m_PlatformCapture.Exception.ToString()); return; }
            if (m_ExitStep != 5) Fail("出口尚未完成远处拒绝与靠近交互验证，就提前通关");
            int expected = ConfigSystem.Instance.Tables.TbLevelConfig.Get(1).Stages.Sum(x => x.Recipes.Sum(r => r.Count));
            if (m_Seen.Count != expected)
            {
                Fail($"怪物总数错误：实际 {m_Seen.Count}，配置 {expected}");
            }

            // 收益进档案：本关获得经验 = 档案累计经验 - 进关时的累计经验（新档进关为 0）。
            int expectedExperience = ConfigSystem.Instance.Tables.TbLevelConfig.Get(1).Stages
                .Sum(x => x.Recipes.Sum(r => r.Count * (r.ExperienceOverride >= 0 ? r.ExperienceOverride :
                    ConfigSystem.Instance.Tables.TbMonsterConfig.DataList.Single(m => m.EntityId == r.MonsterEntityId).AddExp)));
            LevelModule run = SmokeInspection.Level(GF.Procedure.CurrentProcedure as ProcedureLevel);
            int gained = SmokeInspection.Context(GF.Procedure.CurrentProcedure as ProcedureLevel)?.Profile.ActiveHero
                .TotalExperience - m_StartExperience ?? -1;
            if (gained != expectedExperience || SmokeInspection.ReadField<int>(run, "m_AwardedExperience") != expectedExperience)
            {
                Fail($"本关经验错误或重复死亡奖励：档案 {gained}，统计 {SmokeInspection.ReadField<int>(run, "m_AwardedExperience")}，配置 {expectedExperience}");
            }
            GD.Print($"SMOKE-LEVEL: experience={gained}, expected={expectedExperience}");
            int minimumSoul = 0, maximumSoul = 0;
            foreach (var recipe in ConfigSystem.Instance.Tables.TbLevelConfig.Get(1).Stages.SelectMany(x => x.Recipes))
            {
                var monster = ConfigSystem.Instance.Tables.TbMonsterConfig.DataList.Single(x => x.EntityId == recipe.MonsterEntityId);
                minimumSoul += recipe.Count * monster.SoulCount.X * monster.SoulPerOrb;
                maximumSoul += recipe.Count * monster.SoulCount.Y * monster.SoulPerOrb;
            }
            m_ExpectedSoul = m_Context.Profile.Wallet.Soul;
            int gainedSoul = m_ExpectedSoul - m_StartSoul;
            if (gainedSoul < minimumSoul || gainedSoul > maximumSoul) Fail("本关灵魂奖励超出官方掉落范围");
            GD.Print($"SMOKE-LEVEL: soul={gainedSoul}, expected range={minimumSoul}..{maximumSoul}");

            // 通关是检查点：关卡运行已提交并结束。
            if (run?.Outcome != LevelOutcome.Cleared || run.CheckpointWrite == null)
            {
                Fail($"通关没有提交检查点：结局 {run?.Outcome}");
            }

            if (!m_GoHintSeen)
            {
                Fail("清波后未出现前进提示（Go）");
            }

            if (!m_SawFeatured) Fail("第五波未展示大猩猩守关对手");
            m_ExpectedTotalExperience = m_StartExperience + expectedExperience;
            m_WaitingForMap = true;
            Move(null);
        }

        if (m_Time > 180 && !IsDone)
        {
            Fail($"超时：阶段 {SmokeInspection.Sequence(m_Level).Current.StageOrder}/{SmokeInspection.Sequence(m_Level).Phase}，玩家 {m_Hero.GlobalPosition}，相机 {center}");
        }

        m_LastPlayerX = m_Hero.GlobalPosition.X;
        if (IsDone)
        {
            Move(null);
        }
    }

    /// <summary>远处按键不通关；清场后靠近出口，再发送真实 W 按键。</summary>
    private void DriveExit()
    {
        LevelExitNode exit = m_Level.GetNode<LevelExitNode>("World/Exit");
        if (!exit.Visible && m_ExitStep < 5) { Fail("清场后出口未显示"); return; }
        if (m_ExitStep == 0)
        {
            LevelModule run = SmokeInspection.Level(GF.Procedure.CurrentProcedure as ProcedureLevel);
            if (run.Outcome != LevelOutcome.Running || run.CheckpointWrite != null)
                Fail("仅清场就提前通关或写盘");
            PlaceForWallTest(exit.GlobalPosition.X - 220f);
            Move(null);
            m_ExitStep = 1;
        }
        else if (m_ExitStep == 1)
        {
            // 测试瞬移后等待物理重叠缓存更新，避免把原位置的重叠当成远处输入。
            m_ExitStep = 2;
        }
        else if (m_ExitStep == 2)
        {
            PressW();
            m_ExitStep = 3;
        }
        else if (m_ExitStep == 3)
        {
            LevelModule run = SmokeInspection.Level(GF.Procedure.CurrentProcedure as ProcedureLevel);
            if (run.Outcome != LevelOutcome.Running || run.CheckpointWrite != null)
            {
                Fail("远处按 W 就提交通关或写盘");
                return;
            }
            Move(InputActions.MoveRight);
            if (exit.GetOverlappingBodies().Contains(m_Hero) && m_Hero.OnFloor)
            {
                Move(null);
                m_ExitCapture = CaptureExitAsync();
                m_ExitStep = 4;
            }
        }
        else if (m_ExitStep == 4 && m_ExitCapture.IsCompleted)
        {
            if (m_ExitCapture.IsFaulted) { Fail(m_ExitCapture.Exception.ToString()); return; }
            m_ExitRequestedAt = Time.GetTicksMsec();
            m_ExitHadPendingLoot = SmokeInspection.ReadField<HashSet<GameLogic.Drops.LootOrbNode>>(m_Level, "m_LootOrbs").Count != 0;
            PressW();
            m_ExitStep = 5;
        }
    }

    /// <summary>有渲染器时记录真实出口和靠近提示，截图完成后再触发关卡卸载。</summary>
    /// <returns>本帧画面保存完成的任务。</returns>
    private async Task CaptureExitAsync()
    {
        if (DisplayServer.GetName() == "headless") return;
        await m_Hero.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string directory = ProjectSettings.GlobalizePath("res://.godot/validation/encounter");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "level_exit.png");
        Error result = m_Hero.GetViewport().GetTexture().GetImage().SavePng(path);
        if (result != Error.Ok) throw new IOException($"出口预览保存失败：{result}，{path}");
        GD.Print($"SMOKE-LEVEL: exit preview={path}");
    }

    /// <summary>从视口发送完整按下与释放，覆盖 W 绑定和未处理输入路径。</summary>
    private void PressW()
    {
        m_Hero.GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.W, Pressed = true });
        m_Hero.GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.W, Pressed = false });
    }

    /// <summary>流程实际返回地图后重新读盘，避免只检查内存中的框架数据。</summary>
    private void CheckReturnAndReload()
    {
        if (m_Time > 180) { Fail("出口交互后未返回地图或完成自动保存"); return; }
        if (GF.Procedure.CurrentProcedure is not ProcedureMap) return;
        if (GF.UI.GetUIForm(ResourcesCollectionConstant.UIs_map_form) is not MapForm { Visible: true }) return;
        if (!m_ExitLatencyRecorded)
        {
            ulong latency = Time.GetTicksMsec() - m_ExitRequestedAt;
            m_ExitLatencyRecorded = true;
            GD.Print($"SMOKE-LEVEL: exit W → visible map = {latency} ms (checkpoint included, pending loot={m_ExitHadPendingLoot})");
            if (latency > (m_ExitHadPendingLoot ? 10000UL : 1500UL)) { Fail($"出口返回地图过慢：{latency} ms"); return; }
        }
        m_Reload ??= m_Context.Save.LoadOrCreateAsync(GF.Archive.CurrentCatalogue.SlotNumber, GF.Archive.CurrentData.UnitId);
        if (!m_Reload.IsCompleted) return;
        if (m_Reload.IsFaulted || m_Reload.Result.Profile.ActiveHero.TotalExperience != m_ExpectedTotalExperience ||
            m_Reload.Result.Profile.Wallet.Soul != m_ExpectedSoul)
            Fail("返回地图后重新读取存档，经验或灵魂与通关收益不一致");
        else
        {
            GD.Print($"SMOKE-LEVEL: exit W → map → disk reload, experience={m_ExpectedTotalExperience}");
            var notice = m_Hero.GetTree().Root.FindChildren("*", "Control", true, false)
                .OfType<SaveNoticeForm>().FirstOrDefault(x => x.SerialId != 0);
            if (notice == null || !notice.GetNode<Label>("SaveFeedback/m_SaveFeedback").Visible)
                Fail("W 返回地图后的实际检查点未显示全局保存成功提示");
            else GD.Print("SMOKE-LEVEL: checkpoint success notice visible on map");
            IsDone = true;
        }
    }

    private void DriveBattle(double delta, MonsterEntity[] monsters) // 先验证抵达时取景和配置延迟，再验证墙体与真实清波。
    {
        var stage = ConfigSystem.Instance.Tables.TbLevelConfig.Get(1).Stages[SmokeInspection.Sequence(m_Level).Current.StageOrder - 1];
        var region = m_Gates.BoundsOf(stage.StageOrder);
        if (m_StageOrder != stage.StageOrder)
        {
            m_StageOrder = stage.StageOrder;
            m_BattleTime = 0;
            m_Step = Step.RightWall;
            m_BlockedFrames = 0;
            if (Math.Abs(m_Camera.ViewRight - region.Right) > 0.1f ||
                (m_ExpectCenteredArrival && Math.Abs(m_Hero.GlobalPosition.X - m_Camera.GlobalPosition.X) > 60f))
            {
                Fail("开战时相机未抵达右界，或玩家不在中央死区附近");
            }

            GD.Print($"SMOKE-LEVEL: stage {stage.StageOrder} arrived, camera={m_Camera.GlobalPosition.X:0.0}, player={m_Hero.GlobalPosition.X:0.0}");
            if (stage.StageOrder == 3) m_PlatformCapture = CapturePlatformsAsync();
            PlaceForWallTest(region.Right - 40f);
        }

        m_BattleTime += delta;
        if (m_BattleTime < stage.SpawnDelay - 0.05 && monsters.Length > 0)
        {
            Fail("阶段 SpawnDelay 到期之前刷怪");
        }

        if (m_Hero.GlobalPosition.X < region.Left || m_Hero.GlobalPosition.X > region.Right)
        {
            Fail("玩家越过战斗区域的物理边界");
        }

        if (m_Step == Step.Kill)
        {
            Move(null);
            if (stage.StageOrder == 1 && !m_FirstSpawnEntered)
            {
                if (m_BattleTime > 15) Fail("首波场外怪物未通过真实 AI 进入画面");
                return;
            }
            foreach (MonsterEntity monster in monsters)
            {
                AttackInstance attack = AttackInstance.Create(0, default, 100000f, DamageKind.Real, Vector2.Zero,
                    m_Hero.Id, 0, SoundId.None);
                monster.ReceiveHit(attack, m_Hero.Id);
                ReferencePool.Release(attack);
                // 同一死亡再发一次普通事件，刷怪服务必须只发一次奖励和清波名额。
                GF.Event.Fire(this, MonsterDiedEventArgs.Create(monster.Id,
                    SmokeInspection.ReadField<int>(monster, "m_ExperienceReward"), monster.Config, monster.GlobalPosition));
            }

            return;
        }

        bool right = m_Step == Step.RightWall;
        Move(right ? "move_right" : "move_left");
        bool nearWall = right ? m_Hero.GlobalPosition.X > region.Right - 60f : m_Hero.GlobalPosition.X < region.Left + 60f;
        m_BlockedFrames = nearWall && Math.Abs(m_Hero.GlobalPosition.X - m_LastPlayerX) < 0.1f ? m_BlockedFrames + 1 : 0;
        if (m_BlockedFrames >= 20)
        {
            // 对照官方屏幕内侧 20px 的角色碰撞边界，不把阶段锚点当作身体可达位置。
            CollisionShape2D shape = m_Hero.GetNode<CollisionShape2D>("CollisionShape2D");
            Rect2 body = shape.Shape.GetRect();
            float edge = (shape.GlobalTransform * (right ? body.End : body.Position)).X;
            float screenEdge = m_Camera.GetScreenCenterPosition().X + (right ? 0.5f : -0.5f) * m_Camera.ViewSize.X;
            float margin = right ? screenEdge - edge : edge - screenEdge;
            if (Math.Abs(margin - 20f) > 1f) Fail($"角色屏幕边界留白错误：{margin:0.00}px");
            // 第二阶段额外验证左门；其余阶段在右墙前清波，覆盖最容易发生相机跳变的站位。
            if (right && stage.StageOrder == 2)
            {
                m_Step = Step.LeftWall;
                // 通过真实移动让相机回退；传送到视野外无法验证随屏幕移动的边界。
            }
            else
            {
                m_Step = Step.Kill;
            }

            m_BlockedFrames = 0;
        }
    }

    /// <summary>记录第三阶段真实取景中的三个平台。</summary>
    /// <returns>画面保存任务。</returns>
    private async Task CapturePlatformsAsync()
    {
        if (DisplayServer.GetName() == "headless") return;
        await m_Hero.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string path = ProjectSettings.GlobalizePath("res://.godot/validation/stage_3_platforms.png");
        if (m_Hero.GetViewport().GetTexture().GetImage().SavePng(path) != Error.Ok)
            throw new IOException("第三阶段平台截图失败。");
    }

    private void PlaceForWallTest(float x) // 把玩家放到待测墙边，避免中途被地形或怪物的身体卡住。
    {
        m_Hero.GlobalPosition = new Vector2(x, m_Hero.GlobalPosition.Y);
        m_Hero.Velocity = Vector2.Zero;
        m_LastPlayerX = x;
    }

    private static void Move(string action) // 每帧只保持一个移动方向，结束时释放全部测试输入。
    {
        foreach (string direction in new[] { "move_left", "move_right" })
        {
            if (direction == action)
            {
                Input.ActionPress(direction);
            }
            else
            {
                Input.ActionRelease(direction);
            }
        }
    }

    private void Fail(string message) // 留下可读证据并结束自动输入，协议输出由驱动器统一负责。
    {
        m_Failures.Add(message);
        IsDone = true;
    }
}
