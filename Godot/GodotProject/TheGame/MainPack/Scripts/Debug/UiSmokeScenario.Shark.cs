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
using GameLogic.Heroes;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 鲨之涡流的真实招式、低生命动作、召唤所有权和警示回调验证。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 通过正式AI状态检查召唤鲨鱼的追踪、折返节流和近身决策。
    /// </summary>
    /// <param name="hero">只改变位置的真实目标。</param>
    /// <returns>两个物理频率下的自主行为检查。</returns>
    private async Task CheckSharkAiAsync(AbstractHeroEntity hero)
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        try
        {
            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                hero.GlobalPosition = new Vector2(100000, 100000);
                m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EEntityId.SharkSpawn,
                    new EnemySpawn(new Vector2(580, 250)));
                var shark = (SharkSpawnEntity)m_Monster;
                var agent = (IEnemyAiAgent)shark;
                Vector2 offset = new(300, 200);
                void FollowActor()
                {
                    hero.GlobalPosition = shark.GlobalPosition + offset;
                }
                m_Driver.GetTree().PhysicsFrame += FollowActor;
                try
                {
                    FollowActor();
                    typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(shark, [hero]);
                    await WaitUntilAsync(() => agent.AiData.MoveIntent == 1 && shark.Velocity.Y > 0,
                        "召唤鲨鱼没有朝目标上方追踪");
                    offset = new Vector2(-300, 200);
                    await WaitFramesAsync(4);
                    Check(agent.AiData.MoveIntent == 1, "鲨鱼转向没有固定节流间隔");
                    await WaitUntilAsync(() => agent.AiData.MoveIntent == -1,
                        "鲨鱼折返间隔结束后仍未转向", 4000);
                    Check(Math.Abs(shark.Velocity.Y) <= 96.01f, "鲨鱼超过官方垂直速度上限");
                    offset = new Vector2(120, 0);
                    await WaitUntilAsync(() => shark.AttackSegment >= 0, "鲨鱼近身没有自主选招", 5000);
                    int index = shark.AttackSegment;
                    Check(index is 0 or 1, "鲨鱼近身选择了不存在的招式");
                    if (index == 1)
                    {
                        Check(agent.AiData.Cooldowns[1] > 3.8f &&
                                                SmokeInspection.ReadField<float>(shark, "m_InvulnerabilityLeft") > 1.1f,
                                                "旋风没有独立四秒冷却和30源帧无敌");
                    }
                    else
                    {
                        Check(agent.AiData.Cooldowns[1] == 0f, "普通撞击消耗了旋风冷却");
                    }

                    GD.Print($"SMOKE-SHARK AI 3-3 {ticks}Hz PASS: tracking, fixed turn interval, source vertical cap and autonomous attack {index}");
                }
                finally
                {
                    m_Driver.GetTree().PhysicsFrame -= FollowActor;
                }
                ReleaseTestEntity(shark);
                m_Monster = null;
                await WaitFramesAsync(3);
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
        }
    }

    /// <summary>
    /// 两个物理频率、双朝向运行首领高低阶段及召唤物全部招式。
    /// </summary>
    /// <param name="hero">实际英雄受击沙包。</param>
    /// <param name="flow">本局共享模型和系统。</param>
    /// <returns>原生攻击与效果清理完成的任务。</returns>
    private async Task CheckSharkEnemiesAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        var system = flow.GetSystem<HeroSystem>();
        var source = new AttributeSourceId("SharkSmoke", 1);
        system.SetSource(source, [AttributeModifier.Flat(EAttributeType.MaxHp, 100000)]);
        system.Heal(int.MaxValue);
        try
        {
            if (OS.GetCmdlineUserArgs().Contains("--shark-lifecycle-only"))
            {
                await CheckSharkLifecyclesAsync(hero);
                return;
            }
            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                foreach (var (id, low, count) in new[]
                {
                    (EEntityId.SharkDemonKing, false, 3),
                    (EEntityId.SharkDemonKing, true, 3),
                    (EEntityId.SharkSpawn, false, 2),
                })
                {
                    foreach (int direction in new[] { -1, 1 })
                    {
                        for (int index = 0; index < count; index++)
                        {
                            hero.GlobalPosition = new Vector2(100000, 100000);
                            m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(id,
                                new EnemySpawn(new Vector2(580, id == EEntityId.SharkSpawn ? 300 : 100)));
                            var actor = m_Monster;
                            SmokeInspection.FreezeAi(actor);
                            if (id == EEntityId.SharkDemonKing)
                            {
                                await WaitUntilAsync(() => actor.OnFloor, "鲨魔王夹具没有落地");
                            }

                            if (low)
                            {
                                ApplyEastSeaHit(actor, hero.Id, 18000, -direction, false, Vector2.Zero);
                                await WaitUntilAsync(() => !((IEnemyAiAgent)actor).IsCcLocked &&
                                    !((GameLogic.Enemies.Body.IEnemyBody)actor).HasPendingHurt,
                                    "鲨魔王低生命受击没有结束");
                                await WaitUntilAsync(() => GF.Entity.GetAllLoadedEntities().OfType<SharkSpawnEntity>()
                                    .Any(child => child.IsShown), "低生命没有召唤鲨鱼");
                                var children = GF.Entity.GetAllLoadedEntities().OfType<SharkSpawnEntity>()
                                    .Where(child => child.IsShown).ToArray();
                                Check(children.Length == 1, "低生命重复召唤鲨鱼");
                                foreach (var child in children)
                                {
                                    SmokeInspection.FreezeAi(child);
                                    child.GlobalPosition = new Vector2(100000, 100000);
                                }
                            }

                            hero.GlobalPosition = actor.GlobalPosition + new Vector2(direction * 120, 0);
                            typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.NonPublic | BindingFlags.Instance)
                                .Invoke(actor, [hero]);
                            Check(((IEnemyAiAgent)actor).RequestAttack(index), "鲨鱼拒绝正式出招：" + id);
                            await WaitUntilAsync(() => actor.AttackSegment == index, "鲨鱼身体未消费请求");
                            string expectedAnimation = "attack_" + (index + 1) + (low ? "_low" : string.Empty);
                            Check(actor.AnimPlayer.CurrentAnimation == expectedAnimation, "鲨魔王没有选择对应生命阶段动作");
                            flow.GetSystem<HeroSystem>().AdvanceHurtProtection(float.MaxValue);
                            int hp = hero.Hp;
                            bool sawEffect = false;
                            bool sawWarning = false;
                            bool captured = false;
                            ulong started = Time.GetTicksMsec();
                            while (actor.AttackSegment >= 0 || actor.GetParent().GetChildren().OfType<SkillEffectNode>().Any(effect => effect.Visible))
                            {
                                Check(Time.GetTicksMsec() - started < 8000, "鲨鱼招式或警示后续没有结束");
                                var effects = actor.GetParent().GetChildren().OfType<SkillEffectNode>().Where(effect => effect.Visible).ToArray();
                                sawEffect |= effects.Length > 0;
                                sawWarning |= effects.Any(effect => effect.Name.ToString().Contains("warning"));
                                var shape = effects.Select(effect => effect.GetNode<CollisionShape2D>("Origin/Visual/HitBox/CollisionShape2D"))
                                    .FirstOrDefault(value => !value.Disabled) ?? actor.HitBox.GetNode<CollisionShape2D>("CollisionShape2D");
                                if (!shape.Disabled)
                                {
                                    hero.GlobalPosition = shape.GlobalPosition -
                                        (hero.HurtBox.GetGlobalBounds().GetCenter() - hero.GlobalPosition);
                                    if (!captured && ticks == 60 && direction == 1)
                                    {
                                        captured = true;
                                        await CaptureCombatAsync($"shark_{id}_{index}_{low}");
                                    }
                                }
                                await WaitFramesAsync(1);
                            }
                            Check(hero.Hp < hp, $"鲨鱼招式没有真实命中：{id}/{index}/{low}/{direction}/{ticks}");
                            Check(sawEffect == (id == EEntityId.SharkSpawn || index > 0), "鲨鱼独立效果缺失或出现在错误招式");
                            Check(sawWarning == (id == EEntityId.SharkSpawn ? index == 1 : index == 2), "鲨鱼警示缺失或使用错误招式");
                            Check(actor.GetNode<Node2D>("m_Body").Visible && actor.HitBox.GetChildren().OfType<CollisionShape2D>().All(shape => shape.Disabled),
                                "鲨鱼收招没有恢复身体或关闭攻击判定");
                            await WaitUntilAsync(() => !((IEnemyAiAgent)actor).IsAttacking, "鲨鱼收招仍忙碌");
                            ReleaseTestEntity(actor);
                            m_Monster = null;
                            await WaitFramesAsync(3);
                            Check(!GF.Entity.GetAllLoadedEntities().OfType<SharkSpawnEntity>().Any(child => child.IsShown),
                                "首领离场仍遗留召唤鲨鱼");
                            Check(!actor.GetParent().GetChildren().OfType<SkillEffectNode>().Any(effect => effect.Visible),
                                "鲨鱼离场仍遗留攻击或警示");
                            GD.Print($"SMOKE-SHARK ATTACK 3-3 {ticks}Hz {id}/{index}/{low}/{direction} PASS: damage={hp - hero.Hp}");
                        }
                    }
                }
            }
            await CheckSharkLifecyclesAsync(hero);
            GD.Print("SMOKE-SHARK 3-3 PASS: two frequencies, both directions, eight stage/attack choices, warning returns and summon cleanup");
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
    /// 检查原生无敌窗口、警示暂停、首领死亡与隐藏期间回池重用。
    /// </summary>
    /// <param name="hero">真实目标，测试期间不运行自身身体状态机。</param>
    /// <returns>实体和所有独立效果清理完成的任务。</returns>
    private async Task CheckSharkLifecyclesAsync(AbstractHeroEntity hero)
    {
        hero.GlobalPosition = new Vector2(100000, 100000);
        m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EEntityId.SharkDemonKing,
            new EnemySpawn(new Vector2(580, 100)));
        var king = m_Monster;
        SmokeInspection.FreezeAi(king);
        await WaitUntilAsync(() => king.OnFloor, "鲨魔王生命周期夹具没有落地");
        hero.GlobalPosition = king.GlobalPosition + new Vector2(200, 0);
        typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(king, [hero]);
        Check(((IEnemyAiAgent)king).RequestAttack(1), "鲨魔王拒绝鲨波无敌夹具");
        await WaitUntilAsync(() => king.AttackSegment == 1, "鲨魔王没有起手鲨波");
        float invulnerability = SmokeInspection.ReadField<float>(king, "m_InvulnerabilityLeft");
        Check(invulnerability > .79f && invulnerability <= 20f / 24f &&
            ApplyEastSeaHit(king, hero.Id, 100, -king.Facing, false, Vector2.Zero) == 0,
            "鲨波没有20源帧无敌");
        GF.Base.PauseGame();
        await WaitFramesAsync(3);
        float paused = SmokeInspection.ReadField<float>(king, "m_InvulnerabilityLeft");
        await WaitFramesAsync(8);
        Check(SmokeInspection.ReadField<float>(king, "m_InvulnerabilityLeft") == paused, "暂停推进鲨波无敌");
        GF.Base.ResumeGame();
        await WaitUntilAsync(() => !((IEnemyAiAgent)king).IsAttacking, "鲨波生命周期夹具未收招");
        ApplyEastSeaHit(king, hero.Id, 18000, -king.Facing, false, Vector2.Zero);
        await WaitUntilAsync(() => GF.Entity.GetAllLoadedEntities().OfType<SharkSpawnEntity>().Any(value => value.IsShown),
            "低生命生命周期夹具没有召唤");
        var child = GF.Entity.GetAllLoadedEntities().OfType<SharkSpawnEntity>().Single(value => value.IsShown);
        SmokeInspection.FreezeAi(child);
        hero.GlobalPosition = new Vector2(100000, 100000);
        ApplyEastSeaHit(king, hero.Id, 1000000, -king.Facing, false, Vector2.Zero);
        await WaitFramesAsync(3);
        Check(king.Dead && king.IsShown && !king.HurtBox.Monitorable, "首领死亡没有保留演出或关闭受击体");
        await WaitUntilAsync(() => !king.IsShown, "鲨魔王死亡旋涡完成后没有回池");
        Check(!child.IsShown, "首领死亡后召唤鲨鱼没有回池");
        m_Monster = null;
        await WaitFramesAsync(3);
        var revived = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EEntityId.SharkDemonKing,
            new EnemySpawn(new Vector2(580, 100)));
        Check(ReferenceEquals(king, revived) && revived.GetNode<Node2D>("m_Body").Visible &&
            !revived.Dead && revived.Hp == revived.MaxHp,
            "鲨魔王死亡后复用残留隐藏身体或死亡事实");
        ReleaseTestEntity(revived);
        await WaitFramesAsync(3);

        m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EEntityId.SharkSpawn,
            new EnemySpawn(new Vector2(580, 250)));
        var shark = m_Monster;
        SmokeInspection.FreezeAi(shark);
        hero.GlobalPosition = new Vector2(620, 450);
        typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(shark, [hero]);
        Check(((IEnemyAiAgent)shark).RequestAttack(1), "鲨鱼拒绝旋风生命周期夹具");
        await WaitUntilAsync(() => !shark.GetNode<Node2D>("m_Body").Visible, "旋风第49帧没有隐藏身体");
        Check(!shark.HurtBox.Monitorable, "旋风隐藏期受击体仍有效");
        GF.Base.PauseGame();
        await WaitFramesAsync(3);
        var warning = shark.GetParent().GetChildren().OfType<SkillEffectNode>().Single(effect => effect.Visible);
        double position = warning.GetNode<AnimationPlayer>("AnimationPlayer").CurrentAnimationPosition;
        await WaitFramesAsync(8);
        Check(warning.Visible && warning.GetNode<AnimationPlayer>("AnimationPlayer").CurrentAnimationPosition == position,
            "暂停推进旋风警示");
        GF.Base.ResumeGame();
        ReleaseTestEntity(shark);
        m_Monster = null;
        await WaitFramesAsync(3);
        Check(!warning.Visible, "隐藏鲨鱼没有归还其警示");
        var reused = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EEntityId.SharkSpawn,
            new EnemySpawn(new Vector2(580, 250)));
        Check(ReferenceEquals(shark, reused) && reused.GetNode<Node2D>("m_Body").Visible &&
            reused.HurtBox.Monitorable && reused.AttackSegment == -1,
            "隐藏鲨鱼复用残留身体、受击或攻击状态");
        ReleaseTestEntity(reused);
        GD.Print("SMOKE-SHARK LIFECYCLE 3-3 PASS: source invulnerability, pause, death/summon cleanup, hidden warning cancellation and reuse");
    }
}
