using System;
using Godot;

namespace GameLogic.Utility;

/// <summary>矩形的朝向换算和有符号间隙；矩形本身直接使用 Godot.Rect2。</summary>
public static class SpatialExtensions
{
    extension(Rect2 reach)
    {
        /// <summary>素材原生朝左；朝右时水平镜像范围。</summary>
        /// <param name="direction">1 朝右，-1 朝左。</param>
        /// <returns>朝向换算后的范围。</returns>
        public Rect2 Facing(int direction) => direction > 0
            ? new Rect2(-reach.End.X, reach.Position.Y, reach.Size.X, reach.Size.Y) : reach;

        /// <summary>水平有符号间隙：正数为距离，负数为重叠深度。</summary>
        /// <param name="target">相同坐标空间的目标范围。</param>
        /// <returns>水平间隙，单位像素。</returns>
        public float GapX(Rect2 target) => Math.Max(reach.Position.X - target.End.X, target.Position.X - reach.End.X);

        /// <summary>垂直有符号间隙：正数为距离，负数为重叠深度。</summary>
        /// <param name="target">相同坐标空间的目标范围。</param>
        /// <returns>垂直间隙，单位像素。</returns>
        public float GapY(Rect2 target) => Math.Max(reach.Position.Y - target.End.Y, target.Position.Y - reach.End.Y);
    }
    extension((float Min, float Max) range)
    {
        /// <summary>在闭区间内按归一化随机值插值，反向区间按最小值收拢。</summary>
        /// <param name="sample">归一化采样值。</param>
        /// <returns>区间内的值。</returns>
        public float Sample(float sample) => range.Min + (Math.Max(range.Min, range.Max) - range.Min) * sample;
    }
}
