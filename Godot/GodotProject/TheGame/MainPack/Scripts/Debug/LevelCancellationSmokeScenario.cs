using System.Collections.Generic;
using System.Linq;
using GameConfig.Level;
using GameLogic.Enemies;
using GameLogic.Heroes;
using GameLogic.Input;
using GameLogic.Level;
using Godot;
using GodotGameFramework;

/// <summary>
/// 在怪物异步显示尚未完成时停止关卡，验证旧结果被原会话回收。
/// </summary>
public sealed class LevelCancellationSmokeScenario
{
    private readonly AbstractHeroEntity _hero; // 驱动实际移动的玩家。
    private readonly LevelViewController _level; // 受测会话。
    private readonly List<string> _failures = new(); // 取消与回收的断言结果。
    private double _time; // 场景等待时间。
    private double _stopTime; // 停止会话之后经过的游戏秒数。
    private bool _stopped; // 已主动停止会话，正在等待在途资源完成。

    /// <summary>
    /// 回归已经完成。
    /// </summary>
    public bool IsDone { get; private set; }

    /// <summary>
    /// 失败原因。
    /// </summary>
    public IReadOnlyList<string> Failures => _failures;

    /// <summary>
    /// 创建取消回归场景。
    /// </summary>
    /// <param name="hero">当前测试借用的英雄实体。</param>
    /// <param name="level">当前测试借用的关卡控制器。</param>
    public LevelCancellationSmokeScenario(AbstractHeroEntity hero, LevelViewController level)
    {
        _hero = hero;
        _level = level;
        // 构造即有失败清理契约：尚未绑定架构和节点时，也能重复结束。
        var uninitialized = new LevelViewController();
        try
        {
            uninitialized.Cleanup();
            uninitialized.Cleanup();
            GD.Print("SMOKE-LEVEL-INIT PASS: cleanup before initialization is repeatable.");
        }
        catch (System.Exception error)
        {
            _failures.Add($"未初始化的关卡无法清理：{error.Message}");
        }
        finally
        {
            uninitialized.Free();
        }
    }

    /// <summary>
    /// 驱动相机抵达，提交真实异步显示，随后停止并观测晚到结果。
    /// </summary>
    /// <param name="delta">本次逻辑推进的秒数。</param>
    public void Update(double delta)
    {
        _time += delta;
        if (!_stopped && SmokeInspection.Sequence(_level).Phase == LevelStagePhase.Fighting)
        {
            // 首次怪物场景尚未进池，按正式配方首只等待时间强制到期，覆盖真实在途边界。
            LevelStage stage = ConfigSystem.Instance.Tables.TbLevelConfig.Get(1).Stages[0];
            SmokeInspection.Scene(_level).GetNode<LevelSpawnerNode>("Spawner")._Process(stage.SpawnDelay + stage.Recipes.Min(x => x.Delay) + 0.01);
            int pending = GF.Entity.GetAllLoadingEntityIds().Length;
            if (pending == 0)
            {
                _failures.Add("未观察到在途显示，取消用例没有覆盖异步边界");
            }

            _level.Cleanup();
            _stopped = true;
            Input.ActionRelease(InputSystem.MoveRight);
            Input.ActionRelease(InputSystem.Jump);
            GD.Print($"SMOKE-CANCEL: stopped with {pending} pending shows");
        }

        if (_stopped)
        {
            _stopTime += delta;
            if (_stopTime >= 3.0 && GF.Entity.GetAllLoadingEntityIds().Length == 0)
            {
                bool shown = _hero.GetTree().Root.FindChildren("*", "CharacterBody2D", true, false)
                    .OfType<AbstractEnemyEntity>().Any(x => x.IsShown);
                if (shown || SmokeInspection.Sequence(_level).Phase != LevelStagePhase.Stopped)
                {
                    _failures.Add("停止之后仍有怪物显示或关卡继续推进");
                }

                IsDone = true;
            }
        }
        else
        {
            Input.ActionPress(InputSystem.MoveRight);
            if (_hero.IsOnWall() && _hero.IsOnFloor())
            {
                Input.ActionPress(InputSystem.Jump);
            }
            else
            {
                Input.ActionRelease(InputSystem.Jump);
            }
        }

        if (_time > 30)
        {
            _failures.Add("取消回归超时");
            IsDone = true;
        }
    }
}
