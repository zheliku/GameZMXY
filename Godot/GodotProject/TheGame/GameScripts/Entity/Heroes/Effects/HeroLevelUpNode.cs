using Godot;
using GameLogic.Heroes.Body;

namespace GameLogic.Heroes.Effects;

/// <summary>英雄自有的升级动画层，不占用身体、武器或攻击动画播放器。</summary>
public partial class HeroLevelUpNode : AnimatedSprite2D
{
    /// <summary>首次装配时绑定结束回调；默认不播放。</summary>
    public override void _Ready()
    {
        AnimationFinished += Reset;
        Reset();
    }

    /// <summary>每次实际升级立即从第一帧播放，多级连升重启同一个提示。</summary>
    public void ShowLevelUp()
    {
        Stop();
        Frame = 0;
        Visible = true;
        Play(HeroAnims.LevelUp);
    }

    /// <summary>动画结束或英雄回池时停止并隐藏，防止下一次显示残留。</summary>
    public void Reset()
    {
        Stop();
        Visible = false;
    }
}
