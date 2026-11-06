using GameConfig.Battle;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Damage;
using GameFramework;
using Godot;
using Xunit;

namespace GameLogic.Profile.Tests
{
    /// <summary>
    /// 官方英雄各招式的无双基准与本体/离身结算差异。
    /// </summary>
    public sealed class MusouTests
    {
        /// <summary>
        /// 覆盖双方普攻、全部伤害技能及冰龙减伤/暴击，避免小威力多段命中被总攻击力分母吞掉。
        /// </summary>
        /// <param name="attackId">正式攻击身份。</param>
        /// <param name="power">本招冻结威力；角色攻击力固定为100。</param>
        /// <param name="defense">目标物防；离身攻击按减伤前伤害增长。</param>
        /// <param name="crit">本次是否触发暴击。</param>
        /// <param name="effect">是否离身攻击。</param>
        /// <param name="expected">官方无双增长。</param>
        [Theory]
        [InlineData(1001, 104, 0, false, false, 2)]
        [InlineData(1011, 50, 0, false, false, 1)]
        [InlineData(1021, 100, 0, false, false, 2)]
        [InlineData(1031, 200, 0, false, false, 2)]
        [InlineData(1041, 120, 1000, false, true, 2)]
        [InlineData(1051, 100, 0, false, false, 4)]
        [InlineData(1052, 300, 0, false, false, 2)]
        [InlineData(1053, 100, 1000, false, true, 2)]
        [InlineData(2001, 104, 1000, false, true, 2)]
        [InlineData(2011, 20, 1000, false, true, 2)]
        [InlineData(2011, 20, 1000, true, true, 4)]
        [InlineData(2021, 80, 1000, false, true, 2)]
        [InlineData(2051, 200, 1000, false, true, 2)]
        [InlineData(2052, 800, 1000, false, true, 2)]
        public void Hit_UsesOfficialStagePower(int attackId, int power, int defense, bool crit,
            bool effect, int expected)
        {
            var scope = new TestGameScope(1, [(1, 0)], 0, 25);
            var system = scope.GetSystem<HeroSystem>();
            var attacker = new CombatantAttributes(CombatSide.Hero, 1, 100, 0, 0, crit ? 1 : 0, 0, 0, 0, 0, 0, 0, 0);
            var attack = AttackData.Create(attackId, attacker, power, DamageKind.Physics, Vector2.Zero, 1, 0,
                isEffect: effect);
            try
            {
                var defender = new CombatantAttributes(CombatSide.Enemy, 1, 0, defense, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                var result = new CombatSystem(scope).Calculate(attack, defender, 1, 0);
                Assert.Equal(expected, result.AttackerWsGain);
                system.RecordHit(result);
                Assert.Equal(expected, scope.Hero.Ws.Value);
                system.RecordHit(DamageResult.Missed(attack.Kind));
                Assert.Equal(expected, scope.Hero.Ws.Value);
                scope.Hero.Ws.Value = scope.Hero.MaxWs.Value;
                Assert.True(system.TryActivateWs());
                system.RecordHit(result);
                Assert.Equal(scope.Hero.MaxWs.Value, scope.Hero.Ws.Value);
            }
            finally
            {
                ReferencePool.Release(attack);
            }
        }
    }
}
