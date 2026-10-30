using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GameConfig.Attributes;
using GameConfig.Entity;
using GameConfig.Equipment;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attributes;
using GameLogic.Enemies;
using GameLogic.Enemies.AI;
using GameLogic.Enemies.Body;
using GameLogic.Heroes;
using GameLogic.Heroes.Body;
using GameLogic.Equipment;
using GameLogic.Profile;
using GameLogic.UI;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 牛魔谷真实实体的原生招式、吞噬、心脏显隐和池清理专项。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 逐招运行双朝向、两种物理频率，不以资源加载代替实机伤害证据。
    /// </summary>
    /// <param name="hero">本局真实英雄。</param>
    /// <param name="flow">本局权威模型与系统的作用域。</param>
    /// <returns>完整独立首领动作检查。</returns>
    private async Task CheckBullEnemiesAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        var system = flow.GetSystem<HeroSystem>();
        var source = new AttributeSourceId("BullSmoke", 1);
        system.SetSource(source, [AttributeModifier.Flat(AttributeType.MaxHp, 100000)]);
        system.Heal(int.MaxValue);
        try
        {
            await CheckBullChargeAndGuardAsync(hero);
            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                foreach (int direction in new[] { -1, 1 })
                {
                    foreach (int index in Enumerable.Range(0, 5))
                    {
                        hero.GlobalPosition = new Vector2(100000, 100000);
                        m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.BullDemonKing,
                            new EnemySpawn(new Vector2(1350, 100), ContinueEncounterAsync: (_, _, _) => Task.CompletedTask));
                        var king = m_Monster;
                        SmokeInspection.FreezeAi(king);
                        await WaitUntilAsync(() => king.OnFloor, "牛魔王人形未落地");
                        hero.GlobalPosition = king.GlobalPosition + new Vector2(direction * 70, 0);
                        typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(king, [hero]);
                        flow.GetSystem<HeroSystem>().AdvanceHurtProtection(float.MaxValue);
                        int hp = hero.Hp;
                        Check(((IEnemyAiAgent)king).RequestAttack(index), "牛魔王拒绝原生招式" + index);
                        await WaitUntilAsync(() => king.AttackSegment == index, "牛魔王身体没有消费招式");
                        bool captured = false;
                        bool capturedState = false;
                        int pulses = 0;
                        int previousHp = hp;
                        ulong started = Time.GetTicksMsec();
                        while (((IEnemyAiAgent)king).IsAttacking)
                        {
                            Check(Time.GetTicksMsec() - started < 9000, "牛魔王招式没有收招" + index);
                            var shape = king.HitBox.GetNode<CollisionShape2D>("CollisionShape2D");
                            if (index < 4 && !shape.Disabled)
                            {
                                Vector2 center = hero.HurtBox.GetGlobalBounds().GetCenter() - hero.GlobalPosition;
                                hero.GlobalPosition = shape.GlobalPosition - center;
                            }
                            if (index == 4 && hero.ActionData.Captured)
                            {
                                if (!captured)
                                {
                                    hero.SetPhysicsProcess(true);
                                }

                                captured = true;
                                capturedState |= SmokeInspection.ReadField<IFsm<IHeroBody>>(hero, "m_BodyFsm").CurrentState is HeroCapturedState;
                                Check(!hero.Visible, "吞噬没有隐藏英雄");
                            }
                            if (hero.Hp != previousHp)
                            {
                                pulses++;
                                previousHp = hero.Hp;
                            }
                            await WaitFramesAsync(1);
                        }
                        Check(hero.Hp < hp, $"牛魔王招式未命中：{index}/{direction}/{ticks}");
                        if (index == 4)
                        {
                            Check(captured && capturedState && pulses == 5 && hp - hero.Hp == 250 && hero.Visible && !hero.ActionData.Captured,
                                $"吞噬五次伤害、释放或显示错误：pulses={pulses} damage={hp - hero.Hp}");
                        }
                        hero.SetPhysicsProcess(false);
                        Check(king.HitBox.GetChildren().OfType<CollisionShape2D>().All(shape => shape.Disabled), "牛魔王收招残留判定");
                        await CaptureCombatAsync($"bull_king_{ticks}_{index}_{direction}");
                        ReleaseTestEntity(king);
                        m_Monster = null;
                        await WaitFramesAsync(3);
                    }
                }

                GD.Print($"SMOKE-BULL ATTACKS 4-1 {ticks}Hz PASS: five native attacks, both directions, five drain pulses and release");
            }
            await CheckBullHeartAsync(hero);
            await CheckBullTreasureRewardAsync(hero, flow);
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
            hero.SetPhysicsProcess(false);
            hero.Visible = true;
            Engine.PhysicsTicksPerSecond = originalTicks;
            system.RemoveSource(source);
            system.Heal(int.MaxValue);
        }
    }

    /// <summary>
    /// 保留自主冲撞检查边缘停步、反向与无敌，再执行护卫真实近身出招。
    /// </summary>
    /// <param name="hero">真实受击与攻击来源。</param>
    /// <returns>兽形和护卫规则均验证完成。</returns>
    private async Task CheckBullChargeAndGuardAsync(AbstractHeroEntity hero)
    {
        hero.GlobalPosition = new Vector2(100000, 100000);
        m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.BullDemonBeast,
            new EnemySpawn(new Vector2(1350, 100), ContinueEncounterAsync: (_, _, _) => Task.CompletedTask));
        var beast = m_Monster;
        beast.SetPatrolAnchor(m_Driver.GetViewport().GetCamera2D());
        await WaitUntilAsync(() => beast.OnFloor && beast.AttackSegment == 0, "兽形没有开始自主冲撞");
        Check(beast.Velocity.X < 0 && ApplyEastSeaHit(beast, hero.Id, 100, 1, false, Vector2.Zero) == 0,
            "兽形首次方向或冲撞无敌错误");
        await WaitUntilAsync(() => beast.AttackSegment < 0, "兽形到边缘没有停步", 7000);
        Check(ApplyEastSeaHit(beast, hero.Id, 100, 1, false, Vector2.Zero) > 0, "兽形停步时仍然无敌");
        GF.Base.PauseGame();
        await WaitFramesAsync(3);
        Vector2 position = beast.GlobalPosition;
        await WaitFramesAsync(12);
        Check(beast.GlobalPosition == position && beast.AttackSegment < 0, "暂停推进兽形冲撞或边缘等待");
        GF.Base.ResumeGame();
        await WaitUntilAsync(() => beast.AttackSegment == 0 && beast.Velocity.X > 0, "兽形没有反向冲撞", 5000);
        ReleaseTestEntity(beast);
        m_Monster = null;
        await WaitFramesAsync(3);
        foreach (int direction in new[] { -1, 1 })
        {
            m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.BullGuard, new EnemySpawn(new Vector2(1350, 100)));
            var guard = m_Monster;
            SmokeInspection.FreezeAi(guard);
            await WaitUntilAsync(() => guard.OnFloor, "牛妖护卫未落地");
            hero.GlobalPosition = guard.GlobalPosition + new Vector2(direction * 120, 0);
            typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(guard, [hero]);
            Check(!((IEnemyAiAgent)guard).IsInEngageRange, "牛妖场景交战范围错误包含110px以外的目标");
            hero.GlobalPosition = guard.GlobalPosition + new Vector2(direction * 70, 0);
            Check(((IEnemyAiAgent)guard).RequestAttack(0), "牛妖近身没有出招");
            GF.Fsm.GetSystem<HeroSystem>().AdvanceHurtProtection(float.MaxValue);
            int hp = hero.Hp;
            await WaitUntilAsync(() => hero.Hp < hp, "牛妖原生重斩未命中", 4000);
            ReleaseTestEntity(guard);
            m_Monster = null;
            await WaitFramesAsync(3);
        }
        GD.Print("SMOKE-BULL BEAST/GUARD PASS: charge invulnerability, vulnerable rest, reverse, pause, 110px guard range and both-facing hits");
    }

    /// <summary>
    /// 检查最终形态的独立淡入、受伤条件、暂停和火柱归还。
    /// </summary>
    /// <param name="hero">本场英雄沙包。</param>
    /// <returns>心脏与所属节点清理检查。</returns>
    private async Task CheckBullHeartAsync(AbstractHeroEntity hero)
    {
        hero.GlobalPosition = new Vector2(100000, 100000);
        m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.BullDemonHeart,
            new EnemySpawn(new Vector2(1350, 300)));
        var heart = (BullDemonHeartEntity)m_Monster;
        var sprite = heart.GetNode<AnimatedSprite2D>("m_Heart");
        int hp = heart.Hp;
        Check(ApplyEastSeaHit(heart, hero.Id, 100, 1, false, Vector2.Zero) == 0 && heart.Hp == hp,
            "隐藏心脏接受了伤害");
        await WaitUntilAsync(() => sprite.Modulate.A == 1f, "心脏没有按源显现", 14000);
        Check(ApplyEastSeaHit(heart, hero.Id, 100, 1, false, Vector2.Zero) > 0, "完全显现的心脏仍然无敌");
        await CaptureCombatAsync("bull_heart_visible");
        GF.Base.PauseGame();
        await WaitFramesAsync(3);
        var hand = heart.GetNode<Node2D>("m_LeftHand");
        Vector2 position = hand.Position;
        float alpha = sprite.Modulate.A;
        await WaitFramesAsync(10);
        Check(hand.Position == position && sprite.Modulate.A == alpha, "暂停推进心脏或巨掌");
        GF.Base.ResumeGame();
        heart.GetType().GetMethod("SpawnFire", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(heart, [2]);
        var fires = heart.GetParent().GetChildren().OfType<BullDemonFireNode>().Where(value => value.Visible).ToArray();
        Check(fires.Length >= 2, "最终形态没有从节点池生成火柱");
        ReleaseTestEntity(heart);
        m_Monster = null;
        await WaitFramesAsync(3);
        Check(fires.All(value => !value.Visible), "最终形态离场遗留火柱");
        GD.Print("SMOKE-BULL HEART 4-1 PASS: source fade, invulnerability, native pause, independent hands/fire and owned pool cleanup");
    }

    /// <summary>
    /// 用真实致死攻击检查直接赠送、重复击败与满包，复用不能遗留奖励资格。
    /// </summary>
    /// <param name="hero">真实伤害来源。</param>
    /// <param name="flow">隔离档案和物品系统。</param>
    /// <returns>三次实际死亡及背包事务完成。</returns>
    private async Task CheckBullTreasureRewardAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        var profile = flow.GetModel<ProfileModel>();
        var original = profile.EquipmentDatas.ToArray();
        try
        {
            foreach (string scenario in new[] { "first", "duplicate", "full" })
            {
                if (scenario == "full")
                {
                    profile.EquipmentDatas.Clear();
                    while (profile.EquipmentDatas.Count < profile.Capacity)
                    {
                        flow.GetSystem<ProfileSystem>().TryCollectEquipment(flow.GetSystem<EquipmentSystem>().Roll(
                                                Guid.NewGuid(), EquipmentId.RoughPilgrimStaff, () => 0));
                    }
                }
                int before = profile.EquipmentDatas.Count;
                m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.BullDemonHeart,
                    new EnemySpawn(new Vector2(1350, 300)));
                var heart = (BullDemonHeartEntity)m_Monster;
                heart.SetPhysicsProcess(false);
                heart.GetNode<AnimationPlayer>("m_HeartFade").Stop();
                heart.GetNode<AnimatedSprite2D>("m_Heart").Modulate = Colors.White;
                ApplyEastSeaHit(heart, hero.Id, 1000000, 1, false, Vector2.Zero);
                Check(heart.Dead, "奖励夹具未实际击败心脏");
                heart.SetPhysicsProcess(true);
                await WaitUntilAsync(() => SmokeInspection.ReadField<IFsm<IEnemyBody>>(heart, "m_BodyFsm").CurrentState is BullDemonHeartDeathState,
                    "最终形态未进入实际死亡演出");
                int treasures = profile.EquipmentDatas.Count(value => value.EquipmentId == EquipmentId.DiamondSnare);
                Check(profile.EquipmentDatas.Count == before + (scenario == "first" ? 1 : 0) &&
                    treasures == (scenario == "full" ? 0 : 1), "金钢琢重复赠送、满包丢弃或未直接入包：" + scenario);
                if (scenario != "duplicate")
                {
                    var notice = GF.UI.GetAllLoadedUIForms().OfType<SaveNoticeForm>().Single();
                    Check(notice.GetNode<Label>("SaveFeedback/m_SaveFeedback").Text.Contains(
                        scenario == "full" ? "背包已满" : "功能待开放"), "金钢琢获得或满包提示缺失");
                }
                ReleaseTestEntity(heart);
                m_Monster = null;
                await WaitFramesAsync(3);
            }
            GD.Print("SMOKE-BULL TREASURE PASS: lethal hit, direct grant, duplicate defeat, full bag notice and pool reuse");
        }
        finally
        {
            profile.EquipmentDatas.ReplaceAll(original);
        }
    }
}
