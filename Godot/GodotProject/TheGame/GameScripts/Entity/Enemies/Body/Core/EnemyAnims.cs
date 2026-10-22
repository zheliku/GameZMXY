namespace GameLogic.Enemies.Body
{
    /// <summary>
    /// 敌人标准动画名，小怪和首领共用。
    /// 具体攻击动画由实体声明，保存在 <see cref="EnemyBodyParams.AttackAnims"/>。
    /// </summary>
    public static class EnemyAnims
    {
        /// <summary>待机动画名。</summary>
        public const string Idle = "idle";

        /// <summary>奔跑动画名。</summary>
        public const string Run = "run";

        /// <summary>受击动画名。</summary>
        public const string Hurt = "hurt";

        /// <summary>死亡动画名。</summary>
        public const string Death = "death";
    }
}
