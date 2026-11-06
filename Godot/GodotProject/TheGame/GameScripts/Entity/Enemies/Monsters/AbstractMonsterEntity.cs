using GameLogic.Battle.Attacks;
using GameLogic.Battle.Damage;
using System;
using GameFramework.Entity;
using Godot;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 小怪保留致死一击的抛飞运动；活体受击机制共享Enemy，具体物种选择自己的AI与运动方式。
    /// </summary>
    public abstract partial class AbstractMonsterEntity : AbstractEnemyEntity
    {
        /// <summary>
        /// 小怪必须完整绑定公共生命反馈，缺失场景装配时立即报告。
        /// </summary>
        /// <param name="entityId">运行实体编号。</param>
        /// <param name="entityAssetName">正式场景资源路径。</param>
        /// <param name="entityGroup">所属框架实体组。</param>
        /// <param name="isNewInstance">是否首次创建池实例。</param>
        /// <param name="userData">本次初始化参数。</param>
        public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
        {
            base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);
            if (m_HealthBar == null)
            {
                throw new InvalidOperationException($"小怪场景缺少头顶生命条绑定：{entityAssetName}");
            }
        }

        /// <inheritdoc />
        protected override void OnHurt(AttackData data, DamageResult result, Vector2 knockback, int attackerEntityId)
        {
            base.OnHurt(data, result, knockback, attackerEntityId);
            if (Dead && knockback != Vector2.Zero)
            {
                // 死亡事实先于本回调置位；基类播放致死闪光，这里保留死亡抛飞。
                m_PendingHurt = knockback;
            }
        }
    }
}
