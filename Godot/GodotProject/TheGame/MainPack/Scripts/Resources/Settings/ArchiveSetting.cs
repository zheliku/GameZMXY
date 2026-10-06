using Godot;

/// <summary>存档系统资源设置。</summary>
[GlobalClass]
public partial class ArchiveSetting : Resource
{
    /// <summary>存档设置在资源文件中的键名。</summary>
    public static class Parameters
    {
        /// <summary>是否启用 AES 加密的键名。</summary>
        public static readonly string EnableAesEncryption = "EnableAesEncryption";
        /// <summary>加密密钥的键名。</summary>
        public static readonly string KEY = "KEY";
        /// <summary>加密盐值的键名。</summary>
        public static readonly string Salt = "Salt";
    }
    /// <summary>存档目录名，位于用户数据目录下。</summary>
    [Export]
    public string Folder { get; set; } = "GameData";

    /// <summary>是否启用 AES 加密。</summary>
    [Export]
    public bool EnableAesEncryption { get; set; }

    /// <summary>存档加密密钥。</summary>
    [Export]
    public string KEY { get; set; } = "GodotGameFramework";

    /// <summary>存档加密盐值。</summary>
    [Export]
    public string Salt { get; set; } = "Rkb4jvUy/ye7Cd7k89QQgQ==";
}
