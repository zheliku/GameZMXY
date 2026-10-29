using System;
using System.Linq;
using System.Threading.Tasks;
using GameLogic.Level;
using GameLogic.UI;
using GameLogic.Heroes.Body;
using GameLogic.Heroes.Effects;
using Godot;
using GodotGameFramework;

/// <summary>
/// 英雄墓碑的真实物理、非透明底边与死亡后重试/地图重入回归。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 在两个物理频率下空中死亡，确认不再上冲、墓碑贴地、结算可听，重试后恢复英雄。
    /// </summary>
    /// <returns>完成真实死亡、重试和地图重入的任务。</returns>
    private async Task CheckHeroDeathReentryAsync()
    {
        int originalTicks = Engine.PhysicsTicksPerSecond;
        try
        {
            foreach (var (ticks, spawnX) in new[] { (60, 200), (120, 200), (60, 1035), (120, 1035) })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                var hero = FindHero();
                var level = FindLevel();
                hero.GlobalPosition = new Vector2(spawnX, 100);
                hero.Velocity = new Vector2(150, -400);
                KillHero(hero);
                await WaitUntilAsync(() => hero.AnimPlayer.CurrentAnimation == HeroAnims.Death,
                    "空中死亡没有进入死亡动画");
                Check(hero.Velocity.X == 0 && hero.Velocity.Y >= 0,
                    $"墓碑继承了死亡前的上冲/横向速度：{hero.Velocity}");
                var presentation = hero.GetNode<HeroTombstoneNode>("m_Tombstone");
                var tombstone = presentation.GetNode<Sprite2D>("m_Sprite");
                var deathPlayer = presentation.GetNode<AnimationPlayer>("m_AnimationPlayer");
                await WaitUntilAsync(() => presentation.IsOnFloor() && tombstone.Frame == 39, "墓碑稳定帧没有落地", 2500,
                    () => $"ticks={ticks}, x={spawnX}, position={presentation.GlobalPosition}, velocity={presentation.Velocity}, floor={presentation.IsOnFloor()}, frame={tombstone.Frame}");
                deathPlayer.Pause();
                Check(presentation.IsOnFloor() && presentation.Visible && !hero.GetNode<Sprite2D>("m_Body").Visible,
                    "死亡演出没有落地或墓碑/角色可见性错误");

                // 测试读取实际最后一格alpha底边与身体物理底边，不向生产实体添加观测API。
                Vector2I cell = (Vector2I)tombstone.Texture.GetSize() / new Vector2I(tombstone.Hframes, tombstone.Vframes);
                using var image = tombstone.Texture.GetImage();
                using var frame = image.GetRegion(new Rect2I(tombstone.FrameCoords * cell, cell));
                Rect2I opaque = frame.GetUsedRect();
                Vector2 visualBottom = tombstone.ToGlobal(tombstone.GetRect().Position +
                    new Vector2(opaque.Position.X + opaque.Size.X / 2f, opaque.End.Y));
                var collider = presentation.GetNode<CollisionShape2D>("CollisionShape2D");
                Check(!collider.Disabled && hero.GetNode<CollisionShape2D>("CollisionShape2D").Disabled,
                    "死亡仍沿用英雄的大胶囊，或墓碑落地锚点未启用");
                Vector2 foot = collider.ToGlobal(new Vector2(0, collider.Shape.GetRect().End.Y));
                Check(Math.Abs(visualBottom.Y - foot.Y) < 2,
                    $"墓碑底边未贴物理地面：visual={visualBottom.Y}, foot={foot.Y}");
                var query = PhysicsRayQueryParameters2D.Create(visualBottom + Vector2.Up * 12,
                    visualBottom + Vector2.Down * 12, 9);
                var contact = hero.GetWorld2D().DirectSpaceState.IntersectRay(query);
                Check(contact.Count > 0 && Math.Abs(contact["position"].AsVector2().Y - visualBottom.Y) < 2,
                    $"墓碑与实际坡面/地面仍有间隙：x={spawnX}, visual={visualBottom}");
                float height = opaque.Size.Y * tombstone.GlobalScale.Y;
                Check(height is >= 100 and <= 120, $"墓碑可见高度异常：{height}");
                await CaptureCombatAsync($"hero_tombstone_{ticks}_{spawnX}");
                deathPlayer.Play();
                await CheckFailureSettlementAsync(level);
                await WaitUntilAsync(() => FindLevel() != level && CurrentBattleForm() != null &&
                    GF.Procedure.CurrentProcedure is ProcedureLevel, "失败后重试未装配新关卡", 15000);
                await WaitFramesAsync(6);
                CheckHeroRevived();
                GD.Print($"SMOKE-HERO-DEATH PASS: {ticks}Hz, x={spawnX}, rising velocity cleared, actual floor ray, grounded alpha bottom, audible defeat, retry restored body/weapon");
            }
            await CheckFailureReturnMapAsync();
            await WaitFramesAsync(6);
            CheckHeroRevived();
            GD.Print("SMOKE-HERO-REENTRY PASS: repeated deaths, retry, map return/reentry, live body and hidden tombstone");
        }
        finally
        {
            Engine.PhysicsTicksPerSecond = originalTicks;
        }
    }

    /// <summary>
    /// 重试或地图重入后检查真实节点与动画，延迟几帧排除旧死亡播放器再次写回。
    /// </summary>
    private void CheckHeroRevived()
    {
        var hero = FindHero();
        Check(!hero.Dead && hero.Hp > 0 && hero.GetNode<Sprite2D>("m_Body").Visible &&
            hero.GetNode<Sprite2D>("m_Weapon").Visible && !hero.GetNode<HeroTombstoneNode>("m_Tombstone").Visible &&
            hero.AnimPlayer.CurrentAnimation != HeroAnims.Death &&
            !hero.GetNode<CollisionShape2D>("CollisionShape2D").Disabled &&
            !hero.GetNode<HeroTombstoneNode>("m_Tombstone").IsPhysicsProcessing(),
            "死亡重入后残留墓碑、隐藏身体/武器或旧死亡动画");
    }
}
