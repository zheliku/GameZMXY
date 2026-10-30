using System;
using System.Reflection;
using System.Threading.Tasks;
using GameConfig.Entity;
using GameLogic.Enemies;
using GameLogic.Enemies.AI;
using GameLogic.Entity;
using GameLogic.Heroes;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;

/// <summary>
/// 官方距离分支的真实AI边界回归；只在测试侧冻结身体并设置目标，AI仍由GGF推进。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 检查三种飞游怪的外层高度、龙王无高度限制及牛魔王互斥的水平选招区间。
    /// </summary>
    /// <param name="hero">当前真实英雄沙包，检查结束恢复位置。</param>
    /// <returns>两个物理频率下的边界验证。</returns>
    private async Task CheckEnemyRangesAsync(AbstractHeroEntity hero)
    {
        Vector2 origin = hero.GlobalPosition;
        int originalTicks = Engine.PhysicsTicksPerSecond;
        var target = typeof(AbstractEnemyEntity).GetMethod("SetTarget", BindingFlags.NonPublic | BindingFlags.Instance)!;
        try
        {
            foreach (int hz in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = hz;
                foreach (EntityId id in new[] { EntityId.Bat, EntityId.FlyingDemon, EntityId.SharkSpawn })
                {
                    hero.GlobalPosition = new Vector2(100000, 100000);
                    m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(id, new EnemySpawn(new Vector2(600, 200)));
                    var actor = m_Monster;
                    actor.SetPhysicsProcess(false);
                    var agent = (IEnemyAiAgent)actor;
                    typeof(AbstractEnemyEntity).GetField("m_SpawnPosition", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(actor, actor.GlobalPosition + new Vector2(600, 0));
                    actor.AiData.ElapsedSeconds = .49;
                    actor.AiData.ClockOrigin = 0;
                    hero.GlobalPosition = actor.GlobalPosition + new Vector2(-100, -900.1f);
                    target.Invoke(actor, [hero]);
                    await WaitFramesAsync(5);
                    Check(actor.AiData.MoveIntent == 1 && actor.AiData.AttackRequest < 0,
                        $"{id}/{hz} 高度超限没有按场景巡逻边界折返");
                    hero.GlobalPosition = actor.GlobalPosition + new Vector2(-100, -900);
                    await WaitFramesAsync(3);
                    Check(actor.AiData.MoveIntent == -1 && agent.HasTarget, $"{id}/{hz} 官方900px包含边界没有恢复追踪");
                    ReleaseTestEntity(actor);
                    m_Monster = null;
                    await WaitFramesAsync(2);
                }

                hero.GlobalPosition = new Vector2(100000, 100000);
                m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.DragonKing, new EnemySpawn(new Vector2(600, 100)));
                await WaitUntilAsync(() => m_Monster.OnFloor, "龙王范围夹具未落地");
                m_Monster.SetPhysicsProcess(false);
                hero.GlobalPosition = m_Monster.GlobalPosition + new Vector2(80, -700);
                target.Invoke(m_Monster, [hero]);
                m_Monster.AiData.Cooldowns[3] = 0;
                await WaitFramesAsync(3);
                Check(m_Monster.AiData.AttackRequest == 3, "龙王错误限制目标高度，未在100px内选择水柱");
                ReleaseTestEntity(m_Monster);
                m_Monster = null;
                await WaitFramesAsync(2);

                hero.GlobalPosition = new Vector2(100000, 100000);
                m_Monster = (AbstractEnemyEntity)await GF.Entity.ShowEntityAsync(EntityId.BullDemonKing,
                    new EnemySpawn(new Vector2(1350, 100), ContinueEncounterAsync: (_, _, _) => Task.CompletedTask));
                var bull = m_Monster;
                await WaitUntilAsync(() => bull.OnFloor, "牛魔王范围夹具未落地");
                bull.SetPhysicsProcess(false);
                typeof(AbstractEnemyEntity).GetField("m_SpawnPosition", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(bull, bull.GlobalPosition + new Vector2(600, 0));
                bull.AiData.ElapsedSeconds = .49;
                bull.AiData.ClockOrigin = 0;
                hero.GlobalPosition = bull.GlobalPosition + new Vector2(-200.1f, -700);
                target.Invoke(bull, [hero]);
                await WaitFramesAsync(5);
                Check(bull.AiData.MoveIntent == 1 && bull.AiData.AttackRequest < 0, "牛魔王200px外错误追击/施放技能，没有按官方巡逻");
                foreach (var sample in new[] { (Distance: 80f, Expected: 4), (Distance: 100f, Expected: 3), (Distance: 100.1f, Expected: -1) })
                {
                    bull.AiData.AttackRequest = -1;
                    Array.Fill(bull.AiData.Cooldowns, 0);
                    hero.GlobalPosition = bull.GlobalPosition + new Vector2(sample.Distance, -700);
                    await WaitFramesAsync(2);
                    int request = bull.AiData.AttackRequest;
                    Check(sample.Expected < 0 ? request is 1 or 2 : request == sample.Expected,
                        $"牛魔王{sample.Distance}px互斥分支错误：{request}");
                }
                bull.AiData.AttackRequest = -1;
                Array.Fill(bull.AiData.Cooldowns, 0);
                bull.AiData.Cooldowns[3] = 30;
                hero.GlobalPosition = bull.GlobalPosition + new Vector2(90, -700);
                await WaitUntilAsync(() => bull.AiData.AttackRequest >= 0, "牛魔王90px冷却分支没有选择普通攻击");
                Check(bull.AiData.AttackRequest == 0, "牛魔王90px践踏冷却时错误落入100～200px技能区间");
                ReleaseTestEntity(bull);
                m_Monster = null;
                await WaitFramesAsync(2);
                GD.Print($"SMOKE-ENEMY RANGES {hz}Hz PASS: flying outer height, dragon horizontal-only, bull exclusive 80/100/200 branches");
            }
        }
        finally
        {
            if (m_Monster is { IsShown: true })
            {
                ReleaseTestEntity(m_Monster);
            }
            m_Monster = null;
            hero.GlobalPosition = origin;
            Engine.PhysicsTicksPerSecond = originalTicks;
        }
    }
}
