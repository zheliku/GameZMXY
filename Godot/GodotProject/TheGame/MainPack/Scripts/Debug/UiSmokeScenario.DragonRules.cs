using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GameConfig.Attributes;
using GameConfig.Entity;
using GameFramework.Fsm;
using GameLogic;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attributes;
using GameLogic.Config;
using GameLogic.Enemies;
using GameLogic.Enemies.AI;
using GameLogic.Heroes;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.Skills;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 龙王官方距离分支、落地施法移动和真实冻结时间；观测与强制边界只在测试侧。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 以20/21级分界、两种物理频率和两侧目标验证真实龙王。
    /// </summary>
    /// <param name="hero">本关实际悟空。</param>
    /// <param name="flow">本局共享业务作用域。</param>
    /// <returns>全部规则和原生动作回归完成。</returns>
    private async Task CheckDragonRulesAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        HeroSystem system = flow.GetSystem<HeroSystem>();
        HeroStatusSystem status = flow.GetSystem<HeroStatusSystem>();
        GameData snapshot = flow.GetUtility<SaveDataUtility>().Capture(flow.GetModel<ProfileModel>(), flow.GetModel<SkillModel>());
        var source = new AttributeSourceId("DragonRules", 1);
        system.SetSource(hero.HeroId, source, [AttributeModifier.Flat(AttributeType.MaxHp, 100000)]);
        system.Heal(hero.HeroId, int.MaxValue);
        try
        {
            foreach (int tier in new[] { 20, 21 })
            {
                int experience = flow.GetModel<ConfigurationModel>().Tables.TbHeroLevelConfig.DataList
                    .Where(item => item.Id < tier).Sum(item => item.MaxExp);
                flow.GetSystem<ProfileSystem>().AddExperience(GF.Fsm.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1), experience - flow.GetModel<ProfileModel>().GetProgress(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.TotalExperience);
                foreach (int ticks in new[] { 60, 120 })
                {
                    Engine.PhysicsTicksPerSecond = ticks;
                    foreach (int direction in new[] { -1, 1 })
                    {
                        hero.SetPhysicsProcess(false);
                        hero.GlobalPosition = new Vector2(100000, 100000);
                        _monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.DragonKing,
                            new EnemySpawn(new Vector2(580, 100)));
                        AbstractEnemyEntity actor = _monster;
                        FsmState<IEnemyAiAgent> ai = SmokeInspection.ReadField<IFsm<IEnemyAiAgent>>(actor, "AiFsm").CurrentState;
                        SmokeInspection.FreezeAi(actor);
                        await WaitUntilAsync(() => actor.OnFloor, "龙王规则夹具未落地");
                        actor.SetPhysicsProcess(false);
                        CheckDragonDecisions(actor, hero, ai, tier, direction);
                        actor.AiData.AttackRequest = -1;
                        actor.SetPhysicsProcess(true);

                        // 从确有水平速度的状态起手，不能只测原本已经站定的施法。
                        for (int index = 0; index < 4; index++)
                        {
                            hero.GlobalPosition = actor.GlobalPosition + new Vector2(direction * 180, -1000);
                            typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(actor, [hero]);
                            actor.SetFacing(-direction);
                            actor.Velocity = new Vector2(direction * 96, 0);
                            float startX = actor.GlobalPosition.X;
                            Check(((IEnemyAiAgent)actor).RequestAttack(index), "龙王拒绝施法移动夹具请求");
                            await WaitUntilAsync(() => actor.AttackSegment == index, "龙王没有消费动作请求");
                            Check(actor.Facing == (index == 0 ? direction : -direction), "技能未沿用起手朝向或普攻未面向目标");
                            while (actor.AttackSegment >= 0)
                            {
                                Check(Math.Abs(actor.GlobalPosition.X - startX) < .5f,
                                    $"龙王落地施法滑行：tier={tier}, ticks={ticks}, side={direction}, attack={index}");
                                await WaitFramesAsync(1);
                            }
                            Check(actor.HitBox.GetChildren().OfType<CollisionShape2D>().All(shape => shape.Disabled),
                                "收招后七冰柱仍有启用判定");
                        }

                        if (tier == 20)
                        {
                            await CheckDragonIceGapAsync(actor, hero, direction, system);
                        }

                        // 使用实际暴雪命中和英雄物理时钟，冻结期间不冻结测试用身体。
                        hero.GlobalPosition = actor.GlobalPosition + new Vector2(direction * 120, 0);
                        hero.Velocity = Vector2.Zero;
                        hero.SetPhysicsProcess(true);
                        actor.SetFacing(direction);
                        typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(actor, [hero]);
                        system.AdvanceHurtProtection(hero.HeroId, float.MaxValue);
                        Check(((IEnemyAiAgent)actor).RequestAttack(2), "龙王未释放真实冻结");
                        await WaitUntilAsync(() => status.IsFrozen(hero.HeroId), "暴雪没有真实冻结悟空", 5000);
                        double frozenAt = hero.ActionData.Time;
                        GF.Base.PauseGame();
                        await WaitFramesAsync(3);
                        double remaining = flow.GetModel<HeroModel>().GetState(hero.HeroId).GetStatusEffect(HeroState.StatusEffectKind.Freeze).RemainingSeconds;
                        await WaitFramesAsync(10);
                        Check(flow.GetModel<HeroModel>().GetState(hero.HeroId).GetStatusEffect(HeroState.StatusEffectKind.Freeze).RemainingSeconds == remaining,
                            "暂停仍推进冻结时间");
                        GF.Base.ResumeGame();
                        await WaitUntilAsync(() => !status.IsFrozen(hero.HeroId), "冻结未按官方秒数结束", 7000);
                        double seconds = hero.ActionData.Time - frozenAt;
                        Check(Math.Abs(seconds - (tier == 20 ? 2 : 5)) < .08,
                            $"真实冻结时间错误：tier={tier}, actual={seconds}");
                        await WaitFramesAsync(2);
                        Check(!hero.GetNode<AnimatedSprite2D>("m_StatusEffects/m_FrozenEffect").Visible,
                            "控制恢复后冰块未隐藏");
                        await WaitUntilAsync(() => actor.AttackSegment < 0, "暴雪收招未结束");
                        hero.SetPhysicsProcess(false);
                        ReleaseTestEntity(actor);
                        _monster = null;
                        await WaitFramesAsync(3);
                        GD.Print($"SMOKE-DRAGON RULES PASS: level={tier}, ticks={ticks}, side={direction}, 4 stationary casts, source facing, freeze={seconds:F3}s and pause");
                    }
                }
            }
        }
        finally
        {
            if (GF.Base.IsGamePaused)
            {
                GF.Base.ResumeGame();
            }
            ReleaseTestEntity(_monster);
            _monster = null;
            hero.SetPhysicsProcess(false);
            status.AdvanceFreeze(hero.HeroId, 100);
            system.RemoveSource(hero.HeroId, source);
            flow.GetSystem<ProfileSystem>().RestoreCheckpoint(snapshot);
            Engine.PhysicsTicksPerSecond = originalTicks;
        }
    }

    /// <summary>
    /// 在真实冰环的独立柱间放入完整悟空受击盒，再移入冰柱确认实际伤害链路有效。
    /// </summary>
    /// <param name="actor">已停止AI的真实龙王。</param>
    /// <param name="hero">身体不推进的真实悟空。</param>
    /// <param name="direction">施法朝向。</param>
    /// <param name="system">重置测试受击保护的业务入口。</param>
    /// <returns>空隙不命中、柱内命中的验证完成。</returns>
    private async Task CheckDragonIceGapAsync(AbstractEnemyEntity actor, AbstractHeroEntity hero,
        int direction, HeroSystem system)
    {
        hero.GlobalPosition = new Vector2(100000, 100000);
        actor.SetFacing(direction);
        system.AdvanceHurtProtection(hero.HeroId, float.MaxValue);
        Check(((IEnemyAiAgent)actor).RequestAttack(1), "空隙夹具未释放冰环");
        await WaitUntilAsync(() => actor.AttackSegment == 1, "空隙夹具未起手");
        float speed = actor.AnimPlayer.SpeedScale;
        bool checkedGap = false;
        try
        {
            while (actor.AttackSegment == 1 && !checkedGap)
            {
                CollisionShape2D[] shapes = actor.HitBox.GetChildren().OfType<CollisionShape2D>().Where(item => !item.Disabled)
                    .OrderBy(item => item.GlobalPosition.X).ToArray();
                Rect2 hurt = hero.HurtBox.GetGlobalBounds();
                for (int index = 0; index + 1 < shapes.Length; index++)
                {
                    Rect2 left = shapes[index].GlobalTransform * shapes[index].Shape.GetRect();
                    Rect2 right = shapes[index + 1].GlobalTransform * shapes[index + 1].Shape.GetRect();
                    if (right.Position.X - left.End.X < hurt.Size.X + 8)
                    {
                        continue;
                    }
                    Vector2 center = new((left.End.X + right.Position.X) / 2,
                        (Math.Max(left.Position.Y, right.Position.Y) + Math.Min(left.End.Y, right.End.Y)) / 2);
                    Rect2 candidate = new(center - hurt.Size / 2, hurt.Size);
                    if (shapes.Any(item => (item.GlobalTransform * item.Shape.GetRect()).Intersects(candidate)))
                    {
                        continue;
                    }

                    // 固定这个真实姿势，仅为物理服务器留足刷新时间；不在正式实体添加测试API。
                    actor.AnimPlayer.SpeedScale = 0;
                    int hp = hero.Hp;
                    hero.GlobalPosition = center - (hurt.GetCenter() - hero.GlobalPosition);
                    await WaitFramesAsync(10);
                    Check(hero.Hp == hp, "悟空处在冰柱可见空隙中仍然受伤");
                    await CaptureCombatAsync($"dragon_ice_gap_{Engine.PhysicsTicksPerSecond}_{direction}");
                    hero.GlobalPosition = shapes[index].GlobalPosition - (hero.HurtBox.GetGlobalBounds().GetCenter() - hero.GlobalPosition);
                    await WaitUntilAsync(() => hero.Hp < hp, "相同姿势下进入冰柱没有真实受击", 1000);
                    checkedGap = true;
                    break;
                }
                await WaitFramesAsync(1);
            }
        }
        finally
        {
            actor.AnimPlayer.SpeedScale = speed;
            hero.GlobalPosition = new Vector2(100000, 100000);
        }
        Check(checkedGap, "冰环夹具没有找到可容纳完整英雄的柱间空隙");
        await WaitUntilAsync(() => actor.AttackSegment < 0, "空隙夹具未正常收招");
        GD.Print($"SMOKE-DRAGON GAP PASS: {Engine.PhysicsTicksPerSecond}Hz, side={direction}, full hero outside all pillars stays unharmed, pillar contact damages");
    }

    /// <summary>
    /// 同步调用实际AI状态，比较官方100/450px严格分支、随机选招和两档冷却。
    /// </summary>
    /// <param name="actor">真实已落地龙王，身体暂不推进。</param>
    /// <param name="hero">提供实际目标原点。</param>
    /// <param name="state">本次入场由正式工厂创建的AI状态。</param>
    /// <param name="tier">当前20或21级边界。</param>
    /// <param name="direction">目标左右方向。</param>
    private static void CheckDragonDecisions(AbstractEnemyEntity actor, AbstractHeroEntity hero,
        FsmState<IEnemyAiAgent> state, int tier, int direction)
    {
        EnemyAiData data = actor.AiData;
        MethodInfo tick = state.GetType().GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic);
        int desire = SmokeInspection.ReadField<int>(state, "_attackDesire");
        Check(desire == (tier == 20 ? 40 : 20), "龙王出手欲望不符入场等级");
        foreach (float distance in new[] { 99.9f, 100f, 100.1f, 450f, 450.1f })
        {
            foreach (int ready in new[] { 1, 2, 3 })
            {
                data.AttackRequest = -1;
                for (int i = 0; i < data.Cooldowns.Length; i++)
                {
                    data.Cooldowns[i] = i == ready ? 0 : 100;
                }
                data.PreviousElapsedSeconds = .7;
                data.ElapsedSeconds = .71;
                data.ClockOrigin = 0;
                hero.GlobalPosition = actor.GlobalPosition + new Vector2(direction * distance, -2000);
                typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(actor, [hero]);
                tick.Invoke(state, [null, actor, 1f / 120]);
                int expected = distance <= 100 ? ready == 3 ? 3 : -1 : distance <= 450 && ready is 1 or 2 ? ready : -1;
                Check(data.AttackRequest == expected, $"龙王AI距离分支错误：distance={distance}, ready={ready}");
                if (expected >= 0)
                {
                    float frames = ready == 1 ? tier == 20 ? 700 : 550 : ready == 2 ? tier == 20 ? 500 : 400 : tier == 20 ? 400 : 300;
                    Check(Math.Abs(data.Cooldowns[ready] - (frames - 1) / 24f) < .0001f, "龙王冷却不符原版同一步末扣减");
                }
                else if (distance > 100)
                {
                    Check(data.MoveIntent == direction, "中远距离冷却时没有按原版追击");
                }
            }
        }
        for (ulong seed = 0; seed < 16; seed++)
        {
            data.AttackRequest = -1;
            Array.Fill(data.Cooldowns, 0);
            hero.GlobalPosition = actor.GlobalPosition + new Vector2(direction * 200, 0);
            typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(actor, [hero]);
            GD.Seed(seed);
            int expected = GD.Randf() < .5f ? 1 : 2;
            GD.Seed(seed);
            tick.Invoke(state, [null, actor, 1f / 120]);
            Check(data.AttackRequest == expected, "中距离两技能均就绪时不是50%选招");
        }

        // 冷却中只在近身1.5秒/中距离1秒决策，不能每个渲染帧都掷普攻概率。
        foreach (float distance in new[] { 80f, 200f })
        {
            double period = distance < 100 ? 1.5 : 1;
            foreach (bool due in new[] { false, true })
            {
                int attacks = 0;
                for (ulong seed = 0; seed < 32; seed++)
                {
                    data.AttackRequest = -1;
                    Array.Fill(data.Cooldowns, 100);
                    data.ClockOrigin = 0;
                    data.PreviousElapsedSeconds = period - .01;
                    data.ElapsedSeconds = period + (due ? .001 : -.001);
                    data.PatrolStopTime = 4;
                    data.MoveIntent = 0;
                    hero.GlobalPosition = actor.GlobalPosition + new Vector2(direction * distance, 0);
                    typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(actor, [hero]);
                    GD.Seed(seed);
                    bool expected = due && GD.Randf() * 100 < desire;
                    GD.Seed(seed);
                    tick.Invoke(state, [null, actor, 1f / 120]);
                    Check(data.AttackRequest == (expected ? 0 : -1), "龙王冷却中普攻节拍/欲望错误");
                    if (expected)
                    {
                        attacks++;
                    }
                    if (!due)
                    {
                        Check(data.MoveIntent == (distance < 100 ? 0 : direction), "非决策步的近身游荡/中距离追击错误");
                    }
                }
                Check(!due || attacks is > 0 and < 32, "随机夹具未同时覆盖出手和不出手");
            }
        }
        data.AttackRequest = -1;
    }
}
