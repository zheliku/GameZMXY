using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Damage;
using GameLogic.Config;
using GameLogic.Effects;
using GameLogic.Entity;
using GameLogic.Heroes;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 狮驼王毒雾：命中主体后施加十秒中毒；持续状态由本局英雄模型拥有。
    /// </summary>
    public partial class LionCamelPoisonCloudNode : SkillEffectNode
    {
        private const int PoisonAttackId = 11004; // 同版 Monster11 的持续伤害记录，区别于云雾主体伤害。

        /// <inheritdoc />
        protected override void OnHitLanded(AbstractActorEntity target, DamageResult result)
        {
            if (target is AbstractHeroEntity { CanReceiveStatusEffect: true })
            {
                this.GetSystem<HeroSystem>().ApplyPoison(this.GetModel<ConfigurationModel>().Tables.TbAttackConfig.Get(PoisonAttackId));
            }
        }
    }
}
