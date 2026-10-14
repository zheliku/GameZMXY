using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig;
using GameConfig.Skills;
using GameConfig.Sound;
using Godot;

namespace GameLogic.Skills;

/// <summary>在启动边界校验技能动作、独立特效与伤害阶段的配置关联；不执行施放。</summary>
public static class SkillCombatConfigHelper
{
    /// <summary>汇总已准备技能的资源声明、秒制时间、威力和跨表引用错误。</summary>
    /// <param name="tables">与游戏共用的正式配置表。</param>
    /// <param name="errors">预加载阶段持有的错误集合。</param>
    public static void Validate(Tables tables, ICollection<string> errors)
    {
        // 伤害键独立于动作；火魔三个阶段和唐僧追加释放各有明确的定义。
        foreach (var hit in tables.TbSkillHitConfig.DataList)
        {
            if (hit.Id == SkillHitId.None || !Enum.IsDefined(hit.Id) ||
                tables.TbSkillConfig.GetOrDefault(hit.SkillId) == null ||
                string.IsNullOrWhiteSpace(hit.NameCn) || string.IsNullOrWhiteSpace(hit.LegacyId) ||
                !Enum.IsDefined(hit.PowerRounding) || !Enum.IsDefined(hit.DamageKind) ||
                !IsNonnegative(hit.PowerScale) || !IsNonnegative(hit.FlatPower) ||
                hit.PowerScale + hit.FlatPower <= 0 || !IsFinite(hit.Knockback) ||
                hit.HitBudget <= 0 || !float.IsFinite(hit.RepeatIntervalSeconds) || hit.RepeatIntervalSeconds <= 0)
                errors.Add($"技能伤害 {hit.Id}：身份、威力、击退、总命中预算或重复间隔无效。");
        }

        // 本轮只准备悟空动作；唐僧目录与数值可存在，而不伪造尚未迁入的资源。
        foreach (var skill in tables.TbSkillConfig.DataList.Where(x => x.Hero == SkillHero.Wukong))
            if (tables.TbSkillActionConfig.GetOrDefault(skill.Id) == null)
                errors.Add($"技能 {skill.Id}：缺少本轮要求的悟空动作定义。");
        foreach (var action in tables.TbSkillActionConfig.DataList)
        {
            var skill = tables.TbSkillConfig.GetOrDefault(action.SkillId);
            if (skill == null || action.NameCn != skill.NameCn || string.IsNullOrWhiteSpace(action.LegacyId) ||
                !IsResourcePath(action.AnimationLibraryPath, ".tres") || string.IsNullOrWhiteSpace(action.Animation) ||
                !IsNonnegative(action.InvincibleDelaySeconds) || !IsNonnegative(action.InvincibleDurationSeconds) ||
                !IsNonnegative(action.CancelNormalAttackFrameRatio) || action.CancelNormalAttackFrameRatio > 1 ||
                (action.InvincibleDurationSeconds == 0 && action.InvincibleDelaySeconds != 0))
                errors.Add($"技能动作 {action.SkillId}：身份、动画资源或无敌时间无效。");
            ValidateHitReference(tables, action.BodyHitId, action.SkillId, $"技能动作 {action.SkillId}", errors);
            ValidateSound(tables, action.SoundId, $"技能动作 {action.SkillId}", errors);
        }

        foreach (var effect in tables.TbSkillEffectConfig.DataList)
        {
            if (effect.Id == SkillEffectId.None || !Enum.IsDefined(effect.Id) ||
                tables.TbSkillActionConfig.GetOrDefault(effect.SkillId) == null ||
                string.IsNullOrWhiteSpace(effect.NameCn) || string.IsNullOrWhiteSpace(effect.LegacyId) ||
                !IsResourcePath(effect.ScenePath, ".tscn") || !IsFinite(effect.SpawnOffset) ||
                !IsPositiveScale(effect.Scale) || !IsPositiveScale(effect.MusouScale) || !Enum.IsDefined(effect.Mirror))
                errors.Add($"技能特效 {effect.Id}：身份、场景、偏移、缩放或朝向无效。");
            ValidateHitReference(tables, effect.HitId, effect.SkillId, $"技能特效 {effect.Id}", errors);
            ValidateSound(tables, effect.SoundId, $"技能特效 {effect.Id}", errors);
        }
        ValidateTimelines(tables, errors);
    }

