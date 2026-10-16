using GameLogic.Config;
using GameLogic.Architecture;
using GameLogic.Equipment;
using GameLogic.Save;
using GameLogic.Profile;
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

/// <summary>真实技能窗口导航、配置展示、钱包观察及复用回归。</summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>通过正式 HUD 打开技能页，确认准备阶段没有学习或扣费行为。</summary>
    /// <param name="hud">正式 HUD。</param>
    /// <param name="flow">隔离测试档案。</param>
    /// <param name="level">当前关卡。</param>
    /// <returns>全部技能面板检查完成的任务。</returns>
    private async Task CheckSkillsAsync(BattleHud hud, ProcedureOwner flow, LevelViewController level)
    {
        int soul = flow.GetModel<ProfileModel>().Soul.Value;
        var entrance = hud.GetNode<TextureButton>("MenuPanel/m_SkillsButton");
        entrance.EmitSignal(BaseButton.SignalName.Pressed);
        entrance.EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntilAsync(() => GF.UI.GetAllLoadedUIForms().OfType<SkillForm>().Any(x => x.Visible), "技能入口没有打开面板");
        var form = GF.UI.GetAllLoadedUIForms().OfType<SkillForm>().Single(x => x.Visible);
        Check(GF.Base.IsGamePaused, "技能窗口没有暂停本关");
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
        form.GetNode<TextureButton>("Content/Header/m_Back").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitFramesAsync(2);
        Check(!form.Visible && !GF.Base.IsGamePaused, "返回没有关闭技能页或恢复战斗");
        flow.GetModel<ProfileModel>().RestoreSoul(soul);
        Check(balance.Text == (soul + 7).ToString(), "关闭后仍保留钱包订阅");
        entrance.EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntilAsync(() => form.Visible, "技能窗口没有从池复用");
        Check(form.GetNode<Control>("Content/m_ActivePage").Visible &&
            form.GetNode<Control>(active + "m_WukongRows").Visible && balance.Text == soul.ToString(), "技能窗口复用残留旧页或余额");
        form._Input(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        await WaitFramesAsync(2);
        Check(!form.Visible && !GF.Base.IsGamePaused, "Escape没有关闭技能面板");
        await CheckSkillSoundsAsync();
        CheckSkillCombatResources();
        GD.Print("SMOKE-UI: 技能限当前出战角色、禁用角色无交互、被动目录、暂停、钱包观察及窗口复用通过");
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
                foreach (var field in new[] { "Name", "Soul", "Key", "DescriptionCell/Description" })
                    row.GetNode<Label>(field).Text = "编辑器预览";
                row.GetNode<TextureRect>("IconCell/Icon").Texture = null;
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
                    row.GetNode<Label>("Key").Text == definition.KeyHint &&
                    row.GetNode<Label>("DescriptionCell/Description").Text == definition.Description &&
                    row.GetNode<TextureRect>("IconCell/Icon").Texture.ResourcePath == definition.UnlearnedIconPath,
                    "主动技能预填内容没有被正式数据替换");
            }
        }

        var passivePage = form.GetNode<PassiveSkillsPage>("Content/m_PassivePage");
        var passives = configuration.GetPassives(1);
        foreach (var row in passivePage.GetNode<Control>("m_PassiveRows").GetChildren().Cast<GridContainer>())
            foreach (var field in new[] { "Name", "NextEffect", "Soul" })
                row.GetNode<Label>(field).Text = "编辑器预览";
        passivePage.Refresh();
        for (int i = 0; i < passives.Count; i++)
        {
            var row = passivePage.GetNode<GridContainer>($"m_PassiveRows/Passive{i + 1}");
            Check(row.GetNode<Label>("Name").Text == passives[i].NameCn &&
                row.GetNode<Label>("NextEffect").Text == passives[i].Description &&
                row.GetNode<Label>("Soul").Text == passives[i].LearnSoul.ToString(),
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

    /// <summary>用正式表加载动作与特效，核对动画库绑定、独立判定和无自动施放约定。</summary>
    private void CheckSkillCombatResources()
    {
        var tables = ConfigSystem.Instance.Tables;
        var fixtures = new Node();
        var actorDurations = new Dictionary<GameConfig.Skills.SkillId, double>();
        var effectDurations = new Dictionary<GameConfig.Skills.SkillEffectId, double>();
        try
        {
            var hero = GD.Load<PackedScene>("res://TheGame/Entitys/WukongEntity.tscn").Instantiate();
            fixtures.AddChild(hero);
            var player = hero.GetNode<AnimationPlayer>("m_AnimPlayer");
            foreach (var action in tables.TbSkillActionConfig.DataList)
            {
                Check(player.HasAnimation(action.Animation), $"技能动作表未绑定正式实体动画 {action.Animation}");
                string libraryName = action.Animation.Split('/')[0];
                Check(player.GetAnimationLibrary(libraryName).ResourcePath == action.AnimationLibraryPath,
                    $"技能动作库路径不一致 {action.SkillId}");
                var clip = player.GetAnimation(action.Animation);
                actorDurations.Add(action.SkillId, clip.Length);
                Check(clip.Length > 0 && clip.LoopMode == Animation.LoopModeEnum.None, $"技能动作长度或循环无效 {action.SkillId}");
                for (int track = 0; track < clip.GetTrackCount(); track++)
                    Check(clip.TrackGetType(track) == Animation.TrackType.Value, $"技能动画含玩法调用轨道 {action.SkillId}");
            }
            foreach (var effect in tables.TbSkillEffectConfig.DataList)
            {
                var node = GD.Load<PackedScene>(effect.ScenePath).Instantiate();
                fixtures.AddChild(node);
                var animation = node.GetNode<AnimationPlayer>("AnimationPlayer");
                Check(animation.HasAnimation("play") && animation.Autoplay.ToString() == "",
                    $"技能特效缺少时间轴或提前自动播放 {effect.Id}");
                Check(node.GetNode<CollisionShape2D>("HitBox/CollisionShape2D").Disabled,
                    $"技能特效初始判定未关闭 {effect.Id}");
                effectDurations.Add(effect.Id, animation.GetAnimation("play").Length);
            }
            foreach (var entry in tables.TbSkillTimelineConfig.DataList)
            {
                double duration = entry.SourceEffectId == GameConfig.Skills.SkillEffectId.None
                    ? actorDurations[entry.SkillId] : effectDurations[entry.SourceEffectId];
                Check(entry.AtSeconds < duration && entry.UntilSeconds < duration,
                    $"技能事件超出所属角色或特效局部时间轴 {entry.Id}");
                Check(entry.SeekSeconds < actorDurations[entry.SkillId], $"技能事件跳转超出角色动画 {entry.Id}");
            }
            GD.Print("SMOKE-UI: 正式技能动作/特效与47条秒制事件的局部时间轴关联通过；未接入施放");
        }
        finally
        {
            // 离树 Node 的 Dispose 只释放 C# 绑定，必须显式 Free 夹具及其子树。
            fixtures.Free();
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
}
