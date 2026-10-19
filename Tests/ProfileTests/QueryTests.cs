using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Equipment;
using GameLogic.Architecture;
using GameLogic.Equipment;
using GameLogic.Level;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>通过正式背包查询验证跨页汇总、作用域和一次性结果与持续绑定的区别。</summary>
public sealed class QueryTests
{
    /// <summary>查询覆盖整个背包并排除穿戴物，按品质统计的售价与正式出售结果一致。</summary>
    [Fact]
    public void SaleSummary_CoversAllPagesAndMatchesActualSale()
    {
        var scope = Ready();
        var worn = AddItem(scope, EquipmentId.RoughPilgrimStaff);
        Assert.True(scope.SendCommand(new EquipEquipmentCommand(worn.InstanceId)));
        for (int i = 0; i < 75; i++)
            AddItem(scope, i % 3 == 0 ? EquipmentId.CommonPineStaff : EquipmentId.RoughPilgrimStaff);
        var retained = scope.Profile.EquipmentDatas.Where(data => data.EquipmentId == EquipmentId.CommonPineStaff).ToArray();
        var rough = scope.SendQuery(new EquipmentSaleSummaryQuery(EquipmentQuality.Rough));
        var common = scope.SendQuery(new EquipmentSaleSummaryQuery(EquipmentQuality.Common));
        Assert.Equal(50, rough.Count);
        Assert.Equal(25, common.Count);
        Assert.Equal(50L * TestTables.Tables.TbEquipmentConfig.Get(EquipmentId.RoughPilgrimStaff).SellPrice, rough.Price);
        Assert.Equal(25L * TestTables.Tables.TbEquipmentConfig.Get(EquipmentId.CommonPineStaff).SellPrice, common.Price);
        int soul = scope.Profile.Soul.Value;
        Assert.Equal(rough.Count, scope.SendCommand(new SellEquipmentByQualityCommand(EquipmentQuality.Rough)));
        Assert.Equal(soul + rough.Price, scope.Profile.Soul.Value);
        Assert.Same(worn, scope.Profile.Weapon);
        Assert.Equal(retained, scope.Profile.EquipmentDatas);
        Assert.Equal((0, 0L), scope.SendQuery(new EquipmentSaleSummaryQuery(EquipmentQuality.Rough)));
        Assert.Equal(common, scope.SendQuery(new EquipmentSaleSummaryQuery(EquipmentQuality.Common)));
    }

    /// <summary>查询不发变更通知、不订阅变化；查询值保持不变，正常绑定继续观察后来的领取。</summary>
    [Fact]
    public void Query_IsOneShotWhileBindingsObserveLaterChanges()
    {
        var scope = Ready();
        int inventoryChanges = 0;
        int soulChanges = 0;
        Action<IReadOnlyList<EquipmentData>> onInventoryChanged = _ => inventoryChanges++;
        Action<int> onSoulChanged = _ => soulChanges++;
        scope.Profile.EquipmentDatas.Register(onInventoryChanged);
        scope.Profile.Soul.Register(onSoulChanged);
        try
        {
            var query = new EquipmentSaleSummaryQuery(EquipmentQuality.Rough);
            var before = scope.SendQuery(query);
            Assert.Equal((0, 0L), before);
            Assert.Equal(0, inventoryChanges);
            Assert.Equal(0, soulChanges);
            AddItem(scope, EquipmentId.RoughPilgrimStaff);
            Assert.Equal(1, inventoryChanges);
            Assert.Equal((0, 0L), before);
            Assert.Equal(1, scope.SendQuery(query).Count);
            Assert.Equal(1, inventoryChanges);
            Assert.Equal(0, soulChanges);
        }
        finally
        {
            scope.Profile.EquipmentDatas.UnRegister(onInventoryChanged);
            scope.Profile.Soul.UnRegister(onSoulChanged);
        }
    }

