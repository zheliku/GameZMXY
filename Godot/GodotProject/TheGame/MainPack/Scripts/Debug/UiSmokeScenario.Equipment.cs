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
    /// <param name="context">受测档案。</param>
    /// <param name="hero">受测英雄。</param>
    /// <param name="level">地面物品所有者。</param>
    /// <returns>真实引擎验证。</returns>
    private async Task CheckGroundItemsAsync(GameContext context, HeroEntity hero, LevelModule level)
    {
        var tables = ConfigSystem.Instance.Tables;
        var owned = SmokeInspection.ReadField<HashSet<GroundItemNode>>(level, "m_GroundItems");
        var original = context.Profile.Inventory.Items.ToArray();
        SpawnGround(level, hero.GlobalPosition + new Vector2(180, -80), tables.TbPickupConfig.Get(PickupId.SmallPeach), null);
        var peach = owned.Single();
        await WaitUntilAsync(() => peach.IsOnFloor(), "桃子没有落到实际关卡地形上", 5000);
        float age = SmokeInspection.ReadField<float>(peach, "m_Age");
        GF.Base.PauseGame();
        await WaitFramesAsync(12);
        Check(SmokeInspection.ReadField<float>(peach, "m_Age") == age, "暂停时地面物品仍在过期");
        GF.Base.ResumeGame();
        hero.Vitals.Damage(hero.Vitals.Hp - 1);
        peach.GlobalPosition = hero.HurtBox.GetGlobalBounds().GetCenter();
        peach._PhysicsProcess(1.0 / 60);
        Check(owned.Count == 0 && hero.Vitals.Hp == Math.Min(hero.Vitals.MaxHp, 101), "小桃子接触恢复量或上限错误");
        SpawnGround(level, hero.HurtBox.GetGlobalBounds().GetCenter(), tables.TbPickupConfig.Get(PickupId.ManaBottle), null);
        var bottle = owned.Single();
        Check(ReferenceEquals(peach, bottle), "地面物品没有使用 GGF 节点池复用");
        hero.Vitals.TrySpendMp(hero.Vitals.Mp);
        bottle._PhysicsProcess(1.0 / 60);
        Check(owned.Count == 0 && hero.Vitals.Mp == Math.Min(hero.Vitals.MaxMp, 100), "蓝瓶没有接触回蓝或超出上限");
        var numbers = SmokeInspection.ReadField<HashSet<RecoveryPopNode>>(level, "m_RecoveryPops");
        Check(numbers.Count == 2, "恢复品未各显示一次恢复数字");
        foreach (var number in numbers)
        {
            var digit = number.GetChild<Sprite2D>(0);
            Check(digit.Visible && digit.Texture.ResourcePath.Contains("recovery_number_") &&
                number.GetChild<Sprite2D>(1).Position.X - digit.Position.X == 20,
                "恢复数字没有使用恢复专用位图或官方间距");
        }
        var manaNumber = numbers.Single(x => x.GetChild<Sprite2D>(0).Texture.ResourcePath.EndsWith("mana.png"));
        var healthNumber = numbers.Single(x => x.GetChild<Sprite2D>(0).Texture.ResourcePath.EndsWith("health.png"));
        // 仅把同时出现的截图夹具分开；生产规则仍从各自领取点向上移动。
        foreach (var (number, offset) in new[] { (healthNumber, -35f), (manaNumber, 35f) })
        {
            Vector2 start = SmokeInspection.ReadField<Vector2>(number, "m_Start") + new Vector2(offset, 0);
            typeof(GameLogic.UI.RecoveryPopNode).GetField("m_Start", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(number, start);
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
        foreach (var number in numbers.ToArray()) number._Process(1);
        Check(numbers.Count == 0, "恢复数字没有按一秒生命周期回池");

        var item = context.Equipment.Roll(Guid.NewGuid(), EquipmentId.CommonPineStaff, () => .75f);
        while (context.Profile.Inventory.Items.Count < context.Profile.Inventory.Capacity)
            context.Profile.Inventory.TryAdd(context.Equipment.Roll(Guid.NewGuid(), EquipmentId.RoughCassock, () => 0));
        SpawnGround(level, hero.HurtBox.GetGlobalBounds().GetCenter(), null, item);
        var gear = owned.Single();
        gear._PhysicsProcess(1.0 / 60);
        Check(owned.Count == 1 && !context.Profile.Inventory.Items.Any(x => x.InstanceId == item.InstanceId), "满包时装备消失或超出容量");
        context.Profile.Inventory.Restore(original);
        gear._PhysicsProcess(1.0 / 60);
        Check(owned.Count == 0 && context.Profile.Inventory.Items.Last().InstanceId == item.InstanceId, "腾出空间后没有领取同一装备实例");
        context.Profile.Inventory.Restore(original);

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
            typeof(LevelModule).GetMethod("OnGroundItemFinished", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(level, [node]);
        GD.Print("SMOKE-UI: ground physics, health/mana clamp, pause, full inventory, expiration and pool reuse passed");
    }

    /// <summary>用关卡正式创建方法生成受控夹具，不增加生产测试 API。</summary>
    /// <param name="level">真实所有者。</param>
    /// <param name="position">世界位置。</param>
    /// <param name="pickup">恢复品或空。</param>
    /// <param name="equipment">装备或空。</param>
    private static void SpawnGround(LevelModule level, Vector2 position, PickupConfig pickup, EquipmentInstance equipment) =>
        typeof(LevelModule).GetMethod("ShowGroundItem", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(level, [position, pickup, equipment]);
}
