using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GameConfig.Battle;
using GameConfig.Entity;
using GameFramework;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Damage;
using GameLogic.Enemies;
using GameLogic.Enemies.AI;
using GameLogic.Heroes;
using GameLogic.Level.Traps;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 第四关的实际陷阱、飞行身体与静止石像回归；内部事实只由测试反射读取。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 独立验证尖刺伤害周期、预警下落、暂停清理与两种敌方身体。
    /// </summary>
    private async Task CheckBatCaveAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        var heroSystem = flow.GetSystem<HeroSystem>();
        heroSystem.Heal(int.MaxValue);
        hero.GlobalPosition = new Vector2(500, 400);
        hero.Velocity = Vector2.Zero;
        var packed = GD.Load<PackedScene>("res://TheGame/Scenes/Traps/bat_cave_spike.tscn");
        var spike = packed.Instantiate<BatCaveSpikeNode>();
        spike.Position = new Vector2(500, 370);
        spike.ProcessMode = Node.ProcessModeEnum.Disabled;
        m_Driver.GetTree().Root.AddChild(spike);
        Check(!spike.GetNode<RayCast2D>("Trigger").Enabled, "地刺不需要启用悬顶检测射线");
        try
        {
            int hp = hero.Hp;
            await WaitFramesAsync(10);
            Check(hero.Hp == hp, "加载期尖刺造成了伤害");
            flow.GetSystem<GameLogic.Skills.SkillSystem>().GrantInvulnerability(hero.ActionData.Time, 100);
            spike.ProcessMode = Node.ProcessModeEnum.Inherit;
            await WaitFramesAsync(8);
            Check(hero.Hp == hp, "尖刺穿透了英雄无敌");
            flow.GetSystem<GameLogic.Skills.SkillSystem>().CancelSkill();
            await WaitUntilAsync(() => hero.Hp < hp, "尖刺在无敌结束后未命中");
            Check(hero.Hp == hp - 30, "尖刺没有造成固定30真实伤害");
            spike.SetPhysicsProcess(false);
            spike._PhysicsProcess(.5);
            Check(hero.Hp == hp - 30, "地刺同一编号重复命中");
            spike._PhysicsProcess(.6);
            Check(hero.Hp == hp - 60, "地刺一秒后没有更新攻击编号");
            spike.ProcessMode = Node.ProcessModeEnum.Disabled;
            await WaitFramesAsync(8);
            Check(hero.Hp == hp - 60, "停止会话后尖刺仍然伤害英雄");

        }
        finally
        {
            spike.QueueFree();
        }
        var ceiling = packed.Instantiate<BatCaveSpikeNode>();
        typeof(BatCaveSpikeNode).GetField("m_Suspended", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(ceiling, true);
        ceiling.Position = new Vector2(500, 100);
        hero.GlobalPosition = new Vector2(650, 400);
        m_Driver.GetTree().Root.AddChild(ceiling);
        try
        {
            var ray = ceiling.GetNode<RayCast2D>("Trigger");
            Check(ray.Enabled && ray.CollideWithAreas && !ray.CollideWithBodies && ray.TargetPosition.Y > 0,
                "悬顶刺未使用向下且只检测英雄受击区的射线");
            await WaitFramesAsync(8);
            Check(ceiling.GetNode<AnimatedSprite2D>("Sprite").Frame == 0, "英雄在射线旁边时误触发尖刺");
            hero.GlobalPosition = new Vector2(500, 0);
            await WaitFramesAsync(8);
            Check(ceiling.GetNode<AnimatedSprite2D>("Sprite").Frame == 0, "英雄在尖刺上方时误触发下落");
            hero.GlobalPosition = new Vector2(500, 400);
            await WaitUntilAsync(() => ceiling.GetNode<AnimatedSprite2D>("Sprite").Frame >= 2,
                "悬顶刺没有在横向经过时播放预警");
            Check(ceiling.Position.Y == 100, "悬顶刺在预警完成前下落");
            hero.GlobalPosition = new Vector2(800, 400);
            await WaitUntilAsync(() => ceiling.Position.Y > 100, "完整预警后悬顶刺没有下落");
            ceiling.SetPhysicsProcess(false);
            double elapsed = SmokeInspection.ReadField<double>(ceiling, "m_Elapsed");
            ceiling._PhysicsProcess(.25);
            double t = elapsed + .25;
            Check(Math.Abs(ceiling.Position.Y - (100 + 576 * t * (t / 2 + 1d / 48))) < .01,
                "悬顶刺的秒制加速度不符源公式");
            ceiling.SetPhysicsProcess(true);
            float y = ceiling.Position.Y;
            GF.Base.PauseGame();
            await WaitFramesAsync(10);
            Check(ceiling.Position.Y == y, "暂停时悬顶刺仍然下落");
            GF.Base.ResumeGame();
            await WaitUntilAsync(() => !ceiling.Visible, "悬顶刺越过Y500未隐藏");
            await WaitUntilAsync(() => ceiling.Visible && ceiling.Position.Y == 100, "悬顶刺越过Y1000未复位");
            ceiling.ProcessMode = Node.ProcessModeEnum.Disabled;
            await WaitFramesAsync(8);
            Check(!ceiling.CanProcess(), "停用节点后陷阱仍可推进");
        }
        finally
        {
            GF.Base.ResumeGame();
            ceiling.QueueFree();
        }
        GD.Print("SMOKE-SPIKES PASS: downward ray, above/side exclusion, loading, fixed damage, invulnerability retry, one-second cycle, full warning, gravity576, pause, hide/reset and cleanup.");

        heroSystem.Heal(int.MaxValue);
        hero.GlobalPosition = new Vector2(600, 400);
        m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.Bat,
            new EnemySpawn(new Vector2(400, 150)));
        await WaitUntilAsync(() => m_Monster.GlobalPosition.X > 430 && m_Monster.GlobalPosition.Y > 155,
            "蝙蝠没有通过真实AI追踪英雄并下降");
        Check(!m_Monster.OnFloor, "飞行追踪错误地依赖地面接触");
        SmokeInspection.FreezeAi(m_Monster);
        m_Monster.GlobalPosition = new Vector2(640, 380);
        m_Monster.Velocity = Vector2.Zero;
        m_Monster.SetFacing(-1);
        typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(m_Monster, [hero]);
        int previousHp = hero.Hp;
        Check(((IEnemyAiAgent)m_Monster).RequestAttack(0), "空中蝙蝠拒绝出招");
        await WaitUntilAsync(() => m_Monster.AttackSegment == 0, "蝙蝠身体没有进入攻击");
        await WaitUntilAsync(() => hero.Hp < previousHp, "蝙蝠空中攻击没有造成真实伤害");
        await WaitUntilAsync(() => SmokeInspection.BodyStateName(m_Monster) == "SkyMonsterMove", "蝙蝠收招没有返回飞行");
        var data = AttackData.Create(0, default, 1, DamageKind.Real, new Vector2(48, -72), hero.Id, 0);
        try
        { m_Monster.ReceiveHit(data, hero.Id); }
        finally { ReferencePool.Release(data); }
        await WaitUntilAsync(() => SmokeInspection.BodyStateName(m_Monster) == "Hurt", "蝙蝠未进入受击");
        await WaitUntilAsync(() => SmokeInspection.BodyStateName(m_Monster) == "SkyMonsterMove", "蝙蝠受击结束没有返回飞行");
        var bat = m_Monster;
        data = AttackData.Create(0, default, 10000, DamageKind.Real, Vector2.Zero, hero.Id, 0);
        try
        { bat.ReceiveHit(data, hero.Id); }
        finally { ReferencePool.Release(data); }
        await WaitUntilAsync(() => !bat.IsShown, "蝙蝠死亡动画结束未回池");
        m_Monster = null;
        GD.Print("SMOKE-BAT PASS: airborne pursuit, attack hit, recovery/hurt return to flight and death recycle.");

        m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.BatCaveStatue,
            new EnemySpawn(new Vector2(800, 300)));
        Vector2 origin = m_Monster.GlobalPosition;
        Check(m_Monster.MaxHp == 6000 && !((IEnemyAiAgent)m_Monster).RequestAttack(0), "石像生命或静止行为不符原版");
        var attacker = new CombatantAttributes(CombatSide.Hero, m_Monster.Level, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0);
        data = AttackData.Create(0, attacker, 30, DamageKind.Real, new Vector2(800, -800), hero.Id, 0);
        try
        { m_Monster.ReceiveHit(data, hero.Id); }
        finally { ReferencePool.Release(data); }
        await WaitFramesAsync(30);
        Check(m_Monster.Hp == 5970 && m_Monster.GlobalPosition.IsEqualApprox(origin),
            $"石像受击发生移动或未扣血：HP={m_Monster.Hp}，位置={m_Monster.GlobalPosition}，原点={origin}");
        ReleaseTestEntity(m_Monster);
        m_Monster = null;
        GD.Print("SMOKE-STATUE PASS: 6000 health, no attack, fixed position under knockback.");
    }

    /// <summary>
    /// 使用真实悟空受击盒核对音波边缘命中时点及上下留白；不修改攻击数值和运行时形状。
    /// </summary>
    /// <param name="hero">保持静止以测量判定时点的实际英雄。</param>
    /// <param name="flow">本局架构。</param>
    /// <returns>双朝向与60/120Hz回归完成的任务。</returns>
    private async Task CheckBatSonicTimingAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        int oldTicks = Engine.PhysicsTicksPerSecond;
        Vector2 origin = hero.GlobalPosition;
        var heroSystem = flow.GetSystem<HeroSystem>();
        var hp = flow.GetModel<HeroModel>().Hp;
        try
        {
            foreach (int ticks in new[] { 60, 120 })
                foreach (int direction in new[] { -1, 1 })
                    foreach (string sample in new[] { "edge", "above", "below" })
                    {
                        Engine.PhysicsTicksPerSecond = ticks;
                        heroSystem.Heal(int.MaxValue);
                        m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.Bat,
                            new EnemySpawn(new Vector2(500, 200)));
                        SmokeInspection.FreezeAi(m_Monster);
                        m_Monster.Velocity = Vector2.Zero;
                        // 靠近音波第二档下缘；旧四档判定要等到第六源帧才会覆盖此处。
                        Rect2 hurt = hero.HurtBox.GetGlobalBounds();
                        float left = direction < 0 ? 500 - 12 - hurt.Size.X : 500 + 12;
                        float top = sample == "above" ? 200 + 4 - hurt.Size.Y :
                            sample == "below" ? 200 + 82 : 200 + 25;
                        hero.GlobalPosition += new Vector2(left, top) - hurt.Position;
                        typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(m_Monster, [hero]);
                        int previousHp = hp.Value, hits = 0;
                        double firstHit = double.PositiveInfinity;
                        void OnHpChanged(int current)
                        {
                            if (current >= previousHp)
                                return;
                            previousHp = current;
                            hits++;
                            firstHit = Math.Min(firstHit, m_Monster.AnimPlayer.CurrentAnimationPosition);
                        }
                        hp.Register(OnHpChanged);
                        try
                        {
                            // 真正请求身体状态出招，让原生动画、物理重叠与ReceiveHit完成伤害。
                            Check(((IEnemyAiAgent)m_Monster).RequestAttack(0), "蝙蝠音波时序夹具拒绝出招");
                            await WaitUntilAsync(() => m_Monster.AttackSegment == 0, "蝙蝠未开始音波动作");
                            await WaitUntilAsync(() => m_Monster.AttackSegment < 0, "蝙蝠音波动作未收招");
                            GD.Print($"SMOKE-BAT-SONIC: {ticks}Hz facing={direction} {sample}, hits={hits}, first={firstHit:0.0000}s");
                            Check(sample == "edge" ? hits == 1 && firstHit <= 2d / 24d + 2d / ticks + .001 : hits == 0,
                                $"音波判定延迟或范围错误：{ticks}Hz/{direction}/{sample}, hits={hits}, first={firstHit:0.0000}s");
                        }
                        finally
                        {
                            hp.UnRegister(OnHpChanged);
                            ReleaseTestEntity(m_Monster);
                            m_Monster = null;
                        }
                    }
            GD.Print("SMOKE-BAT-SONIC PASS: both directions, 60/120Hz, early wave edge hits once within two physics steps; above/below air excluded; pool reuse.");
        }
        finally
        {
            ReleaseTestEntity(m_Monster);
            m_Monster = null;
            hero.GlobalPosition = origin;
            hero.Velocity = Vector2.Zero;
            heroSystem.Heal(int.MaxValue);
            Engine.PhysicsTicksPerSecond = oldTicks;
        }
    }
}
