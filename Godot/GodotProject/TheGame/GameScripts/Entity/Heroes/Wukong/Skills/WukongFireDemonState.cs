using System;
using GameConfig.Battle;
using GameConfig.Skills;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Config;
using GameLogic.Heroes.Body;
using GameLogic.Skills;
using Godot;

namespace GameLogic.Heroes.Wukong;

/// <summary>
/// 火魔斩：起飞、寻敌九连斩、下冲与落地；原生标记切阶段，状态负责分支和物理。
/// </summary>
public sealed class WukongFireDemonState : AbstractHeroSkillState
{
    private const string AnimationName = "skills/fire_demon_slash"; // 本招的原生动画资源名。
    private const double NormalAttackBlockSeconds = 7d / 24d; // 起手的普攻阻塞；搜敌与落地各自重设后续窗口。

    private const string DescentMarker = "descent"; // 无目标时进入的动画标记。
    private const string RecoveryMarker = "recovery"; // 落地特效第13源帧将本体送至第70帧恢复。
    private IFsm<IHeroBody> _fsm; // 当前施放所属的身体状态机，供异步完成核对生命周期。
    private string _nextMarker; // 搜敌分支要进入的动画阶段。
    private PackedScene _landingScene; // 落地窗口借用动画给出的场景。
    private bool _landingSpawned; // 同一次施放只创建一次落地斩。

    /// <inheritdoc />
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
    {
        base.Enter(fsm, body);
        _fsm = fsm;
        _nextMarker = null;
        _landingScene = null;
        _landingSpawned = false;
        body.ActionData.LastGroundSegment = -1;
        GravitySuspended = true;
        body.ActionData.InputLocked = true;
        body.ActionData.AttackReadyAt = body.ActionData.Time + NormalAttackBlockSeconds;
        BeginAnimation(body, AnimationName);
    }

    /// <summary>
    /// 前奏固定本体；原地视觉由悟空场景自身的动画节点播放。
    /// </summary>
    /// <param name="body">悟空身体。</param>
    public void BeginPrelude(IHeroBody body) => body.Velocity = Vector2.Zero;

    /// <summary>
    /// 动画发起升空运动。
    /// </summary>
    /// <param name="body">悟空身体。</param>
    /// <param name="velocity">朝右时的速度，px/s。</param>
    public void Launch(IHeroBody body, Vector2 velocity) => body.Velocity = new Vector2(velocity.X * ((ISkillActor)body).Facing, velocity.Y);

    /// <summary>
    /// 有目标时瞬移并与本体动画并行播放九连斩，没有则进入原生下降标记。
    /// </summary>
    /// <param name="body">悟空身体。</param>
    /// <param name="combo">九连斩场景。</param>
    /// <param name="range">寻敌距离，px。</param>
    /// <param name="inset">视口边距，px。</param>
    public void SearchTarget(IHeroBody body, PackedScene combo, float range, float inset)
    {
        body.Velocity = Vector2.Zero;
        var actor = (ISkillActor)body;
        if (actor.FindSkillTarget(range, inset) is not { } target)
        {
            _nextMarker = DescentMarker;
            return;
        }
        // 原版命中分支重设普攻尝试冷却，身体动画仍继续推进。
        body.ActionData.AttackReadyAt = body.ActionData.Time + 40d / 24d;
        actor.SkillPosition = target;
        // 本项目修正原版中心重合时向右的偏置，三段击退统一跟随施放朝向。
        actor.PlaySkillEffect(combo, target, SkillHitId.FireDemonCombo,
            FireDemonPower(body, SkillHitId.FireDemonCombo));
        // Role1.addHit10And1() returns after teleporting and creating the combo.
        // The body timeline continues naturally from source frame 7 to frame 47;
        // only the no-target branch jumps directly to the descent frame.
    }

    /// <summary>
    /// 下冲阶段继续暂停重力，使用动画提供的速度与效果。
    /// </summary>
    /// <param name="body">悟空身体。</param>
    /// <param name="descent">下冲场景。</param>
    /// <param name="velocity">朝右时的下冲速度，px/s。</param>
    public void Descend(IHeroBody body, PackedScene descent, Vector2 velocity)
    {
        Launch(body, velocity);
        var actor = (ISkillActor)body;
        actor.PlaySkillEffect(descent, actor.SkillPosition, SkillHitId.FireDemonDescent,
            FireDemonPower(body, SkillHitId.FireDemonDescent));
    }

