using GameLogic.Architecture;
using GameLogic.Heroes.Body;

namespace GameLogic.Input;

/// <summary>统一输入采样、方向双击、动作组合闩锁及连段冷却；每名英雄独占动作数据。</summary>
/// <param name="architecture">所属流程的模型与系统入口。</param>
/// <param name="source">可选替代输入源；默认在内部读取 Godot 输入。</param>
public sealed class InputSystem(IArchitecture architecture, IInputSource source = null) : AbstractSystem(architecture)
{
    private readonly IInputSource m_Source = source ?? new GodotInputSource(); // 输入来源；默认直接读取引擎，替代输入也走相同规则。

    private const double TimeEpsilon = 1e-4; // 物理时钟浮点容差，单位秒。

    /// <summary>打开或关闭背包；默认 C，可在输入映射中修改。</summary>
    public const string InventoryToggle = "inventory_toggle";

    /// <summary>使用关卡出口（W）。</summary>
    public const string Interact = "interact";

    /// <summary>角色向左移动。</summary>
    public const string MoveLeft = "move_left";

    /// <summary>角色向右移动。</summary>
    public const string MoveRight = "move_right";

    /// <summary>下方向，与跳跃组合穿过单向平台（S）。</summary>
    public const string MoveDown = "move_down";

    /// <summary>跳跃。</summary>
    public const string Jump = "jump";

    /// <summary>普通攻击。</summary>
    public const string Attack = "attack";

    /// <summary>发动无双。</summary>
    public const string Musou = "musou";

    /// <summary>Godot 内置界面确认。</summary>
    public const string UiAccept = "ui_accept";

    /// <summary>Godot 内置界面取消。</summary>
    public const string UiCancel = "ui_cancel";

    /// <summary>Godot 内置界面向左。</summary>
    public const string UiLeft = "ui_left";

    /// <summary>Godot 内置界面向右。</summary>
    public const string UiRight = "ui_right";

    /// <summary>Godot 内置界面向上。</summary>
    public const string UiUp = "ui_up";

    /// <summary>Godot 内置界面向下。</summary>
    public const string UiDown = "ui_down";

    /// <summary>内部读取输入来源并采样本英雄；短按边沿、组合闩锁与连段数据均保留在英雄中。</summary>
    /// <param name="hero">本帧采样的英雄宿主，提供动作数据和共享动作参数。</param>
    /// <param name="dt">本帧游戏秒数。</param>
    public void Sample(IHeroBody hero, float dt)
    {
        var data = hero.ActionData;
        float doubleTapWindow = hero.Params.RunDoubleTapWindow;
        bool leftHeld = m_Source.IsHeld(MoveLeft), rightHeld = m_Source.IsHeld(MoveRight);
        bool leftPressed = m_Source.WasPressed(MoveLeft), rightPressed = m_Source.WasPressed(MoveRight);
        bool jumpHeld = m_Source.IsHeld(Jump), attackHeld = m_Source.IsHeld(Attack);
        bool musouPressed = m_Source.WasPressed(Musou);
        bool downHeld = m_Source.IsHeld(MoveDown), upHeld = m_Source.IsHeld(Interact);
        bool jumpPressed = m_Source.WasPressed(Jump), attackPressed = m_Source.WasPressed(Attack);
        bool actionReleased = m_Source.WasReleased(Jump) || m_Source.WasReleased(Attack) ||
                              m_Source.WasReleased(MoveDown) || m_Source.WasReleased(Interact);
        bool leftReleased = m_Source.WasReleased(MoveLeft), rightReleased = m_Source.WasReleased(MoveRight);
        data.Time += dt;
        data.MusouRequested = musouPressed;
        bool jump = jumpHeld || jumpPressed;
        bool attack = attackHeld || attackPressed;

        // 四个动作键共用闩锁；被阻止的跳跃仅在仍按住时等待动作结束。
        if (actionReleased || (data.JumpHeld && !jump) || (data.AttackHeld && !attack) ||
            (data.DownHeld && !downHeld) || (data.UpHeld && !upHeld))
        {
            data.CanAct = true;
        }

        // 松方向取消奔跑；新的方向边沿更新朝向与双击窗口。
        data.LeftReleased = leftReleased || (data.LeftHeld && !leftHeld);
        data.RightReleased = rightReleased || (data.RightHeld && !rightHeld);
        if (data.LeftReleased || data.RightReleased)
        {
            data.Running = false;
        }

        if (leftPressed)
        {
            RegisterDirectionPress(data, doubleTapWindow, -1);
        }

        if (rightPressed)
        {
            RegisterDirectionPress(data, doubleTapWindow, 1);
        }

        if ((jump && !data.JumpHeld) || (attack && !data.AttackHeld) || (downHeld && !data.DownHeld) ||
            (upHeld && !data.UpHeld) ||
            musouPressed)
        {
            data.LastKeyDirection = 0;
        }

        data.LeftHeld = leftHeld;
        data.RightHeld = rightHeld;
        data.JumpHeld = jump;
        data.AttackHeld = attack;
        data.DownHeld = downHeld;
        data.UpHeld = upHeld;
        if (!IsDirectionHeld(data, data.FacingAxis))
        {
            data.FacingAxis = 0;
        }

        data.MoveAxis = data.FacingAxis;
    }

