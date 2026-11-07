using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Battle;
using GameConfig.Sound;
using GameFramework;
using GameFramework.Event;
using GameLogic;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attacks;
using GameLogic.Enemies;
using GameLogic.Event.Combat;
using GameLogic.FloatingText;
using GameLogic.Heroes;
using GameLogic.Input;
using GameLogic.Level;
using GameLogic.UI;
using Godot;
using GodotGameFramework;

/// <summary>
/// 冒烟测试驱动器（开发工具）：仅当以 `-- --smoketest` 启动时生效，平时零开销惰性。
/// 自动注入输入（普攻连打 / 双击跑 / 受击 / 一段跳 / 二段跳），并断言悟空身体状态机与
/// AnimationPlayer 的流转，打印 `SMOKE PASS` / `SMOKE FAIL`（失败附完整观察序列）。
///
/// 它验证的是"身体状态机 → 动画播放"这条链路：观测点同时包含身体状态名与 AnimationPlayer 里真正在播的
/// 动画，不是 C# 自己的日志——所以能抓住"状态切了但动画没切"这一类问题。
///
/// 用法：S:\Godot4\Godot4CSharp_console.exe --headless --path Godot/GodotProject --quit-after N -- --smoketest[=ai]
///   `--smoketest`     英雄控制器 + M4 命中（猴子 AI 冻结为沙包，断言确定；N = 1800）
///   `--smoketest=ai`  怪物 AI（巡逻/追击/出招/转身/平台守候/丢失目标/受控/死亡）。
///   `--smoketest=level` 正式关卡四段移动、锁屏、刷怪与清波。
///   `--smoketest=level5` 运行官方 2-2 七星洞配置与场景。
///   `--smoketest=level-cancel` 在途显示取消与晚到结果回收。
///   `--smoketest=ui` HUD 属性更新、退订复用、实体复用、失败清理与隔离存档重入。
///
/// 关卡首波怪物由相机抵达首阶段右界激活（Level_1 的相机右缘），因此两种模式都先注入移动输入
/// 推动相机抵达后才启动既有战斗时间轴；M4 连打验证沙包，AI 模式验证怪物行为。
///
/// 判定同时检查 stdout 的 SMOKE PASS/FAIL 和进程退出码；预期失败注入、超时和引擎退出诊断
/// 分别记录在 Docs/Validation.md，不能仅凭通用尾句断言全部专项通过。
/// </summary>
public partial class SmokeTestDriver : Node
{
    private static readonly (double At, string Action, string Mode)[] s_plan =
    {
        (2.00, "attack", "mash"),
        (4.60, "attack", "release"),
        (5.00, "move_right", "hold"),
        (5.10, "move_right", "release"),
        (5.20, "move_right", "hold"),
        (6.30, "move_right", "release"),
        (6.35, "hurt", "damage"),
        // 官方 hurt 为15源帧（0.625秒）；跳跃输入在硬直中作废，等硬直结束再测一段跳/二段跳。
        (7.10, "jump", "tap"),
        (7.55, "jump", "tap"),
        (7.70, "attack", "tap"),
        // 首阶段已在准备阶段激活；第二轮连打覆盖跳跃后落地的真实命中链路。
        (8.30, "attack", "mash"),
        (11.00, "attack", "release"),
        (13.00, "attack", "mash-four"),
    }; // 按时间安排英雄烟测的输入动作。

    private static readonly string[] s_required =
    {
        "Attack/attack_1", "Attack/attack_2", "Attack/attack_3", "Attack/attack_4", "Attack/attack_5",
        "Ground/run", "Hurt/hurt", "Air/jump", "Air/fall", "Air/jump_2", "Ground/idle1",
    }; // 必须观测到的身体状态与实际动画组合。

    private static readonly (string A, string B, bool LastA, bool LastB)[] s_order =
    {
        ("Attack/attack_1", "Attack/attack_2", false, false),
        ("Attack/attack_2", "Attack/attack_3", false, false),
        ("Attack/attack_3", "Attack/attack_4", false, false),
        ("Attack/attack_4", "Attack/attack_5", false, false),
        ("Attack/attack_5", "Ground/run", false, false),
        ("Ground/run", "Hurt/hurt", false, false),
        ("Hurt/hurt", "Air/jump", false, false),
        ("Air/jump", "Air/jump_2", false, false),
        ("Air/jump", "Air/fall", false, true),      // 出生时也会下落，取最后一次 fall（起跳后的下落）
        ("Air/jump_2", "Ground/idle1", false, true), // 落地后回待机，取最后一次 idle1
    }; // 必须按顺序出现的状态与动画组合。

    private const double EndTime = 17.5; // 包含第四段松手回归的英雄烟测时长（秒）。
    private const double FindTimeout = 5.0; // 等待实体出现的超时时间（秒）。

