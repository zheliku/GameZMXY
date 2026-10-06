using Godot;

/// <summary>远程热更新地址与补丁存储目录设置。</summary>
[GlobalClass]
public partial class UpdateSettingRes : Resource
{
    /// <summary>资源文件中更新设置字段的键名。</summary>
    public static class Parameters
    {
        /// <summary>远程服务器地址字段键名。</summary>
        public static string RemoteUrl = "RemoteUrl";
        /// <summary>补丁存储目录字段键名。</summary>
        public static string HotUpdatePath = "HotUpdatePath";
    }

    /// <summary>远程更新服务器地址</summary>
    [Export]
    public string RemoteUrl = "http://127.0.0.1:8080";

    /// <summary>
    /// 热更补丁存储目录（空 = 自动选择）。
    /// 自动选择策略：游戏安装目录可写 → 放游戏目录；不可写 → 放 user://
    /// </summary>
    [Export]
    public string HotUpdatePath = "";
}
