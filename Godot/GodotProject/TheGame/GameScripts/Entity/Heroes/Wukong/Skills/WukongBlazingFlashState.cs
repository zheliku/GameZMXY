using GameConfig.Skills;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Heroes.Body;
using GameLogic.Input;
using GameLogic.Skills;
using Godot;

namespace GameLogic.Heroes.Wukong;

/// <summary>
/// 烈焰闪：起手保留移动，闪身阶段生成拖尾并开启无敌，末段停止移动。
/// </summary>
public sealed class WukongBlazingFlashState : AbstractHeroSkillState
{
    private const string AnimationName = "skills/blazing_flash"; // 本招的原生动画资源名。
    private const double NormalAttackBlockSeconds = .5d; // 官方烈焰闪起手后普通攻击的阻塞秒数。
    private const float InvulnerabilitySeconds = .5f; // 闪身阶段开启的官方无敌秒数。

    private int _moveAxis; // 闪身前保留的起手方向。
    private bool _dashing; // 进入闪身后不再应用常规移动。

    /// <inheritdoc />
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
    {
        base.Enter(fsm, body);
        _moveAxis = body.ActionData.MoveAxis;
        _dashing = false;
        body.ActionData.LastGroundSegment = -1;
        body.ActionData.AttackReadyAt = body.ActionData.Time + NormalAttackBlockSeconds;
        BeginAnimation(body, AnimationName);
    }

    /// <summary>
    /// 起手生成本招攻击包，固定威力仍由数值表提供。
    /// </summary>
    /// <param name="body">悟空身体。</param>
    public void BeginHit(IHeroBody body)
    {
        ((ISkillActor)body).NewSkillAttack(
        SkillHitId.BlazingFlash, AttackPower(body, SkillHitId.BlazingFlash));
    }

    /// <summary>
    /// 在动画标出的闪身点开启拖尾、无敌与定向速度。
    /// </summary>
    /// <param name="body">悟空身体。</param>
    /// <param name="trail">动画直接引用的拖尾场景。</param>
    /// <param name="speed">闪身速度，px/s。</param>
    public void StartDash(IHeroBody body, PackedScene trail, float speed)
    {
        _dashing = true;
        var actor = (ISkillActor)body;
        actor.PlaySkillEffect(trail, actor.SkillPosition);
        body.GetSystem<SkillSystem>().GrantInvulnerability(body.ActionData.Time, InvulnerabilitySeconds);
        body.Velocity = new Vector2(actor.Facing * speed, body.Velocity.Y);
    }

    /// <summary>
    /// 收招点停止身体移动。
    /// </summary>
    /// <param name="body">悟空身体。</param>
    public void StopDash(IHeroBody body) => body.Velocity = Vector2.Zero;

    /// <inheritdoc />
    protected override void UpdateSkill(IHeroBody body, float dt)
    {
        if (_dashing)
        {
            return;
        }

        InputSystem input = body.GetSystem<InputSystem>();
        if (!input.IsDirectionHeld(body.ActionData, _moveAxis) || input.WasDirectionReleased(body.ActionData, _moveAxis))
        {
            _moveAxis = 0;
        }

        float speed = body.ActionData.Running ? body.Params.RunSpeed : body.Params.WalkSpeed;
        body.Velocity = new Vector2(_moveAxis * speed * body.MoveSpeedMultiplier, body.Velocity.Y);
    }
}
