using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameConfig.Entity;
using GameConfig.Skills;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Effects;
using GameLogic.Enemies;
using GameLogic.Heroes;
using GameLogic.Heroes.Body;
using GameLogic.Input;
using GameLogic.Skills;
using GameLogic.UI;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 官方注册点、普攻根位移、二跳翻身及水魔细光柱的真实场景专项。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 从正式背包与真实输入截图，按物理步记录根坐标和二跳动画切换。
    /// </summary>
    /// <param name="hero">本关正式英雄。</param>
    /// <param name="flow">本关配置与英雄作用域。</param>
    /// <returns>实际渲染与运动检查任务。</returns>
    private async Task CheckHeroPresentationAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        foreach (var enemy in GF.Entity.GetAllEntityGroups().SelectMany(group => group.GetAllEntities()).OfType<AbstractEnemyEntity>())
        {
            SmokeInspection.FreezeAi(enemy);
            enemy.SetPhysicsProcess(false);
        }
        string name = hero.HeroId == 2 ? "tangseng" : "wukong";
        hero.GlobalPosition = new Vector2(400, 300);
        hero.Velocity = Vector2.Zero;
        hero.SetPhysicsProcess(true);
        await WaitUntilAsync(() => hero.OnFloor, "表现专项未落地");
        PushInventoryKey(true);
        PushInventoryKey(false);
        await WaitUntilAsync(() => GF.UI.GetAllLoadedUIForms().OfType<InventoryForm>().Any(form => form.Visible),
            "表现专项未打开正式背包");
        var inventory = GF.UI.GetAllLoadedUIForms().OfType<InventoryForm>().Single(form => form.Visible);
        await WaitUntilAsync(() => !GF.UI.GetAllLoadedUIForms().OfType<SaveNoticeForm>().Single()
            .GetNode<Label>("SaveFeedback/m_SaveFeedback").Visible, "表现截图前保存提示未结束", 5000);
        var preview = inventory.GetNode<Node2D>("Window/CharacterPanel/m_Preview");
        var player = preview.GetNode<AnimationPlayer>("AnimationPlayer");
        var previewBody = preview.GetNode<Sprite2D>("Body");
        int previewFrames = hero.HeroId == 1 ? 62 : 1;
        var previewIdle = player.GetAnimation("idle");
        Check(previewIdle.TrackGetKeyCount(0) == previewFrames &&
            previewBody.Texture.ResourcePath.Contains("/preview/"),
            "背包没有使用官方专用待机，仍混入战斗立棍或低头动作");
        double before = player.CurrentAnimationPosition;
        await WaitUntilAsync(() => player.CurrentAnimationPosition < before, "官方背包待机没有自然循环", 6000);
        GD.Print($"SMOKE-PREVIEW-NATIVE PASS: hero={hero.HeroId}, official backpack {previewFrames} frames, no combat idle poses.");
        // 预览由GGF OnUpdate驱动；停Node._Process不会冻结它，直接冻结原生播放器速度。
        player.SpeedScale = 0;
        player.Play("idle");
        foreach (double time in new[] { 0d, .5, 2.7, 3.0, 3.6 })
        {
            player.Seek(time, true, true);
            // ForceDraw本身不能刷新同一回调中连续Seek的渲染状态，先经过真实引擎帧。
            await WaitFramesAsync(2);
            await CaptureCombatAsync($"{name}_preview_registered_{time:0.0}");
        }
        Check(preview.Position.IsEqualApprox(new Vector2(149, 128)) &&
            preview.GetNode<Sprite2D>("Body").Position == Vector2.Zero,
            "正式背包未采用已核对的共同锚点和角色注册补偿");
        player.SpeedScale = 1;
        var exp = inventory.GetNode<TextureProgressBar>("Window/CharacterPanel/Experience/m_Experience");
        double expValue = exp.Value;
        foreach (int value in new[] { 0, 50, 100 })
        {
            exp.Value = value;
            await WaitFramesAsync(2);
            await CaptureCombatAsync($"inventory_exp_inset_{value}");
        }
        exp.Value = expValue;
        GF.UI.CloseUIForm(inventory.SerialId);
        await WaitUntilAsync(() => !GF.Base.IsGamePaused, "背包关闭未解除暂停");

        var fsm = SmokeInspection.ReadField<IFsm<IHeroBody>>(hero, "m_BodyFsm");
        int originalTicks = Engine.PhysicsTicksPerSecond;
        try
        {
            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                foreach (int direction in new[] { -1, 1 })
                {
                    hero.SetFacing(direction);
                    await WaitUntilAsync(() => fsm.CurrentState is HeroGroundState &&
                        hero.ActionData.Time >= hero.ActionData.AttackReadyAt, "表现专项没有恢复普攻起手");
                    Vector2 start = hero.GlobalPosition;
                    Input.ActionPress(InputSystem.Attack);
                    await WaitUntilAsync(() => fsm.CurrentState is HeroAttackState, "真实普攻输入未进入攻击");
                    Input.ActionRelease(InputSystem.Attack);
                    int samples = 0;
                    bool captured = false;
                    while (fsm.CurrentState is HeroAttackState)
                    {
                        Check(Math.Abs(hero.GlobalPosition.X - start.X) < .01f, "地面普攻改变了物理根横坐标");
                        if (!captured && hero.AnimPlayer.CurrentAnimationPosition >= .2)
                        {
                            await CaptureCombatAsync($"{name}_attack_registered_{ticks}_{direction}");
                            captured = true;
                        }
                        samples++;
                        await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
                    }
                    Check(samples > 0 && captured, "普攻没有覆盖完整姿态与实际物理步");
                    GD.Print($"SMOKE-HERO-ATTACK-ROOT PASS: hero={hero.HeroId}, {ticks}Hz dir={direction}, samples={samples}, dx={hero.GlobalPosition.X - start.X}.");
                }

                await WaitUntilAsync(() => fsm.CurrentState is HeroGroundState, "二跳专项未回地面");
                Input.ActionPress(InputSystem.Jump);
                await WaitUntilAsync(() => hero.ActionData.JumpCount == 1 && !hero.OnFloor, "一跳未起飞");
                Input.ActionRelease(InputSystem.Jump);
                await WaitFramesAsync(4);
                Input.ActionPress(InputSystem.Jump);
                await WaitUntilAsync(() => hero.ActionData.JumpCount == 2, "二跳未起飞");
                Input.ActionRelease(InputSystem.Jump);
                var jump = hero.AnimPlayer.GetAnimation(HeroAnims.Jump2);
                double endsAt = hero.ActionData.Time + (jump.HasMarker("complete") ? jump.GetMarkerTime("complete") : jump.Length)
                    - hero.AnimPlayer.CurrentAnimationPosition;
                bool flipCaptured = false;
                while (hero.ActionData.Time < endsAt - 2d / ticks)
                {
                    Check(hero.AnimPlayer.CurrentAnimation == HeroAnims.Jump2,
                        $"二跳翻身在原生结束标记前被截断：anim={hero.AnimPlayer.CurrentAnimation}, pos={hero.AnimPlayer.CurrentAnimationPosition}, now={hero.ActionData.Time}, expectedEnd={endsAt}");
                    if (!flipCaptured && endsAt - hero.ActionData.Time < .2)
                    {
                        await CaptureCombatAsync($"{name}_double_jump_flip_{ticks}");
                        flipCaptured = true;
                    }
                    await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
                }
                await WaitUntilAsync(() => hero.AnimPlayer.CurrentAnimation == HeroAnims.Fall, "二跳结束后未切换下落姿势");
                Check(Math.Abs(hero.ActionData.Time - endsAt) < 3d / ticks, "二跳姿态切换未跟随原生结束时点");
                await CaptureCombatAsync($"{name}_double_jump_fall_{ticks}");
                await WaitUntilAsync(() => hero.OnFloor, "二跳没有正常落地");
                GD.Print($"SMOKE-HERO-DOUBLE-JUMP PASS: hero={hero.HeroId}, {ticks}Hz native flip complete, fall and landing.");
            }
            if (hero is TangsengEntity)
            {
                await CheckWaterDemonBeamCoverageAsync(hero, flow);
            }
        }
        finally
        {
            Input.ActionRelease(InputSystem.Attack);
            Input.ActionRelease(InputSystem.Jump);
            Engine.PhysicsTicksPerSecond = originalTicks;
        }
    }

    /// <summary>
    /// 本体真实落到单向平台，再经正式创建入口释放分身，比较接地高度与左右镜像。
    /// </summary>
    /// <param name="hero">本关唐僧。</param>
    /// <returns>两种物理频率和双朝向的平台检查。</returns>
    private async Task CheckTangsengClonePlatformsAsync(TangsengEntity hero)
    {
        var clone = hero.GetNode<GameLogic.Heroes.Tangseng.TangsengWaterCloneNode>("m_WaterClone");
        var mainShape = hero.GetNode<CollisionShape2D>("CollisionShape2D");
        var cloneShape = clone.GetNode<CollisionShape2D>("CollisionShape2D");
        Check(mainShape.Position == cloneShape.Position && mainShape.Shape.GetRect() == cloneShape.Shape.GetRect(),
            "幻影没有使用最新本体身体形状及脚底基线");
        var platforms = FindLevel().GetNode("World/Platforms").GetChildren().OfType<StaticBody2D>().ToArray();
        Check(platforms.Length > 0, "分身专项没有正式单向平台");
        // 复制正式平台到当前波次内，避免把英雄传送过关卡边界触发后续战斗和奖励。
        var platform = (StaticBody2D)platforms[0].Duplicate();
        FindLevel().AddChild(platform);
        platform.GlobalPosition = new Vector2(350, 300);
        int originalTicks = Engine.PhysicsTicksPerSecond;
        try
        {
            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                foreach (int direction in new[] { -1, 1 })
                {
                    var surface = platform.GetNode<CollisionShape2D>("Surface");
                    Rect2 bounds = surface.GlobalTransform * surface.Shape.GetRect();
                    float feet = mainShape.Position.Y + mainShape.Shape.GetRect().End.Y;
                    hero.GlobalPosition = new Vector2(bounds.GetCenter().X, bounds.Position.Y - feet - 60);
                    hero.Velocity = Vector2.Zero;
                    hero.SetFacing(direction);
                    await WaitFramesAsync(2);
                    await WaitUntilAsync(() => hero.OnFloor && Enumerable.Range(0, hero.GetSlideCollisionCount())
                        .Any(i => ReferenceEquals(hero.GetSlideCollision(i).GetCollider(), platform)), "本体未落在正式平台");
                    hero.CreateWaterClone();
                    await WaitUntilAsync(() => clone.IsOnFloor() && Math.Abs(clone.GlobalPosition.X - hero.GlobalPosition.X) >= 15,
                        "从平台释放的水幻影穿过平台");
                    Check(Math.Abs(clone.GlobalPosition.Y - hero.GlobalPosition.Y) < .1,
                        "平台分身与本体的接地高度不同");
                    var previousCamera = hero.GetViewport().GetCamera2D();
                    var camera = new Camera2D { GlobalPosition = new Vector2(bounds.GetCenter().X, 295) };
                    FindLevel().AddChild(camera);
                    camera.MakeCurrent();
                    await WaitFramesAsync(2);
                    await CaptureCombatAsync($"tangseng_clone_platform_{ticks}_{direction}");
                    previousCamera.MakeCurrent();
                    camera.QueueFree();
                    clone.Dismiss();
                }
            }
        }
        finally
        {
            clone.Dismiss();
            platform.QueueFree();
            Engine.PhysicsTicksPerSecond = originalTicks;
            hero.GlobalPosition = new Vector2(400, 300);
            hero.Velocity = Vector2.Zero;
            await WaitFramesAsync(2);
            await WaitUntilAsync(() => hero.OnFloor, "平台专项后未落回普通地面");
        }
    }

    /// <summary>
    /// 在光柱中段放置中心与侧空白目标，实际物理命中必须区分两者；上环和水花仍命中。
    /// </summary>
    /// <param name="hero">拥有水魔效果的唐僧。</param>
    /// <param name="flow">攻击使用的正式规则作用域。</param>
    /// <returns>双朝向、两种物理频率和池复用检查任务。</returns>
    private async Task CheckWaterDemonBeamCoverageAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        var scene = GD.Load<PackedScene>("res://TheGame/Entitys/Heroes/Tangseng/tangseng_water_demon_fall_effect.tscn");
        var effects = SmokeInspection.ReadField<HashSet<SkillEffectNode>>(hero, "m_SkillEffects");
        foreach (int ticks in new[] { 60, 120 })
        {
            Engine.PhysicsTicksPerSecond = ticks;
            foreach (int direction in new[] { -1, 1 })
            {
                hero.SetFacing(direction);
                Vector2 origin = new(600, 160);
                var targets = new List<(AbstractEnemyEntity Enemy, int Hp, bool Hit)>();
                try
                {
                    foreach (var sample in new[] { (Position: new Vector2(0, 126), Hit: true),
                        (Position: new Vector2(100, 126), Hit: false),
                        (Position: new Vector2(65, -50), Hit: true),
                        (Position: new Vector2(90, 289), Hit: true) })
                    {
                        var enemy = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.Gorilla,
                            new EnemySpawn(origin + sample.Position));
                        SmokeInspection.StrengthenTrainingTarget(enemy);
                        SmokeInspection.FreezeAi(enemy);
                        enemy.SetPhysicsProcess(false);
                        targets.Add((enemy, enemy.Hp, sample.Hit));
                    }
                    ((ISkillActor)hero).PlaySkillEffect(scene, origin, SkillHitId.WaterDemonFall,
                        flow.GetModel<HeroModel>().Power.Value * 8);
                    var effect = effects.Single(e => e.SceneFilePath == scene.ResourcePath);
                    await WaitUntilAsync(() => effect.GetNode<AnimatedSprite2D>("Origin/Visual/Sprite").Frame >= 13,
                        "水魔光柱没有展开");
                    await CaptureCombatAsync($"tangseng_water_beam_coverage_{ticks}_{direction}");
                    await WaitUntilAsync(() => !effects.Contains(effect), "水魔范围专项效果没有回池");
                    foreach (var sample in targets)
                    {
                        Check((sample.Enemy.Hp < sample.Hp) == sample.Hit,
                            $"水魔光柱区域错误：{sample.Enemy.GlobalPosition - origin}，expectedHit={sample.Hit}");
                    }
                    GD.Print($"SMOKE-WATER-BEAM-COVERAGE PASS: {ticks}Hz dir={direction}, thin center, side air excluded, top ring and ground splash hit.");
                }
                finally
                {
                    foreach (var sample in targets)
                    {
                        ReleaseTestEntity(sample.Enemy);
                    }
                }
            }
        }
    }
}
