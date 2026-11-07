using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Constant;
using GameLogic;
using GameLogic.Architecture;
using GameLogic.Heroes;
using GameLogic.Level;
using GameLogic.Profile;
using GameLogic.UI;
using GameLogic.UI.Widgets;
using Godot;
using GodotGameFramework;

/// <summary>
/// 真实新档依次通关当前主线，每次退出到标题再读取同一槽，检查英雄身份、最新提示和锁定输入。
/// </summary>
/// <param name="driver">提供真实引擎帧和场景树的烟测驱动。</param>
public sealed class MapProgressSmokeScenario(Node driver)
{
    private int _levelId; // 下一次实际挑战的关卡，首次从隔离档案的最新进度开始。
    private int _step; // 0等待地图，1战斗，2等待设置，3重读检查，4进关，5等待返回标题。
    private int _closedMapSerial; // 刚关闭的地图，用于拒绝读取过渡期间的旧窗口。
    private LevelSmokeScenario _run; // 本次真实五波、出口与检查点回归。
    private double _time; // 等待导航的总秒数。
    private readonly int _heroId = OS.GetCmdlineUserArgs().Contains("--tangseng-only") ? 2 : 1; // 真实选角身份，重读后不能回到默认英雄。

    /// <summary>
    /// 回归已经完成或失败。
    /// </summary>
    public bool IsDone { get; private set; }

    /// <summary>
    /// 首个失败原因。
    /// </summary>
    public string Failure { get; private set; }

    /// <summary>
    /// 在真实物理帧之后推进菜单与关卡操作。
    /// </summary>
    /// <param name="delta">本次游戏秒数。</param>
    public void Update(double delta)
    {
        if (IsDone)
        {
            return;
        }

        try
        {
            _time += delta;
            if (_time > 1200)
            {
                throw new InvalidOperationException("主线解锁回归超时。");
            }

            if (_step == 4)
            {
                if (GF.Procedure.CurrentProcedure is not ProcedureLevel procedure)
                {
                    // 窗口可见与打开成功回调可能相差一帧，重复实际点击直至流程接管。
                    MapForm map = VisibleMap();
                    if (map != null)
                    {
                        map.GetChildren().OfType<GameLogic.UI.Widgets.MapLocationButton>()
                            .Single(button => button.LevelId == _levelId).EmitSignal(BaseButton.SignalName.Pressed);
                    }
                    return;
                }
                LevelViewController level = SmokeInspection.Level(procedure);
                AbstractHeroEntity hero = driver.GetTree().Root.FindChildren("*", "CharacterBody2D", true, false)
                    .OfType<AbstractHeroEntity>().FirstOrDefault(value => value.IsShown);
                if (level != null && hero != null && GF.UI.GetUIForm(ResourcesCollectionConstant.UIs_battle_form) is BattleForm { Visible: true })
                {
                    _run = new LevelSmokeScenario(hero, level);
                    _step = 1;
                }
                return;
            }
            if (_step == 1)
            {
                _run.Update(delta);
                if (!_run.IsDone)
                {
                    return;
                }

                if (_run.Failures.Count != 0)
                {
                    throw new InvalidOperationException(string.Join("; ", _run.Failures));
                }

                MapForm map = VisibleMap();
                CheckMap(map, _levelId);
                _closedMapSerial = map.SerialId;
                map.GetNode<BaseButton>("m_Settings").EmitSignal(BaseButton.SignalName.Pressed);
                _step = 2;
                return;
            }
            if (_step == 2)
            {
                SettingsForm settings = GF.UI.GetAllLoadedUIForms().OfType<SettingsForm>().FirstOrDefault(form => form.Visible);
                if (settings == null)
                {
                    return;
                }
                settings.GetNode<BaseButton>("Scroll/Content/Options/Navigation/m_ReturnTitle").EmitSignal(BaseButton.SignalName.Pressed);
                _step = 5;
                return;
            }
            if (_step == 5)
            {
                if (GF.Procedure.CurrentProcedure is ProcedureMap)
                {
                    return;
                }

                _step = 3;
            }
            MapForm current = VisibleMap();
            if (current == null || current.SerialId == _closedMapSerial)
            {
                // 仅推进真实菜单到地图，不调用 AdvanceToLevel 的关卡夹具准备。
                IEnumerable<Control> forms = GF.UI.GetAllLoadedUIForms().OfType<Control>().Where(form => form.Visible);
                Control form = forms.FirstOrDefault(value => value is SaveForm) ?? forms.FirstOrDefault(value => value is StartForm or CharacterForm);
                string path = form switch
                {
                    StartForm => "m_Start",
                    SaveForm => "Slots/m_Slot1",
                    CharacterForm => _heroId == 2 ? "Roles/m_Sanzang" : "Roles/m_Wukong",
                    _ => null
                };
                if (path != null && !form.GetNode<BaseButton>(path).Disabled)
                {
                    form.GetNode<BaseButton>(path).EmitSignal(BaseButton.SignalName.Pressed);
                }

                return;
            }
            if (_levelId == 0)
            {
                _levelId = GF.Fsm.GetModel<ProfileModel>().HighestClearedLevelId.Value + 1;
                GD.Print($"SMOKE-PROGRESS START: next={_levelId}, hero={_heroId}, read from actual disk profile");
            }
            if (_step == 3)
            {
                CheckMap(current, _levelId);
                GD.Print($"SMOKE-PROGRESS RELOAD: cleared={_levelId}, latest={GF.Fsm.GetSystem<ProfileSystem>().LatestLevelId}");
                if (_levelId == 10)
                {
                    IsDone = true;
                    return;
                }
                _levelId++;
                _step = 0;
            }
            CheckMap(current, _levelId - 1);
            current.GetChildren().OfType<GameLogic.UI.Widgets.MapLocationButton>()
                .Single(button => button.LevelId == _levelId).EmitSignal(BaseButton.SignalName.Pressed);
            _step = 4;
        }
        catch (Exception error)
        {
            Failure = error.ToString();
            IsDone = true;
        }
    }

