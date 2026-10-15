using GameConfig.Attributes;
using System;
using GameConfig.Equipment;

namespace GameLogic.Equipment;

/// <summary>一件已经抽取属性的装备；编号和实际修正不随读档、查看或拾取重抽。</summary>
public sealed class EquipmentInstance
{
    /// <summary>唯一实例编号，不同的同名装备也各占一个格子。</summary>
    public Guid InstanceId { get; }

    /// <summary>只读装备定义的编号。</summary>
    public EquipmentId EquipmentId { get; }

    /// <summary>本件装备已确定的实际属性；配置范围不进入实例。</summary>
    public EquipmentAttributes Attributes { get; }

    /// <summary>从已校验的定义或存档创建独立实例。</summary>
    /// <param name="instanceId">非空实例编号。</param>
    /// <param name="equipmentId">装备定义编号。</param>
    /// <param name="attributes">本件已抽出的不可变具名属性。</param>
    /// <exception cref="ArgumentException">编号无效。</exception>
    /// <exception cref="ArgumentNullException">属性集合为空。</exception>
    public EquipmentInstance(Guid instanceId, EquipmentId equipmentId, EquipmentAttributes attributes)
    {
        if (instanceId == Guid.Empty || equipmentId == EquipmentId.None)
            throw new ArgumentException("装备实例需要非空编号与装备定义。");
        ArgumentNullException.ThrowIfNull(attributes);
        InstanceId = instanceId;
        EquipmentId = equipmentId;
        Attributes = attributes;
    }

    /// <summary>校验存档中实际属性仍对应当前定义，不在读档时重抽或悄悄更换装备。</summary>
    /// <param name="definition">当前装备定义。</param>
    /// <param name="instanceId">保存的实例编号。</param>
    /// <param name="attributes">保存的实际属性。</param>
    /// <returns>已经验证的装备实例。</returns>
    /// <exception cref="ArgumentException">定义不存在或属性不符合定义。</exception>
    public static EquipmentInstance FromSaveData(EquipmentConfig definition, Guid instanceId,
        EquipmentAttributes attributes)
    {
        if (definition == null || attributes == null)
            throw new ArgumentException("装备定义或实际属性不存在。");
        foreach (AttributeType attribute in Enum.GetValues<AttributeType>())
        {
            var expected = definition.AttributeRanges.Find(x => x.Attribute == attribute);
            int value = attributes.Get(attribute);
            if (expected == null ? value != 0 : value < expected.Minimum || value > expected.Maximum)
                throw new ArgumentException("装备实例属性不符合当前配置。");
        }

        return new EquipmentInstance(instanceId, definition.Id, attributes);
    }
}
