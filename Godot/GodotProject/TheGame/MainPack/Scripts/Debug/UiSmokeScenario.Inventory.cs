using System;
using System.Linq;
using System.Threading.Tasks;
using GameConfig.Equipment;
using GameLogic;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Equipment;
using GameLogic.Heroes;
using GameLogic.Heroes.Body;
using GameLogic.Level;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.UI;
using Godot;
using Godot.Collections;
using GodotGameFramework;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 背包与失败结算的真实窗口回归，直接检查节点和既有状态源。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 从真实双人背包格和赠送按钮移交装备，核对双方归属与过期选择。
    /// </summary>
    /// <param name="form">P1打开的背包。</param>
    /// <param name="heroes">按席位排列的双方。</param>
    /// <param name="flow">本槽档案。</param>
    /// <returns>赠送与截图完成的任务。</returns>
    private async Task CheckCoopGiftAsync(InventoryForm form, AbstractHeroEntity[] heroes, ProcedureOwner flow)
    {
        ProfileModel profile = flow.GetModel<ProfileModel>();
        EquipmentData item = flow.GetSystem<EquipmentSystem>().Roll(Guid.NewGuid(), EquipmentId.RoughPineStaff, () => .37f);
        Check(flow.GetSystem<ProfileSystem>().TryCollectEquipment(heroes[0].HeroId, item), "双人赠送夹具入包失败");
        int sourceCount = profile.GetBag(heroes[0].HeroId).Count;
        int targetCount = profile.GetBag(heroes[1].HeroId).Count;
        ClickBagEquipment(form, flow, item.EquipmentId);
        EquipmentActionsMenu menu = form.GetNode<EquipmentActionsMenu>("m_Actions");
        TextureButton gift = menu.GetNode<TextureButton>("Panel/Gift");
        Check(!gift.Disabled && menu.GetNode<TextureButton>("Panel/Equip").Disabled,
            "双人赠送不可用或异职业装备错误可穿戴");
        menu.SetProcess(false);
        // 先离开格子消除原生悬停提示，再采样菜单及提交按钮点击。
        Vector2 giftCenter = gift.GetGlobalTransformWithCanvas() * (gift.Size / 2);
        form.GetViewport().PushInput(new InputEventMouseMotion { Position = giftCenter, GlobalPosition = giftCenter }, true);
        await WaitFramesAsync(2);
        await CaptureCombatAsync("coop_e1_gift_enabled");
        PushMouseClick(gift);
        Check(!menu.Visible && profile.GetBag(heroes[0].HeroId).Count == sourceCount - 1 &&
            profile.GetBag(heroes[1].HeroId).Count == targetCount + 1 &&
            !profile.GetBag(heroes[0].HeroId).Any(value => value.InstanceId == item.InstanceId) &&
            profile.GetBag(heroes[1].HeroId).Single(value => value.InstanceId == item.InstanceId) == item,
            $"赠送按钮没有原样移交实例或双方数量错误：visible={menu.Visible}, source={profile.GetBag(heroes[0].HeroId).Count}/{sourceCount}, target={profile.GetBag(heroes[1].HeroId).Count}/{targetCount}");
        gift.EmitSignal(BaseButton.SignalName.Pressed);
        Check(profile.GetBag(heroes[1].HeroId).Count == targetCount + 1, "旧赠送选择被重复使用");
        await WaitFramesAsync(2);
        await CaptureCombatAsync("coop_e1_gift_source");

        // 同一窗口改绑接收者，确认真实格子读取接收者的背包。
        form.OnRefocus(new InventoryFormData(heroes[1]));
        Check(form.GetNode<GridContainer>("Window/BagPanel/m_BagGrid").GetChildren().OfType<EquipmentCell>()
            .Any(cell => SmokeInspection.ReadField<EquipmentData>(cell, "_equipmentData")?.InstanceId == item.InstanceId),
            "赠送后接收者背包格没有显示实例");
        await WaitFramesAsync(2);
        await CaptureCombatAsync("coop_e1_gift_recipient");
        form.OnRefocus(new InventoryFormData(heroes[0]));

        // 填满接收者，按钮提交后必须保留发送者物品并显示原提示组件。
        EquipmentData[] recipientItems = profile.GetBag(heroes[1].HeroId).ToArray();
        EquipmentSystem equipment = flow.GetSystem<EquipmentSystem>();
        ProfileSystem collection = flow.GetSystem<ProfileSystem>();
        while (profile.GetBag(heroes[1].HeroId).Count < profile.Capacity)
        {
            Check(collection.TryCollectEquipment(heroes[1].HeroId,
                equipment.Roll(Guid.NewGuid(), EquipmentId.EarthFiendRing, () => 0)), "满包夹具入包失败");
        }
        EquipmentData retained = equipment.Roll(Guid.NewGuid(), EquipmentId.CommonPineStaff, () => 0);
        Check(collection.TryCollectEquipment(heroes[0].HeroId, retained), "满包赠送夹具入包失败");
        ClickBagEquipment(form, flow, retained.EquipmentId);
        PushMouseClick(gift);
        SaveNoticeForm notice = GF.UI.GetAllLoadedUIForms().OfType<SaveNoticeForm>().Single();
        Check(profile.GetBag(heroes[0].HeroId).Contains(retained) &&
            profile.GetBag(heroes[1].HeroId).Count == profile.Capacity &&
            notice.GetNode<Label>("SaveFeedback/m_SaveFeedback").Text == "对方背包已满！",
            "接收者满包未拒绝赠送或缺少正确提示");
        await WaitFramesAsync(2);
        await CaptureCombatAsync("coop_e1_gift_full");
        profile.GetBag(heroes[1].HeroId).ReplaceAll(recipientItems);
        await WaitUntilAsync(() => !notice.GetNode<Label>("SaveFeedback/m_SaveFeedback").Visible,
            "满包赠送提示没有自动消失");
        GD.Print("SMOKE-COOP GIFT PASS: enabled gift, original instance, recipient cells, stale selection and full-bag notice.");
    }

    /// <summary>
    /// 验证 HUD 点击、暂停、属性/钱包刷新、退订及池复用。
    /// </summary>
    /// <param name="battleForm">正式战斗窗口。</param>
    /// <param name="hero">本关英雄。</param>
    /// <param name="flow">本槽档案。</param>
    /// <param name="level">本关运行。</param>
    /// <returns>全部背包检查完成的任务。</returns>
    private async Task CheckInventoryAsync(BattleForm battleForm, AbstractHeroEntity hero, ProcedureOwner flow, LevelViewController level)
    {
        EquipmentData[] originalEquipment = flow.GetModel<ProfileModel>().GetBag(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).ToArray();
        EquippedItems originalSlots = flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value;
        foreach (EquipmentConfig definition in ConfigSystem.Instance.Tables.TbEquipmentConfig.DataList)
        {
            flow.GetSystem<ProfileSystem>().TryCollectEquipment(GF.Fsm.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1), flow.GetSystem<EquipmentSystem>().Roll(Guid.NewGuid(), definition.Id, () => .37f));
        }

        PushInventoryKey(true);
        PushInventoryKey(false);
        await WaitUntilAsync(() => GF.UI.GetAllLoadedUIForms().OfType<InventoryForm>().Any(x => x.Visible), "HUD 没有打开背包");
        InventoryForm form = GF.UI.GetAllLoadedUIForms().OfType<InventoryForm>().Single(x => x.Visible);
        SmokeInspection.CheckTooltips(form);
        Check(GF.Base.IsGamePaused, "背包打开后战斗未暂停");
        PushInventoryKey(true, echo: true);
        await WaitFramesAsync(2);
        Check(form.Visible, "长按背包键的重复事件关闭了窗口");
        CheckHeroAnimationLayers(hero);
        Check(form.GetNode<GridContainer>("Window/BagPanel/m_BagGrid").GetChildCount() == 25, "背包不是 5×5 格");
        Check(form.GetNode<Label>("Window/CharacterPanel/Attributes/Left/Hp/m_Hp").Text == $"{GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Hp.Value} / {GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).MaxHp.Value}", "背包生命不是实际值");
        Sprite2D body = form.GetNode<Sprite2D>("Window/CharacterPanel/m_Preview/Body");
        int initialFrame = body.Frame;
        double elapsed = SmokeInspection.Model(level).ElapsedSeconds;
        await WaitUntilAsync(() => body.Frame != initialFrame, "暂停时背包角色预览停止播放");
        var weaponBackground = (ShaderMaterial)form.GetNode<EquipmentCell>("Window/CharacterPanel/EquipmentSlots/m_WeaponSlot").Material;
        var armorBackground = (ShaderMaterial)form.GetNode<EquipmentCell>("Window/CharacterPanel/EquipmentSlots/m_ArmorSlot").Material;
        Check(weaponBackground != armorBackground && !weaponBackground.GetShaderParameter("occupied").AsBool() &&
            !armorBackground.GetShaderParameter("occupied").AsBool(), "空穿戴槽题签未恢复，或两个槽共用可变材质");
        Check(Math.Abs(SmokeInspection.Model(level).ElapsedSeconds - elapsed) < .001, "背包暂停计入关卡时间");
        int originalSoul = flow.GetModel<ProfileModel>().GetSoul(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value;
        flow.GetSystem<ProfileSystem>().AddSoul(GF.Fsm.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1), 17);
        Check(form.GetNode<Label>("Window/BagPanel/Footer/Soul/m_Soul").Text == (originalSoul + 17).ToString(), "背包未响应钱包变化");
        await WaitUntilAsync(() => !GF.UI.GetAllLoadedUIForms().OfType<SaveNoticeForm>().Single().GetNode<Label>("SaveFeedback/m_SaveFeedback").Visible,
            "保存成功提示在背包暂停时没有结束", 5000);
        int tooltipIndex = flow.GetModel<ProfileModel>().GetBag(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).ToList().FindIndex(data => data.EquipmentId == EquipmentId.CommonPineStaff);
        EquipmentCell cell = form.GetNode<GridContainer>("Window/BagPanel/m_BagGrid").GetChild<EquipmentCell>(tooltipIndex);
        using (Control tooltip = cell._MakeCustomTooltip(cell.TooltipText))
        {
            Check(cell.GetNode<TextureRect>("Icon").Texture != null &&
                tooltip.GetNode<Label>("Content/Header/Type/Value").Text == "武器·唐僧" &&
                tooltip.GetNode<VBoxContainer>("Content/Attributes").GetChildren().OfType<Label>().Any(x => x.Text == "攻击：11") &&
                tooltip.GetNode<Label>("Content/Footer/Price").Text == "价值：20 灵魂" && tooltip.GetCombinedMinimumSize().Y > 100,
                "背包提示类型、实际属性、售价或首次布局错误");
            tooltip.Free();
        }
        Check(form.GetNode<Label>("Window/CharacterPanel/Attributes/Right/MagicResistance/m_MagicResistance").Text == "0%", "无装备基础魔抗不是0%");
        _driver.GetViewport().PushInput(new InputEventMouseMotion { Position = cell.GetGlobalRect().GetCenter(), GlobalPosition = cell.GetGlobalRect().GetCenter() }, true);
        await WaitFramesAsync(50);
        await CaptureCombatAsync("inventory_equipment");
        await CaptureCombatAsync("inventory_form");
        await CheckTooltipLayoutAsync(form, flow);
        await CheckBagNavigationAsync(form, flow);
        foreach (EquipmentId id in new[] { EquipmentId.DiamondSnare, EquipmentId.EastSeaBanner })
        {
            EquipmentData previousTreasure = flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Treasure;
            EquipmentData newTreasure = flow.GetModel<ProfileModel>().GetBag(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Single(value => value.EquipmentId == id);
            int previousPower = flow.GetModel<HeroModel>().GetState(hero.HeroId).Power.Value;
            ClickBagEquipment(form, flow, id);
            Check(flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Treasure?.EquipmentId == id &&
                flow.GetModel<HeroModel>().GetState(hero.HeroId).Power.Value == previousPower - (previousTreasure?.Attributes.Power ?? 0) + newTreasure.Attributes.Power,
                "法宝没有进入独立槽位或属性未生效");
            EquipmentCell treasureCell = form.GetNode<EquipmentCell>("Window/CharacterPanel/EquipmentSlots/m_TreasureSlot");
            using Control tooltip = treasureCell._MakeCustomTooltip(treasureCell.TooltipText);
            Check(tooltip.GetNode<Label>("Content/Header/Type/Value").Text == "法宝·通用" &&
                tooltip.GetNode<Label>("Content/Footer/Description").Text != "功能待开放" &&
                treasureCell.GetNode<TextureRect>("Icon").Texture != null, "法宝槽、类型、图标或说明错误");
        }
        GD.Print("SMOKE-TREASURE GUI PASS: native second-page selection, visible icons, tooltip and equip");
        // 通用饰品同样走真实格子、菜单和命令，概率文字不使用属性点曲线。
        ClickBagEquipment(form, flow, EquipmentId.EarthFiendRing);
        EquipmentCell accessory = form.GetNode<EquipmentCell>("Window/CharacterPanel/EquipmentSlots/m_AccessorySlot");
        var accessoryBackground = (ShaderMaterial)accessory.Material;
        Check(flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Accessory?.EquipmentId == EquipmentId.EarthFiendRing &&
            accessoryBackground.GetShaderParameter("occupied").AsBool() &&
            accessory.GetNode<TextureRect>("Icon").Texture != null &&
            form.GetNode<Label>("Window/CharacterPanel/Attributes/Left/Crit/m_Crit").Text == "3%" &&
            form.GetNode<Label>("Window/CharacterPanel/Attributes/Right/Dodge/m_Dodge").Text == "3%",
            "通用饰品无法穿戴、图标缺失或 3% 概率显示错误");
        using (Control tooltip = accessory._MakeCustomTooltip(accessory.TooltipText))
        {
            string[] lines = tooltip.GetNode<VBoxContainer>("Content/Attributes").GetChildren().OfType<Label>().Select(x => x.Text).ToArray();
            Check(tooltip.GetNode<Label>("Content/Header/Type/Value").Text == "饰品·通用" &&
                lines.Contains("暴击：3%") && lines.Contains("闪避：3%"), "饰品 tooltip 概率单位或通用归属错误");
            tooltip.Free();
        }
        await CaptureCombatAsync("inventory_earth_fiend_ring");
        accessory._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        Check(flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Accessory == null &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Crit.Value == 0f && GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Miss.Value == 0f &&
            !accessoryBackground.GetShaderParameter("occupied").AsBool(), "饰品卸下后仍保留图标背景或概率加成");
        GD.Print("SMOKE-ACCESSORY PASS: shared ring slot, GUI commands, direct 3% labels and tooltip, unequip cleanup.");
        // 从真实装备格的 GUI 输入走窗口请求、关卡协调、档案事务、装配刷新整条链路。
        int basePower = GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Power.Value;
        int baseDefense = GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Def.Value;
        int baseMaxHp = GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).MaxHp.Value;
        int currentHp = GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Hp.Value;
        ClickBagEquipment(form, flow, EquipmentId.RoughPilgrimStaff);
        Check(flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Weapon?.EquipmentId == EquipmentId.RoughPilgrimStaff &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Power.Value == basePower + 3, "背包点击未穿戴武器或攻击未刷新");
        Check(weaponBackground.GetShaderParameter("occupied").AsBool() && !armorBackground.GetShaderParameter("occupied").AsBool(),
            "穿戴武器没有隐藏对应题签，或影响了空防具槽");
        CheckHeroAnimationLayers(hero);
        ClickBagEquipment(form, flow, EquipmentId.CommonPilgrimClothes);
        Check(flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Armor?.EquipmentId == EquipmentId.CommonPilgrimClothes &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Def.Value == baseDefense + 3 && GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).MaxHp.Value == baseMaxHp + 27 && GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Hp.Value == currentHp,
            "防具未刷新具名属性，或换装意外补血");
        CheckHeroAnimationLayers(hero);
        // 地煞猿甲必须按具体装备换整套动作纹理，战斗与预览各读对应官方资源。
        ClickBagEquipment(form, flow, EquipmentId.EarthFiendApeArmor);
        Texture2D earthFiendTexture = GD.Load<Texture2D>("res://TheGame/Sprites/Characters/Heroes/wukong/wukong_body_earth_fiend.png");
        Check(flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Armor?.EquipmentId == EquipmentId.EarthFiendApeArmor &&
            hero.GetNode<Sprite2D>("m_Body").Texture == earthFiendTexture && body.Texture == GD.Load<Texture2D>("res://TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_body_earth_fiend.png"),
            "地煞猿甲未同步替换战斗和预览的完整动作图集");
        CheckHeroAnimationLayers(hero);
        await CaptureCombatAsync("inventory_earth_fiend_armor");
        ClickBagEquipment(form, flow, EquipmentId.CommonPilgrimClothes);
        Check(weaponBackground.GetShaderParameter("occupied").AsBool() && armorBackground.GetShaderParameter("occupied").AsBool(),
            "两件穿戴后仍显示空槽题签");
        Check(form.GetNode<Label>("Window/CharacterPanel/Attributes/Left/Attack/m_Attack").Text == GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Power.Value.ToString(),
            "穿戴后界面未响应最终攻击变化");
        ClickBagEquipment(form, flow, EquipmentId.RoughPineStaff);
        Check(flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Weapon.EquipmentId == EquipmentId.RoughPilgrimStaff, "悟空穿上了唐僧武器");
        EquipmentActionsMenu actions = form.GetNode<EquipmentActionsMenu>("m_Actions");
        Check(actions.Visible && actions.GetNode<TextureButton>("Panel/Equip").Disabled &&
            actions.GetNode<TextureButton>("Panel/Gift").Disabled, "单人赠送或异角色装备没有禁用");
        // Freeze only the visual fixture while capturing; the desktop pointer
        // can move independently of the injected viewport mouse event.
        actions.SetProcess(false);
        Guid saleId = flow.GetModel<ProfileModel>().GetBag(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).First(x => x.EquipmentId == EquipmentId.RoughPineStaff).InstanceId;
        await CaptureCombatAsync("inventory_actions");
        int saleSoul = flow.GetModel<ProfileModel>().GetSoul(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value;
        actions.GetNode<TextureButton>("Panel/Sell").EmitSignal(BaseButton.SignalName.Pressed);
        Check(flow.GetModel<ProfileModel>().GetSoul(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value == saleSoul + 10 &&
            flow.GetModel<ProfileModel>().GetBag(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).All(x => x.InstanceId != saleId), "菜单出售没有按配置入账并移除装备");
        await CaptureCombatAsync("inventory_wearing");
        foreach (string path in new[] { "m_WeaponSlot", "m_ArmorSlot" })
        {
            form.GetNode<EquipmentCell>("Window/CharacterPanel/EquipmentSlots/" + path)
                ._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            CheckHeroAnimationLayers(hero);
            Check(!weaponBackground.GetShaderParameter("occupied").AsBool() &&
                armorBackground.GetShaderParameter("occupied").AsBool() == (path == "m_WeaponSlot"),
                "卸装没有独立恢复对应部位题签");
        }
        Check(flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Weapon == null && flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Armor == null &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Power.Value == basePower && GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Def.Value == baseDefense && GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).MaxHp.Value == baseMaxHp,
            "卸装后装备栏或属性来源残留");
        ClickBagEquipment(form, flow, EquipmentId.HeirloomSword);
        Texture2D heirloomTexture = GD.Load<Texture2D>("res://TheGame/Sprites/Characters/Heroes/wukong/wukong_weapon_heirloom.png");
        Check(hero.GetNode<Sprite2D>("m_Weapon").Texture == heirloomTexture &&
            form.GetNode<Sprite2D>("Window/CharacterPanel/m_Preview/Weapon").Texture == GD.Load<Texture2D>("res://TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_weapon_heirloom.png") &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Power.Value == basePower + 15 &&
            Mathf.IsEqualApprox(GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Crit.Value, .03f),
            "家传宝剑未同步替换战斗/背包动作图集或属性错误");
        CheckHeroAnimationLayers(hero);
        ClickBagEquipment(form, flow, EquipmentId.HeirloomBracelet);
        Check(GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Power.Value == basePower + 30 &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Def.Value == baseDefense + 4, "家传手镯属性未加入饰品槽");
        await CaptureCombatAsync("inventory_heirloom_sword");
        ClickBagEquipment(form, flow, EquipmentId.HeirloomClothes);
        Check(actions.GetNode<TextureButton>("Panel/Equip").Disabled && flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Armor == null,
            "悟空错误穿上唐僧家传衣裳");
        actions.Hide();
        foreach (string path in new[] { "m_WeaponSlot", "m_AccessorySlot" })
        {
            form.GetNode<EquipmentCell>("Window/CharacterPanel/EquipmentSlots/" + path)
                        ._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        }

        Check(GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Power.Value == basePower &&
                    GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Crit.Value == 0, "家传装备卸下仍残留属性");
        GD.Print("SMOKE-HEIRLOOM PASS: sword GUI equip, battle/preview texture, both-facing full actions, bracelet stats and Tang ownership.");
        ClickBagEquipment(form, flow, EquipmentId.HeavenlyFiendMoonHalberd);
        Texture2D halberd = GD.Load<Texture2D>("res://TheGame/Sprites/Characters/Heroes/wukong/wukong_weapon_heavenly_fiend.png");
        Check(hero.GetNode<Sprite2D>("m_Weapon").Texture == halberd &&
            form.GetNode<Sprite2D>("Window/CharacterPanel/m_Preview/Weapon").Texture == GD.Load<Texture2D>("res://TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_weapon_heavenly_fiend.png") &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Power.Value == basePower + 50 &&
            Mathf.IsEqualApprox(GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Crit.Value, .03f),
            "天煞月戟未同步战斗/预览动作图集或50攻击、3%暴击");
        CheckHeroAnimationLayers(hero);
        await CaptureCombatAsync("inventory_heavenly_fiend_moon_halberd");
        ClickBagEquipment(form, flow, EquipmentId.HeavenlyFiendRobe);
        Check(actions.GetNode<TextureButton>("Panel/Equip").Disabled && flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Armor == null,
            "悟空错误穿上唐僧天煞羽袍");
        actions.Hide();
        form.GetNode<EquipmentCell>("Window/CharacterPanel/EquipmentSlots/m_WeaponSlot")
            ._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        Check(GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Power.Value == basePower && GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Crit.Value == 0,
            "天煞月戟卸下后仍残留属性");
        GD.Print("SMOKE-GARUDA EQUIPMENT 2-3 PASS: GUI commands, battle/preview animation, official attributes and Tang ownership");
        ClickBagEquipment(form, flow, EquipmentId.ExcellentPilgrimStaff);
        ClickBagEquipment(form, flow, EquipmentId.ExcellentPilgrimClothes);
        Texture2D excellentWeapon = GD.Load<Texture2D>("res://TheGame/Sprites/Characters/Heroes/wukong/wukong_weapon_excellent.png");
        Texture2D excellentArmor = GD.Load<Texture2D>("res://TheGame/Sprites/Characters/Heroes/wukong/wukong_body_excellent.png");
        EquipmentData wornWeapon = flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Weapon;
        EquipmentData wornArmor = flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Armor;
        Check(hero.GetNode<Sprite2D>("m_Weapon").Texture == excellentWeapon &&
            form.GetNode<Sprite2D>("Window/CharacterPanel/m_Preview/Weapon").Texture == GD.Load<Texture2D>("res://TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_weapon_excellent.png") &&
            hero.GetNode<Sprite2D>("m_Body").Texture == excellentArmor && body.Texture == GD.Load<Texture2D>("res://TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_body_excellent.png") &&
            wornWeapon?.EquipmentId == EquipmentId.ExcellentPilgrimStaff && wornArmor?.EquipmentId == EquipmentId.ExcellentPilgrimClothes &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Power.Value == basePower + wornWeapon.Attributes.Power &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Def.Value == baseDefense + 6 &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).MaxHp.Value == baseMaxHp + wornArmor.Attributes.MaxHp,
            "优秀武器/防具未同步战斗、预览和独立属性");
        CheckHeroAnimationLayers(hero);
        await CaptureCombatAsync("inventory_excellent_wardrobe");
        foreach (EquipmentId id in new[] { EquipmentId.ExcellentCassock, EquipmentId.ExcellentPineStaff })
        {
            ClickBagEquipment(form, flow, id);
            Check(actions.GetNode<TextureButton>("Panel/Equip").Disabled &&
                flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Weapon.EquipmentId == EquipmentId.ExcellentPilgrimStaff &&
                flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Armor.EquipmentId == EquipmentId.ExcellentPilgrimClothes,
                "优秀唐僧装备错误替换悟空外观");
            actions.Hide();
        }
        foreach (string path in new[] { "m_WeaponSlot", "m_ArmorSlot" })
        {
            form.GetNode<EquipmentCell>("Window/CharacterPanel/EquipmentSlots/" + path)
                        ._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        }

        Check(GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Power.Value == basePower && GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Def.Value == baseDefense &&
                    GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).MaxHp.Value == baseMaxHp, "优秀装备卸下后仍残留属性");
        GD.Print("SMOKE-SEA EQUIPMENT 3-1 PASS: independent selector-3 layers, GUI equip/unequip, stats and owner rejection");
        ClickBagEquipment(form, flow, EquipmentId.HeavenlyFiendNecklace);
        Check(flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Accessory?.EquipmentId == EquipmentId.HeavenlyFiendNecklace &&
            accessory.GetNode<TextureRect>("Icon").Texture != null &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Power.Value == basePower + 34 &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Def.Value == baseDefense + 11 &&
            form.GetNode<Label>("Window/CharacterPanel/Attributes/Left/Crit/m_Crit").Text == "3%" &&
            form.GetNode<Label>("Window/CharacterPanel/Attributes/Right/Dodge/m_Dodge").Text == "3%",
            "天煞骨链 GUI 穿戴、图标或官方属性不正确");
        using (Control tooltip = accessory._MakeCustomTooltip(accessory.TooltipText))
        {
            Check(tooltip.GetNode<VBoxContainer>("Content/Attributes").GetChildCount() == 4 &&
                tooltip.GetNode<Label>("Content/Footer/Price").Text == "价值：80 灵魂", "天煞骨链提示属性或售价错误");
            tooltip.Free();
        }
        await CaptureCombatAsync("inventory_heavenly_fiend_necklace");
        accessory._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        Check(GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Power.Value == basePower &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Def.Value == baseDefense, "天煞骨链卸下后仍有属性残留");
        GD.Print("SMOKE-QIXING-NECKLACE PASS: transparent icon, GUI equip/unequip, four attributes and sell price 80");
        int baseRegen = GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).HpRegen.Value;
        ClickBagEquipment(form, flow, EquipmentId.BloodSeaShell);
        EquipmentData shell = flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Accessory;
        Check(shell?.EquipmentId == EquipmentId.BloodSeaShell && accessory.GetNode<TextureRect>("Icon").Texture != null &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).MaxHp.Value == baseMaxHp + shell.Attributes.MaxHp &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Def.Value == baseDefense + 20 &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).HpRegen.Value == baseRegen + 2, "血海妖壳未同步饰品属性与图标");
        using (Control tooltip = accessory._MakeCustomTooltip(accessory.TooltipText))
        {
            Check(tooltip.GetNode<VBoxContainer>("Content/Attributes").GetChildCount() == 3 &&
                tooltip.GetNode<Label>("Content/Header/Type/Value").Text == "饰品·通用" &&
                tooltip.GetNode<Label>("Content/Footer/Price").Text == "价值：80 灵魂", "血海妖壳属性说明或售价错误");
            tooltip.Free();
        }
        await CaptureCombatAsync("inventory_blood_sea_shell");
        accessory._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        Check(GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).MaxHp.Value == baseMaxHp &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Def.Value == baseDefense &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).HpRegen.Value == baseRegen, "血海妖壳卸下后仍残留属性");
        GD.Print("SMOKE-EAST SEA EQUIPMENT 3-2 PASS: transparent accessory, GUI equip/unequip, three attributes and price 80");

        // 鲨之涡流的悟空防具必须使用自己的完整动作图集，且负面属性不能被汇总层吞掉。
        ClickBagEquipment(form, flow, EquipmentId.BloodSeaDemonArmor);
        Texture2D bloodSeaArmorTexture = GD.Load<Texture2D>(
            "res://TheGame/Sprites/Characters/Heroes/wukong/wukong_body_blood_sea.png");
        Check(flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Armor?.EquipmentId == EquipmentId.BloodSeaDemonArmor &&
            hero.GetNode<Sprite2D>("m_Body").Texture == bloodSeaArmorTexture &&
            body.Texture == GD.Load<Texture2D>("res://TheGame/Sprites/Characters/Heroes/wukong/preview/wukong_preview_body_blood_sea.png") &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Power.Value == basePower + 80 &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Def.Value == Math.Max(0, baseDefense - 10) &&
            Mathf.IsEqualApprox(GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Crit.Value, .05f) &&
            Mathf.IsEqualApprox(GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Miss.Value, .05f) &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).HpRegen.Value == baseRegen - 2,
            "血海魔甲未同步战斗、预览独立图集或负面属性");
        CheckHeroAnimationLayers(hero);
        await CaptureCombatAsync("inventory_blood_sea_armor");
        form.GetNode<EquipmentCell>("Window/CharacterPanel/EquipmentSlots/m_ArmorSlot")
            ._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        Check(flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Armor == null &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Power.Value == basePower &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).Def.Value == baseDefense &&
            GF.Fsm.GetModel<HeroModel>().GetState(hero.HeroId).HpRegen.Value == baseRegen,
            "血海魔甲卸下后仍残留属性或外观");
        ClickBagEquipment(form, flow, EquipmentId.BloodSeaEvilStaff);
        Check(actions.GetNode<TextureButton>("Panel/Equip").Disabled &&
            flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Weapon == null,
            "血海邪皇错误替换悟空武器");
        actions.Hide();
        GD.Print("SMOKE-SHARK EQUIPMENT 3-3 PASS: official selector-6 armor, negative stats, full motion and Tang ownership rejection");

        CheckAllWearableEquipment(form, hero, flow);

        form.GetNode<Button>("Window/BagPanel/Footer/m_SellQuality").EmitSignal(BaseButton.SignalName.Pressed);
        form.GetNode<PopupMenu>("Window/BagPanel/Footer/m_SellQuality/m_QualityOptions")
            .EmitSignal(PopupMenu.SignalName.IdPressed, (long)EquipmentQuality.Rough);
        Check(form.GetNode<Control>("m_SaleConfirmation").Visible, "关窗夹具没有打开出售确认");
        EquipmentData[] beforeCloseItems = flow.GetModel<ProfileModel>().GetBag(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).ToArray();
        PushInventoryKey(true);
        PushInventoryKey(false);
        await WaitFramesAsync(2);
        Check(!GF.Base.IsGamePaused && !form.Visible, "关闭背包没有恢复战斗");
        string closedValue = form.GetNode<Label>("Window/BagPanel/Footer/Soul/m_Soul").Text;
        flow.GetModel<ProfileModel>().RestoreSoul(GF.Fsm.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1), originalSoul);
        Check(form.GetNode<Label>("Window/BagPanel/Footer/Soul/m_Soul").Text == closedValue, "关闭后背包仍订阅钱包");
        battleForm.GetNode<TextureButton>("MenuPanel/m_InventoryButton").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntilAsync(() => form.Visible, "背包未复用原窗口");
        Check(!weaponBackground.GetShaderParameter("occupied").AsBool() && !armorBackground.GetShaderParameter("occupied").AsBool(),
            "背包复用残留穿戴背景状态");
        Check(form.GetNode<Label>("Window/BagPanel/Footer/Soul/m_Soul").Text == originalSoul.ToString(), "重新打开未拉取当前钱包");
        form.GetNode<Button>("m_SaleConfirmation/Center/Panel/Content/Actions/m_SaleConfirm").EmitSignal(BaseButton.SignalName.Pressed);
        Check(!form.GetNode<Control>("m_SaleConfirmation").Visible &&
            flow.GetModel<ProfileModel>().GetBag(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).SequenceEqual(beforeCloseItems) && flow.GetModel<ProfileModel>().GetSoul(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value == originalSoul,
            "窗口复用残留出售确认或提交了旧交易");
        form.GetNode<TextureButton>("Window/Header/m_Close").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitFramesAsync(2);
        // 临时改映射验证业务只依赖动作名，完成后恢复项目原输入配置。
        Array<InputEvent> originalKeys = InputMap.ActionGetEvents(GameLogic.Input.InputSystem.InventoryToggle);
        try
        {
            InputMap.ActionEraseEvents(GameLogic.Input.InputSystem.InventoryToggle);
            InputMap.ActionAddEvent(GameLogic.Input.InputSystem.InventoryToggle, new InputEventKey { PhysicalKeycode = Key.V });
            PushInventoryKey(true);
            PushInventoryKey(false);
            await WaitUntilAsync(() => form.Visible, "重映射后背包快捷键失效");
            PushInventoryKey(true);
            PushInventoryKey(false);
            await WaitFramesAsync(2);
            Check(!form.Visible && !GF.Base.IsGamePaused, "重映射后快捷键没有关闭背包");
        }
        finally
        {
            InputMap.ActionEraseEvents(GameLogic.Input.InputSystem.InventoryToggle);
            foreach (InputEvent input in originalKeys)
            {
                InputMap.ActionAddEvent(GameLogic.Input.InputSystem.InventoryToggle, input);
            }
        }
        // 穿戴与背包一起还原，不能把本夹具的法宝加成带入后续裸身成长验证。
        flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value = originalSlots;
        flow.GetModel<ProfileModel>().RestoreItems(GF.Fsm.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1), originalEquipment);
        GD.Print("SMOKE-UI: inventory actual attributes, 25 cells, live soul, pause, animated preview, key remap/echo, unsubscribe and reuse passed");
    }

    /// <summary>
    /// 由当前背包格提交一次鼠标穿戴输入，不绕过窗口业务。
    /// </summary>
    /// <param name="form">已打开的真实窗口。</param>
    /// <param name="flow">受测档案。</param>
    /// <param name="id">要点击的装备定义。</param>
    private static void ClickBagEquipment(InventoryForm form, ProcedureOwner flow, EquipmentId id)
    {
        int index = flow.GetModel<ProfileModel>().GetBag(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).ToList().FindIndex(x => x.EquipmentId == id);
        Check(index >= 0, "受测装备不在背包中");
        // 新关装备超过一页后仍从真实分类、翻页按钮选择，不能把全包索引当格子索引。
        form.GetNode<Control>("Window/BagPanel/m_Tabs").GetChild<TextureButton>(0)
            .EmitSignal(BaseButton.SignalName.Pressed);
        GridContainer grid = form.GetNode<GridContainer>("Window/BagPanel/m_BagGrid");
        TextureButton next = form.GetNode<TextureButton>("Window/BagPanel/Footer/Pages/m_NextPage");
        int pageSize = grid.GetChildCount();
        for (int page = 0; page < index / pageSize; page++)
        {
            Check(!next.Disabled, "受测装备所在页不可访问");
            next.EmitSignal(BaseButton.SignalName.Pressed);
        }
        EquipmentCell cell = grid.GetChild<EquipmentCell>(index % pageSize);
        Check(SmokeInspection.ReadField<EquipmentData>(cell, "_equipmentData").EquipmentId == id,
            "跨页点击选择了其他装备");
        form.GetViewport().PushInput(new InputEventMouseMotion { Position = cell.GetGlobalRect().GetCenter(), GlobalPosition = cell.GetGlobalRect().GetCenter() }, true);
        Guid weapon = flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Weapon?.InstanceId ?? Guid.Empty;
        Guid armor = flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Armor?.InstanceId ?? Guid.Empty;
        cell._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        Check((flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Weapon?.InstanceId ?? Guid.Empty) == weapon &&
            (flow.GetModel<ProfileModel>().GetSlots(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.Armor?.InstanceId ?? Guid.Empty) == armor, "点击格子直接穿戴，跳过菜单");
        EquipmentActionsMenu menu = form.GetNode<EquipmentActionsMenu>("m_Actions");
        Check(menu.Visible, "装备格没有打开操作菜单");
        TextureButton equip = menu.GetNode<TextureButton>("Panel/Equip");
        if (!equip.Disabled)
        {
            equip.EmitSignal(BaseButton.SignalName.Pressed);
        }
    }

    /// <summary>
    /// 比较一项和三项属性的实际布局，头部固定、价格只随中间属性下移。
    /// </summary>
    /// <param name="form">提供正式字体主题的背包。</param>
    /// <param name="flow">提供真实装备定义和实际属性的档案。</param>
    /// <returns>布局完成并清理夹具后的任务。</returns>
    private async Task CheckTooltipLayoutAsync(InventoryForm form, ProcedureOwner flow)
    {
        GridContainer grid = form.GetNode<GridContainer>("Window/BagPanel/m_BagGrid");
        grid.Hide(); // 清掉前一张截图的原生悬停框，避免与本测试的布局夹具重叠。
        await WaitFramesAsync(1);
        grid.Show();
        PackedScene scene = GD.Load<PackedScene>("res://TheGame/UIs/Inventory/equipment_tooltip.tscn");
        // PopupPanel 与实际 tooltip 使用相同的首次最小尺寸测量；覆盖长说明和随后的短内容。
        foreach (EquipmentId id in new[] { EquipmentId.EarthFiendRing, EquipmentId.EarthFiendApeArmor, EquipmentId.RoughPilgrimStaff })
        {
            EquipmentTooltip tooltip = scene.Instantiate<EquipmentTooltip>();
            tooltip.Theme = form.Theme;
            tooltip.Bind(flow.GetSystem<EquipmentSystem>().Roll(Guid.NewGuid(), id, () => .37f),
                ConfigSystem.Instance.Tables.TbEquipmentConfig.Get(id));
            var popup = new PopupPanel();
            form.AddChild(popup);
            popup.AddChild(tooltip);
            popup.Popup(new Rect2I(650, 200, 0, 0));
            await WaitFramesAsync(4);
            Label price = tooltip.GetNode<Label>("Content/Footer/Price");
            float bottom = tooltip.GetGlobalRect().End.Y - price.GetGlobalRect().End.Y;
            Check(Math.Abs(tooltip.Size.Y - tooltip.GetCombinedMinimumSize().Y) < 1 && Math.Abs(bottom - 6) < 1,
                $"{id} 原生弹窗底部留白：size={tooltip.Size}, minimum={tooltip.GetCombinedMinimumSize()}, bottom={bottom}");
            if (id == EquipmentId.EarthFiendApeArmor)
            {
                await CaptureCombatAsync("inventory_tooltip_fitted");
            }

            popup.Free();
        }
        GD.Print("SMOKE-TOOLTIP PASS: native popup first measure, ring/armor/staff content heights and 6px bottom margin.");
        Vector2? headerPosition = null;
        float previousPrice = 0;
        int previousCount = 0;
        foreach (EquipmentId id in new[] { EquipmentId.CommonPineStaff, EquipmentId.CommonPilgrimClothes })
        {
            EquipmentTooltip tooltip = scene.Instantiate<EquipmentTooltip>();
            tooltip.Theme = form.Theme;
            tooltip.Position = new Vector2(650, 220);
            tooltip.Bind(flow.GetSystem<EquipmentSystem>().Roll(Guid.NewGuid(), id, () => .37f),
                ConfigSystem.Instance.Tables.TbEquipmentConfig.Get(id));
            form.AddChild(tooltip);
            await WaitFramesAsync(2);
            VBoxContainer header = tooltip.GetNode<VBoxContainer>("Content/Header");
            VBoxContainer attributes = tooltip.GetNode<VBoxContainer>("Content/Attributes");
            Label price = tooltip.GetNode<Label>("Content/Footer/Price");
            Check(headerPosition == null || header.Position == headerPosition &&
                Math.Abs(price.Position.Y + price.GetParent<Control>().Position.Y - previousPrice -
                    (attributes.GetChildCount() - previousCount) * 25) < .01,
                "属性行数改变时详情头部移动、价格重叠或多出空白");
            headerPosition = header.Position;
            previousPrice = price.Position.Y + price.GetParent<Control>().Position.Y;
            previousCount = attributes.GetChildCount();
            TextureRect quality = tooltip.GetNode<TextureRect>("Content/Header/Quality/Caption/Underline");
            TextureRect type = tooltip.GetNode<TextureRect>("Content/Header/Type/Caption/Underline");
            Check(quality.Position == type.Position && quality.Size == new Vector2(80, 2) &&
                type.Size == quality.Size, "品质/类型下划线未对齐");
            if (id == EquipmentId.CommonPilgrimClothes)
            {
                await CaptureCombatAsync("inventory_tooltip_attributes");
            }

            form.RemoveChild(tooltip);
            tooltip.Free();
        }
        Check(form.GetNode<GridContainer>("Window/BagPanel/m_BagGrid").GetChild<EquipmentCell>(0)
            .GetThemeStylebox("panel", "TooltipPanel") is StyleBoxEmpty, "Godot 外层提示仍绘制第二个背景");
        EquipmentTooltip ring = scene.Instantiate<EquipmentTooltip>();
        ring.Theme = form.Theme;
        ring.Position = new Vector2(650, 180);
        ring.Bind(flow.GetSystem<EquipmentSystem>().Roll(Guid.NewGuid(), EquipmentId.EarthFiendRing, () => .37f),
            ConfigSystem.Instance.Tables.TbEquipmentConfig.Get(EquipmentId.EarthFiendRing));
        form.AddChild(ring);
        await WaitFramesAsync(4);
        Label description = ring.GetNode<Label>("Content/Footer/Description");
        Check(ring.Size.X <= 200 && description.Size.X <= 165 && description.GetLineCount() > 1 &&
            description.GetGlobalRect().End.Y <= ring.GetNode<Label>("Content/Footer/Price").GlobalPosition.Y,
            $"长装备说明未按原版宽度换行：panel={ring.Size}, description={description.Size}, lines={description.GetLineCount()}");
        await CaptureCombatAsync("inventory_tooltip_wrapped");
        ring.Free();
    }

    /// <summary>
    /// 从正式按钮验证分类、三页边界、第三页选择与整包出售，结束后还原夹具。
    /// </summary>
    /// <param name="form">正式背包窗口。</param>
    /// <param name="flow">受测档案。</param>
    /// <returns>包含实际窗口截图的回归任务。</returns>
    private async Task CheckBagNavigationAsync(InventoryForm form, ProcedureOwner flow)
    {
        ProfileModel inventory = flow.GetModel<ProfileModel>();
        EquipmentData[] before = inventory.GetBag(inventory.HeroOf(PlayerSlot.P1)).ToArray();
        int soul = flow.GetModel<ProfileModel>().GetSoul(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value;
        EquipmentData[] items = Enumerable.Range(0, 75).Select(i => flow.GetSystem<EquipmentSystem>().Roll(Guid.NewGuid(),
            i % 3 == 0 ? EquipmentId.CommonPineStaff : EquipmentId.RoughPilgrimStaff, () => .37f)).ToArray();
        inventory.RestoreItems(GF.Fsm.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1), items);
        GridContainer grid = form.GetNode<GridContainer>("Window/BagPanel/m_BagGrid");
        Control tabs = form.GetNode<Control>("Window/BagPanel/m_Tabs");
        TextureButton previous = form.GetNode<TextureButton>("Window/BagPanel/Footer/Pages/m_PreviousPage");
        TextureButton next = form.GetNode<TextureButton>("Window/BagPanel/Footer/Pages/m_NextPage");
        Label number = form.GetNode<Label>("Window/BagPanel/Footer/Pages/m_PageNumber");
        Button sale = form.GetNode<Button>("Window/BagPanel/Footer/m_SellQuality");
        Check(previous.Disabled && !next.Disabled && number.Text == "1 / 3", "第一页边界或页数错误");
        foreach (int index in new[] { 1, 2, 3 })
        {
            tabs.GetChild<TextureButton>(index).EmitSignal(BaseButton.SignalName.Pressed);
            Check(tabs.GetChild<TextureButton>(index).ButtonPressed &&
                tabs.GetChildren().OfType<TextureButton>().Count(x => x.ButtonPressed) == 1 &&
                grid.GetChildren().OfType<EquipmentCell>().All(x => x.GetNode<TextureRect>("Icon").Texture == null) &&
                previous.Disabled && next.Disabled && number.Text == "1 / 1" && sale.Disabled,
                $"分类 {index} 选中态、清空内容或分页限制错误");
        }
        tabs.GetChild<TextureButton>(0).EmitSignal(BaseButton.SignalName.Pressed);
        next.EmitSignal(BaseButton.SignalName.Pressed);
        Check(!previous.Disabled && !next.Disabled && number.Text == "2 / 3", "第二页不可双向翻页");
        next.EmitSignal(BaseButton.SignalName.Pressed);
        Check(!previous.Disabled && next.Disabled && number.Text == "3 / 3" &&
            SmokeInspection.ReadField<EquipmentData>(grid.GetChild<EquipmentCell>(0), "_equipmentData").InstanceId == items[50].InstanceId,
            "第三页边界或实例偏移错误");
        grid.GetChild<EquipmentCell>(0)._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        EquipmentActionsMenu actions = form.GetNode<EquipmentActionsMenu>("m_Actions");
        Check(actions.Visible, "第三页装备没有打开操作菜单");
        actions.GetNode<TextureButton>("Panel/Sell").EmitSignal(BaseButton.SignalName.Pressed);
        Check(inventory.GetBag(inventory.HeroOf(PlayerSlot.P1)).All(x => x.InstanceId != items[50].InstanceId) && flow.GetModel<ProfileModel>().GetSoul(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value == soul + 10,
            "第三页出售选择了第一页同位置装备");
        previous.EmitSignal(BaseButton.SignalName.Pressed);
        previous.EmitSignal(BaseButton.SignalName.Pressed);
        sale.EmitSignal(BaseButton.SignalName.Pressed);
        PopupMenu popup = sale.GetNode<PopupMenu>("m_QualityOptions");
        Check(popup.ItemCount == 6 && popup.GetItemText(0) == "出售粗糙装备（49）" &&
            popup.GetItemText(1) == "出售普通装备（25）" && popup.IsItemDisabled(2),
            "品质菜单未统计全部三页或没有禁用空品质");
        await WaitFramesAsync(3);
        Check(popup.Position.Y + popup.Size.Y <= sale.GetScreenPosition().Y &&
            popup.Position.X + popup.Size.X <= (sale.GetScreenTransform() * sale.Size).X, "出售菜单未向上展开或越过按钮右边界");
        await CaptureCombatAsync("inventory_quality_sale");
        popup.Hide();
        Window window = _driver.GetTree().Root;
        Vector2I originalSize = window.Size;
        window.Size = new Vector2I(1175, 738);
        await WaitFramesAsync(3);
        sale.EmitSignal(BaseButton.SignalName.Pressed);
        await WaitFramesAsync(2);
        Check(Math.Abs(popup.Position.X + popup.Size.X - (sale.GetScreenTransform() * sale.Size).X) <= 1 &&
            popup.Position.Y + popup.Size.Y <= sale.GetScreenPosition().Y, "缩放窗口时品质菜单脱离按钮");
        await CaptureCombatAsync("inventory_quality_sale_scaled");
        popup.Hide();
        window.Size = originalSize;
        await WaitFramesAsync(3);
        sale.EmitSignal(BaseButton.SignalName.Pressed);
        popup.EmitSignal(PopupMenu.SignalName.IdPressed, (long)EquipmentQuality.Rough);
        Control confirmation = form.GetNode<Control>("m_SaleConfirmation");
        Label summary = confirmation.GetNode<Label>("Center/Panel/Content/m_SaleSummary");
        Button cancel = confirmation.GetNode<Button>("Center/Panel/Content/Actions/m_SaleCancel");
        Button confirm = confirmation.GetNode<Button>("Center/Panel/Content/Actions/m_SaleConfirm");
        Check(confirmation.Visible && !popup.Visible && summary.Text.Contains("粗糙") &&
            summary.Text.Contains("49 件") && summary.Text.Contains("490 灵魂") && cancel.HasFocus() &&
            inventory.GetBag(inventory.HeroOf(PlayerSlot.P1)).Count == 74 && flow.GetModel<ProfileModel>().GetSoul(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value == soul + 10,
            "选中品质直接出售、确认摘要错误或默认焦点不是取消");
        await WaitFramesAsync(2);
        PushMouseClick(next);
        await WaitFramesAsync(2);
        Check(number.Text == "1 / 3" && confirmation.Visible, "确认弹层未拦截背后的翻页按钮");
        await CaptureCombatAsync("inventory_sale_confirmation");
        window.Size = new Vector2I(1175, 738);
        await WaitFramesAsync(3);
        Check(confirmation.GetNode<PanelContainer>("Center/Panel").GetGlobalRect().GetCenter()
            .DistanceTo(form.GetGlobalRect().GetCenter()) < 1, "缩放窗口时确认框未保持居中");
        await CaptureCombatAsync("inventory_sale_confirmation_scaled");
        window.Size = originalSize;
        await WaitFramesAsync(3);
        cancel.EmitSignal(BaseButton.SignalName.Pressed);
        confirm.EmitSignal(BaseButton.SignalName.Pressed);
        Check(!confirmation.Visible && inventory.GetBag(inventory.HeroOf(PlayerSlot.P1)).Count == 74 && flow.GetModel<ProfileModel>().GetSoul(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value == soul + 10,
            "取消交易后仍可提交旧确认");
        sale.EmitSignal(BaseButton.SignalName.Pressed);
        popup.EmitSignal(PopupMenu.SignalName.IdPressed, (long)EquipmentQuality.Rough);
        using (var escape = new InputEventAction { Action = "ui_cancel", Pressed = true })
        {
            form.GetViewport().PushInput(escape, true);
        }

        Check(!confirmation.Visible && form.Visible && inventory.GetBag(inventory.HeroOf(PlayerSlot.P1)).Count == 74, "Esc 未取消交易或关闭了整个背包");
        sale.EmitSignal(BaseButton.SignalName.Pressed);
        popup.EmitSignal(PopupMenu.SignalName.IdPressed, (long)EquipmentQuality.Rough);
        flow.GetSystem<ProfileSystem>().AddSoul(GF.Fsm.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1), 1);
        confirm.EmitSignal(BaseButton.SignalName.Pressed);
        Check(!confirmation.Visible && inventory.GetBag(inventory.HeroOf(PlayerSlot.P1)).Count == 74 && flow.GetModel<ProfileModel>().GetSoul(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value == soul + 11,
            "数据变化后旧确认仍能出售装备");
        flow.GetModel<ProfileModel>().RestoreSoul(GF.Fsm.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1), soul + 10);
        sale.EmitSignal(BaseButton.SignalName.Pressed);
        popup.EmitSignal(PopupMenu.SignalName.IdPressed, (long)EquipmentQuality.Rough);
        await WaitFramesAsync(2);
        PushMouseClick(confirm);
        await WaitFramesAsync(2);
        Check(inventory.GetBag(inventory.HeroOf(PlayerSlot.P1)).SequenceEqual(items.Where(x => x.EquipmentId == EquipmentId.CommonPineStaff)) &&
            flow.GetModel<ProfileModel>().GetSoul(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value == soul + 500 && !popup.Visible && !confirmation.Visible,
            "品质出售未跨页提交完整交易或未关闭菜单");
        confirm.EmitSignal(BaseButton.SignalName.Pressed);
        Check(flow.GetModel<ProfileModel>().GetSoul(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value == soul + 500 && inventory.GetBag(inventory.HeroOf(PlayerSlot.P1)).Count == 25, "重复确认重复入账");
        inventory.RestoreItems(GF.Fsm.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1), before);
        flow.GetModel<ProfileModel>().RestoreSoul(GF.Fsm.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1), soul);
        GD.Print("SMOKE-UI: four tabs, 75 slots, three-page bounds; quality sale confirmation, cancel/Esc, modal input, stale/duplicate rejection and bulk transaction passed");
    }

    /// <summary>
    /// 通过真实视口鼠标事件点击控件，验证弹层输入阻挡与按钮响应。
    /// </summary>
    /// <param name="control">鼠标目标控件。</param>
    private static void PushMouseClick(Control control)
    {
        // 将控件局部中心变换到视口，兼容镜像父节点；GetGlobalRect不保证负缩放下的中心正确。
        Vector2 position = control.GetGlobalTransformWithCanvas() * (control.Size / 2);
        using var motion = new InputEventMouseMotion { Position = position, GlobalPosition = position };
        control.GetViewport().PushInput(motion, true);
        foreach (bool pressed in new[] { true, false })
        {
            using var click = new InputEventMouseButton
            {
                Position = position,
                GlobalPosition = position,
                ButtonIndex = MouseButton.Left,
                Pressed = pressed
            };
            control.GetViewport().PushInput(click, true);
        }
    }

    /// <summary>
    /// 从项目输入映射取得真实按键，避免烟测绕开动作映射。
    /// </summary>
    /// <param name="pressed">按下或释放。</param>
    /// <param name="echo">是否模拟长按产生的重复按下。</param>
    private void PushInventoryKey(bool pressed, bool echo = false)
    {
        var input = (InputEventKey)InputMap.ActionGetEvents(GameLogic.Input.InputSystem.InventoryToggle).OfType<InputEventKey>().First().Duplicate();
        input.Pressed = pressed;
        input.Echo = echo;
        _driver.GetViewport().PushInput(input, true);
        input.Dispose();
    }

    /// <summary>
    /// 在暂停中的真实实体上逐帧采样全部已接入动作，验证两层同步、有效素材与固定根原点。
    /// </summary>
    /// <param name="hero">当前暂停中的英雄。</param>
    private static void CheckHeroAnimationLayers(AbstractHeroEntity hero)
    {
        AnimationPlayer player = hero.GetNode<AnimationPlayer>("m_AnimPlayer");
        Sprite2D body = hero.GetNode<Sprite2D>("m_Body");
        Sprite2D weapon = hero.GetNode<Sprite2D>("m_Weapon");
        Vector2 root = hero.Position;
        int facing = hero.Facing;
        string previous = player.CurrentAnimation;
        double position = player.CurrentAnimationPosition;
        bool active = player.Active;
        using Image image = body.Texture.GetImage();
        foreach (int direction in new[] { -1, 1 })
        {
            hero.SetFacing(direction);
            foreach (string name in player.GetAnimationList().Where(name => name != HeroAnims.Reset.ToString()))
            {
                Animation animation = player.GetAnimation(name);
                player.Play(name);
                for (double time = 0; time < animation.Length; time += 1d / 60)
                {
                    // 此处只采样画面；不重放每个采样点之前的音效和战斗方法轨道。
                    player.Seek(time, update: true, updateOnly: true);
                    Check(body.Frame >= 0 && body.Frame < body.Hframes * body.Vframes &&
                        weapon.Frame >= 0 && weapon.Frame < weapon.Hframes * weapon.Vframes &&
                        body.FlipH == weapon.FlipH && hero.Position == root,
                        $"动作 {name} 身体/武器帧越界、朝向不同步或修改了角色根坐标");
                    // 每层可有独立紧凑帧映射；用本层网格采样，不要求两层帧号相等。
                    int width = image.GetWidth() / body.Hframes;
                    int height = image.GetHeight() / body.Vframes;
                    Rect2I frame = new(body.Frame % body.Hframes * width, body.Frame / body.Hframes * height, width, height);
                    using Image crop = image.GetRegion(frame);
                    // 官方烈焰闪和火魔斩在技能表现接管时共用透明身体格91。
                    bool effectOnly = hero is WukongEntity && body.Frame == 91 &&
                        name is "skills/blazing_flash" or "skills/fire_demon_slash";
                    Check(crop.IsInvisible() == effectOnly, $"动作 {name} 身体帧 {body.Frame} 透明性不符");
                }
            }
        }
        hero.SetFacing(facing);
        // 死亡采样写过专属可见性；普通动作不负责恢复它，夹具必须先应用正式RESET。
        player.Play(HeroAnims.Reset);
        player.Seek(0, update: true, updateOnly: true);
        player.Play(string.IsNullOrEmpty(previous) ? "idle1" : previous);
        player.Seek(position, update: true, updateOnly: true);
        player.Active = active;
    }

    /// <summary>
    /// 等待失败结算并检查成绩、按钮和暂停，然后按重试。
    /// </summary>
    /// <param name="run">已提交死亡检查点的关卡。</param>
    /// <returns>已提交重试请求的任务。</returns>
    private async Task CheckFailureSettlementAsync(LevelViewController run)
    {
        await WaitUntilAsync(() => GF.UI.GetAllLoadedUIForms().OfType<SettlementForm>().Any(x => x.Visible), "死亡后未显示失败结算");
        SettlementForm form = GF.UI.GetAllLoadedUIForms().OfType<SettlementForm>().Single(x => x.Visible);
        Check(SmokeInspection.Model(run).Result.Outcome == LevelOutcome.Defeated && SmokeInspection.Model(run).Result.HealthPercent == 0, "失败成绩未冻结死亡状态");
        Check(form.GetNode<TextureButton>("Content/Actions/m_Retry").Visible && GF.Base.IsGamePaused, "失败结算缺少重试或未暂停");
        await CheckSettlementMusicAsync("settlement_defeat.mp3");
        await WaitFramesAsync(12);
        Check(ReferenceEquals(SmokeInspection.Level(GF.Procedure.CurrentProcedure as ProcedureLevel), run), "失败结算自动重开了关卡");
        await CaptureCombatAsync("settlement_failure");
        form.GetNode<TextureButton>("Content/Actions/m_Retry").EmitSignal(BaseButton.SignalName.Pressed);
    }

    /// <summary>
    /// 检查实际 GGF 音乐代理，结算不能残留关卡音乐。
    /// </summary>
    /// <param name="file">结算曲资源文件名。</param>
    /// <returns>唯一代理就绪的验证任务。</returns>
    private async Task CheckSettlementMusicAsync(string file)
    {
        await WaitUntilAsync(() => _driver.GetTree().Root.FindChildren("*", "AudioStreamPlayer", true, false)
            .OfType<AudioStreamPlayer>().Any(player => player.Bus == "Music" && player.Playing &&
                player.Stream.ResourcePath.EndsWith(file, StringComparison.Ordinal)), "结算音乐没有经过 GGF 播放");
        AudioStreamPlayer[] music = _driver.GetTree().Root.FindChildren("*", "AudioStreamPlayer", true, false)
            .OfType<AudioStreamPlayer>().Where(player => player.Bus == "Music" && player.Playing).ToArray();
        Check(music.Length == 1, "结算音乐与关卡背景音乐叠加");
        Check(music[0].VolumeDb > -1f && !AudioServer.IsBusMute(AudioServer.GetBusIndex("Music")),
            $"结算代理虽在播放，但暂停使淡入卡在静音：{music[0].VolumeDb}dB");
        GD.Print($"SMOKE-MUSIC PASS: unique paused settlement Music agent {file}, volume={music[0].VolumeDb}dB");
    }

    /// <summary>
    /// 失败页池复用后仍可选择返回地图，再次进关不残留暂停或成绩。
    /// </summary>
    /// <returns>返回地图并重新进关的任务。</returns>
    private async Task CheckFailureReturnMapAsync()
    {
        AbstractHeroEntity hero = FindHero();
        LevelViewController level = FindLevel();
        KillHero(hero);
        await WaitUntilAsync(() => GF.UI.GetAllLoadedUIForms().OfType<SettlementForm>().Any(x => x.Visible), "第二次死亡没有显示结算");
        SettlementForm form = GF.UI.GetAllLoadedUIForms().OfType<SettlementForm>().Single(x => x.Visible);
        Check(SmokeInspection.Model(level).Result.DefeatedEnemies == 0 && form.GetNode<Label>("Content/Results/Kills/m_Kills").Text == "0", "结算池复用遗留上局成绩");
        form.GetNode<TextureButton>("Content/Actions/m_ReturnMap").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntilAsync(() => GF.Procedure.CurrentProcedure is ProcedureMap && GF.UI.GetAllLoadedUIForms().OfType<MapForm>().Any(x => x.Visible), "失败结算没有返回地图");
        Check(!GF.Base.IsGamePaused && !form.Visible, "失败返回地图残留结算或暂停");
        await WaitUntilAsync(() =>
        {
            FrontendSmokeScenario.AdvanceToLevel();
            return GF.Procedure.CurrentProcedure is ProcedureLevel && CurrentBattleForm() != null;
        }, "失败返回地图后无法重新进关");
        GD.Print("SMOKE-UI: failure settlement retry, return map, retained checkpoint and pooled results passed");
    }
}
