using GameConfig.Equipment;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Enemies.Body;
using GameLogic.Event.Equipment;
using GameLogic.Heroes;
using GameLogic.Profile;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies;

/// <summary>
/// 最终形态等原生两秒退场完成才结束配方，前两形态不能触发这个清关出口。
/// </summary>
internal sealed class BullDemonHeartDeathState : AbstractEnemyBodyState
{
    private float _left; // 原生整体退场剩余秒数。
    private bool _reported; // 最终收益与清场只报告一次。

    /// <inheritdoc />
    protected override bool HurtInterrupts => false;

    /// <inheritdoc />
    protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
    {
        var owner = (BullDemonHeartEntity)body;
        body.MoveIntent = 0;
        body.TakeAttackRequest();
        body.TakePendingHurt();
        body.Velocity = Vector2.Zero;
        owner.BeginDeathPresentation();
        // 退场开始时赠入致死者背包；延迟的死亡事件仍留到两秒演出完成，不能提前清关。
        int heroId = GF.Entity.GetEntity(owner.LastHeroAttackerId) is AbstractHeroEntity hero ? hero.HeroId : 0;
        if (heroId != 0)
        {
            EquipmentGrantResult reward = owner.GetSystem<ProfileSystem>().GrantUniqueEquipment(heroId, EquipmentId.DiamondSnare, GD.Randf);
            if (reward != EquipmentGrantResult.AlreadyOwned)
            {
                GF.Event.Fire(owner, EquipmentRewardEventArgs.Create(EquipmentId.DiamondSnare,
                    reward == EquipmentGrantResult.Collected));
            }
        }

        _left = owner.DeathDuration;
        _reported = false;
    }

    /// <inheritdoc />
    protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
    {
        body.Velocity = Vector2.Zero;
        if (Elapsed(_left) && !_reported)
        {
            _reported = true;
            body.BeginDeath();
            body.RequestRecycle();
        }
        _left -= dt;
    }
}
