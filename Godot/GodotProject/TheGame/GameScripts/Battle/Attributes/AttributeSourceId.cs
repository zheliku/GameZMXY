using System;

namespace GameLogic.Battle.Attributes;

/// <summary>
/// 属性修正的来源标识：同一来源的全部修正一起登记、一起移除（如一件装备、一个 Buff 实例、等级成长）。
/// 按值比较；类别区分来源种类，编号区分同类的不同实例。
/// </summary>
public readonly record struct AttributeSourceId
{
    /// <summary>来源类别，如 Equipment、Buff；为空表示无效来源。</summary>
    public readonly string Category;

    /// <summary>同类来源中的实例编号，如装备实例 ID、Buff 实例 ID。</summary>
    public readonly long Id;

    /// <summary>创建来源标识。</summary>
    /// <param name="category">来源类别，不能为空。</param>
    /// <param name="id">同类来源中的实例编号。</param>
    /// <exception cref="ArgumentException">类别为空。</exception>
    public AttributeSourceId(string category, long id)
    {
        if (string.IsNullOrEmpty(category))
        {
            throw new ArgumentException("属性来源类别不能为空。", nameof(category));
        }

        Category = category;
        Id = id;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Category}#{Id}";
}
