using System.Collections.Generic;
using System;
using GameLogic.Architecture;
using GameLogic.Battle;
using static GameLogic.Battle.HeroModel;
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
            var system = scope.GetSystem<HeroStatusSystem>();
            system.ApplyFreeze(seconds);
            for (int step = 0; step < seconds * frequency - 1; step++)
            {
                system.AdvanceFreeze(1f / frequency);
            }

            Assert.True(system.IsFrozen);
            system.AdvanceFreeze(1f / frequency);
            Assert.False(system.IsFrozen);
        }

        /// <summary>
        /// 冻结和中毒可并存，离关重建清空两者，其他作用域不受影响。
        /// </summary>
        [Fact]
        public void Effects_CoexistAndClearWithoutCrossingScopes()
        {
            var scope = new TestGameScope(1, [(1, 0)], 0, 25);
            var otherScope = new TestGameScope(1, [(1, 0)], 0, 25);
            var system = scope.GetSystem<HeroStatusSystem>();
            var otherSystem = otherScope.GetSystem<HeroStatusSystem>();
            var poison = TestTables.Tables.TbAttackConfig.Get(TestTables.ConfigIds.GetAttackId(GameConfig.Entity.EEntityId.LionCamelKing, 3, 2));

            // 冻结限制动作但不停止中毒的游戏时钟，各作用域独立推进。
            system.ApplyFreeze(2);
            system.ApplyPoison(poison);
            otherSystem.ApplyFreeze(5);
            system.AdvanceFreeze(1);
            Assert.Equal(20, system.AdvancePoison(1));
            Assert.True(system.IsFrozen && system.IsPoisoned);
            Assert.False(otherSystem.IsPoisoned);

            // 走正式系统的结束与重新初始化路径，旧效果不能进入新一局。
            scope.RestartHero();
            Assert.False(system.IsFrozen || system.IsPoisoned);
            Assert.Equal(0, system.AdvancePoison(100));
            Assert.True(otherSystem.IsFrozen);
            Assert.Equal(5f, otherScope.Hero.StatusEffects.GetValueOrDefault(EStatusEffectKind.Freeze).RemainingSeconds);
            system.ApplyPoison(poison);
            Assert.Equal(20, system.AdvancePoison(1));
        }

        /// <summary>
        /// 暂停与非法步长不推进，刷新不缩短控制；死亡与重建清理旧状态。
        /// </summary>
        [Fact]
        public void Freeze_RefreshesPausesAndClearsWithSession()
        {
            var scope = new TestGameScope(1, [(1, 0)], 0, 25);
            var system = scope.GetSystem<HeroStatusSystem>();
            var heroSystem = scope.GetSystem<HeroSystem>();
            system.ApplyFreeze(5);
            scope.Hero.Ws.Value = scope.Hero.MaxWs.Value;
            Assert.False(heroSystem.TryActivateWs());
            Assert.False(scope.Hero.WsActive.Value);
            system.AdvanceFreeze(1);
            system.ApplyFreeze(2);
            system.AdvanceFreeze(0);
            system.AdvanceFreeze(float.NaN);
            Assert.True(system.IsFrozen);
            Assert.Throws<ArgumentOutOfRangeException>(() => system.ApplyFreeze(float.PositiveInfinity));
            heroSystem.Damage(int.MaxValue);
            Assert.False(system.IsFrozen);
            system.ApplyFreeze(2);
            Assert.False(system.IsFrozen);
            heroSystem.Reset(1, 1, TestTables.Tables.TbHeroGrowthConfig.Get(1).Levels[0].BaseAttributes);
            system.ApplyFreeze(2);
            heroSystem.Reset(1, 1, TestTables.Tables.TbHeroGrowthConfig.Get(1).Levels[0].BaseAttributes);
            Assert.False(system.IsFrozen);
        }
    }
}
