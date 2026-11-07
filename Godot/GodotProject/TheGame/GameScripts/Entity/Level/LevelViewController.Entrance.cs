using System;
using System.Linq;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Config;
using Godot;

namespace GameLogic.Level;

/// <summary>
/// 隐藏入口只报告物理接触并播放原生淡出，跨场景导航交给所属流程。
/// </summary>
public partial class LevelViewController
{
    [Export] private Area2D _hiddenEntrance; // 水面下的自动接触区，不占用普通阶段触发器。
    [Export] private AnimationPlayer _entranceFade; // 外置一秒淡出动画；始终使用游戏时钟。
    private int _hiddenLevelId; // 仅水帘洞从龙宫专属表取得的入口目标。
    private bool _enteringHiddenLevel; // 接触后只播放一次淡出，防止重复导航。
    private bool _entranceFadeFinished; // 动画回调只登记事实，下一流程帧消费。

    /// <summary>
    /// 原生动画编辑的入口淡出透明度；加载和清理不保留上次实例的显示状态。
    /// </summary>
    [Export(PropertyHint.Range, "0,1")]
    public float EntranceOpacity
    {
        get => Modulate.A;
        set
        {
            Modulate = new Color(1, 1, 1, value);
            if (IsInstanceValid(_player))
            {
                _player.Modulate = Modulate;
            }

            EntranceOpacityChanged?.Invoke(value);
        }
    }

    /// <summary>
    /// 原生淡出完成且本局仍有效时提交导航，流程在自己的更新阶段重入关卡。
    /// </summary>
    public event Action<int> HiddenLevelRequested;

    /// <summary>
    /// 原生淡出同步英雄与流程拥有的HUD，节点层级不跨场景硬编码。
    /// </summary>
    public event Action<float> EntranceOpacityChanged;

    /// <summary>
    /// 入口场景和源表必须一一对应，不以缺失绑定静默省略隐藏关。
    /// </summary>
    /// <exception cref="InvalidOperationException">配置与节点绑定不完整。</exception>
    private void InitializeEntrance()
    {
        bool hasEntrance = DragonPalaceRules.EntranceLevelId == _levelId;
        if (hasEntrance != (_hiddenEntrance != null) ||
            (_hiddenEntrance != null) != (_entranceFade != null))
        {
            throw new InvalidOperationException($"关卡 {_levelId} 的隐藏入口、目标与淡出绑定不一致。");
        }

        _hiddenLevelId = hasEntrance ? DragonPalaceRules.LevelId : 0;
        if (_entranceFade != null && _entranceFade.GetAnimationList().Length != 1)
        {
            throw new InvalidOperationException("隐藏入口淡出播放器必须只绑定一段完整动画。");
        }
    }

    /// <summary>
    /// 会话开始后才开放接触信号，加载期间保持入口禁用。
    /// </summary>
    private void StartEntrance()
    {
        _enteringHiddenLevel = _entranceFadeFinished = false;
        if (_hiddenEntrance == null)
        {
            return;
        }

        _hiddenEntrance.BodyEntered += OnEntranceBodyEntered;
        _entranceFade.AnimationFinished += OnEntranceFadeFinished;
        _hiddenEntrance.SetDeferred(Area2D.PropertyName.Monitoring, true);
    }

    /// <summary>
    /// 只接受本局存活英雄；原版自动触发，不要求W，淡出时只关闭玩家控制。
    /// </summary>
    /// <param name="body">实际进入水下区域的物理身体。</param>
    private void OnEntranceBodyEntered(Node2D body)
    {
        if (!_sessionActive || _enteringHiddenLevel || body != _player || !_player.IsAlive ||
            LevelModel.Outcome.Value != LevelOutcome.Running)
        {
            return;
        }

        _enteringHiddenLevel = true;
        _hiddenEntrance.SetDeferred(Area2D.PropertyName.Monitoring, false);
        this.GetSystem<HeroSystem>().SetControlsEnabled(false);
        _entranceFade.Play(_entranceFade.GetAnimationList().Single());
    }

    /// <summary>
    /// 动画信号中不清理场景，只等待后续正常游戏帧提交。
    /// </summary>
    /// <param name="animation">已完整播放的入口淡出。</param>
    private void OnEntranceFadeFinished(StringName animation) => _entranceFadeFinished = _enteringHiddenLevel;

    /// <summary>
    /// 淡出完成后由业务系统结束当前事务，再报告已验证的隐藏关目标。
    /// </summary>
    private void AdvanceEntrance()
    {
        if (!_entranceFadeFinished)
        {
            return;
        }

        _entranceFadeFinished = false;
        if (this.GetSystem<LevelSystem>().TransferToDragonPalace(_hiddenLevelId))
        {
            HiddenLevelRequested?.Invoke(_hiddenLevelId);
        }
    }

    /// <summary>
    /// 离关或死亡取消当前演出和订阅；旧入口不能回调新一局。
    /// </summary>
    private void StopEntrance()
    {
        if (_hiddenEntrance != null)
        {
            _hiddenEntrance.BodyEntered -= OnEntranceBodyEntered;
            _hiddenEntrance.SetDeferred(Area2D.PropertyName.Monitoring, false);
            if (_entranceFade != null)
            {
                _entranceFade.AnimationFinished -= OnEntranceFadeFinished;
                _entranceFade.Stop();
            }
        }
        _enteringHiddenLevel = _entranceFadeFinished = false;
    }
}
