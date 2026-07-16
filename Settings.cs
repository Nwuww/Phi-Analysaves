namespace analysaves;

/// <summary>
/// 全局环境设置
/// </summary>
public class Settings
{
    /// <summary>存档文件路径（空字符串 = 自动发现）</summary>
    public string SavePath { get; set; } = "";

    /// <summary>分析结果导出路径</summary>
    public string OutPath { get; set; } = Path.Combine("export", "out.txt");

    /// <summary>检索深度，null 表示 max（全部）</summary>
    public int? Depth { get; set; } = null;

    /// <summary>缓存目录</summary>
    public string TmpDir => "tmp";

    /// <summary>num.csv 回退路径</summary>
    public string CsvFallbackPath => Path.Combine("..", "list.csv");

    /// <summary>曲目列表 CSV 路径（空字符串 = 自动发现）</summary>
    public string SongListPath { get; set; } = "";

    // ---- 默认搜索目录 ----
    public string SaveDir => "save";
    public string SongListDir => "songlist";

    /// <summary>获取实际存档路径（自动发现或已设置）</summary>
    public string ActiveSavePath
    {
        get
        {
            if (!string.IsNullOrEmpty(SavePath) && File.Exists(SavePath))
                return SavePath;
            var discovered = DiscoverFirst(SaveDir, "*.json");
            return discovered ?? SavePath; // 回退到原始值（即使不存在）
        }
    }

    /// <summary>获取实际曲目 CSV 路径</summary>
    public string ActiveCsvPath
    {
        get
        {
            if (!string.IsNullOrEmpty(SongListPath) && File.Exists(SongListPath))
                return SongListPath;
            var discovered = DiscoverFirst(SongListDir, "*.csv");
            if (discovered != null) return discovered;
            if (File.Exists(CsvFallbackPath)) return CsvFallbackPath;
            return SongListPath; // 回退
        }
    }

    // ---- 文件发现工具 ----

    /// <summary>扫描目录下匹配 pattern 的第一个文件，null 表示无</summary>
    public static string? DiscoverFirst(string directory, string pattern)
    {
        if (!Directory.Exists(directory)) return null;
        var files = Directory.GetFiles(directory, pattern);
        return files.Length > 0 ? files[0] : null;
    }

    /// <summary>扫描目录下匹配 pattern 的所有文件</summary>
    public static List<string> DiscoverAll(string directory, string pattern)
    {
        if (!Directory.Exists(directory)) return new List<string>();
        return Directory.GetFiles(directory, pattern).ToList();
    }

    /// <summary>重置为默认值</summary>
    public void Reset()
    {
        SavePath = "";
        OutPath = Path.Combine("export", "out.txt");
        Depth = null;
        SongListPath = "";
    }
}
