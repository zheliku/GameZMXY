using System;
using GameConfig.Skills;
using Godot;

namespace GameLogic.Skills;

/// <summary>技能对角色表现的能力要求；调用期间借用，不由模型持有节点。</summary>
public interface ISkillActor
{
    /// <summary>角色世界位置。</summary>
    Vector2 SkillPosition { get; set; }

    /// <summary>速度，像素每秒。</summary>
    Vector2 Velocity { get; set; }

    /// <summary>是否接触地面或平台。</summary>
    bool OnFloor { get; }

    /// <summary>朝右为 1，朝左为 -1。</summary>
    int Facing { get; }

    /// <summary>开始手动驱动的技能动画与声音。</summary>
    /// <param name="action">只读动作配置。</param>
    void BeginSkillAnimation(SkillActionConfig action);

    /// <summary>更新技能动画局部时间。</summary>
    /// <param name="seconds">局部秒数。</param>
    void SetSkillAnimationTime(double seconds);

    /// <summary>释放身体判定并恢复常规动画驱动。</summary>
    void EndSkillAnimation();

    /// <summary>查找可见且在范围内的目标。</summary>
    /// <param name="range">距离上限，像素。</param>
    /// <param name="inset">视口左右留白，像素。</param>
    /// <returns>目标世界位置，没有则为空。</returns>
    Vector2? FindSkillTarget(float range, float inset);

    /// <summary>打开新的身体攻击编号。</summary>
    /// <param name="hit">伤害定义。</param>
    void NewSkillAttack(SkillHitId hit);

    /// <summary>创建独立技能特效。</summary>
    /// <param name="effect">特效定义。</param>
    /// <param name="data">所属技能实例，用于拒绝过期回调。</param>
    /// <param name="position">世界位置。</param>
    /// <param name="completed">可选完成回调；旧执行回调不得改变新执行。</param>
    void SpawnSkillEffect(SkillEffectId effect, SkillData data, Vector2 position, Action completed = null);
}
