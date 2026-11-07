using System;
using GameConfig.Equipment;
using Godot;

namespace GameLogic.UI.Widgets
{
    /// <summary>
    /// 装备品质在详情与出售菜单中的统一名称和颜色。
    /// </summary>
    public static class EquipmentQualityStyle
    {
        extension(EEquipmentQuality quality)
        {
            /// <summary>
            /// 返回品质的中文显示名。
            /// </summary>
            /// <returns>不含排版空格的品质名称。</returns>
            /// <exception cref="ArgumentOutOfRangeException">品质没有定义。</exception>
            public string DisplayName()
            {
                return quality switch
                {
                    EEquipmentQuality.Rough => "粗糙",
                    EEquipmentQuality.Common => "普通",
                    EEquipmentQuality.Excellent => "优秀",
                    EEquipmentQuality.Fine => "精良",
                    EEquipmentQuality.Epic => "史诗",
                    EEquipmentQuality.Legendary => "传说",
                    _ => throw new ArgumentOutOfRangeException(nameof(quality))
                };
            }

            /// <summary>
            /// 返回造梦系列的品质文字颜色。
            /// </summary>
            /// <returns>品质名称与装备名称共用的颜色。</returns>
            /// <exception cref="ArgumentOutOfRangeException">品质没有定义。</exception>
            public Color DisplayColor()
            {
                return quality switch
                {
                    EEquipmentQuality.Rough => new Color("cccccc"),
                    EEquipmentQuality.Common => Colors.White,
                    EEquipmentQuality.Excellent => new Color("00ff00"),
                    EEquipmentQuality.Fine => new Color("0000ff"),
                    EEquipmentQuality.Epic => new Color("660099"),
                    EEquipmentQuality.Legendary => new Color("ff9933"),
                    _ => throw new ArgumentOutOfRangeException(nameof(quality))
                };
            }
        }
    }
}
