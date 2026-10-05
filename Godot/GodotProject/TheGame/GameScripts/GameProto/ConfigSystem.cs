using System;
using Luban;
using GameConfig;
using Godot;
using GameFramework;
using GameConfig.Constant;



/// <summary>
/// 配置加载器。
/// </summary>
public class ConfigSystem
{
    private static ConfigSystem _instance; // 配置加载器的唯一实例。

    /// <summary>当前项目配置加载器实例。</summary>
    public static ConfigSystem Instance => _instance ??= new ConfigSystem();

    private bool _init = false; // 所有关联 Tables 是否已经构造。

    private Tables _tables; // 当前已加载的 Luban 总表对象。

    /// <summary>首次访问时延迟加载的 Luban 总表。</summary>
    public Tables Tables
    {
        get
        {
            if (!_init)
            {
                Load();
            }

            return _tables;
        }
    }


    /// <summary>
    /// 加载配置。
    /// </summary>
    public void Load()
    {
        _tables = new Tables(LoadByteBuf);
        _init = true;
    }

    private ByteBuf LoadByteBuf(string fileName) // 从项目内对应 bytes 资源读取一张 Luban 配置表。
    {
        // 根据统一资源路径模板定位数据文件，并一次性读取当前小型配置表。
        string path = Utility.Text.Format(GameFolderConstant.GameConfigs, fileName);
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        byte[] bytes = file?.GetBuffer((long)file.GetLength());

        // 空文件与路径错误都快速失败，避免构造出部分有效的 Tables。
        if (bytes == null || bytes.Length == 0)
        {
            throw new Exception($"Failed to load config file: res://DataTables/{file}");
        }
        return new ByteBuf(bytes);
    }
}
