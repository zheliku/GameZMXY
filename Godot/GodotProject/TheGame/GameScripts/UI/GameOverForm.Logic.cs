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
	/// 结算界面（M6 简化版，复刻旧 victory.tscn 的框架）：通关/战败两套标题与统计
	/// （用时 / 剩余血量 / 击杀数），数据经 <see cref="GameOverPayload"/>（userData）注入；
	/// "再次挑战 / 返回选人"只广播事件（Retry / ReturnToMenu），由对局流程收尾并切换。
	/// </summary>
	public partial class GameOverForm
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
			if (isNewInstance)
			{
				#region 界面逻辑
				m_BtnRetry.Pressed += OnRetryPressed;
				m_BtnReturn.Pressed += OnReturnPressed;
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
		}

		/// <summary>
		/// 界面打开：按载荷切换标题与统计（通关显示用时/血量/击杀；战败显示击杀数）。
		/// </summary>
		public void OnOpen(object userData)
		{
			#region 框架逻辑
			Visible = true;
			#endregion

			if (userData is not GameOverPayload payload)
			{
				Log.Error("[GameOverForm] userData 不是 GameOverPayload，结算界面无法填充");
				payload = null;
			}

			bool victory = payload?.IsVictory ?? false;
			m_VictoryTitle.Visible = victory;
			m_FailTitle.Visible = !victory;

			if (victory)
			{
				m_StatsLabel.Text =
					$"过关用时：{FormatTime(payload.ElapsedSeconds)}\n" +
					$"剩余状态：{payload.HeroHp}/{payload.HeroMaxHp}\n" +
					$"击杀数：{payload.KillCount}";
			}
			else
			{
				m_StatsLabel.Text = $"胜败乃兵家常事\n击杀数：{payload?.KillCount ?? 0}";
			}
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

		/// <summary>"再次挑战"：广播重试请求（流程收尾后重进对局流程）。</summary>
		private void OnRetryPressed()
		{
			GF.Event.Fire(this, RetryBattleRequestedEventArgs.Create());
		}

		/// <summary>"返回选人"：广播返回请求（流程切回菜单流程）。</summary>
		private void OnReturnPressed()
		{
			GF.Event.Fire(this, ReturnToMenuRequestedEventArgs.Create());
		}

		/// <summary>秒 → "X 分 Y 秒"（结算展示；表现层格式化，非玩法数值）。</summary>
		private static string FormatTime(float seconds)
		{
			int total = Mathf.Max(0, (int)seconds);
			return $"{total / 60} 分 {total % 60} 秒";
		}
	}
}
