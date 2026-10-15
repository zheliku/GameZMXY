using GameLogic.Architecture;
using GameLogic.Save.Slots;
using GameLogic.Event.Navigation;
using System;
using System.Linq;
using GameFramework.UI;
using GameLogic.Audio;
using GameLogic.Save;
using GameConfig.Sound;
using Godot;
using GodotGameFramework;

namespace GameLogic.UI;

/// <summary>六格存档窗口，订阅目录变化并以具名事件请求读档。</summary>
public partial class SaveForm : IController
{
    /// <summary>读取所属游戏流程的架构入口。</summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    private (TextureButton Button, Label Name, Label Date)[] m_SlotViews; // 六个存档格的控件引用，不另建格子脚本。

    /// <summary>绑定 GGF 字段与一次性的按钮订阅。</summary>
    /// <param name="serialId">窗口编号。</param>
    /// <param name="uiFormAssetName">资源路径。</param>
    /// <param name="uiGroup">界面组。</param>
    /// <param name="pauseCoveredUIForm">是否暂停被覆盖窗口。</param>
    /// <param name="isNewInstance">是否首次创建。</param>
    /// <param name="userData">本次参数。</param>
    public void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm,
        bool isNewInstance, object userData)
    {
        m_SerialId = serialId;
        m_UIFormAssetName = uiFormAssetName;
        m_UIGroup = uiGroup;
        m_DepthInUIGroup = 0;
        m_PauseCoveredUIForm = pauseCoveredUIForm;
        UIStringKeys.ForEach(key => key.SetLocalizationValue());
        if (!isNewInstance)
            return;
        m_Close.Pressed += OnCloseClicked;
        var buttons = m_Slots.GetChildren().Cast<TextureButton>().ToArray();
        if (buttons.Length != SaveModel.SlotCount)
            throw new InvalidOperationException("存档页必须为六格。");
        m_SlotViews = new (TextureButton, Label, Label)[buttons.Length];
        // 只在新建实例时收集本表单的固定格子，复用期间沿用引用与订阅。
        for (int i = 0; i < buttons.Length; i++)
        {
            int number = i + 1;
            var button = buttons[i];
            m_SlotViews[i] = (button, button.GetNode<Label>($"m_Name{number}"),
                button.GetNode<Label>($"m_Date{number}"));
            button.Pressed += () => SelectSlot(number);
        }
    }

    /// <summary>订阅目录并立即绘制当前状态。</summary>
    /// <param name="userData">未使用；目录由共享 SaveModel 提供。</param>
    public void OnOpen(object userData)
    {
        var saveModel = this.GetModel<SaveModel>();
        saveModel.Changed -= RefreshDirectory;
        saveModel.Changed += RefreshDirectory;
        RefreshDirectory();
        Visible = true;
    }

    /// <summary>解绑目录，关闭实例不保留本周期数据。</summary>
    /// <param name="isShutdown">是否框架关停。</param>
    /// <param name="userData">关闭参数。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        this.GetModel<SaveModel>().Changed -= RefreshDirectory;
        if (!isShutdown && IsInstanceValid(this))
            Visible = false;
    }

    /// <summary>清除回池窗口编号。</summary>
    public void OnRecycle()
    {
        m_SerialId = 0;
        m_DepthInUIGroup = 0;
        m_PauseCoveredUIForm = true;
        if (IsInstanceValid(this))
            Visible = false;
    }

    /// <summary>界面暂停；点击导航不依赖轮询。</summary>
    public void OnPause()
    {
    }

    /// <summary>恢复界面暂停。</summary>
    public void OnResume()
    {
    }

    /// <summary>窗口被覆盖。</summary>
    public void OnCover()
    {
    }

    /// <summary>窗口恢复显示。</summary>
    public void OnReveal()
    {
    }

    /// <summary>框架要求的更新入口；数值和导航通过事件更新。</summary>
    /// <param name="elapseSeconds">游戏帧间隔秒数。</param>
    /// <param name="realElapseSeconds">真实帧间隔秒数。</param>
    public void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
    }

    /// <summary>重新绑定显式传入的状态源。</summary>
    /// <param name="userData">新打开参数；null 保持当前绑定。</param>
    public void OnRefocus(object userData)
    {
        if (userData != null)
            OnOpen(userData);
    }

    /// <summary>记录框架分配的窗口层级。</summary>
    /// <param name="uiGroupDepth">界面组深度。</param>
    /// <param name="depthInUIGroup">窗口在组内的深度。</param>
    public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
    {
        m_DepthInUIGroup = depthInUIGroup;
        ZIndex = depthInUIGroup;
    }

    /// <summary>完整刷新六个格及加载反馈。</summary>
    private void RefreshDirectory()
    {
        var directory = this.GetModel<SaveModel>();
        bool ready = directory.Phase == SaveListState.Ready;
        m_Status.Text = directory.Phase == SaveListState.Failed ? directory.ErrorMessage : ready ? "" : "正在读取存档…";
        for (int i = 0; i < m_SlotViews.Length; i++)
        {
            var (button, name, date) = m_SlotViews[i];
            var slot = ready ? directory.Slots[i] : null;
            button.Disabled = slot == null || slot.State == SaveSlotState.Unreadable;
            button.MouseDefaultCursorShape = button.Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
            button.TooltipText = slot?.State switch
            {
                SaveSlotState.Empty => "空存档，点击开始新游戏",
                SaveSlotState.Occupied => $"{slot.HeroName} · Lv.{slot.Level}",
                SaveSlotState.Unreadable => "存档不可读，已保留原文件",
                _ => "正在读取存档"
            };
            name.Text = slot?.State == SaveSlotState.Occupied ? $"{slot.HeroName} · Lv.{slot.Level}" :
                slot?.State == SaveSlotState.Unreadable ? "存档不可读" : "";
            date.Text = slot?.State == SaveSlotState.Occupied
                ? slot.SavedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? ""
                : "";
        }
    }

    /// <summary>请求读取指定格，读取期间与损坏格拒绝点击。</summary>
    /// <param name="number">从 1 开始的格号。</param>
    private void SelectSlot(int number)
    {
        if (!GF.UI.HasUIForm(SerialId) || this.GetModel<SaveModel>().Phase != SaveListState.Ready ||
            this.GetModel<SaveModel>().Slots[number - 1].State == SaveSlotState.Unreadable)
            return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.Event.Fire(this, SaveSlotSelectedEventArgs.Create(SerialId, number));
    }

    /// <summary>关闭存档页，返回开始界面。</summary>
    private void OnCloseClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
            return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.UI.CloseUIForm(SerialId);
    }
}
