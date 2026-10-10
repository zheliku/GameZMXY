//------------------------------------------------------------
// 读档流程：读取当前存档槽（含版本迁移与建档），创建档案作用域
//------------------------------------------------------------

using System;
using GameFramework.Procedure;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.Session;
using GodotGameFramework;
using GodotGameFramework.UI;
using GameConfig.Constant;
using GameLogic;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 读档流程：经 <see cref="SaveService"/> 读取当前存档（首次建档、旧版迁移），
/// 创建 <see cref="GameContext"/> 写入流程数据，再进入关卡流程。
/// 读档失败时停在加载界面，绝不新建空档覆盖玩家数据。
/// </summary>
public class ProcedureLoadProfile : ProcedureBase
{
    /// <summary>读档并创建档案作用域。</summary>
    /// <param name="procedureOwner">当前流程状态机。</param>
    protected internal override async void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        try
        {
            // 预加载流程已校验配置并放入经验曲线。
            ExperienceCurve curve = procedureOwner.GetData<ExperienceCurveVariable>(ProcedurePreload.CurveDataKey)?.Value
                ?? throw new InvalidOperationException("流程数据缺少经验曲线（预加载流程未完成）。");
            SaveService save = new(curve, ConfigSystem.Instance.Tables);
            LoadedProfile loaded = await save.LoadOrCreateAsync();
            Log.Info("[ProcedureLoadProfile] 档案就绪：来源 {0}，出战英雄 {1}，等级 {2}", loaded.Origin,
                loaded.Profile.ActiveHeroId, loaded.Profile.ActiveHero.Progression.Level);

            GameContext context = new(loaded.Profile, curve, ConfigSystem.Instance.Tables, save);
            procedureOwner.SetData(GameContext.DataKey, GameContextVariable.Create(context));
            ChangeState<ProcedureLevel>(procedureOwner);
        }
        catch (Exception ex)
        {
            Log.Fatal("[ProcedureLoadProfile] 读档失败：{0}", ex);
            (GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) as LoadingForm)?.SetLogState("存档读取失败", 0);
        }
    }
}
