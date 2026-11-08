using System;
using System.Linq;
using GameConfig.Sound;
using GameFramework.UI;
using GameLogic.Architecture;
using GameLogic.Audio;
using GameLogic.Save;
using GameLogic.Save.Slots;
using Godot;
using GodotGameFramework;

namespace GameLogic.UI;

/// <summary>
/// 六格存档窗口，订阅目录变化并以具名事件请求读档。
/// </summary>
public partial class SaveForm : IController
{
    private bool _isBound; // 本周期拥有目录订阅，关闭或失败后不得在回池时重取模型。

    private (TextureButton Button, Label Name, Label Date)[] _slotViews; // 六个存档格的控件引用，不另建格子脚本。
    private TextureButton[] _deleteButtons; // 六个格内的删除入口，只有已有可读档案启用。
    private Control _deleteConfirmation; // 存档页自己的确认层，保持前端视觉一致。
    private Label _deleteMessage; // 当前确认格号和不可恢复提示。
    private TextureButton _deleteCancel; // 取消确认，不触碰存档。
    private TextureButton _deleteConfirm; // 确认后才向流程提交删除请求。
    private int _deleteSlot; // 当前等待确认的格号。

    /// <summary>
    /// 由所属流程处理 SaveSlotSelected；参数为本次窗口编号及选择编号。
    /// </summary>
    public event Action<int, int> SaveSlotSelected;

    /// <summary>
    /// 由所属流程处理删除存档请求；参数为窗口编号及格号。
    /// </summary>
    public event Action<int, int> SaveSlotDeleteRequested;

