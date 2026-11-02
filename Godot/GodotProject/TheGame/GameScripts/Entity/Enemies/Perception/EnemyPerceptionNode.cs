using System;
using Godot;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 场景的索敌、交战和巡逻形状；直接使用Godot原生形状编辑，实际命中由动画HitBox负责。
    /// </summary>
    public partial class EnemyPerceptionNode : Area2D
    {
        [Export] private CollisionShape2D m_DetectionShape; // 唯一首次索敌矩形，参与原生发现。
        [Export] private CollisionShape2D m_EngageShape; // 交战矩形、圆或水平线段；固定召唤/心脏及只冲撞的角色不配置。
        [Export] private CollisionShape2D m_PatrolShape; // 巡逻边界相对巡逻中心；固定召唤/心脏不配置。

        /// <summary>
        /// 场景的首次发现矩形，包含形状本地偏移。
        /// </summary>
        public Rect2 DetectionBounds
        {
            get
            {
                return Bounds(m_DetectionShape);
            }
        }

        /// <summary>
        /// 场景的交战形状边界，包含原生形状偏移。
        /// </summary>
        public Rect2 EngageBounds
        {
            get
            {
                return Bounds(m_EngageShape);
            }
        }

        /// <summary>
        /// 相对相机或出生点的巡逻左右界，像素。
        /// </summary>
        public Vector2 PatrolBounds
        {
            get
            {
                Rect2 bounds = Bounds(m_PatrolShape);
                return new Vector2(bounds.Position.X, bounds.End.X);
            }
        }

        /// <summary>
        /// 判断目标原点是否在场景交战形状内，包含边界。
        /// </summary>
        /// <param name="offset">目标原点相对敌人的位移。</param>
        /// <returns>是否在场景定义的范围内。</returns>
        public bool ContainsEngage(Vector2 offset)
        {
            Vector2 local = m_EngageShape.Transform.AffineInverse() * offset;
            if (m_EngageShape.Shape is CircleShape2D circle)
            {
                return local.LengthSquared() <= circle.Radius * circle.Radius;
            }
            if (m_EngageShape.Shape is SegmentShape2D segment)
            {
                return local.X >= Math.Min(segment.A.X, segment.B.X) && local.X <= Math.Max(segment.A.X, segment.B.X);
            }
            Rect2 bounds = m_EngageShape.Shape.GetRect();
            return local.X >= bounds.Position.X && local.X <= bounds.End.X &&
                local.Y >= bounds.Position.Y && local.Y <= bounds.End.Y;
        }

        /// <summary>
        /// 初始化时校验场景的唯一范围来源；不回退到表格或代码常量。
        /// </summary>
        /// <exception cref="InvalidOperationException">形状绑定或坐标系无效。</exception>
        public void Validate()
        {
            if (Position != Vector2.Zero || Scale != Vector2.One || Rotation != 0)
            {
                throw new InvalidOperationException($"敌人感知节点必须使用角色本地坐标：{GetPath()}");
            }
            if (m_DetectionShape?.Shape is not RectangleShape2D || m_DetectionShape.Disabled ||
                m_DetectionShape.GetParent() != this)
            {
                throw new InvalidOperationException($"敌人感知节点必须绑定启用的子矩形检测范围：{GetPath()}");
            }
            if (m_PatrolShape != null && (m_PatrolShape.Shape is not SegmentShape2D ||
                !m_PatrolShape.Disabled || m_PatrolShape.GetParent() != this))
            {
                throw new InvalidOperationException($"敌人巡逻范围必须是禁用的子线段形状：{GetPath()}");
            }
            if (m_EngageShape != null &&
                (m_EngageShape.Shape is not (RectangleShape2D or CircleShape2D or SegmentShape2D) ||
                 !m_EngageShape.Disabled || m_EngageShape.GetParent() != this))
            {
                throw new InvalidOperationException($"敌人交战范围的形状、启用状态或父级无效：{GetPath()}");
            }
            // 形状坐标使用本地像素；避免编辑器缩放/旋转产生与AI距离含义不同的范围。
            foreach (Node child in GetChildren())
            {
                if (child is not CollisionShape2D shape)
                {
                    continue;
                }
                if (shape.Shape == null || shape.Scale != Vector2.One || shape.Rotation != 0)
                {
                    throw new InvalidOperationException($"敌人AI范围必须使用正尺寸、无缩放旋转的原生形状：{shape.Name}");
                }

                bool validSize;
                if (shape.Shape is SegmentShape2D segment)
                {
                    bool horizontal = segment.A.Y == 0 && segment.B.Y == 0;
                    bool vertical = segment.A.X == 0 && segment.B.X == 0;
                    validSize = segment.A != segment.B && (horizontal || vertical);
                }
                else
                {
                    validSize = shape.Shape.GetRect().HasArea();
                }
                if (!validSize)
                {
                    throw new InvalidOperationException($"敌人AI范围必须使用正尺寸、无缩放旋转的原生形状：{shape.Name}");
                }
            }
        }

        /// <summary>
        /// 原生形状变换到感知节点空间，不建立范围缓存。
        /// </summary>
        /// <param name="node">已绑定的场景形状。</param>
        /// <returns>含本地位置和变换的边界。</returns>
        private static Rect2 Bounds(CollisionShape2D node)
        {
            return node.Transform * node.Shape.GetRect();
        }
    }
}
