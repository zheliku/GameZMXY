using GameFramework.Event;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Scene;
using GodotGameFramework.UI;
using System;
namespace GameLogic
{
	/// <summary>
	/// 界面逻辑（此文件仅在首次生成时创建，之后不会被覆盖）。
	/// </summary>
	public partial class LoadingForm
	{
		private Tween m_ProgressTween; // 驱动进度条平滑变化的补间。
		private Tween m_CloseTween; // 加载完成后延迟关闭界面的补间。

		private bool m_IsCloseRequested; // 场景和界面加载事件可能重复触发关闭请求，用于防止重入。
		/// <summary>
		/// 初始化界面。
		/// </summary>
		/// <param name="serialId">界面序列编号。</param>
		/// <param name="uiFormAssetName">界面资源名称。</param>
		/// <param name="uiGroup">界面所处的界面组。</param>
		/// <param name="pauseCoveredUIForm">是否暂停被覆盖的界面。</param>
		/// <param name="isNewInstance">是否是新实例。</param>
		/// <param name="userData">用户自定义数据。</param>
		public void OnInit(int serialId, string uiFormAssetName, GameFramework.UI.IUIGroup uiGroup, bool pauseCoveredUIForm, bool isNewInstance, object userData)
		{
			#region 框架逻辑
			m_SerialId = serialId;
			m_UIFormAssetName = uiFormAssetName;
			m_UIGroup = uiGroup;
			m_DepthInUIGroup = 0;
			m_PauseCoveredUIForm = pauseCoveredUIForm;
			m_IsCloseRequested = false;
			UIStringKeys.ForEach(key => key.SetLocalizationValue());
			#endregion
		}

		/// <summary>
		/// 界面回收。
		///
		/// </summary>
		public void OnRecycle()
		{
			#region 框架逻辑
			m_SerialId = 0;
			m_DepthInUIGroup = 0;
			m_PauseCoveredUIForm = true;
			Visible = false;
			#endregion

			m_IsCloseRequested = false;
			m_ProgressTween?.Kill();
			m_ProgressTween = null;
			m_CloseTween?.Kill();
			m_CloseTween = null;
		}

		/// <summary>
		/// 界面打开。
		/// </summary>
		public void OnOpen(object userData)
		{
			#region 框架逻辑
			Visible = true;
			#endregion
			if (m_HSlider != null)
			{
				m_HSlider.Value = 0;
			}
			GF.Event.Subscribe(OpenUIFormUpdateEventArgs.EventId, OnLoadingUpdate);
			GF.Event.Subscribe(LoadSceneUpdateEventArgs.EventId, OnLoadingUpdate);
		}

		/// <summary>
		/// 界面关闭。
		/// </summary>
		public void OnClose(bool isShutdown, object userData)
		{
			#region 框架逻辑
			Visible = false;
			#endregion

			GF.Event.Unsubscribe(OpenUIFormUpdateEventArgs.EventId, OnLoadingUpdate);
			GF.Event.Unsubscribe(LoadSceneUpdateEventArgs.EventId, OnLoadingUpdate);

			// 终止所有 Tween，避免旧实例的延迟关闭回调在实例复用后误关新实例
			m_ProgressTween?.Kill();
			m_ProgressTween = null;
			m_CloseTween?.Kill();
			m_CloseTween = null;
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



		/// <summary>更新加载状态文本和进度值。</summary>
		/// <param name="logState">要显示的状态文本。</param>
		/// <param name="progress">进度百分比，范围会限制为 0 到 100。</param>
		public void SetLogState(string logState, float progress)
		{
			if (m_HSlider == null)
			{
				return;
			}
			float clamped = Mathf.Clamp(progress, 0f, 100f);

			// 平滑过渡，避免进度条跳跃显得生硬
			m_ProgressTween?.Kill();
			m_ProgressTween = CreateTween();
			m_ProgressTween.TweenProperty(m_HSlider, "value", clamped, 0.25f)
				.SetTrans(Tween.TransitionType.Quad)
				.SetEase(Tween.EaseType.Out);
			if (m_State != null)
			{
				m_State.Text = (logState ?? string.Empty) + $" {clamped:F0}%";
			}
		}


		/// <summary>
		/// 显式关闭加载界面（由加载发起方在 finally 中调用）。
		/// 先展示"加载完成"再延迟约 0.1s 关闭，避免关闭瞬间闪烁。
		/// </summary>
		public void CloseLoading()
		{
			if (m_IsCloseRequested)
			{
				return;
			}
			m_IsCloseRequested = true;
			SetLogState("加载完成", 100);

			// 延迟关闭：tween 绑定在节点上，并校验 serialId/Current，
			// 避免旧实例的延迟回调误关复用后的新实例或已关闭的实例。
			int closeSerialId = m_SerialId;
			m_CloseTween?.Kill();
			m_CloseTween = CreateTween();
			m_CloseTween.SetIgnoreTimeScale(true); // 延迟不受游戏暂停/TimeScale 影响
			m_CloseTween.TweenInterval(0.1f);
			m_CloseTween.TweenCallback(Callable.From(() =>
			{
				if (m_SerialId != closeSerialId)
				{
					return;
				}
				GF.UI.CloseUIForm(this);
			}));
		}


		/// <summary>接收界面或场景加载事件并更新进度。</summary>
		/// <param name="sender">事件发送者。</param>
		/// <param name="e">加载进度事件参数。</param>
		private void OnLoadingUpdate(object sender, GameEventArgs e) // 将界面或场景加载进度统一更新到加载界面。
		{
			float progress = 0;
			if (e is OpenUIFormUpdateEventArgs ui)
			{
				progress = ui.Progress;
			}
			else if (e is LoadSceneUpdateEventArgs scene)
			{
				progress = scene.Progress;
			}
			SetLogState("加载中...", progress * 100);
		}
	}
}
