using System;
using System.Collections.Generic;
using GameConfig.Level;

namespace GameLogic.Level;

/// <summary>维护唯一当前阶段和合法迁移，不承担相机、节点或实体职责。</summary>
public sealed class LevelStageSequence
{
    private readonly IReadOnlyList<LevelStage> m_Stages; // 初始化时校验过的阶段列表。
    private int m_Index; // 唯一当前阶段的列表索引。

    /// <summary>校验阶段编排并创建尚未开始的会话。</summary>
    /// <param name="stages">关卡配置中按顺序排列的阶段列表。</param>
    /// <exception cref="InvalidOperationException">阶段为空，或顺序、并存上限、延迟、激活方式非法时抛出。</exception>
    public LevelStageSequence(IReadOnlyList<LevelStage> stages)
    {
        if (stages.Count == 0)
        {
            throw new InvalidOperationException("关卡必须至少包含一个阶段。");
        }

        // 结构错误只在配置进入运行时的边界报告。
        for (int i = 0; i < stages.Count; i++)
        {
            LevelStage stage = stages[i];
            if (stage.StageOrder != i + 1 || stage.MaxActive <= 0 ||
                !float.IsFinite(stage.SpawnDelay) || stage.SpawnDelay < 0f)
            {
                throw new InvalidOperationException($"阶段 {i + 1} 的顺序、并存上限或延迟非法。");
            }

            bool hasTrigger = !string.IsNullOrWhiteSpace(stage.TriggerId);
            bool validActivation = stage.Activation switch
            {
                StageActivation.CameraArrived => !hasTrigger,
                StageActivation.Trigger => hasTrigger,
                _ => false,
            };
            if (!validActivation)
            {
                throw new InvalidOperationException($"阶段 {i + 1} 的激活方式与 TriggerId 不匹配。");
            }
        }

        m_Stages = stages;
    }

    /// <summary>当前阶段；完成后仍保留最后阶段供结算读取。</summary>
    public LevelStage Current => m_Stages[m_Index];

    /// <summary>当前会话阶段状态。</summary>
    public LevelStagePhase Phase { get; private set; } = LevelStagePhase.Ready;

    /// <summary>在加载界面关闭之后开放阶段推进。</summary>
    /// <exception cref="InvalidOperationException">会话不处于 Ready 状态时抛出。</exception>
    public void Begin() => ChangePhase(LevelStagePhase.Ready, LevelStagePhase.Travelling);

    /// <summary>当前阶段满足启动条件，进入战斗。</summary>
    /// <exception cref="InvalidOperationException">会话不处于 Travelling 状态时抛出。</exception>
    public void StartBattle() => ChangePhase(LevelStagePhase.Travelling, LevelStagePhase.Fighting);

    /// <summary>清波后推进一个阶段；最后阶段清除则完成关卡。</summary>
    /// <exception cref="InvalidOperationException">会话不处于 Fighting 状态时抛出。</exception>
    public void ClearBattle()
    {
        ChangePhase(LevelStagePhase.Fighting, LevelStagePhase.Completed);
        if (m_Index + 1 < m_Stages.Count)
        {
            m_Index++;
            Phase = LevelStagePhase.Travelling;
        }
    }

    /// <summary>停止会话，禁止继续推进。</summary>
    public void Stop() => Phase = LevelStagePhase.Stopped;

    /// <summary>执行一次显式状态迁移。</summary>
    /// <param name="expected">调用方必须处于的状态。</param>
    /// <param name="next">迁移后的状态。</param>
    /// <exception cref="InvalidOperationException">当前状态与 expected 不符时抛出。</exception>
    private void ChangePhase(LevelStagePhase expected, LevelStagePhase next)
    {
        // 用显式状态表达调用契约，不根据依赖字段是否为空猜测就绪。
        if (Phase != expected)
        {
            throw new InvalidOperationException($"阶段迁移非法：{Phase} → {next}，应从 {expected} 开始。");
        }

        Phase = next;
    }
}
