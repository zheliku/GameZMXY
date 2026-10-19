using GameConfig.Equipment;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Profile;

namespace GameLogic.Equipment;

/// <summary>汇总整个未穿戴背包的指定品质装备；结果用于展示，出售时仍由 System 重新校验。</summary>
/// <param name="quality">要查询的品质。</param>
public sealed class EquipmentSaleSummaryQuery(EquipmentQuality quality) : AbstractQuery<(int Count, long Price)>
{
    /// <summary>只读取背包与配置，返回执行这一刻的件数和未截断总售价。</summary>
    /// <returns>独立的数值摘要；不受后续背包变化影响。</returns>
    protected override (int Count, long Price) OnDo()
    {
        int count = 0;
        long price = 0;
        var definitions = this.GetModel<ConfigurationModel>().Tables.TbEquipmentConfig;
        // 统计整个权威背包，与界面当前分类和页码无关；穿戴物不在此集合。
        foreach (var data in this.GetModel<ProfileModel>().EquipmentDatas)
        {
            var definition = definitions.Get(data.EquipmentId);
            if (definition.Quality != quality)
                continue;
            count++;
            price += definition.SellPrice;
        }

        return (count, price);
    }
}
