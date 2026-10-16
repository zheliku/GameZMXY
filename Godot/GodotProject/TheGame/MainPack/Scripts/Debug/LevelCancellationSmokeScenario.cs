using GameLogic.Level;
using GameLogic.Input;
using System.Collections.Generic;
using System.Linq;
using GameLogic.Heroes;
using GameLogic.Monsters;
using Godot;
using GodotGameFramework;

/// <summary>在怪物异步显示尚未完成时停止关卡，验证旧结果被原会话回收。</summary>
public sealed class LevelCancellationSmokeScenario
{
    private readonly AbstractHeroEntity m_Hero; // 驱动实际移动的玩家。
    private readonly LevelViewController m_Level; // 受测会话。
    private readonly List<string> m_Failures = new(); // 取消与回收的断言结果。
    private double m_Time; // 场景等待时间。
    private double m_StopTime; // 停止会话之后经过的游戏秒数。
    private bool m_Stopped; // 已主动停止会话，正在等待在途资源完成。

    /// <summary>回归已经完成。</summary>
    public bool IsDone { get; private set; }

    /// <summary>失败原因。</summary>
    public IReadOnlyList<string> Failures => m_Failures;

    /// <summary>创建取消回归场景。</summary>
    public LevelCancellationSmokeScenario(AbstractHeroEntity hero, LevelViewController level)
    {
        m_Hero = hero;
        m_Level = level;
    }

    /// <summary>驱动相机抵达，提交真实异步显示，随后停止并观测晚到结果。</summary>
    public void Update(double delta)
    {
        m_Time += delta;
        if (!m_Stopped && SmokeInspection.Sequence(m_Level).Phase == LevelStagePhase.Fighting)
        {
            // 首次怪物场景尚未进池，按正式配方首只等待时间强制到期，覆盖真实在途边界。
            var stage = ConfigSystem.Instance.Tables.TbLevelConfig.Get(1).Stages[0];
            SmokeInspection.Scene(m_Level).GetNode<LevelSpawnerNode>("Spawner")._Process(stage.SpawnDelay + stage.Recipes.Min(x => x.Delay) + 0.01);
            int pending = GF.Entity.GetAllLoadingEntityIds().Length;
            if (pending == 0)
            {
                m_Failures.Add("未观察到在途显示，取消用例没有覆盖异步边界");
            }

            m_Level.Cleanup();
            m_Stopped = true;
            Input.ActionRelease(InputSystem.MoveRight);
            Input.ActionRelease(InputSystem.Jump);
            GD.Print($"SMOKE-CANCEL: stopped with {pending} pending shows");
        }

        if (m_Stopped)
        {
            m_StopTime += delta;
            if (m_StopTime >= 3.0 && GF.Entity.GetAllLoadingEntityIds().Length == 0)
            {
                bool shown = m_Hero.GetTree().Root.FindChildren("*", "CharacterBody2D", true, false)
                    .OfType<AbstractMonsterEntity>().Any(x => x.IsShown);
                if (shown || SmokeInspection.Sequence(m_Level).Phase != LevelStagePhase.Stopped)
                {
                    m_Failures.Add("停止之后仍有怪物显示或关卡继续推进");
                }

                IsDone = true;
            }
        }
        else
        {
            Input.ActionPress(InputSystem.MoveRight);
            if (m_Hero.IsOnWall() && m_Hero.IsOnFloor())
            {
                Input.ActionPress(InputSystem.Jump);
            }
            else
            {
                Input.ActionRelease(InputSystem.Jump);
            }
        }

        if (m_Time > 30)
        {
            m_Failures.Add("取消回归超时");
            IsDone = true;
        }
    }
}
