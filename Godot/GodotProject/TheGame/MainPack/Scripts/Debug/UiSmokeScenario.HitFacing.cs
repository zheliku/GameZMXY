using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Equipment;
using GameLogic.Save;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;
using System;
using System.Threading.Tasks;
using GameConfig.Battle;
using GameConfig.Sound;
using GameLogic;
using GameLogic.Battle.Attacks;
using GameLogic.Entity;
using GameLogic.Heroes;
using GameLogic.Monsters;
using GameLogic.Profile;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;

/// <summary>用真实伤害入口验证受击保留朝向，击退仍沿攻击方向。</summary>
public sealed partial class UiSmokeScenario
{
    /// <summary>验证悟空及三种怪物的两种朝向、两种受击方向，不增加生产观测接口。</summary>
    /// <param name="flow">创建受测英雄所需的档案作用域。</param>
    /// <returns>实体创建、检查与归还完成的任务。</returns>
    private async Task CheckHitFacingAsync(ProcedureOwner flow)
    {
        m_ExtraHero = await ShowHeroAsync(flow, 0);
        CheckActor(m_ExtraHero);
        ReleaseTestEntity(m_ExtraHero);
        m_ExtraHero = null;
        foreach (int id in new[] { 1, 2, 3 })
        {
            m_Monster = (AbstractMonsterEntity)await GF.Entity.ShowEntityAsync(
                ConfigSystem.Instance.Tables.TbMonsterConfig.Get(id).EntityId, new MonsterSpawn(new Vector2(200, 100)));
            SmokeInspection.FreezeAi(m_Monster);
            m_Monster.SetPhysicsProcess(false);
            CheckActor(m_Monster);
            ReleaseTestEntity(m_Monster);
            m_Monster = null;
        }
        GD.Print("SMOKE-UI: Wukong and three monsters retain facing on hits from either direction; knockback direction passed");

        void CheckActor(AbstractActorEntity actor)
        {
            foreach (int facing in new[] { -1, 1 })
                foreach (int direction in new[] { -1, 1 })
                {
                    actor.SetFacing(facing);
                    var data = AttackData.Create(0, default, 1, DamageKind.Real,
                        new Vector2(2, -5), direction, 0, SoundId.None);
                    var result = actor.ReceiveHit(data, 0);
                    GameFramework.ReferencePool.Release(data);
                    Check(!result.IsMiss && actor.Facing == facing && actor.HasPendingHurt,
                        $"{actor.GetType().Name} 受击意外转身或未登记击退");
                    var knockback = actor.TakePendingHurt();
                    Check(Math.Sign(knockback.X) == direction, "保留朝向时错误反转了击退方向");
                    var player = actor.GetNode<AnimationPlayer>("m_AnimPlayer");
                    player.Play("hurt");
                    player.Seek(1d / 24, true);
                    Check(actor.Facing == facing, "受击动画修改了角色朝向");
                }
        }
    }
}
