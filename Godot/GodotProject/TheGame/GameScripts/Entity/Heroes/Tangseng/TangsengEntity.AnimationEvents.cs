using GameConfig.Battle;
using GameConfig.Skills;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Damage;
using GameLogic.Heroes.Tangseng;
using GameLogic.Input;
using GameLogic.Skills;
using Godot;

namespace GameLogic.Heroes;

/// <summary>
/// 唐僧原生动画的方法入口，技能规则由对应的具体状态执行。
/// </summary>
public partial class TangsengEntity
{
    /// <summary>
    /// 普攻第2源帧仍按住J时请求冰龙蓄力，状态迁移留到下一次身体推进。
    /// </summary>
    private void BeginIceDragonChargeAnimEvent()
    {
        if (this.GetSystem<InputSystem>().IsActionPressed(InputSystem.Attack, Slot))
        {
            this.GetSystem<SkillSystem>().TryActivate(HeroId, SkillId.IceDragonWave, this);
        }
    }

    /// <summary>
    /// 在普通攻击第七源帧发射法弹，冻结本次攻击属性。
    /// </summary>
    /// <param name="scene">动画直接引用的法弹场景。</param>
    private void FireNormalBoltAnimEvent(PackedScene scene)
    {
        AttackConfig hit = OwnAttacks[0];
        CombatantAttributes attributes = CaptureCombatAttributes();
        float power = (attributes.Power * hit.PowerScale.X + hit.FlatPower.X) *
            this.GetSystem<HeroSystem>().GetAttackPowerMultiplier(HeroId);
        var effect = (TangsengBoltNode)RentSkillEffect(scene);
        effect.Show(hit, GlobalPosition, Facing, attributes, (int)power,
            this.GetModel<HeroModel>().GetState(HeroId).WsActive.Value, ReleaseSkillEffect, hitSoundId: HitSoundId);
    }

    /// <summary>
    /// 把冰龙波释放键交给本次技能状态。
    /// </summary>
    /// <param name="normalScene">未满蓄力使用的普通法弹场景。</param>
    /// <param name="chargedScene">满蓄力使用的冰龙法弹场景。</param>
    private void ReleaseIceDragonAnimEvent(PackedScene normalScene, PackedScene chargedScene) => (CurrentSkillState as TangsengIceDragonState)?.Release(this, normalScene, chargedScene);

    /// <summary>
    /// 把玄冰阵出手键交给本次技能状态。
    /// </summary>
    /// <param name="scene">原生轨道引用的玄冰阵场景。</param>
    private void BeginDarkIceHitAnimEvent(PackedScene scene) => (CurrentSkillState as TangsengDarkIceState)?.BeginHit(this, scene);

    /// <summary>
    /// 把甘露治疗键交给本次技能状态。
    /// </summary>
    private void HealHeavenlyDewAnimEvent() => (CurrentSkillState as TangsengHeavenlyDewState)?.Heal(this);

    /// <summary>
    /// 甘露起手同时创建完整视觉与分身动作。
    /// </summary>
    /// <param name="scene">官方治疗场景。</param>
    private void BeginHeavenlyDewAnimEvent(PackedScene scene) => BeginHeavenlyDew(scene);

    /// <summary>
    /// 水魔爆无追加输入时在官方第42源帧收招。
    /// </summary>
    private void TimeoutWaterDemonAnimEvent() => (CurrentSkillState as TangsengWaterDemonBurstState)?.Timeout();
}
