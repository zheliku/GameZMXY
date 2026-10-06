using System;
using Godot;

namespace GameLogic.Level;

/// <summary>以中央小死区跟随玩家，保持实际相机中心受限，并报告右缘抵达阶段边界。</summary>
[Tool]
public partial class LevelCamera : Camera2D
{
    [Export(PropertyHint.Range, "0,200,1")] private float m_DeadZoneHalfWidth = 40f; // 中央死区半宽，单位为屏幕像素。
    [Export(PropertyHint.Range, "1,2000,1")] private float m_MaxPanSpeed = 600f; // 追近取景目标的速度上限，单位为世界像素每秒。
    [Export] private Color m_GuideColor = new(0.2f, 0.85f, 1f, 0.8f); // 视野框与中央死区颜色。
    [Export] private bool m_DrawInGame; // 调试时显示相机取景引导。

    private Node2D m_Target; // 由关卡会话注入的跟随目标。
    private float m_Left; // 当前可显示区域左边缘的世界坐标。
    private float m_Right; // 当前可显示区域右边缘的世界坐标。
    private bool m_WaitingForArrival; // 当前通路的抵达事件尚未发送。

    /// <summary>实际视野右缘抵达当前阶段边界，每次开放通路只发送一次。</summary>
    public event Action RightBoundaryReached;

    /// <summary>场景配置的关卡左界。</summary>
    public float SceneLeft { get; private set; }

    /// <summary>场景配置的关卡右界。</summary>
    public float SceneRight { get; private set; }

    /// <summary>当前视野的世界尺寸，包含窗口扩展和相机缩放。</summary>
    public Vector2 ViewSize => (Engine.IsEditorHint() ? DesignViewSize : GetViewportRect().Size) / Zoom;

    /// <summary>实际取景右缘的世界坐标。</summary>
    public float ViewRight => GetScreenCenterPosition().X + ViewSize.X * 0.5f;

    private static Vector2 DesignViewSize => new( // 编辑器使用项目设计视口，不读取编辑器面板尺寸。
        ProjectSettings.GetSetting("display/window/size/viewport_width").AsSingle(),
        ProjectSettings.GetSetting("display/window/size/viewport_height").AsSingle());

    /// <summary>按死区、可显示边界和单帧位移上限推进相机中心。</summary>
    /// <param name="current">当前相机中心的世界横坐标。</param>
    /// <param name="target">跟随目标的世界横坐标。</param>
    /// <param name="halfDeadZone">中央死区半宽，单位为世界像素。</param>
    /// <param name="minCenter">允许的最小中心横坐标，由区域左界加半屏宽得到。</param>
    /// <param name="maxCenter">允许的最大中心横坐标，由区域右界减半屏宽得到。</param>
    /// <param name="maxMovement">本帧允许的最大位移，单位为世界像素。</param>
    /// <returns>约束后的相机中心世界横坐标，始终位于可显示范围内。</returns>
    public static float StepCenter(float current, float target, float halfDeadZone, float minCenter, float maxCenter, float maxMovement)
    {
        // 目标位于死区内时偏移被抵消，相机不移动；越过死区后按超出量追随。
        float offset = target - current;
        float movement = offset - Math.Clamp(offset, -halfDeadZone, halfDeadZone);
        // 先夹到可显示范围，再限制单帧位移，避免开门后立即重居中。
        float desired = Math.Clamp(current + movement, minCenter, maxCenter);
        return current + Math.Clamp(desired - current, -maxMovement, maxMovement);
    }

    /// <summary>记录关卡边界，只在游戏会话开始后启用物理跟随。</summary>
    public override void _Ready()
    {
        SceneLeft = m_Left = LimitLeft;
        SceneRight = m_Right = LimitRight;
        SetProcess(Engine.IsEditorHint());
        SetPhysicsProcess(false);
        if (!Engine.IsEditorHint())
        {
            // 窗口算法是相机中心的唯一写入方，禁用另一套拖动或平滑逻辑。
            PositionSmoothingEnabled = false;
            LimitSmoothed = false;
            DragHorizontalEnabled = false;
            DragVerticalEnabled = false;
            ProcessCallback = Camera2DProcessCallback.Physics;
            ProcessPhysicsPriority = 100;
        }

        QueueRedraw();
    }

