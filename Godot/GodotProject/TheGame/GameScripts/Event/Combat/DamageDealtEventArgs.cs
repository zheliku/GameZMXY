using GameFramework;
using GameFramework.Event;
using GameLogic.Battle.Damage;
using Godot;

namespace GameLogic.Event.Combat;

/// <summary>携带已完成的伤害结果和飘字位置；按值复制，不保留攻击实例或实体引用。</summary>
public sealed class DamageDealtEventArgs : GameEventArgs
{
    /// <summary>GGF 事件编号。</summary>
    public static readonly int EventId = typeof(DamageDealtEventArgs).GetHashCode();

    /// <inheritdoc />
    public override int Id => EventId;

    /// <summary>本次伤害、闪避、暴击和伤害类型。</summary>
    public DamageResult Result { get; private set; }

    /// <summary>受击方的运行实体编号；用于过滤场景所有权，实体回收后仍保留本次编号。</summary>
    public int TargetEntityId { get; private set; }

    /// <summary>是否使用英雄受伤的飘字样式。</summary>
    public bool TargetIsHero { get; private set; }

    /// <summary>受击时的世界坐标锚点，实体回收后仍有效。</summary>
    public Vector2 PopPosition { get; private set; }

    /// <summary>创建一次飘字通知；分发结束后由事件池归还。</summary>
    /// <param name="result">已经完成的伤害结算。</param>
    /// <param name="targetEntityId">受击方本次显示的运行实体编号。</param>
    /// <param name="targetIsHero">受击方是否为英雄。</param>
    /// <param name="popPosition">飘字的世界坐标。</param>
    /// <returns>仅在本次分发期间有效的参数。</returns>
    public static DamageDealtEventArgs Create(DamageResult result, int targetEntityId, bool targetIsHero, Vector2 popPosition)
    {
        var args = ReferencePool.Acquire<DamageDealtEventArgs>();
        args.Result = result;
        args.TargetEntityId = targetEntityId;
        args.TargetIsHero = targetIsHero;
        args.PopPosition = popPosition;
        return args;
    }

    /// <summary>回池前清除本次结果。</summary>
    public override void Clear()
    {
        Result = default;
        TargetEntityId = 0;
        TargetIsHero = false;
        PopPosition = Vector2.Zero;
    }
}
