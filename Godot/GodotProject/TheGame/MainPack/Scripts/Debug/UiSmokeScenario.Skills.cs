using GameLogic.Config;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Equipment;
using GameLogic.Save;
using GameLogic.Profile;
using GameLogic.Heroes.Body;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;
using System;
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
using GameLogic.Enemies;
using GameLogic.Enemies.Body;
using GameLogic.Effects;
using GodotGameFramework.Entity;

/// <summary>真实技能窗口导航、配置展示、钱包观察及复用回归。</summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>通过正式 HUD 打开技能页，确认准备阶段没有学习或扣费行为。</summary>
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
                InputMap.ActionAddEvent(GameLogic.Input.InputSystem.SkillToggle, input);
        }
        GD.Print("SMOKE-SKILL-TOGGLE PASS: V open/close, pause, echo suppression, no reopen and remapped action.");
        await CheckSkillLearningAndCastingAsync(battleForm, form, flow);
        await CheckSkillImpactsAsync();
        await CheckSkillSoundsAsync();
        await CheckActorAnimationEventsAsync();
        CheckSkillCombatResources();
        GD.Print("SMOKE-UI: 技能限当前出战角色、禁用角色无交互、被动目录、暂停、钱包观察及窗口复用通过");
    }

    /// <summary>通过当前技能开关映射向真实视口注入键盘事件。</summary>
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

    /// <summary>替换编辑器占位内容后重新绑定，确认所有主动/被动行的数据仍来自正式目录。</summary>
    /// <param name="form">已打开的正式技能窗口。</param>
    private static void CheckSkillPreviewReplacement(SkillForm form)
    {
        var configuration = GF.Fsm.GetModel<ConfigurationModel>();
        var activePage = form.GetNode<ActiveSkillsPage>("Content/m_ActivePage");
        foreach (var group in new[] { "m_WukongRows", "m_TangsengRows" })
            foreach (var row in activePage.GetNode<Control>(group).GetChildren().Cast<GridContainer>())
            {
                foreach (var field in new[] { "Name", "Soul", "DescriptionCell/Description" })
                    row.GetNode<Label>(field).Text = "编辑器预览";
                row.GetNode<TextureButton>("IconCell/Icon").TextureNormal = null;
            }
        activePage.Refresh();
        foreach (var (group, hero) in new[]
        {
            ("m_WukongRows", GameConfig.Skills.SkillHero.Wukong),
            ("m_TangsengRows", GameConfig.Skills.SkillHero.Tangseng)
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
            foreach (var field in new[] { "Name", "NextEffect", "Soul" })
                row.GetNode<Label>(field).Text = "编辑器预览";
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

    /// <summary>确认行属于主场景，并在实际布局后检查所有列一致、不重叠且说明未溢出。</summary>
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

    /// <summary>验证实际原生动画与场景，不再验证已删除的表格指令。</summary>
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
                        continue;
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
                Check(node.GetNode<CollisionShape2D>("Visual/HitBox/CollisionShape2D").Disabled,
                    $"特效初始判定未关闭：{name}");
            }
            GD.Print("SMOKE-UI: 五招原生方法绑定、角色内前奏与四个独立特效场景通过");
        }
        finally
        {
            fixtures.Free();
        }
    }

    /// <summary>真实点击学习、提示、HUD 同步及五招输入与火魔斩分支回归。</summary>
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
        string impactPath = flow.GetModel<ConfigurationModel>().Tables.TbSoundConfig.Get((int)SoundId.WukongImpact).Path;
        void OnImpactPlayed(object sender, GameFramework.Event.GameEventArgs args)
        {
            if (args is GodotGameFramework.Sound.PlaySoundSuccessEventArgs sound && sound.SoundAssetName == impactPath)
                impactSounds++;
        }
        GF.Event.Subscribe(GodotGameFramework.Sound.PlaySoundSuccessEventArgs.EventId, OnImpactPlayed);
        string[] skillInputs = [InputSystem.Skill1, InputSystem.Skill2, InputSystem.Skill3,
            InputSystem.Interact, InputSystem.Attack, InputSystem.MoveRight];
        try
        {
            battleForm.GetNode<TextureButton>("MenuPanel/m_SkillsButton").EmitSignal(BaseButton.SignalName.Pressed);
            await WaitUntilAsync(() => form.Visible, "学习时没有打开技能窗口");
            var first = form.GetNode<TextureButton>("Content/m_ActivePage/m_WukongRows/Skill1/IconCell/Icon");
            flow.GetModel<ProfileModel>().Soul.Value = 0;
            first.EmitSignal(BaseButton.SignalName.Pressed);
            var notice = GF.UI.GetAllLoadedUIForms().OfType<SaveNoticeForm>().Single();
            Check(!system.IsLearned(SkillId.RisingDragon) && notice.GetNode<Label>("SaveFeedback/m_SaveFeedback").Text == "灵魂不足！",
                "灵魂不足没有拒绝学习并显示复用提示");
            flow.GetSystem<ProfileSystem>().AddSoul(4000);
            foreach (var definition in ConfigSystem.Instance.Tables.TbSkillConfig.DataList.Where(x => x.Hero == SkillHero.Wukong))
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
            m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.Gorilla,
                new EnemySpawn(hero.GlobalPosition + new Vector2(60, 0), 100));
            SmokeInspection.FreezeAi(m_Monster);
            m_Monster.SetPhysicsProcess(false);
            int bodyTargetHp = m_Monster.Hp;
            int beforeBodyImpact = impactSounds;
            await CastAsync(SkillId.RisingDragon, [InputSystem.Interact, InputSystem.Attack], "skill_rising");
            Check(m_Monster.Hp < bodyTargetHp && impactSounds > beforeBodyImpact,
                "身体技能命中没有通过 GGF 播放悟空有效命中音");
            ReleaseTestEntity(m_Monster);
            m_Monster = null;
            // 原版成功升龙不消费动作闩锁，持续按 W+J 会在收招后再次施放。
            attributes.Mp.Value = attributes.MaxMp.Value;
            Input.ActionPress(InputSystem.Interact);
            Input.ActionPress(InputSystem.Attack);
            await WaitUntilAsync(() => attributes.ActiveSkillId == SkillId.RisingDragon, "持续组合键没有开始升龙");
            var fsm = SmokeInspection.ReadField<GameFramework.Fsm.IFsm<GameLogic.Heroes.Body.IHeroBody>>(hero, "m_BodyFsm");
            var state = fsm.CurrentState;
            var serial = typeof(GameLogic.Body.AbstractBodyState<GameLogic.Heroes.Body.IHeroBody>).GetProperty("EnterSerial",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            int entered = (int)serial.GetValue(state);
            await WaitUntilAsync(() => fsm.CurrentState == state && (int)serial.GetValue(state) > entered,
                "持续 W+J 被错误消费，升龙收招后不能再次释放", 4000);
            Check(attributes.ActiveSkillId.Value == SkillId.RisingDragon, "持续升龙变成了普攻");
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
            await CastAsync(SkillId.FlameThrust, [InputSystem.Attack], "skill_thrust");
            Input.ActionRelease(InputSystem.MoveRight);
            await CastAsync(SkillId.BlazingFlash, [InputSystem.Skill1], "skill_flash");
            await CastAsync(SkillId.FlameStorm, [InputSystem.Skill2], "skill_storm");
            await CastAsync(SkillId.FireDemonSlash, [InputSystem.Skill3], "skill_fire_no_target");

            // 第七源帧前跃进约 (80,-200)，在该位置提供唯一的近距可见目标。
            hero.GlobalPosition = new Vector2(400, 360);
            hero.Velocity = Vector2.Zero;
            await WaitFramesAsync(2);
            m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.Gorilla,
                new EnemySpawn(hero.GlobalPosition + new Vector2(80, -190), 100));
            SmokeInspection.FreezeAi(m_Monster);
            m_Monster.SetPhysicsProcess(false);
            int hp = m_Monster.Hp;
            int beforeEffectImpact = impactSounds;
            await CastAsync(SkillId.FireDemonSlash, [InputSystem.Skill3], "skill_fire_combo", SkillHitId.FireDemonCombo);
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
                Input.ActionRelease(action);
            hero.SetPhysicsProcess(false);
            system.CancelSkill();
            hero.GlobalPosition = originalPosition;
            hero.Velocity = Vector2.Zero;
            attributes.Mp.Value = originalMp;
            if (form.Visible)
                GF.UI.CloseUIForm(form.SerialId);
            flow.GetSystem<ProfileSystem>().RestoreCheckpoint(snapshot);
        }

        async Task CastAsync(SkillId id, string[] inputs, string capture, SkillHitId expectedEffect = SkillHitId.None)
        {
            attributes.Mp.Value = attributes.MaxMp.Value;
            int mana = attributes.Mp.Value;
            foreach (string action in inputs)
                Input.ActionPress(action);
            await WaitUntilAsync(() => attributes.ActiveSkillId == id, $"输入没有释放 {id}", 2000);
            foreach (string action in inputs)
                Input.ActionRelease(action);
            Check(attributes.Mp.Value == mana - ConfigSystem.Instance.Tables.TbSkillConfig.Get(id).ManaCost, $"{id} 扣蓝不正确");
            if (expectedEffect != SkillHitId.None)
                await WaitUntilAsync(() => SmokeInspection.ReadField<HashSet<SkillEffectNode>>(hero, "m_SkillEffects")
                    .Any(x => SmokeInspection.ReadField<GameConfig.Battle.AttackConfig>(x, "m_Hit")?.Id == (int)expectedEffect), "火魔斩没有创建九连特效");
            else
                await m_Driver.ToSignal(m_Driver.GetTree().CreateTimer(.15), SceneTreeTimer.SignalName.Timeout);
            await CaptureCombatAsync(capture);
            if (id == SkillId.FireDemonSlash)
            {
                var prelude = hero.GetNode<AnimatedSprite2D>("m_FireDemonPrelude");
                Check(prelude.Visible && prelude.IsPlaying() && prelude.GlobalPosition.DistanceTo(hero.GlobalPosition) > 30,
                    "火魔前奏没有在出招原地继续播放，而是跟随升空或瞬移");
                Input.ActionPress(InputSystem.Attack);
                Input.ActionPress(InputSystem.Skill1);
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
            if (id == SkillId.FireDemonSlash)
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

    /// <summary>通过正式 GGF 声音入口加载七段技能音效，检查分组、解码与停止。</summary>
    /// <returns>所有音效代理检查完成的任务。</returns>
    private async Task CheckSkillSoundsAsync()
    {
        foreach (SoundId id in new[] { SoundId.WukongRisingDragon, SoundId.WukongFlameThrust,
            SoundId.WukongBlazingFlash, SoundId.WukongFlameStorm, SoundId.WukongFireDemonStart,
            SoundId.WukongFireDemonCombo, SoundId.WukongFireDemonLanding })
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
        GD.Print("SMOKE-UI: 七段原版技能音效经 GGF SFX 代理播放与停止通过");
    }

    /// <summary>成功受击发声，纯受伤动画不发声；死亡仍由原生方法键播放，不增加生产观测接口。</summary>
    /// <returns>声音已实际加载且复位断言完成的任务。</returns>
    private async Task CheckActorAnimationEventsAsync()
    {
        var hero = FindHero();
        hero.SetPhysicsProcess(false);
        var player = hero.GetNode<AnimationPlayer>("m_AnimPlayer");
        var sounds = new Dictionary<string, int>();
        var serials = new List<int>();
        string hurt = GF.Fsm.GetModel<ConfigurationModel>().Tables.TbSoundConfig.Get((int)SoundId.WukongHurt).Path;
        string death = GF.Fsm.GetModel<ConfigurationModel>().Tables.TbSoundConfig.Get((int)SoundId.WukongDeath).Path;
        string impact = GF.Fsm.GetModel<ConfigurationModel>().Tables.TbSoundConfig.Get((int)SoundId.WukongImpact).Path;
        void OnSoundPlayed(object sender, GameFramework.Event.GameEventArgs args)
        {
            var sound = (GodotGameFramework.Sound.PlaySoundSuccessEventArgs)args;
            if (sound.SoundAssetName != hurt && sound.SoundAssetName != death && sound.SoundAssetName != impact)
                return;
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
                    GameConfig.Battle.DamageKind.Real, Vector2.Zero, 1, 0);
                try
                { hero.ReceiveHit(data, 0); }
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
            // 普通敌人与首领均从成功受击入口发声；无击退、霸体与致死也不依赖 hurt 动画。
            foreach (var id in new[] { GameConfig.Entity.EntityId.HuaguoshanMonkey, GameConfig.Entity.EntityId.DemonMonkey,
                         GameConfig.Entity.EntityId.Gorilla, GameConfig.Entity.EntityId.MacaqueKing, GameConfig.Entity.EntityId.TamarinKing })
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
                            GameConfig.Battle.DamageKind.Real, Vector2.Zero, 1, 0);
                        try
                        { enemy.ReceiveHit(data, 0); }
                        finally { GameFramework.ReferencePool.Release(data); }
                        int expected = ++before;
                        await WaitUntilAsync(() => sounds.GetValueOrDefault(impact) == expected, $"{id} 成功受击没有反馈");
                        enemy.RestartAnim(EnemyAnims.Hurt);
                        animation.Advance(0);
                        animation.Advance(.1);
                        await WaitFramesAsync(3);
                        Check(sounds.GetValueOrDefault(impact) == expected, $"{id} hurt 动画重复反馈");
                    }
                    Check(enemy.Dead, $"{id} 致死受击未完成");
                    var ignored = GameLogic.Battle.Attacks.AttackData.Create(0, default, 1,
                        GameConfig.Battle.DamageKind.Real, Vector2.Zero, 1, 0);
                    try
                    { Check(enemy.ReceiveHit(ignored, 0).IsMiss, $"{id} 死亡后仍受击"); }
                    finally { GameFramework.ReferencePool.Release(ignored); }
                    await WaitFramesAsync(3);
                    Check(sounds.GetValueOrDefault(impact) == before, $"{id} 无效受击仍然发声");
                    animation.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Physics;
                }
                finally
                {
                    GF.Entity.HideEntitySafe(enemy);
                }
            }
            GD.Print("SMOKE-ANIMATION-EVENTS PASS: damage feedback independent of hurt animation, death method, interrupted RESET and resume.");
        }
        finally
        {
            GF.Event.Unsubscribe(GodotGameFramework.Sound.PlaySoundSuccessEventArgs.EventId, OnSoundPlayed);
            foreach (int serial in serials)
                GF.Sound.StopSound(serial);
            player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Physics;
            hero.RestartAnim(HeroAnims.Idle);
        }
    }
}
