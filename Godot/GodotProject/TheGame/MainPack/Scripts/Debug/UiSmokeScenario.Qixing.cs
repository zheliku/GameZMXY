using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GameConfig.Attributes;
using GameConfig.Entity;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attributes;
using GameLogic.Effects;
using GameLogic.Enemies;
using GameLogic.Enemies.AI;
using GameLogic.Entity;
using GameLogic.Heroes;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 七星洞的真实招式、离身判定、中毒、暂停与对象池复用验证。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 不代发攻击请求，验证狮驼王真实 AI 的近远选招、秒制冷却、暂停与池复用。
    /// </summary>
    /// <param name="hero">本关英雄，测试期间停止身体步进并保持相对距离。</param>
    /// <param name="flow">本关模型和规则的作用域。</param>
    /// <returns>60/120Hz 的自主选招轨迹验证任务。</returns>
    private async Task CheckLionCamelKingAiAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        Vector2 origin = hero.GlobalPosition;
        var source = new AttributeSourceId("LionAiSmoke", 1);
        var heroSystem = flow.GetSystem<HeroSystem>();
        heroSystem.SetSource(source, [AttributeModifier.Flat(AttributeType.MaxHp, 100000)]);
        heroSystem.Heal(int.MaxValue);
        try
        {
            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                hero.GlobalPosition = new Vector2(100000, 100000);
                m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.LionCamelKing,
                    new EnemySpawn(new Vector2(580, 100)));
                var actor = m_Monster;
                var agent = (IEnemyAiAgent)actor;
                var attacks = (System.Collections.Generic.IReadOnlyList<GameConfig.Battle.AttackConfig>)typeof(AbstractActorEntity)
                    .GetProperty("OwnAttacks", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(actor)!;
                await WaitUntilAsync(() => actor.OnFloor, "狮驼王 AI 夹具未落地");
                Check(agent.AiPreset.IdleStateType == typeof(LionCamelKingEngageState), "狮驼王没有使用专属选招状态");
                Check(Near(attacks[1].AiCooldown.X, 100f / 24f) &&
                    Near(attacks[1].AiCooldown.Y, 100f / 24f) &&
                    Near(attacks[2].AiCooldown.X, 11) && Near(attacks[2].AiCooldown.Y, 11), "狮驼王正式配置的冷却或持续追踪规则不符原版");

                var casts = new System.Collections.Generic.List<(double Time, int Index, float Distance)>();
                float targetDistance = 200;
                int previous = -1;
                // 只调整夹具站位；每次攻击仍由正式 AI、身体状态和动画自行完成。
                void SamplePhysics()
                {
                    hero.GlobalPosition = actor.GlobalPosition + new Vector2(targetDistance, 0);
                    if (actor.AttackSegment >= 0 && actor.AttackSegment != previous)
                    {
                        casts.Add((actor.AiData.ElapsedSeconds, actor.AttackSegment, agent.TargetOffset.X));
                    }

                    previous = actor.AttackSegment;
                }

                // 在固定相对距离等待下一次真实出招，近身冷却期间可允许普攻经过。
                async Task<(double Time, int Index, float Distance)> WaitForCast(int index, float distance, bool allowBasic = false)
                {
                    targetDistance = distance;
                    int observed = casts.Count;
                    double started = actor.AiData.ElapsedSeconds;
                    while (actor.AiData.ElapsedSeconds - started < 15)
                    {
                        while (observed < casts.Count)
                        {
                            var cast = casts[observed++];
                            if (cast.Index == index)
                            {
                                return cast;
                            }

                            Check(allowBasic && cast.Index == 0,
                                                            $"狮驼王选招错误：期望{index}，实际{cast.Index}，距离{distance}，时间{cast.Time:0.000}");
                        }
                        await WaitFramesAsync(1);
                    }
                    throw new InvalidOperationException($"狮驼王在距离{distance}时未自主释放第{index + 1}招。");
                }

                m_Driver.GetTree().PhysicsFrame += SamplePhysics;
                try
                {
                    var cloud = await WaitForCast(2, 200);
                    GF.Base.PauseGame();
                    await WaitFramesAsync(2);
                    double age = actor.AiData.ElapsedSeconds;
                    float remaining = actor.AiData.Cooldowns[2];
                    await WaitFramesAsync(10);
                    Check(actor.AiData.ElapsedSeconds == age && actor.AiData.Cooldowns[2] == remaining,
                        "暂停期间狮驼王继续推进选招时钟或毒雾冷却");
                    GF.Base.ResumeGame();
                    var spin = await WaitForCast(1, 100);
                    var basic = await WaitForCast(0, 100);
                    var nextSpin = await WaitForCast(1, 100, true);
                    Check(nextSpin.Time - spin.Time >= 100d / 24d - .05,
                        "近身旋风未等待原版100/24秒冷却");
                    Check(new[] { spin.Time, basic.Time, nextSpin.Time }.All(time => Math.Abs(time - Math.Round(time)) < .1),
                        "狮驼王近身出招没有遵守原版整秒决策节拍");
                    var nextCloud = await WaitForCast(2, 600);
                    Check(nextCloud.Time - cloud.Time >= 10.95 && nextCloud.Time - cloud.Time <= 11.15 &&
                        agent.HasTarget, $"毒雾11秒独立冷却或追击不符原版：cloud={cloud.Time:F4}, next={nextCloud.Time:F4}, retained={agent.HasTarget}");
                    System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath("res://.godot/validation/qixing_combat_alignment"));
                    System.IO.File.WriteAllText(ProjectSettings.GlobalizePath($"res://.godot/validation/qixing_combat_alignment/lion_ai_{ticks}.json"),
                        Newtonsoft.Json.JsonConvert.SerializeObject(casts.Select(cast => new { cast.Time, cast.Index, cast.Distance }),
                            Newtonsoft.Json.Formatting.Indented));
                    GD.Print($"SMOKE-LION-AI {ticks}Hz: cloud={cloud.Time:0.000}/{nextCloud.Time:0.000}, " +
                        $"spin={spin.Time:0.000}/{nextSpin.Time:0.000}, basic={basic.Time:0.000}");
                }
                finally
                {
                    m_Driver.GetTree().PhysicsFrame -= SamplePhysics;
                }
                ReleaseTestEntity(actor);
                // GGF 在下一次 EntityManager.Update 才把隐藏实例归还对象池。
                await WaitFramesAsync(2);
                m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.LionCamelKing,
                    new EnemySpawn(new Vector2(580, 100)));
                Check(ReferenceEquals(actor, m_Monster) && m_Monster.AttackSegment == -1 &&
                    m_Monster.AiData.ElapsedSeconds < .1 && m_Monster.AiData.Cooldowns[1] == 0,
                    $"狮驼王池复用残留：same={ReferenceEquals(actor, m_Monster)}, attack={m_Monster.AttackSegment}, " +
                    $"age={m_Monster.AiData.ElapsedSeconds}, spinCd={m_Monster.AiData.Cooldowns[1]}, " +
                    $"spinInitial={attacks[1].AiInitCooldown}");
                ReleaseTestEntity(m_Monster);
                m_Monster = null;
                await WaitFramesAsync(2);
            }
            GD.Print("SMOKE-LION-AI PASS: autonomous far poison, near spin/basic cadence, independent cooldowns, pause and pool reuse.");
        }
        finally
        {
            if (GF.Base.IsGamePaused)
            {
                GF.Base.ResumeGame();
            }

            ReleaseTestEntity(m_Monster);
            m_Monster = null;
            Engine.PhysicsTicksPerSecond = originalTicks;
            hero.GlobalPosition = origin;
            heroSystem.RemoveSource(source);
            heroSystem.Heal(int.MaxValue);
        }
    }

    /// <summary>
    /// 每个方向逐招驱动真实身体 FSM，观察动画回调生成的攻击效果与伤害。
    /// </summary>
    /// <param name="hero">测试关卡英雄。</param>
    /// <param name="flow">同一流程拥有的模型与系统。</param>
    /// <returns>全部七星洞角色检查结束的任务。</returns>
    private async Task CheckQixingEnemiesAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        var heroSystem = flow.GetSystem<HeroSystem>();
        var source = new AttributeSourceId("SmokeQixing", 1);
        heroSystem.SetSource(source, [AttributeModifier.Flat(AttributeType.MaxHp, 100000)]);
        heroSystem.Heal(int.MaxValue);
        try
        {
            foreach (var (id, count) in new[] { (EntityId.CaveBrute, 1), (EntityId.KnifeDemon, 2), (EntityId.LionCamelKing, 3) })
            {
                m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(id, new EnemySpawn(new Vector2(580, 100)));
                var actor = m_Monster;
                SmokeInspection.FreezeAi(actor);
                await WaitUntilAsync(() => actor.OnFloor, $"{id} 没有落地");
                typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(actor, [hero]);
                var shape = actor.HitBox.GetNode<CollisionShape2D>("CollisionShape2D");
                foreach (int direction in new[] { -1, 1 })
                {
                    for (int index = 0; index < count; index++)
                    {
                        hero.GlobalPosition = actor.GlobalPosition + new Vector2(direction * 120, 0);
                        hero.Velocity = Vector2.Zero;
                        heroSystem.AdvanceHurtProtection(float.MaxValue);
                        int beforeHp = hero.Hp;
                        if (id == EntityId.LionCamelKing && index == 1)
                        {
                            actor.SetFacing(-direction); // 双侧旋风应保留旧朝向，即使目标在身后。
                        }

                        int oldFacing = actor.Facing;
                        Check(((IEnemyAiAgent)actor).RequestAttack(index), $"{id} 未接受第 {index + 1} 招");
                        await WaitUntilAsync(() => actor.AttackSegment == index, $"{id} 身体 FSM 没有提交招式");
                        if (id == EntityId.LionCamelKing)
                        {
                            Check(actor.Facing == (index == 1 ? oldFacing : direction), "狮驼王的招式转向与原版不同");
                        }

                        bool sawEffect = false;
                        bool captured = false;
                        double elapsed = 0;
                        while (true)
                        {
                            var effects = actor.GetParent().GetChildren().OfType<SkillEffectNode>().Where(x => x.Visible).ToArray();
                            if (actor.AttackSegment != index && effects.Length == 0)
                            {
                                break;
                            }

                            Check(elapsed < 13, $"{id}/{index + 1} 的动画或独立效果没有完成");
                            sawEffect |= effects.Length > 0;
                            var active = effects.Length > 0 ? effects[0].GetNode<CollisionShape2D>("HitBox/CollisionShape2D") : shape;
                            if (!active.Disabled)
                            {
                                Vector2 offset = hero.HurtBox.GetGlobalBounds().GetCenter() - hero.GlobalPosition;
                                hero.GlobalPosition = active.GlobalPosition - offset;
                            }
                            if (id == EntityId.LionCamelKing && index == 1 && actor.AttackSegment == index)
                            {
                                bool invulnerable = (bool)typeof(AbstractActorEntity).GetProperty("Invulnerable", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(actor);
                                Check(invulnerable, "狮驼王旋风没有全程无敌");
                            }
                            if (!captured && (effects.Length > 0 || !shape.Disabled))
                            {
                                captured = true;
                                await CaptureCombatAsync($"qixing_{id}_{index + 1}_{direction}");
                            }
                            await WaitFramesAsync(1);
                            elapsed += 1d / Engine.PhysicsTicksPerSecond;
                        }
                        Check(hero.Hp < beforeHp, $"{id}/{index + 1}/{direction} 没有造成实际伤害");
                        if (id == EntityId.KnifeDemon && index == 1 || id == EntityId.LionCamelKing && index == 2)
                        {
                            Check(sawEffect, $"{id} 的离身招式没有生成效果");
                        }

                        Check(shape.Disabled, $"{id} 收招后判定未关闭");
                        GD.Print($"SMOKE-QIXING ATTACK: {id}/{index + 1}, direction={direction}, damage={beforeHp - hero.Hp}, independent={sawEffect}");
                        if (id == EntityId.LionCamelKing && index == 2)
                        {
                            Check(flow.GetSystem<HeroStatusSystem>().IsPoisoned, "毒雾命中没有施加中毒");
                            int hp = hero.Hp;
                            hero._PhysicsProcess(.5);
                            Check(hero.Hp == hp, "中毒未满一秒即扣血");
                            GF.Base.PauseGame();
                            await WaitFramesAsync(3);
                            Check(hero.Hp == hp, "暂停期间中毒继续推进");
                            GF.Base.ResumeGame();
                            hero._PhysicsProcess(.5);
                            Check(hero.Hp == hp - 20, "中毒没有按秒扣除20固定伤害");
                            Check(hero.GetNode<AnimatedSprite2D>("m_StatusEffects/m_PoisonHead").Visible &&
                                hero.GetNode<AnimatedSprite2D>("m_StatusEffects/m_PoisonPulse").Visible, "中毒头顶或扣血效果未显示");
                            await CaptureCombatAsync($"qixing_poison_{direction}");
                            hero._PhysicsProcess(9);
                            Check(!flow.GetSystem<HeroStatusSystem>().IsPoisoned && hero.Hp == hp - 200, "中毒没有在十秒后结束");
                        }
                        await WaitFramesAsync(2);
                    }
                }
                if (id == EntityId.LionCamelKing)
                {
                    var setTarget = typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.NonPublic);
                    setTarget.Invoke(actor, [null]);
                    hero.GlobalPosition = actor.GlobalPosition + new Vector2(50, 200);
                    for (int frame = 0; frame < 6; frame++)
                    {
                        await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
                    }

                    Check(!((IEnemyAiAgent)actor).HasTarget, "狮驼王首次发现超出了官方150高度");
                    hero.GlobalPosition = actor.GlobalPosition + new Vector2(50, 100);
                    await WaitUntilAsync(() => ((IEnemyAiAgent)actor).HasTarget, "狮驼王未在官方首次发现范围内锁定目标");
                    hero.GlobalPosition = actor.GlobalPosition + new Vector2(500, 0);
                    double started = actor.AiData.ElapsedSeconds;
                    while (actor.AiData.ElapsedSeconds - started < 3.2)
                    {
                        await WaitFramesAsync(1);
                    }

                    Check(((IEnemyAiAgent)actor).HasTarget, "狮驼王错误地在三秒后丢失官方持续追踪的目标");
                    GD.Print("SMOKE-LION-AI PASS: 150px acquisition height, target retained beyond sight, spin facing preserved");
                }
                ReleaseTestEntity(actor);
                m_Monster = null;
                await WaitFramesAsync(2);
                m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(id, new EnemySpawn(new Vector2(580, 100)));
                Check(ReferenceEquals(actor, m_Monster) && actor.AttackSegment == -1 && actor.Hp == actor.MaxHp,
                    $"{id} 池复用状态残留");
                ReleaseTestEntity(m_Monster);
                m_Monster = null;
            }
            // 普通怪物进入死亡动作仍保留原版受击体；Dead 事实让后续攻击只产生 Miss，不显示伤害数字。
            m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.CaveBrute,
                new EnemySpawn(new Vector2(580, 100)));
            SmokeInspection.FreezeAi(m_Monster);
            await WaitUntilAsync(() => m_Monster.OnFloor, "死亡鞭打夹具未落地");
            var lethalData = GameLogic.Battle.Attacks.AttackData.Create(0, default, m_Monster.MaxHp * 100f,
                GameConfig.Battle.DamageKind.Real, Vector2.Zero, hero.Id, 0);
            var lethalResult = default(GameLogic.Battle.Damage.DamageResult);
            try
            {
                lethalResult = m_Monster.ReceiveHit(lethalData, hero.Id);
            }
            finally
            {
                GameFramework.ReferencePool.Release(lethalData);
            }
            Check(m_Monster.Dead, $"致死结算后死亡事实未置位：damage={lethalResult.Damage}, miss={lethalResult.IsMiss}, hp={m_Monster.Hp}/{m_Monster.MaxHp}");
            await WaitFramesAsync(2);
            Check(m_Monster.Dead && m_Monster.HurtBox.Monitorable,
                $"普通怪物死亡动作过早关闭受击体，无法保留原版鞭打表现：dead={m_Monster.Dead}, monitorable={m_Monster.HurtBox.Monitorable}, monitoring={m_Monster.HurtBox.Monitoring}, visible={m_Monster.IsShown}, damage={lethalResult.Damage}, miss={lethalResult.IsMiss}");
            ReleaseTestEntity(m_Monster);
            m_Monster = null;
            GD.Print("SMOKE-QIXING PASS: six actions in both directions, knife/cloud hits, spin invulnerability, poison, pause and reuse");
        }
        finally
        {
            if (GF.Base.IsGamePaused)
            {
                GF.Base.ResumeGame();
            }

            heroSystem.RemoveSource(source);
            heroSystem.Heal(int.MaxValue);
        }
    }
}