    private const float SandbagOffset = 60f; // 沙包站在可见棍身范围内（像素）；越界不命中由逐帧物理验证覆盖。

    private AbstractHeroEntity _hero; // 烟测中的英雄实体。

    // ---- M4 命中链路观测（悟空连打面前的猴子）----

    private AbstractEnemyEntity _monster; // 沙包猴子（场上第一只怪物）。

    private readonly List<(int Damage, bool Crit, bool Miss)> _monsterHits = new(); // 按事件记录猴子每次受击结果。

    private int _maxPopCount; // 场上飘字节点的可见峰值数量。
    private int _maxComboCount; // 烟测期间由真实命中累计到的连击峰值。
    private bool _comboHudShown; // 实际连击数字曾在战斗HUD显示。
    private SmokeMode _mode; // 命令行选择的唯一冒烟模式。
    private int _requestedLevel = 1; // 真实地图点击的关卡，后续关卡仍走完整菜单流程。
    private double _waitTime; // 等待英雄出现的累计时间（秒）。
    private double _time; // 烟测当前时间（秒）。
    private int _nextStep; // 下一条输入计划索引。
    private bool _mashing; // 是否正在交替注入攻击输入。
    private double _mashUntil; // 攻击连打结束时间（秒）。
    private int _mashFrame; // 攻击连打帧计数。
    private bool _stopAfterFourth; // 本轮到第四段后停止输入，检查第五段不会被提前排队。
    private double _fourthReachedAt; // 第四段开始时刻，零表示尚未观察到。
    private string _lastPath = ""; // 上一次记录的身体状态与动画路径。
    private bool _hasQuitAfter; // 命令行是否包含引擎自动退出参数。
    private readonly List<(double Time, string Path)> _observed = new(); // 按时间记录身体状态与动画路径。

    private readonly HashSet<int> _walkFrames = new(); // walk 期间采样到的身体帧号。
    private readonly HashSet<int> _runFrames = new(); // run 期间采样到的身体帧号。

    private LevelSmokeScenario _levelScenario; // 正式关卡四段流程的观测与输入计划。
    private LevelCancellationSmokeScenario _cancelScenario; // 真实在途显示的取消回归。
    private UiSmokeScenario _uiScenario; // HUD 与存档的真实引擎回归。
    private FrontendSmokeScenario _frontendScenario; // 菜单、音乐与进出关卡回归。
    private MapProgressSmokeScenario _progressScenario; // 主线实际通关和退出重读后的地图解锁验证。

    private EnemyAiSmokeScenario _aiScenario; // ai 模式下驱动英雄与猴子的场景。

    private double _aiStartTime; // AI 烟测场景的开始时间。

    private double _travelTime; // 战斗烟测进入首阶段的准备时间。
    private bool _stageEntered; // 相机已到达首阶段右界，战斗时间轴可开始。
    private LevelViewController _level; // 烟测观测的关卡会话。

    private double _monsterFoundAt = -1.0; // 找到受测猴子的时间（秒）；-1 表示尚未找到。

    private readonly HashSet<int> _frozenMonsters = new(); // 已冻结为沙包的怪物 runtime ID。

    /// <summary>
    /// 读取烟测参数并设置物理处理优先级。
    /// </summary>
    public override void _Ready()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            _mode = arg switch
            {
                "--smoketest" => SmokeMode.Hero,
                "--smoketest=ai" => SmokeMode.EnemyAi,
                "--smoketest=level" => SmokeMode.Level,
                "--smoketest=level2" => SmokeMode.Level,
                "--smoketest=level3" => SmokeMode.Level,
                "--smoketest=level4" => SmokeMode.Level,
                "--smoketest=level5" => SmokeMode.Level,
                "--smoketest=level6" => SmokeMode.Level,
                "--smoketest=level7" => SmokeMode.Level,
                "--smoketest=level8" => SmokeMode.Level,
                "--smoketest=level9" => SmokeMode.Level,
                "--smoketest=level10" => SmokeMode.Level,
                "--smoketest=level-cancel" => SmokeMode.Cancellation,
                "--smoketest=ui" => SmokeMode.Ui,
                "--smoketest=bosses" => SmokeMode.Bosses,
                "--smoketest=frontend" => SmokeMode.Frontend,
                "--smoketest=progress" => SmokeMode.Progress,
                _ => _mode,
            };
            if (arg == "--smoketest=level2")
            {
                _requestedLevel = 2;
            }

            if (arg == "--smoketest=level3")
            {
                _requestedLevel = 3;
            }

            if (arg == "--smoketest=level4")
            {
                _requestedLevel = 4;
            }

            if (arg == "--smoketest=level5")
            {
                _requestedLevel = 5;
            }

            if (arg == "--smoketest=level6")
            {
                _requestedLevel = 6;
            }

