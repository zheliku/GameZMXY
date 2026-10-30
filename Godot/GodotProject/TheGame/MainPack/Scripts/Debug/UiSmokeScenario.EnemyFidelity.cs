using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GameConfig.Entity;
using GameConfig.Attributes;
using GameLogic.Battle;
using GameLogic.Battle.Attributes;
using GameLogic.Battle.Damage;
using GameLogic.Battle.Attacks;
using GameLogic.Effects;
using GameLogic.Level;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Enemies;
using GameLogic.Enemies.AI;
using GameLogic.Heroes;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;

/// <summary>
/// 同版官方原点索敌边界与实际近战决策回归；观察手段只保留在开发夹具。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 真正受击并推进身体，检查首、中、末段战斗门都不阻挡敌人；关卡外墙仍属于世界层。
    /// </summary>
    /// <param name="level">已经停止刷怪的正式关卡。</param>
    /// <param name="hero">移至远处的正式英雄。</param>
    /// <returns>两种物理频率下的双向受击穿门检查。</returns>
    private async Task CheckEnemyKnockbackAsync(LevelViewController level, AbstractHeroEntity hero)
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        Vector2 originalPosition = hero.GlobalPosition;
        var gates = SmokeInspection.Scene(level).GetNode("World/StageGates").GetChildren()
            .OfType<StaticBody2D>().OrderBy(x => x.Position.X).ToArray();
        var shapes = gates.SelectMany(x => x.GetChildren().OfType<CollisionShape2D>()).ToArray();
        bool[] disabled = shapes.Select(x => x.Disabled).ToArray();
        try
        {
            hero.GlobalPosition = new Vector2(100000, 100000);
            foreach (var shape in shapes)
            {
                shape.Disabled = false;
            }

            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                foreach (var gate in new[] { gates.First(), gates[gates.Length / 2], gates.Last() })
                {
                    float x = gate.GetChild<CollisionShape2D>(0).GlobalPosition.X;
                    foreach (int direction in new[] { -1, 1 })
                    {
                        m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.DemonMonkey,
                            new EnemySpawn(new Vector2(x - direction * 50, 100)));
                        SmokeInspection.FreezeAi(m_Monster);
                        var actor = m_Monster;
                        await WaitUntilAsync(() => actor.OnFloor, "受击穿门夹具未落地");
                        var data = AttackData.Create(0, default, 1, GameConfig.Battle.DamageKind.Real,
                            new Vector2(1200, -100), direction, 0);
                        try
                        {
                            actor.ReceiveHit(data, 0);
                        }
                        finally
                        {
                            GameFramework.ReferencePool.Release(data);
                        }
                        await WaitUntilAsync(() => direction * (actor.GlobalPosition.X - x) > 25,
                            $"{gate.Name}/{ticks}Hz/{direction} 玩家战斗门阻挡了实际受击敌人", 3000);
                        ReleaseTestEntity(actor);
                        m_Monster = null;
                        await WaitFramesAsync(2);
                    }
                }
                GD.Print($"SMOKE-ENEMY KNOCKBACK {ticks}Hz PASS: actual hurt crosses first/middle/final gates in both directions");
            }
        }
        finally
        {
            for (int i = 0; i < shapes.Length; i++)
            {
                shapes[i].Disabled = disabled[i];
            }

            if (m_Monster is { IsShown: true })
            {
                ReleaseTestEntity(m_Monster);
            }

            m_Monster = null;
            hero.GlobalPosition = originalPosition;
            Engine.PhysicsTicksPerSecond = originalTicks;
        }
    }

    /// <summary>
    /// 在真实效果场景增加第二个基础形状，验证仅第二形状命中、重复接触去重、暂停、取消和复用。
    /// </summary>
    /// <param name="hero">冻结身体但保留正式受击逻辑的目标。</param>
    /// <returns>两种物理频率下的多形状生命周期验证。</returns>
    private async Task CheckEffectShapesAsync(AbstractHeroEntity hero)
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        Vector2 originalPosition = hero.GlobalPosition;
        var system = GF.Fsm.GetSystem<HeroSystem>();
        var source = new AttributeSourceId("EffectShapes", 1);
        system.SetSource(source, [AttributeModifier.Flat(AttributeType.MaxHp, 100000)]);
        system.Heal(int.MaxValue);
        try
        {
            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                var effect = GD.Load<PackedScene>("res://TheGame/Entitys/Enemies/Monsters/Ground/knife_demon/knife_demon_blade_effect.tscn").Instantiate<SkillEffectNode>();
                var area = effect.GetNode<Area2D>("HitBox");
                var primary = area.GetChild<CollisionShape2D>(0);
                var secondary = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(40, 40) }, Disabled = true };
                area.AddChild(secondary);
                hero.GetParent().AddChild(effect);
                try
                {
                    var hit = (GameConfig.Battle.AttackConfig)typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(ConfigSystem.Instance.Tables.TbAttackConfig.Get(2001), null)!;
                    typeof(GameConfig.Battle.AttackConfig).GetField("HitBudget")!.SetValue(hit, 3);
                    var attacker = new CombatantAttributes(CombatSide.Enemy, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -1);
                    int completions = 0;
                    void Released(SkillEffectNode node)
                    {
                        completions++;
                        node.OnRelease();
                    }
                    effect.OnGet();
                    effect.Show(hit, new Vector2(100000, 100000), -1, attacker, 5, false, Released);
                    GF.Base.PauseGame();
                    await WaitFramesAsync(3);
                    var player = effect.GetNode<AnimationPlayer>("AnimationPlayer");
                    double paused = player.CurrentAnimationPosition;
                    await WaitFramesAsync(6);
                    Check(player.CurrentAnimationPosition == paused, "暂停仍推进独立效果动画");
                    GF.Base.ResumeGame();
                    hero.GlobalPosition = new Vector2(400, 200);
                    system.AdvanceHurtProtection(float.MaxValue);
                    effect.GlobalPosition = hero.HurtBox.GetGlobalBounds().GetCenter();
                    primary.Disabled = true;
                    secondary.Disabled = false;
                    int beforeHp = hero.Hp;
                    await WaitUntilAsync(() => hero.Hp < beforeHp, "仅第二碰撞体没有命中");
                    int afterHp = hero.Hp;
                    await WaitFramesAsync(6);
                    Check(hero.Hp == afterHp && SmokeInspection.ReadField<int>(effect, "m_RemainingHits") == 2,
                        "多形状重复接触没有共用攻击去重与预算");
                    typeof(SkillEffectNode).GetMethod("StopAttack", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(effect, null);
                    await WaitFramesAsync(2);
                    Check(primary.Disabled && secondary.Disabled, "取消后有形状未关闭");
                    effect.OnRelease();
                    await WaitFramesAsync(2);
                    Check(!effect.Visible && !effect.IsPhysicsProcessing(), "归还后效果仍活动");
                    effect.OnGet();
                    system.AdvanceHurtProtection(float.MaxValue);
                    effect.Show(hit, hero.HurtBox.GetGlobalBounds().GetCenter(), -1, attacker, 5, false, Released);
                    primary.Disabled = true;
                    secondary.Disabled = false;
                    int reusedHp = hero.Hp;
                    await WaitUntilAsync(() => hero.Hp < reusedHp, "效果复用遗留上次命中记录");
                    await WaitUntilAsync(() => completions == 1, "效果自然结束没有通知归还");
                    await WaitFramesAsync(2);
                    Check(primary.Disabled && secondary.Disabled && !effect.Visible,
                        "自然结束后形状或显示未清理");
                    GD.Print($"SMOKE-EFFECT SHAPES {ticks}Hz PASS: second-shape hit, shared dedup/budget, pause, cancellation, reuse and completion");
                }
                finally
                {
                    if (GF.Base.IsGamePaused)
                    {
                        GF.Base.ResumeGame();
                    }

                    effect.OnRelease();
                    effect.QueueFree();
                }
                await WaitFramesAsync(2);
            }
        }
        finally
        {
            Engine.PhysicsTicksPerSecond = originalTicks;
            hero.GlobalPosition = originalPosition;
            system.RemoveSource(source);
            system.Heal(int.MaxValue);
        }
    }

    /// <summary>
    /// 对全部敌人检查原点距离、两侧边界、目标保留和复用，再验证普通近战的真实自主出招。
    /// </summary>
    /// <param name="hero">已冻结身体的正式英雄目标。</param>
    /// <returns>边界与行为回归完成的任务。</returns>
    private async Task CheckEnemyFidelityAsync(AbstractHeroEntity hero)
    {
        var heights = new Dictionary<int, float>
        {
            [1] = 80,
            [2] = 110,
            [3] = 150,
            [7] = 110,
            [8] = 450,
            [13] = 150,
            [15] = 450,
            [14] = 450,
            [16] = 150,
            [17] = 400,
            [20] = 200,
            [18] = 450,
            [4] = 150,
            [6] = 450,
            [21] = 150,
            [11] = 150,
            [9] = 400,
            [10] = 250,
            [19] = 150,
            [22] = 450,
            [23] = 400,
            [24] = 400,
            [25] = 110,
            [26] = 400,
        };
        MethodInfo accepts = typeof(AbstractEnemyEntity).GetMethod("CanSelectTarget", BindingFlags.NonPublic | BindingFlags.Instance)!;
        MethodInfo setTarget = typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.NonPublic | BindingFlags.Instance)!;
        MethodInfo updateTarget = typeof(AbstractEnemyEntity).GetMethod("UpdateTarget", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var system = GF.Fsm.GetSystem<HeroSystem>();
        var source = new AttributeSourceId("EnemyFidelity", 1);
        system.SetSource(source, [AttributeModifier.Flat(AttributeType.MaxHp, 100000)]);
        system.Heal(int.MaxValue);
        Vector2 original = hero.GlobalPosition;
        int originalTicks = Engine.PhysicsTicksPerSecond;
        try
        {
            foreach (var config in ConfigSystem.Instance.Tables.TbEnemyConfig.DataList)
            {
                hero.GlobalPosition = new Vector2(100000, 100000);
                m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(config.EntityId, new EnemySpawn(new Vector2(600, 100), ContinueEncounterAsync: (_, _, _) => Task.CompletedTask));
                var actor = m_Monster;
                SmokeInspection.FreezeAi(actor);
                actor.SetPhysicsProcess(false);
                float sightRange = SmokeInspection.ReadField<EnemyPerceptionNode>(actor, "m_Perception").DetectionBounds.End.X;
                float height = heights[(int)config.EntityId];
                foreach (int direction in new[] { -1, 1 })
                {
                    foreach (var sample in new[]
                    {
                        (Offset: new Vector2(direction * (sightRange - .1f), 0), Expected: true),
                        (Offset: new Vector2(direction * sightRange, 0), Expected: false),
                        (Offset: new Vector2(direction * (sightRange + .1f), 0), Expected: false),
                        (Offset: new Vector2(direction, height), Expected: true),
                        (Offset: new Vector2(direction, -height), Expected: true),
                        (Offset: new Vector2(direction, height + .1f), Expected: false),
                        (Offset: new Vector2(direction, -height - .1f), Expected: false),
                    })
                    {
                        hero.GlobalPosition = actor.GlobalPosition + sample.Offset;
                        Check((bool)accepts.Invoke(actor, [hero])! == sample.Expected,
                            $"{config.EntityId} 索敌原点边界不符官方：{sample.Offset}");
                    }
                    setTarget.Invoke(actor, [hero]);
                    hero.GlobalPosition = actor.GlobalPosition + new Vector2(direction * (sightRange + 500), 0);
                    updateTarget.Invoke(actor, null);
                    Check(((IEnemyAiAgent)actor).HasTarget, $"{config.EntityId} 错误放弃视野外存活目标");
                }
                ReleaseTestEntity(actor);
                m_Monster = null;
                await WaitFramesAsync(2);
                GD.Print($"SMOKE-ENEMY DETECTION PASS: {config.EntityId}, strict X/inclusive Y, both sides, retained target");
            }

            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                foreach (EntityId id in new[] { EntityId.HuaguoshanMonkey, EntityId.DemonMonkey, EntityId.Gorilla,
                             EntityId.CaveBrute, EntityId.BullGuard, EntityId.KnifeDemon })
                {
                    hero.GlobalPosition = new Vector2(100000, 100000);
                    m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(id, new EnemySpawn(new Vector2(600, 100)));
                    var actor = m_Monster;
                    var agent = (IEnemyAiAgent)actor;
                    FieldInfo desire = actor.Config.GetType().GetField("AttackDesire")!;
                    int oldDesire = actor.Config.AttackDesire;
                    desire.SetValue(actor.Config, 100);
                    float offset = heights[(int)id] + 100;
                    void FollowActor()
                    {
                        hero.GlobalPosition = actor.GlobalPosition + new Vector2(offset, 0);
                    }
                    m_Driver.GetTree().PhysicsFrame += FollowActor;
                    try
                    {
                        await WaitUntilAsync(() => actor.OnFloor, $"{id} 未落地");
                        FollowActor();
                        setTarget.Invoke(actor, [hero]);
                        await WaitUntilAsync(() => agent.AiData.MoveIntent == 1, $"{id} 没有按物种原点范围追击");
                        offset = 30;
                        var casts = new List<(double Time, int Index)>();
                        int previous = -1;
                        ulong start = Time.GetTicksMsec();
                        while (id == EntityId.KnifeDemon ? casts.Count(x => x.Index == 1) < 2 || !casts.Any(x => x.Index == 0) : casts.Count < 2)
                        {
                            Check(Time.GetTicksMsec() - start < 12000, $"{id} 没有自主完成出招");
                            if (actor.AttackSegment >= 0 && actor.AttackSegment != previous)
                            {
                                casts.Add((agent.AiData.ElapsedSeconds, actor.AttackSegment));
                            }
                            previous = actor.AttackSegment;
                            await WaitFramesAsync(1);
                        }
                        if (id == EntityId.KnifeDemon)
                        {
                            var throws = casts.Where(x => x.Index == 1).ToArray();
                            Check(throws[1].Time - throws[0].Time >= 3.95, "飞刀四秒冷却提前结束");
                            Check(throws[1].Time - throws[0].Time < 4.1, "身体延迟消费请求导致飞刀错过四秒后的决策点");
                        }
                        foreach (var cast in casts)
                        {
                            double age = (cast.Time - agent.AiData.ClockOrigin) % (242d / 24d);
                            double phase = age % actor.Config.AttackInterval;
                            Check(phase < .08 || actor.Config.AttackInterval - phase < .08,
                                $"{id} 实际出招脱离官方决策节拍：{cast.Time:F3}");
                        }
                        GD.Print($"SMOKE-ENEMY MELEE PASS: {id}/{ticks}Hz, autonomous chase/cadence/selection, casts={string.Join(',', casts.Select(x => $"{x.Index}@{x.Time:F2}"))}");
                    }
                    finally
                    {
                        m_Driver.GetTree().PhysicsFrame -= FollowActor;
                        desire.SetValue(actor.Config, oldDesire);
                    }
                    ReleaseTestEntity(actor);
                    m_Monster = null;
                    await WaitFramesAsync(2);
                }
            }
        }
        finally
        {
            if (m_Monster is { IsShown: true })
            {
                ReleaseTestEntity(m_Monster);
            }

            m_Monster = null;
            hero.GlobalPosition = original;
            Engine.PhysicsTicksPerSecond = originalTicks;
            system.RemoveSource(source);
            system.Heal(int.MaxValue);
        }
        GD.Print("SMOKE-ENEMY FIDELITY PASS: all 24 acquisition boundaries and six autonomous melee species at 60/120Hz");
    }
}
