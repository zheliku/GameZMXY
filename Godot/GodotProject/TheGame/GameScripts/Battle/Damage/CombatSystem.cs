using GameLogic.Battle.Attacks;
using GameLogic.Architecture;
using GameLogic.Config;
using System;
using GameConfig.Battle;
using Godot;

namespace GameLogic.Battle.Damage
{
    /// <summary>战斗结算系统：查询共享配置，确定性计算伤害、击退和命中无双增量；不持有实体。</summary>
    public sealed class CombatSystem : AbstractSystem
    {
        private const double RatioStep = 0.001; // 比率按旧项目规则保留三位小数。

        /// <summary>
        /// 取整容差：表里的系数是 float（0.05f 转 double 为 0.0500000007…），100×(1−5×0.05) 会算成
        /// 74.9999996 被截成 74；旧项目用 double 字面量不存在该误差。取整前加一个远小于 1 点伤害的容差对齐。
        /// </summary>
        private const double TruncateEpsilon = 1e-6; // 抵消 float 配置转 double 后在取整边界产生的微小误差。

        /// <summary>绑定业务系统的所属架构。</summary>
        /// <param name="architecture">所属游戏入口。</param>
        public CombatSystem(IArchitecture architecture) : base(architecture) { }

        /// <summary>
        /// 结算一次命中。
        /// </summary>
        /// <param name="data">攻击包（含攻击方快照与本招威力）</param>
        /// <param name="defender">防守方属性快照</param>
        /// <param name="missRoll">闪避判定随机数 [0,1)（由 Godot 层取好传入，Battle/ 不自取随机）</param>
        /// <param name="critRoll">暴击判定随机数 [0,1)（由 Godot 层取好传入）</param>
        /// <returns>本次命中的伤害、闪避、暴击和伤害类型结果。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="data"/> 为 <see langword="null"/>。</exception>
        public DamageResult Calculate(AttackData data, in CombatantAttributes defender,
            float missRoll, float critRoll)
        {
            var config = this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.Data;

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            CombatantAttributes attacker = data.Attacker;
            bool heroDefends = defender.Side == CombatSide.Hero;

            // 1. 净值（下限 0）
            double miss = Math.Clamp(defender.Miss - attacker.Htarget, 0f, 1f);
            double crit = Math.Clamp(attacker.Crit - defender.CritReduce, 0f, 1f);
            double lucky = Math.Max(0, attacker.Lucky - defender.Toughness);
            double def = Math.Max(0, defender.Def - attacker.Ar);
            double mdef = Math.Max(0, defender.Mdef - attacker.Sp);

            // 2. 等级压制：sign = +1 攻击方等级高，-1 防守方等级高，0 同级
            int levelGap = attacker.Level - defender.Level;
            int d = Math.Abs(levelGap);
            int sign = Math.Sign(levelGap);

            int damageCapLv = heroDefends ? config.LvDamageCapLvHeroDef : config.LvDamageCapLvMonsterDef;

            double luckyBl = Math.Min(d * (double)config.LvLuckyCoef, config.LvLuckyCap);
            double damageBl = Math.Min(d, damageCapLv) * (double)config.LvDamageCoef;

            lucky *= 1 + sign * luckyBl;
            double hurt = Truncate(data.Power * (1 + sign * damageBl));

            // 3. 造一直接概率：不再按属性点曲线或等级差二次换算。
            if (missRoll < miss)
            {
                return DamageResult.Missed(data.Kind);
            }

            // 4. 暴击
            bool isCrit = critRoll < crit;
            if (isCrit)
            {
                double luckyK = heroDefends ? config.LuckyKMonsterAtk : config.LuckyKHeroAtk;
                hurt *= config.CritBase + CalculateAttributeRatio(lucky, luckyK);
            }

            // 5. 减伤
            switch (data.Kind)
            {
                case DamageKind.Physics:
                    hurt *= 1 - CalculateAttributeRatio(def, heroDefends ? config.DefKHero : config.DefKMonster);
                    break;
                case DamageKind.Magic:
                    hurt *= 1 - CalculateAttributeRatio(mdef, heroDefends ? config.MdefKHero : config.MdefKMonster);
                    break;
            }

            int damage = (int)Math.Max(0, Truncate(hurt));
            return new DamageResult(damage, false, isCrit, data.Kind);
        }

        /// <summary>
        /// 击退速度（px/s）：数值表已经使用秒制，横向按出招方向，纵向向上为负。
        /// 中心型攻击在命中时按目标位置决定左右。
        /// </summary>
        /// <param name="data">包含出招方向和击退参数的攻击包。</param>
        /// <param name="targetX">受击方世界横坐标。</param>
        /// <returns>世界坐标系下的击退速度（px/s）。</returns>
        public Vector2 KnockbackVelocity(AttackData data, float targetX = 0f)
        {
            int direction = data.KnockbackOriginX is float origin ? (origin > targetX ? -1 : 1) : data.Direction;
            return new Vector2(direction * data.Knockback.X, data.Knockback.Y);
        }

        /// <summary>向零取整（旧 int()），带 float 系数容差（见 <see cref="TruncateEpsilon"/>）。</summary>
        /// <param name="value">待取整数值。</param>
        /// <returns>向零截断后的值。</returns>
        private static double Truncate(double value) // 带浮点容差地向零取整，保持旧伤害公式顺序。
        {
            return value >= 0 ? Math.Floor(value + TruncateEpsilon) : Math.Ceiling(value - TruncateEpsilon);
        }

        /// <summary>x/(x+K)，保留三位小数（旧项目 snapped(x, 0.001)）；x、K 均为 0 时视为 0。
        /// Godot 4 的 Mathf.Snapped 同为 floor(x/step+0.5)*step；x≥0 域内二者一致。</summary>
        /// <param name="x">非负净属性值。</param>
        /// <param name="k">非负曲线常数。</param>
        /// <returns>按 <see cref="RatioStep"/> 取整的比率。</returns>
        public static double CalculateAttributeRatio(double x, double k)
        {
            double denominator = x + k;
            if (denominator <= 0)
            {
                return 0;
            }

            // 属性与曲线常数均非负，按旧项目精度对最终比率取整。
            return Mathf.Snapped(x / denominator, RatioStep);
        }
    }
}
