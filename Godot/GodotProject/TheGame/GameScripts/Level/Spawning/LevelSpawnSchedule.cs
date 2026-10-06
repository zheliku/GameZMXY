using GameConfig.Level;

namespace GameLogic.Level;

/// <summary>按游戏时间并行调度配方，统一预约并存名额，不创建实体或等待异步计时器。</summary>
public sealed class LevelSpawnSchedule
{
    private const double TimeTolerance = 0.000001; // 浮点游戏时钟的比较容差，单位为秒。
    private readonly LevelStage m_Stage; // 当前阶段的生成规则。
    private readonly int[] m_Remaining; // 每条配方尚未提交的实体数量。
    private readonly double[] m_NextTimes; // 每条配方下一次可提交的游戏时间。
    private double m_Time; // 当前阶段已经经过的游戏秒数。
    private int m_Cursor; // 轮转取配方的起始下标，避免前一配方占满并存名额。
    private int m_Pending; // 尚未提交的实体总数。

    /// <summary>创建阶段调度；首只怪物的等待时间为阶段延迟加配方延迟。</summary>
    /// <param name="stage">当前阶段的生成规则。</param>
    public LevelSpawnSchedule(LevelStage stage)
    {
        m_Stage = stage;
        m_Remaining = new int[stage.Recipes.Count];
        m_NextTimes = new double[stage.Recipes.Count];
        for (int i = 0; i < stage.Recipes.Count; i++)
        {
            m_Remaining[i] = stage.Recipes[i].Count;
            m_NextTimes[i] = stage.SpawnDelay + stage.Recipes[i].Delay;
            m_Pending += m_Remaining[i];
        }
    }

    /// <summary>已预约、正在显示或仍存活的实体数量。</summary>
    public int ActiveCount { get; private set; }

    /// <summary>配方全部提交且所有预约名额已归还。</summary>
    public bool IsCleared => m_Pending == 0 && ActiveCount == 0;

    /// <summary>推进游戏时间；暂停时调用方停止推进。</summary>
    /// <param name="delta">本帧经过的游戏秒数。</param>
    public void Advance(double delta) => m_Time += delta;

    /// <summary>为一条到期配方预约名额；同一帧可连续调用，让各配方并行提交。</summary>
    /// <param name="recipe">取出的到期配方；没有可提交配方时为 null。</param>
    /// <returns>取出配方时返回 true；并存已满或没有到期配方时返回 false。</returns>
    public bool TryTake(out LevelSpawnRecipe recipe)
    {
        recipe = null;
        if (ActiveCount >= m_Stage.MaxActive)
        {
            return false;
        }

        // 轮转选择到期配方；间隔按实际提交时刻计算，不在卡顿后补发大量实体。
        for (int n = 0; n < m_Remaining.Length; n++)
        {
            int i = (m_Cursor + n) % m_Remaining.Length;
            if (m_Remaining[i] == 0 || m_Time + TimeTolerance < m_NextTimes[i])
            {
                continue;
            }

            recipe = m_Stage.Recipes[i];
            m_Remaining[i]--;
            m_Pending--;
            ActiveCount++;
            m_NextTimes[i] = m_Time + recipe.Interval;
            m_Cursor = (i + 1) % m_Remaining.Length;
            return true;
        }

        return false;
    }

    /// <summary>实体死亡或显示失败时归还一次名额。</summary>
    public void Release() => ActiveCount--;
}
