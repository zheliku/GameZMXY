using GameLogic.Architecture;
using GameLogic.Save.Slots;
using System;
using System.Linq;
using GameFramework.UI;
using GameLogic.Audio;
using GameLogic.Save;
using GameConfig.Sound;
using Godot;
using GodotGameFramework;

namespace GameLogic.UI
{
    /// <summary>
    /// 六格存档窗口，订阅目录变化并以具名事件请求读档。
    /// </summary>
    public partial class SaveForm : IController
    {
        private bool m_IsBound; // 本周期拥有目录订阅，关闭或失败后不得在回池时重取模型。

        private (TextureButton Button, Label Name, Label Date)[] m_SlotViews; // 六个存档格的控件引用，不另建格子脚本。
        private TextureButton[] m_DeleteButtons; // 六个格内的删除入口，只有已有可读档案启用。
        private Control m_DeleteConfirmation; // 存档页自己的确认层，保持前端视觉一致。
        private Label m_DeleteMessage; // 当前确认格号和不可恢复提示。
        private TextureButton m_DeleteCancel; // 取消确认，不触碰存档。
        private TextureButton m_DeleteConfirm; // 确认后才向流程提交删除请求。
        private int m_DeleteSlot; // 当前等待确认的格号。

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
            m_SerialId = serialId;
            m_UIFormAssetName = uiFormAssetName;
            m_UIGroup = uiGroup;
            m_DepthInUIGroup = 0;
            m_PauseCoveredUIForm = pauseCoveredUIForm;
            UIStringKeys.ForEach(key => key.SetLocalizationValue());
            if (!isNewInstance)
            {
                return;
            }

            m_Close.Pressed += OnCloseClicked;
            m_SlotViews =
            [
                (m_Slot1, m_Name1, m_Date1), (m_Slot2, m_Name2, m_Date2),
            (m_Slot3, m_Name3, m_Date3), (m_Slot4, m_Name4, m_Date4),
            (m_Slot5, m_Name5, m_Date5), (m_Slot6, m_Name6, m_Date6)
            ];
            if (m_SlotViews.Length != SaveModel.SlotCount ||
                m_SlotViews.Any(slot => slot.Button == null || slot.Name == null || slot.Date == null))
            {
                throw new InvalidOperationException("存档页必须为六格。");
            }
            m_DeleteButtons = m_SlotViews.Select(slot => slot.Button.GetNode<TextureButton>("Delete")).ToArray();
            m_DeleteConfirmation = GetNode<Control>("DeleteConfirmation");
            m_DeleteMessage = m_DeleteConfirmation.GetNode<Label>("Panel/Message");
            m_DeleteCancel = m_DeleteConfirmation.GetNode<TextureButton>("Panel/Cancel");
            m_DeleteConfirm = m_DeleteConfirmation.GetNode<TextureButton>("Panel/Confirm");
            // 只在新建实例时收集本表单的固定格子，复用期间沿用引用与订阅。
            for (int i = 0; i < m_SlotViews.Length; i++)
            {
                int number = i + 1;
                m_SlotViews[i].Button.Pressed += () => SelectSlot(number);
                m_DeleteButtons[i].Pressed += () => RequestDelete(number);
                m_DeleteButtons[i].MouseEntered += () => SetDeleteHover(number, true);
                m_DeleteButtons[i].MouseExited += () => SetDeleteHover(number, false);
            }
            m_DeleteCancel.Pressed += CancelDelete;
            m_DeleteConfirm.Pressed += ConfirmDelete;
        }

        /// <summary>
        /// 订阅目录并立即绘制当前状态。
        /// </summary>
        /// <param name="userData">未使用；目录由共享 SaveModel 提供。</param>
        public void OnOpen(object userData)
        {
            Unbind();
            m_IsBound = true;
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
            m_SerialId = 0;
            m_DepthInUIGroup = 0;
            m_PauseCoveredUIForm = true;
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
        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GF.Fsm.GetArchitecture();
        }

        /// <summary>
        /// 结束当前目录订阅，允许关闭后回池再次清理而不访问旧作用域。
        /// </summary>
        private void Unbind()
        {
            if (!m_IsBound)
            {
                return;
            }

            m_IsBound = false;
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
            m_DepthInUIGroup = depthInUIGroup;
            ZIndex = depthInUIGroup;
        }

        /// <summary>
        /// 完整刷新六个格及加载反馈。
        /// </summary>
        /// <param name="directory">已原子提交的目录快照。</param>
        private void RefreshDirectory(SaveDirectoryState directory)
        {
            bool ready = directory.Phase == SaveListState.Ready;
            m_Status.Text = directory.Phase switch
            {
                SaveListState.Ready => "",
                SaveListState.Failed => directory.ErrorMessage,
                _ => "正在读取存档…"
            };
            for (int i = 0; i < m_SlotViews.Length; i++)
            {
                var (button, name, date) = m_SlotViews[i];
                var slot = ready ? directory.Slots[i] : null;
                button.Disabled = slot == null || slot.State == SaveSlotState.Unreadable;
                button.MouseDefaultCursorShape = button.Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
                bool canDelete = ready && slot?.State == SaveSlotState.Occupied;
                m_DeleteButtons[i].Visible = canDelete;
                m_DeleteButtons[i].Disabled = !canDelete;
                m_DeleteButtons[i].Modulate = canDelete ? new Color(1f, 1f, 1f, 0.42f) : Colors.Transparent;
                m_DeleteButtons[i].MouseDefaultCursorShape = !canDelete
                    ? CursorShape.Arrow : CursorShape.PointingHand;
                if (slot?.State == SaveSlotState.Occupied)
                {
                    name.Text = $"{slot.HeroName} · Lv.{slot.Level}";
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
        private void SetDeleteHover(int number, bool hovered)
        {
            m_DeleteButtons[number - 1].Modulate = hovered ? Colors.White : new Color(1f, 1f, 1f, 0.42f);
        }

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

            m_DeleteSlot = number;
            m_DeleteMessage.Text = $"确定删除第 {number} 个存档吗？\n删除后无法恢复。";
            m_DeleteConfirmation.Show();
            m_DeleteCancel.GrabFocus();
        }

        /// <summary>
        /// 关闭确认层并清除待确认的格号。
        /// </summary>
        private void CancelDelete()
        {
            m_DeleteSlot = 0;
            if (m_DeleteConfirmation != null)
            {
                m_DeleteConfirmation.Hide();
            }
        }

        /// <summary>
        /// 确认后才提交当前窗口仍有效的删除请求。
        /// </summary>
        private void ConfirmDelete()
        {
            if (m_DeleteSlot <= 0 || !GF.UI.HasUIForm(SerialId))
            {
                CancelDelete();
                return;
            }

            int slot = m_DeleteSlot;
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
}
