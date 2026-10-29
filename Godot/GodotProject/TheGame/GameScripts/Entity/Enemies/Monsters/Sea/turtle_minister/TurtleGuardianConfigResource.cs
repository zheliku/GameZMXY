using System;
using Godot;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 浅岸龟丞相守关数值；离开浅岸后使用敌人表中的基础值。
    /// </summary>
    [Tool]
    public partial class TurtleGuardianConfigResource : Resource
    {
        /// <summary>
        /// 浅岸守关生命上限。
        /// </summary>
        [Export] public int MaxHp { get; set; }

        /// <summary>
        /// 浅岸守关击败经验。
        /// </summary>
        [Export] public int Experience { get; set; }

        /// <summary>
        /// 浅岸守关每颗灵魂球的数值。
        /// </summary>
        [Export] public int SoulPerOrb { get; set; }

        /// <summary>
        /// 浅岸守关装备掉落概率，0表示不掉落。
        /// </summary>
        [Export(PropertyHint.Range, "0,1")] public float EquipmentDropChance { get; set; }

        /// <summary>
        /// 在实体初始化边界拒绝非法守关数值。
        /// </summary>
        /// <exception cref="InvalidOperationException">生命、奖励或概率不合法。</exception>
        public void Validate()
        {
            if (MaxHp <= 0 || Experience < 0 || SoulPerOrb < 0 ||
                !float.IsFinite(EquipmentDropChance) || EquipmentDropChance is < 0f or > 1f)
            {
                throw new InvalidOperationException("浅岸龟丞相配置的生命、奖励或掉落概率不合法。");
            }
        }
    }
}
