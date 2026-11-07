using GameConfig.Entity;
using GameConfig.Equipment;
using GameConfig.Loot;
using GameLogic.Architecture;

namespace GameLogic.Config
{
    /// <summary>
    /// 集中配置编号的编码和范围判断；不分配身份，也不替代表中的稳定枚举。
    /// </summary>
    public sealed class ConfigIdUtility : IUtility
    {
        private const int FirstHeroId = 1; // 英雄编号起点，0保留给None。
        private const int FirstMonsterId = 10000; // 小怪编号起点，也是英雄区间的上界。
        private const int FirstBossId = 100000; // 首领编号起点，也是小怪区间的上界。
        private const int EnemyIdLimit = 200000; // 敌人区间的排他上界。
        private const int AttackOwnerStride = 1000; // 每个实体预留的攻击编号数量。
        private const int AttackActionStride = 10; // 每个动作预留的攻击段数，0不作有效段号。
        private const int EnvironmentAttackBase = 900000000; // 无实体来源的环境攻击独立编号区。
        private const int CommonDropPoolBase = 1000; // 通用品质掉落池起点。
        private const int DropPoolStride = 100; // 每个品质或首领预留的掉落池变体数量。

        /// <summary>
        /// 判断实体身份是否位于英雄预留区间，不检查该身份是否已在表中登记。
        /// </summary>
        /// <param name="entityId">实体配置身份。</param>
        /// <returns>是否属于英雄编号区间。</returns>
        public bool IsHero(EEntityId entityId)
        {
            return (int)entityId is >= FirstHeroId and < FirstMonsterId;
        }

        /// <summary>
        /// 判断实体身份是否位于小怪预留区间，不依赖其身体状态或守关职责。
        /// </summary>
        /// <param name="entityId">实体配置身份。</param>
        /// <returns>是否属于小怪编号区间。</returns>
        public bool IsMonster(EEntityId entityId)
        {
            return (int)entityId is >= FirstMonsterId and < FirstBossId;
        }

        /// <summary>
        /// 判断实体身份是否位于首领预留区间，不检查该身份是否已在表中登记。
        /// </summary>
        /// <param name="entityId">实体配置身份。</param>
        /// <returns>是否属于首领编号区间。</returns>
        public bool IsBoss(EEntityId entityId)
        {
            return (int)entityId is >= FirstBossId and < EnemyIdLimit;
        }

        /// <summary>
        /// 取得指定实体、动作和段号对应的攻击配置编号。
        /// </summary>
        /// <param name="ownerId">攻击所属的非None实体身份。</param>
        /// <param name="action">动作序号0-99，0保留给英雄普攻，其他按技能面板或敌人招式排序。</param>
        /// <param name="segment">同一动作内的段号1-9；离身效果、持续状态或数值档使用后续段。</param>
        /// <returns>统一攻击表的主键；调用方提供约定范围内的参数，表记录由启动校验确认。</returns>
        public int GetAttackId(EEntityId ownerId, int action, int segment = 1)
        {
            return (int)ownerId * AttackOwnerStride + action * AttackActionStride + segment;
        }

        /// <summary>
        /// 判断攻击编号是否与所属实体及有效段号一致，None仅接受环境攻击区。
        /// </summary>
        /// <param name="attackId">待校验的攻击配置编号。</param>
        /// <param name="ownerId">攻击表声明的所属实体身份。</param>
        /// <returns>编号是否符合该所属身份的编码约定。</returns>
        public bool IsAttackIdForOwner(int attackId, EEntityId ownerId)
        {
            return ownerId == EEntityId.None
                ? attackId >= EnvironmentAttackBase
                : attackId / AttackOwnerStride == (int)ownerId && attackId % AttackActionStride is >= 1 and <= 9;
        }

        /// <summary>
        /// 取得关卡内无实体来源的机关攻击编号。
        /// </summary>
        /// <param name="levelId">正式关卡主键。</param>
        /// <param name="sequence">关卡内机关序号1-999。</param>
        /// <returns>OwnerId为None的统一攻击表主键。</returns>
        public int GetEnvironmentAttackId(int levelId, int sequence)
        {
            return EnvironmentAttackBase + levelId * AttackOwnerStride + sequence;
        }

        /// <summary>
        /// 取得通用品质装备掉落池身份，None不使用此编码。
        /// </summary>
        /// <param name="quality">装备品质。</param>
        /// <param name="variant">品质内变体号0-99，0为普通池。</param>
        /// <returns>对应品质及变体的掉落池身份。</returns>
        public EEquipmentDropPoolId GetCommonEquipmentDropPoolId(EEquipmentQuality quality, int variant = 0)
        {
            return (EEquipmentDropPoolId)(CommonDropPoolBase + (int)quality * DropPoolStride + variant);
        }

        /// <summary>
        /// 取得首领拥有的装备掉落池身份。
        /// </summary>
        /// <param name="bossId">首领实体身份。</param>
        /// <param name="variant">首领内变体号0-99，0为普通池，后续号用于数值档或独立奖励。</param>
        /// <returns>对应首领及变体的掉落池身份。</returns>
        public EEquipmentDropPoolId GetBossEquipmentDropPoolId(EEntityId bossId, int variant = 0)
        {
            return (EEquipmentDropPoolId)((int)bossId * DropPoolStride + variant);
        }
    }
}
