using GameLogic.Heroes;
using GameLogic.Input;
using Godot;
using GameLogic.Body;

namespace GameLogic.Heroes.Body
{
    /// <summary>
    /// 英雄身体状态机的宿主接口：在公共契约 <see cref="IActorBody"/> 之上只保留英雄专属成员
    /// （动作数据、无双运动倍率、共享身体参数）。身体状态只经它读写，不接触节点、场景树、GD.*
    /// （纯 C#，可单测；Vector2 是 Godot 纯值类型，同 Battle/ 裁决）。
    /// 实现方：<see cref="AbstractHeroEntity"/>（真实物理）与 Tests 里的假宿主。
    /// </summary>
    public interface IHeroBody : IActorBody, Architecture.ICanGetSystem, Architecture.ICanGetModel
    {
        /// <summary>同类英雄共享的只读身体参数，动作时长来自动画资源。</summary>
        HeroBodyParams Params { get; }

        /// <summary>局内效果对横向移动的倍率，默认不加成。</summary>
        float MoveSpeedMultiplier => 1f;

        /// <summary>局内效果对起跳速度的倍率，默认不加成。</summary>
        float JumpSpeedMultiplier => 1f;

        /// <summary>是否免受击硬直与击退；不代表免伤。</summary>
        bool SuperArmor => false;

        /// <summary>本英雄独占的输入、跳跃和连段数据；与共享 Params 分离。</summary>
        HeroActionData ActionData { get; }

        /// <summary>由具体角色匹配技能输入并向技能系统提交激活请求，不返回动作分类。</summary>
        /// <param name="attacking">是否正在普攻。</param>
        /// <param name="normalRatio">当前普攻帧比例。</param>
        /// <param name="airborne">是否处于空中。</param>
        void ProcessSkillInput(bool attacking, float normalRatio, bool airborne);

        /// <summary>消费当前角色允许的普攻输入并选择攻击段。</summary>
        /// <param name="attacking">是否正在普攻。</param>
        /// <param name="normalRatio">当前普攻帧比例。</param>
        /// <param name="airborne">是否处于空中。</param>
        /// <returns>已选定新的普攻段时为真。</returns>
        bool TryStartAttack(bool attacking, float normalRatio, bool airborne);

        /// <summary>在单向平台上执行向下穿越；普通地面与空中不执行。</summary>
        /// <returns>是否从可穿平台向下离开。</returns>
        bool TryDropThroughPlatform();
    }
}
