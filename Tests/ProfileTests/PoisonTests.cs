using GameConfig.Battle;
using GameLogic.Battle;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>
/// 中毒按秒推进、刷新与清理，不随渲染帧率改变次数。
/// </summary>
public sealed class PoisonTests
{
    /// <summary>
    /// 不同物理步长均产生十次固定伤害，状态随后结束。
    /// </summary>
    /// <param name="fps">每秒推进次数。</param>
    [Theory]
    [InlineData(24)]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(144)]
    public void Poison_UsesTenSecondsAtEveryFrameRate(int fps)
    {
        var scope = new TestGameScope(1, [(1, 0)], 0, 25);
        HeroStatusSystem system = scope.GetSystem<HeroStatusSystem>();
        system.ApplyPoison(TestTables.Tables.TbAttackConfig.Get(TestTables.ConfigIds.GetAttackId(GameConfig.Entity.EntityId.LionCamelKing, 3, 2)));
        int damage = 0;
        for (int i = 0; i < 10 * fps; i++)
        {
            damage += system.AdvancePoison(1f / fps);
        }

        Assert.Equal(200, damage);
        Assert.False(system.IsPoisoned);
        Assert.Equal(0, system.AdvancePoison(20));
    }

    /// <summary>
    /// 刷新不重置下一跳，不叠加；死亡后清理并拒绝重新施加。
    /// </summary>
    [Fact]
    public void Refresh_KeepsTickPhaseAndDeathClearsPoison()
    {
        var scope = new TestGameScope(1, [(1, 0)], 0, 25);
        HeroStatusSystem system = scope.GetSystem<HeroStatusSystem>();
        HeroSystem heroSystem = scope.GetSystem<HeroSystem>();
        AttackConfig attack = TestTables.Tables.TbAttackConfig.Get(TestTables.ConfigIds.GetAttackId(GameConfig.Entity.EntityId.LionCamelKing, 3, 2));
        system.ApplyPoison(attack);
        Assert.Equal(0, system.AdvancePoison(.75f));
        system.ApplyPoison(attack);
        Assert.Equal(20, system.AdvancePoison(.25f));
        Assert.Equal(0, system.AdvancePoison(0));
        Assert.Equal(40, system.AdvancePoison(2));
        Assert.True(heroSystem.Damage(int.MaxValue));
        Assert.False(system.IsPoisoned);
        system.ApplyPoison(attack);
        Assert.False(system.IsPoisoned);
        Assert.Equal(0, system.AdvancePoison(100));
    }
}