    /// <summary>控制器的查询只读自己的作用域；同一查询重发时使用新发送者，空请求明确拒绝。</summary>
    [Fact]
    public void ControllerQuery_UsesCurrentScopeWithoutCachingModels()
    {
        var first = Ready();
        var second = Ready();
        AddItem(first, EquipmentId.RoughPilgrimStaff);
        var query = new EquipmentSaleSummaryQuery(EquipmentQuality.Rough);
        var firstController = new Controller(first);
        var secondController = new Controller(second);
        Assert.Equal(1, firstController.SendQuery(query).Count);
        Assert.Equal((0, 0L), secondController.SendQuery(query));
        AddItem(second, EquipmentId.RoughPilgrimClothes);
        Assert.Equal(1, secondController.SendQuery(query).Count);
        Assert.Equal(1, firstController.SendQuery(query).Count);
        Assert.Throws<ArgumentNullException>(() => first.SendQuery<(int, long)>(null));
        Assert.Throws<ArgumentNullException>(() => ((IArchitecture)null).SendQuery(query));
    }

    /// <summary>展示摘要不预留装备或承诺成交；余额溢出和摘要之后的穿戴仍由出售系统校验。</summary>
    [Fact]
    public void SaleSummary_DoesNotAuthorizeOrReserveTheSale()
    {
        var scope = Ready(int.MaxValue - 15);
        var first = AddItem(scope, EquipmentId.RoughPilgrimStaff);
        AddItem(scope, EquipmentId.RoughPilgrimClothes);
        var summary = scope.SendQuery(new EquipmentSaleSummaryQuery(EquipmentQuality.Rough));
        Assert.Equal((2, 20L), summary);
        Assert.Equal(0, scope.SendCommand(new SellEquipmentByQualityCommand(EquipmentQuality.Rough)));
        Assert.Equal(2, scope.Profile.EquipmentDatas.Count);
        Assert.Equal(int.MaxValue - 15, scope.Profile.Soul.Value);
        Assert.True(scope.SendCommand(new EquipEquipmentCommand(first.InstanceId)));
        Assert.Equal(1, scope.SendCommand(new SellEquipmentByQualityCommand(EquipmentQuality.Rough)));
        Assert.Same(first, scope.Profile.Weapon);
        Assert.Equal(int.MaxValue - 5, scope.Profile.Soul.Value);
        Assert.Equal((2, 20L), summary);
    }

    /// <summary>无匹配品质或无效枚举得到空摘要，不改变背包。</summary>
    /// <param name="quality">无匹配物的品质。</param>
    [Theory]
    [InlineData(EquipmentQuality.Excellent)]
    [InlineData(EquipmentQuality.Legendary)]
    [InlineData((EquipmentQuality)99)]
    public void SaleSummary_UnmatchedQualityIsEmpty(EquipmentQuality quality)
    {
        var scope = Ready();
        var item = AddItem(scope, EquipmentId.RoughPilgrimStaff);
        Assert.Equal((0, 0L), scope.SendQuery(new EquipmentSaleSummaryQuery(quality)));
        Assert.Same(item, Assert.Single(scope.Profile.EquipmentDatas));
    }

    /// <summary>建立使用正式系统的独立关卡。</summary>
    /// <param name="soul">初始灵魂。</param>
    /// <returns>已开始的独立测试作用域。</returns>
    private static TestGameScope Ready(int soul = 0)
    {
        var scope = new TestGameScope(1, [(1, 0)], soul, 75);
        scope.GetSystem<LevelSystem>().Prepare(1);
        scope.GetSystem<LevelSystem>().StartSession(1);
        return scope;
    }

    /// <summary>经正式装备抽取与领取准备背包，不复制生产计算。</summary>
    /// <param name="scope">所属作用域。</param>
    /// <param name="id">装备定义。</param>
    /// <returns>加入的同一装备。</returns>
    private static EquipmentData AddItem(TestGameScope scope, EquipmentId id)
    {
        var data = scope.GetSystem<EquipmentSystem>().Roll(Guid.NewGuid(), id, () => 0);
        Assert.True(scope.GetSystem<ProfileSystem>().TryCollectEquipment(data));
        return data;
    }

    /// <summary>使用正式控制器查询能力，不增加生产观测入口。</summary>
    private sealed class Controller(IArchitecture architecture) : IController
    {
        /// <inheritdoc />
        IArchitecture IBelongToArchitecture.GetArchitecture() => architecture;
    }
}
