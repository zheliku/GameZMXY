using GameLogic.Heroes;
namespace GameLogic.Heroes.Body
{
    /// <summary>
    /// 英雄身体参数（值快照）：HeroEntity 从 HeroConfig / AttackConfig / 动画库拷入，身体状态只读这里。
    /// 纯 C#——状态不直接依赖 Luban 生成类，单测用对象初始化器直接构造。
    /// </summary>
    public readonly struct HeroBodyParams
    {
        /// <summary>慢走速度 px/s（HeroConfig.WalkSpeed）</summary>
        public float WalkSpeed { get; init; }

        /// <summary>跑步速度 px/s（HeroConfig.RunSpeed）</summary>
        public float RunSpeed { get; init; }

        /// <summary>起跳速度 px/s，向上为正（HeroConfig.JumpSpeed）</summary>
        public float JumpSpeed { get; init; }

        /// <summary>重力 px/s²（HeroConfig.Gravity）</summary>
        public float Gravity { get; init; }

        /// <summary>最多跳跃次数（含地面一段，HeroConfig.JumpCountMax）</summary>
        public int JumpCountMax { get; init; }

        /// <summary>受击硬直秒（= hurt 动画长度；0 = 不硬直）</summary>
        public float HurtTime { get; init; }

        /// <summary>普攻连段每段的动画名（下标 = 段序号，AttackConfig.Animation 按 ComboIndex 排序）</summary>
        public string[] AttackAnims { get; init; }

        /// <summary>普攻连段每段的时长秒（= 段动画长度，OnInit 读动画资源；状态计时）</summary>
        public float[] AttackTimes { get; init; }

        /// <summary>按上一次地面段号选取本次起手后的输入冷却，单位秒；沿用 Role1.normalHit 的赋值顺序。</summary>
        public float[] AttackInputCooldowns { get; init; }

        /// <summary>空中普攻起手后的输入冷却，单位秒。</summary>
        public float AirAttackCooldown { get; init; }

        /// <summary>两次普攻尝试超过此间隔后回到第一段，单位秒。</summary>
        public float ComboResetTime { get; init; }

        /// <summary>空中起手使用的普攻段下标，由具体英雄指定。</summary>
        public int AirAttackIndex { get; init; }

        /// <summary>普攻段数</summary>
        public int ComboLength => AttackAnims?.Length ?? 0;
    }
}
