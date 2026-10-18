using System;
using GameConfig.Skills;
using Godot;

namespace GameLogic.Skills;

/// <summary>状态借用的角色节点能力；模型不持有场景节点。</summary>
public interface ISkillActor
{
    /// <summary>角色世界位置。</summary>
    Vector2 SkillPosition { get; set; }
    /// <summary>是否接触地面或平台。</summary>
    bool OnFloor { get; }
    /// <summary>朝右为 1，朝左为 -1。</summary>
    int Facing { get; }

    /// <summary>播放原生技能动画，可从命名阶段开始；立即应用起始键。</summary>
    /// <param name="animation">动画库中的完整名称。</param>
    /// <param name="startMarker">可选的原生动画阶段标记。</param>
    void BeginSkillAnimation(string animation, string startMarker = null);

    /// <summary>由身体物理帧推进原生动画及其方法轨道。</summary>
    /// <param name="dt">游戏秒数。</param>
    void AdvanceSkillAnimation(float dt);

    /// <summary>释放身体判定并恢复常规动画驱动。</summary>
    void EndSkillAnimation();

    /// <summary>查找可见且在范围内的目标。</summary>
    /// <param name="range">距离上限，像素。</param>
    /// <param name="inset">视口左右留白，像素。</param>
    /// <returns>目标世界位置，没有则为空。</returns>
    Vector2? FindSkillTarget(float range, float inset);

    /// <summary>打开新身体攻击包；具体技能已完成自身的威力取整。</summary>
    /// <param name="hit">伤害定义编号。</param>
    /// <param name="power">无双加成前的威力。</param>
    /// <param name="knockbackOriginX">向中心两侧击退的横坐标；为空时沿朝向。</param>
    void NewSkillAttack(SkillHitId hit, double power, float? knockbackOriginX = null);

    /// <summary>从动画引用的场景创建独立特效并冻结本次攻击属性。</summary>
    /// <param name="scene">方法轨道直接引用的特效场景。</param>
    /// <param name="position">世界位置。</param>
    /// <param name="hit">可选伤害编号，纯视觉效果为 None。</param>
    /// <param name="power">无双加成前的威力。</param>
    /// <param name="completed">本次效果完成通知。</param>
    /// <param name="knockbackOriginX">具体招式指定的击退中心。</param>
    void SpawnSkillEffect(PackedScene scene, Vector2 position,
        SkillHitId hit = SkillHitId.None, double power = 0, Action completed = null,
        float? knockbackOriginX = null);
}