    /// <summary>检查准备时间轴的所有者、局部秒数、分支与各类事件的具名参数。</summary>
    /// <param name="tables">正式技能表。</param>
    /// <param name="errors">启动错误集合。</param>
    private static void ValidateTimelines(Tables tables, ICollection<string> errors)
    {
        // 时间以角色或独立特效为原点；同时间的多条事件保持源表顺序。
        foreach (var group in tables.TbSkillTimelineConfig.DataList.GroupBy(x => (x.SkillId, x.SourceEffectId)))
        {
            float previous = 0;
            foreach (var entry in group)
            {
                if (entry.AtSeconds < previous)
                    errors.Add($"技能事件 {entry.Id}：所属时间轴必须按秒数递增排列。");
                previous = entry.AtSeconds;
            }
        }
        foreach (var action in tables.TbSkillActionConfig.DataList)
            if (!tables.TbSkillTimelineConfig.DataList.Any(x => x.SkillId == action.SkillId &&
                x.SourceEffectId == SkillEffectId.None && x.Kind == SkillTimelineKind.CompleteActor))
                errors.Add($"技能动作 {action.SkillId}：缺少角色时间轴结束事件。");

        foreach (var entry in tables.TbSkillTimelineConfig.DataList)
        {
            if (tables.TbSkillActionConfig.GetOrDefault(entry.SkillId) == null || string.IsNullOrWhiteSpace(entry.LegacyId) ||
                !Enum.IsDefined(entry.Kind) || !Enum.IsDefined(entry.Condition) || !Enum.IsDefined(entry.Direction) ||
                !Enum.IsDefined(entry.Anchor) || !IsNonnegative(entry.AtSeconds) || !IsNonnegative(entry.UntilSeconds) ||
                !IsNonnegative(entry.SeekSeconds) || !IsNonnegative(entry.XMultiplier) ||
                !IsNonnegative(entry.TargetRange) || !IsNonnegative(entry.ViewportInset) || !IsNonnegative(entry.ActionLockSeconds) ||
                !IsFinite(entry.Velocity) || !IsFinite(entry.MusouVelocity) || entry.Velocity.X < 0 || entry.MusouVelocity.X < 0)
                errors.Add($"技能事件 {entry.Id}：身份、局部秒数、条件或位移参数无效。");
            ValidateEffectReference(tables, entry.SourceEffectId, entry.SkillId, entry.Id, errors);
            ValidateEffectReference(tables, entry.EffectId, entry.SkillId, entry.Id, errors);

            bool spawn = entry.Kind is SkillTimelineKind.SpawnEffect or SkillTimelineKind.SpawnLandingAndPauseActor;
            bool seek = entry.Kind is SkillTimelineKind.SeekActor or SkillTimelineKind.FinishLandingEffect;
            bool search = entry.Kind == SkillTimelineKind.SearchTarget;
            bool landing = entry.Kind == SkillTimelineKind.SpawnLandingAndPauseActor;
            bool motion = entry.Kind is SkillTimelineKind.SetVelocity or SkillTimelineKind.SetVelocityX;
            if (spawn != (entry.EffectId != SkillEffectId.None) || seek != (entry.SeekSeconds > 0) ||
                search != (entry.TargetRange > 0 && entry.ViewportInset > 0) ||
                (!search && (entry.TargetRange != 0 || entry.ViewportInset != 0)) ||
                (landing ? entry.UntilSeconds < entry.AtSeconds || entry.Condition != SkillTimelineCondition.OnFloor : entry.UntilSeconds != 0) ||
                (entry.Kind == SkillTimelineKind.MultiplyVelocityX ? entry.XMultiplier <= 0 : entry.XMultiplier != 0) ||
                (!motion && (entry.Velocity != Vector2.Zero || entry.MusouVelocity != Vector2.Zero)) ||
                (entry.Kind != SkillTimelineKind.TeleportToTarget && entry.ActionLockSeconds != 0))
                errors.Add($"技能事件 {entry.Id}：参数与事件职责不匹配。");
            if ((entry.Kind is SkillTimelineKind.CompleteEffect or SkillTimelineKind.FinishLandingEffect) &&
                entry.SourceEffectId == SkillEffectId.None)
                errors.Add($"技能事件 {entry.Id}：独立特效结束必须声明所属特效。");
        }
    }

