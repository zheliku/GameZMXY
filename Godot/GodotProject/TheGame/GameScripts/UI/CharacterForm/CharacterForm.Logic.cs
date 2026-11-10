using System;
using System.Collections.Generic;
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

    private int _playerCount; // 本次需要确认的人数。
    private readonly List<int> _chosenHeroIds = new(); // 已按席位确认的英雄。

    /// <summary>全部席位选定；参数为窗口编号与按席位排列的英雄编号。</summary>
    public event Action<int, int[]> CharacterConfirmed;

    /// <summary>玩家放弃选角返回标题；参数为窗口编号。</summary>
    public event Action<int> CharacterCancelled;

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
            _reselect.Pressed += OnReselectClicked;
            _back.Pressed += OnBackClicked;
        }
    }

    /// <summary>
    /// 根据指定人数开放当前窗口的依次选角请求。
    /// </summary>
    /// <param name="userData">包含参战人数的选角参数。</param>
    /// <exception cref="ArgumentException">未提供选角参数。</exception>
    public void OnOpen(object userData)
    {
        if (userData is not CharacterFormData data)
        {
            throw new ArgumentException("选角窗口需要参战人数。", nameof(userData));
        }

        // 每次打开从第一席位开始，不沿用池中窗口的上次选择。
        _playerCount = data.PlayerCount;
        _chosenHeroIds.Clear();
        _reselect.Visible = _playerCount == 2;
        RefreshCards();
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
        CharacterCancelled = null;
        _chosenHeroIds.Clear();
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
        CharacterConfirmed = null;
        CharacterCancelled = null;
        _chosenHeroIds.Clear();
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
        if (!GF.UI.HasUIForm(SerialId) || _chosenHeroIds.Contains(heroId))
        {
            return;
        }

        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        _chosenHeroIds.Add(heroId);
        RefreshCards();
        if (_chosenHeroIds.Count == _playerCount)
        {
            CharacterConfirmed?.Invoke(SerialId, _chosenHeroIds.ToArray());
        }
    }

    /// <summary>
    /// 预览已开放的悟空，将待选席位放到卡片顶部中央。
    /// </summary>
    private void OnWukongMouseEntered() => PreviewRole(_wukong, WukongHeroId);

    /// <summary>
    /// 预览唐僧官方卡片，将待选席位放到卡片顶部中央。
    /// </summary>
    private void OnTangsengMouseEntered() => PreviewRole(_sanzang, TangsengHeroId);

    /// <summary>预览未选卡片，并显示下一个待选席位。</summary>
    /// <param name="card">鼠标进入的已开放角色卡片。</param>
    /// <param name="heroId">卡片对应英雄编号。</param>
    private void PreviewRole(TextureButton card, int heroId)
    {
        if (!GF.UI.HasUIForm(SerialId) || _chosenHeroIds.Contains(heroId) || _chosenHeroIds.Count == _playerCount)
        {
            return;
        }

        RefreshCards();
        ((ShaderMaterial)card.Material).SetShaderParameter("grayscale", 0f);
        Label label = _chosenHeroIds.Count == 0 ? _playerOne : _playerTwo;
        label.GlobalPosition = new Vector2(card.GetGlobalRect().GetCenter().X - label.Size.X / 2,
            label.GlobalPosition.Y);
        label.Visible = true;
    }

    /// <summary>按已确认的席位恢复卡片亮度和玩家标记。</summary>
    private void RefreshCards()
    {
        // 已确认的卡片持续高亮；标记位置与阵容顺序一致。
        ((ShaderMaterial)_wukong.Material).SetShaderParameter("grayscale", _chosenHeroIds.Contains(WukongHeroId) ? 0f : 1f);
        ((ShaderMaterial)_sanzang.Material).SetShaderParameter("grayscale", _chosenHeroIds.Contains(TangsengHeroId) ? 0f : 1f);
        _playerOne.Visible = false;
        _playerTwo.Visible = false;
        for (int i = 0; i < _chosenHeroIds.Count; i++)
        {
            TextureButton card = _chosenHeroIds[i] == WukongHeroId ? _wukong : _sanzang;
            Label label = i == 0 ? _playerOne : _playerTwo;
            label.GlobalPosition = new Vector2(card.GetGlobalRect().GetCenter().X - label.Size.X / 2,
                label.GlobalPosition.Y);
            label.Visible = true;
        }
    }

    /// <summary>鼠标离开时恢复已确认席位，不清除选择。</summary>
    private void ResetRoleHover() => RefreshCards();

    /// <summary>清空当前双人选择，重新从第一席位开始。</summary>
    private void OnReselectClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
        {
            return;
        }

        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        _chosenHeroIds.Clear();
        RefreshCards();
    }

    /// <summary>放弃本次选择并请求返回标题。</summary>
    private void OnBackClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
        {
            return;
        }

        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        CharacterCancelled?.Invoke(SerialId);
    }
}
