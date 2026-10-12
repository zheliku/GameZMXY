using System;
using System.Linq;
using System.Threading.Tasks;
using GameLogic;
using GameLogic.Heroes;
using GameLogic.Level;
using GameLogic.UI;
using Godot;
using GodotGameFramework;

/// <summary>背包与失败结算的真实窗口回归，直接检查节点和既有状态源。</summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>验证 HUD 点击、暂停、属性/钱包刷新、退订及池复用。</summary>
    /// <param name="hud">正式战斗窗口。</param>
    /// <param name="hero">本关英雄。</param>
    /// <param name="context">本槽档案。</param>
    /// <param name="level">本关运行。</param>
    /// <returns>全部背包检查完成的任务。</returns>
    private async Task CheckInventoryAsync(BattleHud hud, HeroEntity hero, GameContext context, LevelModule level)
    {
        hud.GetNode<TextureButton>("MenuPanel/m_InventoryButton").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntilAsync(() => GF.UI.GetAllLoadedUIForms().OfType<InventoryForm>().Any(x => x.Visible), "HUD 没有打开背包");
        InventoryForm form = GF.UI.GetAllLoadedUIForms().OfType<InventoryForm>().Single(x => x.Visible);
        Check(GF.Base.IsGamePaused, "背包打开后战斗未暂停");
        Check(form.GetNode<GridContainer>("Window/BagPanel/BagGrid").GetChildCount() == 25, "背包不是 5×5 格");
        Check(form.GetNode<Label>("Window/CharacterPanel/Attributes/Hp/m_Hp").Text == $"{hero.Vitals.Hp} / {hero.Vitals.MaxHp}", "背包生命不是实际值");
        var body = form.GetNode<Sprite2D>("Window/CharacterPanel/m_Preview/Body");
        int initialFrame = body.Frame;
        double elapsed = SmokeInspection.ReadField<double>(level, "m_ElapsedSeconds");
        await WaitUntilAsync(() => body.Frame != initialFrame, "暂停时背包角色预览停止播放");
        Check(Math.Abs(SmokeInspection.ReadField<double>(level, "m_ElapsedSeconds") - elapsed) < .001, "背包暂停计入关卡时间");
        int originalSoul = context.Profile.Wallet.Soul;
        context.Profile.Wallet.AddSoul(17);
        Check(form.GetNode<Label>("Window/BagPanel/Footer/Soul/m_Soul").Text == (originalSoul + 17).ToString(), "背包未响应钱包变化");
        await WaitUntilAsync(() => !GF.UI.GetAllLoadedUIForms().OfType<SaveNoticeForm>().Single().GetNode<Label>("SaveFeedback/m_SaveFeedback").Visible,
            "保存成功提示在背包暂停时没有结束", 5000);
        await CaptureCombatAsync("inventory_form");
        form.GetNode<TextureButton>("Window/Header/m_Close").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitFramesAsync(2);
        Check(!GF.Base.IsGamePaused && !form.Visible, "关闭背包没有恢复战斗");
        string closedValue = form.GetNode<Label>("Window/BagPanel/Footer/Soul/m_Soul").Text;
        context.Profile.Wallet.Restore(originalSoul);
        Check(form.GetNode<Label>("Window/BagPanel/Footer/Soul/m_Soul").Text == closedValue, "关闭后背包仍订阅钱包");
        hud.GetNode<TextureButton>("MenuPanel/m_InventoryButton").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntilAsync(() => form.Visible, "背包未复用原窗口");
        Check(form.GetNode<Label>("Window/BagPanel/Footer/Soul/m_Soul").Text == originalSoul.ToString(), "重新打开未拉取当前钱包");
        form.GetNode<TextureButton>("Window/Header/m_Close").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitFramesAsync(2);
        GD.Print("SMOKE-UI: inventory actual attributes, 25 cells, live soul, pause, animated preview, unsubscribe and reuse passed");
    }

    /// <summary>等待失败结算并检查成绩、按钮和暂停，然后按重试。</summary>
    /// <param name="run">已提交死亡检查点的关卡。</param>
    /// <returns>已提交重试请求的任务。</returns>
    private async Task CheckFailureSettlementAsync(LevelModule run)
    {
        await WaitUntilAsync(() => GF.UI.GetAllLoadedUIForms().OfType<SettlementForm>().Any(x => x.Visible), "死亡后未显示失败结算");
        var form = GF.UI.GetAllLoadedUIForms().OfType<SettlementForm>().Single(x => x.Visible);
        Check(run.Result.Outcome == LevelOutcome.Defeated && run.Result.HealthPercent == 0, "失败成绩未冻结死亡状态");
        Check(form.GetNode<TextureButton>("Content/Actions/m_Retry").Visible && GF.Base.IsGamePaused, "失败结算缺少重试或未暂停");
        await WaitFramesAsync(12);
        Check(ReferenceEquals(SmokeInspection.Level(GF.Procedure.CurrentProcedure as ProcedureLevel), run), "失败结算自动重开了关卡");
        await CaptureCombatAsync("settlement_failure");
        form.GetNode<TextureButton>("Content/Actions/m_Retry").EmitSignal(BaseButton.SignalName.Pressed);
    }
    /// <summary>失败页池复用后仍可选择返回地图，再次进关不残留暂停或成绩。</summary>
    /// <returns>返回地图并重新进关的任务。</returns>
    private async Task CheckFailureReturnMapAsync()
    {
        HeroEntity hero = FindHero();
        LevelModule level = FindLevel();
        KillHero(hero);
        await WaitUntilAsync(() => GF.UI.GetAllLoadedUIForms().OfType<SettlementForm>().Any(x => x.Visible), "第二次死亡没有显示结算");
        var form = GF.UI.GetAllLoadedUIForms().OfType<SettlementForm>().Single(x => x.Visible);
        Check(level.Result.DefeatedMonsters == 0 && form.GetNode<Label>("Content/Results/Kills/m_Kills").Text == "0", "结算池复用遗留上局成绩");
        form.GetNode<TextureButton>("Content/Actions/m_ReturnMap").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntilAsync(() => GF.Procedure.CurrentProcedure is ProcedureMap && GF.UI.GetAllLoadedUIForms().OfType<MapForm>().Any(x => x.Visible), "失败结算没有返回地图");
        Check(!GF.Base.IsGamePaused && !form.Visible, "失败返回地图残留结算或暂停");
        await WaitUntilAsync(() => { FrontendSmokeScenario.AdvanceToLevel(); return GF.Procedure.CurrentProcedure is ProcedureLevel && CurrentHud() != null; }, "失败返回地图后无法重新进关");
        GD.Print("SMOKE-UI: failure settlement retry, return map, retained checkpoint and pooled results passed");
    }

}
