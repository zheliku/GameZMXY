using GameLogic.Battle;
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
        var original = flow.GetModel<ProfileModel>().EquipmentDatas.ToArray();
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
        Check(GroundOverlaps(peach, otherBody) &&
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
        Check(GroundOverlaps(peach, foreignHero) &&
            !SmokeInspection.ReadField<bool>(peach, "m_Collected"), "其他会话英雄领取了本关物品");
        foreignHero.QueueFree();
        float age = SmokeInspection.ReadField<float>(peach, "m_Age");
        GF.Base.PauseGame();
        await WaitFramesAsync(12);
        Check(SmokeInspection.ReadField<float>(peach, "m_Age") == age, "暂停时地面物品仍在过期");
        GF.Fsm.GetSystem<HeroSystem>().Damage(GF.Fsm.GetModel<HeroModel>().Hp.Value - 1);
        peach.GlobalPosition = hero.HurtBox.GetGlobalBounds().GetCenter();
        await WaitFramesAsync(6);
        Check(GF.Fsm.GetModel<HeroModel>().Hp.Value == 1 && !SmokeInspection.ReadField<bool>(peach, "m_Collected"),
            "暂停时接触仍发放收益");
        GF.Base.ResumeGame();
        await WaitUntilAsync(() => SmokeInspection.ReadField<bool>(peach, "m_Collected"), "真实拾取区域没有识别接触英雄", 3000);
        Check(owned.Count == 1 && GF.Fsm.GetModel<HeroModel>().Hp.Value == Math.Min(GF.Fsm.GetModel<HeroModel>().MaxHp.Value, 101), "小桃子未即时入账或未保留领取动画");
        var icon = peach.GetNode<Sprite2D>("Icon");
        var pickupPlayer = peach.GetNode<AnimationPlayer>("AnimationPlayer");
        Check(pickupPlayer.IsPlaying() && !peach.HasNode("Shadow"),
            "领取后未开始上移淡出或仍显示地面阴影");
        GF.Base.PauseGame();
        pickupPlayer.Seek(0, update: true);
        peach._PhysicsProcess(.2);
        Check(Math.Abs(icon.Position.Y + 43.75f) < .02f && Math.Abs(icon.Modulate.A - .5625f) < .001f,
            "领取动画没有按原版 0.8 秒、上移 100 像素的二次缓出推进");
        Vector2 pausedIconPosition = icon.Position;
        float pausedIconAlpha = icon.Modulate.A;
        await WaitFramesAsync(12);
        Check(icon.Position == pausedIconPosition && icon.Modulate.A == pausedIconAlpha,
            "暂停时领取动画继续推进");
        peach._PhysicsProcess(.2);
        await CaptureCombatAsync("pickup_rising");
        Check(GF.Fsm.GetModel<HeroModel>().Hp.Value == Math.Min(GF.Fsm.GetModel<HeroModel>().MaxHp.Value, 101), "领取动画重复发放恢复收益");
        peach._PhysicsProcess(.39);
        Check(owned.Contains(peach), "领取动画未满 0.8 秒就提前回池");
        peach._PhysicsProcess(.02);
        Check(owned.Count == 0, "领取动画结束后未回池");
        GF.Base.ResumeGame();
        SpawnGround(level, hero.GlobalPosition + new Vector2(180, 0), tables.TbPickupConfig.Get(PickupId.ManaBottle), null);
        var bottle = owned.Single();
        Check(ReferenceEquals(peach, bottle), "地面物品没有使用 GGF 节点池复用");
        CheckGroundShape(bottle);
        Check(icon.Position == Vector2.Zero && icon.Modulate.A == 1 &&
            !bottle.HasNode("Shadow") && !pickupPlayer.IsPlaying(),
            "地面物品复用后未恢复原点、透明度、无阴影或动画状态");
        GF.Fsm.GetSystem<HeroSystem>().TrySpendMp(GF.Fsm.GetModel<HeroModel>().Mp.Value);
        bottle._PhysicsProcess(1.0 / 60);
        await WaitFramesAsync(4);
        Check(GF.Fsm.GetModel<HeroModel>().Mp.Value == 0 && !SmokeInspection.ReadField<bool>(bottle, "m_Collected"),
            "池复用继承了旧的英雄接触");
        bottle.GlobalPosition = hero.HurtBox.GetGlobalBounds().GetCenter();
        await WaitUntilAsync(() => SmokeInspection.ReadField<bool>(bottle, "m_Collected"), "蓝瓶的真实接触未触发领取", 3000);
        Check(owned.Count == 1 && GF.Fsm.GetModel<HeroModel>().Mp.Value == Math.Min(GF.Fsm.GetModel<HeroModel>().MaxMp.Value, 100), "蓝瓶没有接触回蓝或超出上限");
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
        while (flow.GetModel<ProfileModel>().EquipmentDatas.Count < flow.GetModel<ProfileModel>().Capacity)
            flow.GetSystem<ProfileSystem>().TryCollectEquipment(flow.GetSystem<EquipmentSystem>().Roll(Guid.NewGuid(), EquipmentId.RoughCassock, () => 0));
        SpawnGround(level, hero.HurtBox.GetGlobalBounds().GetCenter(), null, item);
        var gear = owned.Single();
        await WaitFramesAsync(6);
        Check(GroundOverlaps(gear, hero) && owned.Count == 1 &&
            !flow.GetModel<ProfileModel>().EquipmentDatas.Any(x => x.InstanceId == item.InstanceId), "满包时装备消失或超出容量");
        Check(!gear.GetNode<AnimationPlayer>("AnimationPlayer").IsPlaying(), "满包拒绝领取却播放了领取动画");
        flow.GetModel<ProfileModel>().RestoreItems(original);
        await WaitUntilAsync(() => SmokeInspection.ReadField<bool>(gear, "m_Collected"), "满包腾出空间后未重试实际接触", 3000);
        Check(owned.Count == 1 && flow.GetModel<ProfileModel>().EquipmentDatas.Last().InstanceId == item.InstanceId, "腾出空间后没有领取同一装备实例");
        gear.GetNode<AnimationPlayer>("AnimationPlayer").Seek(0, update: true);
        gear._PhysicsProcess(.4);
        Check(flow.GetModel<ProfileModel>().EquipmentDatas.Count == original.Length + 1, "装备领取动画重复发放装备");
        gear._PhysicsProcess(.41);
        Check(owned.Count == 0, "装备领取动画结束后未回池");
        flow.GetModel<ProfileModel>().RestoreItems(original);

        SpawnGround(level, hero.GlobalPosition + new Vector2(180, -80), null, item);
        gear = owned.Single();
        typeof(GroundItemNode).GetField("m_Age", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gear, tables.TbBattleConfig.GroundLifetime - .01f);
        gear._PhysicsProcess(.02);
        Check(owned.Count == 0, "地面物品没有按秒过期");

        int groundIndex = 0;
        foreach (var pickup in tables.TbPickupConfig.DataList)
            SpawnGround(level, hero.GlobalPosition + new Vector2(110 + groundIndex++ * 45, -80), pickup, null);
        // 覆盖所有装备，尤其是画布下方有透明留白的唐僧武器与防具。
        foreach (var equipment in tables.TbEquipmentConfig.DataList)
            SpawnGround(level, hero.GlobalPosition + new Vector2(110 + groundIndex++ * 45, -80), null,
                flow.GetSystem<EquipmentSystem>().Roll(Guid.NewGuid(), equipment.Id, () => 0));
        await WaitUntilAsync(() => owned.Count == tables.TbPickupConfig.DataList.Count + tables.TbEquipmentConfig.DataList.Count &&
            owned.All(x => x.IsOnFloor()), "地面物品画面夹具没有全部落地", 5000);
        foreach (var node in owned)
            CheckGroundShape(node);
        await CaptureCombatAsync("ground_pickups");
        foreach (var node in owned.ToArray())
            typeof(LevelViewController).GetMethod("OnGroundItemFinished", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(level, [node]);
        GD.Print("SMOKE-UI: all pickup/equipment opaque bottoms aligned to ground shape, no shadow, pickup rise/fade, single transaction, pause, full inventory, expiration and pool reuse passed");
    }

    /// <summary>用关卡正式创建方法生成受控夹具，不增加生产测试 API。</summary>
    /// <param name="level">真实所有者。</param>
    /// <param name="position">世界位置。</param>
    /// <param name="pickup">恢复品或空。</param>
    /// <param name="data">装备或空。</param>
    private static void SpawnGround(LevelViewController level, Vector2 position, PickupConfig pickup, EquipmentData data) =>
        typeof(LevelViewController).GetMethod("ShowGroundItem", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(level, [position, pickup?.Id ?? PickupId.None, data]);

    /// <summary>测试侧用实际唯一形状查询接触，不为生产代码增加观测接口。</summary>
    /// <param name="node">地面物品。</param>
    /// <param name="body">接触的身体。</param>
    /// <returns>物品形状是否与身体重叠。</returns>
    private static bool GroundOverlaps(GroundItemNode node, Node2D body)
    {
        var shape = node.GetNode<CollisionShape2D>("Shape");
        using var query = new PhysicsShapeQueryParameters2D
        {
            Shape = shape.Shape,
            Transform = shape.GlobalTransform,
            CollisionMask = LayerMask.LayerToMask2D("PlayerBody")
        };
        return node.GetWorld2D().DirectSpaceState.IntersectShape(query)
            .Any(hit => ReferenceEquals(hit["collider"].AsGodotObject(), body));
    }

    /// <summary>逐像素独立检查可见底边与碰撞底边对齐，碰撞尺寸保持统一且没有第二范围或阴影。</summary>
    /// <param name="node">已经显示的物品，尚未播放领取动画。</param>
    private static void CheckGroundShape(GroundItemNode node)
    {
        Check(node.GetChildren().OfType<CollisionShape2D>().Count() == 1 &&
            !node.GetChildren().OfType<Area2D>().Any() && !node.HasNode("Shadow"), "物品保留了两个碰撞范围或阴影");
        var shapeNode = node.GetNode<CollisionShape2D>("Shape");
        var shape = (RectangleShape2D)shapeNode.Shape;
        var icon = node.GetNode<Sprite2D>("Icon");
        Check(shape.Size.IsEqualApprox(new Vector2(32, 40)) && shapeNode.Position.IsEqualApprox(new Vector2(0, -20)),
            "地面物品没有使用统一预设碰撞盒");
        using var image = icon.Texture.GetImage();
        Check(image != null && (!image.IsCompressed() || image.Decompress() == Error.Ok), "掉落物图标不可读取");
        int opaqueBottom = 0;
        for (int y = image.GetHeight() - 1; y >= 0 && opaqueBottom == 0; y--)
            for (int x = 0; x < image.GetWidth(); x++)
                if (image.GetPixel(x, y).A > 0)
                {
                    opaqueBottom = y + 1;
                    break;
                }
        float visibleBottom = icon.ToGlobal(icon.Offset + new Vector2(0,
            opaqueBottom - icon.Texture.GetHeight() * .5f)).Y;
        float collisionBottom = (shapeNode.GlobalTransform * shape.GetRect()).End.Y;
        Check(icon.Centered && opaqueBottom > 0 && Math.Abs(visibleBottom - collisionBottom) < .01f,
            $"图标非透明底边没有对齐碰撞底边：{icon.Texture.ResourcePath}，可见底边 {visibleBottom}，碰撞底边 {collisionBottom}");
    }
}
