using System;
using Godot;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 花果山大猩猩守关数值；由物种场景引用，不加入通用敌人表。
    /// </summary>
    [Tool]
    public partial class GorillaGuardianConfigResource : Resource
    {
        /// <summary>
        /// 花果山守关时的生命上限。
        /// </summary>
        [Export] public int MaxHp { get; set; }

        /// <summary>
        /// 花果山守关时的击败经验。
        /// </summary>
        [Export] public int Experience { get; set; }

        /// <summary>
        /// 在实体初始化边界拒绝不完整数值。
        /// </summary>
        /// <exception cref="InvalidOperationException">生命非正数或经验为负。</exception>
        public void Validate()
        {
            if (MaxHp <= 0 || Experience < 0)
            {
                throw new InvalidOperationException("花果山大猩猩配置需要正生命和非负经验。");
            }
        }
    }
}
