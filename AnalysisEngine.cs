namespace analysaves;

/// <summary>
/// 统计分析引擎
/// </summary>
public class AnalysisEngine
{
    /// <summary>计算平均值</summary>
    public static double Average(List<double> values)
    {
        if (values.Count == 0) return 0;
        return values.Average();
    }

    /// <summary>计算中位数（假设列表可能未排序，先复制再排序）</summary>
    public static double Median(List<double> values)
    {
        if (values.Count == 0) return 0;
        var sorted = new List<double>(values);
        sorted.Sort();
        int n = sorted.Count;
        if (n % 2 == 1)
            return sorted[n / 2];
        else
            return (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
    }

    /// <summary>
    /// 获取中位数在已排序列表中的位置百分比（0-based position / (n-1) * 100）
    /// 如果有偶数个，取两个中间位置的平均值
    /// </summary>
    public static double MedianPositionPercent(List<double> sortedValues)
    {
        int n = sortedValues.Count;
        if (n == 0) return 0;
        if (n == 1) return 50; // 唯一条目在中间

        if (n % 2 == 1)
        {
            int pos = n / 2;
            return (double)pos / (n - 1) * 100.0;
        }
        else
        {
            int pos1 = n / 2 - 1;
            int pos2 = n / 2;
            double avgPos = (pos1 + pos2) / 2.0;
            return avgPos / (n - 1) * 100.0;
        }
    }

    /// <summary>计算 >= 某个值的数量和占比</summary>
    public static (int count, double ratio) AboveOrEqual(List<double> values, double threshold)
    {
        if (values.Count == 0) return (0, 0);
        int count = values.Count(v => v >= threshold);
        double ratio = (double)count / values.Count * 100.0;
        return (count, ratio);
    }

    /// <summary>计算 > 某个值的数量和占比</summary>
    public static (int count, double ratio) StrictlyAbove(List<double> values, double threshold)
    {
        if (values.Count == 0) return (0, 0);
        int count = values.Count(v => v > threshold);
        double ratio = (double)count / values.Count * 100.0;
        return (count, ratio);
    }

    /// <summary>计算 < 某个值的数量和占比</summary>
    public static (int count, double ratio) StrictlyBelow(List<double> values, double threshold)
    {
        if (values.Count == 0) return (0, 0);
        int count = values.Count(v => v < threshold);
        double ratio = (double)count / values.Count * 100.0;
        return (count, ratio);
    }

    /// <summary>从存档列表提取指定歌曲的所有 acc</summary>
    public static List<double> ExtractSongAccs(List<SaveData> saves, int songId)
    {
        var accs = new List<double>();
        int processed = 0;
        foreach (var save in saves)
        {
            processed++;
            var record = save.Songs.FirstOrDefault(s => s.SongId == songId);
            if (record != null)
                accs.Add(record.Acc);

            if (processed % 500 == 0)
                Console.WriteLine($"[PROGRESS] 已扫描 {processed}/{saves.Count} 个存档...");
        }
        return accs;
    }

    /// <summary>从存档列表提取指定定数的所有歌曲 acc，返回每个存档的平均 acc</summary>
    public static (int totalSongs, List<double> avgAccs) ExtractDiffAccs(List<SaveData> saves, double difficulty)
    {
        var avgAccs = new List<double>();
        int totalSongs = 0;
        int processed = 0;

        foreach (var save in saves)
        {
            processed++;
            var matching = save.Songs
                .Where(s => Math.Abs(s.Difficulty - difficulty) < 0.0001)
                .ToList();

            totalSongs += matching.Count;
            if (matching.Count > 0)
                avgAccs.Add(matching.Average(s => s.Acc));
            // 如果没有匹配的歌曲，该存档不贡献 avg（跳过）

            if (processed % 500 == 0)
                Console.WriteLine($"[PROGRESS] 已扫描 {processed}/{saves.Count} 个存档...");
        }

        return (totalSongs, avgAccs);
    }
}
