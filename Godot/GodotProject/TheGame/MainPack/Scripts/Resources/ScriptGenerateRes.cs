using Calcatz.EzpzInspector;
using Godot;
using System;
/// <summary>编辑器脚本生成器的路径和命名设置。</summary>
[GlobalClass]
public partial class ScriptGenerateRes : Resource
{
    /// <summary>脚本生成器设置在资源文件中的键名。</summary>
    public static class Parameters
    {
        /// <summary>业务脚本命名空间键名。</summary>
        public static readonly string NameSpace = "NameSpace";
        /// <summary>字段前缀键名。</summary>
        public static readonly string NodePrefix = "NodePrefix";
        /// <summary>UI 生成代码输出路径键名。</summary>
        public static readonly string UIOutPutPathGe = "UIOutPutPathGe";
        /// <summary>UI 手写逻辑输出路径键名。</summary>
        public static readonly string UIOutPutPathLogic = "UIOutPutPathLogic";
        /// <summary>实体生成代码输出路径键名。</summary>
        public static readonly string EntityOutPutPathGe = "EntityOutPutPathGe";
        /// <summary>实体手写逻辑输出路径键名。</summary>
        public static readonly string EntityOutPutPathLogic = "EntityOutPutPathLogic";
    }
    /// <summary>生成脚本使用的命名空间。</summary>
    [Export]
    public string NameSpace = "GameLogic";
    /// <summary>生成字段使用的前缀。</summary>
    [Export]
    public string NodePrefix = "m_";
    /// <summary>UI 生成代码输出目录。</summary>
    [UpperDescription("UI")]
    [Export(PropertyHint.Dir)]
    public string UIOutPutPathGe = "res://TheGame/GameScripts/GameProto/UIGe/";
    /// <summary>UI 手写逻辑输出目录。</summary>
    [Export(PropertyHint.Dir)]
    public string UIOutPutPathLogic = "res://TheGame/GameScripts/UI/";
    /// <summary>实体生成代码输出目录。</summary>
    [UpperDescription("Entity")]
    [Export(PropertyHint.Dir)]
    public string EntityOutPutPathGe = "res://TheGame/GameScripts/GameProto/EntityGe/";
    /// <summary>实体手写逻辑输出目录。</summary>
    [Export(PropertyHint.Dir)]
    public string EntityOutPutPathLogic = "res://TheGame/GameScripts/Entity/";

}
