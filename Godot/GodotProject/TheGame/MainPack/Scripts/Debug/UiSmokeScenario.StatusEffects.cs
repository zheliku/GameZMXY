using System;
using System.Threading.Tasks;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Heroes;
using GameLogic.Input;
using Godot;
using GodotGameFramework;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 本局状态在真实输入、物理与暂停中的检查，不增加生产观测入口。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 无双承伤保留当前动作与运动，播放白色外光、官方命中动画，并保留五击保护。
    /// </summary>
    /// <param name="hero">真实悟空实体。</param>
    /// <param name="flow">本局数据与规则所有者。</param>
    /// <returns>双向、60/120Hz受击及收尾验证。</returns>
    private async Task CheckMusouHurtAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        var model = flow.GetModel<HeroModel>();
        var system = flow.GetSystem<HeroSystem>();
        int originalTicks = Engine.PhysicsTicksPerSecond;
        try
        {
            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                hero.SetPhysicsProcess(true);
                hero.GlobalPosition = new Vector2(440, 200);
                hero.Velocity = Vector2.Zero;
                model.Miss.Value = 0;
                system.AdvanceHurtProtection(100);
                await WaitUntilAsync(() => hero.OnFloor, "无双夹具未落地");
                system.AddWsFromPickup(model.MaxWs.Value - model.Ws.Value);
                Check(hero.TryActivateMusou(), "无双夹具无法激活");
                await WaitFramesAsync(2);
                await WaitUntilAsync(() => !GF.Base.IsGamePaused, "无双开场没有结束");
                await WaitFramesAsync(2);
                var body = hero.GetNode<Sprite2D>("m_Body");
                var material = (ShaderMaterial)body.Material;
                var feedback = hero.GetNode<AnimatedSprite2D>("m_StatusEffects/m_HurtFeedback");
                int initialHp = hero.Hp;
                string state = SmokeInspection.BodyStateName(hero);
                Vector2 velocity = hero.Velocity;
                for (int hit = 0; hit < 5; hit++)
                {
                    Check(!HitHeroForProtection(hero, new Vector2(20, -4)), "无双被错误当成全程免伤");
                    Check(!hero.HasPendingHurt && hero.Velocity == velocity && SmokeInspection.BodyStateName(hero) == state,
                        "无双受击打断动作或产生击退");
                    Check(feedback.Visible && feedback.IsPlaying() && feedback.Frame == 0, "无双受击缺少官方闪光");
                }
                Check(hero.Hp == initialHp - 5 && HitHeroForProtection(hero), "无双期间连续受击保护不符合第五次规则");
                Check(material.GetShaderParameter("glow_color").AsColor() == Colors.White && material.GetShaderParameter("glow_radius").AsSingle() > 0,
                    "无双没有优先显示白色光晕");
                foreach (int direction in new[] { -1, 1 })
                {
                    hero.SetFacing(direction);
                    await WaitFramesAsync(2);
                    await CaptureCombatAsync($"musou_hurt_{ticks}_{direction}");
                }
                await WaitUntilAsync(() => !feedback.Visible, "受击闪光没有播完自动隐藏");
                system.AdvanceHurtProtection(100);
                Input.ActionPress(InputSystem.Attack);
                await WaitFramesAsync(2);
                Input.ActionRelease(InputSystem.Attack);
                await WaitFramesAsync(1);
                Check(SmokeInspection.BodyStateName(hero) == "Attack", "无双攻击夹具没有起手");
                int segment = hero.AttackSegment;
                double animationTime = hero.AnimPlayer.CurrentAnimationPosition;
                Check(!HitHeroForProtection(hero, new Vector2(20, -4)) && !hero.HasPendingHurt &&
                    hero.AttackSegment == segment && hero.AnimPlayer.CurrentAnimationPosition == animationTime,
                    "无双受击重启或打断正在播放的普攻");
                await WaitFramesAsync(2);
                Check(hero.AnimPlayer.CurrentAnimationPosition > animationTime && feedback.Visible, "无双受击没有继续推进原攻击和命中反馈");
                // 用普通受击保护覆盖最后的白光切换，验证恢复本角色颜色。
                for (int hit = 0; hit < 4; hit++)
                {
                    HitHeroForProtection(hero);
                }

                system.ClearWs();
                await WaitFramesAsync(2);
                Check(material.GetShaderParameter("glow_color").AsColor() == Colors.Red && material.GetShaderParameter("glow_radius").AsSingle() > 0,
                    "无双结束未恢复本角色受击保护颜色");
                system.AdvanceHurtProtection(100);
                await WaitFramesAsync(2);
                Check(material.GetShaderParameter("glow_radius").AsSingle() == 0, "无双/保护结束仍残留光晕");
                GD.Print($"SMOKE-MUSOU HURT {ticks}Hz PASS: damage, armor, white glow, 8 source frames, fifth-hit protection and color restore");
            }
        }
        finally
        {
            system.ClearWs();
            Engine.PhysicsTicksPerSecond = originalTicks;
        }
    }

    /// <summary>
    /// 用真实受击入口验证五次触发、后续免伤、技能区间互不覆盖和红色光晕的暂停清理。
    /// </summary>
    /// <param name="hero">本局英雄。</param>
    /// <param name="flow">持有本局数据的真实流程。</param>
    /// <returns>两个物理频率的命中与渲染验证任务。</returns>
    private async Task CheckHurtProtectionAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        var model = flow.GetModel<HeroModel>();
        var system = flow.GetSystem<HeroSystem>();
        int originalTicks = Engine.PhysicsTicksPerSecond;
        try
        {
            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                hero.SetPhysicsProcess(true);
                hero.GlobalPosition = new Vector2(440, 200);
                hero.Velocity = Vector2.Zero;
                model.Miss.Value = 0;
                await WaitUntilAsync(() => hero.OnFloor, "受击保护夹具没有落地");
                int initialHp = hero.Hp;
                for (int hit = 0; hit < 5; hit++)
                {
                    Check(!HitHeroForProtection(hero), "前五次命中被提前保护");
                }
                double protectedSeconds = model.HurtInvulnerabilitySeconds;
                bool sixthIgnored = HitHeroForProtection(hero);
                Check(hero.Hp == initialHp - 5 && Math.Abs(protectedSeconds - 2) < .02 && sixthIgnored,
                    $"第五次伤害或第六次免伤不符合官方规则：before={initialHp}, hp={hero.Hp}, seconds={protectedSeconds}, count={model.HurtHitCount}, sixthIgnored={sixthIgnored}");
                var material = (ShaderMaterial)hero.GetNode<Sprite2D>("m_Body").Material;
                Check(ReferenceEquals(material, hero.GetNode<Sprite2D>("m_Weapon").Material) &&
                    material.GetShaderParameter("glow_color").AsColor() == Colors.Red &&
                    material.GetShaderParameter("glow_radius").AsSingle() > 0,
                    "受击保护没有为身体和武器显示红色光晕");
                flow.GetSystem<GameLogic.Skills.SkillSystem>().GrantInvulnerability(hero.ActionData.Time, .2);
                await WaitFramesAsync(ticks / 4);
                Check(HitHeroForProtection(hero), "技能无敌结束覆盖了连续受击保护");
                foreach (int direction in new[] { -1, 1 })
                {
                    hero.SetFacing(direction);
                    await WaitFramesAsync(2);
                    await CaptureCombatAsync($"hurt_protection_{ticks}_{direction}");
                }
                GF.Base.PauseGame();
                await WaitFramesAsync(3);
                double paused = model.HurtInvulnerabilitySeconds;
                await WaitFramesAsync(8);
                Check(model.HurtInvulnerabilitySeconds == paused, "暂停仍扣减受击保护时间");
                GF.Base.ResumeGame();
                await WaitUntilAsync(() => model.HurtInvulnerabilitySeconds == 0, "受击保护没有按游戏秒数结束", 3500);
                Check(!HitHeroForProtection(hero) && hero.Hp == initialHp - 6 &&
                    material.GetShaderParameter("glow_radius").AsSingle() == 0,
                    "保护结束后仍免伤或保留红色光晕");
                system.AdvanceHurtProtection(1);
                GD.Print($"SMOKE-HURT-PROTECTION PASS: {ticks}Hz, fifth hit, sixth ignored, skill overlap, red two-layer glow, pause and expiry");
            }
        }
        finally
        {
            if (GF.Base.IsGamePaused)
            {
                GF.Base.ResumeGame();
            }
            Engine.PhysicsTicksPerSecond = originalTicks;
        }
    }

    /// <summary>
    /// 创建一次真实伤害命中；攻击包由夹具立即归池，不增加生产故障注入接口。
    /// </summary>
    /// <param name="hero">受测英雄。</param>
    /// <param name="knockback">可选击退系数，用于确认霸体不会消费位移。</param>
    /// <returns>本次是否被无敌拦截。</returns>
    private static bool HitHeroForProtection(AbstractHeroEntity hero, Vector2? knockback = null)
    {
        var attacker = new GameLogic.Battle.Damage.CombatantAttributes(GameLogic.Battle.Damage.CombatSide.Enemy,
            hero.Level, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var data = GameLogic.Battle.Attacks.AttackData.Create(0, attacker, 1, GameConfig.Battle.DamageKind.Real,
            knockback ?? Vector2.Zero, 1, 0);
        try
        {
            return hero.ReceiveHit(data, 0).IsMiss;
        }
        finally
        {
            GameFramework.ReferencePool.Release(data);
        }
    }

    /// <summary>
    /// 验证冰冻禁止控制但允许原生待机、重力和计时，解冻后不回放锁定期间的输入。
    /// </summary>
    /// <param name="hero">本局真实英雄。</param>
    /// <param name="flow">所属英雄模型和规则作用域。</param>
    /// <returns>两种物理频率与死亡清理检查完成。</returns>
    private async Task CheckFreezeAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        var system = flow.GetSystem<HeroSystem>();
        var model = flow.GetModel<HeroModel>();
        int originalTicks = Engine.PhysicsTicksPerSecond;
        hero.SetPhysicsProcess(true);
        try
        {
            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                hero.GlobalPosition = new Vector2(430, 300);
                hero.Velocity = Vector2.Zero;
                // 传送后的CharacterBody2D仍带上一轮落地缓存，先完成真实物理帧再等新接触。
                await WaitFramesAsync(3);
                await WaitUntilAsync(() => hero.OnFloor, "冰冻夹具没有实际落地");
                Input.ActionPress(InputSystem.MoveRight);
                await WaitFramesAsync(10);
                model.Ws.Value = model.MaxWs.Value;
                system.ApplyFreeze(2);
                Check(!hero.TryActivateMusou() && !model.WsActive.Value,
                    "冰冻期间鼠标调用的无双入口仍可激活");
                Input.ActionPress(InputSystem.Attack);
                Input.ActionPress(InputSystem.Jump);
                Input.ActionPress(InputSystem.Musou);
                await WaitFramesAsync(3);
                var frozen = SmokeInspection.ReadField<AnimatedSprite2D>(hero, "m_FrozenEffect");
                Check(frozen.Visible && frozen.SpriteFrames.GetFrameCount("play") == 35 &&
                    frozen.Position == new Vector2(-90, -115) &&
                    frozen.Material is CanvasItemMaterial { BlendMode: CanvasItemMaterial.BlendModeEnum.Add },
                    "冰冻缺少原版35帧、位置或加法混合表现");
                float x = hero.GlobalPosition.X;
                double animationTime = hero.AnimPlayer.CurrentAnimationPosition;
                await WaitFramesAsync(6);
                Check(Math.Abs(hero.GlobalPosition.X - x) < .01f && hero.ActionData.MoveAxis == 0 &&
                    hero.AttackSegment < 0 && !model.WsActive.Value && hero.OnFloor,
                    "冰冻期间仍可移动、跳跃、普攻或开启无双");
                Check(hero.AnimPlayer.CurrentAnimationPosition != animationTime, "冰冻错误地暂停了原生动作");
                GF.Base.PauseGame();
                await WaitFramesAsync(3);
                float paused = model.FreezeSeconds;
                await WaitFramesAsync(10);
                Check(model.FreezeSeconds == paused, "游戏暂停仍推进冰冻时长");
                GF.Base.ResumeGame();
                await WaitUntilAsync(() => frozen.Frame == 34 && !frozen.IsPlaying(), "冰块原版最后一帧未完整播放", 2500);
                Check(frozen.Visible && model.FreezeSeconds > 0 && !frozen.IsPlaying(), "冰块没有保持末帧到状态结束");
                if (ticks == 60)
                {
                    await CaptureCombatAsync("hero_frozen_official");
                }

                await WaitUntilAsync(() => model.FreezeSeconds == 0, "冰冻没有按游戏秒数结束", 4000);
                Check(hero.AttackSegment < 0 && hero.OnFloor && !frozen.Visible, "解冻后回放了期间的输入或保留冰块");
                ReleaseFreezeInputs();
                await WaitFramesAsync(3);
                Input.ActionPress(InputSystem.MoveRight);
                await WaitFramesAsync(8);
                Check(hero.GlobalPosition.X > x + 1, "解冻后普通移动没有恢复");
                ReleaseFreezeInputs();
                GD.Print($"SMOKE-FREEZE {ticks}Hz PASS: keyboard and UI activation blocked, native animation, no queued actions, pause and thaw");
            }
            system.ApplyFreeze(5);
            system.Damage(int.MaxValue);
            Check(model.FreezeSeconds == 0, "死亡未清理冰冻");
            GD.Print("SMOKE-FREEZE DEATH PASS: no persistent freeze after hero depletion");
        }
        finally
        {
            if (GF.Base.IsGamePaused)
            {
                GF.Base.ResumeGame();
            }

            ReleaseFreezeInputs();
            Engine.PhysicsTicksPerSecond = originalTicks;
        }
    }

    /// <summary>
    /// 清理夹具提交的输入，不让上一轮按住事实进入下一测试周期。
    /// </summary>
    private static void ReleaseFreezeInputs()
    {
        foreach (string action in new[] { InputSystem.MoveRight, InputSystem.Attack, InputSystem.Jump, InputSystem.Musou })
        {
            Input.ActionRelease(action);
        }
    }
}
