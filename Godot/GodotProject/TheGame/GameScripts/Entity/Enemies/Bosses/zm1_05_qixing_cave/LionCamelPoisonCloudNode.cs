using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Damage;
using GameLogic.Config;
using GameLogic.Effects;
using GameLogic.Entity;
using GameLogic.Heroes;

namespace GameLogic.Enemies;

/// <summary>
/// 狮驼王毒雾：命中主体后施加十秒中毒；持续状态由本局状态效果系统拥有。
/// </summary>
public partial class LionCamelPoisonCloudNode : SkillEffectNode
{
    /// <inheritdoc />
    protected override void OnHitLanded(AbstractActorEntity target, DamageResult result)
    {
        if (target is AbstractHeroEntity { CanReceiveStatusEffect: true } hero)
        {
            int attackId = this.GetUtility<ConfigIdUtility>().GetAttackId(GameConfig.Entity.EntityId.LionCamelKing, 3, 2);
            this.GetSystem<HeroStatusSystem>().ApplyPoison(hero.HeroId, this.GetModel<ConfigurationModel>().Tables.TbAttackConfig.Get(attackId));
        }
    }
}
