using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GameConfig.Battle;
using GameConfig.Constant;
using GameConfig.Enemy;
using GameConfig.Level;
using GameConfig.Sound;
using GameFramework;
using GameLogic;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Attributes;
using GameLogic.Enemies;
using GameLogic.Equipment;
using GameLogic.Event.Combat;
using GameLogic.Heroes;
using GameLogic.Heroes.Body;
using GameLogic.Input;
using GameLogic.Level;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.Save.Data;
using GameLogic.UI;
using GameLogic.UI.Widgets;
using Godot;
using GodotGameFramework;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 通过真实移动、碰撞、守关怪和出口输入验证五波清场、自动保存与返回地图。
/// </summary>
public sealed class LevelSmokeScenario
{
    private readonly int _levelId; // 实际关卡编号，期望配方与总收益均读取同一配置。
    private readonly AbstractHeroEntity _hero; // 通过游戏流程实际显示的玩家。
    private readonly LevelViewController _level; // 受测关卡会话。
    private readonly LevelCameraNode _camera; // 受测实际取景。
    private readonly LevelGatesNode _gates; // 受测阶段区域。
    private readonly HashSet<int> _seen = new(); // 所有实际显示过的运行实体 ID。
    private readonly HashSet<int> _killed = new(); // 实际死亡的实体，测试预期只累计一次。
    private readonly List<(GameConfig.Enemy.EnemyConfig Config, int SoulPerOrb)> _defeated = new(); // 实际实例奖励，包含守关变体。
    private int _expectedExperience; // 本次实际死亡配方应发放的经验。
    private bool _exitSurvivorsChecked; // 目标出口必须在持续流仍有活怪时可用。
    private bool _initialLanding; // 出生下落完成后再执行地面路线，避免边落边走进第一组地刺。
    private bool _musicChecked; // 本关正式Music代理已经按场景绑定起播。
    private int _travelRunStep; // 地刺关卡用真实双击起跑，受伤后重新提交而不直接改速度。
    private readonly List<string> _failures = new(); // 累计回归失败信息。
    private float _lastCenter; // 上一帧实际相机中心。
    private float _lastPlayerX; // 上一帧玩家横坐标。
    private int _stageOrder; // 上一次观测到的开战阶段。
    private int _blockedFrames; // 玩家在边界持续停住的物理帧数。
    private Step _step; // 当前战斗物理验证步骤。
    private double _time; // 整体游戏时间，超时用于报告卡住的位置。
    private double _battleTime; // 当前阶段开战后的游戏时间。
    private bool _goHintSeen; // 是否在行进阶段观察到前进提示。
    private int _travelStageOrder; // 当前观测过行进起点的阶段。
    private bool _expectCenteredArrival; // 从新取景中心后方正常走入时，抵达应保持中央死区。
    private readonly int _startExperience; // 进关时档案中的累计经验。
    private readonly int _startSoul; // 进关时的钱包，通关后按真实随机奖励核对写盘。
    private bool _firstSpawnEntered; // 首波保留真实 AI，直到观测到它从场外走入画面。
    private readonly ProcedureOwner _flow; // 本次受测档案与存档入口。
    private ulong _exitRequestedAt; // 近处 W 按下到地图实际可见的实际时刻。
    private bool _exitLatencyRecorded; // 延迟只采样一次，不包含随后验证读盘的时间。
    private bool _exitHadPendingLoot; // W 时还有球在飞行；等待真实拾取与额外关卡延迟分别观测。
    private Task _platformCapture; // 第三阶段的实际画面证据。
    private Task _featuredCapture; // 守关血条的实际位置与无数字画面证据。
    private int _exitStep; // 远处按 W、接近、出口交互的验证顺序。
    private bool _sawFeatured; // 是否实际展示过大猩猩入场和生命条。
    private bool _waitingForMap; // 通关后停止访问即将卸载的场景。
    private Task<(GameData Data, SaveOrigin Origin)> _reload; // 返回地图后重新读盘验证保存结果。
    private Task _settlementCapture; // 成功结算渲染证据。
    private bool _settlementClicked; // 成功页只提交一次返回地图请求。
    private Task _exitCapture; // 有渲染器时先保存出口提示画面再按 W。
    private int _expectedTotalExperience; // 通关保存应保留的累计经验。
    private int _expectedSoul; // 已实际拾取、应由当前检查点保存的灵魂。

    /// <summary>
    /// 物理回归已完成或遇到失败。
    /// </summary>
    public bool IsDone { get; private set; }

    /// <summary>
    /// 完成时的失败原因列表。
    /// </summary>
    public IReadOnlyList<string> Failures => _failures;

