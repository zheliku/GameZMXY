using System;
using GameConfig.Skills;
using GameLogic.Input;
using Godot;

namespace GameLogic.Skills;

/// <summary>一次执行身份与共享身体标志，由 SkillModel 持有；身体招式归 GGF 状态，独立效果可扩展生命周期。</summary>
/// <param name="id">所属技能。</param>
/// <param name="canStart">可选起手条件；身体技能的招式流程由自己的 GGF 状态管理。</param>
public class SkillExecution(SkillId id, Func<ISkillActor, bool> canStart = null)
{
    /// <summary>所属技能。</summary>
    public SkillId Id { get; } = id;

    /// <summary>是否仍占用角色身体；转为持续效果后可释放身体而继续运行。</summary>
    public bool BlocksBody { get; protected internal set; } = true;

    /// <summary>是否结束整个执行，包括持续阶段。</summary>
    public bool Completed { get; protected internal set; }

    /// <summary>身体阶段是否暂停重力。</summary>
    public bool GravitySuspended { get; protected internal set; }

    /// <summary>是否阻止常规动作输入；本技能自己的后续输入仍会送达。</summary>
    public bool InputLocked { get; protected internal set; }

    /// <summary>最近一次推进的游戏时刻，防止释放身体的当帧重复推进。</summary>
    internal double LastAdvancedAt { get; set; } = double.NegativeInfinity;

    /// <summary>身体状态真正进入后开始；独立执行激活时直接开始。</summary>
    internal bool Started { get; set; }

    /// <summary>检查角色当前是否允许开始；此时尚未扣费。</summary>
    /// <param name="actor">本次调用借用的角色能力。</param>
    /// <returns>允许开始时为真。</returns>
    public virtual bool CanStart(ISkillActor actor) => canStart?.Invoke(actor) ?? true;

    /// <summary>费用已结算后开始执行。</summary>
    /// <param name="actor">本次调用借用的角色能力。</param>
    /// <param name="now">当前游戏秒数。</param>
    public virtual void Start(ISkillActor actor, double now) { }

    /// <summary>按游戏秒数推进当前阶段。</summary>
    /// <param name="actor">本次调用借用的角色能力。</param>
    /// <param name="dt">本帧游戏秒数。</param>
    public virtual void Advance(ISkillActor actor, float dt) { }

    /// <summary>接收同一技能的后续输入；二次发动、蓄力、松手与阶段转换由具体技能决定。</summary>
    /// <param name="actor">本次调用借用的角色能力。</param>
    /// <param name="input">本帧按键事实；按住不会伪造第二次按下。</param>
    /// <param name="bodyAvailable">是否可占用身体；其他技能占用期间仍接收松手事实。</param>
    public virtual void HandleInput(ISkillActor actor, SkillButtonInput input, bool bodyAvailable) { }

    /// <summary>完成、中断、换组或离场时释放执行拥有的规则状态。</summary>
    /// <param name="actor">本次调用借用的角色能力。</param>
    public virtual void Stop(ISkillActor actor) { }
}

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
    /// <param name="execution">所属动画执行身份，用于拒绝过期回调。</param>
    /// <param name="position">世界位置。</param>
    /// <param name="completed">可选完成回调；旧执行回调不得改变新执行。</param>
    void SpawnSkillEffect(SkillEffectId effect, SkillExecution execution, Vector2 position, Action completed = null);
}
