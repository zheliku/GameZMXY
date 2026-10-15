using System;
using GameLogic.Architecture;

namespace GameLogic.Utility;

/// <summary>纯相机窗口计算；不持有节点或可变状态。</summary>
public sealed class SpatialUtility : AbstractUtility
{
    /// <summary>按死区、可显示边界和单帧位移上限推进相机中心。</summary>
    /// <param name="current">当前相机中心的世界横坐标。</param>
    /// <param name="target">跟随目标的世界横坐标。</param>
    /// <param name="halfDeadZone">中央死区半宽，单位为世界像素。</param>
    /// <param name="minCenter">允许的最小中心横坐标，由区域左界加半屏宽得到。</param>
    /// <param name="maxCenter">允许的最大中心横坐标，由区域右界减半屏宽得到。</param>
    /// <param name="maxMovement">本帧允许的最大位移，单位为世界像素。</param>
    /// <returns>约束后的相机中心世界横坐标，始终位于可显示范围内。</returns>
    public float StepCameraCenter(float current, float target, float halfDeadZone, float minCenter, float maxCenter,
        float maxMovement)
    {
        // 目标位于死区内时偏移被抵消，相机不移动；越过死区后按超出量追随。
        float offset = target - current;
        float movement = offset - Math.Clamp(offset, -halfDeadZone, halfDeadZone);
        // 先夹到可显示范围，再限制单帧位移，避免开门后立即重居中。
        float desired = Math.Clamp(current + movement, minCenter, maxCenter);
        return current + Math.Clamp(desired - current, -maxMovement, maxMovement);
    }
}