    /// <summary>
    /// 创建正式关卡的物理回归场景。
    /// </summary>
    /// <param name="hero">正式流程显示的英雄。</param>
    /// <param name="level">受测关卡。</param>
    public LevelSmokeScenario(AbstractHeroEntity hero, LevelViewController level)
    {
        _hero = hero;
        _level = level;
        _levelId = SmokeInspection.ReadField<int>(level, "_levelId");
        _flow = SmokeInspection.Flow(GF.Procedure.CurrentProcedure as ProcedureLevel);
        // 边界回归会反复穿过同一陷阱；保留真实受击和击退，只提高本局夹具生命。
        HeroSystem heroSystem = _flow.GetSystem<HeroSystem>();
        heroSystem.SetSource(hero.HeroId, new GameLogic.Battle.Attributes.AttributeSourceId("LevelSmoke", _levelId),
            [GameLogic.Battle.Attributes.AttributeModifier.Flat(GameConfig.Attributes.AttributeType.MaxHp, 100000)]);
        heroSystem.Heal(hero.HeroId, int.MaxValue);
        _startExperience = _flow.GetModel<ProfileModel>().GetProgress(_flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.TotalExperience;
        _startSoul = _flow.GetModel<ProfileModel>().GetSoul(_flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value;
        _camera = SmokeInspection.Scene(level).GetNode<LevelCameraNode>("Camera2D");
        _gates = SmokeInspection.Scene(level).GetNode<LevelGatesNode>("World/StageGates");
        _lastCenter = _camera.GlobalPosition.X;
        _lastPlayerX = hero.GlobalPosition.X;
    }

    /// <summary>
    /// 在相机和实体物理更新之后观测，驱动真实输入和测试攻击；完成后不再访问已可能被重开流程卸载的关卡。
    /// </summary>
    /// <param name="delta">物理帧间隔秒数。</param>
    public void Update(double delta)
    {
        if (IsDone)
        {
            return;
        }

        _time += delta;
        if (_waitingForMap)
        {
            CheckReturnAndReload();
            return;
        }
        LevelModel model = SmokeInspection.Model(_level);
        if (model == null)
        {
            Fail($"关卡运行已结束或模型已释放：关卡 {_levelId}，经过 {_time:F1}s");
            return;
        }
        if (model.Outcome.Value == LevelOutcome.Defeated)
        {
            Fail($"英雄死亡：阶段 {SmokeInspection.Sequence(_level).Current.StageOrder}，位置 {_hero.GlobalPosition}");
            return;
        }
        if (!_musicChecked)
        {
            string expectedMusic = ConfigSystem.Instance.Tables.TbSoundConfig.Get((int)_level.MusicId).Path;
            AudioStreamPlayer[] players = _hero.GetTree().Root.FindChildren("*", "AudioStreamPlayer", true, false)
                .OfType<AudioStreamPlayer>().Where(player => player.Bus == "Music" && player.Playing).ToArray();
            if (players.Length != 1 || players[0].Stream?.ResourcePath != expectedMusic)
            {
                if (_time > 10)
                {
                    Fail("关卡Music代理未按场景绑定起播，或存在重复音乐");
                }

                return;
            }
            _musicChecked = true;
            GD.Print($"SMOKE-LEVEL MUSIC PASS: level={_levelId}, unique GGF Music agent={expectedMusic}");
        }
        if (!_initialLanding)
        {
            if (!_hero.OnFloor)
            {
                Move(null);
                return;
            }
            _initialLanding = true;
        }
        float center = _camera.GlobalPosition.X;
        if (Math.Abs(center - _lastCenter) > 600f * delta + 0.1f)
        {
            Fail($"相机帧位移超限：{_lastCenter} → {center}，阶段 {SmokeInspection.Sequence(_level).Current.StageOrder}");
        }

        if (Math.Abs(center - _camera.GetScreenCenterPosition().X) > 0.1f)
        {
            Fail("脚本相机中心与实际显示中心不一致");
        }

        _lastCenter = center;
        AbstractEnemyEntity[] monsters = _hero.GetTree().Root.FindChildren("*", "CharacterBody2D", true, false)
            .OfType<AbstractEnemyEntity>().Where(x => x.IsShown && !x.Dead).ToArray();
        foreach (AbstractEnemyEntity monster in monsters)
        {
            if (!_firstSpawnEntered && SmokeInspection.Sequence(_level).Current.StageOrder == 1 &&
                monster.GlobalPosition.X < _camera.ViewRight - 20f)
            {
                _firstSpawnEntered = true;
                GD.Print($"SMOKE-LEVEL: offscreen spawn entered, monster={monster.GlobalPosition.X:0.0}, viewRight={_camera.ViewRight:0.0}");
            }
            if (_firstSpawnEntered)
            {
                SmokeInspection.FreezeAi(monster);
            }

            if (_seen.Add(monster.Id))
            {
                if (ReferenceEquals(_level.FeaturedOpponent, monster))
                {
                    _sawFeatured = true;
                    EncounterHealthBar banner = SmokeInspection.ReadField<EncounterHealthBar>(
                        GF.UI.GetUIForm(ResourcesCollectionConstant.UIs_battle_form), "_encounterHealthBar");
                    int expectedHp = _levelId == 1 ? 900 :
                        _levelId == 7 && monster is TurtleMinisterEntity ? 4800 : monster.Config.BaseAttributes.MaxHp;
                    if (monster.MaxHp != expectedHp ||
                          !banner.Visible || banner.HasNode("Entrance") ||
                          !Mathf.IsEqualApprox(banner.GetGlobalRect().GetCenter().X,
                              banner.GetParent<Control>().GetGlobalRect().GetCenter().X) ||
                          !Mathf.IsEqualApprox(banner.GetNode<Control>("Health").GlobalPosition.Y, 36f) ||
                          banner.FindChildren("*", "Label", true, false).Count != 1 ||
                        SmokeInspection.ReadField<Label>(banner, "_name").Text != monster.Config.NameCn)
                    {
                        Fail("守关对手配置或血条未绑定，或仍残留入场弹窗");
                    }

                    _featuredCapture = CaptureFeaturedAsync();
                }
                // 只让可移动敌人横穿阶段门；石像的宽身体会同时碰到关卡外墙，不能用作入场探针。
                if (monster is not BatCaveStatueEntity)
                {
                    (float Left, float Right) region = _gates.BoundsOf(SmokeInspection.Sequence(_level).Current.StageOrder);
                    // 末门靠近场景外墙时，宽身体会同时碰外墙；优先验证远离外墙的独立左门。
                    float gate = region.Left > _camera.SceneLeft ? region.Left : region.Right;
                    Transform2D from = new(0f, new Vector2(gate - 30f, 100f));
                    // 此探针只核对阶段门，避免场景高台侧壁混入；保留两者原有的门层取舍。
                    uint barrierMask = LayerMask.LayerToMask2D("PlayerBarrier");
                    uint heroMask = _hero.CollisionMask;
                    uint monsterMask = monster.CollisionMask;
                    bool blocked;
                    bool monsterBlocked;
                    try
                    {
                        _hero.CollisionMask &= barrierMask;
                        monster.CollisionMask &= barrierMask;
                        blocked = _hero.TestMove(from, new Vector2(60f, 0f));
                        monsterBlocked = monster.TestMove(from, new Vector2(60f, 0f));
                    }
                    finally
                    {
                        _hero.CollisionMask = heroMask;
                        monster.CollisionMask = monsterMask;
                    }
                    if (monsterBlocked || !blocked)
                    {
                        Fail($"阶段门未正确区分玩家阻挡与怪物场外入场：{monster.GetType().Name}，阶段 {_stageOrder}");
                    }
                }
            }
        }

        if (SmokeInspection.Sequence(_level).Phase == LevelStagePhase.Travelling)
        {
            int order = SmokeInspection.Sequence(_level).Current.StageOrder;
            if (_travelStageOrder != order)
            {
                _travelStageOrder = order;
                _travelRunStep = 0;
                float arrivalCenter = _gates.BoundsOf(order).Right - _camera.ViewSize.X * 0.5f;
                // 从上一波屏幕右侧起步的短过渡，相机会按600px/s追近；先判断抵达前是否有足够距离收回中央窗口。
                float panSeconds = Math.Max(0f, arrivalCenter - _camera.GlobalPosition.X) / 600f;
                float speed = Math.Max(((IHeroBody)_hero).Params.WalkSpeed, Math.Abs(_hero.Velocity.X));
                _expectCenteredArrival = _hero.GlobalPosition.X <= arrivalCenter - 40f &&
                    _hero.GlobalPosition.X + speed * panSeconds <= arrivalCenter + 60f;
            }
            // 陷阱击退可让玩家先越过中央窗口，相机仍按600px/s追近；不把受击路线当成匀速居中样本。
            if (_expectCenteredArrival && (_hero.HasPendingHurt || SmokeInspection.BodyStateName(_hero) == "Hurt"))
            {
                _expectCenteredArrival = false;
                GD.Print($"SMOKE-LEVEL: stage {order} travel includes real knockback; retain camera speed/boundary checks");
            }
            if (monsters.Length > 0)
            {
                Fail("相机抵达之前生成了下一阶段敌人");
            }

            // 清波后到下一场开战之间，关卡应处于可前进窗口（HUD 据此显示 Go）。
            if (SmokeInspection.Model(_level).TravelAvailable.Value)
            {
                _goHintSeen = true;
            }

            if (_levelId == 6)
            {
                string body = SmokeInspection.BodyStateName(_hero);
                if (body == "Hurt")
                {
                    _travelRunStep = 0;
                }
                // 连续地刺需要跑跳跨越；先等硬直结束，再像玩家一样松开、按下、松开、按下。
                if (body == "Ground" && _travelRunStep < 4)
                {
                    if (_travelRunStep % 2 == 0)
                    {
                        Input.ActionRelease(InputSystem.MoveRight);
                    }
                    else
                    {
                        Input.ActionPress(InputSystem.MoveRight);
                    }

                    _travelRunStep++;
                    Input.ActionRelease(InputSystem.Jump);
                    return;
                }
            }

            Move("move_right");
            // 用户保留的斜坡含竖直端面，烟测用真实跳跃跨越，不修改场景地形。
            bool groundSpikeAhead = _level.GetNodeOrNull("World/Traps")?.GetChildren()
                .OfType<GameLogic.Level.Traps.BatCaveSpikeNode>().Any(spike =>
                    !SmokeInspection.ReadField<bool>(spike, "_suspended") &&
                    spike.GlobalPosition.X > _hero.GlobalPosition.X &&
                    spike.GlobalPosition.X - _hero.GlobalPosition.X < 110f) == true;
            bool canJump = SmokeInspection.BodyStateName(_hero) is "Ground" or "Air";
            if (canJump && (_hero.OnFloor && (_hero.IsOnWall() || groundSpikeAhead) ||
                groundSpikeAhead && _hero.ActionData.JumpCount == 1 && _hero.Velocity.Y >= 0))
            {
                Input.ActionPress(InputSystem.Jump);
            }
            else
            {
                Input.ActionRelease(InputSystem.Jump);
            }
        }
        else if (SmokeInspection.Sequence(_level).Phase == LevelStagePhase.Fighting)
        {
            if (SmokeInspection.Model(_level).TravelAvailable.Value)
            {
                Fail("开战后仍处于可前进窗口");
            }

            DriveBattle(delta, monsters);
        }
        else if (SmokeInspection.Sequence(_level).Phase == LevelStagePhase.AwaitingExit)
        {
            if (!SmokeInspection.Scene(_level).HasNode("World/Exit"))
            {
                // 原版终关没有传送门；不发送W，等待正式最终死亡和奖励完成后自动结算。
                Move(null);
                if (_exitStep != 5)
                {
                    _exitStep = 5;
                    _exitRequestedAt = Time.GetTicksMsec();
                    _exitHadPendingLoot = true;
                }
            }
            else
            {
                if (_levelId == 4 && !_exitSurvivorsChecked)
                {
                    if (monsters.Length == 0 || _seen.Count <= _killed.Count)
                    {
                        Fail("目标出口未在持续蝙蝠流仍有活怪时开放。");
                    }

                    _exitSurvivorsChecked = true;
                    GD.Print($"SMOKE-LEVEL: statue exit available with {monsters.Length} surviving bats");
                }
                DriveExit();
            }
        }
        else if (SmokeInspection.Sequence(_level).Phase == LevelStagePhase.Completed)
        {
            if (_platformCapture?.IsFaulted == true)
            {
                Fail(_platformCapture.Exception.ToString());
                return;
            }
            if (_featuredCapture?.IsFaulted == true)
            {
                Fail(_featuredCapture.Exception.ToString());
                return;
            }
            if (_featuredCapture is not { IsCompleted: true })
            {
                return;
            }

            if (_exitStep != 5)
            {
                Fail("出口尚未完成远处拒绝与靠近交互验证，就提前通关");
            }

            int expected = ConfigSystem.Instance.Tables.TbLevelConfig.Get(_levelId).Stages.Sum(x => x.Recipes.Sum(r => r.Count))
                            + (_levelId == 10 ? 2 : 0); // 原始31配方单位，加上兽形后的两具真实后继形态。
            if (_levelId != 4 && _seen.Count != expected)
            {
                Fail($"怪物总数错误：实际 {_seen.Count}，配置 {expected}");
            }

            // 收益进档案：本关获得经验 = 档案累计经验 - 进关时的累计经验（新档进关为 0）。
            int expectedExperience = _expectedExperience;
            int expectedDefeats = _levelId == 4 ? ConfigSystem.Instance.Tables.TbLevelConfig.Get(4).Stages
                .Take(4).Sum(stage => stage.Recipes.Sum(recipe => recipe.Count)) + 1 : expected;
            if (_killed.Count != expectedDefeats || SmokeInspection.Model(_level).DefeatedEnemyCount != expectedDefeats)
            {
                Fail($"击败数错误：实际 {_killed.Count}，应为 {expectedDefeats}");
            }

            LevelViewController run = SmokeInspection.Level(GF.Procedure.CurrentProcedure as ProcedureLevel);
            ProfileModel profile = SmokeInspection.Flow(GF.Procedure.CurrentProcedure as ProcedureLevel)?.GetModel<ProfileModel>();
            int gained = profile?.GetProgress(profile.HeroOf(PlayerSlot.P1)).Value.TotalExperience - _startExperience ?? -1;
            int awardedExperience = SmokeInspection.Model(run).AwardedExperience;
            if (gained != expectedExperience || awardedExperience != expectedExperience)
            {
                Fail($"本关经验错误或重复死亡奖励：档案 {gained}，统计 {awardedExperience}，配置 {expectedExperience}");
            }
            GD.Print($"SMOKE-LEVEL: experience={gained}, expected={expectedExperience}");
            int minimumSoul = 0, maximumSoul = 0;
            foreach ((EnemyConfig Config, int SoulPerOrb) monster in _defeated)
            {
                minimumSoul += monster.Config.SoulCount.X * monster.SoulPerOrb;
                maximumSoul += monster.Config.SoulCount.Y * monster.SoulPerOrb;
            }
            _expectedSoul = _flow.GetModel<ProfileModel>().GetSoul(_flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value;
            int gainedSoul = _expectedSoul - _startSoul;
            if (gainedSoul < minimumSoul || gainedSoul > maximumSoul)
            {
                Fail("本关灵魂奖励超出官方掉落范围");
            }

            GD.Print($"SMOKE-LEVEL: soul={gainedSoul}, expected range={minimumSoul}..{maximumSoul}");

            // 通关是检查点：关卡运行已提交并结束。
            if (SmokeInspection.Model(run)?.Outcome.Value != LevelOutcome.Cleared || SmokeInspection.GetLevelSystem(run).CheckpointWrite == null)
            {
                Fail($"通关没有提交检查点：结局 {SmokeInspection.Model(run)?.Outcome.Value}");
            }

            if (!_goHintSeen)
            {
                Fail("清波后未出现前进提示（Go）");
            }

            if (!_sawFeatured)
            {
                Fail("第五波未展示守关对手");
            }

            _expectedTotalExperience = _startExperience + expectedExperience;
            _waitingForMap = true;
            Move(null);
        }

        if (_time > 240 && !IsDone)
        {
            string enemies = string.Join("; ", GF.Entity.GetAllLoadedEntities().OfType<AbstractEnemyEntity>()
                .Where(enemy => enemy.IsShown).Select(enemy => $"{enemy.Id}/{enemy.GetType().Name}:hp={enemy.Hp},dead={enemy.Dead},attack={enemy.AttackSegment},pos={enemy.GlobalPosition}"));
            Fail($"超时：阶段 {SmokeInspection.Sequence(_level).Current.StageOrder}/{SmokeInspection.Sequence(_level).Phase}，步骤 {_step}，玩家 {_hero.GlobalPosition}，相机 {center}，敌人 {enemies}");
        }

        _lastPlayerX = _hero.GlobalPosition.X;
        if (IsDone)
        {
            Move(null);
        }
    }

    /// <summary>
    /// 远处按键不通关；清场后靠近出口，再发送真实 W 按键。
    /// </summary>
    private void DriveExit()
    {
        LevelExitNode exit = SmokeInspection.Scene(_level).GetNode<LevelExitNode>("World/Exit");
        if (!exit.Visible && _exitStep < 5)
        {
            Fail("清场后出口未显示");
            return;
        }
        if (_exitStep == 0)
        {
            LevelViewController run = SmokeInspection.Level(GF.Procedure.CurrentProcedure as ProcedureLevel);
            if (SmokeInspection.Model(run).Outcome.Value != LevelOutcome.Running || SmokeInspection.GetLevelSystem(run).CheckpointWrite != null)
            {
                Fail("仅清场就提前通关或写盘");
            }

            PlaceForWallTest(exit.GlobalPosition.X - 220f);
            Move(null);
            _exitStep = 1;
        }
        else if (_exitStep == 1)
        {
            // 测试瞬移后等待物理重叠缓存更新，避免把原位置的重叠当成远处输入。
            _exitStep = 2;
        }
        else if (_exitStep == 2)
        {
            PressW();
            _exitStep = 3;
        }
        else if (_exitStep == 3)
        {
            LevelViewController run = SmokeInspection.Level(GF.Procedure.CurrentProcedure as ProcedureLevel);
            if (SmokeInspection.Model(run).Outcome.Value != LevelOutcome.Running || SmokeInspection.GetLevelSystem(run).CheckpointWrite != null)
            {
                Fail("远处按 W 就提交通关或写盘");
                return;
            }
            Move(InputSystem.MoveRight);
            if (_hero.IsOnWall() && _hero.OnFloor)
            {
                Input.ActionPress(InputSystem.Jump);
            }
            else
            {
                Input.ActionRelease(InputSystem.Jump);
            }

            if (exit.GetOverlappingBodies().Contains(_hero) && _hero.OnFloor)
            {
                Move(null);
                Input.ActionRelease(InputSystem.Jump);
                _exitCapture = CaptureExitAsync();
                _exitStep = 4;
            }
        }
        else if (_exitStep == 4 && _exitCapture.IsCompleted)
        {
            if (_exitCapture.IsFaulted)
            {
                Fail(_exitCapture.Exception.ToString());
                return;
            }
            // 靠近出口时攻击、跳跃、下方向与 W 的组合不能误提交通关。
            foreach (string action in new[] { InputSystem.Attack, InputSystem.Jump, InputSystem.MoveDown })
            {
                Input.ActionPress(action);
                try
                {
                    PressW();
                    if (!exit.Visible || SmokeInspection.Model(_level).Outcome.Value != LevelOutcome.Running || SmokeInspection.GetLevelSystem(_level).CheckpointWrite != null)
                    {
                        Fail($"出口把 W＋{action} 组合误当成单独 W");
                        return;
                    }
                }
                finally
                {
                    Input.ActionRelease(action);
                }
            }
            GD.Print("SMOKE-LEVEL: exit rejects W+J/K/S combinations");
            // 组合键由真实输入缓存消费；下一物理帧再提交单独W，避免同帧按放合并成旧组合。
            _exitStep = 6;
        }
        else if (_exitStep == 6)
        {
            _exitRequestedAt = Time.GetTicksMsec();
            _exitHadPendingLoot = SmokeInspection.ReadField<HashSet<GameLogic.Drops.LootOrbNode>>(_level, "_lootOrbs").Count != 0;
            PressW();
            _exitStep = 5;
        }
    }

    /// <summary>
    /// 有渲染器时记录真实出口和靠近提示，截图完成后再触发关卡卸载。
    /// </summary>
    /// <returns>本帧画面保存完成的任务。</returns>
    private async Task CaptureExitAsync()
    {
        if (DisplayServer.GetName() == "headless")
        {
            return;
        }

        await _hero.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string directory = ProjectSettings.GlobalizePath("res://.godot/validation/encounter");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "level_exit.png");
        Error result = _hero.GetViewport().GetTexture().GetImage().SavePng(path);
        if (result != Error.Ok)
        {
            throw new IOException($"出口预览保存失败：{result}，{path}");
        }

        GD.Print($"SMOKE-LEVEL: exit preview={path}");
    }

    /// <summary>
    /// 记录正式成功结算画面。
    /// </summary>
    /// <returns>画面保存完成的任务。</returns>
    private async Task CaptureSettlementAsync()
    {
        // 成功页必须实际播放胜利曲，暂停不应让音乐代理停止或保留关卡 BGM。
        for (int frame = 0; frame < 30; frame++)
        {
            await _hero.ToSignal(_hero.GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        AudioStreamPlayer[] music = _hero.GetTree().Root.FindChildren("*", "AudioStreamPlayer", true, false)
                    .OfType<AudioStreamPlayer>().Where(player => player.Bus == "Music" && player.Playing).ToArray();
        if (music.Length != 1 || !music[0].Stream.ResourcePath.EndsWith("settlement_victory.mp3", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("成功结算未唯一播放 GGF 胜利音乐");
        }

        if (music[0].VolumeDb <= -1f || AudioServer.IsBusMute(AudioServer.GetBusIndex("Music")))
        {
            throw new InvalidOperationException($"成功结算音乐仍处于静音：{music[0].VolumeDb}dB");
        }

        int bus = AudioServer.GetBusIndex("Music");
        using var capture = new AudioEffectCapture { BufferLength = 1f };
        int effect = AudioServer.GetBusEffectCount(bus);
        AudioServer.AddBusEffect(bus, capture);
        try
        {
            double position = music[0].GetPlaybackPosition();
            await _hero.ToSignal(_hero.GetTree().CreateTimer(.5, true, false, true), SceneTreeTimer.SignalName.Timeout);
            Vector2[] samples = capture.GetBuffer(capture.GetFramesAvailable());
            float peak = samples.Length == 0 ? 0 : samples.Max(sample => Math.Max(Math.Abs(sample.X), Math.Abs(sample.Y)));
            double rms = samples.Length == 0 ? 0 : Math.Sqrt(samples.Average(sample => (sample.X * sample.X + sample.Y * sample.Y) * .5));
            GD.Print($"SMOKE-VICTORY-AUDIO: driver={AudioServer.GetDriverName()}, position={position:F3}->{music[0].GetPlaybackPosition():F3}, samples={samples.Length}, peak={peak:F6}, rms={rms:F6}, busVolume={AudioServer.GetBusVolumeDb(bus)}, masterVolume={AudioServer.GetBusVolumeDb(0)}");
            if (peak < .01f || rms < .001 || music[0].StreamPaused)
            {
                throw new InvalidOperationException("成功结算代理存在，但实际混音没有可听信号");
            }
        }
        finally
        {
            AudioServer.RemoveBusEffect(bus, effect);
        }
        GD.Print("SMOKE-MUSIC PASS: unique paused victory Music agent with non-silent mixed output");
        if (DisplayServer.GetName() == "headless")
        {
            return;
        }

        await _hero.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string directory = ProjectSettings.GlobalizePath("res://.godot/validation/encounter");
        Directory.CreateDirectory(directory);
        Error result = _hero.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory, "settlement_success.png"));
        if (result != Error.Ok)
        {
            throw new IOException($"成功结算截图失败：{result}");
        }
    }

    /// <summary>
    /// 从视口发送完整按下与释放，覆盖 W 绑定和未处理输入路径。
    /// </summary>
    private void PressW()
    {
        _hero.GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.W, Pressed = true });
        _hero.GetViewport().PushInput(new InputEventKey { PhysicalKeycode = Key.W, Pressed = false });
    }

    /// <summary>
    /// 流程实际返回地图后重新读盘，避免只检查内存中的框架数据。
    /// </summary>
    private void CheckReturnAndReload()
    {
        if (_time > 180)
        {
            Fail("出口交互后未返回地图或完成自动保存");
            return;
        }
        SettlementForm settlement = GF.UI.GetAllLoadedUIForms().OfType<SettlementForm>().FirstOrDefault(x => x.Visible);
        if (settlement != null && !_settlementClicked)
        {
            if (SmokeInspection.Model(_level).Result?.Outcome != LevelOutcome.Cleared || settlement.GetNode<TextureButton>("Content/Actions/m_Retry").Visible || !GF.Base.IsGamePaused)
            {
                Fail("成功结算成绩、重试按钮或暂停状态不正确");
                return;
            }
            _settlementCapture ??= CaptureSettlementAsync();
            if (!_settlementCapture.IsCompleted)
            {
                return;
            }

            if (_settlementCapture.IsFaulted)
            {
                Fail(_settlementCapture.Exception.ToString());
                return;
            }
            settlement.GetNode<TextureButton>("Content/Actions/m_ReturnMap").EmitSignal(BaseButton.SignalName.Pressed);
            _settlementClicked = true;
        }
        if (GF.Procedure.CurrentProcedure is not ProcedureMap)
        {
            return;
        }

        if (GF.UI.GetUIForm(ResourcesCollectionConstant.UIs_map_form) is not MapForm { Visible: true })
        {
            return;
        }

        if (!_exitLatencyRecorded)
        {
            ulong latency = Time.GetTicksMsec() - _exitRequestedAt;
            _exitLatencyRecorded = true;
            GD.Print($"SMOKE-LEVEL: {(_levelId == 10 ? "automatic completion" : "exit W")} → visible map = {latency} ms (checkpoint included, pending loot={_exitHadPendingLoot})");
            if (latency > (_exitHadPendingLoot ? 10000UL : 1500UL))
            {
                Fail($"出口返回地图过慢：{latency} ms");
                return;
            }
        }
        _reload ??= _flow.GetSystem<SaveSystem>().LoadOrCreateAsync(GF.Archive.CurrentCatalogue.SlotNumber, GF.Archive.CurrentData.UnitId);
        if (!_reload.IsCompleted)
        {
            return;
        }

        if (_reload.IsFaulted || _reload.Result.Data.Heroes.Single(x => x.HeroId == _reload.Result.Data.Heroes[0].HeroId).TotalExperience != _expectedTotalExperience ||
                    _reload.Result.Data.Heroes[0].Soul != _expectedSoul ||
                    _reload.Result.Data.HighestClearedLevelId < _levelId)
        {
            Fail("返回地图后重新读取存档，经验或灵魂与通关收益不一致");
        }
        else
        {
            if (_levelId == 10)
            {
                EquipmentData item = _flow.GetModel<ProfileModel>().GetBag(_flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).SingleOrDefault(data =>
                    data.EquipmentId == GameConfig.Equipment.EquipmentId.DiamondSnare);
                EquipmentSaveData saved = _reload.Result.Data.EquipmentItems.SingleOrDefault(data => data.InstanceId == item?.InstanceId);
                if (item == null || saved == null || saved.EquipmentId != item.EquipmentId || saved.Attributes != item.Attributes)
                {
                    Fail("牛魔谷通关后金钢琢实例或随机属性未进入实际检查点");
                    return;
                }
                GD.Print("SMOKE-BULL TREASURE DISK PASS: unique instance and rolled attributes retained after automatic settlement");
            }
            GD.Print($"SMOKE-LEVEL: {(_levelId == 10 ? "automatic completion" : "exit W")} → map → disk reload, experience={_expectedTotalExperience}");
            SaveNoticeForm notice = _hero.GetTree().Root.FindChildren("*", "Control", true, false)
                .OfType<SaveNoticeForm>().FirstOrDefault(x => x.SerialId != 0);
            if (notice == null || !notice.GetNode<Label>("SaveFeedback/m_SaveFeedback").Visible)
            {
                Fail("W 返回地图后的实际检查点未显示全局保存成功提示");
            }
            else
            {
                GD.Print("SMOKE-LEVEL: checkpoint success notice visible on map");
            }

            IsDone = true;
        }
    }

    /// <summary>
    /// 先验证抵达时取景和配置延迟，再验证墙体与真实清波。
    /// </summary>
    /// <param name="delta">本次逻辑推进的秒数。</param>
    /// <param name="monsters">本帧仍属于关卡的敌人实体。</param>
    private void DriveBattle(double delta, AbstractEnemyEntity[] monsters)
    {
        LevelStage stage = ConfigSystem.Instance.Tables.TbLevelConfig.Get(_levelId).Stages[SmokeInspection.Sequence(_level).Current.StageOrder - 1];
        (float Left, float Right) region = _gates.BoundsOf(stage.StageOrder);
        if (_stageOrder != stage.StageOrder)
        {
            _stageOrder = stage.StageOrder;
            _battleTime = 0;
            _step = Step.RightWall;
            _blockedFrames = 0;
            if (Math.Abs(_camera.ViewRight - region.Right) > 0.1f ||
                (_expectCenteredArrival && Math.Abs(_hero.GlobalPosition.X - _camera.GlobalPosition.X) > 60f))
            {
                Fail($"开战时相机未抵达右界，或玩家不在中央死区附近：viewRight={_camera.ViewRight:0.000}, right={region.Right:0.000}, " +
                    $"hero={_hero.GlobalPosition}, camera={_camera.GlobalPosition}, velocity={_hero.Velocity}, body={SmokeInspection.BodyStateName(_hero)}");
            }

            GD.Print($"SMOKE-LEVEL: stage {stage.StageOrder} arrived, camera={_camera.GlobalPosition.X:0.0}, player={_hero.GlobalPosition.X:0.0}");
            if (stage.StageOrder == 3)
            {
                _platformCapture = CapturePlatformsAsync();
            }

            PlaceForWallTest(region.Right - 40f);
        }

        _battleTime += delta;
        if (_battleTime < stage.SpawnDelay - 0.05 && monsters.Length > 0)
        {
            Fail("阶段 SpawnDelay 到期之前刷怪");
        }

        if (_hero.GlobalPosition.X < region.Left || _hero.GlobalPosition.X > region.Right)
        {
            Fail($"玩家越过战斗区域的物理边界：关卡 {_levelId} 阶段 {stage.StageOrder}，步骤 {_step}，玩家 {_hero.GlobalPosition}，区域 {region.Left}..{region.Right}，计时 {_battleTime:0.000}");
        }

        if (_step == Step.Kill)
        {
            Move(null);
            // 守关条先完成真实绘制与截图，再清怪；否则同帧死亡会让截图只留下隐藏后的 HUD。
            if (_featuredCapture is { IsCompleted: false })
            {
                return;
            }

            if (_featuredCapture?.IsFaulted == true)
            {
                Fail(_featuredCapture.Exception.ToString());
                return;
            }
            if (stage.StageOrder == 1 && !_firstSpawnEntered)
            {
                if (_battleTime > 15)
                {
                    Fail("首波场外怪物未通过真实 AI 进入画面");
                }

                return;
            }
            if (_levelId == 4 && stage.StageOrder == 5 &&
                !monsters.Any(monster => !ReferenceEquals(monster, _level.FeaturedOpponent)))
            {
                return; // 先等持续蝙蝠出现，再验证石像死亡即可开出口。
            }

            foreach (AbstractEnemyEntity monster in monsters)
            {
                if (_levelId == 4 && stage.StageOrder == 5 && !ReferenceEquals(monster, _level.FeaturedOpponent))
                {
                    continue; // 官方石像是末段出口目标，持续蝙蝠流不参与清波。
                }

                var data = AttackData.Create(0, default, 100000f, DamageKind.Real, Vector2.Zero,
                                    -monster.Facing, 0, source: new GameLogic.Battle.Attacks.AttackSource(_hero.Id, GameConfig.Entity.EntityId.None, 0, 0));
                monster.ReceiveHit(data);
                ReferencePool.Release(data);
                if (monster.Dead && _killed.Add(monster.Id))
                {
                    int soulPerOrb = (int?)typeof(AbstractEnemyEntity).GetProperty("SoulPerOrbOverride",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(monster)
                        ?? monster.Config.SoulPerOrb;
                    _defeated.Add((monster.Config, soulPerOrb));
                    _expectedExperience += SmokeInspection.ReadField<int>(monster, "ExperienceReward");
                }
                // 同一死亡再发一次普通事件，刷怪服务必须只发一次奖励和清波名额。
                if (monster.Dead && monster is not (AbstractBullDemonFormEntity or BullDemonHeartEntity))
                {
                    GF.Event.Fire(this, EnemyDiedEventArgs.Create(monster.Id,
                                        SmokeInspection.ReadField<int>(monster, "ExperienceReward"), monster.Config, monster.GlobalPosition,
                                        monster.Config.SoulPerOrb, monster.Config.EquipmentDropChance));
                }
            }

            return;
        }

        bool right = _step == Step.RightWall;
        Move(right ? "move_right" : "move_left");
        // 水帘洞地面有原版台阶和斜坡端面，验证边界前同样用真实跳跃通过地形。
        if (SmokeInspection.BodyStateName(_hero) == "Ground" && _hero.IsOnWall() && _hero.OnFloor &&
            (right ? _hero.GlobalPosition.X < region.Right - 70f : _hero.GlobalPosition.X > region.Left + 70f))
        {
            Input.ActionPress(InputSystem.Jump);
        }
        else
        {
            Input.ActionRelease(InputSystem.Jump);
        }

        bool nearWall = right ? _hero.GlobalPosition.X > region.Right - 60f : _hero.GlobalPosition.X < region.Left + 60f;
        _blockedFrames = nearWall && Math.Abs(_hero.GlobalPosition.X - _lastPlayerX) < 0.1f ? _blockedFrames + 1 : 0;
        if (_blockedFrames >= 20)
        {
            // 对照官方屏幕内侧 20px 的角色碰撞边界，不把阶段锚点当作身体可达位置。
            CollisionShape2D shape = _hero.GetNode<CollisionShape2D>("CollisionShape2D");
            Rect2 body = shape.Shape.GetRect();
            float edge = (shape.GlobalTransform * (right ? body.End : body.Position)).X;
            float screenEdge = _camera.GetScreenCenterPosition().X + (right ? 0.5f : -0.5f) * _camera.ViewSize.X;
            float margin = right ? screenEdge - edge : edge - screenEdge;
            if (Math.Abs(margin - 20f) > 1f)
            {
                Fail($"角色屏幕边界留白错误：{margin:0.00}px");
            }
            // 第二阶段额外验证左门；其余阶段在右墙前清波，覆盖最容易发生相机跳变的站位。
            if (right && stage.StageOrder == 2)
            {
                _step = Step.LeftWall;
                // 通过真实移动让相机回退；传送到视野外无法验证随屏幕移动的边界。
            }
            else
            {
                _step = Step.Kill;
            }

            _blockedFrames = 0;
        }
    }

    /// <summary>
    /// 记录第三阶段真实取景中的三个平台。
    /// </summary>
    /// <returns>画面保存任务。</returns>
    private async Task CapturePlatformsAsync()
    {
        if (DisplayServer.GetName() == "headless")
        {
            return;
        }

        await _hero.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string path = ProjectSettings.GlobalizePath("res://.godot/validation/stage_3_platforms.png");
        if (_hero.GetViewport().GetTexture().GetImage().SavePng(path) != Error.Ok)
        {
            throw new IOException("第三阶段平台截图失败。");
        }
    }

    /// <summary>
    /// 记录守关对手出现时的真实 HUD，覆盖血条位置及名称与数字显示。
    /// </summary>
    /// <returns>画面保存任务；无渲染器时直接完成。</returns>
    private async Task CaptureFeaturedAsync()
    {
        if (DisplayServer.GetName() == "headless")
        {
            return;
        }

        await _hero.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string directory = ProjectSettings.GlobalizePath("res://.godot/validation/encounter");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "featured_health_bar.png");
        if (_hero.GetViewport().GetTexture().GetImage().SavePng(path) != Error.Ok)
        {
            throw new IOException("守关血条截图失败。");
        }

        GD.Print($"SMOKE-LEVEL: featured preview={path}");
    }

    /// <summary>
    /// 把玩家放到待测墙边，避免中途被地形或怪物的身体卡住。
    /// </summary>
    /// <param name="x">待测墙边的世界横坐标，单位为像素。</param>
    private void PlaceForWallTest(float x)
    {
        // 新关卡地面高低不同，不能把低洼处的旧 Y 带入较高地面内部。
        // 从场景上方落地，仍由真实物理决定脚底高度和墙体阻挡。
        _hero.GlobalPosition = new Vector2(x, 100f);
        _hero.Velocity = Vector2.Zero;
        _lastPlayerX = x;
    }

    /// <summary>
    /// 每帧只保持一个移动方向，结束时释放全部测试输入。
    /// </summary>
    /// <param name="action">本帧唯一保持按下的原生输入动作。</param>
    private static void Move(string action)
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

    /// <summary>
    /// 留下可读证据并结束自动输入，协议输出由驱动器统一负责。
    /// </summary>
    /// <param name="message">首个失败的可读原因。</param>
    private void Fail(string message)
    {
        _failures.Add(message);
        IsDone = true;
    }

    /// <summary>
    /// 单段战斗内的物理约束验证顺序。
    /// </summary>
    private enum Step
    {
        RightWall,
        LeftWall,
        Kill,
    }
}
