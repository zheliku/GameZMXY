using System;
using System.Linq;
using GameLogic;
using GameLogic.Architecture;
using GameLogic.Heroes;
using GameLogic.Level;
using GameLogic.Profile;
using GameLogic.UI;
using GameConfig.Constant;
using Godot;
using GodotGameFramework;

/// <summary>真实新档依次通关三关，每次退出到标题再读取同一槽，检查最新提示和锁定输入。</summary>
public sealed class MapProgressSmokeScenario(Node driver)
{
    private int m_LevelId = 1; // 下一次实际挑战的关卡。
    private int m_Step; // 0 等待地图，1 战斗，2 退出重读，3 重读检查。
    private int m_ClosedMapSerial; // 刚关闭的地图，用于拒绝读取过渡期间的旧窗口。
    private LevelSmokeScenario m_Run; // 本次真实五波、出口与检查点回归。
    private double m_Time; // 等待导航的总秒数。

    /// <summary>回归已经完成或失败。</summary>
    public bool IsDone { get; private set; }

    /// <summary>首个失败原因。</summary>
    public string Failure { get; private set; }

    /// <summary>在真实物理帧之后推进菜单与关卡操作。</summary>
    /// <param name="delta">本次游戏秒数。</param>
    public void Update(double delta)
    {
        if (IsDone)
            return;
        try
        {
            m_Time += delta;
            if (m_Time > 600)
                throw new InvalidOperationException("三关解锁回归超时。");
            if (m_Step == 4)
            {
                if (GF.Procedure.CurrentProcedure is not ProcedureLevel procedure)
                {
                    // 窗口可见与打开成功回调可能相差一帧，重复实际点击直至流程接管。
                    var map = VisibleMap();
                    if (map != null)
                    {
                        string name = m_LevelId switch { 1 => "m_Huaguoshan", 2 => "m_Shuiliandong", _ => "m_Taohuayuan" };
                        map.GetNode<BaseButton>(name).EmitSignal(BaseButton.SignalName.Pressed);
                    }
                    return;
                }
                var level = SmokeInspection.Level(procedure);
                var hero = driver.GetTree().Root.FindChildren("*", "CharacterBody2D", true, false)
                    .OfType<AbstractHeroEntity>().FirstOrDefault(value => value.IsShown);
                if (level != null && hero != null && GF.UI.GetUIForm(ResourcesCollectionConstant.UIs_battle_form) is BattleForm { Visible: true })
                {
                    m_Run = new LevelSmokeScenario(hero, level);
                    m_Step = 1;
                }
                return;
            }
            if (m_Step == 1)
            {
                m_Run.Update(delta);
                if (!m_Run.IsDone)
                    return;
                if (m_Run.Failures.Count != 0)
                    throw new InvalidOperationException(string.Join("; ", m_Run.Failures));
                var map = VisibleMap();
                CheckMap(map, m_LevelId);
                m_ClosedMapSerial = map.SerialId;
                map.GetNode<BaseButton>("m_Back").EmitSignal(BaseButton.SignalName.Pressed);
                m_Step = 2;
                return;
            }
            if (m_Step == 2)
            {
                if (GF.Procedure.CurrentProcedure is ProcedureMap)
                    return;
                m_Step = 3;
            }
            var current = VisibleMap();
            if (current == null || current.SerialId == m_ClosedMapSerial)
            {
                // 仅推进真实菜单到地图，不调用 AdvanceToLevel 的关卡夹具准备。
                var forms = GF.UI.GetAllLoadedUIForms().OfType<Control>().Where(form => form.Visible);
                var form = forms.FirstOrDefault(value => value is SaveForm) ?? forms.FirstOrDefault(value => value is StartForm or CharacterForm);
                string path = form switch { StartForm => "m_Start", SaveForm => "Slots/m_Slot1", CharacterForm => "Roles/m_Wukong", _ => null };
                if (path != null && !form.GetNode<BaseButton>(path).Disabled)
                    form.GetNode<BaseButton>(path).EmitSignal(BaseButton.SignalName.Pressed);
                return;
            }
            if (m_Step == 3)
            {
                CheckMap(current, m_LevelId);
                GD.Print($"SMOKE-PROGRESS RELOAD: cleared={m_LevelId}, latest={GF.Fsm.GetSystem<ProfileSystem>().LatestLevelId}");
                if (m_LevelId == 3)
                {
                    IsDone = true;
                    return;
                }
                m_LevelId++;
                m_Step = 0;
            }
            CheckMap(current, m_LevelId - 1);
            string location = m_LevelId switch { 1 => "m_Huaguoshan", 2 => "m_Shuiliandong", _ => "m_Taohuayuan" };
            current.GetNode<BaseButton>(location).EmitSignal(BaseButton.SignalName.Pressed);
            m_Step = 4;
        }
        catch (Exception error)
        {
            Failure = error.ToString();
            IsDone = true;
        }
    }

    /// <summary>返回当前真实地图实例。</summary>
    /// <returns>可见的地图窗口，加载中为空。</returns>
    private static MapForm VisibleMap() => GF.UI.GetUIForm(ResourcesCollectionConstant.UIs_map_form) as MapForm is { Visible: true } map ? map : null;

    /// <summary>逐个地点核对解锁、原生提示以及直接发送锁定信号的拒绝。</summary>
    /// <param name="map">当前地图。</param>
    /// <param name="cleared">应已通关的最远编号。</param>
    private static void CheckMap(MapForm map, int cleared)
    {
        if (map == null || GF.Fsm.GetModel<ProfileModel>().HighestClearedLevelId.Value != cleared)
            throw new InvalidOperationException("重读或通关返回后地图进度错误。");
        int latest = Math.Min(3, cleared + 1);
        var player = map.GetNode<AnimationPlayer>("m_LocationAnimation");
        string animation = latest switch { 1 => "location/current", 2 => "shuiliandong/current", _ => "taohuayuan/current" };
        if (player.CurrentAnimation != animation)
            throw new InvalidOperationException($"最新提示错误：{player.CurrentAnimation}，期望 {animation}");
        foreach (var (name, id) in new[] { ("m_Huaguoshan", 1), ("m_Shuiliandong", 2), ("m_Taohuayuan", 3) })
        {
            var button = map.GetNode<BaseButton>(name);
            if (button.Disabled != (id > latest))
                throw new InvalidOperationException("地图解锁状态错误。");
            if (button.GetNode<CanvasItem>("LocationGlow").Visible != (id == latest))
                throw new InvalidOperationException("默认地图同时显示了多个关卡提示。");
            if (id <= latest)
                continue;
            button.EmitSignal(Control.SignalName.MouseEntered);
            button.EmitSignal(BaseButton.SignalName.Pressed);
            if (player.CurrentAnimation != animation || GF.Procedure.CurrentProcedure is not ProcedureMap)
                throw new InvalidOperationException("锁定地点仍然响应悬停或点击。");
        }
    }
}
