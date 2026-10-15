using GameLogic.Architecture;
using GameLogic.Monsters.Body;
using GameLogic.Monsters.AI;
using System;
using GameConfig.Battle;
using Godot;
using GodotGameFramework;

namespace GameLogic.Monsters
{
    /// <summary>怪物攻击装配、范围推导与出招生命周期；具体招式仍可重写。</summary>
    public abstract partial class AbstractMonsterEntity
    {
        /// <summary>把一条 AttackConfig 转成 AI 用法快照（默认读取动画判定；远程等特殊招式可重写）。</summary>
        /// <param name="index">招式下标（= AttackSegment）。</param>
        /// <param name="attack">该招的配置行。</param>
        /// <returns>AI 抽选与释放判定使用的规格。</returns>
        protected virtual MonsterAttackParams BuildAttackParams(int index, AttackConfig attack)
        {
            Rect2 reach = ReadAttackReach(attack.Animation);
            if (attack.AiWeight > 0 && !reach.HasArea() && attack.AiRange.Y <= 0f)
            {
                Log.Warning("[AbstractMonsterEntity] 招式 {0}（动画 {1}）无有效判定范围且 AiRange 为 0，AI 不会使用这招",
                    attack.Id, attack.Animation);
            }

            return new MonsterAttackParams
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

        /// <summary>提交出招：转向目标、装填攻击包并播放起手音。</summary>
        /// <param name="index">本怪物攻击配置数组中的下标。</param>
        private void BeginConfiguredAttack(int index)
        {
            // 出招开始时固定朝向并记录冷却，随后装填本段结算数据。
            SetFacing(Math.Sign(TargetBox.GetCenter().X));
            this.GetSystem<MonsterAiSystem>().MarkUsed(AiData, BodyParams, index, GD.Randf);
            AttackSegment = index;
            PrepareAttack(OwnAttacks[index], m_AttackMultiplier);
            PlaySound(OwnAttacks[index].SoundId);
            Log.Debug("[Monster] {0} 出招 段{1}", Id, index + 1);
        }

        /// <summary>结束或打断出招并归还攻击包。</summary>
        private void EndCurrentAttack()
        {
            AttackSegment = -1;
            ReleaseAttack();
        }

        /// <summary>取攻击动画每个启用窗口内的形状矩形并集；只读取首个直接碰撞子节点。</summary>
        /// <param name="animName">攻击动画名；原生判定坐标朝左。</param>
        /// <returns>相对实体原点的范围；缺少有效判定窗口时为空。</returns>
        private Rect2 ReadAttackReach(string animName)
        {
            AnimationPlayer player = AnimPlayer;
            if (player == null || HitBox == null || string.IsNullOrEmpty(animName) || !player.HasAnimation(animName) ||
                FindHitBoxShape(HitBox) is not { } node)
            {
                return default;
            }

            Animation anim = player.GetAnimation(animName);
            string path = player.GetNode(player.RootNode).GetPathTo(node).ToString();
            int disabled = anim.FindTrack($"{path}:disabled", Animation.TrackType.Value);
            int shape = anim.FindTrack($"{path}:shape", Animation.TrackType.Value);
            int position = anim.FindTrack($"{path}:position", Animation.TrackType.Value);
            // 判定盒容器与 Area2D 的本地位置（容器 scale 只负责朝向镜像，不参与原生坐标）
            Vector2 offset = HitBox.Position + (HitBoxRoot?.Position ?? Vector2.Zero);

            Rect2 GetHitBoxBoundsAtTime(float t)
            {
                Shape2D s = ReadAnimationValueAtTime(anim, shape, t)?.As<Shape2D>() ?? node.Shape;
                if (s == null)
                {
                    return default;
                }

                Vector2 p = (ReadAnimationValueAtTime(anim, position, t)?.AsVector2() ?? node.Position) + offset;
                Rect2 r = s.GetRect();
                return new Rect2(r.Position + p, r.Size);
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
            bool enabled = !node.Disabled;   // 第一个关键帧之前沿用场景值
            for (int k = 0; k <= keys; k++)
            {
                float end = k < keys ? (float)anim.TrackGetKeyTime(disabled, k) : length;
                if (enabled && end > start)
                {
                    IncludeReachBounds(GetHitBoxBoundsAtTime(start));
                    foreach (int track in new[] { shape, position })
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

        /// <summary>读取攻击区域的首个直接碰撞形状。</summary>
        /// <param name="area">攻击判定区域。</param>
        /// <returns>形状节点；未配置时为 null。</returns>
        private static CollisionShape2D FindHitBoxShape(Area2D area)
        {
            foreach (Node child in area.GetChildren())
            {
                if (child is CollisionShape2D shape)
                {
                    return shape;
                }
            }

            return null;
        }
    }
}
