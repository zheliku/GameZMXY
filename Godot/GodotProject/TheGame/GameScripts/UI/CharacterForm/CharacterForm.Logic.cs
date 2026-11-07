using System;
using GameConfig.Sound;
using GameFramework.UI;
using GameLogic.Architecture;
using GameLogic.Audio;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Localization;
using GodotGameFramework.Sound;
using GodotGameFramework.UI;

namespace GameLogic.UI;

/// <summary>
/// 四栏选角：已开放角色响应悬停和确认；未开放角色只作静态展示。
/// </summary>
public partial class CharacterForm : IController
{
    private const int WukongHeroId = 1; // HeroConfig中已实现的悟空身份。
    private const int TangsengHeroId = 2; // HeroConfig中唐僧身份，首次快照随选角确定。

    /// <summary>
    /// 由所属流程处理新档确认；参数为本次窗口编号及英雄配置编号。
    /// </summary>
    public event Action<int, int> CharacterConfirmed;

    /// <summary>
    /// 设置框架字段；按钮事件只在新建实例时绑定一次。
    /// </summary>
    /// <param name="serialId">窗口打开编号。</param>
    /// <param name="uiFormAssetName">窗口资源路径。</param>
    /// <param name="uiGroup">所属界面组。</param>
    /// <param name="pauseCoveredUIForm">是否暂停被覆盖的窗口。</param>
    /// <param name="isNewInstance">是否首次创建节点。</param>
    /// <param name="userData">本次打开参数。</param>
    public void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm,
        bool isNewInstance, object userData)
    {
        _serialId = serialId;
        _uiFormAssetName = uiFormAssetName;
        _uiGroup = uiGroup;
        _depthInUIGroup = 0;
        _pauseCoveredUIForm = pauseCoveredUIForm;
        UIStringKeys.ForEach(key => key.SetLocalizationValue());
        if (isNewInstance)
        {
            _wukong.Pressed += OnWukongClicked;
            _wukong.MouseEntered += OnWukongMouseEntered;
            _wukong.MouseExited += ResetRoleHover;
            _sanzang.Pressed += OnTangsengClicked;
            _sanzang.MouseEntered += OnTangsengMouseEntered;
            _sanzang.MouseExited += ResetRoleHover;
        }
    }

    /// <summary>
    /// 开放当前窗口的角色确认请求。
    /// </summary>
    /// <param name="userData">本次窗口打开参数。</param>
    public void OnOpen(object userData)
    {
        ResetRoleHover();
        Visible = true;
    }

    /// <summary>
    /// 释放本次状态源引用，保留可复用的窗口节点。
    /// </summary>
    /// <param name="isShutdown">是否框架关停。</param>
    /// <param name="userData">关闭参数，不保存。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        CharacterConfirmed = null;
        if (!isShutdown)
        {
            ResetRoleHover();
        }

        if (!isShutdown && IsInstanceValid(this))
        {
            Visible = false;
        }
    }

    /// <summary>
    /// 回池时清除本次打开的引用和框架编号。
    /// </summary>
    public void OnRecycle()
    {
        ResetRoleHover();
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
    /// 确认悟空作为新档角色。
    /// </summary>
    private void OnWukongClicked() => ConfirmCharacter(WukongHeroId);

    /// <summary>
    /// 确认唐僧作为新档角色。
    /// </summary>
    private void OnTangsengClicked() => ConfirmCharacter(TangsengHeroId);

    /// <summary>
    /// 将当前窗口选择交给流程，窗口不创建或修改档案模型。
    /// </summary>
    /// <param name="heroId">已开放角色的英雄配置编号。</param>
    private void ConfirmCharacter(int heroId)
    {
        if (!GF.UI.HasUIForm(SerialId))
        {
            return;
        }

        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        CharacterConfirmed?.Invoke(SerialId, heroId);
    }

    /// <summary>
    /// 预览已开放的悟空，将 1P 放到卡片顶部中央。
    /// </summary>
    private void OnWukongMouseEntered() => PreviewRole(_wukong);

    /// <summary>
    /// 预览唐僧官方卡片，将1P放到卡片顶部中央。
    /// </summary>
    private void OnTangsengMouseEntered() => PreviewRole(_sanzang);

    /// <summary>
    /// 同一时刻只突出当前可选角色，复用官方卡片的灰度材质。
    /// </summary>
    /// <param name="card">鼠标进入的已开放角色卡片。</param>
    private void PreviewRole(TextureButton card)
    {
        if (!GF.UI.HasUIForm(SerialId))
        {
            return;
        }

        ResetRoleHover();
        ((ShaderMaterial)card.Material).SetShaderParameter("grayscale", 0f);
        _playerOne.GlobalPosition = new Vector2(card.GetGlobalRect().GetCenter().X - _playerOne.Size.X / 2,
            _playerOne.GlobalPosition.Y);
        _playerOne.Visible = true;
    }

    /// <summary>
    /// 离开卡片或关闭窗口时恢复灰度并隐藏玩家标记。
    /// </summary>
    private void ResetRoleHover()
    {
        ((ShaderMaterial)_wukong.Material).SetShaderParameter("grayscale", 1f);
        ((ShaderMaterial)_sanzang.Material).SetShaderParameter("grayscale", 1f);
        _playerOne.Visible = false;
    }
}
