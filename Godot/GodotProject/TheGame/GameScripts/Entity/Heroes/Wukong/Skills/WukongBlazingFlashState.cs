using GameConfig.Skills;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Heroes.Body;
using GameLogic.Input;
using GameLogic.Skills;
using Godot;

namespace GameLogic.Heroes.Wukong;

/// <summary>烈焰闪：起手保留移动，闪身阶段生成拖尾并开启无敌，末段停止移动。</summary>
public sealed class WukongBlazingFlashState : AbstractHeroSkillState
{
    private const string AnimationName = "skills/blazing_flash"; // 本招的原生动画资源名。

    private int m_MoveAxis; // 闪身前保留的起手方向。
    private bool m_Dashing; // 进入闪身后不再应用常规移动。

    /// <inheritdoc />
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
    {
        base.Enter(fsm, body);
        m_MoveAxis = body.ActionData.MoveAxis;
        m_Dashing = false;
        body.ActionData.LastGroundSegment = -1;
        BeginAnimation(body, AnimationName);
    }

    /// <summary>起手生成本招攻击包，固定威力仍由数值表提供。</summary>
    /// <param name="body">悟空身体。</param>
    public void BeginHit(IHeroBody body) => ((ISkillActor)body).NewSkillAttack(
        SkillHitId.BlazingFlash, AttackPower(body, SkillHitId.BlazingFlash));

    /// <summary>在动画标出的闪身点开启拖尾、无敌与定向速度。</summary>
    /// <param name="body">悟空身体。</param>
    /// <param name="trail">动画直接引用的拖尾场景。</param>
    /// <param name="speed">闪身速度，px/s。</param>
    public void StartDash(IHeroBody body, PackedScene trail, float speed)
    {
        m_Dashing = true;
        var actor = (ISkillActor)body;
        actor.SpawnSkillEffect(trail, actor.SkillPosition);
        body.GetSystem<SkillSystem>().GrantInvulnerability(body.ActionData.Time,
            body.GetModel<ConfigurationModel>().Tables.TbSkillConfig.Get(SkillId.BlazingFlash).InvincibleDurationSeconds);
        body.Velocity = new Vector2(actor.Facing * speed, body.Velocity.Y);
    }

    /// <summary>收招点停止身体移动。</summary>
    /// <param name="body">悟空身体。</param>
    public void StopDash(IHeroBody body) => body.Velocity = Vector2.Zero;

    /// <inheritdoc />
    protected override void UpdateSkill(IHeroBody body, float dt)
    {
        if (m_Dashing)
            return;
        var input = body.GetSystem<InputSystem>();
        if (!input.IsDirectionHeld(body.ActionData, m_MoveAxis) || input.WasDirectionReleased(body.ActionData, m_MoveAxis))
            m_MoveAxis = 0;
        float speed = body.ActionData.Running ? body.Params.RunSpeed : body.Params.WalkSpeed;
        body.Velocity = new Vector2(m_MoveAxis * speed * body.MoveSpeedMultiplier, body.Velocity.Y);
    }
}