    /// <summary>消费 J 或 S＋J，冷却拒绝的尝试也耗掉闩锁。</summary>
    /// <param name="data">本英雄拥有的动作数据。</param>
    /// <returns>是否存在可消费的普攻组合。</returns>
    public bool ConsumeAttack(HeroActionData data) =>
        ConsumeActionIfMatched(data, data.AttackHeld && !data.JumpHeld && !data.UpHeld);

    /// <summary>消费单独 K。</summary>
    /// <param name="data">本英雄拥有的动作数据。</param>
    /// <returns>是否存在可消费的跳跃组合。</returns>
    public bool ConsumeJump(HeroActionData data) =>
        ConsumeActionIfMatched(data, data.JumpHeld && !data.AttackHeld && !data.DownHeld && !data.UpHeld);

    /// <summary>消费 S＋K 穿平台组合。</summary>
    /// <param name="data">本英雄拥有的动作数据。</param>
    /// <returns>是否存在可消费的穿平台组合。</returns>
    public bool ConsumePlatformDrop(HeroActionData data) => ConsumeActionIfMatched(data,
        data.DownHeld && data.JumpHeld && !data.AttackHeld && !data.UpHeld);

    /// <summary>允许动作时吞掉 J＋K，避免误触普攻或跳跃。</summary>
    /// <param name="data">本英雄拥有的动作数据。</param>
    public void ConsumeJumpAttackCombination(HeroActionData data) =>
        ConsumeActionIfMatched(data, data.AttackHeld && data.JumpHeld && !data.DownHeld && !data.UpHeld);

    /// <summary>查询攻击起手时的方向是否仍按住。</summary>
    /// <param name="data">本英雄拥有的动作数据。</param>
    /// <param name="direction">左为 -1，右为 1。</param>
    /// <returns>指定方向是否仍按住。</returns>
    public bool IsDirectionHeld(HeroActionData data, int direction) =>
        direction < 0 ? data.LeftHeld : direction > 0 && data.RightHeld;

    /// <summary>查询指定方向本帧是否松开过，供攻击状态永久停止已有横移。</summary>
    /// <param name="data">本英雄拥有的动作数据。</param>
    /// <param name="direction">左为 -1，右为 1；零表示没有方向。</param>
    /// <returns>指定方向是否出现松键边沿。</returns>
    public bool WasDirectionReleased(HeroActionData data, int direction) =>
        direction < 0 ? data.LeftReleased : direction > 0 && data.RightReleased;

    /// <summary>受击或未学跑攻的地面普攻取消奔跑档。</summary>
    /// <param name="data">本英雄拥有的动作数据。</param>
    public void StopRunning(HeroActionData data) => data.Running = false;

    /// <summary>消费一次匹配的动作组合。</summary>
    /// <param name="data">本英雄拥有的动作数据。</param>
    /// <param name="matches">按键是否匹配。</param>
    /// <returns>组合与闩锁均允许时返回 true。</returns>
    private bool ConsumeActionIfMatched(HeroActionData data, bool matches)
    {
        if (!data.CanAct || !matches)
        {
            return false;
        }

        data.CanAct = false;
        return true;
    }

