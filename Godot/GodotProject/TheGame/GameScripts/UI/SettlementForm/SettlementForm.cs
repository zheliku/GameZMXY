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
	public partial class SettlementForm : Control, IUIForm
	{
		#region 框架属性
		/// <summary>
		/// 界面序列编号。
		/// </summary>
		private int _serialId;

		/// <summary>
		/// 界面资源名称。
		/// </summary>
		private string _uiFormAssetName;

		/// <summary>
		/// 界面所属的界面组。
		/// </summary>
		private IUIGroup _uiGroup;

		/// <summary>
		/// 界面在界面组中的深度。
		/// </summary>
		private int _depthInUIGroup;

		/// <summary>
		/// 是否暂停被覆盖的界面。
		/// </summary>
		private bool _pauseCoveredUIForm;

		/// <summary>
		/// 获取界面序列编号。
		/// </summary>
		public int SerialId => _serialId;

		/// <summary>
		/// 获取界面资源名称。
		/// </summary>
		public string UIFormAssetName => _uiFormAssetName;

		/// <summary>
		/// 获取界面实例。
		/// </summary>
		public object Handle => this;

		/// <summary>
		/// 获取界面所属的界面组。
		/// </summary>
		public IUIGroup UIGroup => _uiGroup;

		/// <summary>
		/// 获取界面深度。
		/// </summary>
		public int DepthInUIGroup => _depthInUIGroup;

		/// <summary>
		/// 获取是否暂停被覆盖的界面。
		/// </summary>
		public bool PauseCoveredUIForm => _pauseCoveredUIForm;

		private List<IStringKey> _uiStringKeys;
		public List<IStringKey> UIStringKeys
		{
			get
			{
				if (_uiStringKeys == null)
				{
					_uiStringKeys = this.FindChildrenOfType<IStringKey>() ?? new List<IStringKey>();
				}
				return _uiStringKeys;
			}
		}
		#endregion

		[Export]
		private TextureRect _outcomeArt;
		[Export]
		private TextureButton _returnMap;
		[Export]
		private TextureButton _retry;
		[Export]
		private Label _time;
		[Export]
		private Label _health;
		[Export]
		private Label _kills;
		[Export]
		private Label _experience;

	}
}
