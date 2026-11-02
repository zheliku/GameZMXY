using GodotGameFramework;
using GameLogic.Config;
using GameLogic.Architecture;
using System;
using GameConfig.Equipment;
using GameLogic.Equipment;
using Godot;

namespace GameLogic.UI
{
    /// <summary>
    /// 被动装备格：接收图标和实例，显示实际属性提示；不查询档案、不执行穿戴。
    /// </summary>
    public partial class EquipmentCell : TextureRect, IController
    {
        [Export] private TextureRect m_Icon; // 当前格图标，空格时清除。
        [Export] private PackedScene m_TooltipScene; // 可编辑的外置详情布局。
        private EquipmentData m_EquipmentData; // 当前装备数据，供悬停详情读取。
        private EquipmentConfig m_Definition; // 本格当前显示的共享定义；图鉴没有实际装备实例。

        /// <summary>
        /// 玩家点击装备格；窗口决定这是穿戴还是卸装请求。
        /// </summary>
        public event Action<Guid> Clicked;

        /// <summary>
        /// 借用当前游戏流程的架构入口。
        /// </summary>
        /// <returns>所属流程 FSM 的架构。</returns>
        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GF.Fsm.GetArchitecture();
        }

        /// <summary>
        /// 只处理左键按下，不在控件中改变档案。
        /// </summary>
        /// <param name="inputEvent">Godot 分发给本格的鼠标事件。</param>
        public override void _GuiInput(InputEvent inputEvent)
        {
            if (m_EquipmentData != null && inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            {
                Clicked?.Invoke(m_EquipmentData.InstanceId);
                AcceptEvent();
            }
        }

        /// <summary>
        /// 替换当前格显示；空实例恢复为空格。
        /// </summary>
        /// <param name="data">本格装备实例或空。</param>
        /// <param name="icon">GGF 加载的图标或空。</param>
        public void Bind(EquipmentData data, Texture2D icon)
        {
            m_Icon.Texture = icon;
            m_EquipmentData = data;
            var definition = data == null ? null : this.GetModel<ConfigurationModel>().Tables.TbEquipmentConfig.Get(data.EquipmentId);
            m_Definition = definition;
            // 穿戴槽的独立材质清除底图题签，透明图标仍保留；背包格不配置该材质。
            if (Material is ShaderMaterial background)
            {
                background.SetShaderParameter("occupied", data != null);
            }

            TooltipText = definition?.NameCn ?? "";
            MouseDefaultCursorShape = data == null ? CursorShape.Arrow : CursorShape.PointingHand;
        }

        /// <summary>
        /// 显示只读图鉴图标，悬停查看属性范围，点击不执行背包交易。
        /// </summary>
        /// <param name="definition">本格装备定义。</param>
        /// <param name="icon">共用的透明背包图。</param>
        public void BindDefinition(EquipmentConfig definition, Texture2D icon)
        {
            m_EquipmentData = null;
            m_Definition = definition;
            m_Icon.Texture = icon;
            TooltipText = definition.NameCn;
            MouseDefaultCursorShape = CursorShape.Arrow;
        }

        /// <summary>
        /// 让 Godot 负责提示的屏幕边界定位；详情场景只显示注入的文字。
        /// </summary>
        /// <param name="forText">触发提示的装备名称。</param>
        /// <returns>本次提示的独立控件；空装备格返回 null，不显示提示。</returns>
        public override Control _MakeCustomTooltip(string forText)
        {
            if (m_Definition == null)
            {
                return null;
            }

            var tooltip = m_TooltipScene.Instantiate<EquipmentTooltip>();
            if (m_EquipmentData != null)
            {
                tooltip.Bind(m_EquipmentData, m_Definition);
            }
            else
            {
                tooltip.BindDefinition(m_Definition);
            }
            return tooltip;
        }
    }
}