    /// <summary>同方向两次按下间隔严格小于窗口时奔跑。</summary>
    /// <param name="data">本英雄拥有的动作数据。</param>
    /// <param name="doubleTapWindow">双击窗口秒数。</param>
    /// <param name="direction">本次方向。</param>
    private void RegisterDirectionPress(HeroActionData data, float doubleTapWindow, int direction)
    {
        data.Running = direction == data.LastKeyDirection && data.Time - data.LastTapTime < doubleTapWindow;
        data.LastKeyDirection = data.FacingAxis = direction;
        data.LastTapTime = data.Time;
    }

    /// <summary>消费一次普攻尝试；冷却允许时选定下一段，拒绝的输入不留到后续帧。</summary>
    /// <param name="data">本英雄的输入组合和游戏时钟。</param>
    /// <param name="parameters">同类英雄共享的只读动作参数。</param>
    /// <param name="airborne">本次是否从空中出招。</param>
    /// <param name="isAttacking">当前是否已经处于攻击动作。</param>
    /// <returns>已选定本次攻击段时返回 true。</returns>
    public bool TryStartAttack(HeroActionData data, HeroBodyParams parameters, bool airborne, bool isAttacking)
    {
        if (parameters.ComboLength == 0 || !ConsumeAttack(data))
        {
            return false;
        }

        double now = data.Time;
        bool reset = now - data.LastAttackAttempt > parameters.ComboResetTime + TimeEpsilon;
        data.LastAttackAttempt = now;
        if (now + TimeEpsilon < data.AttackReadyAt || (!airborne && data.Running && isAttacking))
        {
            return false;
        }

        // 原版先按上一段设置本次起手后的冷却，再推进段号；与动画收招时间独立。
        float cooldown;
        if (airborne)
        {
            cooldown = parameters.AirAttackCooldown;
            data.LastGroundSegment = -1;
            data.AttackSegment = parameters.AirAttackIndex;
        }
        else
        {
            cooldown = parameters.AttackInputCooldowns[System.Math.Max(data.LastGroundSegment, 0)];
            data.LastGroundSegment = reset ? 0 : (data.LastGroundSegment + 1) % parameters.ComboLength;
            data.AttackSegment = data.LastGroundSegment;
            StopRunning(data);
        }

        data.AttackReadyAt = now + cooldown;
        return true;
    }

    /// <summary>池实例重新显示时重置本英雄的输入、跳跃次数及连段，不影响其他英雄。</summary>
    /// <param name="data">本英雄拥有的动作数据。</param>
    public void Reset(HeroActionData data)
    {
        data.Time = 0;
        data.LastTapTime = data.LastAttackAttempt = double.NegativeInfinity;
        data.LastKeyDirection = data.FacingAxis = data.MoveAxis = data.JumpCount = 0;
        data.LeftHeld = data.RightHeld = data.JumpHeld = data.AttackHeld = data.DownHeld = data.UpHeld = false;
        data.Running = data.MusouRequested = data.LeftReleased = data.RightReleased = false;
        data.CanAct = true;
        data.AttackSegment = data.LastGroundSegment = -1;
        data.AttackReadyAt = 0;
    }

    /// <summary>默认来源直接读取 Godot 的动作映射及本帧边沿。</summary>
    private sealed class GodotInputSource : IInputSource
    {
        /// <inheritdoc />
        public bool IsHeld(string action) => Godot.Input.IsActionPressed(action);

        /// <inheritdoc />
        public bool WasPressed(string action) => Godot.Input.IsActionJustPressed(action);

        /// <inheritdoc />
        public bool WasReleased(string action) => Godot.Input.IsActionJustReleased(action);
    }
}

/// <summary>按动作名提供当前输入事实；替代输入源只负责读键，组合与冷却规则仍由 InputSystem 执行。</summary>
public interface IInputSource
{
    /// <summary>查询当前是否按住。</summary>
    /// <param name="action">Godot 动作映射名称。</param>
    /// <returns>当前按住时为 true。</returns>
    bool IsHeld(string action);

    /// <summary>查询本帧是否刚按下，包括同帧结束的短按。</summary>
    /// <param name="action">Godot 动作映射名称。</param>
    /// <returns>存在按下边沿时为 true。</returns>
    bool WasPressed(string action);

    /// <summary>查询本帧是否松开过，包括同帧重新按住。</summary>
    /// <param name="action">Godot 动作映射名称。</param>
    /// <returns>存在松开边沿时为 true。</returns>
    bool WasReleased(string action);
}