    /// <summary>
    /// 绑定 GGF 字段与一次性的按钮订阅。
    /// </summary>
    /// <param name="serialId">窗口编号。</param>
    /// <param name="uiFormAssetName">资源路径。</param>
    /// <param name="uiGroup">界面组。</param>
    /// <param name="pauseCoveredUIForm">是否暂停被覆盖窗口。</param>
    /// <param name="isNewInstance">是否首次创建。</param>
    /// <param name="userData">本次参数。</param>
    public void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm,
        bool isNewInstance, object userData)
    {
        _serialId = serialId;
        _uiFormAssetName = uiFormAssetName;
        _uiGroup = uiGroup;
        _depthInUIGroup = 0;
        _pauseCoveredUIForm = pauseCoveredUIForm;
        UIStringKeys.ForEach(key => key.SetLocalizationValue());
        if (!isNewInstance)
        {
            return;
        }

        _close.Pressed += OnCloseClicked;
        _slotViews =
        [
            (_slot1, _name1, _date1), (_slot2, _name2, _date2),
        (_slot3, _name3, _date3), (_slot4, _name4, _date4),
        (_slot5, _name5, _date5), (_slot6, _name6, _date6)
        ];
        if (_slotViews.Length != SaveModel.SlotCount ||
            _slotViews.Any(slot => slot.Button == null || slot.Name == null || slot.Date == null))
        {
            throw new InvalidOperationException("存档页必须为六格。");
        }
        _deleteButtons = _slotViews.Select(slot => slot.Button.GetNode<TextureButton>("Delete")).ToArray();
        _deleteConfirmation = GetNode<Control>("DeleteConfirmation");
        _deleteMessage = _deleteConfirmation.GetNode<Label>("Panel/Message");
        _deleteCancel = _deleteConfirmation.GetNode<TextureButton>("Panel/Cancel");
        _deleteConfirm = _deleteConfirmation.GetNode<TextureButton>("Panel/Confirm");
        // 只在新建实例时收集本表单的固定格子，复用期间沿用引用与订阅。
        for (int i = 0; i < _slotViews.Length; i++)
        {
            int number = i + 1;
            _slotViews[i].Button.Pressed += () => SelectSlot(number);
            _deleteButtons[i].Pressed += () => RequestDelete(number);
            _deleteButtons[i].MouseEntered += () => SetDeleteHover(number, true);
            _deleteButtons[i].MouseExited += () => SetDeleteHover(number, false);
        }
        _deleteCancel.Pressed += CancelDelete;
        _deleteConfirm.Pressed += ConfirmDelete;
    }

    /// <summary>
    /// 订阅目录并立即绘制当前状态。
    /// </summary>
    /// <param name="userData">未使用；目录由共享 SaveModel 提供。</param>
    public void OnOpen(object userData)
    {
        Unbind();
        _isBound = true;
        try
        {
            this.GetModel<SaveModel>().Directory.RegisterWithInitValue(RefreshDirectory);
            Visible = true;
        }
        catch
        {
            Unbind();
            throw;
        }
    }

    /// <summary>
    /// 解绑目录，关闭实例不保留本周期数据。
    /// </summary>
    /// <param name="isShutdown">是否框架关停。</param>
    /// <param name="userData">关闭参数。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        SaveSlotSelected = null;
        SaveSlotDeleteRequested = null;
        CancelDelete();
        Unbind();
        if (!isShutdown && IsInstanceValid(this))
        {
            Visible = false;
        }
    }

    /// <summary>
    /// 清除回池窗口编号。
    /// </summary>
    public void OnRecycle()
    {
        CancelDelete();
        Unbind();
        _serialId = 0;
        _depthInUIGroup = 0;
        _pauseCoveredUIForm = true;
        if (IsInstanceValid(this))
        {
            Visible = false;
        }
    }

    /// <summary>
    /// 界面暂停；点击导航不依赖轮询。
    /// </summary>
    public void OnPause()
    {
    }

    /// <summary>
    /// 恢复界面暂停。
    /// </summary>
    public void OnResume()
    {
    }

    /// <summary>
    /// 窗口被覆盖。
    /// </summary>
    public void OnCover()
    {
    }

    /// <summary>
    /// 窗口恢复显示。
    /// </summary>
    public void OnReveal()
    {
    }

    /// <summary>
    /// 框架要求的更新入口；数值和导航通过事件更新。
    /// </summary>
    /// <param name="elapseSeconds">游戏帧间隔秒数。</param>
    /// <param name="realElapseSeconds">真实帧间隔秒数。</param>
    public void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
    }

    /// <summary>
    /// 读取所属游戏流程的架构入口。
    /// </summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    /// <summary>
    /// 结束当前目录订阅，允许关闭后回池再次清理而不访问旧作用域。
    /// </summary>
    private void Unbind()
    {
        if (!_isBound)
        {
            return;
        }

        _isBound = false;
        this.GetModel<SaveModel>().Directory.UnRegister(RefreshDirectory);
    }

    /// <summary>
    /// 重新绑定显式传入的状态源。
    /// </summary>
    /// <param name="userData">新打开参数；null 保持当前绑定。</param>
    public void OnRefocus(object userData)
    {
        if (userData != null)
        {
            OnOpen(userData);
        }
    }

    /// <summary>
    /// 记录框架分配的窗口层级。
    /// </summary>
    /// <param name="uiGroupDepth">界面组深度。</param>
    /// <param name="depthInUIGroup">窗口在组内的深度。</param>
    public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
    {
        _depthInUIGroup = depthInUIGroup;
        ZIndex = depthInUIGroup;
    }

    /// <summary>
    /// 完整刷新六个格及加载反馈。
    /// </summary>
    /// <param name="directory">已原子提交的目录快照。</param>
    private void RefreshDirectory(SaveDirectoryState directory)
    {
        bool ready = directory.Phase == SaveListState.Ready;
        _status.Text = directory.Phase switch
        {
            SaveListState.Ready => "",
            SaveListState.Failed => directory.ErrorMessage,
            _ => "正在读取存档…"
        };
        for (int i = 0; i < _slotViews.Length; i++)
        {
            (TextureButton button, Label name, Label date) = _slotViews[i];
            SaveSlotInfo slot = ready ? directory.Slots[i] : null;
            button.Disabled = slot == null || slot.State == SaveSlotState.Unreadable;
            button.MouseDefaultCursorShape = button.Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
            bool canDelete = ready && slot?.State == SaveSlotState.Occupied;
            _deleteButtons[i].Visible = canDelete;
            _deleteButtons[i].Disabled = !canDelete;
            _deleteButtons[i].Modulate = canDelete ? new Color(1f, 1f, 1f, 0.42f) : Colors.Transparent;
            _deleteButtons[i].MouseDefaultCursorShape = !canDelete
                ? CursorShape.Arrow : CursorShape.PointingHand;
            if (slot?.State == SaveSlotState.Occupied)
            {
                name.Text = string.Join(" · ", slot.Heroes.Select(hero => hero.Name));
                date.Text = slot.SavedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "";
            }
            else
            {
                name.Text = slot?.State == SaveSlotState.Unreadable ? "存档不可读" : "";
                date.Text = "";
            }
        }
    }

    /// <summary>
    /// 鼠标靠近时提亮可用的删除入口。
    /// </summary>
    /// <param name="number">存档格号。</param>
    /// <param name="hovered">是否处于鼠标悬停。</param>
    private void SetDeleteHover(int number, bool hovered) => _deleteButtons[number - 1].Modulate = hovered ? Colors.White : new Color(1f, 1f, 1f, 0.42f);

    /// <summary>
    /// 请求读取指定格，读取期间与损坏格拒绝点击。
    /// </summary>
    /// <param name="number">从 1 开始的格号。</param>
    private void SelectSlot(int number)
    {
        if (!GF.UI.HasUIForm(SerialId) || this.GetModel<SaveModel>().Phase != SaveListState.Ready ||
            this.GetModel<SaveModel>().Slots[number - 1].State == SaveSlotState.Unreadable)
        {
            return;
        }

        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        SaveSlotSelected?.Invoke(SerialId, number);
    }

    /// <summary>
    /// 打开存档页内的确认层，不直接删除档案。
    /// </summary>
    /// <param name="number">要删除的格号。</param>
    private void RequestDelete(int number)
    {
        if (!GF.UI.HasUIForm(SerialId) || this.GetModel<SaveModel>().Phase != SaveListState.Ready ||
            this.GetModel<SaveModel>().Slots[number - 1].State != SaveSlotState.Occupied)
        {
            return;
        }

        _deleteSlot = number;
        _deleteMessage.Text = $"确定删除第 {number} 个存档吗？\n删除后无法恢复。";
        _deleteConfirmation.Show();
        _deleteCancel.GrabFocus();
    }

    /// <summary>
    /// 关闭确认层并清除待确认的格号。
    /// </summary>
    private void CancelDelete()
    {
        _deleteSlot = 0;
        if (_deleteConfirmation != null)
        {
            _deleteConfirmation.Hide();
        }
    }

    /// <summary>
    /// 确认后才提交当前窗口仍有效的删除请求。
    /// </summary>
    private void ConfirmDelete()
    {
        if (_deleteSlot <= 0 || !GF.UI.HasUIForm(SerialId))
        {
            CancelDelete();
            return;
        }

        int slot = _deleteSlot;
        CancelDelete();
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        SaveSlotDeleteRequested?.Invoke(SerialId, slot);
    }

    /// <summary>
    /// 关闭存档页，返回开始界面。
    /// </summary>
    private void OnCloseClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
        {
            return;
        }

        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.UI.CloseUIForm(SerialId);
    }
}
