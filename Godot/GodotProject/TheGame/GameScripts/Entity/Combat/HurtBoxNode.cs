using GameLogic.Entity;
using Godot;

namespace GameLogic.Entity;

/// <summary>
/// 受击判定区：挂在实体的 _hurtBox 上，持有宿主实体的显式引用。
/// 攻击方 HitBox 直接读取 <see cref="OwnerEntity"/>，不爬父节点。
/// 引用由场景 Export 绑定并保留 node_paths，宿主初始化时校验。
/// </summary>
[GlobalClass]
public partial class HurtBoxNode : Area2D
{
    /// <summary>
    /// 宿主实体（场景里绑定到实体根节点）
    /// </summary>
    [Export] private AbstractActorEntity _owner;

    /// <summary>
    /// 宿主实体
    /// </summary>
    public AbstractActorEntity OwnerEntity => _owner;

    /// <summary>
    /// 受击区的世界包围盒：全部启用的碰撞形状 GetRect() 经各自全局变换后的并集（怪物 AI 判断"够不够得着"用）。
    /// 没有可用形状时返回以本节点为中心的零尺寸矩形。
    /// </summary>
    /// <returns>全部启用受击形状的世界包围盒；无形状时为节点位置的零尺寸矩形。</returns>
    public Rect2 GetGlobalBounds()
    {
        var bounds = new Rect2(GlobalPosition, Vector2.Zero);
        bool any = false;
        foreach (Node child in GetChildren())
        {
            if (child is not CollisionShape2D { Shape: { } shape, Disabled: false } shapeNode)
            {
                continue;
            }

            Rect2 rect = shapeNode.GlobalTransform * shape.GetRect();
            bounds = any ? bounds.Merge(rect) : rect;
            any = true;
        }

        return bounds;
    }

    /// <summary>
    /// 以当前启用形状和全局变换复核攻击候选，排除池复用、瞬移或动画缩形后的旧物理重叠缓存。
    /// </summary>
    /// <param name="attacks">攻击方装配时取得的固定形状节点；几何与启闭仍由原生动画拥有。</param>
    /// <returns>任一启用攻击形状与本受击区实际相交时为 true。</returns>
    internal bool Intersects(CollisionShape2D[] attacks)
    {
        foreach (Node child in GetChildren())
        {
            if (child is not CollisionShape2D { Disabled: false, Shape: { } targetShape } target)
            {
                continue;
            }

            foreach (CollisionShape2D attack in attacks)
            {
                if (!attack.Disabled && attack.Shape.Collide(attack.GlobalTransform, targetShape, target.GlobalTransform))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
