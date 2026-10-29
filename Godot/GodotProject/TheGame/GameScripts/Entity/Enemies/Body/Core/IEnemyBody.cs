using GameLogic.Enemies.AI;
using GameLogic.Enemies;
using Godot;
using GameLogic.Body;
using System;

namespace GameLogic.Enemies.Body
{
    /// <summary>
    /// 怪物身体状态机的宿主接口：在公共契约 <see cref="IActorBody"/> 之上只保留怪物专属成员
    /// （移动意图、出招请求、回收）。身体状态只经它读写，不接触节点、场景树、GD.*
    /// （纯 C#，可单测；Vector2 是 Godot 纯值类型，同 Battle/ 裁决）。
    ///
    /// **两台状态机的边界**：AI 状态机（<see cref="AI.IEnemyAiAgent"/>）只写意图——移动意图 <see cref="MoveIntent"/>
    /// 与出招请求（<see cref="TakeAttackRequest"/> 取走）；身体状态机是受击、死亡、出招、收招硬直的唯一权威，
    /// 它不知道 AI 的存在。实现方：<see cref="AbstractEnemyEntity"/> 与 Tests 里的假宿主。
    /// </summary>
    public interface IEnemyBody : IActorBody
    {
        /// <summary>
        /// 同类怪物共享的只读身体定义，动作时长来自动画资源。
        /// </summary>
        EnemyBodyParams Params { get; }

        /// <summary>
        /// 普通行动状态；受击和收招结束后回到本敌人的移动方式。
        /// </summary>
        Type MovementStateType { get; }

        /// <summary>
        /// 本敌人的死亡状态；普通尸体与首领完整死亡演出使用各自状态。
        /// </summary>
        Type DeathStateType { get; }

        /// <summary>
        /// 移动意图：-1 左 / 0 停 / 1 右（AI 写入；身体在可移动状态下执行）
        /// </summary>
        int MoveIntent { get; set; }

        /// <summary>
        /// 取走 AI 的出招请求（招式序号；-1 = 没有）。受击/死亡时取走即作废。
        /// </summary>
        /// <returns>待处理的招式编号；没有请求时为-1。</returns>
        int TakeAttackRequest();

        /// <summary>
        /// 死亡进入时按敌人职责保留或关闭受击判定，并报告一次击败收益。
        /// </summary>
        void BeginDeath();

        /// <summary>
        /// 请求回收（宿主在本帧物理结束后 HideEntity，不在状态机推进中途销毁自己）
        /// </summary>
        void RequestRecycle();
    }

    /// <summary>
    /// 飞行与游动身体共享的垂直运动意图；地面行走无需实现。
    /// </summary>
    public interface IEnemyFreeMoveBody : IEnemyBody
    {
        /// <summary>
        /// -1上行、0保持高度、1下行。
        /// </summary>
        int VerticalIntent { get; }

        /// <summary>
        /// 当前阶段直接使用速度，不经过垂直加速。
        /// </summary>
        bool DirectVerticalMovement { get; }
    }
}
