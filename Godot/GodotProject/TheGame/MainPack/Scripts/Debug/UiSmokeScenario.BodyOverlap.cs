using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GameConfig.Battle;
using GameConfig.Entity;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Damage;
using GameLogic.Entity;
using GameLogic.Enemies;
using GameLogic.Heroes;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;

/// <summary>
/// 用真实身体Area缓存验证同帧移动的候选复核，不在生产代码增加测试接口。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 建立真实重叠后在同一物理步移走攻击形状；新招不能消耗旧位置候选，恢复本地后仍须命中且去重。
    /// </summary>
    /// <param name="hero">真实攻击方，身体推进由夹具暂时停止。</param>
    /// <returns>60/120Hz两种物理频率的缓存与真实命中检查。</returns>
    private async Task CheckBodyOverlapReuseAsync(AbstractHeroEntity hero)
    {
        int oldTicks = Engine.PhysicsTicksPerSecond;
        var shapes = hero.HitBox.GetChildren().OfType<CollisionShape2D>().ToArray();
        var original = shapes.Select(shape => (shape.Shape, shape.Transform, shape.Disabled)).ToArray();
        var setAttack = typeof(AbstractActorEntity).GetMethod("SetAttack", BindingFlags.Instance | BindingFlags.NonPublic);
        var poll = typeof(AbstractActorEntity).GetMethod("PollAttackOverlaps", BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            hero.ReleaseAttack();
            hero.AnimPlayer.Stop();
            hero.SetPhysicsProcess(false);
            foreach (var shape in shapes)
            {
                shape.Disabled = true;
            }

            var hit = shapes[0];
            hit.Shape = new RectangleShape2D { Size = new Vector2(80, 80) };
            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EEntityId.Gorilla,
                    new EnemySpawn(new Vector2(650, 150)));
                SmokeInspection.FreezeAi(m_Monster);
                m_Monster.SetPhysicsProcess(false);
                SmokeInspection.StrengthenTrainingTarget(m_Monster);
                Vector2 center = m_Monster.HurtBox.GetGlobalBounds().GetCenter();
                hit.GlobalTransform = new Transform2D(0, center);
                hit.Disabled = false;
                await WaitUntilAsync(() => hero.HitBox.GetOverlappingAreas().Contains(m_Monster.HurtBox),
                    "身体复用夹具未建立真实Area重叠");
                int hp = m_Monster.Hp;
                var attacker = new CombatantAttributes(ECombatSide.Hero, 1, 10, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, hero.Id);
                // 保留物理引擎的旧候选，再模拟新招的形状或角色同帧移出。
                hit.GlobalPosition += new Vector2(700, 0);
                setAttack.Invoke(hero, [AttackData.Create(0, attacker, 10, EDamageKind.Real, Vector2.Zero, 1, 0)]);
                Check(hero.HitBox.GetOverlappingAreas().Contains(m_Monster.HurtBox), "身体Area未保留要复现的旧缓存");
                poll.Invoke(hero, null);
                Check(m_Monster.Hp == hp, $"{ticks}Hz身体攻击误伤700px外的旧候选");

                hit.GlobalPosition = center;
                poll.Invoke(hero, null);
                Check(m_Monster.Hp < hp, "恢复本地相交后身体攻击未命中，或旧候选提前消耗去重");
                int afterHp = m_Monster.Hp;
                poll.Invoke(hero, null);
                Check(m_Monster.Hp == afterHp, "同一招对同一身体重复结算");
                hero.ReleaseAttack();
                ReleaseTestEntity(m_Monster);
                m_Monster = null;
                hit.Disabled = true;
                await WaitFramesAsync(3);
                GD.Print($"SMOKE-BODY OVERLAP {ticks}Hz PASS: stale 700px candidate rejected; local hit and dedup retained");
            }
        }
        finally
        {
            hero.ReleaseAttack();
            for (int i = 0; i < shapes.Length; i++)
            {
                shapes[i].Shape = original[i].Shape;
                shapes[i].Transform = original[i].Transform;
                shapes[i].Disabled = original[i].Disabled;
            }
            ReleaseTestEntity(m_Monster);
            m_Monster = null;
            Engine.PhysicsTicksPerSecond = oldTicks;
        }
    }
}