            if (arg == "--garuda-flow-only")
            {
                _requestedLevel = 6;
            }

            if (arg is "--smoketest=level7" or "--sea-flow-only")
            {
                _requestedLevel = 7;
            }

            if (arg is "--smoketest=level8" or "--east-sea-flow-only")
            {
                _requestedLevel = 8;
            }

            if (arg is "--smoketest=level9" or "--shark-flow-only")
            {
                _requestedLevel = 9;
            }

            if (arg is "--smoketest=level10" or "--bull-only" or "--bull-flow-only" or "--bull-capture-only")
            {
                _requestedLevel = 10;
            }
        }

        // 引擎参数里带了 --quit-after 时，通过流程交给引擎自己退出（见 StopDriving 注释）
        foreach (string arg in OS.GetCmdlineArgs())
        {
            if (arg == "--quit-after")
            {
                _hasQuitAfter = true;
            }
        }

        if (_mode == SmokeMode.Disabled)
        {
            SetPhysicsProcess(false);
        }
        else
        {
            // 每次烟测使用独立存档目录，自动保存和失败测试不触碰玩家实际进度。
            string archive = OS.GetCmdlineUserArgs().FirstOrDefault(argument => argument.StartsWith("--smoke-run-id="));
            Guid runId = archive == null ? Guid.NewGuid() : Guid.Parse(archive["--smoke-run-id=".Length..]);
            GF.Archive.Setting.Folder = $"Validation/{_mode}/{runId:N}";
            GD.Print($"SMOKE ARCHIVE: user://{GF.Archive.Setting.Folder}");
        }

        // 排在实体与 AnimationPlayer 之后处理：采样到的才是"本帧更新完"的状态，
        // 否则读到的永远是上一帧的播放状态（会误判成"动画晚一帧"）。
        ProcessPriority = 1000;
        ProcessPhysicsPriority = 1000;
    }

    /// <summary>
    /// 每物理帧驱动输入、实体采样及烟测断言。
    /// </summary>
    /// <param name="delta">上一帧到当前帧的秒数。</param>
    public override void _PhysicsProcess(double delta)
    {
        if (_mode == SmokeMode.Progress)
        {
            _progressScenario ??= new MapProgressSmokeScenario(this);
            _progressScenario.Update(delta);
            if (_progressScenario.IsDone)
            {
                if (_progressScenario.Failure != null)
                {
                    Fail(_progressScenario.Failure);
                }
                else
                {
                    GD.Print("SMOKE-PROGRESS PASS: ten real mainline clears, disk saves, title reload, hero identity, only latest hint and locked input rejection.");
                    StopDriving(false);
                }
            }
            return;
        }
        if (_mode == SmokeMode.Frontend)
        {
            _frontendScenario ??= new FrontendSmokeScenario(this);
            if (_frontendScenario.IsDone)
            {
                if (_frontendScenario.Failure != null)
                {
                    Fail(_frontendScenario.Failure);
                }
                else
                {
                    GD.Print("SMOKE PASS：开始、六格存档、选角、地图、花果山、设置保存后返回、提示复用与 GGF 声音全部通过");
                    StopDriving(false);
                }
            }
            return;
        }
        if (_mode == SmokeMode.Level && _levelScenario != null)
        {
            DriveLevelScenario(delta);
            return;
        }
        if (_mode is SmokeMode.Ui or SmokeMode.Bosses && _uiScenario != null)
        {
            // 初次进关后由专项拥有导航，不能在它观察返回地图时自动再次点击地图。
            DriveUiScenario(delta);
            return;
        }
        // 既有战斗回归也从真实菜单进入，不给生产流程增加测试专用跳转。
        if (!FrontendSmokeScenario.AdvanceToLevel(_requestedLevel))
        {
            return;
        }

        if (_mode is SmokeMode.Ui or SmokeMode.Bosses)
        {
            DriveUiScenario(delta);
            return;
        }
        if (_hero == null)
        {
            _waitTime += delta;
            if (_waitTime > FindTimeout)
            {
                Fail("5 秒内没有找到英雄（游戏没起来？）");
            }

            // FindChildren 的类型过滤只认引擎原生类名（C# 脚本类名不匹配），按 CharacterBody2D 取再判脚本类型
            foreach (Node node in GetTree().Root.FindChildren("*", "CharacterBody2D", true, false))
            {
                if (node is AbstractHeroEntity hero)
                {
                    _hero = hero;
                    break;
                }
            }

            if (_hero == null)
            {
                return;
            }

            GF.Event.Subscribe(DamageDealtEventArgs.EventId, OnDamageDealt);
            GD.Print("SMOKE: 找到 hero，开始时间轴");
        }

        if (_mode == SmokeMode.Cancellation)
        {
            DriveCancellationScenario(delta);
            return;
        }

        if (_mode == SmokeMode.Level)
        {
            DriveLevelScenario(delta);
            return;
        }

        FindMonster();
        if (!_stageEntered)
        {
            DriveStageEntry(delta);
            return;
        }

        _time += delta;
        if (_mode == SmokeMode.EnemyAi)
        {
            DriveAiScenario();
            return;
        }

        DriveInput();
        Sample();
        TrackBodyFrames();
        TrackPops();
        TrackCombo();
        if (_time >= EndTime)
        {
            Finish();
        }
    }

    /// <summary>
    /// 等待正式 HUD 就绪，再驱动真实 UI 生命周期与存档回归。
    /// </summary>
    /// <param name="delta">物理帧间隔秒数。</param>
    private void DriveUiScenario(double delta)
    {
        if (_uiScenario == null)
        {
            // 场景自行等待关卡流程装配完成。
            _uiScenario = new UiSmokeScenario(this, _mode == SmokeMode.Bosses);
            return;
        }

        if (_uiScenario.IsDone)
        {
            if (_uiScenario.Failure == null)
            {
                GD.Print(_mode == SmokeMode.Bosses
                    ? "SMOKE PASS：两位 BOSS 的原生招式、实际命中、无敌、独立风刃与回池复用全部通过"
                    : "SMOKE PASS：UI 数值、解绑复用、实体复用、加载取消、失败清理与存档重入全部通过");
                StopDriving(false);
            }
            else
            {
                Fail(_uiScenario.Failure);
            }
        }
    }

    /// <summary>
    /// 按时间计划注入测试输入。
    /// </summary>
    private void DriveInput()
    {
        // 先维持连打期间的交替按键，再执行所有已到时刻的计划动作。
        if (_mashing)
        {
            if (_stopAfterFourth && _fourthReachedAt == 0 && _hero.AttackSegment == 3)
            {
                _fourthReachedAt = _time;
                _mashUntil = _time + 0.12; // 继续注入过早的普攻，再彻底松开。
            }
            _mashFrame++;
            if (_time >= _mashUntil)
            {
                Input.ActionRelease(InputSystem.Attack);
                _mashing = false;
            }
            else if (_mashFrame % 2 == 0)
            {
                Input.ActionPress(InputSystem.Attack);
            }
            else
            {
                Input.ActionRelease(InputSystem.Attack);
            }
        }

        while (_nextStep < s_plan.Length && _time >= s_plan[_nextStep].At)
        {
            (double _, string action, string mode) = s_plan[_nextStep];
            switch (mode)
            {
                case "tap":
                    Input.ActionPress(action);
                    Input.ActionRelease(action);
                    break;
                case "hold":
                    Input.ActionPress(action);
                    break;
                case "release":
                    Input.ActionRelease(action);
                    break;
                case "mash-four":
                    _stopAfterFourth = true;
                    goto case "mash";
                case "mash":
                    _mashing = true;
                    _mashUntil = _time + 2.0;
                    _mashFrame = 0;
                    break;
                case "damage":
                    HurtHeroOnce();   // 验证受击：硬直 + hurt 动画（走真实结算链路）
                    break;
            }

            _nextStep++;
        }
    }

    /// <summary>
    /// 经真实战斗结算链路对英雄造成一次 1 点伤害。
    /// </summary>
    private void HurtHeroOnce()
    {
        // 临时攻击数据只在结算期间持有，并在调用后归还对象池。
        var data = AttackData.Create(0, default, 1f, DamageKind.Real, Vector2.Zero, -1, 0);
        _hero.ReceiveHit(data);
        ReferencePool.Release(data);
    }

    /// <summary>
    /// 等待测试实体并推进 AI 场景断言。
    /// </summary>
    private void DriveAiScenario()
    {
        // 场景只在英雄与猴子都准备好后创建，避免测试时钟先行。
        if (_aiScenario == null)
        {
            if (_monster == null)
            {
                if (_time > FindTimeout + 2.0)
                {
                    Fail("相机抵达首阶段后 7 秒内没有找到猴子");
                }

                return;
            }

            // 清除夹具生成前继承的目标，让首次索敌从真正无目标状态开始。
            typeof(AbstractEnemyEntity).GetField("_target", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(_monster, null);
            _aiScenario = new EnemyAiSmokeScenario(_hero, _monster);
            _aiStartTime = _time;
            GD.Print("SMOKE-AI: 开始怪物 AI 场景");
        }

        double t = _time - _aiStartTime;
        // 每帧驱动场景一次；未结束时等待下一帧，结束时统一采集断言并关闭。
        _aiScenario.Update(t);
        if (!_aiScenario.IsDone)
        {
            return;
        }

        List<string> failures = _aiScenario.Finish();
        _aiScenario = null;
        if (failures.Count == 0)
        {
            GD.Print("SMOKE PASS：怪物 AI 场景断言全部通过");
            StopDriving(false);
        }
        else
        {
            Fail(string.Join("；", failures));
        }
    }

    /// <summary>
    /// 通过真实移动推动相机到首阶段右界，再启动既有战斗回归时间轴。
    /// </summary>
    /// <param name="delta">本次逻辑推进的秒数。</param>
    private void DriveStageEntry(double delta)
    {
        _travelTime += delta;
        foreach (Node node in GetTree().Root.FindChildren("*", "Node2D", true, false))
        {
            if (node is LevelViewController && SmokeInspection.Level(GF.Procedure.CurrentProcedure as ProcedureLevel) is LevelViewController level)
            {
                _level = level;
                break;
            }
        }

        if (SmokeInspection.Sequence(_level)?.Phase == LevelStagePhase.Fighting)
        {
            Input.ActionRelease(InputSystem.MoveRight);
            Input.ActionRelease(InputSystem.Jump);
            // 战斗回归需要平地，使用场景中已配置的平坦玩家出生点，避免斜坡改变高度断言。
            _hero.GlobalPosition = _level.PlayerSpawnPosition;
            _hero.Velocity = Vector2.Zero;
            GF.Fsm.GetSystem<InputSystem>().Reset(_hero.ActionData);
            _stageEntered = true;
            GD.Print("SMOKE: 相机抵达首阶段，开始战斗时间轴");
            return;
        }

        Input.ActionPress(InputSystem.MoveRight);
        if (_hero.IsOnWall() && _hero.IsOnFloor())
        {
            Input.ActionPress(InputSystem.Jump);
        }
        else
        {
            Input.ActionRelease(InputSystem.Jump);
        }
        if (_travelTime > 20.0)
        {
            Fail("20 秒内相机未抵达首阶段右界");
        }
    }

    /// <summary>
    /// 正式关卡烟测独立于战斗回归时间轴，按阶段事件推进。
    /// </summary>
    /// <param name="delta">本次逻辑推进的秒数。</param>
    private void DriveLevelScenario(double delta)
    {
        if (_levelScenario == null)
        {
            foreach (Node node in GetTree().Root.FindChildren("*", "Node2D", true, false))
            {
                if (node is LevelViewController && SmokeInspection.Level(GF.Procedure.CurrentProcedure as ProcedureLevel) is LevelViewController level && SmokeInspection.Sequence(level).Phase != LevelStagePhase.Ready)
                {
                    _levelScenario = new LevelSmokeScenario(_hero, level);
                    break;
                }
            }
        }

        if (_levelScenario == null)
        {
            return;
        }

        _levelScenario.Update(delta);
        if (_levelScenario.IsDone)
        {
            if (_levelScenario.Failures.Count == 0)
            {
                GD.Print($"SMOKE PASS：关卡 {_requestedLevel} 的阶段、相机、物理门、守关目标、W 出口、自动存档与地图重读全部通过");
                StopDriving(false);
            }
            else
            {
                Fail(string.Join("；", _levelScenario.Failures));
            }
        }
    }

    /// <summary>
    /// 单独验证停止时仍在加载的实体不会泄漏到场景。
    /// </summary>
    /// <param name="delta">本次逻辑推进的秒数。</param>
    private void DriveCancellationScenario(double delta)
    {
        if (_cancelScenario == null)
        {
            foreach (Node node in GetTree().Root.FindChildren("*", "Node2D", true, false))
            {
                if (node is LevelViewController && SmokeInspection.Level(GF.Procedure.CurrentProcedure as ProcedureLevel) is LevelViewController level && SmokeInspection.Sequence(level).Phase != LevelStagePhase.Ready)
                {
                    _cancelScenario = new LevelCancellationSmokeScenario(_hero, level);
                    break;
                }
            }
        }

        _cancelScenario?.Update(delta);
        if (_cancelScenario?.IsDone == true)
        {
            if (_cancelScenario.Failures.Count == 0)
            {
                GD.Print("SMOKE PASS：在途显示取消，晚到怪物已回收且关卡不再推进");
                StopDriving(false);
            }
            else
            {
                Fail(string.Join("；", _cancelScenario.Failures));
            }
        }
    }

    /// <summary>
    /// 记录身体状态和动画路径变化。
    /// </summary>
    private void Sample()
    {
        string path = ObservePath(SmokeInspection.BodyStateName(_hero), _hero.AnimPlayer.AssignedAnimation.ToString());
        if (path.Length == 0 || path == _lastPath)
        {
            return;
        }

        _lastPath = path;
        _observed.Add((_time, path));
        GD.Print($"SMOKE[{_time:F2}] 身体/动画 {path}  ({Facts()})");
    }

    /// <summary>
    /// 观测点 = `身体状态/播放器当前动画`（如 Attack/attack_2、Ground/idle1）。
    /// 动画为空（播放器还没播过）时返回空串，不计入观测。
    /// </summary>
    /// <param name="bodyState">当前身体状态名。</param>
    /// <param name="anim">当前动画名。</param>
    /// <returns>组合路径；动画为空时返回空串。</returns>
    public static string ObservePath(string bodyState, string anim) => anim.Length == 0 ? "" : $"{bodyState}/{anim}";

    /// <summary>
    /// 身体帧采样：walk/run 状态下记录 _body 的帧号。动画库若漏了 _body:frame 轨道，
    /// 帧会停在进入前的一个值上不动——这里用"出现过的不同帧数"抓这类回归。
    /// </summary>
    private void TrackBodyFrames() // 收集 walk/run 期间身体动画的帧变化证据。
    {
        if (_hero == null || SmokeInspection.ReadField<Node2D>(_hero, "_body") is not Sprite2D body)
        {
            return;
        }

        if (_lastPath == "Ground/walk")
        {
            _walkFrames.Add(body.Frame);
        }
        else if (_lastPath == "Ground/run")
        {
            _runFrames.Add(body.Frame);
        }
    }

    /// <summary>
    /// 生成英雄身体状态快照供烟测日志定位问题。
    /// </summary>
    /// <returns>供失败日志定位的身体状态与运动快照。</returns>
    private string Facts()
    {
        return $"move={_hero.ActionData.MoveAxis} run={_hero.ActionData.Running} jump={_hero.ActionData.JumpCount} "
            + $"seg={_hero.AttackSegment} dead={_hero.Dead} "
            + $"floor={_hero.IsOnFloor()} vy={_hero.Velocity.Y:F0}";
    }

    /// <summary>
    /// 汇总状态顺序、动画、移动帧和战斗断言并结束驱动。
    /// </summary>
    private void Finish()
    {
        // 汇总必须出现的观测点与先后约束。
        List<string> failures = new();
        foreach (string required in s_required)
        {
            if (LastIndex(required) < 0)
            {
                failures.Add($"未观察到 {required}");
            }
        }

        foreach ((string a, string b, bool lastA, bool lastB) in s_order)
        {
            int indexA = lastA ? LastIndex(a) : FirstIndex(a);
            int indexB = lastB ? LastIndex(b) : FirstIndex(b);
            if (indexA >= 0 && indexB >= 0 && indexA > indexB)
            {
                failures.Add($"顺序错误：{a} 晚于 {b}");
            }
        }

        // 补充检查动画层、移动帧和实际命中链路的回归条件。
        CheckEffectLayer(failures);
        CheckLocomotionFrames(failures);
        CheckAttackFinishTiming(failures);
        CheckCombat(failures);
        if (_fourthReachedAt == 0 || _observed.Any(item => item.Time >= 13 && item.Path == "Attack/attack_5"))
        {
            failures.Add("第四段提前输入并松手后仍补出了第五段，或未到达第四段");
        }

        if (failures.Count == 0)
        {
            GD.Print($"SMOKE PASS：观察到 {_observed.Count} 次状态切换，断言全部通过");
            StopDriving(false);
            return;
        }

        Fail(string.Join("；", failures));
    }

    /// <summary>
    /// 校验待机特效层已回到空白基准状态。
    /// </summary>
    /// <param name="failures">本次验证累积的失败说明。</param>
    private void CheckEffectLayer(List<string> failures)
    {
        if (_hero.GetNodeOrNull<Node2D>("m_EffectRoot/m_Effect") is not AnimatedSprite2D effect)
        {
            failures.Add("场景里找不到 m_EffectRoot/m_Effect");
            return;
        }

        if (effect.Animation.ToString() != "empty")
        {
            failures.Add($"待机时特效动画应为 empty，实际 {effect.Animation}");
        }

        if (!effect.Scale.IsEqualApprox(Vector2.One))
        {
            failures.Add($"待机时特效 scale 应为 (1,1)，实际 {effect.Scale}");
        }

        if (effect.Frame != 0)
        {
            failures.Add($"待机时特效帧应为 0，实际 {effect.Frame}");
        }
    }

    /// <summary>
    /// 校验走跑动画持续改变身体帧。
    /// </summary>
    /// <param name="failures">本次验证累积的失败说明。</param>
    private void CheckLocomotionFrames(List<string> failures)
    {
        if (_runFrames.Count < 3)
        {
            failures.Add($"run 期间身体帧没有在动（观察到 {_runFrames.Count} 个不同帧：{string.Join(",", _runFrames)}）");
        }

        if (_walkFrames.Count < 1)
        {
            failures.Add("walk 期间没有采样到身体帧");
        }
    }

    /// <summary>
    /// 校验普攻冷却与动画时长独立，连打不会提前越过冷却。
    /// </summary>
    /// <param name="failures">本次验证累积的失败说明。</param>
    private void CheckAttackFinishTiming(List<string> failures)
    {
        for (int seg = 1; seg < 5; seg++)
        {
            int indexA = FirstIndex($"Attack/attack_{seg}");
            int indexB = FirstIndex($"Attack/attack_{seg + 1}");
            if (indexA < 0 || indexB < 0)
            {
                continue;
            }

            double gap = _observed[indexB].Time - _observed[indexA].Time;
            double cooldown = _hero.Config.AttackInputCooldowns[Math.Max(seg - 2, 0)];
            double tick = 1d / Engine.PhysicsTicksPerSecond;
            if (gap < cooldown - tick || gap > cooldown + 3 * tick)
            {
                failures.Add($"连段 {seg}→{seg + 1} 间隔 {gap:F3}s 与输入冷却 {cooldown:F3}s 不符");
            }
        }
    }

    /// <summary>
    /// 找到场景猴子并按烟测模式调整其行为。
    /// </summary>
    private void FindMonster()
    {
        // 首波猴子在英雄走进首阶段右界后成批出现；除受测/沙包目标外的所有猴子都冻结成沙包，
        // 避免活怪攻击英雄带来不确定的受击/击退时序（AI 行为由 ai 场景单独覆盖）。
        foreach (Node node in GetTree().Root.FindChildren("*", "CharacterBody2D", true, false))
        {
            if (node is not AbstractEnemyEntity { IsShown: true } monster)
            {
                continue;
            }

            if (_monster == null)
            {
                _monster = monster;
                if (_mode == SmokeMode.EnemyAi)
                {
                    // 受测猴子在相机抵达后生成时会索敌到英雄；
                    // 先把夹具挪到视野外，AI专项开始时显式清除旧目标。
                    monster.GlobalPosition = new Vector2(_hero.GlobalPosition.X + 900f, monster.GlobalPosition.Y);
                    monster.Velocity = Vector2.Zero;
                    _monsterFoundAt = _time;
                    GD.Print($"SMOKE[{_time:F2}] 找到受测猴子 HP {monster.Hp}/{monster.MaxHp}，已挪出视野等待丢失目标");
                }
                else
                {
                    // M4 站位：悟空右侧、普攻判定范围内，供第二轮连打命中。
                    monster.GlobalPosition = new Vector2(_hero.GlobalPosition.X + SandbagOffset, monster.GlobalPosition.Y);
                    GD.Print($"SMOKE[{_time:F2}] 找到沙包猴子 HP {monster.Hp}/{monster.MaxHp} 位置 {monster.GlobalPosition}");
                }
            }

            // ai 模式保留受测怪物的 AI；M4 模式连同沙包一起全部冻结。
            if (!_frozenMonsters.Add(monster.Id) || (_mode == SmokeMode.EnemyAi && _monster == monster))
            {
                continue;
            }

            SmokeInspection.FreezeAi(monster);
            monster.Velocity = Vector2.Zero;
        }
    }

    /// <summary>
    /// 只记录烟测目标猴子的命中事件。
    /// </summary>
    /// <param name="sender">框架事件发送者。</param>
    /// <param name="args">当前同步回调借用的事件参数。</param>
    private void OnDamageDealt(object sender, GameEventArgs args)
    {
        if (args is DamageDealtEventArgs e && _monster != null && ReferenceEquals(sender, _monster))
        {
            _monsterHits.Add((e.Result.Damage, e.Result.IsCrit, e.Result.IsMiss));
            GD.Print($"SMOKE[{_time:F2}] 猴子受击 伤害={e.Result.Damage} 暴击={e.Result.IsCrit} 闪避={e.Result.IsMiss} 剩余HP={_monster.Hp} "
                + $"猴子x={_monster.GlobalPosition.X:F0} 英雄x={_hero.GlobalPosition.X:F0}");
        }
    }

    /// <summary>
    /// 统计当前场景可见飘字数量峰值。
    /// </summary>
    private void TrackPops()
    {
        int count = 0;
        // FindChildren 的类型过滤只认引擎原生类名（C# 脚本类名不匹配），按 Node2D 取再判脚本类型
        foreach (Node node in GetTree().Root.FindChildren("*", "Node2D", true, false))
        {
            if (node is FloatingTextNode pop && pop.Visible && pop.IsInsideTree() &&
                SmokeInspection.ReadField<object>(pop, "_kind").ToString() != "Recovery")
            {
                count++;
            }
        }

        _maxPopCount = Mathf.Max(_maxPopCount, count);
    }

    /// <summary>
    /// 记录权威连击数与实际HUD显示，不直接写入任何战斗状态。
    /// </summary>
    private void TrackCombo()
    {
        int count = GF.Fsm.GetArchitecture().GetModel<LevelModel>()?.ComboCount.Value ?? 0;
        _maxComboCount = Math.Max(_maxComboCount, count);
        _comboHudShown |= GetTree().Root.FindChildren("m_Combo", "Control", true, false)
            .OfType<GameLogic.UI.Widgets.ComboHud>().Any(hud => hud.Visible &&
                hud.GetChildren().OfType<AnimatedSprite2D>().Any(digit => digit.Visible));
    }

    /// <summary>
    /// 校验猴子受击、伤害结果与飘字创建。
    /// </summary>
    /// <param name="failures">本次验证累积的失败说明。</param>
    private void CheckCombat(List<string> failures)
    {
        if (_monster == null)
        {
            failures.Add("场上没有找到沙包猴子");
            return;
        }

        if (_monsterHits.Count == 0)
        {
            failures.Add("连打期间猴子没有受到任何命中");
            return;
        }

        int total = 0;
        // 按真实伤害事件累计并检查每次命中结果。
        foreach ((int damage, bool crit, bool miss) in _monsterHits)
        {
            total += damage;
            if (miss || crit)
            {
                failures.Add($"双方闪避/暴击为 0 时不应出现闪避/暴击（伤害 {damage} 暴击 {crit} 闪避 {miss}）");
            }
            else if (damage < 10 || damage > 14)
            {
                failures.Add($"命中伤害 {damage} 不在一级悟空对零防猴子的区间 [10,14]");
            }
        }

        // 事件累计伤害应与实体血量一致，再检查命中后是否确实创建池化飘字。
        int expectedHp = Mathf.Max(0, _monster.MaxHp - total);
        if (_monster.Hp != expectedHp && !_monster.Dead)
        {
            failures.Add($"猴子 HP {_monster.Hp} 与事件累计 {expectedHp} 不一致");
        }

        if (_maxPopCount == 0)
        {
            failures.Add("命中后场上没有出现过伤害飘字（NodePool 未取出 FloatingTextNode）");
        }

        if (_maxComboCount < 2)
        {
            failures.Add($"真实命中只累计到 {_maxComboCount} 连击，未达到HUD显示阈值");
        }
        if (!_comboHudShown)
        {
            failures.Add("实际战斗HUD从未显示连击数字");
        }

        GD.Print($"SMOKE: 猴子共受击 {_monsterHits.Count} 次，累计伤害 {total}，连击峰值 {_maxComboCount}，HUD {_comboHudShown}，HP {_monster.Hp}/{_monster.MaxHp}，同屏飘字峰值 {_maxPopCount}");
    }

    /// <summary>
    /// 输出失败原因及已观察序列，然后停止驱动。
    /// </summary>
    /// <param name="reason">本次停止驱动的失败原因。</param>
    private void Fail(string reason)
    {
        GD.PrintErr($"SMOKE FAIL：{reason}");
        foreach ((double time, string path) in _observed)
        {
            GD.PrintErr($"  观察[{time:F2}] {path}");
        }

        StopDriving(true);
    }

    /// <summary>
    /// 退订事件并按退出参数安排引擎关闭。
    /// </summary>
    /// <param name="failed">是否按失败结果结束并安排退出。</param>
    private void StopDriving(bool failed)
    {
        // 先停止帧驱动并释放事件订阅，避免退出等待期间继续触发烟测逻辑。
        SetPhysicsProcess(false);
        Input.ActionRelease(InputSystem.MoveLeft);
        Input.ActionRelease(InputSystem.MoveRight);
        Input.ActionRelease(InputSystem.Jump);
        if (_hero != null)
        {
            GF.Event.Unsubscribe(DamageDealtEventArgs.EventId, OnDamageDealt);
        }

        if (!failed && _hasQuitAfter)
        {
            return;
        }

        // 失败可能发生在暂停断言中；退出等待必须使用真实时间。
        SceneTreeTimer timer = GetTree().CreateTimer(0.05, ignoreTimeScale: true);
        timer.Timeout += () => GetTree().Quit(failed ? 1 : 0);
    }

    /// <summary>
    /// 返回观测序列中路径首次出现的位置，不存在时为 -1。
    /// </summary>
    /// <param name="path">待查找的状态路径。</param>
    /// <returns>路径首次出现的下标；不存在时为-1。</returns>
    private int FirstIndex(string path) => _observed.FindIndex(o => o.Path == path);

    /// <summary>
    /// 返回观测序列中路径最后出现的位置，不存在时为 -1。
    /// </summary>
    /// <param name="path">待查找的状态路径。</param>
    /// <returns>路径最后出现的下标；不存在时为-1。</returns>
    private int LastIndex(string path) => _observed.FindLastIndex(o => o.Path == path);

    /// <summary>
    /// 互斥的自动回归模式。
    /// </summary>
    private enum SmokeMode
    {
        Disabled,
        Hero,
        EnemyAi,
        Level,
        Cancellation,
        Ui,
        Bosses,
        Frontend,
        Progress,
    }
}
