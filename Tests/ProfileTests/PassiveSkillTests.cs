using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameConfig.Attributes;
using GameConfig.Equipment;
using GameConfig.Skills;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attributes;
using GameLogic.Equipment;
using GameLogic.Skills;
using Newtonsoft.Json;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>
/// 被动购买的费用、总加成、英雄归属、绑定及完整保存/回滚。
/// </summary>
public sealed class PassiveSkillTests
{
    /// <summary>
    /// 两人的装备、被动和同名 Buff 来源各自汇总，移除或卸下一人的来源不改变另一人。
    /// </summary>
    /// <param name="heroId">本次变更来源的英雄。</param>
    /// <param name="otherId">保留已有来源的另一英雄。</param>
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    public void TwoHeroes_KeepEquipmentPassivesAndBuffSourcesIndependent(int heroId, int otherId)
    {
        var scope = new TestGameScope([(1, 0), (2, 0)], 100000, 20);
        HeroSystem heroes = scope.GetSystem<HeroSystem>();
        SkillSystem skills = scope.GetSystem<SkillSystem>();
        EquipmentSystem equipment = scope.GetSystem<EquipmentSystem>();
        ProfileSystem profile = scope.GetSystem<ProfileSystem>();
        HeroState hero = scope.GetModel<HeroModel>().GetState(heroId);
        HeroState other = scope.GetModel<HeroModel>().GetState(otherId);
        var source = new AttributeSourceId("Buff", 17);
        int basePower = hero.Power.Value;
        int baseHp = hero.MaxHp.Value;
        float baseCrit = hero.Crit.Value;

        // 两人使用同一来源键与同类装备，仍只由各自的状态字典拥有修正。
        EquipmentData otherRing = equipment.Roll(Guid.NewGuid(), EquipmentId.EarthFiendRing, () => .7f);
        Assert.True(profile.TryCollectEquipment(otherId, otherRing));
        Assert.True(equipment.TryEquip(otherId, otherRing.InstanceId));
        Assert.Equal(SkillLearnResult.Learned, skills.Upgrade(otherId, PassiveId.Vitality));
        heroes.SetSource(otherId, source, [AttributeModifier.Flat(AttributeType.Power, 19)]);
        var otherBefore = (other.CaptureCombatAttributes(-otherId), other.MaxHp.Value, other.MaxMp.Value,
            other.Hp.Value, other.Mp.Value, other.Sources.Count);
        IReadOnlyList<AttributeModifier> otherBuff = other.Sources[source];

        EquipmentData ring = equipment.Roll(Guid.NewGuid(), EquipmentId.EarthFiendRing, () => .2f);
        Assert.True(profile.TryCollectEquipment(heroId, ring));
        Assert.True(equipment.TryEquip(heroId, ring.InstanceId));
        Assert.Equal(basePower + ring.Attributes.Power, hero.Power.Value);
        Assert.True(ring.Attributes.Crit > 0);
        Assert.Equal(baseCrit + ring.Attributes.Crit, hero.Crit.Value);
        Assert.Equal(SkillLearnResult.Learned, skills.Upgrade(heroId, PassiveId.Vitality));
        int passiveHp = (int)TestTables.Tables.TbPassiveConfig.Get(PassiveId.Vitality).Levels[0].Value;
        Assert.Equal(baseHp + ring.Attributes.MaxHp + passiveHp, hero.MaxHp.Value);
        heroes.SetSource(heroId, source, [AttributeModifier.Flat(AttributeType.Power, 7)]);
        Assert.Equal(basePower + ring.Attributes.Power + 7, hero.Power.Value);
        Assert.True(heroes.RemoveSource(heroId, source));
        Assert.Equal(basePower + ring.Attributes.Power, hero.Power.Value);
        Assert.True(equipment.TryUnequip(heroId, EquipmentSlot.Accessory));
        Assert.Equal(basePower, hero.Power.Value);
        Assert.Equal(baseCrit, hero.Crit.Value);
        Assert.Equal(baseHp + passiveHp, hero.MaxHp.Value);
        Assert.Same(otherBuff, other.Sources[source]);
        Assert.Equal(otherBefore, (other.CaptureCombatAttributes(-otherId), other.MaxHp.Value, other.MaxMp.Value,
            other.Hp.Value, other.Mp.Value, other.Sources.Count));
    }

