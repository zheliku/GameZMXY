using System;
using GameConfig.Battle;
using GameLogic.Architecture;
using GameLogic.Effects;
using GameLogic.Enemies.AI;
using GameLogic.Enemies.Body;
using Godot;
using GodotGameFramework;
using GodotGameFramework.NodePool;

namespace GameLogic.Enemies;

/// <summary>
/// 怪物招式冷却装配与出招生命周期；AI距离由场景感知节点负责。
/// </summary>
public abstract partial class AbstractEnemyEntity
{
    /// <summary>
    /// 取得并登记本敌人拥有的离身攻击，供具体敌人装填攻击数据。
    /// </summary>
    /// <param name="scene">已登记到 GGF 节点池的场景。</param>
    /// <returns>由本敌人负责归还的效果。</returns>
    /// <exception cref="InvalidOperationException">场景未登记到节点池。</exception>
    protected SkillEffectNode RentSkillEffect(PackedScene scene)
    {
        SkillEffectNode effect = NodePool.Instance.RentNode<SkillEffectNode>(scene.ResourcePath, GetParent()) ??
            throw new InvalidOperationException($"敌人技能未登记到节点池：{scene.ResourcePath}");
        _skillEffects.Add(effect);
        effect.Source = CaptureAttackSource();
        return effect;
    }

    /// <summary>
    /// 效果完成后解除所有权并归还，只处理本敌人仍拥有的实例。
    /// </summary>
    /// <param name="effect">已完成的效果。</param>
    protected void ReleaseSkillEffect(SkillEffectNode effect)
    {
        if (_skillEffects.Remove(effect))
        {
            NodePool.Instance.Release(effect);
        }
    }

    /// <summary>
    /// 提交出招：转向目标并装填攻击包，声音时点由动画方法轨道控制。
    /// </summary>
    /// <param name="index">本怪物攻击配置数组中的下标。</param>
    private void BeginConfiguredAttack(int index)
    {
        // 冷却已在AI接受请求时开始，身体消费请求不再次推迟到下一物理帧。
        FaceAttackTarget(index);
        AttackSegment = index;
        PrepareAttack(OwnAttacks[index]);
        Log.Debug("[Enemy] {0} 出招 段{1}", Id, index + 1);
    }

    /// <summary>
    /// 通常朝目标原点出招；保持原朝向的特殊招式由具体敌人决定。
    /// </summary>
    /// <param name="index">即将开始的招式下标。</param>
    protected virtual void FaceAttackTarget(int index) => SetFacing(Math.Sign(((IEnemyAiAgent)this).TargetOffset.X));

    /// <summary>
    /// 结束或打断出招并归还攻击包。
    /// </summary>
    private void EndCurrentAttack()
    {
        AttackSegment = -1;
        ReleaseAttack();
    }

    /// <summary>
    /// 更新当前招式的攻击编号，不重新计算冷却或切换身体状态。
    /// </summary>
    protected void RefreshAttack()
    {
        if (AttackSegment >= 0)
        {
            PrepareAttack(OwnAttacks[AttackSegment]);
        }
    }

    /// <summary>
    /// 由原生动画多段方法键刷新当前攻击包。
    /// </summary>
    private void NewAttackAnimEvent() => RefreshAttack();

}