    /// <summary>保证事件的来源或生成特效属于同一技能。</summary>
    /// <param name="tables">正式配置。</param>
    /// <param name="effectId">来源或生成特效，None 表示角色或不生成。</param>
    /// <param name="skillId">事件所属技能。</param>
    /// <param name="eventId">错误信息中的事件键。</param>
    /// <param name="errors">启动错误集合。</param>
    private static void ValidateEffectReference(Tables tables, SkillEffectId effectId, SkillId skillId,
        string eventId, ICollection<string> errors)
    {
        if (effectId == SkillEffectId.None)
            return;
        var effect = tables.TbSkillEffectConfig.GetOrDefault(effectId);
        if (effect == null || effect.SkillId != skillId)
            errors.Add($"技能事件 {eventId}：特效 {effectId} 不存在或不属于该技能。");
    }

    /// <summary>校验可选伤害引用属于同一技能，防止动作与特效误用其他招式的伤害。</summary>
    /// <param name="tables">正式配置表。</param>
    /// <param name="hitId">None 表示当前来源只负责表现。</param>
    /// <param name="skillId">拥有该动作或特效的技能。</param>
    /// <param name="owner">错误信息中的来源名称。</param>
    /// <param name="errors">启动错误集合。</param>
    private static void ValidateHitReference(Tables tables, SkillHitId hitId, SkillId skillId,
        string owner, ICollection<string> errors)
    {
        if (hitId == SkillHitId.None)
            return;
        var hit = tables.TbSkillHitConfig.GetOrDefault(hitId);
        if (hit == null || hit.SkillId != skillId)
            errors.Add($"{owner}：伤害阶段 {hitId} 不存在或不属于该技能。");
    }

    /// <summary>技能声音必须引用 GGF 的 SFX 组；None 不创建播放请求。</summary>
    /// <param name="tables">正式配置表。</param>
    /// <param name="soundId">配置声音标识。</param>
    /// <param name="owner">错误信息中的来源名称。</param>
    /// <param name="errors">启动错误集合。</param>
    private static void ValidateSound(Tables tables, SoundId soundId, string owner, ICollection<string> errors)
    {
        if (soundId == SoundId.None)
            return;
        var sound = tables.TbSoundConfig.GetOrDefault((int)soundId);
        if (sound == null || sound.Group != "SFX")
            errors.Add($"{owner}：声音 {soundId} 必须引用 GGF SFX 配置。");
    }

    /// <summary>检查资源路径的协议和类型；资源能否加载由引擎装配验证负责。</summary>
    /// <param name="path">表中声明的资源路径。</param>
    /// <param name="extension">要求的文件扩展名。</param>
    /// <returns>是否是对应类型的 Godot 项目资源路径。</returns>
    private static bool IsResourcePath(string path, string extension) =>
        path.StartsWith("res://", StringComparison.Ordinal) && path.EndsWith(extension, StringComparison.Ordinal);

    /// <summary>检查有限非负的秒数或威力。</summary>
    /// <param name="value">待检查值。</param>
    /// <returns>值是否有限且非负。</returns>
    private static bool IsNonnegative(float value) => float.IsFinite(value) && value >= 0;

    /// <summary>检查向量两个分量均为有限值。</summary>
    /// <param name="value">偏移或击退向量。</param>
    /// <returns>是否不含非数值与无穷大。</returns>
    private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);

    /// <summary>检查独立特效的实际缩放，镜像由独立朝向字段表达。</summary>
    /// <param name="value">普通或无双缩放。</param>
    /// <returns>两个分量是否均为有限正数。</returns>
    private static bool IsPositiveScale(Vector2 value) => IsFinite(value) && value.X > 0 && value.Y > 0;
}