    /// <summary>
    /// 连续重建后旧状态不再更新，三类旧闭包确实退订，新闭包各注册一次并更新本人的新状态。
    /// </summary>
    [Fact]
    public void TwoHeroes_RestartDetachesAllOldCallbacksAndStates()
    {
        var scope = new TestGameScope([(1, 0), (2, 0)], 100000, 20);
        HeroSystem system = scope.GetSystem<HeroSystem>();
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        var subscriptions = (Dictionary<int, (Action<HeroProgressionState> Progress, Action<EquippedItems> Slots,
            Action<IReadOnlyDictionary<PassiveId, int>> Passives)>)typeof(HeroSystem)
            .GetField("_subscriptions", PrivateInstance).GetValue(system);
        for (int cycle = 0; cycle < 2; cycle++)
        {
            HeroModel oldModel = scope.GetModel<HeroModel>();
            Dictionary<int, HeroState> oldStates = oldModel.Heroes.ToDictionary(pair => pair.Key, pair => pair.Value);
            var oldCallbacks = subscriptions.ToDictionary(pair => pair.Key, pair => pair.Value);
            scope.RestartHero();
            Assert.NotSame(oldModel, scope.GetModel<HeroModel>());
            Assert.Empty(oldModel.Heroes);

            foreach (int heroId in scope.Profile.HeroIds)
            {
                HeroState old = oldStates[heroId];
                HeroState current = scope.GetModel<HeroModel>().GetState(heroId);
                Assert.NotSame(old, current);
                var before = (old.CaptureCombatAttributes(-heroId), old.MaxHp.Value, old.MaxMp.Value, old.Hp.Value, old.Mp.Value);

                // 仅旧状态不变化不能证明退订：闭包会重新取当前模型，因此核对绑定源内的实际委托。
                var progress = (PriorityCallbacks<Action<HeroProgressionState>>)scope.Profile.GetProgress(heroId)
                    .GetType().GetField("_callbacks", PrivateInstance).GetValue(scope.Profile.GetProgress(heroId));
                var slots = (PriorityCallbacks<Action<EquippedItems>>)scope.Profile.GetSlots(heroId)
                    .GetType().GetField("_callbacks", PrivateInstance).GetValue(scope.Profile.GetSlots(heroId));
                var passives = (PriorityCallbacks<Action<IReadOnlyDictionary<PassiveId, int>>>)scope.Skills.GetPassiveLevels(heroId)
                    .GetType().GetField("_callbacks", PrivateInstance).GetValue(scope.Skills.GetPassiveLevels(heroId));
                Assert.DoesNotContain(oldCallbacks[heroId].Progress, progress.Snapshot);
                Assert.DoesNotContain(oldCallbacks[heroId].Slots, slots.Snapshot);
                Assert.DoesNotContain(oldCallbacks[heroId].Passives, passives.Snapshot);
                Assert.Equal(1, progress.Snapshot.Count(callback => callback == subscriptions[heroId].Progress));
                Assert.Equal(1, slots.Snapshot.Count(callback => callback == subscriptions[heroId].Slots));
                Assert.Equal(1, passives.Snapshot.Count(callback => callback == subscriptions[heroId].Passives));

                int previousLevel = current.Level.Value;
                scope.GetSystem<ProfileSystem>().AddExperience(heroId, 1000);
                Assert.True(current.Level.Value > previousLevel);
                EquipmentData ring = scope.GetSystem<EquipmentSystem>().Roll(Guid.NewGuid(), EquipmentId.EarthFiendRing, () => .5f);
                Assert.True(scope.GetSystem<ProfileSystem>().TryCollectEquipment(heroId, ring));
                Assert.True(scope.GetSystem<EquipmentSystem>().TryEquip(heroId, ring.InstanceId));
                Assert.Equal(ring, scope.Profile.GetSlots(heroId).Value.Accessory);
                Assert.Equal(current.BaseAttributes.Crit + ring.Attributes.Crit, current.Crit.Value);
                int maxHp = current.MaxHp.Value;
                Assert.Equal(SkillLearnResult.Learned, scope.GetSystem<SkillSystem>().Upgrade(heroId, PassiveId.Vitality));
                Assert.True(current.MaxHp.Value > maxHp);
                Assert.Equal(before, (old.CaptureCombatAttributes(-heroId), old.MaxHp.Value, old.MaxMp.Value, old.Hp.Value, old.Mp.Value));
            }
        }
    }

    /// <summary>
    /// 当前等级贡献为总值，升2级只增加100上限并保留原有生命缺损。
    /// </summary>
    [Fact]
    public void Upgrade_ChargesNextLevelAndReplacesTotalBonus()
    {
        var scope = new TestGameScope([(1, 0)], 15000, 25);
        SkillSystem skill = scope.GetSystem<SkillSystem>();
        scope.GetSystem<HeroSystem>().Damage(scope.Hero.HeroId, 30);
        Assert.Equal(SkillLearnResult.Learned, skill.Upgrade(1, PassiveId.Vitality));
        Assert.Equal((280, 250, 10000, 1), (scope.Hero.MaxHp.Value, scope.Hero.Hp.Value,
            scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value, scope.Skills.GetPassiveLevels(1)[PassiveId.Vitality]));
        Assert.Equal(SkillLearnResult.Learned, skill.Upgrade(1, PassiveId.Vitality));
        Assert.Equal((380, 350, 0, 2), (scope.Hero.MaxHp.Value, scope.Hero.Hp.Value,
            scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value, scope.Skills.GetPassiveLevels(1)[PassiveId.Vitality]));
    }

