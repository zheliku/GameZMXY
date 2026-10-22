using GameLogic.Battle;
using System;
using GameConfig.Skills;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Heroes.Body;
using GameLogic.Skills;
using Godot;

namespace GameLogic.Heroes.Wukong;

/// <summary>火魔斩：起飞、寻敌九连斩、下冲与落地；原生标记切阶段，状态负责分支和物理。</summary>
public sealed class WukongFireDemonState : AbstractHeroSkillState
{
    private const string AnimationName = "skills/fire_demon_slash"; // 本招的原生动画资源名。

    private const string DescentMarker = "descent"; // 无目标时进入的动画标记。
    private const string RecoveryMarker = "recovery"; // 落地效果完成后进入的动画标记。

    private IFsm<IHeroBody> m_Fsm; // 由当前状态机和进入序号共同拒绝旧效果通知。
    private double m_ComboUntil; // 九连斩期间本体停留的游戏时刻，不另建动画游标。
    private string m_NextMarker; // 回调返回后要进入的原生动画阶段。
    private PackedScene m_LandingScene; // 落地窗口借用动画给出的场景。
    private bool m_LandingSpawned; // 同一次施放只创建一次落地斩。

    /// <inheritdoc />
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
    {
        base.Enter(fsm, body);
        m_Fsm = fsm;
        m_ComboUntil = 0;
        m_NextMarker = null;
        m_LandingScene = null;
        m_LandingSpawned = false;
        body.ActionData.LastGroundSegment = -1;
        GravitySuspended = true;
        body.ActionData.InputLocked = true;
        body.GetSystem<SkillSystem>().GrantInvulnerability(body.ActionData.Time,
            body.GetModel<ConfigurationModel>().Tables.TbSkillConfig.Get(SkillId.FireDemonSlash).InvincibleDurationSeconds);
        BeginAnimation(body, AnimationName);
    }

    /// <inheritdoc />
    protected override void Leave(IFsm<IHeroBody> fsm, IHeroBody body, bool isShutdown)
    {
        body.ActionData.InputLocked = false;
        m_Fsm = null;
        base.Leave(fsm, body, isShutdown);
    }

    /// <summary>前奏固定本体；原地视觉由悟空场景自身的动画节点播放。</summary>
    /// <param name="body">悟空身体。</param>
    public void BeginPrelude(IHeroBody body) => body.Velocity = Vector2.Zero;

    /// <summary>动画发起升空运动。</summary>
    /// <param name="body">悟空身体。</param>
    /// <param name="velocity">朝右时的速度，px/s。</param>
    public void Launch(IHeroBody body, Vector2 velocity) =>
        body.Velocity = new Vector2(velocity.X * ((ISkillActor)body).Facing, velocity.Y);

    /// <summary>有目标时瞬移并等待九连斩，没有则进入原生下降标记。</summary>
    /// <param name="body">悟空身体。</param>
    /// <param name="combo">九连斩场景。</param>
    /// <param name="range">寻敌距离，px。</param>
    /// <param name="inset">视口边距，px。</param>
    /// <param name="holdSeconds">本体停留秒数。</param>
    public void SearchTarget(IHeroBody body, PackedScene combo, float range, float inset, double holdSeconds)
    {
        body.Velocity = Vector2.Zero;
        var actor = (ISkillActor)body;
        if (actor.FindSkillTarget(range, inset) is not { } target)
        {
            m_NextMarker = DescentMarker;
            return;
        }
        actor.SkillPosition = target;
        actor.SpawnSkillEffect(combo, target, SkillHitId.FireDemonCombo,
            FireDemonPower(body, SkillHitId.FireDemonCombo), knockbackOriginX: target.X);
        AnimationPaused = true;
        m_ComboUntil = body.ActionData.Time + holdSeconds;
    }

