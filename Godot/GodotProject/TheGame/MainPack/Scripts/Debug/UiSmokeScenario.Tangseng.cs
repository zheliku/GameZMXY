using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameConfig.Equipment;
using GameLogic;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Drops;
using GameLogic.Equipment;
using GameLogic.Heroes;
using GameLogic.Heroes.Tangseng;
using GameLogic.Level;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.Save.Data;
using GameLogic.UI;
using Godot;
using GodotGameFramework;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 唐僧正式档案的装备、状态和完整流程验收，复用共有窗口及档案检查。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>验证本体方框边界、超界排除及分身独立中心的真实末帧治疗。</summary>
    /// <param name="heroes">本局按席位显示的悟空与唐僧。</param>
    /// <param name="flow">本局权威模型与系统。</param>
    /// <returns>甘露双人治疗验证任务。</returns>
    private async Task CheckCoopHeavenlyDewAsync(AbstractHeroEntity[] heroes, ProcedureOwner flow)
    {
        HeroSystem system = flow.GetSystem<HeroSystem>();
        HeroModel states = flow.GetModel<HeroModel>();
        var tangseng = (TangsengEntity)heroes.Single(hero => hero.HeroId == 2);
        AbstractHeroEntity teammate = heroes.Single(hero => hero.HeroId == 1);
        tangseng.GlobalPosition = new Vector2(400, 350);
        teammate.GlobalPosition = tangseng.GlobalPosition + new Vector2(200, 200);
        system.Damage(teammate.HeroId, 50);
        system.Damage(tangseng.HeroId, 30);
        int teammateBefore = states.GetState(teammate.HeroId).Hp.Value;
        int casterBefore = states.GetState(tangseng.HeroId).Hp.Value;
        int amount = (int)(states.GetState(tangseng.HeroId).MaxMp.Value / 8d + 8);
        tangseng.ApplyHeavenlyDew();
        Check(states.GetState(teammate.HeroId).Hp.Value == teammateBefore + amount &&
            states.GetState(tangseng.HeroId).Hp.Value == casterBefore + amount,
            "甘露本体没有治疗自己与横纵200像素边界的队友");
        await CaptureCombatAsync("coop_d3_heal");
        teammate.GlobalPosition = tangseng.GlobalPosition + new Vector2(201, 0);
        teammateBefore = states.GetState(teammate.HeroId).Hp.Value;
        tangseng.ApplyHeavenlyDew();
        Check(states.GetState(teammate.HeroId).Hp.Value == teammateBefore, "甘露本体治疗了横向超界队友");

        TangsengWaterCloneNode clone = SmokeInspection.ReadField<TangsengWaterCloneNode>(tangseng, "_waterClone");
        Vector2 cloneCenter = new(750, 350);
        tangseng.GlobalPosition = cloneCenter - new Vector2(201, 0);
        teammate.GlobalPosition = cloneCenter + new Vector2(200, 160);
        clone.Activate(cloneCenter, 1);
        teammateBefore = states.GetState(teammate.HeroId).Hp.Value;
        casterBefore = states.GetState(tangseng.HeroId).Hp.Value;
        PackedScene cure = ResourceLoader.Load<PackedScene>("res://TheGame/Entitys/Heroes/Tangseng/tangseng_cure_role_effect.tscn");
        tangseng.BeginHeavenlyDew(cure);
        await WaitUntilAsync(() => !clone.IsActive, "分身甘露没有在原生末帧结束", 5000);
        Check(states.GetState(teammate.HeroId).Hp.Value == teammateBefore + amount &&
            states.GetState(tangseng.HeroId).Hp.Value == casterBefore,
            "分身甘露未以自身200×160范围治疗队友，或治疗了范围外本体");
        system.Damage(teammate.HeroId, 10);
        teammateBefore = states.GetState(teammate.HeroId).Hp.Value;
        teammate.GlobalPosition = cloneCenter + new Vector2(0, 161);
        clone.Activate(cloneCenter, 1);
        tangseng.BeginHeavenlyDew(cure);
        await WaitUntilAsync(() => !clone.IsActive, "第二次分身甘露没有结束", 5000);
        Check(states.GetState(teammate.HeroId).Hp.Value == teammateBefore, "分身甘露治疗了纵向161像素外的队友");
        GD.Print("SMOKE-COOP HEAL PASS: self/team, inclusive 200x200 body, independent 200x160 clone and out-of-range exclusion.");
    }

    /// <summary>
    /// 实际拾取全部唐僧装备，通过背包穿卸并核对持久实例与两层完整动作。
    /// </summary>
    /// <param name="hero">正式选角创建的唐僧。</param>
    /// <param name="flow">本格隔离档案作用域。</param>
    /// <param name="battle">正式战斗窗口。</param>
    /// <param name="level">真实地面物品所有者。</param>
    /// <returns>装备拾取、穿卸和磁盘检查任务。</returns>
    private async Task CheckTangsengEquipmentAsync(TangsengEntity hero, ProcedureOwner flow,
        BattleForm battle, LevelViewController level)
    {
        ProfileModel profile = flow.GetModel<ProfileModel>();
        EquipmentSystem equipment = flow.GetSystem<EquipmentSystem>();
        EquipmentConfig[] definitions = ConfigSystem.Instance.Tables.TbEquipmentConfig.DataList
            .Where(item => item.Owner == EquipmentOwner.Tangseng && item.Slot is EquipmentSlot.Weapon or EquipmentSlot.Armor)
            .ToArray();
        var items = new List<EquipmentData>();
        HashSet<GroundItemNode> ground = SmokeInspection.ReadField<HashSet<GroundItemNode>>(level, "_groundItems");
        foreach (EquipmentConfig definition in definitions)
        {
            EquipmentData item = equipment.Roll(Guid.NewGuid(), definition.Id, () => .37f);
            SpawnGround(level, hero.HurtBox.GetGlobalBounds().GetCenter(), null, item);
            GroundItemNode pickup = ground.Single();
            CheckGroundShape(pickup);
            await WaitUntilAsync(() => profile.GetBag(profile.HeroOf(PlayerSlot.P1)).Any(value => value.InstanceId == item.InstanceId),
                $"{definition.NameCn}未通过真实接触拾取");
            pickup._PhysicsProcess(.81);
            items.Add(item);
        }
        Check(items.Count == 15, "未覆盖七件唐僧防具与八件武器");

        PushInventoryKey(true);
        PushInventoryKey(false);
        await WaitUntilAsync(() => GF.UI.GetAllLoadedUIForms().OfType<InventoryForm>().Any(form => form.Visible),
            "唐僧装备专项没有打开正式背包");
        InventoryForm form = GF.UI.GetAllLoadedUIForms().OfType<InventoryForm>().Single(value => value.Visible);
        CheckAllWearableEquipment(form, hero, flow);
        ClickBagEquipment(form, flow, EquipmentId.NineRingMonkStaff);
        ClickBagEquipment(form, flow, EquipmentId.HeavenlyFiendRobe);
        EquipmentData weapon = profile.GetSlots(profile.HeroOf(PlayerSlot.P1)).Value.Weapon;
        EquipmentData armor = profile.GetSlots(profile.HeroOf(PlayerSlot.P1)).Value.Armor;
        Check(weapon?.EquipmentId == EquipmentId.NineRingMonkStaff && armor?.EquipmentId == EquipmentId.HeavenlyFiendRobe,
            "唐僧没有通过正式菜单穿上九环禅杖与天煞羽袍");
        await WaitFramesAsync(3);
        await CaptureCombatAsync("tangseng_inventory_wearing");
        GF.UI.CloseUIForm(form.SerialId);
        await WaitUntilAsync(() => !GF.Base.IsGamePaused, "关闭唐僧背包没有恢复游戏");

        Check(await flow.GetSystem<SaveSystem>().SaveCheckpointAsync(profile), "唐僧装备检查点未写盘");
        GameData disk = await ReadDiskAsync();
        HeroSaveData savedHero = disk.Heroes.Single();
        Check(savedHero.HeroId == 2 && disk.Heroes[0].HeroId == 2 &&
            savedHero.WeaponInstanceId == weapon.InstanceId && savedHero.ArmorInstanceId == armor.InstanceId &&
            items.All(item => disk.EquipmentItems.Any(saved => saved.InstanceId == item.InstanceId && saved.Attributes == item.Attributes)),
            "唐僧装备身份、随机属性或穿戴引用没有保持到磁盘");
        GD.Print("SMOKE-TANGSENG-EQUIPMENT PASS: 15 real pickups, all GUI equips/unequips, independent body/weapon grids, both-facing native motions and disk instance/attribute/slot persistence.");
    }

    /// <summary>
    /// 复用装备、受击、无双、被动和死亡验收，再经标题与磁盘重建唐僧档案。
    /// </summary>
    /// <param name="hero">首次建档创建的唐僧。</param>
    /// <param name="flow">正式档案作用域。</param>
    /// <param name="battle">正式战斗窗口。</param>
    /// <param name="level">本次关卡。</param>
    /// <returns>实际流程与磁盘重入检查任务。</returns>
    private async Task CheckTangsengLifecycleAsync(TangsengEntity hero, ProcedureOwner flow,
        BattleForm battle, LevelViewController level)
    {
        await CheckTangsengWaterCloneAsync(hero);
        await CheckTangsengEffectsAsync(hero, flow, battle);
        await CheckTangsengEquipmentAsync(hero, flow, battle, level);
        await CheckMusouHurtAsync(hero, flow);
        await CheckHurtProtectionAsync(hero, flow);
        await CheckPassiveUpgradesAsync(battle, flow);
        await CheckHeroDeathReentryAsync();

        ProfileModel profile = flow.GetModel<ProfileModel>();
        EquipmentData weapon = profile.GetSlots(profile.HeroOf(PlayerSlot.P1)).Value.Weapon;
        EquipmentData armor = profile.GetSlots(profile.HeroOf(PlayerSlot.P1)).Value.Armor;
        EquipmentData[] items = profile.GetBag(profile.HeroOf(PlayerSlot.P1)).ToArray();
        int expectedHp = flow.GetModel<HeroModel>().GetState(hero.HeroId).MaxHp.Value;
        BaseButton settingsButton = CurrentBattleForm().GetNode<BaseButton>("MenuPanel/m_SettingsButton");
        settingsButton.EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntilAsync(() => GF.UI.GetAllLoadedUIForms().OfType<SettingsForm>().Any(value => value.Visible),
            "唐僧磁盘重入未打开设置");
        // 窗口OnOpen的可见性先于GGF打开成功回调，先让所属流程完成导航订阅。
        await WaitFramesAsync(2);
        GF.UI.GetAllLoadedUIForms().OfType<SettingsForm>().Single(value => value.Visible)
            .GetNode<BaseButton>("Scroll/Content/Options/Navigation/m_ReturnMap").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntilAsync(() => GF.Procedure.CurrentProcedure is ProcedureMap &&
            GF.UI.GetAllLoadedUIForms().OfType<MapForm>().Any(value => value.Visible), "唐僧设置未返回地图");
        await WaitFramesAsync(2);
        GF.UI.GetAllLoadedUIForms().OfType<MapForm>().Single(value => value.Visible)
            .GetNode<BaseButton>("m_Settings").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntilAsync(() => GF.UI.GetAllLoadedUIForms().OfType<SettingsForm>().Any(value => value.Visible),
            "唐僧地图未打开返回主菜单的设置窗口");
        GF.UI.GetAllLoadedUIForms().OfType<SettingsForm>().Single(value => value.Visible)
            .GetNode<BaseButton>("Scroll/Content/Options/Navigation/m_ReturnTitle").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntilAsync(() => GF.Procedure.CurrentProcedure is ProcedureTitle && flow.GetModel<ProfileModel>() == null,
            "唐僧返回标题未释放旧档案");
        await WaitUntilAsync(() =>
        {
            FrontendSmokeScenario.AdvanceToLevel();
            return GF.Procedure.CurrentProcedure is ProcedureLevel && CurrentBattleForm() != null;
        }, "唐僧已有档案无法经存档列表重读进关", 15000);
        await WaitFramesAsync(6);
        ProfileModel restored = flow.GetModel<ProfileModel>();
        Check(!ReferenceEquals(profile, restored) && restored.HeroOf(PlayerSlot.P1) == 2 && FindHero() is TangsengEntity &&
            restored.GetSlots(restored.HeroOf(PlayerSlot.P1)).Value.Weapon?.InstanceId == weapon.InstanceId && restored.GetSlots(restored.HeroOf(PlayerSlot.P1)).Value.Armor?.InstanceId == armor.InstanceId &&
            items.All(item => restored.GetBag(restored.HeroOf(PlayerSlot.P1)).Any(value => value.InstanceId == item.InstanceId && value.Attributes == item.Attributes)) &&
            flow.GetModel<GameLogic.Skills.SkillModel>().GetPassiveLevels(2).Values.All(value => value == 5) &&
            flow.GetModel<GameLogic.Skills.SkillModel>().GetLearnedSkills(2).Count == 5 &&
            flow.GetModel<HeroModel>().GetState(hero.HeroId).MaxHp.Value == expectedHp,
            "唐僧磁盘重入丢失身份、装备实例、随机属性、槽位或被动效果");
        CheckHeroRevived();
        PushInventoryKey(true);
        PushInventoryKey(false);
        await WaitUntilAsync(() => GF.UI.GetAllLoadedUIForms().OfType<InventoryForm>().Any(value => value.Visible),
            "唐僧磁盘重入无法打开复用背包");
        InventoryForm inventory = GF.UI.GetAllLoadedUIForms().OfType<InventoryForm>().Single(value => value.Visible);
        CheckAllWearableEquipment(inventory, FindHero(), flow);
        GF.UI.CloseUIForm(inventory.SerialId);
        GD.Print("SMOKE-TANGSENG-LIFECYCLE PASS: equipment, 60/120Hz hurt/musou/death, 25 passive purchases, retry/map/title and fresh disk profile with all equipment/passives.");
    }
}
