using Godot;
using System;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;
using GameFramework.Resource;
using GodotGameFramework.Archive;

namespace GodotGameFramework.Json;

/// <summary>
/// 轻量级 JSON 持久化工具。
/// 所有路径在使用前通过 ProjectSettings.GlobalizePath 解析。
/// 注意：Godot 文件 API（FileAccess）不是线程安全的，
/// 因此异步方法内部使用纯 .NET 的 StreamWriter/StreamReader + Task.Run。
/// </summary>
public static class EasySave
{
    private static readonly string s_UserDir = ProjectSettings.GlobalizePath("user://");
    private static readonly string s_ProjectDir = ProjectSettings.GlobalizePath("res://");

    // ──────────────────────────
    //  同步方法（主线程安全）
    // ──────────────────────────

    public static bool TrySave<T>(T data, string filePath)
    {
        try
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
            writer.Write(json);
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasySave] 保存失败: {filePath} — {ex.Message}");
            return false;
        }
    }

    public static T LoadOrDefault<T>(string filePath) where T : class, new()
    {
        try
        {
            if (!File.Exists(filePath))
                return null;

            using var reader = new StreamReader(filePath, System.Text.Encoding.UTF8);
            string json = reader.ReadToEnd();
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasySave] 加载失败: {filePath} — {ex.Message}");
            return null;
        }
    }

    public static bool TryDelete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasySave] 删除失败: {filePath} — {ex.Message}");
            return false;
        }
    }

    public static bool FileExists(string filePath) => File.Exists(filePath);

    // ──────────────────────────
    //  user:// 便捷方法
    // ──────────────────────────

    public static bool SaveInUser<T>(T data, string fileName) =>
        TrySave(data, Path.Combine(s_UserDir, fileName));

    public static T LoadFromUser<T>(string fileName) where T : class, new() =>
        LoadOrDefault<T>(Path.Combine(s_UserDir, fileName));

    public static bool DeleteInUser(string fileName) =>
        TryDelete(Path.Combine(s_UserDir, fileName));

    public static bool ExistsInUser(string fileName) =>
        File.Exists(Path.Combine(s_UserDir, fileName));



    // ──────────────────────────
    //  res:// 便捷方法
    // ──────────────────────────

    public static bool SaveInProject<T>(T data, string fileName) =>
        TrySave(data, Path.Combine(s_ProjectDir, fileName));

    public static T LoadFromProject<T>(string fileName) where T : class, new() =>
        LoadOrDefault<T>(Path.Combine(s_ProjectDir, fileName));

    public static bool DeleteInProject(string fileName) =>
        TryDelete(Path.Combine(s_ProjectDir, fileName));
    public static bool ExistsInProject(string fileName) =>
            File.Exists(Path.Combine(s_ProjectDir, fileName));


    // ──────────────────────────
    //  异步方法（用于非 Godot 线程的调用场景）
    // ──────────────────────────

    public static async Task<bool> SaveInUserAsync<T>(T data, string fileName)
    {
        string path = Path.Combine(s_UserDir, fileName);
        try
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);

            await Task.Run(() =>
            {
                using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
                writer.Write(json);
            });
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasySave] 异步保存失败: {path} — {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> SaveInUserAsync<T>(T data, string fileName, bool encrypt, string key, string salt)
    {
        string path = Path.Combine(s_UserDir, fileName);
        try
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = encrypt ? Rijindael.Encrypt(JsonConvert.SerializeObject(data, Formatting.Indented), key, salt) : JsonConvert.SerializeObject(data, Formatting.Indented);
            await Task.Run(() =>
            {
                using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
                writer.Write(json);
            });
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasySave] 异步保存失败: {path} — {ex.Message}");
            return false;
        }
    }

    public static async Task<T> LoadFromUserAsync<T>(string fileName) where T : class, new()
    {
        string path = Path.Combine(s_UserDir, fileName);
        try
        {
            return await Task.Run(() =>
            {
                if (!File.Exists(path)) return null;
                using var reader = new StreamReader(path, System.Text.Encoding.UTF8);
                string json = reader.ReadToEnd();
                return JsonConvert.DeserializeObject<T>(json);
            });
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasySave] 异步加载失败: {path} — {ex.Message}");
            return null;
        }
    }

    public static async Task<T> LoadFromUserAsync<T>(string fileName, bool encrypt, string key, string salt) where T : class, new()
    {
        string path = Path.Combine(s_UserDir, fileName);
        try
        {
            return await Task.Run(() =>
            {
                if (!File.Exists(path)) return null;
                using var reader = new StreamReader(path, System.Text.Encoding.UTF8);
                string json = encrypt ? Rijindael.Decrypt(reader.ReadToEnd(), key, salt) : reader.ReadToEnd();
                return JsonConvert.DeserializeObject<T>(json);
            });
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasySave] 异步加载失败: {path} — {ex.Message}");
            return null;
        }
    }

    public static async Task<bool> DeleteInUserAsync(string fileName)
    {
        string path = Path.Combine(s_UserDir, fileName);
        try
        {
            await Task.Run(() =>
            {
                if (File.Exists(path))
                    File.Delete(path);
            });
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasySave] 异步删除失败: {path} — {ex.Message}");
            return false;
        }
    }

    // ──────────────────────────
    //  原子写与备份（存档系统使用；2026-10 项目登记的框架例外）
    // ──────────────────────────

    /// <summary>备份文件后缀：原子替换时保留上一份完整内容。</summary>
    public const string BackupSuffix = ".bak";

    /// <summary>临时文件后缀：写完整后才替换目标。</summary>
    private const string TempSuffix = ".tmp";

    /// <summary>
    /// 在调用线程上序列化（可选加密）。存档系统在主线程调用，快照在调用时刻即固定，
    /// 之后再改数据对象不会影响已排队的写入。
    /// </summary>
    /// <param name="data">要序列化的数据。</param>
    /// <param name="encrypt">是否 AES 加密。</param>
    /// <param name="key">加密密钥。</param>
    /// <param name="salt">加密盐值。</param>
    /// <returns>待写入的文本。</returns>
    public static string Serialize<T>(T data, bool encrypt, string key, string salt)
    {
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        return encrypt ? Rijindael.Encrypt(json, key, salt) : json;
    }

    /// <summary>
    /// 原子写入 user:// 下的文件：先完整写入 <c>.tmp</c>，目标已存在时用 File.Replace 替换并把旧内容留作 <c>.bak</c>，
    /// 否则直接移动到位。任何一步失败都删除临时文件并返回 false，目标文件保持原样。
    /// </summary>
    /// <param name="fileName">相对 user:// 的文件路径。</param>
    /// <param name="text">已序列化的完整内容。</param>
    /// <returns>是否成功写入。</returns>
    public static async Task<bool> WriteUserTextAtomicAsync(string fileName, string text)
    {
        string path = Path.Combine(s_UserDir, fileName);
        string temp = path + TempSuffix;
        try
        {
            await Task.Run(() =>
            {
                // 临时文件与目标同目录（同一卷），File.Replace/Move 才是原子的。
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (var writer = new StreamWriter(temp, false, System.Text.Encoding.UTF8))
                {
                    writer.Write(text);
                    writer.Flush();
                    ((FileStream)writer.BaseStream).Flush(true);
                }

                if (File.Exists(path))
                    File.Replace(temp, path, path + BackupSuffix, ignoreMetadataErrors: true);
                else
                    File.Move(temp, path);
            });
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasySave] 原子写入失败: {path} — {ex.Message}");
            try
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
            catch (Exception cleanup)
            {
                GD.PrintErr($"[EasySave] 清理临时文件失败: {temp} — {cleanup.Message}");
            }
            return false;
        }
    }

    /// <summary>
    /// 读取 user:// 下的文件；主文件不存在或无法解析时尝试 <c>.bak</c>。
    /// </summary>
    /// <param name="fileName">相对 user:// 的文件路径。</param>
    /// <param name="encrypt">是否 AES 加密。</param>
    /// <param name="key">加密密钥。</param>
    /// <param name="salt">加密盐值。</param>
    /// <returns>读取结果；两份都不可用时 Data 为 null。FromBackup 表示数据来自备份。</returns>
    public static async Task<(T Data, bool FromBackup)> LoadFromUserWithBackupAsync<T>(string fileName, bool encrypt,
        string key, string salt) where T : class, new()
    {
        string path = Path.Combine(s_UserDir, fileName);
        return await Task.Run(() =>
        {
            T primary = TryRead<T>(path, encrypt, key, salt);
            if (primary != null)
                return (primary, false);

            T backup = TryRead<T>(path + BackupSuffix, encrypt, key, salt);
            return (backup, backup != null);
        });
    }

    /// <summary>主文件或备份文件存在其一。</summary>
    /// <param name="fileName">相对 user:// 的文件路径。</param>
    /// <returns>是否存在可尝试读取的文件。</returns>
    public static bool ExistsInUserOrBackup(string fileName)
    {
        string path = Path.Combine(s_UserDir, fileName);
        return File.Exists(path) || File.Exists(path + BackupSuffix);
    }

    /// <summary>删除 user:// 下的文件及其备份与临时文件。</summary>
    /// <param name="fileName">相对 user:// 的文件路径。</param>
    /// <returns>是否全部删除成功（文件不存在视为成功）。</returns>
    public static async Task<bool> DeleteInUserWithBackupAsync(string fileName)
    {
        string path = Path.Combine(s_UserDir, fileName);
        try
        {
            await Task.Run(() =>
            {
                foreach (string candidate in new[] { path, path + BackupSuffix, path + TempSuffix })
                {
                    if (File.Exists(candidate))
                        File.Delete(candidate);
                }
            });
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasySave] 删除失败: {path} — {ex.Message}");
            return false;
        }
    }

    /// <summary>读取并解析单个文件；不存在、解密或解析失败都返回 null 并记日志。</summary>
    private static T TryRead<T>(string path, bool encrypt, string key, string salt) where T : class, new()
    {
        try
        {
            if (!File.Exists(path))
                return null;

            using var reader = new StreamReader(path, System.Text.Encoding.UTF8);
            string content = reader.ReadToEnd();
            string json = encrypt ? Rijindael.Decrypt(content, key, salt) : content;
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasySave] 读取失败: {path} — {ex.Message}");
            return null;
        }
    }

}
