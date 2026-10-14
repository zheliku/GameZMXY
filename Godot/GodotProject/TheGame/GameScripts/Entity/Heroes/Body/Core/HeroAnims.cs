namespace GameLogic.Heroes.Body
{
    /// <summary>英雄共用的身体与提示动画名；普攻动画名由 AttackConfig.Animation 提供。</summary>
    public static class HeroAnims
    {
        /// <summary>基础待机动画名。</summary>
        public const string Idle = "idle1";

        /// <summary>行走动画名。</summary>
        public const string Walk = "walk";

        /// <summary>跑步动画名。</summary>
        public const string Run = "run";

        /// <summary>首次跳跃动画名。</summary>
        public const string Jump = "jump";

        /// <summary>二段跳动画名。</summary>
        public const string Jump2 = "jump_2";

        /// <summary>下落动画名。</summary>
        public const string Fall = "fall";

        /// <summary>受击动画名。</summary>
        public const string Hurt = "hurt";

        /// <summary>死亡动画名。</summary>
        public const string Death = "death";

        /// <summary>独立升级特效动画名。</summary>
        public const string LevelUp = "level_up";

        /// <summary>屏幕空间无双开启提示动画名。</summary>
        public const string MusouActivation = "musou_activation";
    }
}
