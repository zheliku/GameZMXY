using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GameConfig.Attributes;
using GameConfig.Battle;
using GameConfig.Entity;
using GameConfig.Sound;
using GameFramework;
using GameLogic;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Attributes;
using GameLogic.Entity;
using GameLogic.Effects;
using GameLogic.Heroes;
using GameLogic.Level;
using GameLogic.Enemies;
using GameLogic.Enemies.AI;
using GameLogic.Profile;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>实际 BOSS 实体、原生招式、无敌窗口、独立风刃和实体池复用；不增加生产测试 API。</summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>以真实物理落地和身体 FSM 出招，分别覆盖两位 BOSS 的每项攻击。</summary>
    /// <param name="hero">本关正式英雄。</param>
    /// <param name="flow">已装配规则与配置的主流程。</param>
    /// <param name="level">正式场景，用于截图。</param>
    /// <returns>全部 BOSS 回归完成。</returns>
    private async Task CheckBossesAsync(AbstractHeroEntity hero, ProcedureOwner flow, LevelViewController level)
    {
        var bystanders = m_Driver.GetTree().Root.FindChildren("*", "CharacterBody2D", true, false)
            .OfType<AbstractEnemyEntity>().Where(x => x.IsVisibleInTree()).ToArray();
        foreach (var actor in bystanders)
        {
            SmokeInspection.FreezeAi(actor);
            actor.SetPhysicsProcess(false);
            actor.GlobalPosition = new Vector2(100000, 100000);
        }
        var source = new AttributeSourceId("SmokeBoss", 1);
        var heroSystem = flow.GetSystem<HeroSystem>();
        heroSystem.SetSource(source, [AttributeModifier.Flat(AttributeType.MaxHp, 100000)]);
        heroSystem.Heal(100000);
        try
        {
            hero.GlobalPosition = new Vector2(450, 100);
            hero.SetPhysicsProcess(true);
            await WaitFramesAsync(2);
            await WaitUntilAsync(() => hero.OnFloor, "BOSS 夹具英雄没有落地");
            hero.SetPhysicsProcess(false);
            foreach (var id in new[] { EntityId.MacaqueKing, EntityId.TamarinKing })
            {
                m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(id,
                    new EnemySpawn(new Vector2(580, 100)));
                SmokeInspection.FreezeAi(m_Monster);
                await WaitUntilAsync(() => m_Monster.OnFloor, $"{id} 没有真实落地");
                var actor = m_Monster;
                var player = actor.GetNode<AnimationPlayer>("m_AnimPlayer");
                var shape = actor.GetNodeOrNull<CollisionShape2D>("m_HitBoxRoot/m_HitBox/CollisionShape2D");
                var effect = actor.GetNode<AnimatedSprite2D>("m_Body/Effect");
                Check(actor.MaxHp == (id == EntityId.MacaqueKing ? 1500 : 3000), $"{id} 生命不符原版");
                typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(actor, [hero]);
                for (int index = 0; index < (id == EntityId.MacaqueKing ? 3 : 2); index++)
                {
                    heroSystem.Heal(100000);
                    int beforeHp = hero.Hp;
                    Check(((IEnemyAiAgent)actor).RequestAttack(index), $"{id} 拒绝第 {index + 1} 招");
                    await WaitUntilAsync(() => actor.AttackSegment == index, $"{id} 身体 FSM 没有开始第 {index + 1} 招");
                    Check(player.CurrentAnimation == $"attack_{index + 1}", $"{id} 攻击动画与状态不一致");
                    int hitFrames = 0;
                    int previousHp = hero.Hp;
                    bool enabled = false;
                    bool captured = false;
                    while (actor.AttackSegment == index)
                    {
                        if (hero.Hp < previousHp)
                        {
                            hitFrames++;
                            previousHp = hero.Hp;
                        }
                        enabled |= shape?.Disabled == false;
                        // 冻结目标在判定附近，英雄身体不推进也必须由怪物轮询命中。
                        if (shape?.Disabled == false)
                        {
                            Vector2 center = shape.GlobalPosition;
                            hero.GlobalPosition = new Vector2(center.X, hero.GlobalPosition.Y);
                        }
                        if (!captured && player.CurrentAnimationPosition > (index == 1 ? 1.7 : .2))
                        {
                            captured = true;
                            await CaptureCombatAsync($"boss_{id}_attack_{index + 1}");
                        }
                        if (index == 1)
                        {
                            bool invulnerable = (bool)typeof(AbstractActorEntity).GetProperty("Invulnerable",
                                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(actor);
                            Check(invulnerable == (id == EntityId.MacaqueKing || player.CurrentAnimationPosition < 2),
                                $"{id} 无敌时段不符原版");
                        }
                        await WaitFramesAsync(1);
                    }
                    if (index != 2)
                        Check(enabled, $"{id} 第 {index + 1} 招没有启用碰撞判定");
                    if (id == EntityId.MacaqueKing && index == 2)
                        await WaitUntilAsync(() => !actor.GetParent().GetChildren().OfType<SkillEffectNode>().Any(x => x.Visible),
                            "风刃完整 50 帧后没有回池");
                    Check(actor.HitBox.GetChildren().OfType<CollisionShape2D>().All(x => x.Disabled) &&
                        !effect.Visible, $"{id} 收招后仍保留判定或特效");
                    Check(hero.Hp < beforeHp && (index != 1 || hitFrames > 1), $"{id} 第 {index + 1} 招未造成实际伤害，或多段招式只结算一次");
                    GD.Print($"SMOKE-BOSS ATTACK: {id}/{index + 1}, hero damage={beforeHp - hero.Hp}, hit frames={hitFrames}");
                }
                // 重新取同一池实例时不得保留无敌、伤害、局部时钟或特效。
                ReleaseTestEntity(actor);
                m_Monster = null;
                await WaitFramesAsync(2);
                m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(id,
                    new EnemySpawn(new Vector2(580, 100)));
                Check(ReferenceEquals(actor, m_Monster) && actor.Hp == actor.MaxHp && actor.AttackSegment == -1 &&
                    actor.AiData.ElapsedSeconds < .2 && !effect.Visible, $"{id} 池复用残留状态或更换实例");
                ReleaseTestEntity(m_Monster);
                m_Monster = null;
            }
            GD.Print("SMOKE-BOSSES PASS: original health, all five native actions, hit windows, invulnerability, wind lifetime and pool reset.");
        }
        finally
        {
            heroSystem.RemoveSource(source);
            heroSystem.Heal(100000);
        }
    }
}
