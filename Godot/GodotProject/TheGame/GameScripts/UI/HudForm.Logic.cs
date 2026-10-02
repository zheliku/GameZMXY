using GameFramework.Event;
using GameFramework.UI;
using Godot;
using GodotGameFramework.UI;
using GodotGameFramework;
using System;
using GameLogic.Event;

namespace GameLogic
{
	/// <summary>
	/// 界面逻辑（此文件仅在首次生成时创建，之后不会被覆盖）。
	/// 战斗 HUD（M6 裁剪版）：血条 + 等级 + 无双条 + 波次指示 + 清场开闸 gogo 箭头。
	/// 纯事件驱动（订阅英雄状态与波次事件，不持实体引用，根规范 §9）；布局复刻旧 Role_information
	/// 的左上血条组与右中前进箭头（素材见 LegacyAssetMap M6 节）。
	/// </summary>
	public partial class HudForm
	{
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
		}

		/// <summary>
		/// 界面打开：订阅状态事件（LoadingForm 同款"开订关退"模式；池化复用不重复订阅）。
		/// 初始数值由英雄进场时广播的 HeroVitalsChanged 到达（流程保证 HUD 先开、英雄后生成）。
		/// </summary>
		public void OnOpen(object userData)
		{
			#region 框架逻辑
			Visible = true;
			#endregion

			ResetDisplays();
			GF.Event.Subscribe(HeroVitalsChangedEventArgs.EventId, OnHeroVitals);
			GF.Event.Subscribe(WaveStartedEventArgs.EventId, OnWaveStarted);
			GF.Event.Subscribe(WaveClearedEventArgs.EventId, OnWaveCleared);
		}

		/// <summary>
		/// 界面关闭：退订（事件参数即发即收，不持有）。
		/// </summary>
		public void OnClose(bool isShutdown, object userData)
		{
			#region 框架逻辑
			Visible = false;
			#endregion

			GF.Event.Unsubscribe(HeroVitalsChangedEventArgs.EventId, OnHeroVitals);
			GF.Event.Unsubscribe(WaveStartedEventArgs.EventId, OnWaveStarted);
			GF.Event.Unsubscribe(WaveClearedEventArgs.EventId, OnWaveCleared);
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

		/// <summary>复位显示（等待第一个事件到达；gogo 箭头默认隐藏，波次文本待 WaveStarted）。</summary>
		private void ResetDisplays()
		{
			m_HpUnder.MaxValue = 1;
			m_HpUnder.Value = 1;
			m_HpBar.MaxValue = 1;
			m_HpBar.Value = 0;
			m_HpText.Text = "--/--";
			m_LevelLabel.Text = "Lv.-";
			m_WsBar.MaxValue = 1;
			m_WsBar.Value = 0;
			m_WaveLabel.Text = "";
			m_Gogo.Visible = false;
		}

		/// <summary>英雄状态 → 血条/等级/无双条（参数即到即用，不持有）。</summary>
		private void OnHeroVitals(object sender, GameEventArgs args)
		{
			if (args is not HeroVitalsChangedEventArgs e)
			{
				return;
			}

			m_HpUnder.MaxValue = e.MaxHp;
			m_HpUnder.Value = e.MaxHp;
			m_HpBar.MaxValue = e.MaxHp;
			m_HpBar.Value = e.Hp;
			m_HpText.Text = $"{e.Hp}/{e.MaxHp}";
			m_LevelLabel.Text = $"Lv.{e.Level}";
			m_WsBar.MaxValue = e.WsMax;
			m_WsBar.Value = e.WsValue;
		}

		/// <summary>波次开始 → 波次文本、藏箭头（开闸后玩家前进即触发下一波）。</summary>
		private void OnWaveStarted(object sender, GameEventArgs args)
		{
			if (args is not WaveStartedEventArgs e)
			{
				return;
			}

			m_WaveLabel.Text = $"第 {e.WaveIndex}/{e.WaveTotal} 波";
			m_Gogo.Visible = false;
		}

		/// <summary>波次清空 → 显示前进箭头（旧 Role_information.gogo 同语义：清场指路）。</summary>
		private void OnWaveCleared(object sender, GameEventArgs args)
		{
			m_Gogo.Visible = true;
		}
	}
}
