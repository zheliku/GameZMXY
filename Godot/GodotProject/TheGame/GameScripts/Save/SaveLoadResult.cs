using GameLogic.Profile;

namespace GameLogic.Save
{
    /// <summary>读档结果：档案与它来自哪种原始状态。</summary>
    public readonly struct SaveLoadResult
    {
        /// <summary>运行时档案。</summary>
        public readonly PlayerModel PlayerModel;

        /// <summary>存档来源（读取或新建）。</summary>
        public readonly SaveOrigin Origin;

        /// <summary>创建读档结果。</summary>
        /// <param name="playerModel">运行时档案。</param>
        /// <param name="origin">存档来源。</param>
        public SaveLoadResult(PlayerModel playerModel, SaveOrigin origin)
        {
            PlayerModel = playerModel;
            Origin = origin;
        }
    }

    /// <summary>档案加载来源：已有档案或本次新建。</summary>
    public enum SaveOrigin
    {
        /// <summary>从所选存档格读取的已有档案。</summary>
        Loaded,

        /// <summary>本次选中的空格按建档规则创建并写入。</summary>
        Created,
    }
}
