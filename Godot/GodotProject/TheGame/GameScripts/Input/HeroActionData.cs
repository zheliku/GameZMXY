using System.Collections.Generic;

namespace GameLogic.Input;

/// <summary>每个英雄独占的动作数据；不注册为全局 Model，不写入存档。由 InputSystem 与身体状态推进。</summary>
public sealed class HeroActionData
{
    /// <summary>本角色本帧已消费的动作边沿；由 InputSystem 管理，下一次采样清空。</summary>
    internal HashSet<string> ConsumedActions { get; } = new();

    /// <summary>角色输入规则已选定一次普攻，身体状态消费后立即清除。</summary>
    internal bool AttackRequested { get; set; }

    /// <summary>当前身体动作暂时锁定采样；进入该动作的状态负责在退出时解除。</summary>
    internal bool InputLocked { get; set; }

    /// <summary>游戏时钟秒数。</summary>
    public double Time { get; internal set; } = 0;

    /// <summary>当前水平移动方向。</summary>
    public int MoveAxis { get; internal set; } = 0;

    /// <summary>双击奔跑档。</summary>
    public bool Running { get; internal set; } = false;

    /// <summary>本帧无双请求。</summary>
    public bool MusouRequested { get; internal set; } = false;

    /// <summary>本次腾空已用跳数，落地归零。</summary>
    public int JumpCount { get; internal set; } = 0;

    /// <summary>最近成功选定的普攻段号。</summary>
    public int AttackSegment { get; internal set; } = -1;

    /// <summary>最近按下的方向。</summary>
    internal int LastKeyDirection { get; set; } = 0;

    /// <summary>最后按下且仍按住的方向。</summary>
    internal int FacingAxis { get; set; } = 0;

    /// <summary>最近方向按下时刻。</summary>
    internal double LastTapTime { get; set; } = double.NegativeInfinity;

    /// <summary>任意动作键松开后重新开启的动作闩锁。</summary>
    internal bool CanAct { get; set; } = true;

    /// <summary>上一次地面连段下标。</summary>
    internal int LastGroundSegment { get; set; } = -1;

    /// <summary>上一次普攻尝试秒数。</summary>
    internal double LastAttackAttempt { get; set; } = double.NegativeInfinity;

    /// <summary>下一次允许出招的游戏时刻。</summary>
    internal double AttackReadyAt { get; set; } = 0;

    /// <summary>输入组合或本帧松键事实。</summary>
    internal bool LeftHeld { get; set; } = false;

    /// <summary>输入组合或本帧松键事实。</summary>
    internal bool RightHeld { get; set; } = false;

    /// <summary>输入组合或本帧松键事实。</summary>
    internal bool LeftReleased { get; set; } = false;

    /// <summary>输入组合或本帧松键事实。</summary>
    internal bool RightReleased { get; set; } = false;

    /// <summary>输入组合或本帧松键事实。</summary>
    internal bool JumpHeld { get; set; } = false;

    /// <summary>输入组合或本帧松键事实。</summary>
    internal bool AttackHeld { get; set; } = false;

    /// <summary>输入组合或本帧松键事实。</summary>
    internal bool DownHeld { get; set; } = false;

    /// <summary>输入组合或本帧松键事实。</summary>
    internal bool UpHeld { get; set; } = false;

}
