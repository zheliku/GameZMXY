using System.Collections.Generic;
using GameLogic.Body;
using Godot;

namespace GameLogic.Battle.Tests;

/// <summary>
/// 公共假宿主：实现 <see cref="IActorBody"/> 的重合成员（英雄与怪物的身体状态测试共用）。
/// </summary>
internal abstract class AbstractFakeActorBody : IActorBody
{
    /// <summary>
    /// 待处理的受击击退速度。
    /// </summary>
    public Vector2? PendingHurt;
    /// <summary>
    /// 当前朝向，右为 1、左为 -1。
    /// </summary>
    public int Facing = 1;
    /// <summary>
    /// 最近播放的动画名称。
    /// </summary>
    public string LastAnim = "";
    /// <summary>
    /// 测试注入的动画完成事实，与真实宿主的播放器查询对应。
    /// </summary>
    public bool AnimationComplete;

    /// <summary>
    /// 查询假动画的完成事实或切换状态。
    /// </summary>
    /// <param name="animation">待核对的动作。</param>
    /// <returns>完成或已经切换时为true。</returns>
    public bool IsAnimationComplete(string animation) => LastAnim != animation || AnimationComplete;
    /// <summary>
    /// 最近一次重播使用的原生起点标记。
    /// </summary>
    public string LastStartMarker;
    /// <summary>
    /// 按播放顺序记录的动画名称。
    /// </summary>
    public readonly List<string> Anims = new();
    /// <summary>
    /// 开始攻击回调次数。
    /// </summary>
    public int BeginCount;
    /// <summary>
    /// 结束攻击回调次数。
    /// </summary>
    public int EndCount;
    /// <summary>
    /// 死亡回调次数。
    /// </summary>
    public int DiedCount;

    /// <summary>
    /// 当前水平和垂直速度。
    /// </summary>
    public Vector2 Velocity { get; set; }

    /// <summary>
    /// 测试可控的公共落地事实。
    /// </summary>
    public bool OnFloor { get; set; } = true;

    /// <summary>
    /// 宿主是否已死亡。
    /// </summary>
    public bool Dead { get; set; }

    /// <summary>
    /// 测试使用的重力常量。
    /// </summary>
    public float Gravity => 980f;

    /// <summary>
    /// 当前攻击段索引，未攻击时为 -1。
    /// </summary>
    public int AttackSegment { get; set; } = -1;

    /// <summary>
    /// 是否存在待处理受击。
    /// </summary>
    public bool HasPendingHurt => PendingHurt.HasValue;

    /// <summary>
    /// 取出并清空测试身体本帧待消费的击退。
    /// </summary>
    /// <returns>本次待消费的击退速度；无待处理受击时为零向量。</returns>
    public Vector2 TakePendingHurt()
    {
        Vector2 v = PendingHurt ?? Vector2.Zero;
        PendingHurt = null;
        return v;
    }

    /// <summary>
    /// 按方向输入更新朝向。
    /// </summary>
    /// <param name="dir">方向符号：-1向左、0保持或停止、1向右。</param>
    public void SetFacing(int dir)
    {
        if (dir != 0)
        {
            Facing = dir > 0 ? 1 : -1;
        }
    }

    /// <summary>
    /// 仅在动画改变时播放动画。
    /// </summary>
    /// <param name="anim">已登记的动画名称；同名请求不重播。</param>
    public void PlayAnim(string anim)
    {
        if (anim != LastAnim)
        {
            RestartAnim(anim);
        }
    }

    /// <summary>
    /// 强制重新播放指定动画。
    /// </summary>
    /// <param name="anim">动作名称。</param>
    /// <param name="startMarker">可选原生起点标记。</param>
    public void RestartAnim(string anim, string startMarker = null)
    {
        AnimationComplete = false;
        LastAnim = anim;
        LastStartMarker = startMarker;
        Anims.Add(anim);
    }

    /// <summary>
    /// 记录攻击段开始。
    /// </summary>
    /// <param name="index">当前宿主攻击参数中的招式编号。</param>
    public void BeginAttack(int index)
    {
        BeginCount++;
        AttackSegment = index;
    }

    /// <summary>
    /// 记录攻击段结束并清除当前段。
    /// </summary>
    public void EndAttack()
    {
        EndCount++;
        AttackSegment = -1;
    }

    /// <summary>
    /// 记录一次死亡回调。
    /// </summary>
    public void BeginDeath() => DiedCount++;
}
