//------------------------------------------------------------
// 预加载流程：准备完成后加载语言与常驻界面
//------------------------------------------------------------

using System;
using GameConfig;
using GameLogic.UI;
using GameFramework.Localization;
using GameFramework.Procedure;
using GodotGameFramework;
using GodotGameFramework.UI;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 预加载流程：应用准备已完成；加载语言与常驻界面，成功后进入标题。
/// 任一步失败即停在加载界面并记录致命错误，不进入游戏。
/// </summary>
public class ProcedurePreload : ProcedureBase
{
    private int m_EntrySerial; // 离开后忽略旧加载结果及取消异常。

    /// <summary>加载语言和常驻提示界面，再进入标题流程。</summary>
    /// <param name="procedureOwner">当前流程状态机。</param>
    protected internal async override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        int entrySerial = ++m_EntrySerial;

        try
        {
            // 运行时恢复保存语言，编辑器资源模式跟随编辑器或系统语言。
            if (!GF.Base.EnableEditorResLoad)
                GF.Localization.Language = (Language)GF.Setting.GetInt("Language", (int)Language.English);
            else
            {
                GF.Localization.Language = GF.Base.EditorLanguage != Language.Unspecified
                    ? GF.Base.EditorLanguage : GF.Localization.SystemLanguage;
                Log.Info("[ProcedurePreload] Editor res load enabled, set language to SystemLanguage: {0}.", GF.Localization.Language);
            }
        }
        catch (Exception ex)
        {
            Log.Fatal("[ProcedurePreload] 加载本地化失败: {0}", ex);
            return;
        }

        try
        {
            await GF.UI.OpenLoadingUIFormAsync();
        }
        catch (Exception ex)
        {
            if (entrySerial != m_EntrySerial)
                return;
            Log.Fatal("[ProcedurePreload] 打开加载界面失败: {0}", ex);
            return;
        }

        if (entrySerial != m_EntrySerial)
            return;
        // 主流程 FSM 是唯一共享存储；业务系统绑定明确的 Architecture，不接管注册和移除能力。
        // 先建立应用级提示监听，再允许标题流程新建档案；切页不会关闭这张非交互窗口。
        try
        {
            var notice = await GF.UI.OpenUIFormAsync<SaveNoticeForm>(UIFormId.SaveNoticeForm);
            if (entrySerial != m_EntrySerial)
            {
                GF.UI.CloseUIForm(notice.SerialId);
                return;
            }
        }
        catch (Exception ex)
        {
            if (entrySerial != m_EntrySerial)
                return;
            Log.Fatal("[ProcedurePreload] 保存提示初始化失败：{0}", ex);
            return;
        }
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

}
