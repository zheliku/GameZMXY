namespace GameLogic.Input;

/// <summary>项目输入映射的统一名称；与 project.godot 和 Godot 内置动作保持一致。</summary>
public static class InputActions
{
    /// <summary>打开或关闭背包；默认 C，可在输入映射中修改。</summary>
    public const string InventoryToggle = "inventory_toggle";

    /// <summary>使用关卡出口（W）。</summary>
    public const string Interact = "interact";

    /// <summary>角色向左移动。</summary>
    public const string MoveLeft = "move_left";

    /// <summary>角色向右移动。</summary>
    public const string MoveRight = "move_right";

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
}
