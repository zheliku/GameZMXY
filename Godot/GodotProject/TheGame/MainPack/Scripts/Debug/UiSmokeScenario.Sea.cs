using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GameConfig.Attributes;
using GameConfig.Entity;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attributes;
using GameLogic.Enemies;
using GameLogic.Enemies.AI;
using GameLogic.Enemies.Body;
using GameLogic.Heroes;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GameFramework.Event;
using GameLogic.Event.Combat;
using GameLogic.Level;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 海域三类怪物的真实GGF装配、出招、命中、弹体击散与回池验证。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 不冻结AI、只控制目标站位，核验海怪各自的追退、选招和冷却。
    /// </summary>
    /// <param name="hero">保留正式受击规则的英雄沙包。</param>
    /// <param name="flow">本局规则作用域。</param>
    /// <returns>两种物理频率下的自主行为回归。</returns>
    private async Task CheckSeaAiAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        var system = flow.GetSystem<HeroSystem>();
        var source = new AttributeSourceId("SeaAiSmoke", 1);
        system.SetSource(source, [AttributeModifier.Flat(AttributeType.MaxHp, 100000)]);
        system.Heal(int.MaxValue);
        try
        {
            foreach (int ticks in new[] { 60, 120 })
            {
                foreach (EntityId id in new[] { EntityId.ShrimpDemon, EntityId.TideDemon, EntityId.TurtleMinister })
                {
                    Engine.PhysicsTicksPerSecond = ticks;
                    hero.GlobalPosition = new Vector2(100000, 100000);
                    m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(id, new EnemySpawn(new Vector2(580, 100)));
                    var actor = m_Monster;
                    var agent = (IEnemyAiAgent)actor;
                    await WaitUntilAsync(() => actor.OnFloor, "海怪AI夹具未落地");
                    GD.Seed(3147);
                    float distance = id == EntityId.ShrimpDemon ? 120f : id == EntityId.TurtleMinister ? 500f : 240f;
                    float height = 0f;
                    void FollowActor()
                    {
                        hero.GlobalPosition = actor.GlobalPosition + new Vector2(distance, height);
                    }
                    m_Driver.GetTree().PhysicsFrame += FollowActor;
                    try
                    {
                        FollowActor();
                        typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(actor, [hero]);
                        await WaitUntilAsync(() => agent.AiData.MoveIntent == (id == EntityId.ShrimpDemon ? -1 : 1),
                            "海怪没有自主退避或追击：" + id);
                        if (id == EntityId.ShrimpDemon)
                        {
                            distance = 400f;
                            await WaitUntilAsync(() => agent.AiData.MoveIntent == 1, "虾兵远距离没有追击");
                        }
                        distance = id == EntityId.TideDemon ? 120f : 300f;
                        var casts = new System.Collections.Generic.List<(double Time, int Index)>();
                        int previous = -1;
                        ulong started = Time.GetTicksMsec();
                        while (id == EntityId.TideDemon ? casts.Count(value => value.Index == 1) < 2 || !casts.Any(value => value.Index == 0) : casts.Count < 2)
                        {
                            Check(Time.GetTicksMsec() - started < 22000, "海怪未自主完成选招：" + id);
                            if (actor.AttackSegment >= 0 && previous != actor.AttackSegment)
                            {
                                casts.Add((agent.AiData.ElapsedSeconds, actor.AttackSegment));
                            }

                            previous = actor.AttackSegment;
                            await WaitFramesAsync(1);
                        }
                        for (int index = 1; index < casts.Count; index++)
                        {
                            Check(casts[index].Time - casts[index - 1].Time >= actor.Config.AttackInterval - 2f / ticks,
                                                    "海怪决策间隔快于官方节拍：" + id);
                        }

                        if (id == EntityId.TideDemon)
                        {
                            var heavy = casts.Where(value => value.Index == 1).ToArray();
                            Check(heavy[1].Time - heavy[0].Time >= 10f - 2f / ticks, "潮汐妖十秒重击冷却提前结束");
                        }
                        if (id != EntityId.ShrimpDemon)
                        {
                            await WaitUntilAsync(() => !agent.IsAttacking, "高处目标夹具未收招");
                            height = id == EntityId.TideDemon ? -350f : -850f;
                            FollowActor();
                            for (int frame = 0; frame < ticks; frame++)
                            {
                                await WaitFramesAsync(1);
                                Check(actor.AttackSegment < 0, "海怪仍攻击高度不可达的目标：" + id);
                            }
                        }
                        GF.Base.PauseGame();
                        await WaitFramesAsync(3);
                        double paused = agent.AiData.ElapsedSeconds;
                        await WaitFramesAsync(10);
                        Check(agent.AiData.ElapsedSeconds == paused, "暂停仍推进海怪决策时钟");
                        GF.Base.ResumeGame();
                        GD.Print($"SMOKE-SEA AI 3-1 {id} {ticks}Hz PASS: native pursuit/retreat, attacks, cadence, cooldown and pause");
                    }
                    finally
                    {
                        m_Driver.GetTree().PhysicsFrame -= FollowActor;
                        if (GF.Base.IsGamePaused)
                        {
                            GF.Base.ResumeGame();
                        }

                        ReleaseTestEntity(actor);
                        m_Monster = null;
                    }
                    await WaitFramesAsync(3);
                }
            }
        }
        finally
        {
            Engine.PhysicsTicksPerSecond = originalTicks;
            system.RemoveSource(source);
            system.Heal(int.MaxValue);
        }
    }

    /// <summary>
    /// 同一龟丞相身份在守关与普通海域间复用，真实死亡事件必须使用本次实例奖励。
    /// </summary>
    /// <param name="hero">提供真实攻击者身份的本局英雄。</param>
    /// <param name="flow">本局作用域，不修改正式存档。</param>
    /// <returns>死亡通知和池复用验证。</returns>
    private async Task CheckSeaRewardsAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        var model = flow.GetModel<LevelModel>();
        int originalLevel = model.LevelId;
        try
        {
            foreach (int level in new[] { 7, 8, 7, 8 })
            {
                model.LevelId = level;
                hero.GlobalPosition = new Vector2(100000, 100000);
                m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.TurtleMinister,
                    new EnemySpawn(new Vector2(580, 100)));
                var actor = m_Monster;
                SmokeInspection.FreezeAi(actor);
                await WaitUntilAsync(() => actor.OnFloor, "奖励夹具龟丞相未落地");
                int notifications = 0;
                void Observe(object sender, GameEventArgs args)
                {
                    if (args is not EnemyDiedEventArgs data || data.EntityId != actor.Id)
                    {
                        return;
                    }

                    notifications++;
                    int soulPerOrb = data.SoulPerOrbOverride ?? data.Config.SoulPerOrb;
                    Check(level == 7
                        ? data.SoulPerOrbOverride == 60 && data.EquipmentDropChanceOverride == 0f
                        : data.SoulPerOrbOverride == null && data.EquipmentDropChanceOverride == null,
                        "普通敌人不应复制基础值到Override，守关的0概率必须作为明确覆盖保留");
                    Check(soulPerOrb == (level == 7 ? 60 : 30) &&
                        (data.EquipmentDropChanceOverride ?? data.Config.EquipmentDropChance) == (level == 7 ? 0f : .15f) &&
                        data.ExperienceReward == (level == 7 ? 200 : 75), "龟丞相死亡奖励没有使用本次关卡变体");
                    var model = flow.GetModel<HeroModel>();
                    var loot = flow.GetSystem<GameLogic.Drops.LootSystem>().Roll(data.Config, soulPerOrb,
                        model.MaxHp.Value, model.MaxMp.Value, () => 0f);
                    Check(loot.Where(value => value.Kind == GameLogic.Drops.LootKind.Soul).All(value => value.Amount == soulPerOrb),
                        "灵魂球没有使用实例奖励");
                }
                GF.Event.Subscribe(EnemyDiedEventArgs.EventId, Observe);
                try
                {
                    Check(actor.MaxHp == (level == 7 ? 4800 : 1600), "龟丞相池复用未重置守关生命");
                    ApplyGarudaTestHit(actor, hero.Id);
                    await WaitUntilAsync(() => notifications > 0, "龟丞相没有广播死亡奖励");
                    ApplyGarudaTestHit(actor, hero.Id);
                    await WaitFramesAsync(3);
                    Check(notifications == 1 && actor.Config.SoulPerOrb == 30 && actor.Config.EquipmentDropChance == .15f,
                        "死亡奖励重复或污染了共享配置");
                }
                finally
                {
                    GF.Event.Unsubscribe(EnemyDiedEventArgs.EventId, Observe);
                    ReleaseTestEntity(actor);
                    m_Monster = null;
                }
                await WaitFramesAsync(3);
            }
        }
        finally
        {
            model.LevelId = originalLevel;
        }
        GD.Print("SMOKE-SEA REWARDS 3-1 PASS: guardian/base lifecycle, soul, equipment chance and single death notification");
    }

    /// <summary>
    /// 用真实玩家攻击层触发弹体消散，检查暂停和池复用，不直接调用取消回调。
    /// </summary>
    /// <param name="hero">本局英雄，暂放在弹体飞行范围外。</param>
    /// <returns>双向弹体取消与复用验证。</returns>
    private async Task CheckShrimpProjectileCancellationAsync(AbstractHeroEntity hero)
    {
        foreach (int direction in new[] { -1, 1 })
        {
            hero.GlobalPosition = new Vector2(100000, 100000);
            m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.ShrimpDemon,
                new EnemySpawn(new Vector2(580, 100)));
            var actor = m_Monster;
            SmokeInspection.FreezeAi(actor);
            await WaitUntilAsync(() => actor.OnFloor, "虾兵弹体取消夹具未落地");
            actor.SetFacing(direction);
            ((IEnemyBody)actor).BeginAttack(0);
            actor.SetFacing(direction);
            actor.SetPhysicsProcess(false);
            actor.AnimPlayer.CallbackModeMethod = AnimationMixer.AnimationCallbackModeMethod.Immediate;
            actor.RestartAnim("attack_1");
            actor.AnimPlayer.Advance(2d / 24d);
            var effect = actor.GetParent().GetChildren().OfType<ShrimpProjectileEffectNode>().Single(x => x.Visible);
            float start = effect.GlobalPosition.X;
            await WaitFramesAsync(4);
            Check(Math.Sign(effect.GlobalPosition.X - start) == direction, "虾兵弹体方向错误");
            var attack = new Area2D { CollisionLayer = 16, CollisionMask = 0, Monitoring = false };
            attack.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(100, 100) } });
            actor.GetParent().AddChild(attack);
            attack.GlobalPosition = effect.GlobalPosition;
            try
            {
                await WaitUntilAsync(() => SmokeInspection.ReadField<float>(effect, "m_FadeLeft") > 0f,
                    "玩家攻击没有击散虾兵魔法弹");
                await WaitFramesAsync(2);
                Check(effect.GetNode<CollisionShape2D>("HitBox/CollisionShape2D").Disabled,
                    "击散后弹体仍有伤害判定");
                Vector2 position = effect.GlobalPosition;
                GF.Base.PauseGame();
                await WaitFramesAsync(3);
                float alpha = effect.Modulate.A;
                await WaitFramesAsync(8);
                Check(effect.Visible && effect.GlobalPosition == position && effect.Modulate.A == alpha,
                    "暂停仍在推进魔法弹消散");
                GF.Base.ResumeGame();
                await WaitUntilAsync(() => !effect.Visible, "魔法弹一秒消散后没有回池");
            }
            finally
            {
                if (GF.Base.IsGamePaused)
                {
                    GF.Base.ResumeGame();
                }

                attack.QueueFree();
            }
            actor.RestartAnim("attack_1");
            actor.AnimPlayer.Advance(2d / 24d);
            var reused = actor.GetParent().GetChildren().OfType<ShrimpProjectileEffectNode>().Single(x => x.Visible);
            Check(ReferenceEquals(effect, reused) && reused.Modulate.A == 1f &&
                SmokeInspection.ReadField<float>(reused, "m_FadeLeft") == 0f, "弹体池复用残留消散状态");
            ReleaseTestEntity(actor);
            m_Monster = null;
            await WaitFramesAsync(3);
            Check(!reused.Visible, "敌人离场没有归还其投射物");
        }
        GD.Print("SMOKE-SEA PROJECTILE 3-1 PASS: native attack collision, fade, pause, both directions and reuse");
    }

    /// <summary>
    /// 通过正式实体和身体状态运行两种频率，不为验证增加生产观测接口。
    /// </summary>
    /// <param name="hero">当前关卡的真实英雄沙包。</param>
    /// <param name="flow">本局模型和系统的拥有者。</param>
    /// <returns>完整的异步战斗验证。</returns>
    private async Task CheckSeaEnemiesAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        var system = flow.GetSystem<HeroSystem>();
        var source = new AttributeSourceId("SeaSmoke", 1);
        system.SetSource(source, [AttributeModifier.Flat(AttributeType.MaxHp, 100000)]);
        system.Heal(int.MaxValue);
        try
        {
            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                foreach (EntityId id in new[] { EntityId.ShrimpDemon, EntityId.TideDemon, EntityId.TurtleMinister })
                {
                    int count = id == EntityId.TideDemon ? 2 : 1;
                    for (int direction = -1; direction <= 1; direction += 2)
                    {
                        for (int index = 0; index < count; index++)
                        {
                            hero.GlobalPosition = new Vector2(100000, 100000);
                            m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(id,
                                new EnemySpawn(new Vector2(580, 100)));
                            var actor = m_Monster;
                            var body = (IEnemyBody)actor;
                            await WaitUntilAsync(() => actor.OnFloor, "海怪未落地：" + id);
                            SmokeInspection.FreezeAi(actor);
                            hero.GlobalPosition = actor.GlobalPosition + new Vector2(direction * 120f, 0);
                            typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.NonPublic | BindingFlags.Instance)
                                .Invoke(actor, [hero]);
                            // 本夹具固定英雄位置并冻结身体物理；上一招留下的保护时间不会自行流逝。
                            // 每招都使用未受保护的沙包，保护持续时间另有真实物理专项验证。
                            double protection = flow.GetModel<HeroModel>().HurtInvulnerabilitySeconds;
                            system.AdvanceHurtProtection(float.MaxValue);
                            GD.Print($"SMOKE-SEA TARGET: {id}/{index}/{direction}/{ticks}, previousProtection={protection:F3}");
                            int before = hero.Hp;
                            Check(((IEnemyAiAgent)actor).RequestAttack(index), "海怪拒绝正式出招：" + id);
                            await WaitUntilAsync(() => actor.AttackSegment == index, "海怪身体未消费请求：" + id);
                            bool captured = false;
                            ulong started = Time.GetTicksMsec();
                            while (actor.AttackSegment >= 0 || actor.GetParent().GetChildren().OfType<ShrimpProjectileEffectNode>().Any(x => x.Visible))
                            {
                                Check(Time.GetTicksMsec() - started < 6000, "海怪出招或弹体未结束：" + id);
                                var projectile = actor.GetParent().GetChildren().OfType<ShrimpProjectileEffectNode>().FirstOrDefault(x => x.Visible);
                                var shape = projectile?.GetNode<CollisionShape2D>("HitBox/CollisionShape2D") ??
                                    actor.HitBox.GetNode<CollisionShape2D>("CollisionShape2D");
                                if (!shape.Disabled)
                                {
                                    Vector2 offset = hero.HurtBox.GetGlobalBounds().GetCenter() - hero.GlobalPosition;
                                    hero.GlobalPosition = shape.GlobalPosition - offset;
                                    if (!captured)
                                    {
                                        captured = true;
                                        await CaptureCombatAsync($"sea_{ticks}_{id}_{index}_{direction}");
                                    }
                                }
                                await WaitFramesAsync(1);
                            }
                            Check(hero.Hp < before, $"海怪招式未命中：{id}/{index}/{direction}/{ticks}");
                            Check(actor.HitBox.GetChildren().OfType<CollisionShape2D>().All(x => x.Disabled), "海怪收招判定未关闭");
                            await WaitUntilAsync(() => !((IEnemyAiAgent)actor).IsAttacking, "海怪收招仍忙碌");
                            ReleaseTestEntity(actor);
                            m_Monster = null;
                            await WaitFramesAsync(3);
                        }
                    }
                }
                await CheckShrimpProjectileCancellationAsync(hero);
                GD.Print($"SMOKE-SEA 3-1 {ticks}Hz PASS: three real entities, four attacks, both directions and pool cleanup");
            }
        }
        finally
        {
            if (m_Monster != null)
            {
                ReleaseTestEntity(m_Monster);
                m_Monster = null;
            }
            Engine.PhysicsTicksPerSecond = originalTicks;
            system.RemoveSource(source);
            system.Heal(int.MaxValue);
        }
    }
}
