using System;
using System.Linq;
using GameConfig.Sound;
using GameFramework.UI;
using GameLogic.Architecture;
using GameLogic.Audio;
using GameLogic.Battle;
using GameLogic.Enemies;
using GameLogic.Heroes;
using GameLogic.Level;
using GameLogic.UI.Widgets;
using Godot;
using GodotGameFramework;

namespace GameLogic.UI;

/// <summary>
/// 战斗 HUD：统一驱动个人面板绑定，观察共享关卡状态并处理菜单与全屏演出；由 GGF 管理打开、关闭和复用。
/// 不写任何状态，不执行玩法结算。
/// </summary>
public partial class BattleForm : IController
{
    private const double ComboDisplaySeconds = 3.5d; // 官方连击数字每次命中后渐隐的显示时长。
    private BattleFormData _formData; // 本次打开的状态源引用，关闭后释放。
    private AbstractEnemyEntity _opponent; // 本次观察的守关怪，实体所有权仍归关卡。
    private LevelModel _levelModel; // 本次窗口订阅的共享关卡状态。
    private double _comboElapsed; // 当前连击数字已显示的游戏秒数。

    /// <summary>
    /// 由所属流程处理 SettingsRequested；参数为本次窗口编号。
    /// </summary>
    public event Action<int> SettingsRequested;

    /// <summary>
    /// 由所属流程处理 InventoryRequested；参数为本次窗口编号与请求席位。
    /// </summary>
    public event Action<int, PlayerSlot> InventoryRequested;

    /// <summary>
    /// 由所属流程处理 SkillsRequested；参数为本次窗口编号与请求席位。
    /// </summary>
    public event Action<int, PlayerSlot> SkillsRequested;

