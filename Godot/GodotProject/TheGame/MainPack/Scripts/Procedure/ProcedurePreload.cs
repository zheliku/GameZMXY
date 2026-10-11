using GameLogic.Startup.FlowData;
//------------------------------------------------------------
// 预加载流程：注册实体/UI/声音分组与本地化、启动节点池，并集中校验配置
//------------------------------------------------------------

using System;
using GameFramework.Localization;
using GameFramework.Procedure;
using GameLogic.Config;
using GameLogic.Profile;
using GameLogic.Session;
using GameLogic.Startup;
using GodotGameFramework;
using GodotGameFramework.NodePool;
using GodotGameFramework.UI;
using GameLogic;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 预加载流程：资源组与本地化注册、节点池启动、配置集中校验；全部成功后进入标题流程。
/// 任一步失败即停在加载界面并记录致命错误，不进入游戏。
/// </summary>
public class ProcedurePreload : ProcedureBase
{
    private int m_EntrySerial; // 离开后忽略旧加载结果及取消异常。

    /// <summary>加载语言及 UI、实体和声音分组，校验配置，再进入标题流程。</summary>
    /// <param name="procedureOwner">当前流程状态机。</param>
    protected internal async override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        int entrySerial = ++m_EntrySerial;
        bool groupsLoaded = GameResourceGroups.Register();

        try
        {
            LoadLocalization();
        }
        catch (Exception ex)
        {
            Log.Fatal("[ProcedurePreload] 加载本地化失败: {0}", ex);
            groupsLoaded = false;
        }

        NodePool.Instance.Active(); // 启动节点池
        LayerMask.Instance.Active(); // 启动层级工具
        LoadingForm loadingForm;
        try
        {
            loadingForm = await GF.UI.OpenLoadingUIFormAsync();
        }
        catch (Exception ex)
        {
            if (entrySerial != m_EntrySerial) return;
            Log.Fatal("[ProcedurePreload] 打开加载界面失败: {0}", ex);
            return;
        }

        if (entrySerial != m_EntrySerial) return;
        if (!groupsLoaded)
        {
            Log.Fatal("[ProcedurePreload] 必要资源组加载失败，停止进入游戏。");
            loadingForm?.SetLogState("资源加载失败", 0);
            return;
        }

        // 跨表约束在这里一次性校验，运行期不再各自兜底；失败即停止启动。
        ExperienceCurve curve;
        try
        {
            curve = ConfigValidator.ValidateAll(ConfigSystem.Instance.Tables);
        }
        catch (Exception ex)
        {
            Log.Fatal("[ProcedurePreload] {0}", ex.Message);
            loadingForm?.SetLogState("配置校验失败", 0);
            return;
        }

        procedureOwner.SetData(FlowKeys.ExperienceCurve, ExperienceCurveVariable.Create(curve));
        ChangeState<ProcedureTitle>(procedureOwner);
    }
    /// <summary>使在途加载结果失效，不让旧周期继续启动游戏。</summary>
    /// <param name="procedureOwner">流程状态机。</param>
    /// <param name="isShutdown">是否框架关停。</param>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        ++m_EntrySerial;
        base.OnLeave(procedureOwner, isShutdown);
    }

    /// <summary>根据用户设置或编辑器资源模式选择界面语言。</summary>
    private void LoadLocalization()
    {
        // 运行时恢复保存语言，编辑器资源模式跟随编辑器或系统语言。
        if (!GF.Base.EnableEditorResLoad)
        {
            GF.Localization.Language = (Language)GF.Setting.GetInt("Language", (int)Language.English);
        }
        else
        {
            GF.Localization.Language = GF.Base.EditorLanguage != Language.Unspecified ? GF.Base.EditorLanguage : GF.Localization.SystemLanguage;
            Log.Info("[ProcedurePreload] Editor res load enabled, set language to SystemLanguage: {0}.", GF.Localization.Language);
        }
    }
}
