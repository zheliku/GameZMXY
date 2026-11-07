using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameConfig;
using GameConfig.Battle;
using GameConfig.Entity;
using GameConfig.Skills;
using GameConfig.Sound;
using GameLogic;
using GameLogic.Architecture;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Damage;
using GameLogic.Config;
using GameLogic.Enemies;
using GameLogic.Enemies.Body;
using GameLogic.Entity;
using GameLogic.Equipment;
using GameLogic.Event.Combat;
using GameLogic.Heroes;
using GameLogic.Profile;
using GameLogic.Save;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using Newtonsoft.Json;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 用真实伤害入口验证受击保留朝向，击退仍沿攻击方向。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 用真实致死伤害检查三类地面小怪的抛飞、空中死亡循环、落地回收与一次性收益。
    /// </summary>
    /// <returns>六组60/120Hz物理验证完成的任务。</returns>
    private async Task CheckEnemyDeathMotionAsync()
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        try
        {
            foreach (int ticks in new[] { 60, 120 })
            {
                foreach (EntityId id in new[] { EntityId.HuaguoshanMonkey, EntityId.DemonMonkey, EntityId.Gorilla })
                {
                    Engine.PhysicsTicksPerSecond = ticks;
                    _monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(id,
                        new EnemySpawn(new Vector2(650, 200)));
                    SmokeInspection.FreezeAi(_monster);
                    await WaitUntilAsync(() => _monster.OnFloor, "死亡运动目标没有落地");
                    float floorY = _monster.GlobalPosition.Y;
                    EnemyBodyParams parameters = ((GameLogic.Enemies.Body.IEnemyBody)_monster).Params;
                    Check(parameters.DeathAirLoopTime.HasValue && Math.Abs(parameters.DeathAirLoopTime.Value - 8d / 24) < .001,
                        $"{id} 缺少第9源帧死亡循环起点");
                    int entityId = _monster.Id, deaths = 0;
                    void OnDeath(object sender, GameFramework.Event.GameEventArgs args)
                    {
                        if (args is EnemyDiedEventArgs death && death.EntityId == entityId)
                        {
                            deaths++;
                        }
                    }
                    GF.Event.Subscribe(EnemyDiedEventArgs.EventId, OnDeath);
                    try
                    {
                        var data = AttackData.Create(0, default, 100000, DamageKind.Real, new Vector2(240, -720), 1, 0);
                        try
                        {
                            Check(!_monster.ReceiveHit(data).IsMiss && _monster.Dead, $"{id} 没有进入致死流程");
                            Check(SmokeInspection.ReadField<HashSet<GameLogic.Effects.MonsterHitFeedbackNode>>(
                                _monster, "_hitFeedback").Count == 1, $"{id} 致死一击缺少官方命中闪光");
                        }
                        finally
                        {
                            GameFramework.ReferencePool.Release(data);
                        }
                        double previousTime = 0;
                        float rise = 0, speed = 0;
                        int loops = 0, frames = 0;
                        while (_monster.IsShown && frames++ < ticks * 4)
                        {
                            double time = _monster.AnimPlayer.CurrentAnimationPosition;
                            if (time + .02 < previousTime)
                            {
                                loops++;
                            }

                            previousTime = time;
                            rise = Math.Max(rise, floorY - _monster.GlobalPosition.Y);
                            speed = Math.Max(speed, Math.Abs(_monster.Velocity.X));
                            await WaitFramesAsync(1);
                        }
                        Check(!_monster.IsShown && loops > 0 && rise > 250 && speed > 100 && deaths == 1,
                            $"{id}/{ticks} 死亡运动或循环错误：shown={_monster.IsShown}, loops={loops}, rise={rise}, speed={speed}, deaths={deaths}");
                        GD.Print($"SMOKE-ENEMY-DEATH: {id}/{ticks}Hz, loops={loops}, rise={rise:0.00}, lethal speed={speed:0.00}, death events={deaths}");
                    }
                    finally
                    {
                        GF.Event.Unsubscribe(EnemyDiedEventArgs.EventId, OnDeath);
                        ReleaseTestEntity(_monster);
                        _monster = null;
                    }
                }
            }

            GD.Print("SMOKE-ENEMY-DEATH PASS: six lethal launch/air-loop/landing recycle cases; one death reward event each.");
        }
        finally
        {
            Engine.PhysicsTicksPerSecond = originalTicks;
            ReleaseTestEntity(_monster);
            _monster = null;
        }
    }

    /// <summary>
    /// 核对七段悟空技能的实际击退入口，并采样三种怪物的升龙与突刺轨迹。
    /// </summary>
    /// <returns>真实物理采样与对象池回收任务。</returns>
    private async Task CheckSkillImpactsAsync()
    {
        Tables tables = ConfigSystem.Instance.Tables;
        AbstractHeroEntity hero = FindHero();
        var traces = new List<object>();
        (SkillHitId, Vector2)[] stages = new[]
        {
            (SkillHitId.RisingDragon, new Vector2(48, -528)),
            (SkillHitId.FlameThrust, new Vector2(840, -48)),
            (SkillHitId.BlazingFlash, new Vector2(0, -48)),
            (SkillHitId.FlameStorm, new Vector2(480, -48)),
            (SkillHitId.FireDemonCombo, new Vector2(48, -72)),
            (SkillHitId.FireDemonDescent, new Vector2(960, 600)),
            (SkillHitId.FireDemonLanding, new Vector2(960, -360))
        };
        foreach (int id in new[] { (int)EntityId.HuaguoshanMonkey, (int)EntityId.DemonMonkey, (int)EntityId.Gorilla })
        {
            _monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(tables.TbEnemyConfig.Get(id).EntityId,
                new EnemySpawn(new Vector2(600, 300)));
            SmokeInspection.StrengthenTrainingTarget(_monster);
            SmokeInspection.FreezeAi(_monster);
            _monster.SetPhysicsProcess(false);
            foreach ((SkillHitId hit, Vector2 expected) in stages)
            {
                AttackConfig definition = tables.TbAttackConfig.Get((int)hit);
                var data = AttackData.Create((int)hit, default, 1, DamageKind.Real, definition.Knockback, 1, 0);
                _monster.ReceiveHit(data);
                GameFramework.ReferencePool.Release(data);
                Check(_monster.TakePendingHurt() == expected, $"{hit}/{id} 实际击退没有按造一24fps换算");
            }
            ReleaseTestEntity(_monster);
            _monster = null;

            // 每条轨迹从新取池开始，避免上一招硬直、落地或追击意图影响下一条。
            foreach (SkillHitId hit in new[] { SkillHitId.RisingDragon, SkillHitId.FlameThrust })
            {
                _monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(tables.TbEnemyConfig.Get(id).EntityId,
                    new EnemySpawn(new Vector2(600, 300)));
                SmokeInspection.StrengthenTrainingTarget(_monster);
                SmokeInspection.FreezeAi(_monster);
                _monster.SetPhysicsProcess(true);
                await WaitUntilAsync(() => _monster.OnFloor, "击退采样目标没有落地");
                Vector2 start = _monster.GlobalPosition;
                AttackConfig definition = tables.TbAttackConfig.Get((int)hit);
                var data = AttackData.Create((int)hit, default, 1, DamageKind.Real, definition.Knockback, 1, 0);
                _monster.ReceiveHit(data);
                GameFramework.ReferencePool.Release(data);
                await WaitUntilAsync(() => !_monster.HasPendingHurt, "身体状态没有消费技能击退");
                ulong frame = Engine.GetPhysicsFrames();
                float maximumRise = 0, maximumXSpeed = Math.Abs(_monster.Velocity.X);
                var samples = new List<object>();
                while (Engine.GetPhysicsFrames() - frame < (ulong)Engine.PhysicsTicksPerSecond * 3 / 2)
                {
                    await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
                    await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
                    Vector2 offset = _monster.GlobalPosition - start;
                    maximumRise = Math.Max(maximumRise, -offset.Y);
                    maximumXSpeed = Math.Max(maximumXSpeed, Math.Abs(_monster.Velocity.X));
                    samples.Add(new
                    {
                        Seconds = (double)(Engine.GetPhysicsFrames() - frame) / Engine.PhysicsTicksPerSecond,
                        X = offset.X,
                        Y = offset.Y,
                        Vx = _monster.Velocity.X,
                        Vy = _monster.Velocity.Y
                    });
                }
                EnemyBodyParams parameters = ((GameLogic.Enemies.Body.IEnemyBody)_monster).Params;
                // 第13帧一进入就返回wait，实际硬直是12/24秒，不能把终止帧再停留一帧。
                Check(parameters.Gravity == 864 && Math.Abs(parameters.HurtTime - .5d) < .001,
                    $"怪物重力或受击时长与官方源不符：{id}, gravity={parameters.Gravity}, hurt={parameters.HurtTime}");
                if (hit == SkillHitId.RisingDragon)
                {
                    Check(maximumRise > 170 && maximumRise < 175, $"升龙挑起高度异常：{id}/{maximumRise}");
                }
                else
                {
                    Check(Math.Abs(_monster.Velocity.X) <= 72.1f && maximumXSpeed < 841,
                                        "突刺击退没有衰减或被怪物体型放大");
                }

                traces.Add(new
                {
                    Monster = id,
                    Skill = hit.ToString(),
                    MaximumRise = maximumRise,
                    Samples = samples
                });
                ReleaseTestEntity(_monster);
                _monster = null;
            }
        }
        DirAccess.MakeDirRecursiveAbsolute("res://.godot/validation");
        using var file = FileAccess.Open("res://.godot/validation/skill_impacts.json", FileAccess.ModeFlags.Write);
        file.StoreString(JsonConvert.SerializeObject(traces, Formatting.Indented));
        GD.Print("SMOKE-SKILL-IMPACTS PASS: seven Wukong impact stages, three monster types, live launch and thrust trajectories.");
    }

    /// <summary>
    /// 验证悟空及三种怪物的两种朝向、两种受击方向，不增加生产观测接口。
    /// </summary>
    /// <param name="flow">创建受测英雄所需的档案作用域。</param>
    /// <returns>实体创建、检查与归还完成的任务。</returns>
    private async Task CheckHitFacingAsync(ProcedureOwner flow)
    {
        _extraHero = await ShowHeroAsync(flow, 0);
        CheckActor(_extraHero);
        ReleaseTestEntity(_extraHero);
        _extraHero = null;
        foreach (int id in new[] { (int)EntityId.HuaguoshanMonkey, (int)EntityId.DemonMonkey, (int)EntityId.Gorilla })
        {
            _monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(
                ConfigSystem.Instance.Tables.TbEnemyConfig.Get(id).EntityId, new EnemySpawn(new Vector2(200, 100)));
            SmokeInspection.FreezeAi(_monster);
            _monster.SetPhysicsProcess(false);
            CheckActor(_monster);
            ReleaseTestEntity(_monster);
            _monster = null;
        }
        GD.Print("SMOKE-UI: Wukong and three monsters retain facing on hits from either direction; knockback direction passed");

        void CheckActor(AbstractActorEntity actor)
        {
            foreach (int facing in new[] { -1, 1 })
            {
                foreach (int direction in new[] { -1, 1 })
                {
                    actor.SetFacing(facing);
                    var data = AttackData.Create(0, default, 1, DamageKind.Real,
                        new Vector2(2, -5), direction, 0);
                    DamageResult result = actor.ReceiveHit(data);
                    GameFramework.ReferencePool.Release(data);
                    Check(!result.IsMiss && actor.Facing == facing && actor.HasPendingHurt,
                        $"{actor.GetType().Name} 受击意外转身或未登记击退");
                    Vector2 knockback = actor.TakePendingHurt();
                    Check(Math.Sign(knockback.X) == direction, "保留朝向时错误反转了击退方向");
                    AnimationPlayer player = actor.GetNode<AnimationPlayer>("m_AnimPlayer");
                    player.Play("hurt");
                    player.Seek(1d / 24, true);
                    Check(actor.Facing == facing, "受击动画修改了角色朝向");
                }
            }
        }
    }
}
