namespace GameLogic.Input;

/// <summary>每个英雄独占的动作数据；不注册为全局 Model，不写入存档。由 InputSystem 与身体状态推进。</summary>
public sealed class HeroActionData
{
    private readonly SkillButtonInput[] m_SkillButtons = new SkillButtonInput[3]; // 当前帧的直接技能按键事实，不缓存待释放招式。

    /// <summary>本次按下的直接技能槽（1..3）；不跨物理帧缓存，不携带具体招式。</summary>
    public int? DirectSkillSlot { get; internal set; }

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

    /// <summary>读取技能槽本帧的按下、按住与松开事实。</summary>
    /// <param name="slot">从 1 开始的技能槽。</param>
    /// <returns>当前帧的按键事实。</returns>
    public SkillButtonInput GetSkillButton(int slot) => m_SkillButtons[slot - 1];

    /// <summary>输入采样写入槽位事实，角色规则不改写采样结果。</summary>
    /// <param name="slot">从 1 开始的技能槽。</param>
    /// <param name="input">当前帧输入。</param>
    internal void SetSkillButton(int slot, SkillButtonInput input) => m_SkillButtons[slot - 1] = input;
}

/// <summary>技能槽的一帧输入；连续按两次使用 Pressed，蓄力与松手使用 Held 和 Released。</summary>
/// <param name="Pressed">本帧是否出现按下边沿。</param>
/// <param name="Held">当前是否按住。</param>
/// <param name="Released">本帧是否出现松开边沿。</param>
public readonly record struct SkillButtonInput(bool Pressed, bool Held, bool Released);
