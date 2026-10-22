using System;
using System.Linq;
using System.Threading.Tasks;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Heroes;
using GameLogic.Input;
using GameLogic.Profile;
using Godot;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>以真实英雄输入和身体 FSM 检查两关坡面，夹具整体移到现有关卡之外。</summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>在 60/120Hz 下双向走跑越过完整土坡，停步和主动跳跃分别检查。</summary>
    /// <param name="flow">创建英雄的当前作用域。</param>
    /// <returns>原生物理与真实动画检查任务。</returns>
    private async Task CheckSlopeMovementAsync(ProcedureOwner flow)
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        try
        {
            foreach (int number in new[] { 1, 2 })
            {
                if (OS.GetCmdlineUserArgs().Contains("--terrain-level2") && number == 1)
                    continue;
                var terrain = GD.Load<PackedScene>(flow.GetModel<ConfigurationModel>().Tables.TbLevelConfig.Get(number).ScenePath).Instantiate<Node2D>();
                terrain.Position = new Vector2(10000, 5000);
                terrain.SetProcess(false);
                terrain.SetPhysicsProcess(false);
                terrain.GetNode<Camera2D>("Camera2D").Enabled = false;
                m_Driver.GetTree().Root.AddChild(terrain);
                try
                {
                    foreach (int ticks in new[] { 60, 120 })
                        foreach (bool running in new[] { false, true })
                            foreach (int direction in new[] { 1, -1 })
                            {
                                Engine.PhysicsTicksPerSecond = ticks;
                                m_ExtraHero = await ShowHeroAsync(flow, flow.GetModel<ProfileModel>().TotalExperience);
                                AbstractHeroEntity actor = m_ExtraHero;
                                actor.CollisionMask = 1 | 8; // 排除本关相机屏障；被测地形仍使用正式物理层。
                                float start = number == 1 ? 900 : 260;
                                float end = number == 1 ? 1430 : 770;
                                actor.GlobalPosition = terrain.Position + new Vector2(direction > 0 ? start : end, 350);
                                actor.SetPhysicsProcess(true);
                                for (int frame = 0; frame < ticks; frame++)
                                    await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
                                Check(actor.OnFloor, $"坡面夹具未落地：level={number}, pos={actor.GlobalPosition - terrain.Position}, v={actor.Velocity}");
                                string action = direction > 0 ? InputSystem.MoveRight : InputSystem.MoveLeft;
                                if (running)
                                {
                                    Input.ActionPress(action);
                                    await WaitFramesAsync(2);
                                    Input.ActionRelease(action);
                                    await WaitFramesAsync(2);
                                }
                                Input.ActionPress(action);
                                int samples = 0;
                                for (int frame = 0; frame < ticks * 15; frame++)
                                {
                                    await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
                                    string state = SmokeInspection.BodyStateName(actor);
                                    Check(actor.OnFloor && state == "Ground",
                                        $"坡面意外离地：level={number}, {ticks}Hz, run={running}, dir={direction}, pos={actor.GlobalPosition - terrain.Position}, v={actor.Velocity}, state={state}");
                                    samples++;
                                    if (direction > 0 ? actor.GlobalPosition.X >= terrain.Position.X + end : actor.GlobalPosition.X <= terrain.Position.X + start)
                                        break;
                                }
                                string animation = actor.GetNode<AnimationPlayer>("m_AnimPlayer").CurrentAnimation;
                                var collisionNames = Enumerable.Range(0, actor.GetSlideCollisionCount())
                                    .Select(i => ((Node)actor.GetSlideCollision(i).GetCollider()).GetPath().ToString());
                                Check(samples < ticks * 15 && animation == (running ? "run" : "walk"),
                                    $"未按真实走跑输入完成坡面路线：level={number}, running={running}, anim={animation}, " +
                                    $"pos={actor.GlobalPosition - terrain.Position}, v={actor.Velocity}, collisions={string.Join(',', collisionNames)}");
                                Input.ActionRelease(action);
                                for (int frame = 0; frame < 5; frame++)
                                    await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
                                Check(actor.OnFloor && actor.GetNode<AnimationPlayer>("m_AnimPlayer").CurrentAnimation == "idle1", "坡后停步错误进入下落");
                                Input.ActionPress(InputSystem.Jump);
                                for (int frame = 0; frame < 4; frame++)
                                    await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
                                Check(!actor.OnFloor && actor.Velocity.Y < 0, "贴地修复阻止了主动跳跃");
                                Input.ActionRelease(InputSystem.Jump);
                                ReleaseTestEntity(m_ExtraHero);
                                m_ExtraHero = null;
                                GD.Print($"SMOKE-SLOPE PASS: level={number}, {ticks}Hz, running={running}, direction={direction}, samples={samples}");
                            }
                }
                finally
                {
                    terrain.Free();
                }
            }
        }
        finally
        {
            Engine.PhysicsTicksPerSecond = originalTicks;
            Input.ActionRelease(InputSystem.MoveLeft);
            Input.ActionRelease(InputSystem.MoveRight);
            Input.ActionRelease(InputSystem.Jump);
            if (m_ExtraHero != null)
            {
                ReleaseTestEntity(m_ExtraHero);
                m_ExtraHero = null;
            }
        }
    }
}
