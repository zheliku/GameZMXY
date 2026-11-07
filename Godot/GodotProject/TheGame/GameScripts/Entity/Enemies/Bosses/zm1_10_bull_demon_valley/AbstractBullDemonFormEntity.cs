using System;
using System.Threading;
using System.Threading.Tasks;
using GameConfig.Entity;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies;

/// <summary>
/// 牛魔王前两形态拥有异步变身周期，原配方名额交给后继形态而不提前归还。
/// </summary>
public abstract partial class AbstractBullDemonFormEntity : AbstractBossEntity
{
    private EnemySpawn _spawn; // 本次显示的出生位置、会话交接与失败出口。
    private CancellationTokenSource _showCancellation; // 回池立即作废本形态的异步完成回调。

    /// <inheritdoc />
    protected override bool EncounterComplete => false;

    /// <summary>
    /// 具体形态选择下一具实体资源。
    /// </summary>
    protected abstract EntityId NextForm { get; }

    /// <summary>
    /// 原版变身后的固定世界出生原点。
    /// </summary>
    protected abstract Vector2 NextFormPosition { get; }

    /// <inheritdoc />
    public override void OnShow(object userData)
    {
        _spawn = (EnemySpawn)userData;
        if (_spawn.ContinueEncounterAsync == null)
        {
            throw new InvalidOperationException("牛魔王变身必须由配方会话提供后继所有权交接。");
        }

        _showCancellation = new CancellationTokenSource();
        base.OnShow(userData);
        // 死亡闪烁可能让上次显示的身体隐藏，原生RESET恢复所有表现默认值。
        AnimPlayer.Play("RESET");
        AnimPlayer.Advance(0);
    }

    /// <inheritdoc />
    public override void OnHide(bool isShutdown, object userData)
    {
        _showCancellation?.Cancel();
        _showCancellation?.Dispose();
        _showCancellation = null;
        _spawn = default;
        base.OnHide(isShutdown, userData);
    }

    /// <summary>
    /// 死亡状态在原生演出完成后提交下一形态，等待前捕获原显示周期。
    /// </summary>
    /// <param name="completed">只有原显示周期仍有效时通知原死亡状态。</param>
    internal void BeginTransformation(Action completed)
    {
        EnemySpawn spawn = _spawn with
        {
            Position = NextFormPosition
        };
        _ = TransformAsync(Id, NextForm, spawn, _showCancellation.Token, completed);
    }

    /// <summary>
    /// 后继由原关卡服务登记；异步失败回到原会话，不让战斗区永久锁住。
    /// </summary>
    /// <param name="previousId">死亡形态的运行编号。</param>
    /// <param name="nextForm">具体状态选择的后继身份。</param>
    /// <param name="spawn">等待前捕获的原配方参数。</param>
    /// <param name="token">原显示周期取消令牌。</param>
    /// <param name="completed">原死亡状态的完成通知。</param>
    /// <returns>后继登记任务。</returns>
    private static async Task TransformAsync(int previousId, EntityId nextForm, EnemySpawn spawn,
        CancellationToken token, Action completed)
    {
        try
        {
            await spawn.ContinueEncounterAsync(previousId, nextForm, spawn);
            if (!token.IsCancellationRequested)
            {
                completed();
            }
        }
        catch (Exception error)
        {
            if (!token.IsCancellationRequested)
            {
                Log.Error("[BullDemon] 变身失败：{0}", error);
                spawn.SpawnFailed?.Invoke(error);
            }
        }
    }
}
