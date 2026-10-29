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

/// <summary>
/// 以真实英雄输入和身体 FSM 检查关卡坡面，夹具整体移到现有关卡之外。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 从倒T两端以真实走跑、二段跳登台，保留正式平台尺寸与身体碰撞。
    /// </summary>
    /// <param name="flow">创建英雄的当前作用域。</param>
    /// <returns>60/120Hz完整路线的验证任务。</returns>
    private async Task CheckQixingPlatformAsync(ProcedureOwner flow)
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        var terrain = GD.Load<PackedScene>(flow.GetModel<ConfigurationModel>().Tables.TbLevelConfig.Get(5).ScenePath).Instantiate<Node2D>();
        terrain.Position = new Vector2(10000, 5000);
        terrain.GetNode<Camera2D>("Camera2D").Enabled = false;
        m_Driver.GetTree().Root.AddChild(terrain);
        try
        {
            var failures = new System.Collections.Generic.List<string>();
            foreach (int ticks in new[] { 60, 120 })
            {
                foreach (bool running in new[] { false, true })
                {
                    foreach (int direction in new[] { 1, -1 })
                    {
                        Engine.PhysicsTicksPerSecond = ticks;
                        m_ExtraHero = await ShowHeroAsync(flow, 0);
                        var actor = m_ExtraHero;
                        actor.CollisionMask = 1 | 8;
                        actor.GlobalPosition = terrain.Position + new Vector2(direction > 0 ? 2940 : 3612, 350);
                        actor.SetPhysicsProcess(true);
                        await WaitUntilAsync(() => actor.OnFloor, "倒T夹具未落地");
                        float floor = actor.GlobalPosition.Y;
                        string action = direction > 0 ? InputSystem.MoveRight : InputSystem.MoveLeft;
                        if (running)
                        {
                            Input.ActionPress(action);
                            await WaitFramesAsync(2);
                            Input.ActionRelease(action);
                            await WaitFramesAsync(2);
                        }
                        Input.ActionPress(InputSystem.Jump);
                        bool jumpedAgain = false, landed = false, moving = false;
                        float minimum = floor;
                        for (int frame = 0; frame < ticks * 3; frame++)
                        {
                            await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
                            if (frame == 2)
                            {
                                Input.ActionRelease(InputSystem.Jump);
                            }
                            // 接近一段跳顶点再二跳；不瞬移、不直接改速度。
                            if (!jumpedAgain && actor.ActionData.JumpCount == 1 && actor.Velocity.Y >= -10)
                            {
                                Input.ActionPress(InputSystem.Jump);
                                jumpedAgain = true;
                            }
                            else if (actor.ActionData.JumpCount == 2)
                            {
                                Input.ActionRelease(InputSystem.Jump);
                            }

                            minimum = Math.Min(minimum, actor.GlobalPosition.Y);
                            // 身体先在平台外侧升高，脚底越过台面才横移，避免从下方顶头。
                            if (!moving && floor - actor.GlobalPosition.Y > 264)
                            {
                                Input.ActionPress(action);
                                moving = true;
                            }
                            if (jumpedAgain && actor.OnFloor && actor.GlobalPosition.Y < floor - 200)
                            {
                                landed = true;
                                break;
                            }
                        }
                        string label = $"{ticks}Hz, run={running}, dir={direction}, rise={floor - minimum:0.00}, pos={actor.GlobalPosition - terrain.Position}";
                        GD.Print($"SMOKE-QIXING-PLATFORM: landed={landed}, {label}");
                        if (!landed)
                        {
                            failures.Add(label);
                        }

                        Input.ActionRelease(action);
                        Input.ActionRelease(InputSystem.Jump);
                        ReleaseTestEntity(actor);
                        m_ExtraHero = null;
                        await WaitFramesAsync(3);
                    }
                }
            }

            Check(failures.Count == 0, "倒T平台无法二段跳登台：" + string.Join("; ", failures));
            GD.Print("SMOKE-QIXING-PLATFORM PASS: official geometry, real double jumps, both edges, walk/run, 60/120Hz");
        }
        finally
        {
            Input.ActionRelease(InputSystem.MoveLeft);
            Input.ActionRelease(InputSystem.MoveRight);
            Input.ActionRelease(InputSystem.Jump);
            Engine.PhysicsTicksPerSecond = originalTicks;
            if (m_ExtraHero != null)
            {
                ReleaseTestEntity(m_ExtraHero);
            }

            m_ExtraHero = null;
            terrain.Free();
        }
    }

    /// <summary>
    /// 在 60/120Hz 下双向走跑越过完整土坡，停步和主动跳跃分别检查。
    /// </summary>
    /// <param name="flow">创建英雄的当前作用域。</param>
    /// <returns>原生物理与真实动画检查任务。</returns>
    private async Task CheckSlopeMovementAsync(ProcedureOwner flow)
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        try
        {
            foreach (int number in new[] { 1, 2, 5 })
            {
                if (OS.GetCmdlineUserArgs().Contains("--terrain-level2") && number != 2)
                {
                    continue;
                }

                if (OS.GetCmdlineUserArgs().Contains("--terrain-level5") && number != 5)
                {
                    continue;
                }

                var terrain = GD.Load<PackedScene>(flow.GetModel<ConfigurationModel>().Tables.TbLevelConfig.Get(number).ScenePath).Instantiate<Node2D>();
                terrain.Position = new Vector2(10000, 5000);
                terrain.SetProcess(false);
                terrain.SetPhysicsProcess(false);
                terrain.GetNode<Camera2D>("Camera2D").Enabled = false;
                terrain.GetNodeOrNull("World/Traps")?.Free(); // 本夹具只测行走；正式陷阱接触另有实战回归。
                m_Driver.GetTree().Root.AddChild(terrain);
                try
                {
                    foreach (StaticBody2D hill in terrain.GetNode("World/Geometry/Slopes").GetChildren().OfType<StaticBody2D>())
                    {
                        foreach (int ticks in new[] { 60, 120 })
                        {
                            foreach (bool running in new[] { false, true })
                            {
                                foreach (int direction in new[] { 1, -1 })
                                {
                                    Engine.PhysicsTicksPerSecond = ticks;
                                    m_ExtraHero = await ShowHeroAsync(flow, flow.GetModel<ProfileModel>().TotalExperience);
                                    AbstractHeroEntity actor = m_ExtraHero;
                                    actor.CollisionMask = 1 | 8; // 排除本关相机屏障；被测地形仍使用正式物理层。
                                    Vector2[] contour = hill.GetNode<CollisionPolygon2D>("Surface").Polygon;
                                    float start = hill.Position.X + contour.Min(point => point.X) - 60;
                                    // 花果山最后一坡伸到关卡右墙外，仅测试可玩范围，不能把穿墙当作通过条件。
                                    float end = Math.Min(hill.Position.X + contour.Max(point => point.X) + 60,
                                        terrain.GetNode<Camera2D>("Camera2D").LimitRight - 70);
                                    float top = hill.Position.Y + contour.Min(point => point.Y);
                                    actor.GlobalPosition = terrain.Position + new Vector2(direction > 0 ? start : end, top - 100);
                                    actor.SetPhysicsProcess(true);
                                    for (int frame = 0; frame < ticks; frame++)
                                    {
                                        await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
                                    }

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
                                    bool stoppedOnSlope = false;
                                    float stopAt = direction > 0 ? start + 95 : end - 50;
                                    for (int frame = 0; frame < ticks * 15; frame++)
                                    {
                                        await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
                                        string state = SmokeInspection.BodyStateName(actor);
                                        Check(actor.OnFloor && state == "Ground",
                                            $"坡面意外离地：level={number}, {ticks}Hz, run={running}, dir={direction}, pos={actor.GlobalPosition - terrain.Position}, v={actor.Velocity}, state={state}");
                                        samples++;
                                        if (!stoppedOnSlope && (direction > 0 ? actor.GlobalPosition.X >= terrain.Position.X + stopAt :
                                            actor.GlobalPosition.X <= terrain.Position.X + stopAt))
                                        {
                                            Input.ActionRelease(action);
                                            Vector2 stopPosition = actor.GlobalPosition;
                                            for (int pause = 0; pause < ticks * 3 / 5; pause++)
                                            {
                                                await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
                                            }

                                            Check(actor.OnFloor && actor.GlobalPosition.DistanceTo(stopPosition) < 1,
                                                                                            $"坡中停步滑动或离地：level={number}, hill={hill.Name}, dir={direction}");
                                            if (running)
                                            {
                                                Input.ActionPress(action);
                                                await WaitFramesAsync(2);
                                                Input.ActionRelease(action);
                                                await WaitFramesAsync(2);
                                            }
                                            Input.ActionPress(action);
                                            stoppedOnSlope = true;
                                        }
                                        if (direction > 0 ? actor.GlobalPosition.X >= terrain.Position.X + end : actor.GlobalPosition.X <= terrain.Position.X + start)
                                        {
                                            break;
                                        }
                                    }
                                    string animation = actor.GetNode<AnimationPlayer>("m_AnimPlayer").CurrentAnimation;
                                    var collisionNames = Enumerable.Range(0, actor.GetSlideCollisionCount())
                                        .Select(i => ((Node)actor.GetSlideCollision(i).GetCollider()).GetPath().ToString());
                                    Check(samples < ticks * 15 && animation == (running ? "run" : "walk"),
                                        $"未按真实走跑输入完成坡面路线：level={number}, running={running}, anim={animation}, " +
                                        $"pos={actor.GlobalPosition - terrain.Position}, v={actor.Velocity}, collisions={string.Join(',', collisionNames)}");
                                    Input.ActionRelease(action);
                                    for (int frame = 0; frame < 5; frame++)
                                    {
                                        await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
                                    }

                                    Check(actor.OnFloor && actor.GetNode<AnimationPlayer>("m_AnimPlayer").CurrentAnimation == "idle1", "坡后停步错误进入下落");
                                    Input.ActionPress(InputSystem.Jump);
                                    for (int frame = 0; frame < 4; frame++)
                                    {
                                        await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.PhysicsFrame);
                                    }

                                    Check(!actor.OnFloor && actor.Velocity.Y < 0, "贴地修复阻止了主动跳跃");
                                    Input.ActionRelease(InputSystem.Jump);
                                    ReleaseTestEntity(m_ExtraHero);
                                    m_ExtraHero = null;
                                    GD.Print($"SMOKE-SLOPE PASS: level={number}, hill={hill.Name}, {ticks}Hz, running={running}, direction={direction}, samples={samples}; stop/resume/jump");
                                }
                            }
                        }
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