    /// <summary>
    /// 余额不足、满级与未知编号拒绝扣费，五级总价按官方每级递增。
    /// </summary>
    [Fact]
    public void Upgrade_RejectsInsufficientBalanceMaximumAndUnknownId()
    {
        var scope = new TestGameScope([(1, 0)], 4999, 25);
        SkillSystem skill = scope.GetSystem<SkillSystem>();
        Assert.Equal(SkillLearnResult.InsufficientSoul, skill.Upgrade(1, PassiveId.Vitality));
        Assert.Equal((4999, 0, 80), (scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value,
            scope.Skills.GetPassiveLevels(1)[PassiveId.Vitality], scope.Hero.MaxHp.Value));
        scope.GetSystem<ProfileSystem>().AddSoul(1, 75000 - 4999);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(SkillLearnResult.Learned, skill.Upgrade(1, PassiveId.Vitality));
        }

        Assert.Equal((0, 680, 5), (scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value, scope.Hero.MaxHp.Value,
                    scope.Skills.GetPassiveLevels(1)[PassiveId.Vitality]));
        Assert.Equal(SkillLearnResult.AlreadyLearned, skill.Upgrade(1, PassiveId.Vitality));
        Assert.Equal(SkillLearnResult.Unavailable, skill.Upgrade(1, (PassiveId)999));
        Assert.Equal(0, scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value);
    }

    /// <summary>
    /// 五种效果进入同一英雄属性汇总，保存重读和关卡重开不丢失、不叠加。
    /// </summary>
    [Fact]
    public void AllEffects_SurviveSaveRestartAndRemainHeroOwned()
    {
        var scope = new TestGameScope([(1, 0), (2, 0)], 25000, 25);
        foreach (PassiveId id in Enum.GetValues<PassiveId>())
        {
            Assert.Equal(SkillLearnResult.Learned, scope.GetSystem<SkillSystem>().Upgrade(1, id));
        }

        Assert.Equal((280, 250, .02f, 2, 2), (scope.Hero.MaxHp.Value, scope.Hero.MaxMp.Value,
                    scope.Hero.Crit.Value, scope.Hero.HpRegen.Value, scope.Hero.MpRegen.Value));
        Assert.All(scope.Skills.GetPassiveLevels(2).Values, level => Assert.Equal(0, level));
        scope.RestartHero();
        Assert.Equal(280, scope.Hero.MaxHp.Value);
        GameData saved = JsonConvert.DeserializeObject<GameData>(JsonConvert.SerializeObject(scope.CaptureSaveData()));
        var loaded = TestGameScope.FromSaveData(saved, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25);
        Assert.Equal((280, 250, .02f, 2, 2), (loaded.Hero.MaxHp.Value, loaded.Hero.MaxMp.Value,
            loaded.Hero.Crit.Value, loaded.Hero.HpRegen.Value, loaded.Hero.MpRegen.Value));
    }

    /// <summary>
    /// 回滚原地恢复等级与扣费，原字典仍被订阅，快照不会跟随之后的升级变化。
    /// </summary>
    [Fact]
    public void Rollback_RestoresLevelsAndBalanceUsingSameBinding()
    {
        var scope = new TestGameScope([(1, 0)], 15000, 25);
        BindableDictionary<PassiveId, int> levels = scope.Skills.GetPassiveLevels(1);
        GameData checkpoint = scope.CaptureSaveData();
        scope.GetSystem<SkillSystem>().Upgrade(1, PassiveId.Vitality);
        Assert.Equal(0, checkpoint.Heroes[0].PassiveLevels[PassiveId.Vitality]);
        scope.RestoreCheckpoint(checkpoint);
        Assert.Same(levels, scope.Skills.GetPassiveLevels(1));
        Assert.Equal((0, 15000, 80), (levels[PassiveId.Vitality], scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value, scope.Hero.MaxHp.Value));
    }

    /// <summary>
    /// 坏被动记录在任何写入前失败；玩家命令还必须满足当前关卡资格。
    /// </summary>
    /// <param name="level">本次测试使用的等级或被动技能等级。</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void InvalidLevels_RejectBeforeWriting(int level)
    {
        var scope = new TestGameScope([(1, 0)], 10000, 25);
        GameData data = scope.CaptureSaveData();
        data.Heroes[0].PassiveLevels[PassiveId.Vitality] = level;
        Assert.Throws<ArgumentException>(() => scope.RestoreCheckpoint(data));
        Assert.Equal(0, scope.Skills.GetPassiveLevels(1)[PassiveId.Vitality]);
        Assert.Equal(SkillLearnResult.Unavailable, scope.SendCommand(new UpgradePassiveCommand(1, PassiveId.Vitality)));
        Assert.Equal(10000, scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value);
    }
}
