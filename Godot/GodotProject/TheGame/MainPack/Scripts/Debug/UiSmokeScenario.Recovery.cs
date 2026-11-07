using System.Threading.Tasks;
using GameConfig.Attributes;
using GameFramework.Event;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attributes;
using GameLogic.Event.Combat;
using GameLogic.Heroes;
using Godot;
using GodotGameFramework;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 自然恢复在真实物理帧、暂停及死亡流程中的验证。
/// </summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>
    /// 直接使用本局系统与实体，不增加用于测试的生产入口。
    /// </summary>
    /// <param name="hero">真实英雄。</param>
    /// <param name="flow">本局规则作用域。</param>
    /// <returns>双频率恢复与无受击反馈的流失验证。</returns>
    private async Task CheckNaturalRecoveryAsync(AbstractHeroEntity hero, ProcedureOwner flow)
    {
        HeroSystem system = flow.GetSystem<HeroSystem>();
        HeroModel model = flow.GetModel<HeroModel>();
        var source = new AttributeSourceId("RecoverySmoke", 1);
        int originalTicks = Engine.PhysicsTicksPerSecond;
        int damageNotifications = 0;
        int deaths = 0;
        void ObserveDamage(object sender, GameEventArgs args)
        {
            if (args is DamageDealtEventArgs data && data.TargetEntityId == hero.Id)
            {
                damageNotifications++;
            }
        }
        void ObserveDeath(GameLogic.Entity.AbstractActorEntity actor)
        {
            deaths++;
        }
        GF.Event.Subscribe(DamageDealtEventArgs.EventId, ObserveDamage);
        hero.Died += ObserveDeath;
        hero.SetPhysicsProcess(true);
        try
        {
            foreach (int ticks in new[] { 60, 120 })
            {
                Engine.PhysicsTicksPerSecond = ticks;
                system.Heal(int.MaxValue);
                system.RestoreMp(int.MaxValue);
                system.Damage(20);
                system.TrySpendMp(10);
                model.RecoveryProgress = 0;
                system.SetSource(source, [AttributeModifier.Flat(AttributeType.HpRegen, 2), AttributeModifier.Flat(AttributeType.MpRegen, 3)]);
                int hp = hero.Hp;
                int mp = model.Mp.Value;
                await WaitUntilAsync(() => hero.Hp > hp, "装备回血没有在真实物理帧结算");
                Check(hero.Hp == hp + 2 && model.Mp.Value == mp + 3, "回血回蓝没有使用同一节拍");
                GF.Base.PauseGame();
                await WaitFramesAsync(3);
                double progress = model.RecoveryProgress;
                await WaitFramesAsync(30);
                Check(hero.Hp == hp + 2 && model.Mp.Value == mp + 3 && model.RecoveryProgress == progress, "暂停仍推进自然恢复");
                GF.Base.ResumeGame();
                system.SetSource(source, [AttributeModifier.Flat(AttributeType.HpRegen, -2), AttributeModifier.Flat(AttributeType.MpRegen, -3)]);
                await WaitUntilAsync(() => hero.Hp < hp + 2, "负回血没有实际消耗生命");
                Check(hero.Hp == hp && model.Mp.Value == mp, "负恢复钳制或结算不正确");
                Check(damageNotifications == 0 && deaths == 0, "自然恢复错误地产生受击数字或死亡");
                GD.Print($"SMOKE-RECOVERY {ticks}Hz PASS: positive/negative resources, shared source cadence and pause");
            }
            system.SetSource(source, [AttributeModifier.Flat(AttributeType.HpRegen, -100000)]);
            await WaitUntilAsync(() => hero.Dead, "负回血耗尽没有进入正式死亡");
            await WaitFramesAsync(3);
            Check(deaths == 1 && damageNotifications == 0 && model.IsDepleted, "生命流失死亡没有唯一通知或生成受击数字");
            GD.Print("SMOKE-RECOVERY DEATH PASS: one native death and no hurt-number event");
        }
        finally
        {
            GF.Event.Unsubscribe(DamageDealtEventArgs.EventId, ObserveDamage);
            hero.Died -= ObserveDeath;
            if (GF.Base.IsGamePaused)
            {
                GF.Base.ResumeGame();
            }

            system.RemoveSource(source);
            Engine.PhysicsTicksPerSecond = originalTicks;
        }
    }
}