    /// <summary>
    /// 战斗界面通过输入映射请求背包或技能，暂停或覆盖时不响应。
    /// </summary>
    /// <param name="inputEvent">输入映射处理的事件。</param>
    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_formData == null || !Visible || GF.Base.IsGamePaused)
        {
            return;
        }

        // 按阵容实际存在的席位处理快捷键，单人档不能用 P2 键请求不存在的英雄。
        foreach (AbstractHeroEntity hero in _formData.Heroes)
        {
            PlayerSlot slot = hero.Slot;
            bool inventory = inputEvent.IsActionPressed(Input.InputSystem.ActionOf(Input.InputSystem.InventoryToggle, slot));
            bool skills = inputEvent.IsActionPressed(Input.InputSystem.ActionOf(Input.InputSystem.SkillToggle, slot));
            if (!inventory && !skills)
            {
                continue;
            }
            if (!this.GetModel<HeroModel>().GetState(hero.HeroId).ControlsEnabled)
            {
                return;
            }

            GetViewport().SetInputAsHandled();
            GF.Sound.PlayConfigured(SoundId.MenuConfirm);
            if (inventory)
            {
                InventoryRequested?.Invoke(SerialId, slot);
            }
            else
            {
                SkillsRequested?.Invoke(SerialId, slot);
            }
            return;
        }
    }

    /// <summary>
    /// 初始化框架字段；业务订阅只在 OnOpen 建立。
    /// </summary>
    /// <param name="serialId">界面请求编号。</param>
    /// <param name="uiFormAssetName">界面资源路径。</param>
    /// <param name="uiGroup">所属界面组。</param>
    /// <param name="pauseCoveredUIForm">是否暂停被覆盖界面。</param>
    /// <param name="isNewInstance">是否首次创建实例。</param>
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
            _menu.SettingsSelected += OnSettingsClicked;
            _menu.InventorySelected += OnInventoryClicked;
            _menu.SkillsSelected += OnSkillsClicked;
            _menu.MusouStateChanged += OnMusouStateChanged;
            _menuP2.SettingsSelected += OnSettingsClicked;
            _menuP2.InventorySelected += OnInventoryClicked;
            _menuP2.SkillsSelected += OnSkillsClicked;
            _menuP2.MusouStateChanged += OnMusouStateChanged;
        }
    }

    /// <summary>
    /// 回池时重复执行本地解绑，清空框架字段。
    /// </summary>
    public void OnRecycle()
    {
        Unbind();
        _serialId = 0;
        _depthInUIGroup = 0;
        _pauseCoveredUIForm = true;
        if (IsInstanceValid(this))
        {
            Visible = false;
        }
    }

    /// <summary>
    /// 验证装配，订阅各状态源的变更事件，并用当前值一次性初始化显示。
    /// </summary>
    /// <param name="userData">携带本次英雄与场景的 <see cref="BattleFormData"/>。</param>
    /// <exception cref="ArgumentException">打开参数类型错误。</exception>
    /// <exception cref="InvalidOperationException">必需节点未绑定或英雄未显示。</exception>
    public void OnOpen(object userData)
    {
        Modulate = Colors.White;
        Unbind();
        Visible = false;
        if (userData is not BattleFormData data)
        {
            throw new ArgumentException("BattleForm 必须接收 BattleFormData。", nameof(userData));
        }

        LevelModel levelModel = this.GetModel<LevelModel>();
        if (data.Heroes.Any(hero => !IsInstanceValid(hero) || !hero.IsShown) || levelModel == null || !levelModel.Initialized)
        {
            throw new InvalidOperationException("BattleForm 绑定的英雄或关卡已经失效。");
        }

        if (_status == null || _menu == null || _statusP2 == null || _menuP2 == null || _go == null || _encounterHealthBar == null ||
            _musouActivation == null || _combo == null)
        {
            throw new InvalidOperationException("BattleForm 必须绑定状态区、菜单区、Go、守关生命条、无双提示和连击数字。");
        }

        // 先登记本次引用再订阅，部分订阅失败时也能完整退订。
        _formData = data;
        _levelModel = levelModel;
        _statusP2.Visible = _menuP2.Visible = data.Heroes.Count == 2;
        try
        {
            // 每个现有面板只绑定一名英雄；窗口仍统一掌握绑定开始、结束与失败清理。
            foreach (AbstractHeroEntity hero in data.Heroes)
            {
                BattleStatusPanel status = hero.Slot == PlayerSlot.P1 ? _status : _statusP2;
                BattleMenuPanel menu = hero.Slot == PlayerSlot.P1 ? _menu : _menuP2;
                status.BindHero(hero.HeroId);
                menu.BindHero(hero.HeroId, hero.Slot);
            }
            levelModel.TravelAvailable.RegisterWithInitValue(RefreshTravel);
            levelModel.ComboCount.RegisterWithInitValue(RefreshCombo);
            data.ViewController.FeaturedOpponentChanged += RefreshEncounter;
            RefreshEncounter();
            Visible = true;
        }
        catch
        {
            Unbind();
            Visible = false;
            throw;
        }
    }

    /// <summary>
    /// 关闭时退订，即使窗口仍在场景树内也不再观察旧对象。
    /// </summary>
    /// <param name="isShutdown">是否框架关停；此时节点可能已经释放。</param>
    /// <param name="userData">关闭参数，不使用。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        SettingsRequested = null;
        InventoryRequested = null;
        SkillsRequested = null;
        Unbind();
        if (!isShutdown && IsInstanceValid(this))
        {
            Visible = false;
        }
    }

    /// <summary>
    /// 界面暂停，数据仍保持订阅。
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
    /// 界面被覆盖。
    /// </summary>
    public void OnCover()
    {
    }

    /// <summary>
    /// 恢复被覆盖界面。
    /// </summary>
    public void OnReveal()
    {
    }

    /// <summary>
    /// 界面轮询；数值显示由状态源变更事件驱动，计时类显示（冷却、Buff 剩余）以后在这里读取。
    /// </summary>
    /// <param name="elapseSeconds">游戏帧间隔秒数。</param>
    /// <param name="realElapseSeconds">实际帧间隔秒数。</param>
    public void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        if (_formData != null)
        {
            RefreshEncounterHealth();
            AdvanceComboDisplay(elapseSeconds);
        }
    }

    /// <summary>
    /// 读取所属游戏流程的架构入口。
    /// </summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    /// <summary>
    /// 重新绑定显式传入的打开参数。
    /// </summary>
    /// <param name="userData">新绑定参数；null 保持当前绑定。</param>
    public void OnRefocus(object userData)
    {
        if (userData != null)
        {
            OnOpen(userData);
        }
    }

    /// <summary>
    /// 记录框架分配的界面深度。
    /// </summary>
    /// <param name="uiGroupDepth">界面组深度。</param>
    /// <param name="depthInGroup">界面在组内的深度。</param>
    public void OnDepthChanged(int uiGroupDepth, int depthInGroup)
    {
        _depthInUIGroup = depthInGroup;
        ZIndex = depthInGroup;
    }

    /// <summary>
    /// 请关卡流程打开对应席位发起的共享设置菜单。
    /// </summary>
    /// <param name="slot">发起点击的菜单面板所属席位。</param>
    private void OnSettingsClicked(PlayerSlot slot)
    {
        AbstractHeroEntity hero = _formData?.Heroes.FirstOrDefault(participant => participant.Slot == slot);
        if (hero == null || !this.GetModel<HeroModel>().GetState(hero.HeroId).ControlsEnabled)
        {
            return;
        }

        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        SettingsRequested?.Invoke(SerialId);
    }

    /// <summary>
    /// 请关卡流程打开战斗背包。
    /// </summary>
    /// <param name="slot">发起点击的菜单面板所属席位。</param>
    private void OnInventoryClicked(PlayerSlot slot)
    {
        AbstractHeroEntity hero = _formData?.Heroes.FirstOrDefault(participant => participant.Slot == slot);
        if (hero == null || !this.GetModel<HeroModel>().GetState(hero.HeroId).ControlsEnabled)
        {
            return;
        }

        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        InventoryRequested?.Invoke(SerialId, slot);
    }

    /// <summary>
    /// 请关卡流程打开技能目录。
    /// </summary>
    /// <param name="slot">发起点击的菜单面板所属席位。</param>
    private void OnSkillsClicked(PlayerSlot slot)
    {
        AbstractHeroEntity hero = _formData?.Heroes.FirstOrDefault(participant => participant.Slot == slot);
        if (hero == null || !this.GetModel<HeroModel>().GetState(hero.HeroId).ControlsEnabled)
        {
            return;
        }

        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        SkillsRequested?.Invoke(SerialId, slot);
    }

    /// <summary>
    /// 释放本次订阅与表现任务，重复调用安全。
    /// </summary>
    private void Unbind()
    {
        BattleFormData data = _formData;
        _formData = null;
        LevelModel levelModel = _levelModel;
        _levelModel = null;
        levelModel?.ComboCount.UnRegister(RefreshCombo);
        levelModel?.TravelAvailable.UnRegister(RefreshTravel);
        ResetComboDisplay();
        if (data != null)
        {
            data.ViewController.FeaturedOpponentChanged -= RefreshEncounter;
        }

        // GGF关窗不移出节点树，必须在模型移除前显式解除所有面板绑定。
        if (IsInstanceValid(_status))
        {
            _status.UnbindHero();
        }
        if (IsInstanceValid(_statusP2))
        {
            _statusP2.UnbindHero();
        }
        if (IsInstanceValid(_menu))
        {
            _menu.UnbindHero();
        }
        if (IsInstanceValid(_menuP2))
        {
            _menuP2.UnbindHero();
        }

        if (IsInstanceValid(_musouActivation))
        {
            _musouActivation.Reset();
        }

        BindEncounter(null);
        if (IsInstanceValid(_go))
        {
            _go.Stop();
            _go.Visible = false;
        }
    }

    /// <summary>
    /// 在 HUD 绑定关卡指定的守关对手；该怪物仍由实体系统管理。
    /// </summary>
    private void RefreshEncounter() => BindEncounter(_formData.ViewController.FeaturedOpponent);

    /// <summary>
    /// 只在对手改变时重绑生命事件；控件本身只接收完整数值。
    /// </summary>
    /// <param name="opponent">关卡指定的怪物；空值解除显示。</param>
    private void BindEncounter(AbstractEnemyEntity opponent)
    {
        _opponent = opponent;
        RefreshEncounterHealth();
    }

    /// <summary>
    /// 从唯一生命状态源读取并更新左名右条；关闭或死亡后隐藏。
    /// </summary>
    /// <param name="value">已提交的属性值；同时读取关联上限。</param>
    private void RefreshEncounterHealth(int value = 0)
    {
        if (!IsInstanceValid(_encounterHealthBar))
        {
            return;
        }

        if (_opponent == null)
        {
            _encounterHealthBar.Visible = false;
        }
        else
        {
            _encounterHealthBar.SetValue(_opponent.Config.NameCn, _opponent.Hp, _opponent.MaxHp);
        }
    }

    /// <summary>处理个人菜单同步报告的无双变化，共享立绘与暂停仍由窗口持有。</summary>
    /// <param name="heroId">本次状态变化所属的英雄编号。</param>
    /// <param name="active">已经提交的开启状态。</param>
    private void OnMusouStateChanged(int heroId, bool active)
    {
        if (active)
        {
            _musouActivation.ShowActivation(heroId);
        }
        else
        {
            _musouActivation.Reset();
        }
    }

    /// <summary>
    /// 显示或隐藏前进提示，并同步控制动画。
    /// </summary>
    /// <param name="visible">本局已提交的前进窗口。</param>
    private void RefreshTravel(bool visible)
    {
        _go.Visible = visible;
        if (visible)
        {
            _go.Play();
        }
        else
        {
            _go.Stop();
        }
    }

    /// <summary>
    /// 第二次及后续命中时更新数字，并重新开始原版渐隐时长。
    /// </summary>
    /// <param name="count">关卡规则累计的有效命中数。</param>
    private void RefreshCombo(int count)
    {
        if (count < 2)
        {
            return;
        }

        _combo.SetCount(count);
        _comboElapsed = 0;
        _combo.Modulate = Colors.White;
        _combo.Visible = true;
    }

    /// <summary>
    /// 按游戏时间推进数字渐隐，暂停时保留当前显示。
    /// </summary>
    /// <param name="delta">本次游戏帧秒数。</param>
    private void AdvanceComboDisplay(float delta)
    {
        if (!_combo.Visible || GF.Base.IsGamePaused)
        {
            return;
        }

        _comboElapsed += delta;
        if (_comboElapsed >= ComboDisplaySeconds)
        {
            ResetComboDisplay();
            return;
        }

        float alpha = 1f - (float)(_comboElapsed / ComboDisplaySeconds);
        _combo.Modulate = new Color(1f, 1f, 1f, alpha);
    }

    /// <summary>
    /// 关闭或回池时清理旧数字和渐隐状态。
    /// </summary>
    private void ResetComboDisplay()
    {
        _comboElapsed = 0;
        if (IsInstanceValid(_combo))
        {
            _combo.Clear();
            _combo.Modulate = Colors.White;
            _combo.Visible = false;
        }
    }
}
