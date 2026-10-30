using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GameConfig.Attributes;
using GameConfig.Entity;
using GameConfig.Skills;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attributes;
using GameLogic.Config;
using GameLogic.Effects;
using GameLogic.Enemies;
using GameLogic.Heroes;
using GameLogic.Input;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.Skills;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using Newtonsoft.Json;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 按真实输入记录火魔斩完整轨迹；覆盖源原点边界、命中、左右朝向、复用与60/120Hz。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 保留敌人真实身体运动，检查左右火魔九连击退与下冲、落地命中，不冻结受击位置。
    /// </summary>
    /// <param name="hero">本关实际悟空。</param>
    /// <param name="flow">本局模型与系统所属作用域。</param>
    /// <returns>八组真实输入和物理采样完成的任务。</returns>
    private async Task CheckFireDemonKnockbackAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        string folder = ProjectSettings.GlobalizePath("res://.godot/validation/fire_demon_knockback_20261031/traces");
        Directory.CreateDirectory(folder);
        int oldTicks = Engine.PhysicsTicksPerSecond;
        Vector2 origin = hero.GlobalPosition;
        var source = new AttributeSourceId("FireKnockbackSmoke", 1);
        var profile = flow.GetModel<ProfileModel>();
        var skills = flow.GetSystem<SkillSystem>();
        var vitals = flow.GetModel<HeroModel>();
        var heroSystem = flow.GetSystem<HeroSystem>();
        var snapshot = flow.GetUtility<SaveDataUtility>().Capture(profile, flow.GetModel<SkillModel>());
        var failures = new List<string>();
        try
        {
            // 只排除随机闪避和致死干扰，保留真实重力、硬直、位移、动画和判定。
            heroSystem.SetSource(source,
                [AttributeModifier.Flat(AttributeType.MaxMp, 1000), AttributeModifier.Flat(AttributeType.Htarget, 1)]);
            flow.GetSystem<ProfileSystem>().AddSoul(4000);
            skills.Learn(SkillId.FireDemonSlash);
            hero.SetPhysicsProcess(true);
            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                foreach (int direction in new[] { 1, -1 })
                {
                    foreach (EntityId enemy in new[] { EntityId.Gorilla, EntityId.HuaguoshanMonkey })
                    {
                        hero.GlobalPosition = new Vector2(direction > 0 ? 400 : 750, 280);
                        hero.Velocity = Vector2.Zero;
                        await WaitFramesAsync(3);
                        await WaitForFireHeroLandingAsync(hero);
                        hero.SetFacing(direction);
                        m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(enemy,
                            new EnemySpawn(hero.GlobalPosition + new Vector2(direction * 80, -160)));
                        SmokeInspection.StrengthenTrainingTarget(m_Monster);
                        SmokeInspection.FreezeAi(m_Monster);
                        m_Monster.SetPhysicsProcess(true);
                        heroSystem.RestoreMp(int.MaxValue);
                        var effects = SmokeInspection.ReadField<HashSet<SkillEffectNode>>(hero, "m_SkillEffects");
                        var rows = new List<object>();
                        bool comboHit = false, descentHit = false, landingHit = false, wrongDirection = false;
                        int firstDirection = 0, previousHp = m_Monster.Hp;
                        double start = hero.ActionData.Time;
                        string label = $"{ticks}_{direction}_{enemy}";
                        void SamplePhysics()
                        {
                            Vector2? pending = SmokeInspection.ReadField<Vector2?>(m_Monster, "m_PendingHurt");
                            bool damaged = previousHp != m_Monster.Hp;
                            if (damaged && firstDirection == 0)
                            {
                                firstDirection = Math.Sign(pending?.X ?? m_Monster.Velocity.X);
                            }

                            if (damaged && Math.Sign(pending?.X ?? m_Monster.Velocity.X) != direction)
                            {
                                wrongDirection = true;
                            }

                            foreach (var effect in effects)
                            {
                                var data = SmokeInspection.ReadField<GameLogic.Battle.Attacks.AttackData>(effect, "m_AttackData");
                                if (data == null || !SmokeInspection.ReadField<HashSet<ulong>>(data, "m_HitTargets")
                                    .Contains(m_Monster.GetInstanceId()))
                                {
                                    continue;
                                }

                                comboHit |= data.AttackId == (int)SkillHitId.FireDemonCombo;
                                descentHit |= data.AttackId == (int)SkillHitId.FireDemonDescent;
                                landingHit |= data.AttackId == (int)SkillHitId.FireDemonLanding;
                            }

                            rows.Add(new
                            {
                                time = hero.ActionData.Time - start,
                                hero = hero.GlobalPosition,
                                target = m_Monster.GlobalPosition,
                                velocity = m_Monster.Velocity,
                                pending,
                                hp = m_Monster.Hp,
                                damaged,
                                comboHit,
                                descentHit,
                                landingHit
                            });
                            previousHp = m_Monster.Hp;
                        }

                        // 按每个物理步采样，渲染帧率不能影响命中或漏掉短时击退请求。
                        m_Driver.GetTree().PhysicsFrame += SamplePhysics;
                        try
                        {
                            Input.ActionPress(InputSystem.Skill3);
                            await WaitUntilAsync(() => vitals.ActiveSkillId == SkillId.FireDemonSlash, "火魔击退专项未响应真实O输入");
                            Input.ActionRelease(InputSystem.Skill3);
                            await WaitUntilAsync(() => vitals.ActiveSkillId == null, "火魔击退专项未完成收招");
                            await WaitFramesAsync(4);
                        }
                        finally
                        {
                            m_Driver.GetTree().PhysicsFrame -= SamplePhysics;
                        }

                        File.WriteAllText(Path.Combine(folder, label + ".json"), JsonConvert.SerializeObject(rows, Formatting.Indented));
                        GD.Print($"SMOKE-FIRE-KNOCKBACK: {label}, first={firstDirection}, combo={comboHit}, descent={descentHit}, landing={landingHit}, target={m_Monster.GlobalPosition}, hero={hero.GlobalPosition}");
                        if (!comboHit || !descentHit || !landingHit || firstDirection != direction || wrongDirection)
                        {
                            failures.Add($"{label}: first={firstDirection}/{direction}, wrongDirection={wrongDirection}, combo={comboHit}, descent={descentHit}, landing={landingHit}");
                        }

                        ReleaseTestEntity(m_Monster);
                        m_Monster = null;
                        await WaitFramesAsync(3);
                    }
                }
            }

            Check(failures.Count == 0, string.Join("; ", failures));
            GD.Print("SMOKE-FIRE-KNOCKBACK PASS: eight live casts, both directions, 60/120Hz, two enemy sizes, facing knockback and actual descent/landing hits.");
        }
        finally
        {
            Input.ActionRelease(InputSystem.Skill3);
            Engine.PhysicsTicksPerSecond = oldTicks;
            ReleaseTestEntity(m_Monster);
            m_Monster = null;
            skills.CancelSkill();
            hero.SetPhysicsProcess(false);
            hero.GlobalPosition = origin;
            hero.Velocity = Vector2.Zero;
            heroSystem.RemoveSource(source);
            flow.GetSystem<ProfileSystem>().RestoreCheckpoint(snapshot);
        }
    }

    /// <summary>
    /// 每物理时刻读取位置和速度；测试不修改招式时序或生产动画资源。
    /// </summary>
    /// <param name="hero">本关实际悟空。</param>
    /// <param name="flow">本局作用域。</param>
    /// <returns>完整运动与收招断言完成的任务。</returns>
    private async Task CheckFireDemonMotionAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        string folder = ProjectSettings.GlobalizePath("res://.godot/validation/fire_demon_review/after_grounded");
        Directory.CreateDirectory(folder);
        int oldTicks = Engine.PhysicsTicksPerSecond;
        var source = new AttributeSourceId("FireMotionSmoke", 1);
        var profile = flow.GetModel<ProfileModel>();
        var skills = flow.GetSystem<SkillSystem>();
        var vitals = flow.GetModel<HeroModel>();
        var heroSystem = flow.GetSystem<HeroSystem>();
        var snapshot = flow.GetUtility<SaveDataUtility>().Capture(profile, flow.GetModel<SkillModel>());
        Vector2 origin = hero.GlobalPosition;
        var failures = new List<string>();
        try
        {
            heroSystem.SetSource(source, [AttributeModifier.Flat(AttributeType.MaxMp, 1000)]);
            flow.GetSystem<ProfileSystem>().AddSoul(4000);
            skills.Learn(SkillId.FireDemonSlash);
            hero.SetPhysicsProcess(true);
            foreach (int ticks in new[] { 60, 120 })
            {
                foreach (int direction in new[] { 1, -1 })
                {
                    foreach (var targetCase in new[]
                                    {
                        (Name: "none", OffsetY: (float?)null, Combo: false, Enemy: EntityId.Gorilla),
                        (Name: "center", OffsetY: (float?)-190, Combo: true, Enemy: EntityId.Gorilla),
                        (Name: "lower_edge", OffsetY: (float?)-43, Combo: false, Enemy: EntityId.Gorilla),
                        (Name: "upper_outside", OffsetY: (float?)-329, Combo: true, Enemy: EntityId.Gorilla),
                        (Name: "monkey_lower_edge", OffsetY: (float?)-30, Combo: false, Enemy: EntityId.HuaguoshanMonkey)
                    })
                    {
                        Engine.PhysicsTicksPerSecond = ticks;
                        hero.GlobalPosition = new Vector2(direction > 0 ? 400 : 750, 280);
                        hero.Velocity = Vector2.Zero;
                        // 瞬移后先让MoveAndSlide刷新地面事实，不能拿上次施放的IsOnFloor作为本次起手条件。
                        await WaitFramesAsync(3);
                        await WaitForFireHeroLandingAsync(hero);
                        await WaitFramesAsync(2);
                        Check(hero.OnFloor, "火魔轨迹夹具未从稳定地面起手");
                        hero.SetFacing(direction);
                        float floorY = hero.GlobalPosition.Y;
                        if (targetCase.OffsetY.HasValue)
                        {
                            m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(targetCase.Enemy,
                                new EnemySpawn(hero.GlobalPosition + new Vector2(direction * 80, targetCase.OffsetY.Value)));
                            SmokeInspection.StrengthenTrainingTarget(m_Monster);
                            SmokeInspection.FreezeAi(m_Monster);
                            m_Monster.SetPhysicsProcess(false);
                        }
                        int hp = m_Monster?.Hp ?? 0;
                        heroSystem.RestoreMp(int.MaxValue);
                        int mana = vitals.Mp.Value;
                        var rows = new List<object>();
                        var effects = SmokeInspection.ReadField<HashSet<SkillEffectNode>>(hero, "m_SkillEffects");
                        string label = $"{ticks}_{direction}_{targetCase.Name}";
                        double start = hero.ActionData.Time, previous = -1, ended = double.PositiveInfinity, captureAt = 0;
                        bool descended = false, movedDown = false, contacted = false, roseAgain = false, combo = false, landing = false;
                        bool invalidEffect = false; // 对照原版九连/落地保留角色镜像、下冲反向和目标原点，不能只验证招式结束。
                        var invalidReasons = new HashSet<string>(StringComparer.Ordinal);
                        void MarkInvalid(string reason)
                        {
                            invalidEffect = true;
                            invalidReasons.Add(reason);
                        }
                        int frame = 0;
                        Input.ActionPress(InputSystem.Skill3);
                        await WaitUntilAsync(() => vitals.ActiveSkillId == SkillId.FireDemonSlash, "实际O输入未释放火魔斩");
                        Input.ActionRelease(InputSystem.Skill3);
                        var prelude = hero.GetNode<AnimatedSprite2D>("m_FireDemonPrelude");
                        int previousPreludeFrame = prelude.Frame, preludeRestarts = 0;
                        bool previousPreludeVisible = prelude.Visible;
                        // 渲染/截图可跳过多个物理步；用原生物理信号记录每一步，避免漏掉一帧反弹。
                        void SamplePhysics()
                        {
                            double time = hero.ActionData.Time - start;
                            if (hero.ActionData.Time == previous)
                            {
                                return;
                            }

                            previous = hero.ActionData.Time;
                            bool active = vitals.ActiveSkillId == SkillId.FireDemonSlash;
                            double position = active ? hero.AnimPlayer.CurrentAnimationPosition : -1;
                            descended |= active && position >= 46d / 24d - .00001;
                            movedDown |= descended && active && hero.Velocity.Y > 400;
                            contacted |= descended && hero.OnFloor;
                            roseAgain |= descended && hero.Velocity.Y < -.5f || contacted && hero.GlobalPosition.Y < floorY - 1;
                            combo |= effects.Any(x => SmokeInspection.ReadField<GameConfig.Battle.AttackConfig>(x, "m_Hit")?.Id == (int)SkillHitId.FireDemonCombo);
                            landing |= effects.Any(x => SmokeInspection.ReadField<GameConfig.Battle.AttackConfig>(x, "m_Hit")?.Id == (int)SkillHitId.FireDemonLanding);
                            foreach (var effect in effects)
                            {
                                int hit = SmokeInspection.ReadField<GameConfig.Battle.AttackConfig>(effect, "m_Hit")?.Id ?? 0;
                                if (hit == (int)SkillHitId.FireDemonCombo || hit == (int)SkillHitId.FireDemonLanding)
                                {
                                    if (Math.Sign(effect.Scale.X) != -direction || !Near(Math.Abs(effect.Scale.X), 1) ||
                                        !Near(effect.Scale.Y, 1))
                                    {
                                        MarkInvalid($"scale/{hit}");
                                    }
                                }
                                if (hit == (int)SkillHitId.FireDemonCombo)
                                {
                                    if (m_Monster == null || effect.GlobalPosition.DistanceTo(m_Monster.GlobalPosition) > .1f)
                                    {
                                        MarkInvalid("combo_target");
                                    }
                                }
                                if (hit == (int)SkillHitId.FireDemonLanding)
                                {
                                    if (!effect.GetNode<Node2D>("Origin/Visual").Scale.IsEqualApprox(new Vector2(1.4f, 2)))
                                    {
                                        MarkInvalid("landing_scale");
                                    }

                                    if (!effect.GetNode<Node2D>("Origin").Position.IsZeroApprox())
                                    {
                                        MarkInvalid("landing_source_origin");
                                    }
                                    // 效果固定在创建点；之后的 MoveAndSlide 可在两侧 SafeMargin 内恢复胶囊接触。
                                    float anchorGap = effect.GlobalPosition.DistanceTo(((ISkillActor)hero).SkillPosition);
                                    if (anchorGap > hero.SafeMargin * 2 + .001f)
                                    {
                                        MarkInvalid($"landing_anchor/{anchorGap:0.000}/{effect.GlobalPosition}/{hero.GlobalPosition}");
                                    }
                                }
                                if (hit == (int)SkillHitId.FireDemonDescent)
                                {
                                    if (Math.Sign(effect.Scale.X) != direction)
                                    {
                                        MarkInvalid("descent_scale");
                                    }
                                }
                            }
                            if (time > .15 && prelude.Visible &&
                                (!previousPreludeVisible || prelude.Frame < previousPreludeFrame))
                            {
                                preludeRestarts++;
                            }

                            previousPreludeFrame = prelude.Frame;
                            previousPreludeVisible = prelude.Visible;
                            rows.Add(new
                            {
                                time,
                                x = hero.GlobalPosition.X,
                                y = hero.GlobalPosition.Y,
                                actionX = hero.GlobalPosition.X,
                                actionY = hero.GlobalPosition.Y,
                                targetX = m_Monster?.GlobalPosition.X,
                                targetY = m_Monster?.GlobalPosition.Y,
                                vx = hero.Velocity.X,
                                vy = hero.Velocity.Y,
                                active,
                                position,
                                animation = hero.AnimPlayer.CurrentAnimation.ToString(),
                                floor = hero.OnFloor,
                                preludeFrame = prelude.Frame,
                                preludeVisible = prelude.Visible
                            });
                            if (!active)
                            {
                                ended = Math.Min(ended, time);
                            }
                        }
                        m_Driver.GetTree().PhysicsFrame += SamplePhysics;
                        try
                        {
                            while (hero.ActionData.Time - start < 7)
                            {
                                await WaitFramesAsync(1);
                                double time = hero.ActionData.Time - start;
                                if (time >= captureAt && ticks == 60)
                                {
                                    await m_Driver.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                                    using var image = m_Driver.GetViewport().GetTexture().GetImage();
                                    image.SavePng(Path.Combine(folder, $"{label}_{frame++:000}.png"));
                                    captureAt = time + .1;
                                }
                                if (time > ended + .75 && hero.OnFloor)
                                {
                                    break;
                                }
                            }
                        }
                        finally
                        {
                            m_Driver.GetTree().PhysicsFrame -= SamplePhysics;
                        }
                        File.WriteAllText(Path.Combine(folder, label + ".json"), JsonConvert.SerializeObject(rows, Formatting.Indented));
                        int expectedMana = mana - ConfigSystem.Instance.Tables.TbSkillConfig.Get(SkillId.FireDemonSlash).ManaCost;
                        bool manaMismatch = vitals.Mp.Value != expectedMana;
                        bool damageMismatch = targetCase.Combo && m_Monster.Hp >= hp;
                        string effectSummary = string.Join(" | ", effects.Select(effect =>
                        {
                            int hit = SmokeInspection.ReadField<GameConfig.Battle.AttackConfig>(effect, "m_Hit")?.Id ?? 0;
                            Vector2 visualScale = effect.GetNodeOrNull<Node2D>("Origin/Visual")?.Scale ?? Vector2.Zero;
                            return $"hit={hit},pos={effect.GlobalPosition},scale={effect.Scale},visual={visualScale},targetGap={(m_Monster == null ? 0 : effect.GlobalPosition.DistanceTo(m_Monster.GlobalPosition)):0.###},heroGap={effect.GlobalPosition.DistanceTo(((ISkillActor)hero).SkillPosition):0.###}";
                        }));
                        if (!descended || !movedDown || roseAgain || invalidEffect || preludeRestarts != 0 || vitals.ActiveSkillId != null || !hero.OnFloor || !landing || combo != targetCase.Combo || manaMismatch || damageMismatch)
                        {
                            failures.Add($"{label}: descent={descended}, movedDown={movedDown}, rebound={roseAgain}, invalidEffect={invalidEffect}, reasons={string.Join(',', invalidReasons)}, preludeRestarts={preludeRestarts}, active={vitals.ActiveSkillId}, floor={hero.OnFloor}, combo={combo}/{targetCase.Combo}, landing={landing}, mana={vitals.Mp.Value}/{expectedMana}, hp={m_Monster?.Hp}/{hp}, effects=[{effectSummary}]");
                        }

                        GD.Print($"SMOKE-FIRE-MOTION: {label}, descended={descended}, movedDown={movedDown}, rebound={roseAgain}, preludeRestarts={preludeRestarts}, ended={ended:0.000}, samples={rows.Count}");
                        if (m_Monster != null)
                        {
                            ReleaseTestEntity(m_Monster);
                        }

                        m_Monster = null;
                        skills.CancelSkill();
                        await WaitFramesAsync(3);
                    }
                }
            }

            Check(failures.Count == 0, string.Join("; ", failures));
            GD.Print("SMOKE-FIRE-MOTION PASS: 20 source-origin/target-boundary casts, 60/120Hz, both directions, no second rise, one mana cost, landing damage and recovery.");
        }
        finally
        {
            Input.ActionRelease(InputSystem.Skill3);
            Engine.PhysicsTicksPerSecond = oldTicks;
            ReleaseTestEntity(m_Monster);
            m_Monster = null;
            skills.CancelSkill();
            hero.SetPhysicsProcess(false);
            hero.GlobalPosition = origin;
            hero.Velocity = Vector2.Zero;
            heroSystem.RemoveSource(source);
            flow.GetSystem<ProfileSystem>().RestoreCheckpoint(snapshot);
        }
    }

    /// <summary>
    /// 以有限物理帧等待火魔夹具落地，避免地面绑定异常时烟测永久等待。
    /// </summary>
    /// <param name="hero">本次火魔测试英雄。</param>
    /// <returns>英雄落地后完成的任务；超出有限帧预算时验证失败。</returns>
    private async Task WaitForFireHeroLandingAsync(AbstractHeroEntity hero)
    {
        for (int frame = 0; frame < 240 && !hero.OnFloor; frame++)
        {
            await WaitFramesAsync(1);
        }

        Check(hero.OnFloor, $"火魔轨迹夹具未落地（position={hero.GlobalPosition}, velocity={hero.Velocity}）");
    }
}
