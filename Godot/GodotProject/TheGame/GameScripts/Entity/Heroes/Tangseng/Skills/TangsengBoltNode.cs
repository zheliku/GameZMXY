using GameFramework.Entity;
using GameLogic.Battle.Damage;
using GameLogic.Effects;
using GameLogic.Enemies;
using GameLogic.Entity;
using Godot;
using GodotGameFramework;

namespace GameLogic.Heroes.Tangseng;

/// <summary>
/// 唐僧的直线法弹，复用独立效果的攻击预算、暂停和回池生命周期。
/// </summary>
public partial class TangsengBoltNode : SkillEffectNode
{
    [Export] private float _speed = 192f; // 场景指定的官方速度，像素每秒。
    [Export] private float _travelDistance = 304f; // 普攻源距离300，8px源步在第38次移动后销毁。
    [Export] private float _splashRadius; // 普攻在主命中附近的官方溅射半径；冰龙波为零。
    private float _distanceLeft; // 本次弹体仍可移动的距离，不写回共享场景。

    /// <inheritdoc />
    public override void OnGet()
    {
        base.OnGet();
        _distanceLeft = _travelDistance;
    }

    /// <summary>
    /// 按本次蓄力结果配置剩余射程，不改写场景默认值。
    /// </summary>
    /// <param name="distance">最大射程，像素。</param>
    public void ConfigureTravel(float distance) => _distanceLeft = distance;

    /// <inheritdoc />
    public override void _PhysicsProcess(double delta)
    {
        if (!IsActive || delta <= 0)
        {
            return;
        }

        // 先完成本帧位移并登记过期，再交给基类推进判定和回池；基类可能在本帧释放对象，
        // 因此派生逻辑不能在 base 返回后继续读取位置或距离。
        float distance = Mathf.Min(_distanceLeft, _speed * (float)delta);
        Position += Vector2.Right * (Scale.X > 0 ? -1 : 1) * distance;
        _distanceLeft -= distance;
        if (_distanceLeft <= 0)
        {
            RequestFinish();
        }
        base._PhysicsProcess(delta);
    }

    /// <inheritdoc />
    protected override void OnHitLanded(AbstractActorEntity target, DamageResult result)
    {
        if (_splashRadius <= 0)
        {
            return;
        }
        // 源attackRange只扩散这次主命中，不递归触发或消费额外弹体预算。
        foreach (IEntityGroup group in GF.Entity.GetAllEntityGroups())
        {
            foreach (IEntity actor in group.GetAllEntities())
            {
                if (actor is AbstractEnemyEntity enemy &&
                    target.GlobalPosition.DistanceTo(enemy.GlobalPosition) <= _splashRadius)
                {
                    ApplySplashHit(enemy);
                }
            }
        }
    }
}
