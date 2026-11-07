using GameConfig.Battle;
using GameLogic.Effects;
using GameLogic.Enemies.AI;
using Godot;

namespace GameLogic.Enemies;

/// <summary>
/// 虾兵：保持射击距离，原生第3帧发出可被玩家攻击打散的魔法弹。
/// </summary>
public partial class ShrimpDemonEntity : AbstractSeaMonsterEntity
{
    private EnemyAiPreset _preset; // 巡逻与交战共用实例时钟，状态工厂注入场景退避内界。

    [Export] private CollisionShape2D _retreatRange; // 本物种的退避内界，不扩展通用敌人接口。
    [Export] private PackedScene _projectileScene; // 原生场景绑定的加速魔法弹。

    /// <inheritdoc />
    protected override EnemyAiPreset AiPreset
    {
        get
        {
            return _preset ??= new(typeof(PatrolState), typeof(ShrimpDemonEngageState),
                () => new PatrolState(), () => new ShrimpDemonEngageState(RetreatDistance));
        }
    }

    /// <summary>
    /// 场景退避形状的水平半径，像素。
    /// </summary>
    internal float RetreatDistance => _retreatRange.Shape.GetRect().End.X;

    /// <inheritdoc />
    public override void OnInit(int entityId, string entityAssetName, GameFramework.Entity.IEntityGroup entityGroup,
        bool isNewInstance, object userData)
    {
        base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);
        if (isNewInstance && (_retreatRange?.Shape is not SegmentShape2D || !_retreatRange.Disabled))
        {
            throw new System.InvalidOperationException("本物种必须绑定禁用物理碰撞的原生选招范围。");
        }
    }

    /// <summary>
    /// 在官方攻击第3源帧创建离身魔法弹，弹体拥有独立命中与消散生命周期。
    /// </summary>
    private void SpawnProjectileAnimEvent()
    {
        if (AttackSegment != 0)
        {
            return;
        }

        AttackConfig attack = OwnAttacks[0];
        SkillEffectNode effect = RentSkillEffect(_projectileScene);
        int power = (int)Mathf.Lerp(attack.FlatPower.X, attack.FlatPower.Y, GD.Randf());
        effect.Show(attack, GlobalPosition, Facing, CaptureCombatAttributes(), power,
            false, ReleaseSkillEffect, hitSoundId: HitSoundId);
    }
}
