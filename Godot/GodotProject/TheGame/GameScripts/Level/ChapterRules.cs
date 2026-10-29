namespace GameLogic.Level
{
    /// <summary>
    /// 蝙蝠洞石像决定出口开放，不等待其召唤的蝙蝠清空。
    /// </summary>
    internal static class BatCaveRules
    {
        /// <summary>
        /// 蝙蝠洞的稳定关卡编号。
        /// </summary>
        internal const int LevelId = 4;

        /// <summary>
        /// 末波唯一石像对应的生成点。
        /// </summary>
        internal const string StatueSpawnPointId = "stage_5_d";
    }

    /// <summary>
    /// 水帘洞隐藏龙宫的导航与主线进度边界。
    /// </summary>
    internal static class DragonPalaceRules
    {
        /// <summary>
        /// 隐藏龙宫的稳定关卡编号。
        /// </summary>
        internal const int LevelId = 11;

        /// <summary>
        /// 水下入口所属的水帘洞关卡。
        /// </summary>
        internal const int EntranceLevelId = 2;
    }
}
