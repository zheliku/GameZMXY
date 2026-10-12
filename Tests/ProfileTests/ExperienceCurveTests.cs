using System;
using GameConfig.Hero;
using Luban;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>使用正式 Luban 产物验证经验门槛、连升、满级边界。</summary>
public sealed class ExperienceCurveTests
{
    /// <summary>读取与游戏相同的等级表。</summary>
    /// <returns>经过业务初始化校验的经验曲线。</returns>
    private static ExperienceCurve LoadCurve() => new(TestTables.Tables.TbHeroLevelConfig.DataList);

    /// <summary>精确到达门槛即升级，余量进入下一级而不丢失。</summary>
    /// <param name="total">累计经验。</param>
    /// <param name="level">预期等级。</param>
    /// <param name="experience">预期本级进度。</param>
    /// <param name="maximum">预期本级上限。</param>
    [Theory]
    [InlineData(-1, 1, 0, 135)]
    [InlineData(0, 1, 0, 135)]
    [InlineData(134, 1, 134, 135)]
    [InlineData(135, 2, 0, 145)]
    [InlineData(136, 2, 1, 145)]
    [InlineData(280, 3, 0, 155)]
    [InlineData(472, 4, 37, 165)]
    public void ResolvesThresholds(int total, int level, int experience, int maximum)
        => Assert.Equal((level, experience, maximum), LoadCurve().Evaluate(total));

    /// <summary>一次超大收益支持跨多级并在表末级封顶，不溢出。</summary>
    [Fact]
    public void CapsLargeRewards()
    {
        ExperienceCurve curve = LoadCurve();
        Assert.Equal(55, curve.MaxLevel);
        Assert.Equal((55, 0, 0), curve.Evaluate(curve.Add(139, int.MaxValue)));
        Assert.Equal(curve.MaxTotalExperience, curve.Add(curve.MaxTotalExperience, int.MaxValue));
        Assert.Equal((54, 179999, 180000), curve.Evaluate(curve.MaxTotalExperience - 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => curve.Add(0, -1));
    }

    /// <summary>初始化拒绝等级缺失、重复、错误末级标记和整数溢出。</summary>
    [Fact]
    public void RejectsInvalidTables()
    {
        Assert.Throws<ArgumentException>(() => new ExperienceCurve(Array.Empty<HeroLevelConfig>()));
        Assert.Throws<ArgumentException>(() => new ExperienceCurve([Row(1, 10), Row(1, 0)]));
        Assert.Throws<ArgumentException>(() => new ExperienceCurve([Row(1, 10), Row(3, 0)]));
        Assert.Throws<ArgumentException>(() => new ExperienceCurve([Row(1, 0), Row(2, 0)]));
        Assert.Throws<ArgumentException>(() => new ExperienceCurve([Row(1, 10), Row(2, 10)]));
        Assert.Throws<ArgumentException>(() => new ExperienceCurve([Row(1, int.MaxValue), Row(2, 1), Row(3, 0)]));
        Assert.Equal((2, 1, 20), new ExperienceCurve([Row(3, 0), Row(1, 10), Row(2, 20)]).Evaluate(11));
    }

    /// <summary>用生成类的真实反序列化入口构造错误表数据，不添加业务测试接口。</summary>
    /// <param name="level">等级。</param>
    /// <param name="maximum">升级所需经验。</param>
    /// <returns>生成类型的表行。</returns>
    private static HeroLevelConfig Row(int level, int maximum)
    {
        ByteBuf buffer = new();
        buffer.WriteInt(level);
        buffer.WriteString("test");
        buffer.WriteString("validation");
        buffer.WriteInt(maximum);
        return new HeroLevelConfig(buffer);
    }
}
