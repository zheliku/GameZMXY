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
using GameLogic.Event.Equipment;
using GameConfig;
using GameConfig.Constant;
using System.Collections.Generic;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GodotGameFramework.UI;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 牛魔谷真实实体的原生招式、吞噬、心脏显隐和池清理专项。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 用实际游戏秒记录完整显隐周期，检查淡入结束立即受伤、停留和淡出的原版边界。
    /// </summary>
    /// <param name="hero">真实攻击来源，身体冻结避免干扰时序。</param>
    /// <returns>两种物理频率下的完整原生显隐验证。</returns>
    private async Task CheckBullHeartTimingAsync(AbstractHeroEntity hero)
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        var heroSystem = GF.Fsm.GetSystem<HeroSystem>();
        var source = new AttributeSourceId("BullHeartTiming", 1);
        heroSystem.SetSource(source, [AttributeModifier.Flat(AttributeType.MaxHp, 100000)]);
        heroSystem.Heal(int.MaxValue);
        try
        {
            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                hero.GlobalPosition = new Vector2(100000, 100000);
                m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.BullDemonHeart,
                    new EnemySpawn(new Vector2(1350, 300)));
                var heart = (BullDemonHeartEntity)m_Monster;
                var sprite = heart.GetNode<AnimatedSprite2D>("m_Heart");
                Check(sprite.Modulate.A == 0f, "心脏取池后没有立即恢复隐藏");
                var fsm = SmokeInspection.ReadField<IFsm<IEnemyBody>>(heart, "m_BodyFsm");
                var state = fsm.CurrentState;
                float hidden = SmokeInspection.ReadField<float>(state, "m_HeartLeft");
                Check(hidden >= 5 && hidden <= 9, "首次隐藏秒数不符合官方5..9");
                double clock = 0;
                double showingAt = -1, shownAt = -1, hidingAt = -1, hiddenAt = -1;
                float shownDuration = 0;
                float previousAlpha = 0;
                void Sample()
                {
                    clock += 1d / ticks;
                    float alpha = sprite.Modulate.A;
                    if (alpha > 0 && showingAt < 0)
                    {
                        showingAt = clock;
                    }

                    if (alpha == 1 && shownAt < 0)
                    {
                        shownAt = clock;
                        Check(ApplyEastSeaHit(heart, hero.Id, 10, 1, false, Vector2.Zero) > 0,
                            "原生淡入完成后仍拒绝伤害");
                    }
                    if (shownAt >= 0 && hidingAt < 0 && previousAlpha == 1 && alpha < 1)
                    {
                        hidingAt = clock;
                    }

                    if (hidingAt >= 0 && alpha == 0 && hiddenAt < 0)
                    {
                        hiddenAt = clock;
                    }

                    if (SmokeInspection.ReadField<object>(state, "m_HeartPhase").ToString() == "Shown")
                    {
                        shownDuration = Math.Max(shownDuration, SmokeInspection.ReadField<float>(state, "m_HeartLeft"));
                    }

                    if (alpha < 1)
                    {
                        Check(ApplyEastSeaHit(heart, hero.Id, 10, 1, false, Vector2.Zero) == 0,
                                                "未完全显现的心脏接受伤害");
                    }

                    previousAlpha = alpha;
                }
                m_Driver.GetTree().PhysicsFrame += Sample;
                try
                {
                    await WaitUntilAsync(() => hiddenAt >= 0, "心脏没有完成完整显隐周期", 22000);
                    GD.Print($"BULL HEART TRACE {ticks}: expectedHidden={hidden}, showing={showingAt:F3}, shown={shownAt:F3}, hiding={hidingAt:F3}, hidden={hiddenAt:F3}, hold={shownDuration:F3}");
                    Check(Math.Abs(showingAt - hidden) < .15, "首次显现开始时间与原版倒计时不符");
                    Check(Math.Abs(shownAt - showingAt - 2) < .15, "原生淡入不是2秒");
                    Check(shownDuration >= 1.9f && shownDuration <= 5.01f &&
                        Math.Abs(hidingAt - shownAt - shownDuration) < .15, "完整可伤害停留不符合原版2..5秒");
                    Check(Math.Abs(hiddenAt - hidingAt - 2) < .15, "原生淡出不是2秒");
                    Check(ReferenceEquals(fsm.CurrentState, state), "心脏受伤重启了显隐状态");
                    await WaitUntilAsync(() => SmokeInspection.ReadField<object>(state, "m_HeartPhase").ToString() == "Hidden",
                        "淡出后没有进入下一隐藏周期");
                    float nextHidden = SmokeInspection.ReadField<float>(state, "m_HeartLeft");
                    Check(nextHidden >= 4.9f && nextHidden <= 11 && sprite.Modulate.A == 0f,
                        "后续隐藏不符合原版5..11秒或未完全隐藏");
                    GD.Print($"SMOKE-BULL HEART TIMING {ticks}Hz PASS: hidden={hidden}, fadeIn={shownAt - showingAt:F3}, vulnerable={hidingAt - shownAt:F3}, fadeOut={hiddenAt - hidingAt:F3}; first-full-alpha hit accepted");
                    await CheckBullFireRangeAsync(hero, heart, ticks);
                }
                finally
                {
                    m_Driver.GetTree().PhysicsFrame -= Sample;
                    ReleaseTestEntity(heart);
                    m_Monster = null;
                    await WaitFramesAsync(3);
                }
            }
        }
        finally
        {
            Engine.PhysicsTicksPerSecond = originalTicks;
            heroSystem.RemoveSource(source);
            heroSystem.Heal(int.MaxValue);
        }
    }

    /// <summary>
    /// 实际生成火柱，确认可见上部能伤人，火焰上方留白不能伤人。
    /// </summary>
    /// <param name="hero">已冻结运动的实际英雄。</param>
    /// <param name="heart">持有本轮火柱及攻击编号的真实首领。</param>
    /// <param name="ticks">当前物理频率。</param>
    /// <returns>上边界排除与上部实际伤害检查。</returns>
    private async Task CheckBullFireRangeAsync(AbstractHeroEntity hero, BullDemonHeartEntity heart, int ticks)
    {
        var owned = SmokeInspection.ReadField<HashSet<BullDemonFireNode>>(heart, "m_Fires");
        var before = owned.ToArray();
        typeof(BullDemonHeartEntity).GetMethod("SpawnFire", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(heart, [1]);
        var fire = owned.Except(before).Single();
        var player = fire.GetNode<AnimationPlayer>("AnimationPlayer");
        Vector2 hurtOffset = hero.HurtBox.GetGlobalBounds().GetCenter() - hero.GlobalPosition;
        hero.GlobalPosition = fire.GlobalPosition + new Vector2(0, -350) - hurtOffset;
        var system = GF.Fsm.GetSystem<HeroSystem>();
        system.AdvanceHurtProtection(float.MaxValue);
        int hp = hero.Hp;
        await WaitUntilAsync(() => player.CurrentAnimationPosition >= 1.05, "火柱未进入喷发帧");
        Check(hero.Hp == hp, "火柱误伤了高于源火焰的留白区");
        hero.GlobalPosition = fire.GlobalPosition + new Vector2(0, -220) - hurtOffset;
        await WaitUntilAsync(() => hero.Hp < hp, "火柱可见上部没有实际伤害", 1000);
        Check(player.CurrentAnimationPosition < 35d / 24d, "伤害落在火焰销毁窗口之后");
        hero.GlobalPosition = new Vector2(100000, 100000);
        GD.Print($"SMOKE-BULL FIRE RANGE {ticks}Hz PASS: above flame misses, visible upper flame at -220px deals damage");
    }

    /// <summary>
    /// 逐招运行双朝向、两种物理频率，不以资源加载代替实机伤害证据。
    /// </summary>
    /// <param name="hero">本局真实英雄。</param>
    /// <param name="flow">本局权威模型与系统的作用域。</param>
    /// <returns>完整独立首领动作检查。</returns>
    private async Task CheckBullEnemiesAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        bool captureOnly = OS.GetCmdlineUserArgs().Contains("--bull-capture-only");
        var system = flow.GetSystem<HeroSystem>();
        var source = new AttributeSourceId("BullSmoke", 1);
        system.SetSource(source, [AttributeModifier.Flat(AttributeType.MaxHp, 100000)]);
        system.Heal(int.MaxValue);
        try
        {
            if (!captureOnly)
            {
                await CheckBullChargeAndGuardAsync(hero);
            }
            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                foreach (int direction in new[] { -1, 1 })
                {
                    foreach (int index in captureOnly ? new[] { 4 } : Enumerable.Range(0, 5))
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
                        bool capturedFrame = false;
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
                                var sprite = king.GetNode<AnimatedSprite2D>("m_Body");
                                Check(sprite.SpriteFrames.ResourcePath.EndsWith(hero is TangsengEntity
                                    ? "/bull_demon_king_tangseng_frames.tres" : "/bull_demon_king_official_frames.tres"),
                                    "吞噬立绘未按实际捕获的英雄选择");
                                if (!capturedFrame && sprite.Frame >= 46)
                                {
                                    capturedFrame = true;
                                    await CaptureCombatAsync($"hero_{hero.HeroId}_bull_captured_{ticks}_{direction}");
                                }
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
                            Check(captured && capturedState && capturedFrame && pulses == 5 && hp - hero.Hp == 250 && hero.Visible && !hero.ActionData.Captured,
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

                GD.Print($"SMOKE-BULL ATTACKS 4-1 {ticks}Hz PASS: hero={hero.HeroId}, {(captureOnly ? "swallowing" : "five native attacks")}, both directions, five drain pulses and release");
            }
            if (captureOnly)
            {
                return;
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
    /// 用真实致死攻击检查直接赠送、重复击败、满包与提示缺席，复用不能遗留奖励资格。
    /// </summary>
    /// <param name="hero">真实伤害来源。</param>
    /// <param name="flow">隔离档案和物品系统。</param>
    /// <returns>实际死亡、窗口复用及背包事务完成。</returns>
    private async Task CheckBullTreasureRewardAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        var profile = flow.GetModel<ProfileModel>();
        var original = profile.EquipmentDatas.ToArray();
        try
        {
            foreach (string scenario in new[] { "first", "duplicate", "full", "without-notice" })
            {
                if (scenario == "without-notice")
                {
                    profile.EquipmentDatas.Clear();
                    var notice = (SaveNoticeForm)GF.UI.GetUIForm(ResourcesCollectionConstant.UIs_save_notice_form);
                    GF.UI.CloseUIForm(notice.SerialId);
                    await WaitFramesAsync(3);
                    Check(GF.Event.Count(EquipmentRewardEventArgs.EventId) == 0, "提示关闭后遗留奖励订阅");
                }

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
                Check(profile.EquipmentDatas.Count == before + (scenario is "first" or "without-notice" ? 1 : 0) &&
                    treasures == (scenario == "full" ? 0 : 1), "金钢琢重复赠送、满包丢弃或未直接入包：" + scenario);
                if (scenario is "first" or "full")
                {
                    var notice = GF.UI.GetAllLoadedUIForms().OfType<SaveNoticeForm>().Single();
                    await WaitUntilAsync(() => notice.GetNode<Label>("SaveFeedback/m_SaveFeedback").Text.Contains(
                        scenario == "full" ? "背包已满" : "获得 金钢琢"), "金钢琢获得或满包提示缺失");
                }

                if (scenario == "without-notice")
                {
                    await WaitUntilAsync(() => !heart.IsShown, "提示缺席阻止了心脏完成死亡回池");
                    var notice = await GF.UI.OpenUIFormAsync<SaveNoticeForm>(UIFormId.SaveNoticeForm);
                    Check(GF.Event.Count(EquipmentRewardEventArgs.EventId) == 1, "提示池复用后奖励订阅不唯一");
                    Check(!notice.GetNode<Label>("SaveFeedback/m_SaveFeedback").Visible, "提示重开播放了已过期奖励");
                }
                else
                {
                    ReleaseTestEntity(heart);
                }
                m_Monster = null;
                await WaitFramesAsync(3);
            }
            GD.Print("SMOKE-BULL TREASURE PASS: lethal hit, direct grant, duplicate defeat, full bag, absent notice, death completion and subscription reuse");
        }
        finally
        {
            if (GF.UI.GetUIForm(ResourcesCollectionConstant.UIs_save_notice_form) == null)
            {
                await GF.UI.OpenUIFormAsync<SaveNoticeForm>(UIFormId.SaveNoticeForm);
            }

            profile.EquipmentDatas.ReplaceAll(original);
        }
    }
}
