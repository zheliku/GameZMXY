using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Skills;
using Godot;

namespace GameLogic.Heroes;

/// <summary>
/// 英雄捕获表现归本实体和身体状态，捕获者必须用本次运行编号释放或施加吞噬伤害。
/// </summary>
public abstract partial class AbstractHeroEntity
{
    private int _captorId; // 当前捕获者的运行编号，零表示未被捕获。

    /// <summary>
    /// 捕获存活且非无敌的英雄，终止本次攻击并隐藏身体；身体状态负责停止输入与物理。
    /// </summary>
    /// <param name="captorEntityId">捕获者的非零运行编号。</param>
    /// <returns>当前可以被这个来源捕获时为真。</returns>
    public bool TryCapture(int captorEntityId)
    {
        if (captorEntityId == 0 || !IsAlive || Invulnerable || _captorId != 0)
        {
            return false;
        }

        _captorId = captorEntityId;
        this.GetSystem<SkillSystem>().CancelSkill(HeroId);
        ReleaseAttack();
        PendingHurt = null;
        Velocity = Vector2.Zero;
        ActionData.Captured = true;
        AnimPlayer.Active = false;
        Visible = false;
        return true;
    }

    /// <summary>
    /// 仅当前捕获来源可恢复显示与动作，迟到的旧来源不能解除新的捕获。
    /// </summary>
    /// <param name="captorEntityId">原捕获者运行编号。</param>
    public void ReleaseCapture(int captorEntityId)
    {
        if (_captorId == 0 || _captorId != captorEntityId)
        {
            return;
        }

        _captorId = 0;
        ActionData.Captured = false;
        AnimPlayer.Active = true;
        Visible = IsShown;
    }

    /// <summary>
    /// 吞噬按固定威力扣血，不再闪避、减防或触发受击动作；死亡前恢复英雄供墓碑演出。
    /// </summary>
    /// <param name="captorEntityId">当前捕获来源。</param>
    /// <param name="amount">已从招式配置读取的伤害。</param>
    public void ReceiveCaptureDamage(int captorEntityId, int amount)
    {
        if (_captorId != captorEntityId || !IsAlive)
        {
            return;
        }

        if (amount >= this.GetModel<HeroModel>().GetState(HeroId).Hp.Value)
        {
            ReleaseCapture(captorEntityId);
        }

        ReceivePeriodicDamage(amount);
    }
}