    /// <summary>下冲阶段继续暂停重力，使用动画提供的速度与效果。</summary>
    /// <param name="body">悟空身体。</param>
    /// <param name="descent">下冲场景。</param>
    /// <param name="velocity">朝右时的下冲速度，px/s。</param>
    public void Descend(IHeroBody body, PackedScene descent, Vector2 velocity)
    {
        AnimationPaused = false;
        Launch(body, velocity);
        var actor = (ISkillActor)body;
        actor.SpawnSkillEffect(descent, actor.SkillPosition, SkillHitId.FireDemonDescent,
            FireDemonPower(body, SkillHitId.FireDemonDescent), knockbackOriginX: actor.SkillPosition.X);
    }

    /// <summary>开启实际落地检测；不在动画配置中解释条件。</summary>
    /// <param name="landing">窗口内落地时创建的场景。</param>
    public void OpenLandingWindow(PackedScene landing) => m_LandingScene = landing;

    /// <summary>关闭提前落地窗口，末段仍会补齐一次落地效果。</summary>
    public void CloseLandingWindow() => m_LandingScene = null;

    /// <summary>恢复段补齐尚未出现的落地斩，并恢复身体物理与收招。</summary>
    /// <param name="body">悟空身体。</param>
    /// <param name="landing">末段保底效果。</param>
    public void Recover(IHeroBody body, PackedScene landing)
    {
        if (!m_LandingSpawned)
            SpawnLanding(body, landing, false);
        GravitySuspended = AnimationPaused = false;
        CompleteAnimation();
    }

    /// <inheritdoc />
    protected override void UpdateSkill(IHeroBody body, float dt)
    {
        if (m_ComboUntil > 0 && body.ActionData.Time >= m_ComboUntil)
        {
            m_ComboUntil = 0;
            AnimationPaused = false;
        }
        if (m_LandingScene != null && body.OnFloor)
            SpawnLanding(body, m_LandingScene, true);
    }

    /// <inheritdoc />
    protected override void AfterAnimation(IHeroBody body)
    {
        // 阶段方法可能刚刚开启落地窗口，必须同帧检查地面事实。
        if (m_LandingScene != null && body.OnFloor)
            SpawnLanding(body, m_LandingScene, true);
        if (m_NextMarker == null)
            return;
        string marker = m_NextMarker;
        m_NextMarker = null;
        ((ISkillActor)body).BeginSkillAnimation(AnimationName, marker);
    }

    /// <summary>落地效果独立播放，完成通知只允许恢复同一次施放。</summary>
    /// <param name="body">悟空身体。</param>
    /// <param name="scene">落地场景。</param>
    /// <param name="wait">是否等待效果完成后进入恢复段。</param>
    private void SpawnLanding(IHeroBody body, PackedScene scene, bool wait)
    {
        m_LandingScene = null;
        m_LandingSpawned = true;
        AnimationPaused = wait;
        body.Velocity = Vector2.Zero;
        var actor = (ISkillActor)body;
        var fsm = m_Fsm;
        int serial = EnterSerial;
        Action completed = null;
        if (wait)
            completed = () =>
            {
                if (m_Fsm != fsm || fsm.CurrentState != this || EnterSerial != serial ||
                    body.GetModel<HeroModel>().ActiveSkillId != SkillId.FireDemonSlash)
                    return;
                AnimationPaused = false;
                m_NextMarker = RecoveryMarker;
            };
        actor.SpawnSkillEffect(scene, actor.SkillPosition, SkillHitId.FireDemonLanding,
            FireDemonPower(body, SkillHitId.FireDemonLanding), completed, actor.SkillPosition.X);
    }

    /// <summary>火魔各段遵循原版先对基础攻击取整，再乘该段倍率的顺序。</summary>
    /// <param name="body">悟空身体。</param>
    /// <param name="hit">当前伤害阶段。</param>
    /// <returns>无双加成之前的威力。</returns>
    private static double FireDemonPower(IHeroBody body, SkillHitId hit)
    {
        var definition = body.GetModel<ConfigurationModel>().Tables.TbAttackConfig.Get((int)hit);
        return Math.Round((double)body.GetModel<GameLogic.Battle.HeroModel>().Power.Value,
            MidpointRounding.AwayFromZero) * definition.PowerScale.X + definition.FlatPower.X;
    }
}
