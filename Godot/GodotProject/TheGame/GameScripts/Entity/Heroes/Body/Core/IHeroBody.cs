using GameLogic.Heroes;
using GameLogic.Heroes.Input;
using Godot;
using GameLogic.Body;

namespace GameLogic.Heroes.Body
{
    /// <summary>
    /// 英雄身体状态机的宿主接口：在公共契约 <see cref="IActorBody"/> 之上只保留英雄专属成员
    /// （输入层、跳跃/连段事实、参数快照）。身体状态只经它读写，不接触节点、场景树、GD.*
    /// （纯 C#，可单测；Vector2 是 Godot 纯值类型，同 Battle/ 裁决）。
    /// 实现方：<see cref="HeroEntity"/>（真实物理）与 Tests 里的假宿主。
    /// </summary>
    public interface IHeroBody : IActorBody
    {
        /// <summary>身体参数（配置快照，含各动作时长 = 动画长度）</summary>
        HeroBodyParams Params { get; }

        /// <summary>局内效果对横向移动的倍率，默认不加成。</summary>
        float MoveSpeedMultiplier => 1f;

        /// <summary>局内效果对起跳速度的倍率，默认不加成。</summary>
        float JumpSpeedMultiplier => 1f;

        /// <summary>是否免受击硬直与击退；不代表免伤。</summary>
        bool SuperArmor => false;

        /// <summary>输入层，提供移动档位与动作组合闩锁。</summary>
        HeroInput Input { get; }

        /// <summary>本英雄拥有的普攻序列；跨移动、受击与攻击状态保留连段和冷却。</summary>
        HeroAttackSequence NormalAttack { get; }

        /// <summary>已用跳跃次数（起跳 +1；宿主在落地后归零）</summary>
        int JumpCount { get; set; }

        /// <summary>在单向平台上执行向下穿越；普通地面与空中不执行。</summary>
        /// <returns>是否从可穿平台向下离开。</returns>
        bool TryDropThroughPlatform();
    }
}
