using GameLogic.Startup.FlowData;
//------------------------------------------------------------
// 读档流程：读取当前存档槽（当前格式读取或建档），创建档案作用域
//------------------------------------------------------------

using System;
using GameFramework.Procedure;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic;
using GameLogic.Level;
using GodotGameFramework;
using GodotGameFramework.UI;
using GameConfig.Constant;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 读档流程：经 <see cref="SaveModule"/> 读取当前存档（首次建档或当前格式读取），
/// 创建 <see cref="GameContext"/> 写入流程数据，新档进入选角，已有档案直接进入地图。
/// 读档失败时停在加载界面，绝不新建空档覆盖玩家数据。
/// </summary>
public class ProcedureLoadProfile : ProcedureBase
{
    private int m_EntrySerial; // 每次进出使旧异步结果失效，不让离开后的读档覆盖新作用域。
    /// <summary>读档并创建档案作用域。</summary>
    /// <param name="procedureOwner">当前流程状态机。</param>
    protected internal override async void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        int entrySerial = ++m_EntrySerial;
        try
        {
            // 标题界面已关闭加载页；读档期间重新显示框架共享加载界面。
            await GF.UI.OpenLoadingUIFormAsync();
            if (entrySerial != m_EntrySerial) return;
            // 预加载流程已校验配置并放入经验曲线。
            ExperienceCurve curve = procedureOwner.GetData<ExperienceCurveVariable>(FlowKeys.ExperienceCurve)?.Value
                ?? throw new InvalidOperationException("流程数据缺少经验曲线（预加载流程未完成）。");
            SaveModule save = new(curve, ConfigSystem.Instance.Tables);
            var selection = procedureOwner.GetData<SaveSelectionVariable>(FlowKeys.SaveSelection)?.Value
                ?? throw new InvalidOperationException("缺少存档选择结果。");
            SaveLoadResult loaded = await save.LoadOrCreateAsync(selection.SlotNumber, selection.UnitId);
            if (entrySerial != m_EntrySerial) return;
            Log.Info("[ProcedureLoadProfile] 档案就绪：来源 {0}，出战英雄 {1}，等级 {2}", loaded.Origin,
                loaded.Profile.ActiveHeroId, loaded.Profile.ActiveHero.Level);

            GameContext session = new(loaded.Profile, save);
            procedureOwner.SetData(FlowKeys.GameContext, GameContextVariable.Create(session));
            // 新建档案才需要确认角色；已有档案直接恢复地图入口。
            if (loaded.Origin == SaveOrigin.Created)
                ChangeState<ProcedureCharacter>(procedureOwner);
            else
                ChangeState<ProcedureMap>(procedureOwner);
        }
        catch (Exception ex)
        {
            if (entrySerial != m_EntrySerial) return;
            Log.Fatal("[ProcedureLoadProfile] 读档失败：{0}", ex);
            (GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) as LoadingForm)?.SetLogState("存档读取失败", 0);
        }
    }

    /// <summary>使尚未完成的加载结果失效；磁盘读写仍由 SaveModule 串行完成。</summary>
    /// <param name="procedureOwner">当前流程状态机。</param>
    /// <param name="isShutdown">是否框架关停。</param>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        ++m_EntrySerial;
        base.OnLeave(procedureOwner, isShutdown);
    }
}
