using GameFramework.UI;
using Godot;
using GodotGameFramework;
using System.Collections.Generic;
using GodotGameFramework.Localization;
namespace GameLogic.UI
{
    /// <summary>
    /// 界面,生成时会被覆盖，请勿手动修改
    /// </summary>
    public partial class SaveNoticeForm : Control, IUIForm
    {
        #region 框架属性
        /// <summary>
        /// 界面序列编号。
        /// </summary>
        private int m_SerialId;

        /// <summary>
        /// 界面资源名称。
        /// </summary>
        private string m_UIFormAssetName;

        /// <summary>
        /// 界面所属的界面组。
        /// </summary>
        private IUIGroup m_UIGroup;

        /// <summary>
        /// 界面在界面组中的深度。
        /// </summary>
        private int m_DepthInUIGroup;

        /// <summary>
        /// 是否暂停被覆盖的界面。
        /// </summary>
        private bool m_PauseCoveredUIForm;

        /// <summary>
        /// 获取界面序列编号。
        /// </summary>
        public int SerialId
        {
            get
            {
                return m_SerialId;
            }
        }

        /// <summary>
        /// 获取界面资源名称。
        /// </summary>
        public string UIFormAssetName
        {
            get
            {
                return m_UIFormAssetName;
            }
        }

        /// <summary>
        /// 获取界面实例。
        /// </summary>
        public object Handle
        {
            get
            {
                return this;
            }
        }

        /// <summary>
        /// 获取界面所属的界面组。
        /// </summary>
        public IUIGroup UIGroup
        {
            get
            {
                return m_UIGroup;
            }
        }

        /// <summary>
        /// 获取界面深度。
        /// </summary>
        public int DepthInUIGroup
        {
            get
            {
                return m_DepthInUIGroup;
            }
        }

        /// <summary>
        /// 获取是否暂停被覆盖的界面。
        /// </summary>
        public bool PauseCoveredUIForm
        {
            get
            {
                return m_PauseCoveredUIForm;
            }
        }

        private List<IStringKey> m_UIStringKeys;
        public List<IStringKey> UIStringKeys
        {
            get
            {
                if (m_UIStringKeys == null)
                {
                    m_UIStringKeys = this.FindChildrenOfType<IStringKey>() ?? new List<IStringKey>();
                }
                return m_UIStringKeys;
            }
        }
        #endregion

        [Export]
        private Label m_SaveFeedback;
        [Export]
        private AnimationPlayer m_SaveFeedbackAnimation;

    }
}
