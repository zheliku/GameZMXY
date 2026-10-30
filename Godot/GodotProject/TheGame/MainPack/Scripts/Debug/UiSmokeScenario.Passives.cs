using System.Linq;
using System.Threading.Tasks;
using GameConfig.Skills;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.Skills;
using GameLogic.UI;
using Godot;
using GodotGameFramework;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 被动页真实控件、技能模型、属性、暂停和磁盘重入验证。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 点击全部五项到满级，校验余额不足提示、总效果和重开后的保存数据。
    /// </summary>
    /// <param name="battle">真实战斗窗口。</param>
    /// <param name="flow">隔离测试所属架构。</param>
    /// <returns>按钮交易和重入全部完成的任务。</returns>
    private async Task CheckPassiveUpgradesAsync(BattleForm battle, ProcedureOwner flow)
    {
        var profile = flow.GetModel<ProfileModel>();
        var levels = flow.GetModel<SkillModel>().GetPassiveLevels(profile.ActiveHeroId);
        profile.RestoreSoul(4999);
        battle.GetNode<TextureButton>("MenuPanel/m_SkillsButton").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntilAsync(() => GF.UI.GetAllLoadedUIForms().OfType<SkillForm>().Any(form => form.Visible),
            "被动专项未打开技能窗口");
        var form = GF.UI.GetAllLoadedUIForms().OfType<SkillForm>().Single(form => form.Visible);
        form.GetNode<TextureButton>("Content/Footer/m_Passive").EmitSignal(BaseButton.SignalName.Pressed);
        const string rows = "Content/m_PassivePage/m_PassiveRows/";
        var firstButton = form.GetNode<TextureButton>(rows + "Passive1/Upgrade");
        Check(!firstButton.Disabled && firstButton.MouseDefaultCursorShape == Control.CursorShape.PointingHand,
            "被动升级仍没有真实可点击按钮");
        // 原生鼠标事件检查普通、悬停、按下三态；松开后执行真实余额不足请求。
        var viewport = m_Driver.GetViewport();
        Vector2 outside = new(900, 570);
        viewport.PushInput(new InputEventMouseMotion { Position = outside, GlobalPosition = outside }, true);
        await WaitFramesAsync(3);
        Check(!firstButton.IsHovered(), "升级按钮普通态仍然悬停");
        await CaptureCombatAsync("passive_upgrade_normal");
        Vector2 pointer = firstButton.GetGlobalRect().GetCenter();
        viewport.PushInput(new InputEventMouseMotion { Position = pointer, GlobalPosition = pointer }, true);
        await WaitFramesAsync(3);
        Check(firstButton.IsHovered() && !firstButton.ButtonPressed, "升级按钮未切到悬停状态");
        await CaptureCombatAsync("passive_upgrade_hover");
        viewport.PushInput(new InputEventMouseButton { Position = pointer, GlobalPosition = pointer, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        await WaitFramesAsync(3);
        Check(firstButton.ButtonPressed, "升级按钮原生按下状态没有生效");
        await CaptureCombatAsync("passive_upgrade_pressed");
        viewport.PushInput(new InputEventMouseButton { Position = pointer, GlobalPosition = pointer, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        await WaitFramesAsync(3);
        Check(profile.Soul.Value == 4999 && levels[PassiveId.Vitality] == 0 &&
            GF.UI.GetAllLoadedUIForms().OfType<SaveNoticeForm>().Single().Visible,
            "被动余额不足仍然扣费，或没有显示提示");
        profile.RestoreSoul(375000);
        int expectedSoul = profile.Soul.Value;
        foreach (var definition in flow.GetModel<GameLogic.Config.ConfigurationModel>().Tables.TbPassiveConfig.DataList)
        {
            int row = (int)definition.PassiveId;
            var button = form.GetNode<TextureButton>($"{rows}Passive{row}/Upgrade");
            foreach (var definitionLevel in definition.Levels)
            {
                button.EmitSignal(BaseButton.SignalName.Pressed);
                expectedSoul -= definitionLevel.LearnSoul;
                Check(profile.Soul.Value == expectedSoul && levels[definition.PassiveId] == definitionLevel.Level,
                    $"被动{definition.PassiveId}/{definitionLevel.Level}费用或等级不符");
                Check(form.GetNode<Label>($"{rows}Passive{row}/Level").Text == definitionLevel.Level.ToString() &&
                    form.GetNode<Label>($"{rows}Passive{row}/CurrentEffect").Text == definitionLevel.Description,
                    "被动升级后当前等级/总效果没有同步刷新");
            }
            Check(button.Disabled && !button.Visible, "被动满级后仍然显示升级按钮或可升级");
            button.EmitSignal(BaseButton.SignalName.Pressed);
            Check(profile.Soul.Value == expectedSoul, "直接发送满级按钮信号仍然扣费");
        }
        var hero = flow.GetModel<HeroModel>();
        Check((hero.MaxHp.Value, hero.MaxMp.Value, hero.Crit.Value, hero.HpRegen.Value, hero.MpRegen.Value) ==
            (680, 650, .06f, 6, 6), "被动未正确接入实际英雄最终属性");
        await CaptureCombatAsync("passive_upgrades_maximum");
        GF.UI.CloseUIForm(form.SerialId);
        await WaitFramesAsync(2);
        Check(!GF.Base.IsGamePaused, "关闭被动页仍残留暂停");
        Check(await flow.GetSystem<SaveSystem>().SaveCheckpointAsync(profile), "被动升级检查点写盘失败");
        var disk = await ReadDiskAsync();
        Check(disk.Heroes.Single().PassiveLevels.Values.All(level => level == 5), "磁盘没有保存五项被动等级");
        // 关卡中途重入按规则回滚进关快照；这里通过真实死亡提交本次购买后再重试。
        var previousLevel = FindLevel();
        KillHero(FindHero());
        await CheckFailureSettlementAsync(previousLevel);
        await WaitForLevelAsync();
        await WaitUntilAsync(() => FindLevel() != previousLevel && CurrentBattleForm() != null,
            "被动升级提交后重试没有进入新关卡", 15000);
        await WaitFramesAsync(5);
        Check(flow.GetModel<HeroModel>().MaxHp.Value == 680 && flow.GetModel<HeroModel>().Crit.Value == .06f &&
            ReferenceEquals(levels, flow.GetModel<SkillModel>().GetPassiveLevels(profile.ActiveHeroId)),
            "重开关卡后被动丢失、重复叠加或换成另一份数据");
        GD.Print("SMOKE-PASSIVE-UPGRADES PASS: all 25 purchases, insufficient soul, UI binding, maximum refusal, five real attributes, disk checkpoint and reentry");
    }
}
