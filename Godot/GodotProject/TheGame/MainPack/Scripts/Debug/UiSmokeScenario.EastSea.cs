using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GameConfig.Attributes;
using GameConfig.Battle;
using GameConfig.Entity;
using GameFramework;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Attributes;
using GameLogic.Effects;
using GameLogic.Enemies;
using GameLogic.Enemies.AI;
using GameLogic.Enemies.Body;
using GameLogic.Heroes;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 东海的原生招式、背击规则、无敌与死亡回池专项。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 两个物理频率、双向逐招运行，攻击时点和范围仍由正式动画控制。
    /// </summary>
    /// <param name="hero">真实英雄沙包。</param>
    /// <param name="flow">本局共享模型与系统。</param>
    /// <returns>全部招式与生命周期完成的任务。</returns>
    private async Task CheckEastSeaEnemiesAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        var system = flow.GetSystem<HeroSystem>();
        var source = new AttributeSourceId("EastSeaSmoke", 1);
        system.SetSource(source, [AttributeModifier.Flat(AttributeType.MaxHp, 100000)]);
        system.Heal(int.MaxValue);
        try
        {
            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                foreach (var (id, count) in new[] { (EntityId.JiaoDemonKing, 3), (EntityId.LanternFish, 1) })
                {
                    foreach (int direction in new[] { -1, 1 })
                    {
                        for (int index = 0; index < count; index++)
                        {
                            hero.GlobalPosition = new Vector2(100000, 100000);
                            m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(id, new EnemySpawn(new Vector2(580, 100)));
                            var actor = m_Monster;
                            await WaitUntilAsync(() => actor.OnFloor, "东海出招夹具未落地");
                            SmokeInspection.FreezeAi(actor);
                            hero.GlobalPosition = actor.GlobalPosition + new Vector2(direction * 120, 0);
                            typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(actor, [hero]);
                            int hp = hero.Hp;
                            Check(((IEnemyAiAgent)actor).RequestAttack(index), "东海拒绝出招：" + id);
                            await WaitUntilAsync(() => actor.AttackSegment == index, "东海身体未消费攻击请求");
                            bool captured = false;
                            bool sawWave = false;
                            int actualHits = 0;
                            int previousHp = hp;
                            Vector2 castPosition = actor.GlobalPosition;
                            ulong started = Time.GetTicksMsec();
                            while (actor.AttackSegment >= 0 || actor.GetParent().GetChildren().OfType<SkillEffectNode>().Any(effect => effect.Visible))
                            {
                                Check(Time.GetTicksMsec() - started < 6500, "东海招式或龙波未结束");
                                var effect = actor.GetParent().GetChildren().OfType<SkillEffectNode>().FirstOrDefault(effect => effect.Visible);
                                if (effect != null && !sawWave)
                                {
                                    sawWave = true;
                                    Check(effect.GlobalPosition.IsEqualApprox(castPosition + new Vector2(direction * 30, direction * 30)),
                                        "龙波没有保留官方双向位置");
                                }
                                var shape = effect?.GetNode<CollisionShape2D>("Origin/Visual/HitBox/CollisionShape2D") ??
                                    actor.HitBox.GetNode<CollisionShape2D>("CollisionShape2D");
                                if (!shape.Disabled)
                                {
                                    hero.GlobalPosition = shape.GlobalPosition - (hero.HurtBox.GetGlobalBounds().GetCenter() - hero.GlobalPosition);
                                    if (!captured)
                                    {
                                        captured = true;
                                        await CaptureCombatAsync($"east_sea_{ticks}_{id}_{index}_{direction}");
                                    }
                                }
                                await WaitFramesAsync(1);
                                if (hero.Hp != previousHp)
                                {
                                    actualHits++;
                                }

                                previousHp = hero.Hp;
                            }
                            Check(hero.Hp < hp, $"东海招式未命中：{id}/{index}/{direction}/{ticks}");
                            int expectedHits = id == EntityId.LanternFish ? 2 : index == 2 ? 4 : 1;
                            Check(actualHits == expectedHits, $"嵌套源帧刷新次数不正确：{id}/{index} expected={expectedHits} actual={actualHits}");
                            Check(sawWave == (id == EntityId.JiaoDemonKing && index == 1), "龙波生成招式不正确");
                            Check(actor.HitBox.GetChildren().OfType<CollisionShape2D>().All(shape => shape.Disabled), "收招没有关闭身体判定");
                            await WaitUntilAsync(() => !((IEnemyAiAgent)actor).IsAttacking, "东海招式收招未完成");
                            if (actor is JiaoDemonKingEntity)
                            {
                                await WaitUntilAsync(() => SmokeInspection.ReadField<float>(actor, "m_InvulnerabilityLeft") == 0f,
                                    "45源帧旋转无敌没有结束");
                                await CheckJiaoDefenseAndDeathAsync(actor, hero);
                            }
                            else
                            {
                                ReleaseTestEntity(actor);
                            }

                            m_Monster = null;
                            GD.Print($"SMOKE-EAST SEA ATTACK 3-2 {ticks}Hz {id}/{index}/{direction} PASS: damage={hp - hero.Hp}, hits={actualHits}");
                            await WaitFramesAsync(3);
                        }
                    }
                }

                GD.Print($"SMOKE-EAST SEA 3-2 {ticks}Hz PASS: four attacks, both directions, back defense, pause, native death and reuse");
            }
        }
        finally
        {
            if (GF.Base.IsGamePaused)
            {
                GF.Base.ResumeGame();
            }

            if (m_Monster is { IsShown: true })
            {
                ReleaseTestEntity(m_Monster);
            }

            m_Monster = null;
            Engine.PhysicsTicksPerSecond = originalTicks;
            system.RemoveSource(source);
            system.Heal(int.MaxValue);
        }
    }

    /// <summary>
    /// 原生十帧背击反馈、效果硬直、暂停和死亡完成后复用验证。
    /// </summary>
    /// <param name="actor">已经收招的首领。</param>
    /// <param name="hero">来源英雄。</param>
    /// <returns>首领回池并重新显示后的检查。</returns>
    private async Task CheckJiaoDefenseAndDeathAsync(AbstractEnemyEntity actor, AbstractHeroEntity hero)
    {
        hero.GlobalPosition = new Vector2(100000, 100000);
        int facing = actor.Facing;
        int hp = actor.Hp;
        Check(ApplyEastSeaHit(actor, hero.Id, 100, facing, false, new Vector2(48, -48)) == 1 && actor.Hp == hp - 1,
            "鲛魔王背击不是固定一点伤害");
        var backHit = actor.GetNode<AnimatedSprite2D>("m_BackHit");
        Check(backHit.Visible && !((IEnemyBody)actor).HasPendingHurt, "身体背击没有专属反馈或错误地硬直");
        await WaitUntilAsync(() => !backHit.Visible, "十帧背击反馈没有结束");
        Check(ApplyEastSeaHit(actor, hero.Id, 100, facing, true, new Vector2(48, -48)) == 1 &&
            ((IEnemyBody)actor).HasPendingHurt, "效果背击没有走官方硬直路径");
        await WaitUntilAsync(() => !((IEnemyBody)actor).HasPendingHurt && !((IEnemyAiAgent)actor).IsCcLocked, "效果背击硬直没有结束");
        int frontDamage = ApplyEastSeaHit(actor, hero.Id, 100, -actor.Facing, false, Vector2.Zero);
        Check(frontDamage > 1 && frontDamage <= 100, "正面伤害错误地使用背击规则：" + frontDamage);
        int died = 0;
        void OnDied(GameLogic.Entity.AbstractActorEntity value)
        {
            died++;
        }
        actor.Died += OnDied;
        try
        {
            ApplyEastSeaHit(actor, hero.Id, 100000, -actor.Facing, false, Vector2.Zero);
            await WaitFramesAsync(3);
            Check(actor.Dead && actor.IsShown && !actor.HurtBox.Monitorable && died == 1, "首领死亡未保留演出或受击体没有关闭");
            GF.Base.PauseGame();
            await WaitFramesAsync(3);
            double position = actor.AnimPlayer.CurrentAnimationPosition;
            await WaitFramesAsync(8);
            Check(actor.IsShown && actor.AnimPlayer.CurrentAnimationPosition == position, "暂停推进了首领死亡");
            GF.Base.ResumeGame();
            await WaitUntilAsync(() => actor.GetParent().GetChildren().OfType<SkillEffectNode>().Any(effect => effect.Visible), "身体闪烁后未创建BossDead");
            Check(actor.IsShown && died == 1, "旋涡结束前提前回池或死亡重复通知");
            await WaitUntilAsync(() => !actor.IsShown, "死亡旋涡结束后没有回池");
        }
        finally
        {
            actor.Died -= OnDied;
        }
        await WaitFramesAsync(3);
        var reused = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.JiaoDemonKing, new EnemySpawn(new Vector2(580, 100)));
        Check(ReferenceEquals(actor, reused) && !reused.Dead && reused.Hp == reused.MaxHp &&
            !reused.GetNode<AnimatedSprite2D>("m_BackHit").Visible && SmokeInspection.ReadField<float>(reused, "m_InvulnerabilityLeft") == 0f,
            $"鲛魔王复用残留死亡、背击或无敌：same={ReferenceEquals(actor, reused)}, dead={reused.Dead}, hp={reused.Hp}/{reused.MaxHp}, back={reused.GetNode<AnimatedSprite2D>("m_BackHit").Visible}");
        ReleaseTestEntity(reused);
    }

    /// <summary>
    /// 使用正式受击入口，不绕过背部防御与无敌。
    /// </summary>
    /// <param name="actor">受击敌人。</param>
    /// <param name="attackerId">来源英雄编号。</param>
    /// <param name="power">真实伤害威力。</param>
    /// <param name="direction">来源朝向。</param>
    /// <param name="effect">是否独立效果。</param>
    /// <param name="knockback">击退速度。</param>
    /// <returns>实际伤害。</returns>
    private static int ApplyEastSeaHit(AbstractEnemyEntity actor, int attackerId, int power, int direction, bool effect, Vector2 knockback)
    {
        var data = AttackData.Create(0, default, power, DamageKind.Real, knockback, direction, 0, isEffect: effect);
        try
        {
            return actor.ReceiveHit(data, attackerId).Damage;
        }
        finally
        {
            ReferencePool.Release(data);
        }
    }

    /// <summary>
    /// 保留AI调度，通过目标距离检查远距旋转、近距龙波、普通攻击和冷却。
    /// </summary>
    /// <param name="hero">只改变位置的沙包。</param>
    /// <param name="flow">本局共享规则。</param>
    /// <returns>双频率自主行为验证。</returns>
    private async Task CheckEastSeaAiAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        var system = flow.GetSystem<HeroSystem>();
        var source = new AttributeSourceId("EastSeaAi", 1);
        system.SetSource(source, [AttributeModifier.Flat(AttributeType.MaxHp, 100000)]);
        system.Heal(int.MaxValue);
        try
        {
            foreach (int ticks in new[] { 60, 120 })
            {
                foreach (EntityId id in new[] { EntityId.JiaoDemonKing, EntityId.LanternFish })
                {
                    Engine.PhysicsTicksPerSecond = ticks;
                    hero.GlobalPosition = new Vector2(100000, 100000);
                    m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(id, new EnemySpawn(new Vector2(580, 100)));
                    var actor = m_Monster;
                    var agent = (IEnemyAiAgent)actor;
                    await WaitUntilAsync(() => actor.OnFloor, "东海AI未落地");
                    float distance = 280f;
                    void FollowActor()
                    {
                        hero.GlobalPosition = actor.GlobalPosition + new Vector2(distance, 0);
                    }
                    m_Driver.GetTree().PhysicsFrame += FollowActor;
                    try
                    {
                        FollowActor();
                        typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(actor, [hero]);
                        if (id == EntityId.JiaoDemonKing)
                        {
                            await WaitUntilAsync(() => actor.AttackSegment == 2, "鲛魔王远距没有旋转");
                            float invulnerability = SmokeInspection.ReadField<float>(actor, "m_InvulnerabilityLeft");
                            Check(invulnerability > 1.8f && invulnerability <= 45f / 24f &&
                                ApplyEastSeaHit(actor, hero.Id, 100, -actor.Facing, false, Vector2.Zero) == 0,
                                "旋转没有完整45源帧无敌");
                            GF.Base.PauseGame();
                            await WaitFramesAsync(3);
                            float paused = SmokeInspection.ReadField<float>(actor, "m_InvulnerabilityLeft");
                            await WaitFramesAsync(8);
                            Check(SmokeInspection.ReadField<float>(actor, "m_InvulnerabilityLeft") == paused, "暂停推进了旋转无敌");
                            GF.Base.ResumeGame();
                            await WaitUntilAsync(() => !agent.IsAttacking, "旋转未收招");
                            Check(agent.AiData.Cooldowns[2] > 7.9f && agent.AiData.Cooldowns[2] < 8.2f, "旋转不是十秒冷却");
                            distance = 120;
                            await WaitUntilAsync(() => actor.AttackSegment == 1, "近距没有优先龙波");
                            Check(agent.AiData.Cooldowns[1] > 5.9f, "龙波不是六秒冷却");
                            await WaitUntilAsync(() => actor.AttackSegment == 0, "龙波冷却中未改用普攻");
                        }
                        else
                        {
                            await WaitUntilAsync(() => agent.AiData.MoveIntent == 1, "灯笼鱼远距没有追击");
                            distance = 120;
                            await WaitUntilAsync(() => actor.AttackSegment == 0, "灯笼鱼没有自主扑击", 16000);
                        }
                        GD.Print($"SMOKE-EAST SEA AI 3-2 {id} {ticks}Hz PASS");
                    }
                    finally
                    {
                        m_Driver.GetTree().PhysicsFrame -= FollowActor;
                    }
                    ReleaseTestEntity(actor);
                    m_Monster = null;
                }
            }
        }
        finally
        {
            if (GF.Base.IsGamePaused)
            {
                GF.Base.ResumeGame();
            }

            if (m_Monster is { IsShown: true })
            {
                ReleaseTestEntity(m_Monster);
            }

            m_Monster = null;
            Engine.PhysicsTicksPerSecond = originalTicks;
            system.RemoveSource(source);
            system.Heal(int.MaxValue);
        }
    }
}