    /// <summary>刷新编辑器中的视野和死区引导。</summary>
    /// <param name="delta">距上一帧的秒数；绘制刷新与它无关。</param>
    public override void _Process(double delta) => QueueRedraw();

    /// <summary>开始跟随已经显示的玩家；保留场景初始取景。</summary>
    /// <param name="target">关卡会话注入的玩家节点。</param>
    public void Follow(Node2D target)
    {
        m_Target = target;
        SetPhysicsProcess(true);
    }

    /// <summary>开放到下一阶段右界的通路，保留当前实际中心和左界。</summary>
    /// <param name="right">下一阶段区域右界的世界坐标。</param>
    public void TravelTo(float right)
    {
        m_Right = right;
        // 原生限位只有整数，向外取整，避免它再次裁掉脚本计算的分数坐标。
        LimitRight = Mathf.CeilToInt(right);
        m_WaitingForArrival = true;
    }

    /// <summary>锁定战斗区域左界；保留当前视野覆盖范围，避免边界收紧造成跳变。</summary>
    /// <param name="left">战斗区域左界的世界坐标。</param>
    public void LockLeft(float left)
    {
        m_Left = Math.Min(left, GetScreenCenterPosition().X - ViewSize.X * 0.5f);
        LimitLeft = Mathf.FloorToInt(m_Left);
        m_WaitingForArrival = false;
    }

    /// <summary>停止跟随和抵达回调，释放玩家引用。</summary>
    public void StopFollowing()
    {
        SetPhysicsProcess(false);
        m_Target = null;
        m_WaitingForArrival = false;
    }

    /// <summary>用实际中心推进相机窗口，随后按实际视野检测阶段抵达。</summary>
    /// <param name="delta">距上一物理帧的秒数，用于换算本帧位移上限。</param>
    public override void _PhysicsProcess(double delta)
    {
        // 玩家由会话显式注入和解除，不根据节点是否存在推测加载状态。
        float halfView = ViewSize.X * 0.5f;
        float center = StepCenter(GlobalPosition.X, m_Target.GlobalPosition.X,
            m_DeadZoneHalfWidth / Zoom.X, m_Left + halfView, m_Right - halfView, m_MaxPanSpeed * (float)delta);
        GlobalPosition = new Vector2(center, GlobalPosition.Y);
        ForceUpdateScroll();

        if (m_WaitingForArrival && Mathf.IsEqualApprox(ViewRight, m_Right))
        {
            m_WaitingForArrival = false;
            RightBoundaryReached?.Invoke();
        }

        if (m_DrawInGame)
        {
            QueueRedraw();
        }
    }

    /// <summary>绘制视野框和中央死区，两侧区域表示角色会推动相机。</summary>
    public override void _Draw()
    {
        if (!Engine.IsEditorHint() && !m_DrawInGame)
        {
            return;
        }

        Vector2 view = ViewSize;
        float deadZone = m_DeadZoneHalfWidth / Zoom.X;
        Rect2 frame = new(-view * 0.5f, view);
        Rect2 window = new(new Vector2(-deadZone, -view.Y * 0.5f), new Vector2(deadZone * 2f, view.Y));
        DrawRect(frame, m_GuideColor, false, 1f);
        DrawRect(window, new Color(m_GuideColor, 0.12f));
        DrawRect(window, m_GuideColor, false, 2f);
        DrawString(ThemeDB.FallbackFont, frame.Position + new Vector2(6f, 18f),
            $"Camera / dead zone ±{m_DeadZoneHalfWidth:0}px", fontSize: 14, modulate: m_GuideColor);
    }
}
