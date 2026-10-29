namespace GameLogic.Enemies
{
    /// <summary>
    /// 地面小怪：装配走动/重力身体，仅在落地时接受攻击请求；小怪AI继承Monster。
    /// </summary>
    public abstract partial class AbstractGroundMonsterEntity : AbstractMonsterEntity
    {
        /// <summary>
        /// 地面攻击必须真实落地，其他请求约束与登记仍由Enemy统一执行。
        /// </summary>
        /// <param name="index">本怪物的招式下标。</param>
        /// <returns>是否接受本次攻击请求。</returns>
        protected override bool TryRequestAttack(int index)
        {
            return OnFloor && base.TryRequestAttack(index);
        }
    }
}
