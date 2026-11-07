using GameConfig.Equipment;
using GameFramework;
using GameFramework.Event;

namespace GameLogic.Event.Equipment
{
    /// <summary>
    /// 一次直接装备奖励的领取结果；只携带值，应用提示缺席不影响奖励与死亡流程。
    /// </summary>
    public sealed class EquipmentRewardEventArgs : GameEventArgs
    {
        /// <summary>
        /// GGF 事件编号。
        /// </summary>
        public static readonly int EventId = typeof(EquipmentRewardEventArgs).GetHashCode();

        /// <inheritdoc />
        public override int Id => EventId;

        /// <summary>
        /// 本次尝试直接赠送的装备定义。
        /// </summary>
        public EEquipmentId EquipmentId { get; private set; }

        /// <summary>
        /// 是否已加入背包；false 表示背包满而未创建实例。
        /// </summary>
        public bool Collected { get; private set; }

        /// <summary>
        /// 取得一次值通知，由框架在分发后回收。
        /// </summary>
        /// <param name="equipmentId">本次奖励的定义。</param>
        /// <param name="collected">本次是否领取成功。</param>
        /// <returns>仅在本次分发期间有效的参数。</returns>
        public static EquipmentRewardEventArgs Create(EEquipmentId equipmentId, bool collected)
        {
            var args = ReferencePool.Acquire<EquipmentRewardEventArgs>();
            args.EquipmentId = equipmentId;
            args.Collected = collected;
            return args;
        }

        /// <summary>
        /// 回池前清除本次奖励值。
        /// </summary>
        public override void Clear()
        {
            EquipmentId = EEquipmentId.None;
            Collected = false;
        }
    }
}
