using GameLogic.Heroes.Wukong;
using Godot;

namespace GameLogic.Heroes;

/// <summary>悟空原生动画的方法入口；只连接当前具体状态，招式规则留在对应状态。</summary>
public partial class WukongEntity
{
    /// <summary>升龙起手判定。</summary>
    private void BeginRisingDragonHitAnimEvent() => (CurrentSkillState as WukongRisingDragonState)?.BeginHit(this);

    /// <summary>突刺起手。</summary>
    /// <param name="speed">普通速度，px/s。</param>
    /// <param name="musouSpeed">无双速度，px/s。</param>
    private void BeginFlameThrustAnimEvent(float speed, float musouSpeed) =>
        (CurrentSkillState as WukongFlameThrustState)?.BeginThrust(this, speed, musouSpeed);

    /// <summary>突刺后续段。</summary>
    /// <param name="multiplier">横速倍率。</param>
    private void ContinueFlameThrustAnimEvent(float multiplier) =>
        (CurrentSkillState as WukongFlameThrustState)?.ContinueThrust(this, multiplier);

    /// <summary>烈焰闪起手判定。</summary>
    private void BeginBlazingFlashHitAnimEvent() => (CurrentSkillState as WukongBlazingFlashState)?.BeginHit(this);

    /// <summary>烈焰闪进入闪身。</summary>
    /// <param name="trail">动画直接引用的场景。</param>
    /// <param name="speed">速度，px/s。</param>
    private void StartBlazingFlashDashAnimEvent(PackedScene trail, float speed) =>
        (CurrentSkillState as WukongBlazingFlashState)?.StartDash(this, trail, speed);

    /// <summary>烈焰闪停止移动。</summary>
    private void StopBlazingFlashAnimEvent() => (CurrentSkillState as WukongBlazingFlashState)?.StopDash(this);

    /// <summary>烈焰风暴新段判定。</summary>
    private void BeginFlameStormHitAnimEvent() => (CurrentSkillState as WukongFlameStormState)?.BeginHit(this);

    /// <summary>火魔前奏。</summary>
    private void BeginFireDemonAnimEvent()
    {
        if (CurrentSkillState is not WukongFireDemonState state)
            return;
        state.BeginPrelude(this);
        // TopLevel 保持出招原地，不跟随升空或瞬移；动画完成由场景信号隐藏。
        m_FireDemonPrelude.GlobalPosition = GlobalPosition + new Vector2(0, 20);
        m_FireDemonPrelude.Scale = new Vector2(Facing > 0 ? 1 : -1, 1);
        m_FireDemonPrelude.Stop();
        m_FireDemonPrelude.Frame = 0;
        m_FireDemonPrelude.Show();
        m_FireDemonPrelude.Play();
    }

    /// <summary>火魔升空。</summary>
    /// <param name="velocity">朝右时的速度，px/s。</param>
    private void LaunchFireDemonAnimEvent(Vector2 velocity) => (CurrentSkillState as WukongFireDemonState)?.Launch(this, velocity);

    /// <summary>火魔寻敌并决定九连斩或直接下冲。</summary>
    /// <param name="combo">九连斩场景。</param>
    /// <param name="range">范围，px。</param>
    /// <param name="inset">视口边距，px。</param>
    /// <param name="holdSeconds">本体停留秒数。</param>
    private void SearchFireDemonTargetAnimEvent(PackedScene combo, float range, float inset, double holdSeconds) =>
        (CurrentSkillState as WukongFireDemonState)?.SearchTarget(this, combo, range, inset, holdSeconds);

    /// <summary>火魔下冲。</summary>
    /// <param name="scene">下冲效果。</param>
    /// <param name="velocity">朝右时的速度，px/s。</param>
    private void DescendFireDemonAnimEvent(PackedScene scene, Vector2 velocity) =>
        (CurrentSkillState as WukongFireDemonState)?.Descend(this, scene, velocity);

    /// <summary>火魔开启落地窗口。</summary>
    /// <param name="scene">落地效果。</param>
    private void OpenFireDemonLandingAnimEvent(PackedScene scene) =>
        (CurrentSkillState as WukongFireDemonState)?.OpenLandingWindow(scene);

    /// <summary>火魔关闭提前落地窗口。</summary>
    private void CloseFireDemonLandingAnimEvent() => (CurrentSkillState as WukongFireDemonState)?.CloseLandingWindow();

    /// <summary>火魔进入恢复段。</summary>
    /// <param name="scene">末段保底落地效果。</param>
    private void RecoverFireDemonAnimEvent(PackedScene scene) =>
        (CurrentSkillState as WukongFireDemonState)?.Recover(this, scene);
}
