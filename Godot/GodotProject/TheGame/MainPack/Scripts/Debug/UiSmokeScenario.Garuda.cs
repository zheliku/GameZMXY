using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GameConfig.Attributes;
using GameConfig.Battle;
using GameConfig.Entity;
using GameConfig.Equipment;
using GameFramework;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Attributes;
using GameLogic.Drops;
using GameLogic.Effects;
using GameLogic.Enemies;
using GameLogic.Enemies.AI;
using GameLogic.Enemies.Body;
using GameLogic.Entity;
using GameLogic.Equipment;
using GameLogic.Heroes;
using GameLogic.Level;
using GameLogic.Profile;
using GameLogic.UI;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 迦楼罗之穴专属的原生招式、火焰伤害、化卵、复生与死亡生命周期回归。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 只改变沙包站位，验证正式AI自主选招、持续追踪和形态切换。
    /// </summary>
    /// <param name="hero">保留正式受击规则的英雄沙包。</param>
    /// <param name="flow">本局作用域。</param>
    /// <returns>两个物理频率下的自主决策验证。</returns>
    private async Task CheckGarudaAiAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        HeroSystem system = flow.GetSystem<HeroSystem>();
        var source = new AttributeSourceId("GarudaAiSmoke", 1);
        system.SetSource(source, [AttributeModifier.Flat(AttributeType.MaxHp, 100000)]);
        system.Heal(int.MaxValue);
        try
        {
            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                hero.GlobalPosition = new Vector2(100000, 100000);
                _monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.PengDemonKing,
                    new EnemySpawn(new Vector2(580, 100)));
                var peng = (PengDemonKingEntity)_monster;
                var agent = (IEnemyAiAgent)peng;
                await WaitUntilAsync(() => peng.OnFloor, "自主选招夹具未落地");
                typeof(PengDemonKingEntity).GetField("_groundLeft", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(peng, 60f);
                GD.Seed(7711);
                float distance = 250f;
                void FollowPeng()
                {
                    hero.GlobalPosition = peng.GlobalPosition + new Vector2(distance, 0);
                }
                _driver.GetTree().PhysicsFrame += FollowPeng;
                try
                {
                    FollowPeng();
                    typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(peng, [hero]);
                    var casts = new System.Collections.Generic.HashSet<int>();
                    ulong started = Time.GetTicksMsec();
                    while (!casts.Contains(1) || !casts.Contains(2))
                    {
                        Check(Time.GetTicksMsec() - started < 25000, "彭魔王中距离没有自主选择冲刺和地面生火");
                        if (peng.AttackSegment >= 0)
                        {
                            casts.Add(peng.AttackSegment);
                            Check(peng.AttackSegment is 1 or 2, "地面中距离使用了错误招式");
                            if (peng.AttackSegment == 2)
                            {
                                Check(Math.Abs(peng.AiData.Cooldowns[0] - peng.AiData.Cooldowns[2]) < .01f, "地空生火未共用冷却");
                            }
                        }
                        await WaitFramesAsync(1);
                    }
                    distance = 80f;
                    await WaitUntilAsync(() => peng.AttackSegment == 3, "彭魔王近身没有自主刺击");
                    await WaitUntilAsync(() => !agent.IsAttacking, "近身刺击未收招");
                    typeof(PengDemonKingEntity).GetField("_groundLeft", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(peng, .05f);
                    await WaitUntilAsync(() => peng.Flying, "地面时钟到点未起飞");
                    Check(SmokeInspection.ReadField<float>(peng, "_invulnerableLeft") > 2.9f, "起飞没有三秒无敌");
                    typeof(PengDemonKingEntity).GetField("_flightLeft", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(peng, .1f);
                    await WaitUntilAsync(() => !peng.Flying, "飞行时钟到点未下落");
                    Check(Math.Abs(peng.GlobalPosition.Y - 150f) < 10, "下落没有使用官方150px高度");
                }
                finally
                {
                    _driver.GetTree().PhysicsFrame -= FollowPeng;
                }
                ReleaseTestEntity(peng);
                _monster = null;
                hero.GlobalPosition = new Vector2(100000, 100000);
                _monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.FlyingDemon,
                    new EnemySpawn(new Vector2(580, 200)));
                AbstractEnemyEntity flyer = _monster;
                var flyerAgent = (IEnemyAiAgent)flyer;
                IFsm<IEnemyAiAgent> fsm = SmokeInspection.ReadField<IFsm<IEnemyAiAgent>>(flyer, "AiFsm");
                FlyingPursuitState state = fsm.GetState<FlyingPursuitState>();
                float turn = SmokeInspection.ReadField<float>(state, "_turnInterval");
                void FollowFlyer()
                {
                    hero.GlobalPosition = flyer.GlobalPosition + new Vector2(80, 60);
                }
                _driver.GetTree().PhysicsFrame += FollowFlyer;
                try
                {
                    FollowFlyer();
                    typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(flyer, [hero]);
                    await WaitUntilAsync(() => flyer.AttackSegment == 0, "飞行魔没有自主近身攻击");
                    double retreat = SmokeInspection.ReadField<double>(state, "_retreatUntil");
                    Check(retreat - flyer.AiData.ElapsedSeconds > 2.8 && retreat - flyer.AiData.ElapsedSeconds <= 3,
                        "飞行魔退避没有从出招时开始计三秒");
                    await WaitUntilAsync(() => flyer.AttackSegment < 0, "飞行魔自主攻击未完成");
                    var data = AttackData.Create(0, default, 1, DamageKind.Real, new Vector2(24, -24), 1, 0, source: new GameLogic.Battle.Attacks.AttackSource(hero.Id, GameConfig.Entity.EntityId.None, 0, 0));
                    try
                    {
                        flyer.ReceiveHit(data);
                    }
                    finally
                    {
                        ReferencePool.Release(data);
                    }
                    await WaitUntilAsync(() => ((IEnemyBody)flyer).HasPendingHurt || flyerAgent.IsCcLocked, "飞行魔未进入真实受击");
                    await WaitUntilAsync(() => !flyerAgent.IsCcLocked && !((IEnemyBody)flyer).HasPendingHurt, "飞行魔受击未恢复");
                    Check(SmokeInspection.ReadField<float>(state, "_turnInterval") == turn &&
                        SmokeInspection.ReadField<double>(state, "_retreatUntil") == retreat,
                        "飞行魔受击重入重新抽取了折返间隔或退避截止时间");
                }
                finally
                {
                    _driver.GetTree().PhysicsFrame -= FollowFlyer;
                }
                hero.GlobalPosition = new Vector2(100000, 100000);
                double age = flyer.AiData.ElapsedSeconds;
                await WaitUntilAsync(() => flyer.AiData.ElapsedSeconds - age > 3.2, "飞行魔时钟未推进");
                Check(flyerAgent.HasTarget, "飞行魔错误地在超出视野三秒后丢失原版持续目标");
                ReleaseTestEntity(flyer);
                _monster = null;
                GD.Print($"SMOKE-GARUDA AI 2-3 {ticks}Hz PASS: autonomous casts, shared cooldown, takeoff/fall, retreat and hurt continuity");
            }
        }
        finally
        {
            Engine.PhysicsTicksPerSecond = originalTicks;
            if (_monster is { IsShown: true })
            {
                ReleaseTestEntity(_monster);
            }

            _monster = null;
            system.RemoveSource(source);
            system.Heal(int.MaxValue);
        }
    }

    /// <summary>
    /// 当前关卡实际掉落接触、穿戴界面、失败检查点、重试和返回地图验证。
    /// </summary>
    /// <param name="hero">本局实际英雄。</param>
    /// <param name="flow">本次档案作用域。</param>
    /// <param name="level">正式关卡场景。</param>
    /// <param name="form">已打开的战斗界面。</param>
    /// <param name="levelId">实际进入的关卡主键。</param>
    /// <param name="equipmentIds">本关需要实际接触拾取的装备身份。</param>
    /// <param name="label">用于区分关卡证据的官方标签。</param>
    /// <returns>场景与磁盘重读完成的任务。</returns>
    private async Task CheckEquipmentFlowAsync(AbstractHeroEntity hero, ProcedureOwner flow, LevelViewController level,
        BattleForm form, int levelId, EquipmentId[] equipmentIds, string label)
    {
        Check(SmokeInspection.ReadField<int>(level, "_levelId") == levelId, "流程夹具进入错误关卡");
        await CheckGroundItemsAsync(flow, hero, level);
        HashSet<GroundItemNode> owned = SmokeInspection.ReadField<System.Collections.Generic.HashSet<GroundItemNode>>(level, "_groundItems");
        var equipmentDatas = new System.Collections.Generic.List<EquipmentData>();
        foreach (EquipmentId id in equipmentIds)
        {
            EquipmentData data = flow.GetSystem<EquipmentSystem>().Roll(Guid.NewGuid(), id, () => .5f);
            SpawnGround(level, hero.HurtBox.GetGlobalBounds().GetCenter(), null, data);
            GroundItemNode item = owned.Single();
            await WaitUntilAsync(() => flow.GetModel<ProfileModel>().EquipmentDatas.Any(value => value.InstanceId == data.InstanceId),
                "新装备没有通过真实接触进入背包");
            equipmentDatas.Add(data);
            item._PhysicsProcess(.81);
            Check(owned.Count == 0, "新装备领取后未归还节点池");
        }
        await CheckInventoryAsync(form, hero, flow, level);
        // 界面夹具恢复了原始背包，新装备必须仍是同一档案的权威实例。
        Check(equipmentDatas.All(data => flow.GetModel<ProfileModel>().EquipmentDatas.Any(value => value.InstanceId == data.InstanceId)),
            "穿卸界面恢复后丢失真实拾取的新装备");
        KillHero(hero);
        await WaitUntilAsync(() => SmokeInspection.Model(level).Outcome.Value == LevelOutcome.Defeated,
            "第六关墓碑结束后未提交失败", 15000);
        Check(await SmokeInspection.GetLevelSystem(level).CheckpointWrite, "第六关失败检查点写入失败");
        GameData saved = await ReadDiskAsync();
        Check(equipmentDatas.All(data => saved.EquipmentItems.Any(value => value.InstanceId == data.InstanceId && value.Attributes == data.Attributes)),
            "第六关失败检查点没有保存两件新装备及实际属性");
        await CheckFailureSettlementAsync(level);
        await WaitUntilAsync(() => GF.Procedure.CurrentProcedure is ProcedureLevel && FindLevel() is { } current &&
            !ReferenceEquals(current, level) && CurrentBattleForm() != null, "第六关失败页重试未重建场景", 15000);
        LevelViewController retry = FindLevel();
        Check(SmokeInspection.ReadField<int>(retry, "_levelId") == levelId &&
            equipmentDatas.All(data => flow.GetModel<ProfileModel>().EquipmentDatas.Any(value => value.InstanceId == data.InstanceId)),
            "重试进入错误关卡或丢失已保存装备");
        await CheckFailureReturnMapAsync();
        GD.Print($"SMOKE-EQUIPMENT FLOW {label} PASS: all ground icons, {equipmentIds.Length} real pickups, GUI wardrobe, failure save, retry and map return");
    }

    /// <summary>
    /// 在真实框架与物理场景中验证两类新敌人，不增加正式测试接口。
    /// </summary>
    /// <param name="hero">正式流程拥有的英雄。</param>
    /// <param name="flow">同一本局模型与规则作用域。</param>
    /// <returns>所有动作和池生命周期验证完成的任务。</returns>
    private async Task CheckGarudaEnemiesAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        HeroSystem system = flow.GetSystem<HeroSystem>();
        var source = new AttributeSourceId("SmokeGaruda", 1);
        system.SetSource(source, [AttributeModifier.Flat(AttributeType.MaxHp, 100000)]);
        system.Heal(int.MaxValue);
        try
        {
            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                foreach ((EntityId id, int count) in new[] { (EntityId.FlyingDemon, 1), (EntityId.PengDemonKing, 4) })
                {
                    hero.GlobalPosition = new Vector2(100000, 100000);
                    _monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(id, new EnemySpawn(new Vector2(580, 100)));
                    AbstractEnemyEntity actor = _monster;
                    SmokeInspection.FreezeAi(actor);
                    if (id == EntityId.PengDemonKing)
                    {
                        await WaitUntilAsync(() => actor.OnFloor, "彭魔王夹具未落地");
                    }

                    typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(actor, [hero]);
                    foreach (int direction in new[] { -1, 1 })
                    {
                        for (int index = 0; index < count; index++)
                        {
                            if (actor is PengDemonKingEntity)
                            {
                                typeof(PengDemonKingEntity).GetProperty("Flying", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(actor, index == 0);
                                typeof(PengDemonKingEntity).GetField("_groundLeft", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(actor, 60f);
                                typeof(PengDemonKingEntity).GetField("_flightLeft", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(actor, 60f);
                                if (index == 0)
                                {
                                    actor.GlobalPosition = new Vector2(580, 150);
                                }
                                else
                                {
                                    await WaitUntilAsync(() => actor.OnFloor, "彭魔王地面出招夹具未落地");
                                }
                            }
                            hero.GlobalPosition = actor.GlobalPosition + new Vector2(direction * 100, 0);
                            flow.GetSystem<HeroSystem>().AdvanceHurtProtection(float.MaxValue);
                            int hp = hero.Hp;
                            var agent = (IEnemyAiAgent)actor;
                            Check(agent.RequestAttack(index), $"{id}/{index + 1} 拒绝出招");
                            await WaitUntilAsync(() => actor.AttackSegment == index, $"{id} 身体状态未消费攻击请求");
                            bool sawFire = false;
                            bool hitDuringFade = false;
                            bool captured = false;
                            ulong start = Time.GetTicksMsec();
                            while (actor.AttackSegment >= 0 || actor.GetParent().GetChildren().OfType<SkillEffectNode>().Any(effect => effect.Visible))
                            {
                                Check(Time.GetTicksMsec() - start < 7000, $"{id}/{index + 1} 未完成或效果未回池");
                                SkillEffectNode effect = actor.GetParent().GetChildren().OfType<SkillEffectNode>().FirstOrDefault(effect => effect.Visible);
                                CollisionShape2D shape = effect?.GetNode<CollisionShape2D>("Origin/Visual/HitBox/CollisionShape2D") ??
                                    actor.HitBox.GetNode<CollisionShape2D>("CollisionShape2D");
                                sawFire |= effect != null;
                                if (!shape.Disabled)
                                {
                                    Vector2 offset = hero.HurtBox.GetGlobalBounds().GetCenter() - hero.GlobalPosition;
                                    hero.GlobalPosition = shape.GlobalPosition - offset;
                                    if (effect != null && effect.GetNode<AnimationPlayer>("AnimationPlayer").CurrentAnimationPosition < .6 && hero.Hp < hp)
                                    {
                                        hitDuringFade = true;
                                    }

                                    if (!captured)
                                    {
                                        captured = true;
                                        await CaptureCombatAsync($"garuda_{ticks}_{id}_{index + 1}_{direction}");
                                    }
                                }
                                await WaitFramesAsync(1);
                            }
                            Check(hero.Hp < hp, $"{id}/{index + 1}/{direction}/{ticks} 未造成真实伤害");
                            if (id == EntityId.PengDemonKing && index is 0 or 2)
                            {
                                Check(sawFire && hitDuringFade, "火焰未生成，或错误地将淡入期改成无伤害预警");
                            }

                            Check(actor.HitBox.GetChildren().OfType<CollisionShape2D>().All(shape => shape.Disabled), "收招后攻击判定未关闭");
                            await WaitUntilAsync(() => !agent.IsAttacking, "招式收招后身体仍然忙碌");
                            GD.Print($"SMOKE-GARUDA ATTACK 2-3: {ticks}Hz {id}/{index + 1}, dir={direction}, damage={hp - hero.Hp}, fadeHit={hitDuringFade}");
                        }
                    }
                    if (actor is PengDemonKingEntity peng)
                    {
                        hero.GlobalPosition = new Vector2(100000, 100000);
                        ApplyGarudaTestHit(peng, hero.Id);
                        await WaitFramesAsync(3);
                        Check(peng.Hp == 0 && !peng.Dead && peng.HurtBox.Monitorable, "首次耗尽应化卵且保留受击体");
                        GF.Base.PauseGame();
                        await WaitFramesAsync(3);
                        double position = peng.AnimPlayer.CurrentAnimationPosition;
                        await WaitFramesAsync(10);
                        Check(peng.AnimPlayer.CurrentAnimationPosition == position && peng.Hp == 0, "暂停推进了凤凰蛋动作或复生");
                        GF.Base.ResumeGame();
                        await WaitUntilAsync(() => peng.Hp == peng.MaxHp, "七秒后没有回满复生", 12000);
                        await WaitUntilAsync(() => peng.AnimPlayer.CurrentAnimation != "rebirth", "复生动作没有正常收招");
                        ApplyGarudaTestHit(peng, hero.Id);
                        await WaitFramesAsync(3);
                        for (int hit = 1; hit <= 5; hit++)
                        {
                            ApplyGarudaTestHit(peng, hero.Id);
                            Check(peng.Dead == (hit == 5), $"凤凰蛋第{hit}次命中的死亡事实不正确");
                        }
                        await WaitFramesAsync(3);
                        Check(!peng.HurtBox.Monitorable && peng.IsShown, "破蛋后应关闭受击体并等待死亡演出");
                        await WaitUntilAsync(() => !peng.IsShown, "首领死亡旋涡结束后没有回池");
                        _monster = null;
                    }
                    else
                    {
                        ReleaseTestEntity(actor);
                        _monster = null;
                    }
                    await WaitFramesAsync(3);
                    _monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(id, new EnemySpawn(new Vector2(580, 100)));
                    Check(ReferenceEquals(actor, _monster) && actor.Hp == actor.MaxHp && !actor.Dead && actor.AttackSegment == -1 &&
                        actor.GetNode<AnimatedSprite2D>("m_Body").Visible, "新敌人复用后残留生命、招式或死亡可见性");
                    ReleaseTestEntity(_monster);
                    _monster = null;
                }
            }
            GD.Print("SMOKE-GARUDA 2-3 PASS: 60/120Hz, both directions, five actions, active fade, egg/rebirth/death and pool reuse");
        }
        finally
        {
            Engine.PhysicsTicksPerSecond = 60;
            if (GF.Base.IsGamePaused)
            {
                GF.Base.ResumeGame();
            }

            if (_monster is { IsShown: true })
            {
                ReleaseTestEntity(_monster);
            }

            _monster = null;
            system.RemoveSource(source);
            system.Heal(int.MaxValue);
        }
    }

    /// <summary>
    /// 夹具通过正式受击入口提交一次独立真实伤害，不绕过化卵规则。
    /// </summary>
    /// <param name="actor">受测首领。</param>
    /// <param name="attackerId">英雄实体编号。</param>
    private static void ApplyGarudaTestHit(AbstractEnemyEntity actor, int attackerId)
    {
        var data = AttackData.Create(0, default, 100000f, DamageKind.Real, Vector2.Zero, 1, 0, source: new GameLogic.Battle.Attacks.AttackSource(attackerId, GameConfig.Entity.EntityId.None, 0, 0));
        try
        {
            actor.ReceiveHit(data);
        }
        finally
        {
            ReferencePool.Release(data);
        }
    }
}
