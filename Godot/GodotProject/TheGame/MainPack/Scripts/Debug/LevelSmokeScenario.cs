using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Battle;
using GameConfig.Sound;
using GameFramework;
using GameLogic.Battle;
using GameLogic.Entity.Heroes;
using GameLogic.Entity.Monsters;
using GameLogic.Level;
using Godot;

/// <summary>通过真实移动、碰撞和死亡结算验证 Level_1 的四段相机抵达与清波流程。</summary>
public sealed class LevelSmokeScenario
{
    private enum Step // 单段战斗内的物理约束验证顺序。
    {
        RightWall,
        LeftWall,
        Kill,
    }

    private readonly HeroEntity m_Hero; // 通过游戏流程实际显示的玩家。
    private readonly LevelController m_Level; // 受测关卡会话。
    private readonly LevelCamera m_Camera; // 受测实际取景。
    private readonly LevelStageGateSet m_Gates; // 受测阶段区域。
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

    /// <summary>创建正式关卡的物理回归场景。</summary>
    public LevelSmokeScenario(HeroEntity hero, LevelController level)
    {
        m_Hero = hero;
        m_Level = level;
        m_Camera = level.GetNode<LevelCamera>("Camera2D");
        m_Gates = level.GetNode<LevelStageGateSet>("World/StageGates");
        m_LastCenter = m_Camera.GlobalPosition.X;
        m_LastPlayerX = hero.GlobalPosition.X;
    }

    /// <summary>物理回归已完成或遇到失败。</summary>
    public bool IsDone { get; private set; }

    /// <summary>完成时的失败原因列表。</summary>
    public IReadOnlyList<string> Failures => m_Failures;

    /// <summary>在相机和实体物理更新之后观测，驱动真实输入和测试攻击。</summary>
    public void Update(double delta)
    {
        m_Time += delta;
        float center = m_Camera.GlobalPosition.X;
        if (Math.Abs(center - m_LastCenter) > 600f * delta + 0.1f)
        {
            Fail($"相机帧位移超限：{m_LastCenter} → {center}，阶段 {m_Level.StageOrder}");
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
            monster.SetAiEnabled(false);
            m_Seen.Add(monster.Id);
        }

        if (m_Level.Phase == LevelStagePhase.Travelling)
        {
            if (monsters.Length > 0)
            {
                Fail("相机抵达之前生成了下一阶段敌人");
            }

            // 清波后到下一场开战之间，右侧应亮起前进提示（旧项目 role_information.gogo）。
            if (m_Level.GoHintVisible)
            {
                m_GoHintSeen = true;
            }

            Move("move_right");
            // 用户保留的斜坡含竖直端面，烟测用真实跳跃跨越，不修改场景地形。
            if (m_Hero.IsOnWall() && m_Hero.IsOnFloor())
            {
                Input.ActionPress("jump");
            }
            else
            {
                Input.ActionRelease("jump");
            }
        }
        else if (m_Level.Phase == LevelStagePhase.Fighting)
        {
            if (m_Level.GoHintVisible)
            {
                Fail("开战后前进提示仍然可见");
            }

            DriveBattle(delta, monsters);
        }
        else if (m_Level.Phase == LevelStagePhase.Completed)
        {
            int expected = ConfigSystem.Instance.Tables.TbLevelConfig.Get(1).Stages.Sum(x => x.Recipes.Sum(r => r.Count));
            if (m_Seen.Count != expected)
            {
                Fail($"怪物总数错误：实际 {m_Seen.Count}，配置 {expected}");
            }

            if (!m_GoHintSeen)
            {
                Fail("清波后未出现前进提示（Go）");
            }

            IsDone = true;
        }

        if (m_Time > 180)
        {
            Fail($"超时：阶段 {m_Level.StageOrder}/{m_Level.Phase}，玩家 {m_Hero.GlobalPosition}，相机 {center}");
        }

        m_LastPlayerX = m_Hero.GlobalPosition.X;
        if (IsDone)
        {
            Move(null);
        }
    }

    private void DriveBattle(double delta, MonsterEntity[] monsters) // 先验证抵达时取景和配置延迟，再验证墙体与真实清波。
    {
        var stage = ConfigSystem.Instance.Tables.TbLevelConfig.Get(1).Stages[m_Level.StageOrder - 1];
        var region = m_Gates.BoundsOf(stage.StageOrder);
        if (m_StageOrder != stage.StageOrder)
        {
            m_StageOrder = stage.StageOrder;
            m_BattleTime = 0;
            m_Step = Step.RightWall;
            m_BlockedFrames = 0;
            if (Math.Abs(m_Camera.ViewRight - region.Right) > 0.1f ||
                Math.Abs(m_Hero.GlobalPosition.X - m_Camera.GlobalPosition.X) > 60f)
            {
                Fail("开战时相机未抵达右界，或玩家不在中央死区附近");
            }

            GD.Print($"SMOKE-LEVEL: stage {stage.StageOrder} arrived, camera={m_Camera.GlobalPosition.X:0.0}, player={m_Hero.GlobalPosition.X:0.0}");
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
            foreach (MonsterEntity monster in monsters)
            {
                AttackData attack = AttackData.Create(0, default, 100000f, DamageKind.Real, Vector2.Zero,
                    m_Hero.Id, 0, SoundId.None);
                monster.ReceiveHit(attack, m_Hero.Id);
                ReferencePool.Release(attack);
            }

            return;
        }

        bool right = m_Step == Step.RightWall;
        Move(right ? "move_right" : "move_left");
        bool nearWall = right ? m_Hero.GlobalPosition.X > region.Right - 60f : m_Hero.GlobalPosition.X < region.Left + 60f;
        m_BlockedFrames = nearWall && Math.Abs(m_Hero.GlobalPosition.X - m_LastPlayerX) < 0.1f ? m_BlockedFrames + 1 : 0;
        if (m_BlockedFrames >= 20)
        {
            // 第二阶段额外验证左门；其余阶段在右墙前清波，覆盖最容易发生相机跳变的站位。
            if (right && stage.StageOrder == 2)
            {
                m_Step = Step.LeftWall;
                PlaceForWallTest(region.Left + 40f);
            }
            else
            {
                m_Step = Step.Kill;
            }

            m_BlockedFrames = 0;
        }
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
