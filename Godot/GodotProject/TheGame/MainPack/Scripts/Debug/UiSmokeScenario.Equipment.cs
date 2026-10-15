using GameLogic.Architecture;
using GameLogic.Save;
using GameLogic.Profile;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GameConfig.Equipment;
using GameConfig.Loot;
using GameLogic;
using GameLogic.Drops;
using GameLogic.Equipment;
using GameLogic.Heroes;
using GameLogic.Level;
using GameLogic.UI;
using Godot;
using GodotGameFramework;

/// <summary>真实地面物品、接触恢复、满包与背包详情验证；只在测试侧使用反射。</summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>验证实际物理落地、暂停、恢复量、满包保留和池复用。</summary>
    /// <param name="flow">受测档案。</param>
    /// <param name="hero">受测英雄。</param>
    /// <param name="level">地面物品所有者。</param>
    /// <returns>真实引擎验证。</returns>
    private async Task CheckGroundItemsAsync(ProcedureOwner flow, AbstractHeroEntity hero, LevelViewController level)
    {
        var tables = ConfigSystem.Instance.Tables;
        var owned = SmokeInspection.ReadField<HashSet<GroundItemNode>>(level, "m_GroundItems");
        var original = flow.GetModel<PlayerModel>().Inventory.Items.ToArray();
        SpawnGround(level, hero.GlobalPosition + new Vector2(180, -80), tables.TbPickupConfig.Get(PickupId.SmallPeach), null);
        var peach = owned.Single();
        await WaitUntilAsync(() => peach.IsOnFloor(), "桃子没有落到实际关卡地形上", 5000);
        // 同一玩家物理层的非英雄，以及其他作用域英雄，都不能给本会话发放收益。
        CharacterBody2D otherBody = new()
        {
            GlobalPosition = peach.GlobalPosition,
            CollisionLayer = LayerMask.LayerToMask2D("PlayerBody"),
            CollisionMask = 0
        };
        otherBody.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 20 } });
        level.AddChild(otherBody);
        await WaitFramesAsync(6);
        Check(peach.GetNode<Area2D>("PickupArea").GetOverlappingBodies().Contains(otherBody) &&
            !SmokeInspection.ReadField<bool>(peach, "m_Collected"), "非英雄碰撞体触发了拾取");
        otherBody.QueueFree();
        WukongEntity foreignHero = new()
        {
            Position = peach.GlobalPosition,
            CollisionLayer = LayerMask.LayerToMask2D("PlayerBody"),
            CollisionMask = 0
        };
        foreignHero.SetPhysicsProcess(false);
        typeof(GameLogic.Entity.AbstractActorEntity).GetProperty("IsShown").SetValue(foreignHero, true);
        foreignHero.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 20 } });
        level.AddChild(foreignHero);
        foreignHero.SetPhysicsProcess(false);
        foreignHero.SetProcess(false);
        await WaitFramesAsync(6);
        Check(peach.GetNode<Area2D>("PickupArea").GetOverlappingBodies().Contains(foreignHero) &&
            !SmokeInspection.ReadField<bool>(peach, "m_Collected"), "其他会话英雄领取了本关物品");
        foreignHero.QueueFree();
        float age = SmokeInspection.ReadField<float>(peach, "m_Age");
        GF.Base.PauseGame();
        await WaitFramesAsync(12);
        Check(SmokeInspection.ReadField<float>(peach, "m_Age") == age, "暂停时地面物品仍在过期");
        hero.Vitals.Damage(hero.Vitals.Hp - 1);
        peach.GlobalPosition = hero.HurtBox.GetGlobalBounds().GetCenter();
        await WaitFramesAsync(6);
        Check(hero.Vitals.Hp == 1 && !SmokeInspection.ReadField<bool>(peach, "m_Collected"),
            "暂停时接触仍发放收益");
        GF.Base.ResumeGame();
        await WaitUntilAsync(() => SmokeInspection.ReadField<bool>(peach, "m_Collected"), "真实拾取区域没有识别接触英雄", 3000);
        Check(owned.Count == 1 && hero.Vitals.Hp == Math.Min(hero.Vitals.MaxHp, 101), "小桃子未即时入账或未保留领取动画");
        var icon = peach.GetNode<Sprite2D>("Icon");
        var pickupPlayer = peach.GetNode<AnimationPlayer>("AnimationPlayer");
        Check(pickupPlayer.IsPlaying() && !peach.GetNode<Polygon2D>("Shadow").Visible,
            "领取后未开始上移淡出或仍显示地面阴影");
        GF.Base.PauseGame();
        pickupPlayer.Seek(0, update: true);
        peach._PhysicsProcess(.2);
        Check(Math.Abs(icon.Position.Y + 61.75f) < .02f && Math.Abs(icon.Modulate.A - .5625f) < .001f,
            "领取动画没有按原版 0.8 秒、上移 100 像素的二次缓出推进");
        Vector2 pausedIconPosition = icon.Position;
        float pausedIconAlpha = icon.Modulate.A;
        await WaitFramesAsync(12);
        Check(icon.Position == pausedIconPosition && icon.Modulate.A == pausedIconAlpha,
            "暂停时领取动画继续推进");
        peach._PhysicsProcess(.2);
        await CaptureCombatAsync("pickup_rising");
        Check(hero.Vitals.Hp == Math.Min(hero.Vitals.MaxHp, 101), "领取动画重复发放恢复收益");
        peach._PhysicsProcess(.39);
        Check(owned.Contains(peach), "领取动画未满 0.8 秒就提前回池");
        peach._PhysicsProcess(.02);
        Check(owned.Count == 0, "领取动画结束后未回池");
        GF.Base.ResumeGame();
        SpawnGround(level, hero.GlobalPosition + new Vector2(180, 0), tables.TbPickupConfig.Get(PickupId.ManaBottle), null);
        var bottle = owned.Single();
        Check(ReferenceEquals(peach, bottle), "地面物品没有使用 GGF 节点池复用");
        Check(icon.Position == new Vector2(0, -18) && icon.Modulate.A == 1 &&
            bottle.GetNode<Polygon2D>("Shadow").Visible && !pickupPlayer.IsPlaying(),
            "地面物品复用后未恢复原点、透明度、阴影或动画状态");
        hero.Vitals.TrySpendMp(hero.Vitals.Mp);
        bottle._PhysicsProcess(1.0 / 60);
        await WaitFramesAsync(4);
        Check(hero.Vitals.Mp == 0 && !SmokeInspection.ReadField<bool>(bottle, "m_Collected"),
            "池复用继承了旧的英雄接触");
        bottle.GlobalPosition = hero.HurtBox.GetGlobalBounds().GetCenter();
        await WaitUntilAsync(() => SmokeInspection.ReadField<bool>(bottle, "m_Collected"), "蓝瓶的真实接触未触发领取", 3000);
        Check(owned.Count == 1 && hero.Vitals.Mp == Math.Min(hero.Vitals.MaxMp, 100), "蓝瓶没有接触回蓝或超出上限");
        bottle._PhysicsProcess(.81);
        Check(owned.Count == 0, "蓝瓶领取动画结束后未回池");
        var floatingText = SmokeInspection.ReadField<GameLogic.FloatingText.FloatingTextPresenter>(level, "m_FloatingText");
        var numbers = SmokeInspection.ReadField<HashSet<GameLogic.FloatingText.FloatingTextNode>>(floatingText, "m_Texts");
        Check(numbers.Count == 2, "恢复品未各显示一次恢复数字");
        foreach (var number in numbers)
        {
            var digits = SmokeInspection.ReadField<Sprite2D[]>(number, "m_Digits");
            var digit = digits[0];
            Check(digit.Visible && digit.Texture.ResourcePath.Contains("recovery_number_"), "恢复数字没有使用恢复专用位图");
            CheckDigitSpacing(number, 3, false);
        }
        var manaNumber = numbers.Single(x => SmokeInspection.ReadField<Sprite2D[]>(x, "m_Digits")[0].Texture.ResourcePath.EndsWith("mana.png"));
        var healthNumber = numbers.Single(x => SmokeInspection.ReadField<Sprite2D[]>(x, "m_Digits")[0].Texture.ResourcePath.EndsWith("health.png"));
        // 仅把同时出现的截图夹具分开；生产规则仍从各自领取点向上移动。
        foreach (var (number, offset) in new[] { (healthNumber, -35f), (manaNumber, 35f) })
        {
            Vector2 start = SmokeInspection.ReadField<Vector2>(number, "m_Start") + new Vector2(offset, 0);
            typeof(GameLogic.FloatingText.FloatingTextNode).GetField("m_Start", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(number, start);
            number.GlobalPosition += new Vector2(offset, 0);
        }
        GF.Base.PauseGame();
        await CaptureCombatAsync("recovery_numbers");
        float recoveryAge = SmokeInspection.ReadField<float>(manaNumber, "m_Age");
        await WaitFramesAsync(3);
        Check(SmokeInspection.ReadField<float>(manaNumber, "m_Age") == recoveryAge, "暂停时恢复数字继续上升");
        GF.Base.ResumeGame();
        Vector2 recoveryStart = SmokeInspection.ReadField<Vector2>(manaNumber, "m_Start");
        manaNumber._Process(.4);
        Check(Math.Abs(manaNumber.GlobalPosition.Y - (recoveryStart.Y - 75 * (recoveryAge + .4))) < .01,
            "恢复数字没有按秒上升");
        foreach (var number in numbers.ToArray())
            number._Process(1);
        Check(numbers.Count == 0, "恢复数字没有按一秒生命周期回池");

        var item = flow.GetSystem<EquipmentSystem>().Roll(Guid.NewGuid(), EquipmentId.CommonPineStaff, () => .75f);
        while (flow.GetModel<PlayerModel>().Inventory.Items.Count < flow.GetModel<PlayerModel>().Inventory.Capacity)
            flow.GetModel<PlayerModel>().Inventory.TryAdd(flow.GetSystem<EquipmentSystem>().Roll(Guid.NewGuid(), EquipmentId.RoughCassock, () => 0));
        SpawnGround(level, hero.HurtBox.GetGlobalBounds().GetCenter(), null, item);
        var gear = owned.Single();
        await WaitFramesAsync(6);
        Check(gear.GetNode<Area2D>("PickupArea").GetOverlappingBodies().Contains(hero) && owned.Count == 1 &&
            !flow.GetModel<PlayerModel>().Inventory.Items.Any(x => x.InstanceId == item.InstanceId), "满包时装备消失或超出容量");
        Check(!gear.GetNode<AnimationPlayer>("AnimationPlayer").IsPlaying(), "满包拒绝领取却播放了领取动画");
        flow.GetModel<PlayerModel>().Inventory.Restore(original);
        await WaitUntilAsync(() => SmokeInspection.ReadField<bool>(gear, "m_Collected"), "满包腾出空间后未重试实际接触", 3000);
        Check(owned.Count == 1 && flow.GetModel<PlayerModel>().Inventory.Items.Last().InstanceId == item.InstanceId, "腾出空间后没有领取同一装备实例");
        gear.GetNode<AnimationPlayer>("AnimationPlayer").Seek(0, update: true);
        gear._PhysicsProcess(.4);
        Check(flow.GetModel<PlayerModel>().Inventory.Items.Count == original.Length + 1, "装备领取动画重复发放装备");
        gear._PhysicsProcess(.41);
        Check(owned.Count == 0, "装备领取动画结束后未回池");
        flow.GetModel<PlayerModel>().Inventory.Restore(original);

        SpawnGround(level, hero.GlobalPosition + new Vector2(180, -80), null, item);
        gear = owned.Single();
        typeof(GroundItemNode).GetField("m_Age", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gear, tables.TbDropConfig.GroundLifetime - .01f);
        gear._PhysicsProcess(.02);
        Check(owned.Count == 0, "地面物品没有按秒过期");

        foreach (var pickup in tables.TbPickupConfig.DataList)
            SpawnGround(level, hero.GlobalPosition + new Vector2(110 + (int)pickup.Id * 55, -80), pickup, null);
        SpawnGround(level, hero.GlobalPosition + new Vector2(330, -80), null, item);
        await WaitUntilAsync(() => owned.Count == 4 && owned.All(x => x.IsOnFloor()), "地面物品画面夹具没有全部落地", 5000);
        await CaptureCombatAsync("ground_pickups");
        foreach (var node in owned.ToArray())
            typeof(LevelViewController).GetMethod("OnGroundItemFinished", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(level, [node]);
        GD.Print("SMOKE-UI: ground physics, pickup rise/fade, single transaction, pause, full inventory, expiration and pool reuse passed");
    }

    /// <summary>用关卡正式创建方法生成受控夹具，不增加生产测试 API。</summary>
    /// <param name="level">真实所有者。</param>
    /// <param name="position">世界位置。</param>
    /// <param name="pickup">恢复品或空。</param>
    /// <param name="equipment">装备或空。</param>
    private static void SpawnGround(LevelViewController level, Vector2 position, PickupConfig pickup, EquipmentInstance equipment) =>
        typeof(LevelViewController).GetMethod("ShowGroundItem", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(level, [position, pickup?.Id ?? PickupId.None, equipment]);
}
