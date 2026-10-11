using GameLogic.Save.Slots;
using GameLogic.Event.Navigation;
using System;
using GameFramework.UI;
using GameLogic.Audio;
using GameLogic.Save;
using GameConfig.Sound;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Localization;
using GodotGameFramework.Sound;
using GodotGameFramework.UI;

namespace GameLogic.UI;

/// <summary>六格存档窗口，订阅目录变化并以具名事件请求读档。</summary>
public partial class SaveForm
{
    private SaveFormData m_Data; // 当前目录引用，关闭即解绑。
    private TextureButton[] m_Slots; // 六个固定的场景槽位。
    private Label[] m_Names; // 各格角色名称。
    private Label[] m_Dates; // 各格检查点时间。

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
        if (!isNewInstance) return;
        m_Slots = [m_Slot1, m_Slot2, m_Slot3, m_Slot4, m_Slot5, m_Slot6];
        m_Names = [m_Name1, m_Name2, m_Name3, m_Name4, m_Name5, m_Name6];
        m_Dates = [m_Date1, m_Date2, m_Date3, m_Date4, m_Date5, m_Date6];
        m_Close.Pressed += OnCloseClicked;
        m_Slot1.Pressed += OnSlot1Clicked;
        m_Slot2.Pressed += OnSlot2Clicked;
        m_Slot3.Pressed += OnSlot3Clicked;
        m_Slot4.Pressed += OnSlot4Clicked;
        m_Slot5.Pressed += OnSlot5Clicked;
        m_Slot6.Pressed += OnSlot6Clicked;
    }

    /// <summary>订阅目录并立即绘制当前状态。</summary>
    /// <param name="userData">SaveFormData。</param>
    /// <exception cref="ArgumentException">打开参数不正确。</exception>
    public void OnOpen(object userData)
    {
        ReleaseDirectory();
        m_Data = userData as SaveFormData ?? throw new ArgumentException("需要 SaveFormData。", nameof(userData));
        m_Data.Directory.Changed += RefreshDirectory;
        RefreshDirectory();
        Visible = true;
    }

    /// <summary>解绑目录，关闭实例不保留本周期数据。</summary>
    /// <param name="isShutdown">是否框架关停。</param>
    /// <param name="userData">关闭参数。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        ReleaseDirectory();
        if (!isShutdown && IsInstanceValid(this)) Visible = false;
    }

    /// <summary>清除回池窗口编号。</summary>
    public void OnRecycle()
    {
        ReleaseDirectory();
        m_SerialId = 0;
        m_DepthInUIGroup = 0;
        m_PauseCoveredUIForm = true;
        if (IsInstanceValid(this)) Visible = false;
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
        if (userData != null) OnOpen(userData);
    }

    /// <summary>记录框架分配的窗口层级。</summary>
    /// <param name="uiGroupDepth">界面组深度。</param>
    /// <param name="depthInUIGroup">窗口在组内的深度。</param>
    public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
    {
        m_DepthInUIGroup = depthInUIGroup;
        ZIndex = depthInUIGroup;
    }

    /// <summary>解除旧目录订阅。</summary>
    private void ReleaseDirectory()
    {
        if (m_Data != null) m_Data.Directory.Changed -= RefreshDirectory;
        m_Data = null;
    }

    /// <summary>完整刷新六个格及加载反馈。</summary>
    private void RefreshDirectory()
    {
        var directory = m_Data.Directory;
        bool ready = directory.Phase == SaveDirectoryPhase.Ready;
        m_Status.Text = directory.Phase == SaveDirectoryPhase.Failed ? directory.ErrorMessage : ready ? "" : "正在读取存档…";
        for (int index = 0; index < m_Slots.Length; index++)
        {
            var slot = ready ? directory.Slots[index] : null;
            m_Slots[index].Disabled = slot == null || slot.State == SaveSlotState.Unreadable;
            m_Slots[index].MouseDefaultCursorShape = m_Slots[index].Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
            m_Slots[index].TooltipText = slot?.State switch
            {
                SaveSlotState.Empty => "空存档，点击开始新游戏",
                SaveSlotState.Occupied => $"{slot.HeroName} · Lv.{slot.Level}",
                SaveSlotState.Unreadable => "存档不可读，已保留原文件",
                _ => "正在读取存档",
            };
            m_Names[index].Text = slot?.State == SaveSlotState.Occupied ? $"{slot.HeroName} · Lv.{slot.Level}" :
                slot?.State == SaveSlotState.Unreadable ? "存档不可读" : "";
            m_Dates[index].Text = slot?.State == SaveSlotState.Occupied
                ? slot.SavedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? ""
                : "";
        }
    }

    /// <summary>请求读取指定格，读取期间与损坏格拒绝点击。</summary>
    /// <param name="number">从 1 开始的格号。</param>
    private void SelectSlot(int number)
    {
        if (m_Data?.Directory.Phase != SaveDirectoryPhase.Ready ||
            m_Data.Directory.Slots[number - 1].State == SaveSlotState.Unreadable) return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.Event.Fire(this, SaveSlotSelectedEventArgs.Create(SerialId, number));
    }

    /// <summary>选择第一个存档格。</summary>
    private void OnSlot1Clicked() => SelectSlot(1);

    /// <summary>选择第二个存档格。</summary>
    private void OnSlot2Clicked() => SelectSlot(2);

    /// <summary>选择第三个存档格。</summary>
    private void OnSlot3Clicked() => SelectSlot(3);

    /// <summary>选择第四个存档格。</summary>
    private void OnSlot4Clicked() => SelectSlot(4);

    /// <summary>选择第五个存档格。</summary>
    private void OnSlot5Clicked() => SelectSlot(5);

    /// <summary>选择第六个存档格。</summary>
    private void OnSlot6Clicked() => SelectSlot(6);

    /// <summary>关闭存档页，返回开始界面。</summary>
    private void OnCloseClicked()
    {
        if (m_Data == null) return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.UI.CloseUIForm(SerialId);
    }
}
