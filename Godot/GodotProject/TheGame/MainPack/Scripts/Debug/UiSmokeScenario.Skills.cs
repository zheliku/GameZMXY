using GameLogic.Config;
using GameLogic.Battle;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Equipment;
using GameLogic.Save;
using GameLogic.Profile;
using GameLogic.Heroes.Body;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;
using System;
using GodotGameFramework.UI;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameLogic;
using GameLogic.Level;
using GameLogic.UI;
using GameLogic.UI.Widgets;
using GameLogic.Audio;
using GameConfig.Sound;
using Godot;
using GodotGameFramework;
using GameConfig.Skills;
using GameConfig.Entity;
using GameLogic.Skills;
using GameLogic.Input;
using GameLogic.Heroes;
using GameLogic.Heroes.Tangseng;
using GameLogic.Enemies;
using GameLogic.Enemies.Body;
using GameLogic.Effects;
using GameLogic.Event.Combat;
using GameLogic.FloatingText;
using GodotGameFramework.Entity;

/// <summary>
/// 真实技能窗口导航、配置展示、钱包观察及复用回归。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 经正式学习控件与真实技能输入验证甘露、玄冰阵及水魔爆的完整视觉链路。
    /// </summary>
    /// <param name="hero">真实创建的唐僧。</param>
    /// <param name="flow">当前隔离档案作用域。</param>
    /// <param name="battleForm">本关正式HUD。</param>
    /// <returns>技能实际播放与独立命中的验证任务。</returns>
    private async Task CheckTangsengEffectsAsync(TangsengEntity hero, ProcedureOwner flow, BattleForm battleForm)
    {
        foreach (var enemy in GF.Entity.GetAllEntityGroups().SelectMany(g => g.GetAllEntities()).OfType<AbstractEnemyEntity>())
        {
            SmokeInspection.FreezeAi(enemy);
            enemy.SetPhysicsProcess(false);
            enemy.GlobalPosition = new Vector2(100000, 100000);
        }
        var model = flow.GetModel<GameLogic.Battle.HeroModel>();
        var system = flow.GetSystem<SkillSystem>();
        var effects = SmokeInspection.ReadField<HashSet<SkillEffectNode>>(hero, "m_SkillEffects");
        flow.GetSystem<ProfileSystem>().AddSoul(10000);
        battleForm.GetNode<BaseButton>("MenuPanel/m_SkillsButton").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntilAsync(() => GF.UI.GetAllLoadedUIForms().OfType<SkillForm>().Any(f => f.Visible), "唐僧技能页没有打开");
        var form = GF.UI.GetAllLoadedUIForms().OfType<SkillForm>().Single(f => f.Visible);
        foreach (var skill in ConfigSystem.Instance.Tables.TbSkillConfig.DataList.Where(s => s.Hero == ESkillHero.Tangseng))
        {
            form.GetNode<BaseButton>($"Content/m_ActivePage/m_TangsengRows/Skill{skill.Order}/IconCell/Icon")
                .EmitSignal(BaseButton.SignalName.Pressed);
            Check(system.IsLearned(skill.Id), $"唐僧技能没有经正式控件学习：{skill.Id}");
        }
        form.GetNode<BaseButton>("Content/Header/m_Back").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntilAsync(() => !GF.Base.IsGamePaused, "学习后没有解除暂停");
        hero.GlobalPosition = new Vector2(400, 300);
        hero.Velocity = Vector2.Zero;
        hero.SetPhysicsProcess(true);
        hero.SetFacing(1);
        await WaitUntilAsync(() => hero.OnFloor, "唐僧技能回归没有落地");
        await CheckTangsengClonePlatformsAsync(hero);
        string cloneSound = ConfigSystem.Instance.Tables.TbSoundConfig.Get((int)ESoundId.TangsengWaterClone).Path;
        await WaitUntilAsync(() => !m_Driver.GetTree().Root.FindChildren("*", "AudioStreamPlayer", true, false)
            .OfType<AudioStreamPlayer>().Any(player => player.Playing && player.Stream?.ResourcePath == cloneSound),
            "平台释放的分身声音未播放结束");
        var soundIds = Enum.GetValues<ESoundId>().Where(id => id.ToString().StartsWith("Tangseng")).ToArray();
        await CheckSkillSoundsAsync(soundIds);
        var soundPaths = soundIds.ToDictionary(id => ConfigSystem.Instance.Tables.TbSoundConfig.Get((int)id).Path);
        var sounds = soundIds.ToDictionary(id => id, _ => 0);
        void OnTangsengSound(object sender, GameFramework.Event.GameEventArgs args)
        {
            var played = (GodotGameFramework.Sound.PlaySoundSuccessEventArgs)args;
            if (soundPaths.TryGetValue(played.SoundAssetName, out var id))
            {
                sounds[id]++;
            }
        }
        GF.Event.Subscribe(GodotGameFramework.Sound.PlaySoundSuccessEventArgs.EventId, OnTangsengSound);
        try
        {
            // 精确时点检查原生方法键，GGF成功事件用于验证播放结果；异步加载耗时不等于动画键时间。
            CheckNativeMethodAt(hero.AnimPlayer.GetAnimation("attack_1"), "PlaySoundAnimEvent", 0);
            CheckNativeMethodAt(hero.AnimPlayer.GetAnimation("skills/dark_ice"), "BeginDarkIceHitAnimEvent", 14d / 24d);
            // 创建与换位走真实输入；分身的直接生命周期测试不替代两次输入发声。
            var waterClone = hero.GetNode<TangsengWaterCloneNode>("m_WaterClone");
            Input.ActionPress(InputSystem.Skill2);
            await WaitUntilAsync(() => waterClone.IsAvailable && model.ActiveSkillId == null, "水幻影创建没有完成");
            Input.ActionRelease(InputSystem.Skill2);
            await WaitFramesAsync(3);
            Input.ActionPress(InputSystem.Skill2);
            await WaitUntilAsync(() => !waterClone.IsActive, "水幻影第二次输入没有换位");
            Input.ActionRelease(InputSystem.Skill2);
            await WaitUntilAsync(() => sounds[ESoundId.TangsengWaterClone] == 2, "分身创建/换位声音次数错误");
            await CheckTangsengBoltInputAsync(hero, flow, sounds);
            await CheckTangsengAirSkillMotionAsync(hero, flow);

            var floating = SmokeInspection.ReadField<FloatingTextPresenter>(FindLevel(), "m_FloatingText");
            var texts = SmokeInspection.ReadField<HashSet<FloatingTextNode>>(floating, "m_Texts");
            await WaitUntilAsync(() => texts.Count == 0, "技能检查前旧飘字没有结束");
            model.Hp.Value = 10;
            int cureSounds = sounds[ESoundId.TangsengHeavenlyDew];
            await BeginAsync(ESkillId.HeavenlyDew, InputSystem.Skill3);
            var cure = effects.Single(e => e.SceneFilePath.EndsWith("tangseng_cure_role_effect.tscn"));
            var cureSprite = cure.GetNode<AnimatedSprite2D>("Origin/Visual/Sprite");
            await WaitUntilAsync(() => cureSprite.Frame >= 11, "甘露仍停在首帧");
            Check(cureSprite.SpriteFrames.GetFrameCount("play") == 34 && model.Hp.Value == 10 &&
                cureSprite.Material is CanvasItemMaterial { BlendMode: CanvasItemMaterial.BlendModeEnum.Add },
                "甘露帧数、加法混合或治疗时点错误");
            await CaptureCombatAsync("tangseng_cure_full");
            await WaitUntilAsync(() => model.ActiveSkillId == null, "甘露没有在末帧收招");
            Check(model.Hp.Value == 30, $"甘露没有按int(MaxMP/8+8)恢复20HP，实际{model.Hp.Value}");
            CheckRecoveryText(texts.Single(), 20);
            await CaptureCombatAsync("tangseng_dew_heal_number");
            Check(sounds[ESoundId.TangsengHeavenlyDew] == cureSounds + 1, "无分身甘露起手未播放一次声音");
            model.Hp.Value = 10;
            waterClone.Activate(hero.GlobalPosition, hero.Facing);
            await BeginAsync(ESkillId.HeavenlyDew, InputSystem.Skill3);
            await WaitUntilAsync(() => model.Hp.Value == 30, "带分身甘露的本体没有先治疗");
            CheckRecoveryText(texts.Single(), 20);
            await WaitUntilAsync(() => model.Hp.Value == 50, "水幻影甘露没有第二次治疗本体");
            Check(texts.Count == 2, "本体与分身甘露没有分别显示治疗数字");
            foreach (var text in texts)
            {
                CheckRecoveryText(text, 20);
            }
            await CaptureCombatAsync("tangseng_clone_dew_heal_number");
            await WaitUntilAsync(() => model.ActiveSkillId == null && !waterClone.IsActive && effects.Count == 0,
                "带分身甘露没有完整收招");
            Check(sounds[ESoundId.TangsengHeavenlyDew] == cureSounds + 3, "分身甘露没有保留官方两次起手音");

            m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EEntityId.Gorilla,
                new EnemySpawn(hero.GlobalPosition + new Vector2(90, 0)));
            SmokeInspection.StrengthenTrainingTarget(m_Monster);
            SmokeInspection.FreezeAi(m_Monster);
            m_Monster.SetPhysicsProcess(false);
            int hp = m_Monster.Hp;
            var clone = hero.GetNode<TangsengWaterCloneNode>("m_WaterClone");
            var clonePlayer = clone.GetNode<AnimationPlayer>("AnimationPlayer");
            int originalTicks = Engine.PhysicsTicksPerSecond;
            try
            {
                foreach (int rate in new[] { 60, 120 })
                {
                    Engine.PhysicsTicksPerSecond = rate;
                    foreach (bool musou in new[] { false, true })
                    {
                        model.Ws.Value = musou ? model.MaxWs.Value : 0;
                        model.WsActive.Value = musou;
                        await WaitUntilAsync(() => !GF.Base.IsGamePaused, "无双演出结束后没有恢复游戏");
                        await WaitFramesAsync(2);
                        clone.Activate(hero.GlobalPosition + new Vector2(-80, 0), -1);
                        hp = m_Monster.Hp;
                        int iceSounds = sounds[ESoundId.TangsengDarkIce];
                        await BeginAsync(ESkillId.DarkIceFormation, InputSystem.Skill1);
                        await WaitUntilAsync(() => clonePlayer.CurrentAnimation == "dark_ice",
                            "玄冰阵起手没有同步切换分身 hit3");
                        Check(hero.AnimPlayer.CurrentAnimationPosition < .1 && !clone.IsAvailable && clone.GetNode<Sprite2D>("m_Body").Scale.X > 0,
                            "分身辅助起手延迟、仍可换位或左向素材被错误镜像");
                        await WaitUntilAsync(() => effects.Count == 2, "本体与分身没有各自产生独立玄冰阵");
                        var dark = effects.OrderBy(e => e.GlobalPosition.DistanceSquaredTo(hero.GlobalPosition)).First();
                        var shadowDark = effects.Single(e => e != dark);
                        var player = dark.GetNode<AnimationPlayer>("AnimationPlayer");
                        var shadowPlayer = shadowDark.GetNode<AnimationPlayer>("AnimationPlayer");
                        Check(dark.Source.OwnerEntityId == hero.Id && shadowDark.Source.OwnerEntityId == hero.Id &&
                            dark.Source.CasterInstanceId == hero.GetInstanceId() &&
                            shadowDark.Source.CasterInstanceId == clone.GetInstanceId() &&
                            dark.Source.EmitterInstanceId == dark.GetInstanceId() &&
                            shadowDark.Source.EmitterInstanceId == shadowDark.GetInstanceId(),
                            "本体/分身攻击来源没有区分施放者、判定节点及共同伤害归属");
                        Check(Math.Abs(player.CurrentAnimationPosition - shadowPlayer.CurrentAnimationPosition) <= 1.5 / rate,
                            "本体与分身第15源帧的攻击错开");
                        Check(player.CurrentAnimation == (musou ? "musou" : "play") && shadowPlayer.CurrentAnimation == "play",
                            "分身错误取得本体无双才有的第26/31帧攻击刷新");
                        Check(dark.Scale.X < 0 && shadowDark.Scale.X > 0 &&
                            effects.All(e => e.ZIndex > hero.ZIndex && e.ZIndex > m_Monster.ZIndex),
                            "玄冰阵没有保留各自朝向或离身攻击被角色遮挡");
                        float width = musou ? 1.26f : 1f;
                        float height = musou ? 1.8f : 1f;
                        foreach (var effect in effects)
                        {
                            Transform2D visual = effect.GetNode<Node2D>("Origin/Visual").GlobalTransform;
                            Check(Math.Abs(visual.X.Length() - width) < .001 && Math.Abs(visual.Y.Length() - height) < .001 &&
                                SmokeInspection.ReadField<int>(effect, "m_Power") == (int)(model.Power.Value * .8f * (musou ? 1.5f : 1f)),
                                "玄冰阵无双缩放或官方整数威力错误");
                        }
                        await WaitUntilAsync(() => dark.GetNode<AnimatedSprite2D>("Origin/Visual/Sprite").Frame >= 5,
                            "玄冰阵没有推进到第6帧");
                        if (rate == 120 && !musou)
                        {
                            await CaptureCombatAsync("tangseng_dark_ice_full");
                        }
                        if (rate == 120)
                        {
                            await WaitUntilAsync(() => dark.GetNode<AnimatedSprite2D>("Origin/Visual/Sprite").Frame >= 20,
                                "玄冰阵没有推进到共同视觉对照帧");
                            var shape = hero.GetNode<CollisionShape2D>("CollisionShape2D");
                            float feet = shape.Position.Y + shape.Shape.GetRect().End.Y;
                            Check(dark.GlobalPosition.IsEqualApprox(hero.GlobalPosition - new Vector2(0, musou ? feet * .8f : 0)) &&
                                dark.GetNode<Node2D>("Origin/Visual").Position == Vector2.Zero,
                                "玄冰阵普通/无双没有保持角色接地线");
                            await CaptureCombatAsync(musou ? "tangseng_dark_ice_musou_registered" : "tangseng_dark_ice_normal_registered");
                        }
                        await WaitUntilAsync(() => m_Monster.Hp < hp, "玄冰阵真实判定未命中近侧目标");
                        await WaitUntilAsync(() => model.ActiveSkillId == null && effects.Count == 0 && !clone.IsActive,
                            "玄冰阵本体、分身或效果没有完整结束");
                        Check(sounds[ESoundId.TangsengDarkIce] == iceSounds + 1,
                            "玄冰阵本体未发声或分身错误重复出手音");
                        GD.Print($"SMOKE-TANGSENG-DARK-ICE PASS: {rate}Hz musou={musou}, simultaneous casts, independent facing/refresh, scale/power and draw order.");
                    }
                }
            }
            finally
            {
                clone.Dismiss();
                model.WsActive.Value = false;
                model.Ws.Value = 0;
                Engine.PhysicsTicksPerSecond = originalTicks;
            }

            int waterSounds = sounds[ESoundId.TangsengWaterDemon];
            await BeginAsync(ESkillId.WaterDemonBurst, InputSystem.Skill4);
            await WaitUntilAsync(() => hero.CanReleaseWaterDemon, "水魔爆第10源帧没有出现roundMc");
            var waterBall = hero.GetNode<AnimatedSprite2D>("m_EffectRoot/m_WaterDemonBall");
            Vector2 sourceOrigin = hero.GlobalPosition + new Vector2(
                -(waterBall.Position.X + 12.45f - .85f), waterBall.Position.Y - 8.05f - 11.3f);
            Check(waterBall.ToGlobal(waterBall.Offset).DistanceTo(sourceOrigin) < .01f,
                "水魔瞄准圈没有保留官方 hit4 父矩阵与原始图像注册点");
            await CaptureCombatAsync("tangseng_water_ball");
            await WaitUntilAsync(() => model.ActiveSkillId == null, "无二次输入的水魔爆未在第42帧结束");
            Check(effects.Count == 0 && !hero.CanReleaseWaterDemon, "无追加输入仍自动创建光柱或残留球体");
            Check(sounds[ESoundId.TangsengWaterDemon] == waterSounds, "水魔爆无追加输入却播放下落声音");

            await BeginAsync(ESkillId.WaterDemonBurst, InputSystem.Skill4);
            await WaitUntilAsync(() => hero.AnimPlayer.CurrentAnimationPosition > .7, "水魔球没有前移");
            Vector2 ball = hero.GetNode<AnimatedSprite2D>("m_EffectRoot/m_WaterDemonBall").Position;
            Vector2 expected = hero.GlobalPosition + new Vector2(-ball.X, ball.Y - 300);
            m_Monster.GlobalPosition = new Vector2(expected.X, hero.GlobalPosition.Y);
            hp = m_Monster.Hp;
            Input.ActionPress(InputSystem.Skill4);
            await WaitUntilAsync(() => effects.Any(e => e.SceneFilePath.EndsWith("tangseng_water_demon_fall_effect.tscn")), "二次输入没有独立下落效果");
            Input.ActionRelease(InputSystem.Skill4);
            var fall = effects.Single(e => e.SceneFilePath.EndsWith("tangseng_water_demon_fall_effect.tscn"));
            CheckNativeMethodAt(fall.GetNode<AnimationPlayer>("AnimationPlayer").GetAnimation("play"),
                "PlaySoundAnimEvent", 13d / 24d);
            Check(fall.GlobalPosition.DistanceTo(expected) < 16 && hero.AnimPlayer.CurrentAnimationPosition >= 42d / 24d,
                $"水魔追加落点或恢复标记错误：实际{fall.GlobalPosition}、预期{expected}");
            await WaitUntilAsync(() => fall.GetNode<AnimatedSprite2D>("Origin/Visual/Sprite").Frame >= 13, "水魔下落没有完整播放");
            await CaptureCombatAsync("tangseng_water_fall");
            await WaitUntilAsync(() => m_Monster.Hp < hp, "独立水魔下落未在光柱位置命中目标");
            await WaitUntilAsync(() => model.ActiveSkillId == null && effects.Count == 0, "追加水魔效果未收招和回池");
            Check(sounds[ESoundId.TangsengWaterDemon] == waterSounds + 1, "水魔追加第14帧没有播放一次声音");
            var damage = GameLogic.Battle.Attacks.AttackData.Create(0, default, 1,
                GameConfig.Battle.EDamageKind.Real, Vector2.Zero, 1, 0);
            try
            {
                hero.ReceiveHit(damage);
            }
            finally
            {
                GameFramework.ReferencePool.Release(damage);
            }
            await WaitUntilAsync(() => sounds[ESoundId.TangsengHurt] > 0, "唐僧成功受击没有自己的声音");
            Check(sounds.All(pair => pair.Value > 0), "真实输入链未覆盖全部唐僧声音");
            GD.Print("SMOKE-TANGSENG-SOUNDS PASS: eight official MP3s on GGF SFX; real attack/partial/full charge, clone create/teleport, one ice voice, two clone-dew voices, follow-up-only fall, impact and hurt.");
            GD.Print("SMOKE-TANGSENG-EFFECTS PASS: real skill learning, 34-frame additive cure, stopped nested ice, moving roundMc, timeout and independent fall hit.");
        }
        finally
        {
            GF.Event.Unsubscribe(GodotGameFramework.Sound.PlaySoundSuccessEventArgs.EventId, OnTangsengSound);
            Input.ActionRelease(InputSystem.Skill1);
            Input.ActionRelease(InputSystem.Skill2);
            Input.ActionRelease(InputSystem.Skill3);
            Input.ActionRelease(InputSystem.Skill4);
            ReleaseTestEntity(m_Monster);
            m_Monster = null;
        }

        async Task BeginAsync(ESkillId id, string action)
        {
            model.Mp.Value = model.MaxMp.Value;
            await WaitUntilAsync(() => SmokeInspection.ReadField<IFsm<IHeroBody>>(hero, "m_BodyFsm")
                .CurrentState is HeroGroundState, "上次技能释放模型状态后尚未回到可起手身体状态");
            Input.ActionPress(action);
            await WaitUntilAsync(() => model.ActiveSkillId == id, $"真实输入没有激活唐僧技能{id}",
                describe: () => $"hp={model.Hp.Value} mp={model.Mp.Value} active={model.ActiveSkillId} controls={model.ControlsEnabled} anim={hero.AnimPlayer.CurrentAnimation} time={hero.ActionData.Time}");
            Input.ActionRelease(action);
        }

        void CheckNativeMethodAt(Animation clip, string method, double time)
        {
            int matches = 0;
            for (int track = 0; track < clip.GetTrackCount(); track++)
            {
                if (clip.TrackGetType(track) != Animation.TrackType.Method)
                {
                    continue;
                }
                for (int key = 0; key < clip.TrackGetKeyCount(track); key++)
                {
                    if (clip.TrackGetKeyValue(track, key).AsGodotDictionary()["method"].AsString() == method)
                    {
                        Check(Math.Abs(clip.TrackGetKeyTime(track, key) - time) < .000001,
                            $"{clip.ResourceName}/{method}未保留官方源帧时点");
                        matches++;
                    }
                }
            }
            Check(matches == 1, $"{clip.ResourceName}/{method}缺少唯一原生方法键");
        }
    }

    /// <summary>
    /// 按官方运动门槛验证空中蓄力、瞬发分身和完全定身的两招，覆盖60/120Hz。
    /// </summary>
    /// <param name="hero">正式唐僧实例。</param>
    /// <param name="flow">隔离战斗作用域。</param>
    /// <returns>真实身体运动与技能输入检查。</returns>
    private async Task CheckTangsengAirSkillMotionAsync(TangsengEntity hero, ProcedureOwner flow)
    {
        var model = flow.GetModel<HeroModel>();
        var fsm = SmokeInspection.ReadField<IFsm<IHeroBody>>(hero, "m_BodyFsm");
        var effects = SmokeInspection.ReadField<HashSet<SkillEffectNode>>(hero, "m_SkillEffects");
        var clone = hero.GetNode<TangsengWaterCloneNode>("m_WaterClone");
        int originalTicks = Engine.PhysicsTicksPerSecond;
        Vector2 origin = hero.GlobalPosition;
        try
        {
            foreach (int rate in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = rate;
                await StartAirAsync(-180);
                Input.ActionPress(InputSystem.Attack);
                await WaitUntilAsync(() => fsm.CurrentState is TangsengIceDragonState, "空中普攻没有进入冰龙蓄力");
                Vector2 start = hero.GlobalPosition;
                float speedY = hero.Velocity.Y;
                double time = hero.ActionData.Time;
                await WaitUntilAsync(() => hero.ActionData.Time >= time + .2, "空中蓄力时钟没有推进");
                Check(hero.Velocity.X > 140 && hero.Velocity.Y > speedY + 150 && hero.GlobalPosition.X > start.X + 20,
                    "冰龙波蓄力错误清空空中横移或暂停重力");
                Input.ActionRelease(InputSystem.MoveRight);
                time = hero.ActionData.Time;
                await WaitUntilAsync(() => hero.ActionData.Time >= time + .05, "空中松方向没有采样");
                float stoppedX = hero.GlobalPosition.X;
                Input.ActionPress(InputSystem.MoveRight);
                time = hero.ActionData.Time;
                await WaitUntilAsync(() => hero.ActionData.Time >= time + .1, "空中蓄力没有继续推进");
                Check(hero.Velocity.X == 0 && Math.Abs(hero.GlobalPosition.X - stoppedX) < .1,
                    "冰龙波松方向后再次按下恢复了本次攻击横移");
                Input.ActionRelease(InputSystem.Attack);
                Input.ActionRelease(InputSystem.MoveRight);
                await WaitUntilAsync(() => model.ActiveSkillId == null && effects.Count == 0 && fsm.CurrentState is HeroGroundState,
                    "空中冰龙释放后没有落地收招");

                await StartAirAsync(120);
                start = hero.GlobalPosition;
                Input.ActionPress(InputSystem.Skill2);
                await WaitUntilAsync(() => clone.IsActive && model.ActiveSkillId == null, "空中水幻影没有瞬发完成");
                Input.ActionRelease(InputSystem.Skill2);
                Check(hero.Velocity.X > 140 && hero.Velocity.Y >= 120, "瞬发水幻影清掉了空中移动或下落速度");
                int mana = model.Mp.Value;
                Input.ActionPress(InputSystem.Skill4);
                time = hero.ActionData.Time;
                await WaitUntilAsync(() => hero.ActionData.Time >= time + .1, "水幻影后空中运动未推进");
                Input.ActionRelease(InputSystem.Skill4);
                Check(model.ActiveSkillId == null && model.Mp.Value == mana && hero.GlobalPosition.Y > start.Y + 10,
                    "空中水魔爆被错误激活或瞬发分身让身体悬空");
                clone.Dismiss();
                Input.ActionRelease(InputSystem.MoveRight);
                await WaitUntilAsync(() => fsm.CurrentState is HeroGroundState, "水幻影后没有正常落地");

                foreach (var (skill, action) in new[] { (ESkillId.DarkIceFormation, InputSystem.Skill1),
                    (ESkillId.HeavenlyDew, InputSystem.Skill3) })
                {
                    await StartAirAsync(-180);
                    Input.ActionPress(action);
                    await WaitUntilAsync(() => model.ActiveSkillId == skill && hero.Velocity == Vector2.Zero,
                        $"{skill}没有按官方完全定身");
                    Input.ActionRelease(action);
                    start = hero.GlobalPosition;
                    time = hero.ActionData.Time;
                    await WaitUntilAsync(() => hero.ActionData.Time >= time + .2, "定身技能时钟没有推进");
                    Check(hero.GlobalPosition.DistanceTo(start) < .01 && hero.Velocity == Vector2.Zero,
                        $"{skill}空中施法期间仍移动");
                    Input.ActionRelease(InputSystem.MoveRight);
                    await WaitUntilAsync(() => model.ActiveSkillId == null && effects.Count == 0 && fsm.CurrentState is HeroGroundState,
                        $"{skill}收招后没有恢复重力并落地");
                }
                GD.Print($"SMOKE-TANGSENG-AIR-MOTION PASS: {rate}Hz ice-charge gravity/held direction, instant clone momentum, grounded-only burst and dark-ice/dew suspension.");
            }
        }
        finally
        {
            foreach (string action in new[] { InputSystem.Attack, InputSystem.MoveRight, InputSystem.Skill1,
                InputSystem.Skill2, InputSystem.Skill3, InputSystem.Skill4 })
            {
                Input.ActionRelease(action);
            }
            clone.Dismiss();
            Engine.PhysicsTicksPerSecond = originalTicks;
            hero.GlobalPosition = origin;
            hero.Velocity = Vector2.Zero;
            model.Mp.Value = model.MaxMp.Value;
        }

        async Task StartAirAsync(float speedY)
        {
            await WaitUntilAsync(() => fsm.CurrentState is HeroGroundState, "空中专项没有恢复起手状态");
            model.Mp.Value = model.MaxMp.Value;
            hero.GlobalPosition = new Vector2(400, 180);
            hero.Velocity = new Vector2(144, speedY);
            hero.ActionData.JumpCount = 1;
            Input.ActionPress(InputSystem.MoveRight);
            await WaitUntilAsync(() => fsm.CurrentState is HeroAirState, "空中专项没有进入真实离地状态");
            // 先让MoveAndSlide刷新传送前的落地缓存，再设定待测的空中速度。
            hero.Velocity = new Vector2(144, speedY);
        }
    }

    /// <summary>
    /// 以真实J输入核对短按、未满/满蓄力、低蓝回退和同池复用，覆盖60/120Hz。
    /// </summary>
    /// <param name="hero">正式唐僧实例。</param>
    /// <param name="flow">隔离档案与战斗作用域。</param>
    /// <param name="sounds">本次真实GGF成功播放事件的累计计数。</param>
    /// <returns>输入、弹体、命中与资源校验任务。</returns>
    private async Task CheckTangsengBoltInputAsync(TangsengEntity hero, ProcedureOwner flow,
        IReadOnlyDictionary<ESoundId, int> sounds)
    {
        var model = flow.GetModel<HeroModel>();
        var effects = SmokeInspection.ReadField<HashSet<SkillEffectNode>>(hero, "m_SkillEffects");
        var fsm = SmokeInspection.ReadField<IFsm<IHeroBody>>(hero, "m_BodyFsm");
        var chargeBar = hero.GetNode<ProgressBar>("m_IceDragonCharge");
        Check(chargeBar.Size.IsEqualApprox(new Vector2(67.2f, 9)) &&
            chargeBar.Position.IsEqualApprox(new Vector2(-33.6f, -76.025f)), "蓄力条没有采用官方尺寸和头顶偏移");
        int ticks = Engine.PhysicsTicksPerSecond;
        var target = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EEntityId.Gorilla,
            new EnemySpawn(hero.GlobalPosition + new Vector2(200, 0)));
        SmokeInspection.StrengthenTrainingTarget(target);
        SmokeInspection.FreezeAi(target);
        target.SetPhysicsProcess(false);
        int wsGained = 0;
        int lastWs = model.Ws.Value;
        void OnWsChanged(int value)
        {
            wsGained += Math.Max(0, value - lastWs);
            lastWs = value;
        }
        model.Ws.Register(OnWsChanged);
        try
        {
            foreach (int rate in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = rate;
                hero.SetFacing(rate == 60 ? -1 : 1);
                foreach (var test in new[] { (Name: "tap", Hold: 0f, Mana: 100),
                    (Name: "partial", Hold: .2f, Mana: 100), (Name: "full", Hold: 1.3f, Mana: 100),
                    (Name: "poor/reuse", Hold: .5f, Mana: 19) })
                {
                    await WaitUntilAsync(() => fsm.CurrentState is HeroGroundState &&
                        hero.ActionData.Time >= hero.ActionData.AttackReadyAt, "法弹测试没有恢复地面起手");
                    model.Mp.Value = test.Mana;
                    model.Ws.Value = 0;
                    int wsBefore = wsGained;
                    Vector2 origin = hero.GlobalPosition;
                    target.GlobalPosition = origin + new Vector2(hero.Facing * 200, 0);
                    int hp = target.Hp;
                    int attackSounds = sounds[ESoundId.TangsengAttack];
                    int dragonSounds = sounds[ESoundId.TangsengIceDragon];
                    int impactSounds = sounds[ESoundId.TangsengImpact];
                    Input.ActionPress(InputSystem.Attack);
                    await WaitUntilAsync(() => fsm.CurrentState is HeroAttackState, "J没有进入普通攻击");
                    bool charge = test.Hold > 0 && test.Mana >= 20;
                    if (charge)
                    {
                        await WaitUntilAsync(() => fsm.CurrentState is TangsengIceDragonState,
                            "普攻第2源帧没有在长按J时进入冰龙蓄力");
                        Check(effects.Count == 0 && !hero.AnimPlayer.IsPlaying() &&
                            hero.AnimPlayer.CurrentAnimationPosition == 0, "蓄力提前发弹或姿态仍推进");
                        await WaitUntilAsync(() => chargeBar.Value > 0, "进入蓄力后头顶进度条没有增长");
                    }
                    double start = hero.ActionData.Time;
                    if (test.Name == "full")
                    {
                        GF.Base.PauseGame();
                        double progress = chargeBar.Value;
                        float alpha = chargeBar.Modulate.A;
                        await WaitFramesAsync(12);
                        Check(hero.ActionData.Time == start && effects.Count == 0 && model.Mp.Value == test.Mana,
                            "蓄力暂停时仍推进或扣蓝");
                        Check(chargeBar.Value == progress && chargeBar.Modulate.A == alpha,
                            "暂停时蓄力条进度或渐显仍推进");
                        GF.Base.ResumeGame();
                    }
                    if (test.Hold > 0)
                    {
                        await WaitUntilAsync(() => hero.ActionData.Time >= start + test.Hold, "长按时间未推进");
                    }
                    if (charge)
                    {
                        Check(chargeBar.FillMode == (hero.Facing < 0 ? 0 : 1) && chargeBar.Modulate.A > 0 &&
                            (test.Name != "full" || chargeBar.Value == 1 && chargeBar.Modulate.A == 1),
                            "蓄力条朝向、显示或满蓄比例不符合官方");
                        await CaptureCombatAsync($"tangseng_charge_{rate}_{test.Name}");
                    }
                    Input.ActionRelease(InputSystem.Attack);
                    await WaitUntilAsync(() => effects.Any(e => e is TangsengBoltNode), "松键后没有法弹");
                    Check(chargeBar.Value == 0, "冰龙释放后还保留旧蓄力进度");
                    Check(effects.Count == 1, "一次输入生成多枚法弹");
                    var bolt = effects.Single();
                    bool full = test.Name == "full";
                    var definition = SmokeInspection.ReadField<GameConfig.Battle.AttackConfig>(bolt, "m_Hit");
                    Check(bolt.SceneFilePath.EndsWith(full ? "tangseng_ice_dragon_bolt_effect.tscn" :
                        "tangseng_normal_bolt_effect.tscn"), "未满蓄力错误使用了冰龙素材");
                    Check(definition.DamageKind == (full ? GameConfig.Battle.EDamageKind.Magic : GameConfig.Battle.EDamageKind.Physics),
                        "法弹伤害类型错误");
                    Check(model.Mp.Value == test.Mana - (full ? 20 : 0), "冰龙只有满蓄力才应扣20MP");
                    Check(Math.Abs(bolt.Scale.X) == (full ? .8f : .6f) &&
                        SmokeInspection.ReadField<float>(bolt, "m_Speed") == (full ? 360 : 192), "法弹速度或缩放错误");
                    Check(SmokeInspection.ReadField<float>(bolt, "m_TravelDistance") == (full ? 600 : 304),
                        "临时蓄力射程污染了池实例的场景默认值");
                    int power = (int)(model.Power.Value * (full ? .2f : 1f));
                    Check(SmokeInspection.ReadField<int>(bolt, "m_Power") == power, "法弹威力未按官方整数截断");
                    int hits = 0;
                    int previousHp = hp;
                    await WaitUntilAsync(() => target.Hp < hp, "法弹没有发生首次命中");
                    var flashes = SmokeInspection.ReadField<HashSet<MonsterHitFeedbackNode>>(target, "m_HitFeedback");
                    Check(flashes.Count > 0 && flashes.All(f => f.SpriteFrames.ResourcePath.Contains("/tangseng/")),
                        "唐僧普攻/冰龙命中没有播放官方变色闪光");
                    if (test.Name == "tap")
                    {
                        await CaptureCombatAsync($"tangseng_normal_hit_feedback_{rate}");
                    }
                    await WaitUntilAsync(() =>
                    {
                        if (target.Hp < previousHp)
                        {
                            hits++;
                            previousHp = target.Hp;
                        }
                        return !effects.Contains(bolt);
                    }, "法弹没有按射程结束并回池", 4000);
                    Check(target.Hp < hp && (full ? hits >= 2 : hits == 1),
                        $"{rate}Hz {test.Name} 实际命中次数异常：{hits}");
                    Check(wsGained - wsBefore >= hits * 2,
                        $"{rate}Hz {test.Name}法弹命中无双错误：{hits}次命中只增长{wsGained - wsBefore}");
                    Check(model.ActiveSkillId == null && hero.GlobalPosition.DistanceTo(origin) < 1,
                        "法弹收招未释放身体或施放期间漂移");
                    await WaitUntilAsync(() => sounds[ESoundId.TangsengImpact] > impactSounds,
                        "法弹成功命中后GGF未派发唐僧命中音事件");
                    Check(sounds[ESoundId.TangsengAttack] == attackSounds + 1 &&
                        sounds[ESoundId.TangsengIceDragon] == dragonSounds + (full ? 1 : 0) &&
                        sounds[ESoundId.TangsengImpact] > impactSounds,
                        $"{rate}Hz {test.Name}声音错误：普攻{sounds[ESoundId.TangsengAttack] - attackSounds}，冰龙{sounds[ESoundId.TangsengIceDragon] - dragonSounds}，命中{sounds[ESoundId.TangsengImpact] - impactSounds}，预期蓄力{charge}、满蓄{full}");
                    GD.Print($"SMOKE-TANGSENG-BOLT PASS: {rate}Hz {test.Name}, damage={definition.DamageKind}, hits={hits}, ws={wsGained - wsBefore}, mana={model.Mp.Value}.");
                }
            }
        }
        finally
        {
            Input.ActionRelease(InputSystem.Attack);
            model.Ws.UnRegister(OnWsChanged);
            GF.Base.ResumeGame();
            Engine.PhysicsTicksPerSecond = ticks;
            model.Mp.Value = model.MaxMp.Value;
            hero.SetFacing(1);
            ReleaseTestEntity(target);
        }
    }

    /// <summary>
    /// 验证正式唐僧分身的原生动画、暂停、一次回调、寿命与复用。
    /// </summary>
    /// <param name="hero">真实选角与档案流程创建的唐僧。</param>
    /// <returns>原生分身生命周期验证任务。</returns>
    private async Task CheckTangsengWaterCloneAsync(TangsengEntity hero)
    {
        bool processing = hero.IsPhysicsProcessing();
        hero.SetPhysicsProcess(true);
        await WaitUntilAsync(() => hero.OnFloor, "分身专项前本体没有落到真实地形");
        var clone = hero.GetNode<GameLogic.Heroes.Tangseng.TangsengWaterCloneNode>("m_WaterClone");
        var player = clone.GetNode<AnimationPlayer>("AnimationPlayer");
        var sprite = clone.GetNode<Sprite2D>("m_Body");
        int originalTicks = Engine.PhysicsTicksPerSecond;
        try
        {
            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                Vector2 start = hero.GlobalPosition;
                clone.Activate(start + new Vector2(0, -140), 1);
                Vector2 airborne = clone.GlobalPosition;
                await WaitUntilAsync(() => clone.GlobalPosition.Y > airborne.Y + 20, "水幻影没有受重力下落");
                Check(clone.Velocity.Y > 0, "水幻影下落没有物理速度");
                await CaptureCombatAsync($"tangseng_clone_gravity_{ticks}");
                await WaitUntilAsync(() => clone.IsOnFloor(), "水幻影没有落到真实地形");
                Check(Math.Abs(clone.GlobalPosition.Y - start.Y) < 1,
                    "分身与本体落地脚底不在同一高度");
                clone.Activate(start, 1);
                await WaitUntilAsync(() => clone.GlobalPosition.X > start.X + 30, "水幻影没有独立行进");
                Check(player.CurrentAnimation == "walk" && sprite.Frame is >= 0 and < 16,
                    "水幻影走路没有使用原生帧轨道");
                GF.Base.PauseGame();
                Vector2 pausedPosition = clone.GlobalPosition;
                double pausedAnimation = player.CurrentAnimationPosition;
                await WaitFramesAsync(12);
                Check(clone.GlobalPosition == pausedPosition && player.CurrentAnimationPosition == pausedAnimation,
                    "分身暂停时仍移动或推进动画");
                GF.Base.ResumeGame();

                int impacts = 0;
                clone.PlayDarkIce(() => impacts++);
                Check(!clone.IsAvailable, "已经辅助施法的分身仍可换位或重复接招");
                Vector2 castPosition = clone.GlobalPosition;
                await WaitUntilAsync(() => impacts == 1, "原生玄冰阵第15源帧没有回调");
                Check(player.CurrentAnimationPosition >= 14d / 24d - .001 &&
                    player.CurrentAnimationPosition < 14d / 24d + .1 && clone.GlobalPosition == castPosition,
                    "分身玄冰阵回调时点或定身错误");
                await WaitUntilAsync(() => !clone.IsActive, "分身玄冰阵第50源帧没有结束");
                Check(impacts == 1, "分身玄冰阵重复回调");

                int heals = 0;
                int healFrame = -1;
                clone.Activate(start, -1);
                clone.PlayHeavenlyDew(() =>
                {
                    heals++;
                    healFrame = sprite.Frame;
                });
                await WaitUntilAsync(() => !clone.IsActive, "分身甘露末帧没有结束");
                Check(heals == 1 && healFrame == 115, $"分身甘露未在原生末帧治疗一次：次数{heals}、回调帧{healFrame}");

                clone.Activate(start, 1);
                clone.PlayDarkIce(() => impacts++);
                clone.Dismiss();
                await WaitFramesAsync(3);
                Check(impacts == 1 && !clone.Visible && !clone.IsPhysicsProcessing(), "取消后仍回调或继续推进");
                clone.Activate(start, -1);
                await WaitUntilAsync(() => !clone.IsActive, "水幻影192源帧寿命未结束", 10000);
                Check(clone.GlobalPosition.X < start.X - 30 && clone.GlobalPosition.X > 0 && !clone.Visible,
                    "水幻影反向移动、墙体约束或到期隐藏错误");
                GD.Print($"SMOKE-TANGSENG-CLONE PASS: {ticks}Hz gravity/landing/wall, native walk/hit3/hit5, pause, one callback, cancel/reuse and 192-frame expiry.");
            }
        }
        finally
        {
            GF.Base.ResumeGame();
            clone.Dismiss();
            Engine.PhysicsTicksPerSecond = originalTicks;
            hero.SetPhysicsProcess(processing);
        }
    }

    /// <summary>
    /// 通过正式 HUD 打开技能页，确认准备阶段没有学习或扣费行为。
    /// </summary>
    /// <param name="battleForm">正式 HUD。</param>
    /// <param name="flow">隔离测试档案。</param>
    /// <param name="level">当前关卡。</param>
    /// <returns>全部技能面板检查完成的任务。</returns>
    private async Task CheckSkillsAsync(BattleForm battleForm, ProcedureOwner flow, LevelViewController level)
    {
        int soul = flow.GetModel<ProfileModel>().Soul.Value;
        var entrance = battleForm.GetNode<TextureButton>("MenuPanel/m_SkillsButton");
        Check(InputMap.ActionGetEvents(GameLogic.Input.InputSystem.SkillToggle).OfType<InputEventKey>()
            .Any(input => input.PhysicalKeycode == Key.V), "技能面板没有默认 V 输入映射");
        PushSkillKey(true);
        PushSkillKey(false);
        entrance.EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntilAsync(() => GF.UI.GetAllLoadedUIForms().OfType<SkillForm>().Any(x => x.Visible), "技能入口没有打开面板");
        var form = GF.UI.GetAllLoadedUIForms().OfType<SkillForm>().Single(x => x.Visible);
        SmokeInspection.CheckTooltips(form);
        Check(GF.Base.IsGamePaused, "技能窗口没有暂停本关");
        PushSkillKey(true, echo: true);
        PushSkillKey(false);
        await WaitFramesAsync(3);
        Check(form.Visible && GF.Base.IsGamePaused, "长按 V 的重复事件关闭了技能窗口");
        const string active = "Content/m_ActivePage/";
        const string passive = "Content/m_PassivePage/";
        const string footer = "Content/Footer/";
        CheckSkillPreviewReplacement(form);
        Check(form.GetNode<Control>(active + "m_WukongRows").Visible &&
            form.GetNode<Label>(active + "m_WukongRows/Skill1/Name").Text == "升龙斩" &&
            form.GetNode<Label>(active + "m_WukongRows/Skill5/Soul").Text == "2000", "悟空技能顺序或费用错误");
        double elapsed = SmokeInspection.Model(level).ElapsedSeconds;
        await WaitFramesAsync(4);
        CheckInlineSkillGrid(form, active + "m_WukongRows");
        Check(Math.Abs(SmokeInspection.Model(level).ElapsedSeconds - elapsed) < .001, "技能窗口暂停仍累计关卡时间");
        await CaptureCombatAsync("skills_wukong");
        var otherHero = form.GetNode<TextureButton>("Content/Header/m_Tangseng");
        Check(otherHero.Disabled && otherHero.MouseDefaultCursorShape == Control.CursorShape.Arrow &&
            otherHero.TextureDisabled == otherHero.TextureNormal, "非出战角色页签没有禁用或保留了彩色交互");
        otherHero.EmitSignal(BaseButton.SignalName.Pressed);
        Check(!form.GetNode<Control>(active + "m_TangsengRows").Visible &&
            form.GetNode<Control>(active + "m_WukongRows").Visible, "直接发送信号仍能切换到非出战角色");
        form.GetNode<TextureButton>(footer + "m_Passive").EmitSignal(BaseButton.SignalName.Pressed);
        Check(form.GetNode<Control>("Content/m_PassivePage").Visible &&
            !form.GetNode<Control>("Content/m_ActivePage").Visible &&
            form.GetNode<Label>(passive + "m_PassiveRows/Passive3/NextEffect").Text == "暴击率增加 2 %", "被动页未展示一级总效果");
        await WaitFramesAsync(2);
        CheckInlineSkillGrid(form, passive + "m_PassiveRows");
        await CaptureCombatAsync("skills_passive");
        otherHero.EmitSignal(BaseButton.SignalName.Pressed);
        Check(form.GetNode<Control>("Content/m_PassivePage").Visible &&
            !form.GetNode<Control>("Content/m_ActivePage").Visible, "非出战页签改变了当前主动/被动分类");
        Check(flow.GetModel<ProfileModel>().Soul.Value == soul, "技能展示页意外消费灵魂");
        flow.GetSystem<ProfileSystem>().AddSoul(7);
        var balance = form.GetNode<Label>(footer + "Balance/m_Soul");
        Check(balance.Text == (soul + 7).ToString(), "技能面板没有观察钱包变化");
        PushSkillKey(true);
        PushSkillKey(false);
        await WaitFramesAsync(4);
        Check(!form.Visible && !GF.Base.IsGamePaused, "V 没有关闭技能页，或同一输入又把窗口打开");
        flow.GetModel<ProfileModel>().RestoreSoul(soul);
        Check(balance.Text == (soul + 7).ToString(), "关闭后仍保留钱包订阅");
        entrance.EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntilAsync(() => form.Visible, "技能窗口没有从池复用");
        Check(form.GetNode<Control>("Content/m_ActivePage").Visible &&
            form.GetNode<Control>(active + "m_WukongRows").Visible && balance.Text == soul.ToString(), "技能窗口复用残留旧页或余额");
        form._Input(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        await WaitFramesAsync(2);
        Check(!form.Visible && !GF.Base.IsGamePaused, "Escape没有关闭技能面板");
        var originalKeys = InputMap.ActionGetEvents(GameLogic.Input.InputSystem.SkillToggle);
        try
        {
            InputMap.ActionEraseEvents(GameLogic.Input.InputSystem.SkillToggle);
            InputMap.ActionAddEvent(GameLogic.Input.InputSystem.SkillToggle, new InputEventKey { PhysicalKeycode = Key.B });
            PushSkillKey(true);
            PushSkillKey(false);
            await WaitUntilAsync(() => form.Visible, "技能开关重映射后无法打开面板");
            PushSkillKey(true);
            PushSkillKey(false);
            await WaitFramesAsync(4);
            Check(!form.Visible && !GF.Base.IsGamePaused, "技能开关重映射后无法关闭面板");
        }
        finally
        {
            InputMap.ActionEraseEvents(GameLogic.Input.InputSystem.SkillToggle);
            foreach (var input in originalKeys)
            {
                InputMap.ActionAddEvent(GameLogic.Input.InputSystem.SkillToggle, input);
            }
        }
        GD.Print("SMOKE-SKILL-TOGGLE PASS: V open/close, pause, echo suppression, no reopen and remapped action.");
        await CheckSkillLearningAndCastingAsync(battleForm, form, flow);
        await CheckSkillImpactsAsync();
        await CheckSkillSoundsAsync();
        await CheckActorAnimationEventsAsync();
        CheckSkillCombatResources();
        GD.Print("SMOKE-UI: 技能限当前出战角色、禁用角色无交互、被动目录、暂停、钱包观察及窗口复用通过");
    }

    /// <summary>
    /// 通过当前技能开关映射向真实视口注入键盘事件。
    /// </summary>
    /// <param name="pressed">按下或释放。</param>
    /// <param name="echo">是否模拟长按重复事件。</param>
    private void PushSkillKey(bool pressed, bool echo = false)
    {
        using var input = (InputEventKey)InputMap.ActionGetEvents(GameLogic.Input.InputSystem.SkillToggle)
            .OfType<InputEventKey>().First().Duplicate();
        input.Pressed = pressed;
        input.Echo = echo;
        m_Driver.GetViewport().PushInput(input, true);
    }

    /// <summary>
    /// 替换编辑器占位内容后重新绑定，确认所有主动/被动行的数据仍来自正式目录。
    /// </summary>
    /// <param name="form">已打开的正式技能窗口。</param>
    private static void CheckSkillPreviewReplacement(SkillForm form)
    {
        var configuration = GF.Fsm.GetModel<ConfigurationModel>();
        var activePage = form.GetNode<ActiveSkillsPage>("Content/m_ActivePage");
        foreach (var group in new[] { "m_WukongRows", "m_TangsengRows" })
        {
            foreach (var row in activePage.GetNode<Control>(group).GetChildren().Cast<GridContainer>())
            {
                foreach (var field in new[] { "Name", "Soul", "DescriptionCell/Description" })
                {
                    row.GetNode<Label>(field).Text = "编辑器预览";
                }

                row.GetNode<TextureButton>("IconCell/Icon").TextureNormal = null;
            }
        }

        activePage.Refresh();
        foreach (var (group, hero) in new[]
        {
            ("m_WukongRows", GameConfig.Skills.ESkillHero.Wukong),
            ("m_TangsengRows", GameConfig.Skills.ESkillHero.Tangseng)
        })
        {
            var definitions = configuration.GetActive(hero);
            for (int i = 0; i < definitions.Count; i++)
            {
                var row = activePage.GetNode<GridContainer>($"{group}/Skill{i + 1}");
                var definition = definitions[i];
                Check(row.GetNode<Label>("Name").Text == definition.NameCn &&
                    row.GetNode<Label>("Soul").Text == definition.LearnSoul.ToString() &&
                    !string.IsNullOrWhiteSpace(row.GetNode<Label>("Key").Text) &&
                    row.GetNode<Label>("DescriptionCell/Description").Text == definition.Description &&
                    row.GetNode<TextureButton>("IconCell/Icon").TextureNormal.ResourcePath.EndsWith("_unlearned.png", StringComparison.Ordinal),
                    "主动技能预填内容没有被正式数据替换");
            }
        }

        var passivePage = form.GetNode<PassiveSkillsPage>("Content/m_PassivePage");
        var passives = configuration.Tables.TbPassiveConfig.DataList;
        foreach (var row in passivePage.GetNode<Control>("m_PassiveRows").GetChildren().Cast<GridContainer>())
        {
            foreach (var field in new[] { "Name", "NextEffect", "Soul" })
            {
                row.GetNode<Label>(field).Text = "编辑器预览";
            }
        }

        passivePage.Refresh();
        for (int i = 0; i < passives.Count; i++)
        {
            var row = passivePage.GetNode<GridContainer>($"m_PassiveRows/Passive{i + 1}");
            Check(row.GetNode<Label>("Name").Text == passives[i].NameCn &&
                row.GetNode<Label>("NextEffect").Text == passives[i].Levels[0].Description &&
                row.GetNode<Label>("Soul").Text == passives[i].Levels[0].LearnSoul.ToString(),
                "被动技能预填内容没有被正式数据替换");
        }
    }

    /// <summary>
    /// 确认行属于主场景，并在实际布局后检查所有列一致、不重叠且说明未溢出。
    /// </summary>
    /// <param name="form">拥有全部可编辑节点的窗口。</param>
    /// <param name="path">当前可见的五行容器。</param>
    private static void CheckInlineSkillGrid(SkillForm form, string path)
    {
        float[] columnStarts = null;
        foreach (var row in form.GetNode<Control>(path).GetChildren().Cast<GridContainer>())
        {
            Check(row.Owner == form && string.IsNullOrEmpty(row.SceneFilePath), "技能行仍来自独立子场景");
            var cells = row.GetChildren().Cast<Control>().ToArray();
            float[] starts = cells.Select(x => x.Position.X).ToArray();
            Check(columnStarts == null || starts.SequenceEqual(columnStarts), "同页技能列未按网格对齐");
            columnStarts = starts;
            for (int i = 0; i < cells.Length; i++)
            {
                Check(cells[i].Owner == form && cells[i].GetRect().End.X <= row.Size.X + .01f &&
                    (i == 0 || cells[i].Position.X >= cells[i - 1].GetRect().End.X), "技能网格列溢出或重叠");
            }
            if (row.HasNode("DescriptionCell/Description"))
            {
                var description = row.GetNode<Label>("DescriptionCell/Description");
                Check(description.GetMinimumSize().Y <= row.Size.Y + .01f, "技能长说明超过一行的可用高度");
            }
        }
        GD.Print($"SMOKE-UI: {path} 主场景网格归属、五行列对齐与长文本范围通过");
    }

    /// <summary>
    /// 验证实际原生动画与场景，不再验证已删除的表格指令。
    /// </summary>
    private void CheckSkillCombatResources()
    {
        var fixtures = new Node();
        try
        {
            var hero = GD.Load<PackedScene>("res://TheGame/Entitys/Heroes/Wukong/wukong_entity.tscn").Instantiate();
            fixtures.AddChild(hero);
            var player = hero.GetNode<AnimationPlayer>("m_AnimPlayer");
            foreach (string name in new[] { "rising_dragon", "flame_thrust", "blazing_flash", "flame_storm", "fire_demon_slash" })
            {
                var clip = player.GetAnimation("skills/" + name);
                Check(clip != null && clip.Length > 0 && clip.LoopMode == Animation.LoopModeEnum.None,
                    $"技能原生动画缺失或循环：{name}");
                bool methods = false;
                for (int track = 0; track < clip.GetTrackCount(); track++)
                {
                    if (clip.TrackGetType(track) != Animation.TrackType.Method)
                    {
                        continue;
                    }

                    methods = true;
                    for (int key = 0; key < clip.TrackGetKeyCount(track); key++)
                    {
                        var call = clip.TrackGetKeyValue(track, key).AsGodotDictionary();
                        Check(call["method"].AsString().EndsWith("AnimEvent", StringComparison.Ordinal) &&
                            hero.HasMethod(call["method"].AsStringName()), $"原生方法未绑定或后缀不符：{name}/{call["method"]}");
                    }
                }
                Check(methods, $"技能动画缺少具名方法轨道：{name}");
            }
            var prelude = hero.GetNode<AnimatedSprite2D>("m_FireDemonPrelude");
            Check(prelude.TopLevel && !prelude.Visible && prelude.SpriteFrames.GetFrameCount(prelude.Animation) == 40,
                "火魔前奏未保留原地独立播放或完整源帧");
            foreach (string name in new[] { "blazing_flash_trail", "fire_demon_combo", "fire_demon_descent", "fire_demon_landing" })
            {
                var node = GD.Load<PackedScene>($"res://TheGame/Entitys/Heroes/Wukong/wukong_{name}_effect.tscn").Instantiate();
                fixtures.AddChild(node);
                var animation = node.GetNode<AnimationPlayer>("AnimationPlayer");
                Check(animation.HasAnimation("play") && animation.Autoplay.ToString() == "",
                    $"特效原生动画缺失或提前播放：{name}");
                Check(node.GetNode<CollisionShape2D>("Origin/Visual/HitBox/CollisionShape2D").Disabled,
                    $"特效初始判定未关闭：{name}");
            }
            GD.Print("SMOKE-UI: 五招原生方法绑定、角色内前奏与四个独立特效场景通过");
        }
        finally
        {
            fixtures.Free();
        }
    }

    /// <summary>
    /// 真实点击学习、提示、HUD 同步及五招输入与火魔斩分支回归。
    /// </summary>
    /// <param name="battleForm">正式 HUD。</param>
    /// <param name="form">池化技能窗口。</param>
    /// <param name="flow">测试作用域。</param>
    /// <returns>全部交互与动画验证完成的任务。</returns>
    private async Task CheckSkillLearningAndCastingAsync(BattleForm battleForm, SkillForm form, ProcedureOwner flow)
    {
        var snapshot = flow.GetUtility<SaveDataUtility>().Capture(flow.GetModel<ProfileModel>(), flow.GetModel<SkillModel>());
        var hero = FindHero();
        var attributes = flow.GetModel<GameLogic.Battle.HeroModel>();
        var system = flow.GetSystem<SkillSystem>();
        Vector2 originalPosition = hero.GlobalPosition;
        int originalMp = attributes.Mp.Value;
        int impactSounds = 0;
        string impactPath = flow.GetModel<ConfigurationModel>().Tables.TbSoundConfig.Get((int)ESoundId.WukongImpact).Path;
        void OnImpactPlayed(object sender, GameFramework.Event.GameEventArgs args)
        {
            if (args is GodotGameFramework.Sound.PlaySoundSuccessEventArgs sound && sound.SoundAssetName == impactPath)
            {
                impactSounds++;
            }
        }
        GF.Event.Subscribe(GodotGameFramework.Sound.PlaySoundSuccessEventArgs.EventId, OnImpactPlayed);
        string[] skillInputs = [InputSystem.Skill1, InputSystem.Skill2, InputSystem.Skill3,
            InputSystem.Interact, InputSystem.Attack, InputSystem.MoveRight];
        try
        {
            CheckWukongBodyTiming(hero);
            battleForm.GetNode<TextureButton>("MenuPanel/m_SkillsButton").EmitSignal(BaseButton.SignalName.Pressed);
            await WaitUntilAsync(() => form.Visible, "学习时没有打开技能窗口");
            var first = form.GetNode<TextureButton>("Content/m_ActivePage/m_WukongRows/Skill1/IconCell/Icon");
            flow.GetModel<ProfileModel>().Soul.Value = 0;
            var notice = GF.UI.GetAllLoadedUIForms().OfType<SaveNoticeForm>().Single();
            GF.UI.CloseUIForm(notice.SerialId);
            await WaitFramesAsync(3);
            first.EmitSignal(BaseButton.SignalName.Pressed);
            Check(!system.IsLearned(ESkillId.RisingDragon) && flow.GetModel<ProfileModel>().Soul.Value == 0,
                "提示窗口缺席影响了技能学习拒绝结果");
            notice = await GF.UI.OpenUIFormAsync<SaveNoticeForm>(GameConfig.EUIFormId.SaveNoticeForm);
            first.EmitSignal(BaseButton.SignalName.Pressed);
            Check(!system.IsLearned(ESkillId.RisingDragon) && notice.GetNode<Label>("SaveFeedback/m_SaveFeedback").Text == "灵魂不足！",
                "灵魂不足没有拒绝学习并显示复用提示");
            flow.GetSystem<ProfileSystem>().AddSoul(4000);
            foreach (var definition in ConfigSystem.Instance.Tables.TbSkillConfig.DataList.Where(x => x.Hero == ESkillHero.Wukong))
            {
                var button = form.GetNode<TextureButton>($"Content/m_ActivePage/m_WukongRows/Skill{definition.Order}/IconCell/Icon");
                button.EmitSignal(BaseButton.SignalName.Pressed);
                var hudIcon = battleForm.GetNode<TextureRect>($"MenuPanel/SkillBar/Skill{definition.Order}/Icon").Texture as AtlasTexture;
                Check(system.IsLearned(definition.Id) && button.Disabled && button.TextureNormal.ResourcePath.EndsWith("_learned.png", StringComparison.Ordinal) &&
                    hudIcon?.Atlas.ResourcePath == "res://TheGame/Sprites/UI/hud/battle_skill_icons.png" &&
                    hudIcon.Region == new Rect2(definition.Order * 45, 0, 45, 45),
                    $"学习没有同步两个图标：{definition.Id}");
            }
            Check(flow.GetModel<ProfileModel>().Soul.Value == 200, "五招学习费用不是 3800");
            first.EmitSignal(BaseButton.SignalName.Pressed);
            Check(flow.GetModel<ProfileModel>().Soul.Value == 200, "重复学习重复扣费");
            await CaptureCombatAsync("skills_learned");
            form.GetNode<TextureButton>("Content/Header/m_Back").EmitSignal(BaseButton.SignalName.Pressed);
            await WaitFramesAsync(2);

            // 使用正式英雄、真实动作输入与物理帧；测试怪物不属于本关刷怪配方。
            hero.GlobalPosition = new Vector2(400, 300);
            hero.Velocity = Vector2.Zero;
            hero.SetPhysicsProcess(true);
            await WaitUntilAsync(() => hero.OnFloor, "技能测试英雄没有落地");
            hero.SetFacing(1);
            m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EEntityId.Gorilla,
                new EnemySpawn(hero.GlobalPosition + new Vector2(60, 0)));
            SmokeInspection.StrengthenTrainingTarget(m_Monster);
            SmokeInspection.FreezeAi(m_Monster);
            m_Monster.SetPhysicsProcess(false);
            int bodyTargetHp = m_Monster.Hp;
            int beforeBodyImpact = impactSounds;
            await CastAsync(ESkillId.RisingDragon, [InputSystem.Interact, InputSystem.Attack], "skill_rising");
            Check(m_Monster.Hp < bodyTargetHp && impactSounds > beforeBodyImpact,
                "身体技能命中没有通过 GGF 播放悟空有效命中音");
            ReleaseTestEntity(m_Monster);
            m_Monster = null;
            // 原版成功升龙不消费动作闩锁，持续按 W+J 会在收招后再次施放。
            attributes.Mp.Value = attributes.MaxMp.Value;
            Input.ActionPress(InputSystem.Interact);
            Input.ActionPress(InputSystem.Attack);
            await WaitUntilAsync(() => attributes.ActiveSkillId == ESkillId.RisingDragon, "持续组合键没有开始升龙");
            var fsm = SmokeInspection.ReadField<GameFramework.Fsm.IFsm<GameLogic.Heroes.Body.IHeroBody>>(hero, "m_BodyFsm");
            var state = fsm.CurrentState;
            var serial = typeof(GameLogic.Body.AbstractBodyState<GameLogic.Heroes.Body.IHeroBody>).GetProperty("EnterSerial",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            int entered = (int)serial.GetValue(state);
            await WaitUntilAsync(() => fsm.CurrentState == state && (int)serial.GetValue(state) > entered,
                "持续 W+J 被错误消费，升龙收招后不能再次释放", 4000);
            Check(attributes.ActiveSkillId.Value == ESkillId.RisingDragon, "持续升龙变成了普攻");
            Input.ActionRelease(InputSystem.Interact);
            Input.ActionRelease(InputSystem.Attack);
            await WaitUntilAsync(() => attributes.ActiveSkillId == null && hero.OnFloor, "连续升龙没有正常落地", 4000);
            await WaitFramesAsync(24);

            // 地面双击 D 后 J，走正式奔跑攻击分支。
            Input.ActionPress(InputSystem.MoveRight);
            await WaitFramesAsync(2);
            Input.ActionRelease(InputSystem.MoveRight);
            await WaitFramesAsync(2);
            Input.ActionPress(InputSystem.MoveRight);
            await WaitFramesAsync(2);
            Check(hero.ActionData.Running, "技能测试没有进入奔跑");
            // 宽沙包始终与突刺重叠，隔离移动脱离，验证每段只命中一次且段点确实重建攻击包。
            m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EEntityId.Gorilla,
                new EnemySpawn(hero.GlobalPosition + new Vector2(100, 0)));
            SmokeInspection.StrengthenTrainingTarget(m_Monster);
            SmokeInspection.FreezeAi(m_Monster);
            m_Monster.SetPhysicsProcess(false);
            var thrustShape = m_Monster.HurtBox.GetNode<CollisionShape2D>("CollisionShape2D");
            Shape2D originalShape = thrustShape.Shape;
            thrustShape.Shape = new RectangleShape2D { Size = new Vector2(1000, 300) };
            int thrustHits = 0;
            void OnThrustDamage(object sender, GameFramework.Event.GameEventArgs args)
            {
                if (args is DamageDealtEventArgs damage && damage.TargetEntityId == m_Monster.Id && !damage.Result.IsMiss)
                {
                    thrustHits++;
                }
            }
            GF.Event.Subscribe(DamageDealtEventArgs.EventId, OnThrustDamage);
            try
            {
                await CastAsync(ESkillId.FlameThrust, [InputSystem.Attack], "skill_thrust");
                Check(thrustHits == 4, $"突刺没有按第 1/5/8/11 源帧形成四个独立命中，实际 {thrustHits}");
                GD.Print("SMOKE-THRUST PASS: four native attack stages hit the same overlapping target once each.");
            }
            finally
            {
                GF.Event.Unsubscribe(DamageDealtEventArgs.EventId, OnThrustDamage);
                thrustShape.Shape = originalShape;
                ReleaseTestEntity(m_Monster);
                m_Monster = null;
            }
            Input.ActionRelease(InputSystem.MoveRight);
            await CastAsync(ESkillId.BlazingFlash, [InputSystem.Skill1], "skill_flash");
            await CastAsync(ESkillId.FlameStorm, [InputSystem.Skill2], "skill_storm");
            await CastAsync(ESkillId.FireDemonSlash, [InputSystem.Skill3], "skill_fire_no_target");

            // 第七源帧前跃进约 (80,-200)，在该位置提供唯一的近距可见目标。
            hero.GlobalPosition = new Vector2(400, 360);
            hero.Velocity = Vector2.Zero;
            await WaitFramesAsync(2);
            m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EEntityId.Gorilla,
                new EnemySpawn(hero.GlobalPosition + new Vector2(80, -190)));
            SmokeInspection.StrengthenTrainingTarget(m_Monster);
            SmokeInspection.FreezeAi(m_Monster);
            m_Monster.SetPhysicsProcess(false);
            int hp = m_Monster.Hp;
            int beforeEffectImpact = impactSounds;
            await CastAsync(ESkillId.FireDemonSlash, [InputSystem.Skill3], "skill_fire_combo", ESkillHitId.FireDemonCombo);
            Check(m_Monster.Hp < hp, "九连斩未通过真实判定伤害目标");
            Check(impactSounds > beforeEffectImpact, "独立技能特效命中没有通过 GGF 播放悟空有效命中音");
            ReleaseTestEntity(m_Monster);
            m_Monster = null;
            GD.Print("SMOKE-SKILL-HIT-SOUND PASS: body and independent effect hits play the original Wukong impact through GGF.");
            GD.Print("SMOKE-UI: 技能点击学习/不足提示/去重扣费/HUD同步、五招真实输入、火焰拖尾、火魔斩两分支与真实命中通过");
        }
        finally
        {
            GF.Event.Unsubscribe(GodotGameFramework.Sound.PlaySoundSuccessEventArgs.EventId, OnImpactPlayed);
            foreach (string action in skillInputs)
            {
                Input.ActionRelease(action);
            }

            hero.SetPhysicsProcess(false);
            system.CancelSkill();
            hero.GlobalPosition = originalPosition;
            hero.Velocity = Vector2.Zero;
            attributes.Mp.Value = originalMp;
            if (form.Visible)
            {
                GF.UI.CloseUIForm(form.SerialId);
            }

            flow.GetSystem<ProfileSystem>().RestoreCheckpoint(snapshot);
        }

        async Task CastAsync(ESkillId id, string[] inputs, string capture, ESkillHitId expectedEffect = ESkillHitId.None)
        {
            attributes.Mp.Value = attributes.MaxMp.Value;
            int mana = attributes.Mp.Value;
            foreach (string action in inputs)
            {
                Input.ActionPress(action);
            }

            await WaitUntilAsync(() => attributes.ActiveSkillId == id, $"输入没有释放 {id}", 2000);
            double castAt = hero.ActionData.Time;
            foreach (string action in inputs)
            {
                Input.ActionRelease(action);
            }

            Check(attributes.Mp.Value == mana - ConfigSystem.Instance.Tables.TbSkillConfig.Get(id).ManaCost, $"{id} 扣蓝不正确");
            if (expectedEffect != ESkillHitId.None)
            {
                await WaitUntilAsync(() => SmokeInspection.ReadField<HashSet<SkillEffectNode>>(hero, "m_SkillEffects")
                    .Any(x => SmokeInspection.ReadField<GameConfig.Battle.AttackConfig>(x, "m_Hit")?.Id == (int)expectedEffect), "火魔斩没有创建九连特效");
                var combo = SmokeInspection.ReadField<HashSet<SkillEffectNode>>(hero, "m_SkillEffects")
                    .Single(x => SmokeInspection.ReadField<GameConfig.Battle.AttackConfig>(x, "m_Hit")?.Id == (int)expectedEffect);
                Check(hero.GlobalPosition.DistanceTo(combo.GlobalPosition) < 1,
                    $"命中目标后身体与九连斩没有定位到同一位置：hero={hero.GlobalPosition}, combo={combo.GlobalPosition}, target={m_Monster?.GlobalPosition}, velocity={hero.Velocity}, frame={hero.AnimPlayer.CurrentAnimationPosition:0.000}");
                double comboStart = hero.AnimPlayer.CurrentAnimationPosition;
                await WaitFramesAsync(12);
                Check(hero.AnimPlayer.CurrentAnimationPosition > comboStart + .1,
                    "九连斩期间角色时间轴没有并行推进");
            }
            else
            {
                await m_Driver.ToSignal(m_Driver.GetTree().CreateTimer(.15), SceneTreeTimer.SignalName.Timeout);
            }

            await CaptureCombatAsync(capture);
            if (id == ESkillId.FireDemonSlash)
            {
                var prelude = hero.GetNode<AnimatedSprite2D>("m_FireDemonPrelude");
                Check(prelude.Visible && prelude.IsPlaying() && prelude.GlobalPosition.DistanceTo(hero.GlobalPosition) > 30,
                    "火魔前奏没有在出招原地继续播放，而是跟随升空或瞬移");
                Input.ActionPress(InputSystem.Attack);
                Input.ActionPress(InputSystem.Skill1);
                await WaitUntilAsync(() => hero.AnimPlayer.CurrentAnimationPosition >= 46d / 24d,
                    "火魔斩未进入第 47 帧下冲", 5000);
                Check(hero.ActionData.Time - castAt < (expectedEffect == ESkillHitId.None ? .4 : 2.0),
                    "火魔斩第 47 帧超过原版时间轴；独立效果不得延迟角色下冲");

                if (expectedEffect != ESkillHitId.None)
                {
                    var fireEffects = SmokeInspection.ReadField<HashSet<SkillEffectNode>>(hero, "m_SkillEffects");
                    await WaitUntilAsync(() => fireEffects.Any(x =>
                        SmokeInspection.ReadField<GameConfig.Battle.AttackConfig>(x, "m_Hit")?.Id == (int)ESkillHitId.FireDemonLanding),
                        "火魔斩在落地窗口内没有创建落地斩", 5000);
                    var landing = fireEffects.Single(x =>
                        SmokeInspection.ReadField<GameConfig.Battle.AttackConfig>(x, "m_Hit")?.Id == (int)ESkillHitId.FireDemonLanding);
                    var landingPlayer = SmokeInspection.ReadField<AnimationPlayer>(landing, "m_Player");
                    double bodyAtLanding = hero.AnimPlayer.CurrentAnimationPosition;
                    double effectAtLanding = landingPlayer.CurrentAnimationPosition;
                    await WaitFramesAsync(6);
                    Check(!hero.AnimPlayer.IsPlaying() && Math.Abs(hero.AnimPlayer.CurrentAnimationPosition - bodyAtLanding) < .001,
                        "提前落地后本体没有按官方停住姿态");
                    Check(landingPlayer.CurrentAnimationPosition > effectAtLanding + .05,
                        "身体停住时落地斩独立动画未继续播放");
                }
            }
            try
            {
                await WaitUntilAsync(() => attributes.ActiveSkillId == null, $"{id} 没有正确收招", 8000);
            }
            catch (InvalidOperationException error)
            {
                var fsm = SmokeInspection.ReadField<IFsm<IHeroBody>>(hero, "m_BodyFsm");
                var player = hero.AnimPlayer;
                string effects = string.Join(", ", SmokeInspection.ReadField<HashSet<SkillEffectNode>>(hero, "m_SkillEffects")
                    .Select(effect => $"{effect.Name}:{SmokeInspection.ReadField<AnimationPlayer>(effect, "m_Player").CurrentAnimationPosition:0.###}"));
                throw new InvalidOperationException($"{error.Message}；动作 {fsm.CurrentState?.GetType().Name}，" +
                    $"游戏时钟 {hero.ActionData.Time:0.###}，暂停 {GF.Base.IsGamePaused}，物理 {hero.IsPhysicsProcessing()}，" +
                    $"位置 {hero.GlobalPosition}，速度 {hero.Velocity}，落地 {hero.OnFloor}，" +
                    $"动画 {player.CurrentAnimation}:{player.CurrentAnimationPosition:0.###}/{player.CurrentAnimationLength:0.###}，效果 [{effects}]。", error);
            }
            if (id == ESkillId.FireDemonSlash)
            {
                await WaitFramesAsync(2);
                await WaitUntilAsync(() => !hero.GetNode<AnimatedSprite2D>("m_FireDemonPrelude").Visible,
                    "角色内前奏播放完成后没有隐藏");
                Check(attributes.ActiveSkillId == null && hero.AttackSegment < 0, "火魔斩禁用输入期间的按键被排队到收招后");
                Input.ActionRelease(InputSystem.Attack);
                Input.ActionRelease(InputSystem.Skill1);
            }
            await WaitUntilAsync(() => hero.OnFloor, $"{id} 后没有落地", 8000);
            await WaitFramesAsync(24);
        }
    }

    /// <summary>
    /// 用真实身体参数和原生播放器检查收招时点及悟空整数源帧取消窗口。
    /// </summary>
    /// <param name="hero">本轮已经初始化的悟空。</param>
    private void CheckWukongBodyTiming(AbstractHeroEntity hero)
    {
        var parameters = ((IHeroBody)hero).Params;
        int[] counts = [8, 8, 11, 20, 15];
        int[] terminalFrames = [8, 8, 10, 19, 14];
        var cancel = hero.GetType().GetMethod("CanCancelNormalAttack",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Check(cancel != null && Math.Abs(parameters.HurtTime - 14d / 24) < .001,
            "悟空受击不是第15源帧收招，或角色输入入口缺失");
        var body = (GameLogic.Body.IActorBody)hero;
        int originalSegment = hero.ActionData.AttackSegment;
        try
        {
            for (int segment = 0; segment < counts.Length; segment++)
            {
                Check(Math.Abs(parameters.AttackTimes[segment] - (terminalFrames[segment] - 1d) / 24) < .001,
                    $"悟空第{segment + 1}段收招不符源帧脚本");
                body.BeginAttack(segment);
                hero.ActionData.AttackSegment = segment;
                hero.AnimPlayer.Stop();
                hero.AnimPlayer.Play(parameters.AttackAnims[segment]);
                for (int frame = 0; frame < counts[segment]; frame++)
                {
                    foreach (double fraction in new[] { .01, .99 })
                    {
                        hero.AnimPlayer.Seek((frame + fraction) / 24d, true, updateOnly: true);
                        bool expected = segment < 4 && (frame + 1d) / counts[segment] >= .7;
                        bool actual = (bool)cancel.Invoke(hero, [true]);
                        Check(actual == expected,
                            $"悟空取消窗口没有使用整数源帧：attack={segment + 1}, segment={hero.ActionData.AttackSegment}, frame={frame + 1}, fraction={fraction}, actual={actual}, time={hero.AnimPlayer.CurrentAnimationPosition}, length={hero.AnimPlayer.CurrentAnimationLength}, expectedRatio=0.7");
                    }
                }
            }
        }
        finally
        {
            body.EndAttack();
            hero.ActionData.AttackSegment = originalSegment;
            hero.RestartAnim(HeroAnims.Idle);
        }
        GD.Print("SMOKE-BODY-TIMING PASS: five native terminal frame times and 124 integer-frame cancellation samples.");
    }

    /// <summary>
    /// 通过正式 GGF 声音入口加载指定角色音效，检查分组、解码与停止。
    /// </summary>
    /// <param name="requested">需验证的声音列表；未提供时沿用悟空七段技能音效。</param>
    /// <returns>所有音效代理检查完成的任务。</returns>
    private async Task CheckSkillSoundsAsync(IReadOnlyList<ESoundId> requested = null)
    {
        IReadOnlyList<ESoundId> ids = requested ?? new[] { ESoundId.WukongRisingDragon, ESoundId.WukongFlameThrust,
            ESoundId.WukongBlazingFlash, ESoundId.WukongFlameStorm, ESoundId.WukongFireDemonStart,
            ESoundId.WukongFireDemonCombo, ESoundId.WukongFireDemonLanding };
        foreach (ESoundId id in ids)
        {
            var definition = ConfigSystem.Instance.Tables.TbSoundConfig.Get((int)id);
            int serial = GF.Sound.PlayConfigured(id);
            try
            {
                // 只观察真实框架代理，不给生产声音模块增加测试状态。
                await WaitUntilAsync(() => m_Driver.GetTree().Root.FindChildren("*", "AudioStreamPlayer", true, false)
                    .OfType<AudioStreamPlayer>().Any(player => player.Playing && player.Stream?.ResourcePath == definition.Path),
                    $"GGF 未播放技能音效 {id}");
                var player = m_Driver.GetTree().Root.FindChildren("*", "AudioStreamPlayer", true, false)
                    .OfType<AudioStreamPlayer>().Single(value => value.Playing && value.Stream?.ResourcePath == definition.Path);
                Check(player.Bus == "SFX" && player.Stream is AudioStreamMP3 { Loop: false } && player.Stream.GetLength() > .1,
                    $"技能音效 {id} 分组、循环或解码错误");
            }
            finally
            {
                GF.Sound.StopSound(serial);
            }
        }
        GD.Print($"SMOKE-SKILL-SOUNDS PASS: {ids.Count} original MP3s decoded, played and stopped on GGF SFX.");
    }

    /// <summary>
    /// 成功受击发声，纯受伤动画不发声；死亡仍由原生方法键播放，不增加生产观测接口。
    /// </summary>
    /// <returns>声音已实际加载且复位断言完成的任务。</returns>
    private async Task CheckActorAnimationEventsAsync()
    {
        var hero = FindHero();
        hero.SetPhysicsProcess(false);
        var player = hero.GetNode<AnimationPlayer>("m_AnimPlayer");
        var sounds = new Dictionary<string, int>();
        var serials = new List<int>();
        string hurt = GF.Fsm.GetModel<ConfigurationModel>().Tables.TbSoundConfig.Get((int)ESoundId.WukongHurt).Path;
        string death = GF.Fsm.GetModel<ConfigurationModel>().Tables.TbSoundConfig.Get((int)ESoundId.WukongDeath).Path;
        string impact = GF.Fsm.GetModel<ConfigurationModel>().Tables.TbSoundConfig.Get((int)ESoundId.WukongImpact).Path;
        void OnSoundPlayed(object sender, GameFramework.Event.GameEventArgs args)
        {
            var sound = (GodotGameFramework.Sound.PlaySoundSuccessEventArgs)args;
            if (sound.SoundAssetName != hurt && sound.SoundAssetName != death && sound.SoundAssetName != impact)
            {
                return;
            }

            sounds[sound.SoundAssetName] = sounds.GetValueOrDefault(sound.SoundAssetName) + 1;
            serials.Add(sound.SerialId);
        }
        GF.Event.Subscribe(GodotGameFramework.Sound.PlaySoundSuccessEventArgs.EventId, OnSoundPlayed);
        try
        {
            player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
            player.CallbackModeMethod = AnimationMixer.AnimationCallbackModeMethod.Immediate;
            for (int expected = 1; expected <= 2; expected++)
            {
                var data = GameLogic.Battle.Attacks.AttackData.Create(0, default, 1,
                    GameConfig.Battle.EDamageKind.Real, Vector2.Zero, 1, 0);
                try
                { hero.ReceiveHit(data); }
                finally { GameFramework.ReferencePool.Release(data); }
                await WaitUntilAsync(() => sounds.GetValueOrDefault(hurt) == expected, "成功受击未播放反馈");
                hero.RestartAnim(HeroAnims.Hurt);
                player.Advance(0);
                player.Advance(.1);
                await WaitFramesAsync(3);
                Check(sounds[hurt] == expected, "受伤动画重复播放受击反馈");
            }
            hero.RestartAnim(HeroAnims.Death);
            player.Advance(0);
            await WaitUntilAsync(() => sounds.GetValueOrDefault(death) == 1, "死亡原生声音键没有播放");

            // 身体不处于技能状态，具名规则入口应无副作用；RESET 仍须恢复所有可见值。
            var actor = (GameLogic.Skills.ISkillActor)hero;
            actor.BeginSkillAnimation("skills/flame_storm");
            actor.AdvanceSkillAnimation(.2f);
            actor.EndSkillAnimation();
            var effect = hero.GetNode<AnimatedSprite2D>("m_EffectRoot/m_Effect");
            Check(effect.Animation == "empty" && effect.Frame == 0 && effect.Scale == Vector2.One &&
                hero.GetNode<CollisionShape2D>("m_HitBoxRoot/m_HitBox/CollisionShape2D").Disabled,
                "中断技能后 RESET 没有恢复光效或判定");
            hero.PlayAnim(HeroAnims.Idle);
            Check(player.IsPlaying() && player.CurrentAnimation == HeroAnims.Idle,
                "RESET 后普通动画请求仍被旧缓存吞掉");
            Check(sounds.GetValueOrDefault(hurt) == 2 && sounds.GetValueOrDefault(death) == 1 && !hero.Dead,
                "动画声音重复，或单独播放死亡视觉改变了业务事实");
            // 无攻击来源的直接伤害不能凭空播放悟空打击声；hurt动画也不承担攻击者反馈。
            foreach (var id in new[] { GameConfig.Entity.EEntityId.HuaguoshanMonkey, GameConfig.Entity.EEntityId.DemonMonkey,
                         GameConfig.Entity.EEntityId.Gorilla, GameConfig.Entity.EEntityId.MacaqueKing, GameConfig.Entity.EEntityId.TamarinKing,
                         GameConfig.Entity.EEntityId.CaveBrute, GameConfig.Entity.EEntityId.KnifeDemon, GameConfig.Entity.EEntityId.LionCamelKing,
                         GameConfig.Entity.EEntityId.Bat, GameConfig.Entity.EEntityId.BatCaveStatue })
            {
                var enemy = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(id, new EnemySpawn(new Vector2(10000, 100)));
                try
                {
                    SmokeInspection.FreezeAi(enemy);
                    enemy.SetPhysicsProcess(false);
                    var animation = enemy.GetNode<AnimationPlayer>("m_AnimPlayer");
                    animation.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
                    animation.CallbackModeMethod = AnimationMixer.AnimationCallbackModeMethod.Immediate;
                    int before = sounds.GetValueOrDefault(impact);
                    foreach (int damage in new[] { 1, enemy.MaxHp * 2 })
                    {
                        var data = GameLogic.Battle.Attacks.AttackData.Create(0, default, damage,
                            GameConfig.Battle.EDamageKind.Real, Vector2.Zero, 1, 0);
                        try
                        { enemy.ReceiveHit(data); }
                        finally { GameFramework.ReferencePool.Release(data); }
                        int expected = before;
                        if (animation.HasAnimation(EnemyAnims.Hurt))
                        {
                            enemy.RestartAnim(EnemyAnims.Hurt);
                            animation.Advance(0);
                            animation.Advance(.1);
                        }
                        await WaitFramesAsync(3);
                        Check(sounds.GetValueOrDefault(impact) == expected, $"{id} hurt 动画重复反馈");
                    }
                    Check(enemy.Dead, $"{id} 致死受击未完成");
                    var ignored = GameLogic.Battle.Attacks.AttackData.Create(0, default, 1,
                        GameConfig.Battle.EDamageKind.Real, Vector2.Zero, 1, 0);
                    try
                    { Check(enemy.ReceiveHit(ignored).IsMiss, $"{id} 死亡后仍受击"); }
                    finally { GameFramework.ReferencePool.Release(ignored); }
                    await WaitFramesAsync(3);
                    Check(sounds.GetValueOrDefault(impact) == before, $"{id} 无效受击仍然发声");
                    animation.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Physics;
                }
                finally
                {
                    enemy.SetPhysicsProcess(true); // 归还前恢复声音夹具关闭的身体推进，避免污染后续池复用测试。
                    GF.Entity.HideEntitySafe(enemy);
                }
            }
            GD.Print("SMOKE-ANIMATION-EVENTS PASS: impact belongs to attacker, victim voice/death, interrupted RESET and resume.");
        }
        finally
        {
            GF.Event.Unsubscribe(GodotGameFramework.Sound.PlaySoundSuccessEventArgs.EventId, OnSoundPlayed);
            foreach (int serial in serials)
            {
                GF.Sound.StopSound(serial);
            }

            player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Physics;
            hero.RestartAnim(HeroAnims.Idle);
        }
    }
}
