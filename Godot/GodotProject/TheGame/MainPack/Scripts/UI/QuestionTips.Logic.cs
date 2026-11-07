using System;
using GameFramework.UI;
using Godot;
using GodotGameFramework;
using GodotGameFramework.UI;

namespace GameLogic;

/// <summary>
/// 池化确认提示，关闭即释放回调；按钮订阅只建立一次。
/// </summary>
public partial class QuestionTips : ITips
{
    private Action _cancel; // 本周期取消回调。
    private Action _confirm; // 本周期确认回调。

    /// <summary>
    /// 设置框架字段；按钮事件只在新建实例时绑定一次。
    /// </summary>
    /// <param name="serialId">窗口打开编号。</param>
    /// <param name="uiFormAssetName">窗口资源路径。</param>
    /// <param name="uiGroup">所属界面组。</param>
    /// <param name="pauseCoveredUIForm">是否暂停被覆盖的窗口。</param>
    /// <param name="isNewInstance">是否首次创建节点。</param>
    /// <param name="userData">本次打开参数。</param>
    public void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm, bool isNewInstance, object userData)
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

        _btnCancle.Pressed += OnCancelClicked;
        _btnConfirm.Pressed += OnConfirmClicked;
    }

    /// <summary>
    /// 显示提示，回调由调用者随后通过 SetAction 绑定。
    /// </summary>
    /// <param name="userData">不使用的打开参数。</param>
    public void OnOpen(object userData) => Visible = true;

    /// <summary>
    /// 关闭即释放本周期回调。
    /// </summary>
    /// <param name="isShutdown">是否框架关停。</param>
    /// <param name="userData">不使用的关闭参数。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        _cancel = _confirm = null;
        if (!isShutdown && IsInstanceValid(this))
        {
            Visible = false;
        }
    }

    /// <summary>
    /// 回池清除编号与回调。
    /// </summary>
    public void OnRecycle()
    {
        _cancel = _confirm = null;
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
    /// 设置正文、按钮及本周期回调。
    /// </summary>
    /// <param name="content">提示正文。</param>
    /// <param name="cancelTxt">取消按钮文本。</param>
    /// <param name="confirmTxt">确认按钮文本。</param>
    /// <param name="cancel">取消回调。</param>
    /// <param name="confirm">确认回调。</param>
    public void SetAction(string content, string cancelTxt, string confirmTxt, Action cancel, Action confirm)
    {
        _label.Text = content;
        _btnCancle.Text = cancelTxt;
        _btnConfirm.Text = confirmTxt;
        _cancel = cancel;
        _confirm = confirm;
    }

    /// <summary>
    /// 提示参数通过 SetAction 显式设置。
    /// </summary>
    /// <param name="userData">不使用的重新聚焦参数。</param>
    public void OnRefocus(object userData)
    {
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
    /// 取消前捕获回调，关闭会同步释放窗口引用。
    /// </summary>
    private void OnCancelClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
        {
            return;
        }

        Action callback = _cancel;
        GF.UI.CloseUIForm(this);
        callback?.Invoke();
    }

    /// <summary>
    /// 确认前捕获回调，允许回调安全打开下一提示。
    /// </summary>
    private void OnConfirmClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
        {
            return;
        }

        Action callback = _confirm;
        GF.UI.CloseUIForm(this);
        callback?.Invoke();
    }
}
