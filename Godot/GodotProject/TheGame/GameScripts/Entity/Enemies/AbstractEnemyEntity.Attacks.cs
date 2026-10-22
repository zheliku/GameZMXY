using GameLogic.Architecture;
using GameLogic.Enemies.Body;
using GameLogic.Enemies.AI;
using System;
using GameConfig.Battle;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies
{
    /// <summary>怪物攻击装配、范围推导与出招生命周期；具体招式仍可重写。</summary>
    public abstract partial class AbstractEnemyEntity
    {
        /// <summary>把一条 AttackConfig 转成 AI 用法快照（默认读取动画判定；远程等特殊招式可重写）。</summary>
        /// <param name="index">招式下标（= AttackSegment）。</param>
        /// <param name="attack">该招的配置行。</param>
        /// <returns>AI 抽选与释放判定使用的规格。</returns>
        protected virtual EnemyAttackParams BuildAttackParams(int index, AttackConfig attack)
        {
            string animation = AttackAnimations[index];
            Rect2 reach = ReadAttackReach(animation);
            if (attack.AiWeight > 0 && !reach.HasArea() && attack.AiRange.Y <= 0f)
            {
                Log.Warning("[AbstractEnemyEntity] 招式 {0}（动画 {1}）无有效判定范围且 AiRange 为 0，AI 不会使用这招",
                    attack.Id, animation);
            }

            return new EnemyAttackParams
            {
                Index = index,
                Reach = reach,
                Weight = attack.AiWeight,
                Priority = attack.AiPriority,
                Range = (attack.AiRange.X, attack.AiRange.Y),
                Cooldown = (attack.AiCooldown.X, attack.AiCooldown.Y),
                InitCooldown = (attack.AiInitCooldown.X, attack.AiInitCooldown.Y),
            };
        }

        /// <summary>提交出招：转向目标并装填攻击包，声音时点由动画方法轨道控制。</summary>
        /// <param name="index">本怪物攻击配置数组中的下标。</param>
        private void BeginConfiguredAttack(int index)
        {
            // 出招开始时固定朝向并记录冷却，随后装填本段结算数据。
            SetFacing(Math.Sign(TargetBox.GetCenter().X));
            this.GetSystem<EnemyAiSystem>().MarkUsed(AiData, BodyParams, index, GD.Randf);
            AttackSegment = index;
            PrepareAttack(OwnAttacks[index], m_AttackMultiplier);
            Log.Debug("[Enemy] {0} 出招 段{1}", Id, index + 1);
        }

        /// <summary>结束或打断出招并归还攻击包。</summary>
        private void EndCurrentAttack()
        {
            AttackSegment = -1;
            ReleaseAttack();
        }

        /// <summary>原生动画多段时点更新当前招式的攻击编号，不重新计算冷却或切换身体状态。</summary>
        protected void NewAttackAnimEvent()
        {
            if (AttackSegment >= 0)
                PrepareAttack(OwnAttacks[AttackSegment], m_AttackMultiplier);
        }

        /// <summary>合并攻击区域中少量基础形状的有效窗口，供 AI 估算出手距离。</summary>
        /// <param name="animName">攻击动画名；原生判定坐标朝左。</param>
        /// <returns>相对实体原点的范围；缺少有效判定窗口时为空。</returns>
        private Rect2 ReadAttackReach(string animName)
        {
            AnimationPlayer player = AnimPlayer;
            if (player == null || HitBox == null || string.IsNullOrEmpty(animName) || !player.HasAnimation(animName))
            {
                return default;
            }

            Animation anim = player.GetAnimation(animName);
            Rect2 reach = default;
            foreach (Node child in HitBox.GetChildren())
            {
                if (child is not CollisionShape2D shape)
                    continue;
                Rect2 sample = ReadShapeReach(anim, shape);
                if (sample.HasArea())
                    reach = reach.HasArea() ? reach.Merge(sample) : sample;
            }
            return reach;
        }

        /// <summary>从同一节点的离散几何和启闭轨道计算范围；实际命中仍交给物理引擎。</summary>
        /// <param name="anim">攻击动画。</param>
        /// <param name="node">直接碰撞子节点。</param>
        /// <returns>动画中所有启用几何的并集。</returns>
        private Rect2 ReadShapeReach(Animation anim, CollisionShape2D node)
        {
            AnimationPlayer player = AnimPlayer;
            string path = player.GetNode(player.RootNode).GetPathTo(node).ToString();
            int disabled = anim.FindTrack($"{path}:disabled", Animation.TrackType.Value);
            int shape = anim.FindTrack($"{path}:shape", Animation.TrackType.Value);
            int position = anim.FindTrack($"{path}:position", Animation.TrackType.Value);
            int rotation = anim.FindTrack($"{path}:rotation", Animation.TrackType.Value);
            // 判定盒容器与 Area2D 的本地位置（容器 scale 只负责朝向镜像，不参与原生坐标）
            Vector2 offset = HitBox.Position + (HitBoxRoot?.Position ?? Vector2.Zero);

            Rect2 GetHitBoxBoundsAtTime(float t)
            {
                Shape2D s = ReadAnimationValueAtTime(anim, shape, t)?.As<Shape2D>() ?? node.Shape;
                if (s == null)
                    return default;
                Vector2 p = (ReadAnimationValueAtTime(anim, position, t)?.AsVector2() ?? node.Position) + offset;
                float angle = ReadAnimationValueAtTime(anim, rotation, t)?.AsSingle() ?? node.Rotation;
                return new Transform2D(angle, p) * s.GetRect();
            }

            // 采样时刻：disabled 轨道把动画切成若干窗口；每个启用窗口取起点 + 窗口内 shape/position 的关键帧
            Rect2 reach = default;
            void IncludeReachBounds(Rect2 sample)
            {
                if (sample.HasArea())
                    reach = reach.HasArea() ? reach.Merge(sample) : sample;
            }
            float length = (float)anim.Length;
            int keys = disabled < 0 ? 0 : anim.TrackGetKeyCount(disabled);
            float start = 0f;
            bool enabled = !node.Disabled;
            for (int k = 0; k <= keys; k++)
            {
                float end = k < keys ? (float)anim.TrackGetKeyTime(disabled, k) : length;
                if (enabled && end > start)
                {
                    IncludeReachBounds(GetHitBoxBoundsAtTime(start));
                    foreach (int track in new[] { shape, position, rotation })
                    {
                        for (int i = 0; track >= 0 && i < anim.TrackGetKeyCount(track); i++)
                        {
                            float t = (float)anim.TrackGetKeyTime(track, i);
                            if (t > start && t < end)
                            {
                                IncludeReachBounds(GetHitBoxBoundsAtTime(t));
                            }
                        }
                    }
                }

                if (k < keys)
                {
                    start = end;
                    enabled = !anim.TrackGetKeyValue(disabled, k).AsBool();
                }
            }

            return reach;
        }

        /// <summary>读取不晚于采样时刻的最后一个离散关键帧。</summary>
        /// <param name="anim">攻击动画。</param>
        /// <param name="track">值轨道下标；负数表示缺失。</param>
        /// <param name="t">采样时刻，秒。</param>
        /// <returns>关键帧值；缺失时使用场景默认值。</returns>
        private static Variant? ReadAnimationValueAtTime(Animation anim, int track, float t)
        {
            Variant? value = null;
            // 关键帧有序；走到首个晚于 t 的帧即可停止。
            for (int k = 0; track >= 0 && k < anim.TrackGetKeyCount(track) && anim.TrackGetKeyTime(track, k) <= t + 1e-4; k++)
            {
                value = anim.TrackGetKeyValue(track, k);
            }

            return value;
        }

    }
}