    /// <summary>
    /// 开启实际落地检测；不在动画配置中解释条件。
    /// </summary>
    /// <param name="landing">窗口内落地时创建的场景。</param>
    public void OpenLandingWindow(PackedScene landing) => _landingScene = landing;

    /// <summary>
    /// 关闭提前落地窗口，末段仍会补齐一次落地效果。
    /// </summary>
    public void CloseLandingWindow() => _landingScene = null;

    /// <summary>
    /// 第70源帧补齐尚未出现的落地斩并恢复身体；提前落地则由效果完成后跳至此处。
    /// </summary>
    /// <param name="body">悟空身体。</param>
    /// <param name="landing">末段保底效果。</param>
    public void Recover(IHeroBody body, PackedScene landing)
    {
        if (!_landingSpawned)
        {
            SpawnLanding(body, landing, false);
        }

        body.Velocity = Vector2.Zero;
        GravitySuspended = false;
        CompleteAnimation();
    }

    /// <inheritdoc />
    protected override void UpdateSkill(IHeroBody body, float dt)
    {
        if (_landingScene != null && body.OnFloor)
        {
            SpawnLanding(body, _landingScene, true);
        }
    }

    /// <inheritdoc />
    protected override void AfterAnimation(IHeroBody body)
    {
        // 阶段方法可能刚刚开启落地窗口，必须同帧检查地面事实。
        if (_landingScene != null && body.OnFloor)
        {
            SpawnLanding(body, _landingScene, true);
        }

        if (_nextMarker == null)
        {
            return;
        }

        string marker = _nextMarker;
        _nextMarker = null;
        ((ISkillActor)body).BeginSkillAnimation(AnimationName, marker);
    }

    /// <inheritdoc />
    protected override void Leave(IFsm<IHeroBody> fsm, IHeroBody body, bool isShutdown)
    {
        body.ActionData.InputLocked = false;
        _fsm = null;
        _nextMarker = null;
        _landingScene = null;
        base.Leave(fsm, body, isShutdown);
    }

    /// <summary>
    /// 提前落地时冻结身体姿态，落地斩第13源帧恢复；空中兜底仍直接收招。
    /// </summary>
    /// <param name="body">悟空身体。</param>
    /// <param name="scene">落地场景。</param>
    /// <param name="holdBody">是否等待落地效果完成后恢复身体。</param>
    private void SpawnLanding(IHeroBody body, PackedScene scene, bool holdBody)
    {
        _landingScene = null;
        _landingSpawned = true;
        body.Velocity = Vector2.Zero;
        body.ActionData.AttackReadyAt = body.ActionData.Time + 13d / 24d;
        var actor = (ISkillActor)body;
        // 落地斩取下冲后的角色位置；搜敌位置只用于九连斩，不能作为落地表现锚点。
        Vector2 landingPosition = actor.SkillPosition;
        IFsm<IHeroBody> fsm = _fsm;
        int serial = EnterSerial;
        if (holdBody)
        {
            actor.PauseSkillAnimation();
        }

        actor.PlaySkillEffect(scene, landingPosition, SkillHitId.FireDemonLanding,
                    FireDemonPower(body, SkillHitId.FireDemonLanding),
                    completed: holdBody ? () =>
                    {
                        // 已取消或复用的效果不得改变下一次施放；回调只登记阶段，下一物理帧再切动画。
                        if (!fsm.IsDestroyed && ReferenceEquals(fsm.CurrentState, this) && EnterSerial == serial)
                        {
                            _nextMarker = RecoveryMarker;
                        }
                    }
        : null);
    }

    /// <summary>
    /// 火魔各段遵循原版先对基础攻击取整，再乘该段倍率的顺序。
    /// </summary>
    /// <param name="body">悟空身体。</param>
    /// <param name="hit">当前伤害阶段。</param>
    /// <returns>无双加成之前的威力。</returns>
    private static double FireDemonPower(IHeroBody body, SkillHitId hit)
    {
        AttackConfig definition = body.GetModel<ConfigurationModel>().Tables.TbAttackConfig.Get((int)hit);
        return Math.Round((double)body.GetModel<GameLogic.Battle.HeroModel>().Power.Value,
            MidpointRounding.AwayFromZero) * definition.PowerScale.X + definition.FlatPower.X;
    }
}
