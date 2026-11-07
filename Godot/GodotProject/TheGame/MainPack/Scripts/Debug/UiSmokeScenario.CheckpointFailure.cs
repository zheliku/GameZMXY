using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GameLogic.Architecture;
using GameLogic.Level;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.UI;
using Godot;
using GodotGameFramework;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 在结算真实按钮处注入faulted检查点任务，验证原生async void边界与地图手动保存恢复。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 文件本身不损坏；只替换已完成检查点的任务，内存收益必须仍可保存到同一隔离档案。
    /// </summary>
    /// <param name="flow">正式主流程FSM。</param>
    /// <returns>结算导航、地图重新保存与磁盘读回完成的任务。</returns>
    private async Task CheckCheckpointFailureAsync(ProcedureOwner flow)
    {
        flow.GetSystem<ProfileSystem>().AddSoul(17);
        int soul = flow.GetModel<ProfileModel>().Soul.Value;
        KillHero(FindHero());
        await WaitUntilAsync(() => GF.UI.GetAllLoadedUIForms().OfType<SettlementForm>().Any(form => form.Visible),
            "检查点异常夹具未显示真实失败结算");
        LevelSystem system = flow.GetSystem<LevelSystem>();
        Check(await system.CheckpointWrite, "异常注入前的隔离检查点未成功完成");
        typeof(LevelSystem).GetProperty(nameof(LevelSystem.CheckpointWrite)).SetValue(system,
            Task.FromException<bool>(new InvalidOperationException("checkpoint failure probe")));
        SettlementForm settlement = GF.UI.GetAllLoadedUIForms().OfType<SettlementForm>().Single(form => form.Visible);
        await WaitUntilAsync(() => SmokeInspection.CallbackCount(settlement, "MapRequested") == 1,
            "结算窗口未完成流程回调绑定");
        settlement.GetNode<BaseButton>("Content/Actions/m_ReturnMap").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntilAsync(() => GF.Procedure.CurrentProcedure is ProcedureMap &&
            GF.UI.GetAllLoadedUIForms().OfType<MapForm>().Any(form => form.Visible),
            "faulted检查点任务阻止了按既有失败语义返回地图");
        Check(flow.GetModel<ProfileModel>().Soul.Value == soul, "检查点任务异常丢失了已提交的内存收益");
        MapForm map = GF.UI.GetAllLoadedUIForms().OfType<MapForm>().Single(form => form.Visible);
        map.GetNode<BaseButton>("m_Save").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntilAsync(() => !map.GetNode<BaseButton>("m_Save").Disabled, "地图未恢复手动保存");
        Check(await flow.GetSystem<SaveSystem>().PendingWrite, "异常后的地图手动保存失败");
        Check((await ReadDiskAsync()).Soul == soul, "异常后重新保存未写入同一档案的收益");
        GD.Print("SMOKE-CHECKPOINT FAILURE PASS: faulted task contained; settlement -> map; memory retained and manual save reread");
    }
}
