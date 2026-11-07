using System.Collections.Generic;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GameConfig.Attributes;
using GameConfig.Entity;
using GameConfig.Equipment;
using GameConfig.Loot;
using GameLogic.Architecture;
using GameLogic.Battle;
using static GameLogic.Battle.HeroModel;
using GameLogic.Battle.Attributes;
using GameLogic.Effects;
using GameLogic.Enemies;
using GameLogic.Enemies.AI;
using GameLogic.Heroes;
using GameLogic.Profile;
using GameLogic.Level;
using GameLogic.UI;
using GameLogic.Input;
using GameLogic.Procedure.FlowData;
using GameLogic.Drops;
using GameLogic.Equipment;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 龙王两档数值、双向技能、冻结、暂停与池回收的真实引擎专项。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 从真实水面下穿到隐藏关，保留来源收益，再验证首次掉落、传送门和磁盘检查点。
    /// </summary>
    /// <param name="flow">初始真实流程的数据入口。</param>
    /// <param name="level">初始正式场景。</param>
    /// <returns>水下入口和完整隐藏关流程完成。</returns>
    private async Task CheckDragonEntranceFlowAsync(ProcedureOwner flow, LevelViewController level)
    {
        level.Cleanup();
        flow.GetSystem<ProfileSystem>().RecordLevelClear(1);
        flow.SetData(FlowKeys.SelectedLevelId, (VarInt32)2);
        ReenterLevel();
        await WaitForLevelAsync();
        var cave = FindLevel();
        var hero = FindHero();
        hero.SetPhysicsProcess(true);
        Check(SmokeInspection.ReadField<int>(cave, "m_LevelId") == 2, "入口夹具未进入水帘洞");
        flow.GetSystem<ProfileSystem>().AddSoul(17);
        int soul = flow.GetModel<ProfileModel>().Soul.Value;
        hero.GlobalPosition = new Vector2(2150, 450);
        hero.Velocity = Vector2.Zero;
        await WaitFramesAsync(3);
        await WaitUntilAsync(() => hero.OnFloor, "水帘洞水面没有实际落地");
        Check(flow.GetModel<HeroModel>().ControlsEnabled && !SmokeInspection.ReadField<bool>(cave, "m_EnteringHiddenLevel"),
            "站在水面就触发隐藏入口，入口高度错误");
        Input.ActionPress(InputSystem.MoveDown);
        Input.ActionPress(InputSystem.Jump);
        await WaitUntilAsync(() => !flow.GetModel<HeroModel>().ControlsEnabled, "真实S加K没有从水面进入隐藏接触区", 5000);
        Input.ActionRelease(InputSystem.Jump);
        Input.ActionRelease(InputSystem.MoveDown);
        var fade = SmokeInspection.ReadField<AnimationPlayer>(cave, "m_EntranceFade");
        await WaitFramesAsync(5);
        GF.Base.PauseGame();
        await WaitFramesAsync(3);
        double paused = fade.CurrentAnimationPosition;
        await WaitFramesAsync(6);
        Check(fade.CurrentAnimationPosition == paused, "游戏暂停仍推进入口淡出");
        GF.Base.ResumeGame();
        await WaitUntilAsync(() => FindLevel() is { } current && !ReferenceEquals(current, cave) &&
            SmokeInspection.ReadField<int>(current, "m_LevelId") == 11 &&
            SmokeInspection.Model(current).SessionActive && CurrentBattleForm() != null,
            "自动入口没有经GGF重入龙宫", 15000);
        var room = FindLevel();
        hero = FindHero();
        Check(flow.GetModel<ProfileModel>().Soul.Value == soul && flow.GetModel<ProfileModel>().HighestClearedLevelId.Value == 1,
            "隐藏转场回滚已领取收益或误通关水帘洞");
        Check(hero.Modulate.A == 1 && CurrentBattleForm().Modulate.A == 1 && flow.GetModel<HeroModel>().ControlsEnabled,
            "转场复用后英雄或HUD仍透明，或未恢复控制");
        Input.ActionPress(InputSystem.MoveRight);
        await WaitUntilAsync(() => FindMonster() is DragonKingEntity, "龙宫没有按配方生成龙王", 15000);
        Input.ActionRelease(InputSystem.MoveRight);
        var dragon = FindMonster();
        SmokeInspection.FreezeAi(dragon);
        hero.SetPhysicsProcess(false);
        hero.GlobalPosition = new Vector2(450, 450);
        await WaitUntilAsync(() => SmokeInspection.ReadField<float>(dragon, "m_InvulnerabilityLeft") <= 0,
            "龙王技能无敌没有按秒结束", 7000);
        Check(!flow.GetModel<ProfileModel>().EastSeaBannerAvailable &&
            (EEquipmentDropPoolId?)typeof(DragonKingEntity).GetProperty("EquipmentDropPoolIdOverride", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dragon) == EEquipmentDropPoolId.EastSeaBanner,
            "首次龙王未在出生时消费旗幡资格");
        await CaptureCombatAsync("dragon_palace_before_defeat");
        ApplyEastSeaHit(dragon, hero.Id, 10000000, 1, false, Vector2.Zero);
        Check(dragon.Dead, "真实伤害未击败龙王");
        var exit = SmokeInspection.ReadField<LevelExitNode>(room, "m_Exit");
        await WaitFramesAsync(2);
        Check(!dragon.HurtBox.Monitorable, "龙王死亡闪烁期间仍保留首领受击盒");
        Check(!exit.Visible && SmokeInspection.Model(room).Outcome.Value == ELevelOutcome.Running,
            "龙王死亡动画未完成就开放出口");
        await WaitUntilAsync(() => !dragon.IsShown && exit.Visible, "龙王完成死亡后没有开放传送门", 5000);
        var drops = SmokeInspection.ReadField<System.Collections.Generic.HashSet<GroundItemNode>>(room, "m_GroundItems");
        var banner = drops.Single(item => SmokeInspection.ReadField<EquipmentData>(item, "m_EquipmentData")?.EquipmentId == EEquipmentId.EastSeaBanner);
        EquipmentData data = SmokeInspection.ReadField<EquipmentData>(banner, "m_EquipmentData");
        hero.GlobalPosition = banner.GlobalPosition;
        await WaitUntilAsync(() => flow.GetModel<ProfileModel>().HasEquipment(EEquipmentId.EastSeaBanner), "旗幡没有通过真实地面接触拾取");
        await WaitFramesAsync(55);
        hero.GlobalPosition = exit.GlobalPosition - new Vector2(0, 80);
        hero.Velocity = Vector2.Zero;
        hero.SetPhysicsProcess(true);
        await WaitFramesAsync(3);
        await WaitUntilAsync(() => hero.OnFloor, "龙宫出口前未落地");
        m_Driver.GetViewport().PushInput(new InputEventAction { Action = InputSystem.Interact, Pressed = true }, true);
        await WaitFramesAsync(2);
        m_Driver.GetViewport().PushInput(new InputEventAction { Action = InputSystem.Interact, Pressed = false }, true);
        await WaitUntilAsync(() => flow.GetModel<LevelModel>().Outcome.Value == ELevelOutcome.Cleared,
            "龙宫W出口没有提交结算", 7000);
        Check(await flow.GetSystem<LevelSystem>().CheckpointWrite, "龙宫检查点写入失败");
        var disk = await ReadDiskAsync();
        Check(!disk.EastSeaBannerAvailable && disk.HighestClearedLevelId == 1 &&
            disk.EquipmentItems.Any(item => item.InstanceId == data.InstanceId && item.Attributes == data.Attributes),
            "真实磁盘没有保留旗幡资格、实际属性或主线隔离");
        await WaitUntilAsync(() => GF.UI.GetAllLoadedUIForms().OfType<SettlementForm>()
            .Any(item => item.Visible && SmokeInspection.CallbackCount(item, "MapRequested") > 0), "龙宫胜利页未完成流程导航绑定");
        GF.UI.GetAllLoadedUIForms().OfType<SettlementForm>().Single(item => item.Visible)
            .GetNode<BaseButton>("Content/Actions/m_ReturnMap").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntilAsync(() => GF.Procedure.CurrentProcedure is ProcedureMap, "龙宫结算没有返回地图");
        Check(flow.GetSystem<ProfileSystem>().LatestLevelId == 2, "龙宫通关改变了地图最新提示");
        GD.Print("SMOKE-DRAGON FLOW PASS: real water drop, paused fade, GGF transfer, retained rewards, first banner ground pickup, native death, W portal, disk checkpoint and map");
    }

    /// <summary>
    /// 按24fps源时序运行四招，读取实际受击与池化效果，不直接调用动画方法。
    /// </summary>
    /// <param name="hero">真实受击英雄。</param>
    /// <param name="flow">本次测试的模型与业务作用域。</param>
    /// <returns>两档、两个物理频率和两种朝向均完成。</returns>
    private async Task CheckDragonAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        var system = flow.GetSystem<HeroSystem>();
        var profile = flow.GetModel<ProfileModel>();
        var source = new AttributeSourceId("DragonSmoke", 1);
        system.SetSource(source, [AttributeModifier.Flat(EAttributeType.MaxHp, 100000)]);
        system.Heal(int.MaxValue);
        try
        {
            foreach (int tier in new[] { 1, 21 })
            {
                if (tier == 21)
                {
                    int experience = flow.GetModel<GameLogic.Config.ConfigurationModel>().Tables.TbHeroLevelConfig.DataList
                        .Where(item => item.Id < 21).Sum(item => item.MaxExp);
                    flow.GetSystem<ProfileSystem>().AddExperience(experience - profile.TotalExperience);
                }
                foreach (int ticks in new[] { 60, 120 })
                {
                    Engine.PhysicsTicksPerSecond = ticks;
                    foreach (int direction in new[] { -1, 1 })
                    {
                        for (int index = 0; index < 4; index++)
                        {
                            flow.GetSystem<HeroStatusSystem>().AdvanceFreeze(100);
                            system.Heal(int.MaxValue);
                            hero.GlobalPosition = new Vector2(100000, 100000);
                            m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EEntityId.DragonKing,
                                new EnemySpawn(new Vector2(580, 100)));
                            var actor = m_Monster;
                            SmokeInspection.FreezeAi(actor);
                            await WaitUntilAsync(() => actor.OnFloor, "龙王夹具没有实际落地");
                            Check(actor.MaxHp == (tier == 1 ? 12000 : 40000), "龙王没有使用本次入场等级档");
                            hero.GlobalPosition = actor.GlobalPosition + new Vector2(direction * 120, 0);
                            typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(actor, [hero]);
                            actor.SetFacing(direction);
                            Vector2 castPosition = actor.GlobalPosition;
                            float targetX = hero.GlobalPosition.X;
                            flow.GetSystem<HeroSystem>().AdvanceHurtProtection(float.MaxValue);
                            int hp = hero.Hp;
                            Check(((IEnemyAiAgent)actor).RequestAttack(index), "龙王拒绝正式攻击" + index);
                            await WaitUntilAsync(() => actor.AttackSegment == index, "龙王身体没有消费请求");
                            if (index != 0)
                            {
                                Check(ApplyEastSeaHit(actor, hero.Id, 100, 1, false, Vector2.Zero) == 0, "龙王技能起手缺少无敌");
                            }

                            bool sawEffect = false;
                            bool captured = false;
                            int actualHits = 0;
                            int previousHp = hp;
                            ulong started = Time.GetTicksMsec();
                            while (actor.AttackSegment >= 0 || actor.GetParent().GetChildren().OfType<SkillEffectNode>().Any(effect => effect.Visible))
                            {
                                Check(Time.GetTicksMsec() - started < 8000, "龙王招式或独立攻击未结束");
                                var effect = actor.GetParent().GetChildren().OfType<SkillEffectNode>().FirstOrDefault(item => item.Visible);
                                if (effect != null && !sawEffect)
                                {
                                    sawEffect = true;
                                    Check(effect.GlobalPosition.IsEqualApprox(index == 0 ? castPosition : new Vector2(targetX - 65, castPosition.Y - 120)),
                                        "龙王独立攻击原点不符原版");
                                }
                                var shape = effect?.GetNode<CollisionShape2D>("Origin/Visual/HitBox/CollisionShape2D") ??
                                    actor.HitBox.GetChildren().OfType<CollisionShape2D>().FirstOrDefault(item => !item.Disabled);
                                if (shape is { Disabled: false })
                                {
                                    hero.GlobalPosition = shape.GlobalPosition - (hero.HurtBox.GetGlobalBounds().GetCenter() - hero.GlobalPosition);
                                    if (!captured)
                                    {
                                        captured = true;
                                        await CaptureCombatAsync($"dragon_{tier}_{ticks}_{index}_{direction}");
                                    }
                                }
                                await WaitFramesAsync(1);
                                if (hero.Hp != previousHp)
                                {
                                    actualHits++;
                                }

                                previousHp = hero.Hp;
                            }
                            Check(hero.Hp < hp && captured, $"龙王攻击未实际命中：{tier}/{index}/{ticks}/{direction}");
                            // 目标身体被冻结，五次受击后两秒保护不会衰减；七个攻击段中的后两段应被免疫。
                            Check(actualHits == (index == 1 ? 5 : 1), $"龙王命中/英雄连击保护错误：{tier}/{index} hits={actualHits}");
                            if (index == 1)
                            {
                                Animation ice = actor.AnimPlayer.GetAnimation("attack_2");
                                int refreshes = 0;
                                for (int track = 0; track < ice.GetTrackCount(); track++)
                                {
                                    if (ice.TrackGetType(track) != Animation.TrackType.Method)
                                    {
                                        continue;
                                    }

                                    for (int key = 0; key < ice.TrackGetKeyCount(track); key++)
                                    {
                                        if (ice.MethodTrackGetName(track, key) == "NewAttackAnimEvent")
                                        {
                                            refreshes++;
                                        }
                                    }
                                }
                                Check(refreshes == 6, "龙王冰环未保留起手加六次刷新");
                            }
                            Check(sawEffect == (index is 0 or 3), "龙王离身攻击的所属招式错误");
                            if (index == 2)
                            {
                                Check((flow.GetModel<HeroModel>().StatusEffects.GetValueOrDefault(EStatusEffectKind.Freeze)?.RemainingSeconds ?? 0) == (tier == 1 ? 2 : 5), "冰冻持续时间不符等级档");
                            }

                            Check(actor.HitBox.GetChildren().OfType<CollisionShape2D>().All(item => item.Disabled), "龙王收招残留身体判定");
                            Check(!actor.GetNode<AnimatedSprite2D>("m_HitBoxRoot/m_IceAura").Visible, "龙王收招残留冰环");
                            ReleaseTestEntity(actor);
                            m_Monster = null;
                            Check(!hero.GetParent().GetChildren().OfType<SkillEffectNode>().Any(item => item.Visible), "龙王回池后遗留独立攻击");
                            await WaitFramesAsync(3);
                            GD.Print($"SMOKE-DRAGON ATTACK PASS: level={tier} ticks={ticks} direction={direction} index={index} damage={hp - hero.Hp} hits={actualHits}");
                        }
                    }
                }
            }
            await CheckDragonStatusAndCancelAsync(hero, flow);
        }
        finally
        {
            if (m_Monster is { IsShown: true })
            {
                ReleaseTestEntity(m_Monster);
            }

            m_Monster = null;
            flow.GetSystem<HeroStatusSystem>().AdvanceFreeze(100);
            system.RemoveSource(source);
            system.Heal(int.MaxValue);
            Engine.PhysicsTicksPerSecond = originalTicks;
        }
    }

    /// <summary>
    /// 悟空无双免疫新附加状态；暂停冻结首领时钟，回池取消已离身的真实水波。
    /// </summary>
    /// <param name="hero">实际悟空实体。</param>
    /// <param name="flow">本局正式规则入口。</param>
    /// <returns>免疫、暂停与效果取消完成。</returns>
    private async Task CheckDragonStatusAndCancelAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        var model = flow.GetModel<HeroModel>();
        var system = flow.GetSystem<HeroSystem>();
        flow.GetSystem<HeroStatusSystem>().AdvanceFreeze(100);
        model.Ws.Value = model.MaxWs.Value;
        Check(hero.TryActivateMusou() && !hero.CanReceiveStatusEffect, "悟空无双没有禁止附加控制状态");
        hero.GlobalPosition = new Vector2(100000, 100000);
        m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EEntityId.DragonKing, new EnemySpawn(new Vector2(580, 100)));
        var actor = m_Monster;
        SmokeInspection.FreezeAi(actor);
        await WaitUntilAsync(() => actor.OnFloor, "无双免疫夹具龙王未落地");
        hero.GlobalPosition = actor.GlobalPosition - new Vector2(120, 0);
        typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(actor, [hero]);
        Check(((IEnemyAiAgent)actor).RequestAttack(2), "冰冻免疫夹具未出招");
        await WaitUntilAsync(() => actor.AttackSegment == 2, "冰冻免疫招式未起手");
        GF.Base.PauseGame();
        await WaitFramesAsync(3);
        double position = actor.AnimPlayer.CurrentAnimationPosition;
        float invulnerable = SmokeInspection.ReadField<float>(actor, "m_InvulnerabilityLeft");
        await WaitFramesAsync(6);
        Check(actor.AnimPlayer.CurrentAnimationPosition == position &&
            SmokeInspection.ReadField<float>(actor, "m_InvulnerabilityLeft") == invulnerable,
            "游戏暂停仍推进龙王动作或无敌");
        GF.Base.ResumeGame();
        flow.GetSystem<HeroSystem>().AdvanceHurtProtection(float.MaxValue);
        int hp = hero.Hp;
        while (actor.AttackSegment >= 0)
        {
            var shape = actor.HitBox.GetNode<CollisionShape2D>("CollisionShape2D");
            if (!shape.Disabled)
            {
                hero.GlobalPosition = shape.GlobalPosition - (hero.HurtBox.GetGlobalBounds().GetCenter() - hero.GlobalPosition);
            }

            await WaitFramesAsync(1);
        }
        Check(hero.Hp < hp && !flow.GetSystem<HeroStatusSystem>().IsFrozen, "无双悟空没有实际受击，或被错误施加冰冻");
        system.ClearWs();
        await WaitUntilAsync(() => !((IEnemyAiAgent)actor).IsAttacking, "龙王冰冻收招未结束");
        Check(((IEnemyAiAgent)actor).RequestAttack(0), "取消夹具未释放水波");
        await WaitUntilAsync(() => actor.GetParent().GetChildren().OfType<SkillEffectNode>().Any(item => item.Visible), "取消夹具水波未出现");
        var parent = actor.GetParent();
        ReleaseTestEntity(actor);
        m_Monster = null;
        Check(parent.GetChildren().OfType<SkillEffectNode>().All(item => !item.Visible), "龙王回池没有取消正在飞行的水波");
        GD.Print("SMOKE-DRAGON CONTROL PASS: Wukong musou status immunity, paused native clocks and active projectile cancellation");
    }
}
