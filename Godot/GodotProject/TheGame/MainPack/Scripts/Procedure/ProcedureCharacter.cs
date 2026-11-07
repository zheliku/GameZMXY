using IUIForm = GameFramework.UI.IUIForm;
using GameLogic.UI;
using GameConfig.Constant;
using GameLogic.Save;
using GameLogic.Architecture;
using GameLogic.Procedure.FlowData;
using GameLogic.Audio;
using GameFramework.Procedure;
using GameConfig;
using GameConfig.Sound;
using GameFramework.Event;
using GameLogic;
using GodotGameFramework;
using GodotGameFramework.UI;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 新档选角：首次建档前确定英雄身份，再交给读档流程创建档案。
/// </summary>
public sealed class ProcedureCharacter : ProcedureBase
{
    private int m_FormSerialId; // 本周期窗口，含在途加载请求。
    private int m_MusicSerialId; // 本周期 GGF 音乐请求。
    private ProcedureOwner m_Owner; // 仅当前流程周期借用主 FSM。

    /// <summary>
    /// 订阅本页面的点击事件后打开窗口。
    /// </summary>
    /// <param name="procedureOwner">流程状态机。</param>
    protected internal override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        m_Owner = procedureOwner;
        GF.Event.Subscribe(OpenUIFormSuccessEventArgs.EventId, OnFormOpened);
        GF.Event.Subscribe(OpenUIFormFailureEventArgs.EventId, OnFormFailed);
        m_FormSerialId = GF.UI.OpenUIForm(EUIFormId.CharacterForm);
        SetFormCallbacks(GF.UI.GetUIForm(m_FormSerialId), bind: true);
        m_MusicSerialId = GF.Sound.PlayConfigured(ESoundId.MenuTheme, 0.3f);
    }

    /// <summary>
    /// 退出时解除点击事件和本周期窗口。
    /// </summary>
    /// <param name="procedureOwner">流程状态机。</param>
    /// <param name="isShutdown">是否框架关停。</param>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        SetFormCallbacks(GF.UI.GetUIForm(m_FormSerialId), bind: false);

        GF.Event.Unsubscribe(OpenUIFormSuccessEventArgs.EventId, OnFormOpened);
        GF.Event.Unsubscribe(OpenUIFormFailureEventArgs.EventId, OnFormFailed);
        if (m_FormSerialId != 0 && (GF.UI.HasUIForm(m_FormSerialId) || GF.UI.IsLoadingUIForm(m_FormSerialId)))
        {
            GF.UI.CloseUIForm(m_FormSerialId);
        }

        if (m_MusicSerialId != 0)
        {
            GF.Sound.StopSound(m_MusicSerialId, isShutdown ? 0f : 0.2f);
        }
        m_FormSerialId = m_MusicSerialId = 0;
        m_Owner = null;
        base.OnLeave(procedureOwner, isShutdown);
    }

    /// <summary>
    /// 空槽确认英雄后才进入读档流程，首次快照直接写入所选角色。
    /// </summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    /// <param name="heroId">当前窗口明确选择的英雄配置编号。</param>
    private void OnCharacterConfirmed(int formSerialId, int heroId)
    {
        if (formSerialId == m_FormSerialId)
        {
            var selection = m_Owner.GetData<SaveSelectionVariable>(FlowKeys.SaveSelection);
            selection.Value = (selection.Value.SlotNumber, selection.Value.UnitId, heroId);
            ChangeState<ProcedureLoadGame>(m_Owner);
        }
    }

    /// <summary>
    /// 本周期界面可见后关闭加载页。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">只在本次分发内读取的池化参数。</param>
    private void OnFormOpened(object sender, GameEventArgs args)
    {
        if (args is OpenUIFormSuccessEventArgs opened)
        {
            SetFormCallbacks(opened.UIForm, bind: true);
        }

        if (args is not OpenUIFormSuccessEventArgs success || success.UIForm.SerialId != m_FormSerialId)
        {
            return;
        }

        (GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) as LoadingForm)?.CloseLoading();
    }

    /// <summary>
    /// 报告当前窗口请求的装配错误。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">只在本次分发内读取的池化参数。</param>
    private void OnFormFailed(object sender, GameEventArgs args)
    {
        if (args is OpenUIFormFailureEventArgs failure && failure.SerialId == m_FormSerialId)
        {
            Log.Error("[ProcedureCharacter] 界面加载失败：{0}", failure.ErrorMessage);
        }
    }

    /// <summary>
    /// 按实际窗口类型绑定本流程回调；窗口编号拒绝旧作用域和无关实例。
    /// </summary>
    /// <param name="form">已打开窗口；在途时为空。</param>
    /// <param name="bind">绑定或解除。</param>
    private void SetFormCallbacks(IUIForm form, bool bind)
    {
        if (bind && (form == null || !GF.UI.IsValidUIForm(form)))
        {
            return;
        }

        switch (form)
        {
            case CharacterForm characterForm when characterForm.SerialId == m_FormSerialId:
                characterForm.CharacterConfirmed -= OnCharacterConfirmed;
                if (bind)
                {
                    characterForm.CharacterConfirmed += OnCharacterConfirmed;
                }

                break;
        }
    }
}
