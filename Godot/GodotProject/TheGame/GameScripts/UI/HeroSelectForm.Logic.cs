using GameFramework.UI;
using Godot;
using GodotGameFramework.UI;
using GodotGameFramework;
using System;
using GameLogic.Event;
using GodotGameFramework.Localization;

namespace GameLogic
{
	/// <summary>
	/// 界面逻辑（此文件仅在首次生成时创建，之后不会被覆盖）。
	/// 选人界面（M6 兼作开始界面）：英雄信息全部表驱动（HeroConfig），点"开始战斗"广播
	/// <see cref="StartBattleRequestedEventArgs"/>——UI 不驱动流程，由 ProcedureGame 订阅后切对局流程。
	/// </summary>
	public partial class HeroSelectForm
	{
		/// <summary>当前展示的英雄行（M6 垂直切片只有悟空一行；多英雄时在此扩展切换）</summary>
		private GameConfig.Hero.HeroConfig m_HeroConfig;

		/// <summary>
		/// 初始化界面。
		/// </summary>
		public void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm, bool isNewInstance, object userData)
		{
			#region 框架逻辑
			m_SerialId = serialId;
			m_UIFormAssetName = uiFormAssetName;
			m_UIGroup = uiGroup;
			m_DepthInUIGroup = 0;
			m_PauseCoveredUIForm = pauseCoveredUIForm;
			UIStringKeys.ForEach(key => key.SetLocalizationValue());
			#endregion
			if (isNewInstance)
			{
				#region 界面逻辑
				m_Btn_Start.Pressed += OnStartPressed;
				#endregion
			}
		}

		/// <summary>
		/// 界面回收。
		/// </summary>
		public void OnRecycle()
		{
			#region 框架逻辑
			m_SerialId = 0;
			m_DepthInUIGroup = 0;
			m_PauseCoveredUIForm = true;
			Visible = false;
			#endregion

			m_HeroConfig = null;
		}

		/// <summary>
		/// 界面打开：填充英雄信息（表驱动）。
		/// </summary>
		public void OnOpen(object userData)
		{
			#region 框架逻辑
			Visible = true;
			#endregion

			m_HeroConfig = null;
			foreach (GameConfig.Hero.HeroConfig hero in ConfigSystem.Instance.Tables.TbHeroConfig.DataList)
			{
				m_HeroConfig = hero;
				break;
			}

			if (m_HeroConfig == null)
			{
				Log.Error("[HeroSelectForm] HeroConfig 没有任何行，选人界面无法填充");
				return;
			}

			m_HeroName.Text = m_HeroConfig.NameCn;
			m_HeroDesc.Text = m_HeroConfig.Desc;
		}

		/// <summary>
		/// 界面关闭。
		/// </summary>
		public void OnClose(bool isShutdown, object userData)
		{
			#region 框架逻辑
			Visible = false;
			#endregion
		}

		/// <summary>
		/// 界面暂停。
		/// </summary>
		public void OnPause()
		{

		}

		/// <summary>
		/// 界面暂停恢复。
		/// </summary>
		public void OnResume()
		{

		}

		/// <summary>
		/// 界面遮挡。
		/// </summary>
		public void OnCover()
		{

		}

		/// <summary>
		/// 界面遮挡恢复。
		/// </summary>
		public void OnReveal()
		{

		}

		/// <summary>
		/// 界面重新获得焦点。
		/// </summary>
		public void OnRefocus(object userData)
		{

		}

		/// <summary>
		/// 界面轮询。
		/// </summary>
		public void OnUpdate(float elapseSeconds, float realElapseSeconds)
		{

		}

		/// <summary>
		/// 界面深度改变。
		/// </summary>
		public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
		{
			#region 框架逻辑
			m_DepthInUIGroup = depthInUIGroup;
			#endregion
		}

		/// <summary>"开始战斗"：广播对局请求（跨模块只走事件，流程负责 ChangeState）。</summary>
		private void OnStartPressed()
		{
			if (m_HeroConfig == null)
			{
				return;
			}

			GF.Event.Fire(this, StartBattleRequestedEventArgs.Create(m_HeroConfig.Id));
		}
	}
}
