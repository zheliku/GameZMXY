using System;
using System.Linq;
using System.Threading.Tasks;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Drops;
using GameLogic.Heroes;
using GameLogic.Input;
using GameLogic.Heroes.Body;
using GameLogic.Heroes.Effects;
using GameLogic.Level;
using GameLogic.UI;
using Godot;
using Godot.Collections;
using GodotGameFramework;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 英雄墓碑的真实物理、非透明底边与死亡后重试/地图重入回归。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>验证一死一活保留奖励、死者禁开窗，以及全灭等待每个墓碑完成才结算。</summary>
    /// <param name="flow">双人档案与本局系统。</param>
    /// <returns>失败结算后重试进入新关卡的任务。</returns>
    private async Task CheckCoopDeathAsync(ProcedureOwner flow)
    {
        ProcedureLevel procedure = FindLevelProcedure();
        LevelViewController level = FindLevel();
        AbstractHeroEntity[] heroes = SmokeInspection.ReadField<System.Collections.Generic.List<AbstractHeroEntity>>(procedure, "_heroes").ToArray();
        heroes[0].GlobalPosition = new Vector2(400, 450);
        heroes[1].GlobalPosition = new Vector2(600, 450);
        foreach (AbstractHeroEntity hero in heroes)
        {
            hero.SetPhysicsProcess(true);
        }
        LootOrbNode livingOrb = SpawnOrb(level, new Vector2(1000, 400), new(GameLogic.Drops.LootKind.Soul, 5), heroes[0].Id);
        LootOrbNode deadOrb = SpawnOrb(level, new Vector2(11000, 400), new(GameLogic.Drops.LootKind.Soul, 5), heroes[1].Id);
        var orbs = SmokeInspection.ReadField<System.Collections.Generic.HashSet<LootOrbNode>>(level, "_lootOrbs");
        var ground = SmokeInspection.ReadField<System.Collections.Generic.HashSet<GroundItemNode>>(level, "_groundItems");
        SpawnGround(level, new Vector2(12000, 400), ConfigSystem.Instance.Tables.TbPickupConfig.Get(GameConfig.Loot.PickupId.SmallPeach), null);
        int groundCount = ground.Count;
        KillHero(heroes[1]);
        AnimationPlayer p2Death = heroes[1].GetNode<AnimationPlayer>("m_Tombstone/m_AnimationPlayer");
        await WaitUntilAsync(() => p2Death.IsPlaying() && p2Death.CurrentAnimation == "play", "P2墓碑没有开始");
        p2Death.Pause();
        await WaitFramesAsync(3);
        Check(SmokeInspection.Model(level).Outcome.Value == LevelOutcome.Running && heroes[1].IsShown &&
            orbs.Contains(livingOrb) && !orbs.Contains(deadOrb) && ground.Count == groundCount &&
            heroes[1].GetNode<CollisionShape2D>("CollisionShape2D").Disabled,
            "P2死亡提前结算、隐藏实体、清掉队友奖励或未关闭身体碰撞");
        BattleForm form = CurrentBattleForm();
        Check(form.GetNode<Label>("StatusPanelP2/m_HpBar/m_HpText").Text.StartsWith("0/"), "死者HUD未显示0血");
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.KpDivide, Keycode = Key.KpDivide, Pressed = true });
        await WaitFramesAsync(2);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.KpDivide, Keycode = Key.KpDivide, Pressed = false });
        Check(!GF.UI.GetAllLoadedUIForms().OfType<InventoryForm>().Any(window => window.Visible), "死者仍可打开背包");
        float beforeX = heroes[0].GlobalPosition.X;
        Input.ActionPress(InputSystem.MoveRight);
        await WaitFramesAsync(18);
        Input.ActionRelease(InputSystem.MoveRight);
        Check(heroes[0].GlobalPosition.X > beforeX + 10, "P2死亡后P1不能继续移动");
        LevelCameraNode camera = SmokeInspection.ReadField<LevelCameraNode>(level, "_camera");
        heroes[0].SetPhysicsProcess(false);
        heroes[0].GlobalPosition = new Vector2(800, 450);
        heroes[1].GlobalPosition = new Vector2(1800, 450);
        camera.TravelTo(camera.SceneRight);
        camera.GlobalPosition = new Vector2(1100, camera.GlobalPosition.Y);
        camera._PhysicsProcess(1);
        Check(Math.Abs(camera.GlobalPosition.X - heroes[0].GlobalPosition.X) <= 42, "相机仍把死者位置计入中点");
        int soulBeforePickup = flow.GetModel<GameLogic.Profile.ProfileModel>().GetSoul(heroes[0].HeroId).Value;
        await WaitUntilAsync(() => !orbs.Contains(livingOrb), "P2死亡后P1的球不再结算");
        Check(flow.GetModel<GameLogic.Profile.ProfileModel>().GetSoul(heroes[0].HeroId).Value == soulBeforePickup + 5,
            "P1球被移除但没有给存活P1实际入账");
        await CaptureCombatAsync("coop_d3_one_dead");
        KillHero(heroes[0]);
        float allDeadCenter = camera.GlobalPosition.X;
        camera._PhysicsProcess(1);
        Check(camera.GlobalPosition.X == allDeadCenter, "全部死亡后相机仍移动");
        var pending = SmokeInspection.ReadField<System.Collections.Generic.HashSet<AbstractHeroEntity>>(level, "_deathPresentations");
        await WaitUntilAsync(() => pending.Count == 1 && pending.Contains(heroes[1]), "P1墓碑没有完成", 5000);
        Check(SmokeInspection.Model(level).Outcome.Value == LevelOutcome.Running, "P2墓碑暂停时已判全灭");
        p2Death.Play();
        await WaitUntilAsync(() => SmokeInspection.Model(level).Outcome.Value == LevelOutcome.Defeated, "全部墓碑结束后没有失败结算", 6000);
        Check(SmokeInspection.Model(level).Result.HealthPercent == 0 && pending.Count == 0 &&
            SmokeInspection.ReadField<System.Collections.Generic.Dictionary<AbstractHeroEntity, Action>>(level, "_deathHandlers").Count == 0,
            "全灭成绩非0或死亡演出订阅残留");
        await CaptureCombatAsync("coop_d3_defeat");
        await CheckFailureSettlementAsync(level);
        await WaitUntilAsync(() => FindLevel() != level && CurrentBattleForm() != null, "双人失败重试未装配", 15000);
        await WaitForLevelAsync();
        Check(SmokeInspection.ReadField<System.Collections.Generic.List<AbstractHeroEntity>>(FindLevelProcedure(), "_heroes")
            .All(hero => hero.IsAlive && !hero.GetNode<CollisionShape2D>("CollisionShape2D").Disabled), "双人重试未恢复身体与存活状态");
        GD.Print("SMOKE-COOP DEATH PASS: one survivor acts/collects, tombstone/zero HUD, no dead backpack, living camera, all native presentations before 0% defeat and retry.");
    }

    /// <summary>
    /// 在两个物理频率下空中死亡，确认不再上冲、墓碑贴地、结算可听，重试后恢复英雄。
    /// </summary>
    /// <returns>完成真实死亡、重试和地图重入的任务。</returns>
    private async Task CheckHeroDeathReentryAsync()
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        try
        {
            foreach ((int ticks, int spawnX) in new[] { (60, 200), (120, 200), (60, 1035), (120, 1035) })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                AbstractHeroEntity hero = FindHero();
                LevelViewController level = FindLevel();
                hero.GlobalPosition = new Vector2(spawnX, 100);
                hero.Velocity = new Vector2(150, -400);
                KillHero(hero);
                await WaitUntilAsync(() => hero.AnimPlayer.CurrentAnimation == HeroAnims.Death,
                    "空中死亡没有进入死亡动画");
                Check(hero.Velocity.X == 0 && hero.Velocity.Y >= 0,
                    $"墓碑继承了死亡前的上冲/横向速度：{hero.Velocity}");
                HeroTombstoneNode presentation = hero.GetNode<HeroTombstoneNode>("m_Tombstone");
                Sprite2D tombstone = presentation.GetNode<Sprite2D>("m_Sprite");
                AnimationPlayer deathPlayer = presentation.GetNode<AnimationPlayer>("m_AnimationPlayer");
                await WaitUntilAsync(() => presentation.IsOnFloor() && tombstone.Frame == 39, "墓碑稳定帧没有落地", 2500,
                    () => $"ticks={ticks}, x={spawnX}, position={presentation.GlobalPosition}, velocity={presentation.Velocity}, floor={presentation.IsOnFloor()}, frame={tombstone.Frame}");
                deathPlayer.Pause();
                Check(presentation.IsOnFloor() && presentation.Visible && !hero.GetNode<Sprite2D>("m_Body").Visible,
                    "死亡演出没有落地或墓碑/角色可见性错误");

                // 测试读取实际最后一格alpha底边与身体物理底边，不向生产实体添加观测API。
                Vector2I cell = (Vector2I)tombstone.Texture.GetSize() / new Vector2I(tombstone.Hframes, tombstone.Vframes);
                using Image image = tombstone.Texture.GetImage();
                using Image frame = image.GetRegion(new Rect2I(tombstone.FrameCoords * cell, cell));
                Rect2I opaque = frame.GetUsedRect();
                Vector2 visualBottom = tombstone.ToGlobal(tombstone.GetRect().Position +
                    new Vector2(opaque.Position.X + opaque.Size.X / 2f, opaque.End.Y));
                CollisionShape2D collider = presentation.GetNode<CollisionShape2D>("CollisionShape2D");
                Check(!collider.Disabled && hero.GetNode<CollisionShape2D>("CollisionShape2D").Disabled,
                    "死亡仍沿用英雄的大胶囊，或墓碑落地锚点未启用");
                Vector2 foot = collider.ToGlobal(new Vector2(0, collider.Shape.GetRect().End.Y));
                Check(Math.Abs(visualBottom.Y - foot.Y) < 2,
                    $"墓碑底边未贴物理地面：visual={visualBottom.Y}, foot={foot.Y}");
                var query = PhysicsRayQueryParameters2D.Create(visualBottom + Vector2.Up * 12,
                    visualBottom + Vector2.Down * 12, 9);
                Dictionary contact = hero.GetWorld2D().DirectSpaceState.IntersectRay(query);
                Check(contact.Count > 0 && Math.Abs(contact["position"].AsVector2().Y - visualBottom.Y) < 2,
                    $"墓碑与实际坡面/地面仍有间隙：x={spawnX}, visual={visualBottom}");
                float height = opaque.Size.Y * tombstone.GlobalScale.Y;
                Check(height is >= 100 and <= 120, $"墓碑可见高度异常：{height}");
                await CaptureCombatAsync($"hero_tombstone_{ticks}_{spawnX}");
                deathPlayer.Play();
                await CheckFailureSettlementAsync(level);
                await WaitUntilAsync(() => FindLevel() != level && CurrentBattleForm() != null &&
                    GF.Procedure.CurrentProcedure is ProcedureLevel, "失败后重试未装配新关卡", 15000);
                await WaitFramesAsync(6);
                CheckHeroRevived();
                GD.Print($"SMOKE-HERO-DEATH PASS: {ticks}Hz, x={spawnX}, rising velocity cleared, actual floor ray, grounded alpha bottom, audible defeat, retry restored body/weapon");
            }
            await CheckFailureReturnMapAsync();
            await WaitFramesAsync(6);
            CheckHeroRevived();
            GD.Print("SMOKE-HERO-REENTRY PASS: repeated deaths, retry, map return/reentry, live body and hidden tombstone");
        }
        finally
        {
            Engine.PhysicsTicksPerSecond = originalTicks;
        }
    }

    /// <summary>
    /// 重试或地图重入后检查真实节点与动画，延迟几帧排除旧死亡播放器再次写回。
    /// </summary>
    private void CheckHeroRevived()
    {
        AbstractHeroEntity hero = FindHero();
        Check(!hero.Dead && hero.Hp > 0 && hero.GetNode<Sprite2D>("m_Body").Visible &&
            hero.GetNode<Sprite2D>("m_Weapon").Visible && !hero.GetNode<HeroTombstoneNode>("m_Tombstone").Visible &&
            hero.AnimPlayer.CurrentAnimation != HeroAnims.Death &&
            !hero.GetNode<CollisionShape2D>("CollisionShape2D").Disabled &&
            !hero.GetNode<HeroTombstoneNode>("m_Tombstone").IsPhysicsProcessing(),
            "死亡重入后残留墓碑、隐藏身体/武器或旧死亡动画");
    }
}
