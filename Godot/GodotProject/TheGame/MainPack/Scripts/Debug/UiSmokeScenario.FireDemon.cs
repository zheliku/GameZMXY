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
/// 按真实输入记录火魔斩完整轨迹；覆盖无目标、命中、左右朝向、复用与60/120Hz。
/// </summary>
public sealed partial class UiSmokeScenario
{
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
                foreach (int direction in new[] { 1, -1 })
                    foreach (bool target in new[] { false, true })
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
                        if (target)
                        {
                            m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.Gorilla,
                                new EnemySpawn(hero.GlobalPosition + new Vector2(direction * 80, -190), 100));
                            SmokeInspection.FreezeAi(m_Monster);
                            m_Monster.SetPhysicsProcess(false);
                        }
                        int hp = m_Monster?.Hp ?? 0;
                        heroSystem.RestoreMp(int.MaxValue);
                        int mana = vitals.Mp.Value;
                        var rows = new List<object>();
                        var effects = SmokeInspection.ReadField<HashSet<SkillEffectNode>>(hero, "m_SkillEffects");
                        string label = $"{ticks}_{direction}_{target}";
                        double start = hero.ActionData.Time, previous = -1, ended = double.PositiveInfinity, captureAt = 0;
                        bool descended = false, movedDown = false, contacted = false, roseAgain = false, combo = false, landing = false;
                        bool invalidEffect = false; // 对照原版九连/落地保留角色镜像、下冲反向和目标原点，不能只验证招式结束。
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
                                return;
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
                                    invalidEffect |= Math.Sign(effect.Scale.X) != -direction || !Near(Math.Abs(effect.Scale.X), 1) ||
                                        !Near(effect.Scale.Y, 1);
                                if (hit == (int)SkillHitId.FireDemonCombo)
                                    invalidEffect |= m_Monster == null || effect.GlobalPosition.DistanceTo(m_Monster.GlobalPosition) > .1f;
                                if (hit == (int)SkillHitId.FireDemonLanding)
                                {
                                    invalidEffect |= !effect.GetNode<Node2D>("Visual").Scale.IsEqualApprox(new Vector2(1.4f, 2));
                                    invalidEffect |= effect.GlobalPosition.DistanceTo(hero.GlobalPosition) > .1f;
                                }
                                if (hit == (int)SkillHitId.FireDemonDescent)
                                    invalidEffect |= Math.Sign(effect.Scale.X) != direction;
                            }
                            if (time > .15 && prelude.Visible &&
                                (!previousPreludeVisible || prelude.Frame < previousPreludeFrame))
                                preludeRestarts++;
                            previousPreludeFrame = prelude.Frame;
                            previousPreludeVisible = prelude.Visible;
                            rows.Add(new
                            {
                                time,
                                x = hero.GlobalPosition.X,
                                y = hero.GlobalPosition.Y,
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
                                ended = Math.Min(ended, time);
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
                                    break;
                            }
                        }
                        finally
                        {
                            m_Driver.GetTree().PhysicsFrame -= SamplePhysics;
                        }
                        File.WriteAllText(Path.Combine(folder, label + ".json"), JsonConvert.SerializeObject(rows, Formatting.Indented));
                        if (!descended || !movedDown || roseAgain || invalidEffect || preludeRestarts != 0 || vitals.ActiveSkillId != null || !hero.OnFloor || !landing || combo != target ||
                            vitals.Mp.Value != mana - ConfigSystem.Instance.Tables.TbSkillConfig.Get(SkillId.FireDemonSlash).ManaCost ||
                            target && m_Monster.Hp >= hp)
                            failures.Add($"{label}: descent={descended}, movedDown={movedDown}, rebound={roseAgain}, invalidEffect={invalidEffect}, preludeRestarts={preludeRestarts}, active={vitals.ActiveSkillId}, floor={hero.OnFloor}, combo={combo}, landing={landing}");
                        GD.Print($"SMOKE-FIRE-MOTION: {label}, descended={descended}, movedDown={movedDown}, rebound={roseAgain}, preludeRestarts={preludeRestarts}, ended={ended:0.000}, samples={rows.Count}");
                        if (m_Monster != null)
                            ReleaseTestEntity(m_Monster);
                        m_Monster = null;
                        skills.CancelSkill();
                        await WaitFramesAsync(3);
                    }
            Check(failures.Count == 0, string.Join("; ", failures));
            GD.Print("SMOKE-FIRE-MOTION PASS: grounded starts, 60/120Hz, both directions, hit/miss, repeated casts, no second rise, one mana cost, landing damage and recovery.");
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
    private async Task WaitForFireHeroLandingAsync(AbstractHeroEntity hero)
    {
        for (int frame = 0; frame < 240 && !hero.OnFloor; frame++)
        {
            await WaitFramesAsync(1);
        }

        Check(hero.OnFloor, $"火魔轨迹夹具未落地（position={hero.GlobalPosition}, velocity={hero.Velocity}）");
    }
}
