namespace analysaves;

/// <summary>
/// 单首歌曲记录：[songId, difficulty, acc]
/// </summary>
public class SongRecord
{
    public int SongId { get; set; }
    public double Difficulty { get; set; }
    public double Acc { get; set; }
}

/// <summary>
/// CSV 中曲目名称与 ID 的映射
/// </summary>
public class SongInfo
{
    public int Id { get; set; }          // 整数 ID
    public string RawId { get; set; } = ""; // CSV 原始 6 位 ID
    public string Name { get; set; } = "";  // 曲名.作者
}

/// <summary>
/// 一个存档 = 一组 SongRecord
/// </summary>
public class SaveData
{
    public List<SongRecord> Songs { get; set; } = new();
}

/// <summary>
/// 歌曲分析的缓存文件格式
/// </summary>
public class SongCache
{
    public List<double> Accs { get; set; } = new();
}

/// <summary>
/// 定数分析的缓存文件格式
/// </summary>
public class DiffCache
{
    public int Sum { get; set; }
    public List<double> AvgAccs { get; set; } = new();
}
