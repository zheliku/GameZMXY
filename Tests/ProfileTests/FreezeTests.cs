using System;
using GameLogic.Architecture;
using GameLogic.Battle;
using Xunit;

namespace GameLogic.Profile.Tests
{
    /// <summary>
    /// 冰冻控制状态的秒制、刷新、暂停和本局生命周期检查。
    /// </summary>
    public sealed class FreezeTests
    {
        /// <summary>
        /// 两档官方持续时间在不同物理频率下按秒结束，不受渲染帧数影响。
        /// </summary>
        /// <param name="frequency">每秒游戏步数。</param>
        /// <param name="seconds">原版普通或高等级龙王的冰冻时长。</param>
        [Theory]
        [InlineData(24, 2)]
        [InlineData(60, 2)]
        [InlineData(120, 2)]
        [InlineData(144, 5)]
        public void Freeze_ExpiresInGameSeconds(int frequency, int seconds)
        {
            var scope = new TestGameScope(1, [(1, 0)], 0, 25);
            var system = scope.GetSystem<HeroSystem>();
            system.ApplyFreeze(seconds);
            for (int step = 0; step < seconds * frequency - 1; step++)
            {
                system.AdvanceFreeze(1f / frequency);
            }

            Assert.True(scope.Hero.FreezeSeconds > 0);
            system.AdvanceFreeze(1f / frequency);
            Assert.Equal(0, scope.Hero.FreezeSeconds);
        }

        /// <summary>
        /// 暂停与非法步长不推进，刷新不缩短控制；死亡与重建清理旧状态。
        /// </summary>
        [Fact]
        public void Freeze_RefreshesPausesAndClearsWithSession()
        {
            var scope = new TestGameScope(1, [(1, 0)], 0, 25);
            var system = scope.GetSystem<HeroSystem>();
            system.ApplyFreeze(5);
            scope.Hero.Ws.Value = scope.Hero.MaxWs.Value;
            Assert.False(system.TryActivateWs());
            Assert.False(scope.Hero.WsActive.Value);
            system.AdvanceFreeze(1);
            system.ApplyFreeze(2);
            system.AdvanceFreeze(0);
            system.AdvanceFreeze(float.NaN);
            Assert.Equal(4, scope.Hero.FreezeSeconds);
            Assert.Throws<ArgumentOutOfRangeException>(() => system.ApplyFreeze(float.PositiveInfinity));
            system.Damage(int.MaxValue);
            Assert.Equal(0, scope.Hero.FreezeSeconds);
            system.ApplyFreeze(2);
            Assert.Equal(0, scope.Hero.FreezeSeconds);
            system.Reset(1, 1, TestTables.Tables.TbHeroGrowthConfig.Get(1).Levels[0].BaseAttributes);
            system.ApplyFreeze(2);
            system.Reset(1, 1, TestTables.Tables.TbHeroGrowthConfig.Get(1).Levels[0].BaseAttributes);
            Assert.Equal(0, scope.Hero.FreezeSeconds);
        }
    }
}