    /// <summary>
    /// 返回当前真实地图实例。
    /// </summary>
    /// <returns>可见的地图窗口，加载中为空。</returns>
    private static MapForm VisibleMap() => GF.UI.GetUIForm(ResourcesCollectionConstant.UIs_map_form) as MapForm is { Visible: true } map ? map : null;

    /// <summary>
    /// 逐个地点核对解锁、原生提示以及直接发送锁定信号的拒绝。
    /// </summary>
    /// <param name="map">当前地图。</param>
    /// <param name="cleared">应已通关的最远编号。</param>
    private void CheckMap(MapForm map, int cleared)
    {
        if (map == null || GF.Fsm.GetModel<ProfileModel>().HighestClearedLevelId.Value != cleared ||
            GF.Fsm.GetModel<ProfileModel>().ActiveHeroId != _heroId)
        {
            throw new InvalidOperationException("重读或通关返回后地图进度错误。");
        }

        int latest = Math.Min(10, cleared + 1);
        AnimationPlayer player = map.GetNode<AnimationPlayer>("m_LocationAnimation");
        MapLocationButton[] locations = map.GetChildren().OfType<GameLogic.UI.Widgets.MapLocationButton>().ToArray();
        MapLocationButton latestButton = locations.Single(button => button.LevelId == latest);
        string animation = latest == 1 ? "location/current" : latestButton.Name.ToString()[2..].ToLowerInvariant() + "/current";
        if (player.CurrentAnimation != animation)
        {
            throw new InvalidOperationException($"最新提示错误：{player.CurrentAnimation}，期望 {animation}");
        }

        foreach (MapLocationButton button in locations)
        {
            int id = button.LevelId;
            if (button.Disabled != (id > latest))
            {
                throw new InvalidOperationException("地图解锁状态错误。");
            }

            if (button.GetNode<CanvasItem>("CurrentHint").Visible != (id == latest) ||
                button.GetNode<CanvasItem>("LocationGlow").Visible)
            {
                throw new InvalidOperationException($"地图地点提示不符：id={id}, latest={latest}, animation={player.CurrentAnimation}");
            }

            if (id <= latest)
            {
                continue;
            }

            button.EmitSignal(Control.SignalName.MouseEntered);
            button.EmitSignal(BaseButton.SignalName.Pressed);
            if (player.CurrentAnimation != animation || GF.Procedure.CurrentProcedure is not ProcedureMap)
            {
                throw new InvalidOperationException("锁定地点仍然响应悬停或点击。");
            }
        }
    }
}
