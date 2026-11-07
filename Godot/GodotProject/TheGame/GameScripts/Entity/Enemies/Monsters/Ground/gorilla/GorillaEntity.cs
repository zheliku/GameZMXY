using System;
using GameFramework.Entity;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Enemies.AI;
using GameLogic.Level;
using Godot;

namespace GameLogic.Enemies;

/// <summary>
/// 大猩猩：使用同一物种身份，只有花果山守关时采用本关专属生命和经验。
/// </summary>
public partial class GorillaEntity : AbstractGroundMonsterEntity
{
    private static readonly EnemyAiPreset s_preset = EnemyAiPresets.Patrol; // Monster3 的原点出招距离，守关倍率不改变AI。
    private const int HuaguoshanLevelId = 1; // 本物种守关规则只适用于花果山。
    [Export] private GorillaGuardianConfigResource _guardianConfig; // 独立守关数值；基础配置仍来自敌人表。

    /// <inheritdoc />
    public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
    {
        base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);
        if (_guardianConfig == null)
        {
            throw new InvalidOperationException("大猩猩必须绑定花果山守关配置资源。");
        }

        _guardianConfig.Validate();
    }

    /// <summary>
    /// Monster3 使用近战巡逻与追击；守关身份不改变行为。
    /// </summary>
    protected override EnemyAiPreset AiPreset => s_preset;

    /// <inheritdoc />
    protected override void ApplyConfigOverrides()
    {
        base.ApplyConfigOverrides();
        if (this.GetModel<LevelModel>()?.LevelId == HuaguoshanLevelId)
        {
            MaximumHp = _guardianConfig.MaxHp;
            ExperienceReward = _guardianConfig.Experience;
        }
    }
}
