using System.Text.Json;

namespace analysaves;

/// <summary>
/// 数据加载服务：存档文件、CSV 映射、缓存读写
/// </summary>
public class DataLoader
{
    private readonly Settings _settings;
    private List<SaveData>? _saves;
    private List<SongInfo>? _songInfos;

    public DataLoader(Settings settings)
    {
        _settings = settings;
    }

    // ==================== 存档加载 ====================

    /// <summary>
    /// 加载存档文件。返回存档数量；-1 表示文件不存在或格式错误。
    /// 结果缓存在 _saves 中。
    /// </summary>
    public int LoadSaves()
    {
        var path = _settings.SavePath;
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"[ERROR] 存档文件不存在: {path}");
            _saves = null;
            return -1;
        }

        try
        {
            var json = File.ReadAllText(path);
            // 格式: [[[id,diff,acc], ...], ...]
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array)
            {
                Console.Error.WriteLine("[ERROR] 存档文件根元素不是数组。");
                _saves = null;
                return -1;
            }

            _saves = new List<SaveData>();
            int saveIdx = 0;
            foreach (var saveEl in root.EnumerateArray())
            {
                saveIdx++;
                if (saveEl.ValueKind != JsonValueKind.Array) continue;

                var save = new SaveData();
                foreach (var songEl in saveEl.EnumerateArray())
                {
                    if (songEl.ValueKind != JsonValueKind.Array) continue;
                    var arr = songEl.EnumerateArray().ToArray();
                    if (arr.Length < 3) continue;

                    save.Songs.Add(new SongRecord
                    {
                        SongId = arr[0].GetInt32(),
                        Difficulty = arr[1].GetDouble(),
                        Acc = arr[2].GetDouble()
                    });
                }
                _saves.Add(save);

                // 每 200 个存档报告一次进度
                if (saveIdx % 200 == 0)
                    Console.WriteLine($"[PROGRESS] 已加载 {saveIdx} 个存档...");
            }

            Console.WriteLine($"[DONE] 共加载 {_saves.Count} 个存档。");
            return _saves.Count;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] 加载存档失败: {ex.Message}");
            _saves = null;
            return -1;
        }
    }

    /// <summary>获取已加载的存档（仅在 LoadSaves 成功后有效）</summary>
    public List<SaveData>? GetSaves() => _saves;

    /// <summary>
    /// 获取指定深度的存档切片
    /// </summary>
    public List<SaveData> GetSavesWithDepth()
    {
        if (_saves == null) return new List<SaveData>();
        int count = _settings.Depth ?? Math.Min(_saves.Count, 1000);
        count = Math.Clamp(count, 1, _saves.Count);
        return _saves.Take(count).ToList();
    }

    // ==================== CSV 加载 ====================

    /// <summary>
    /// 加载 num.csv，建立曲目名称↔ID 的映射。
    /// </summary>
    public bool LoadCsv()
    {
        var path = _settings.ActiveCsvPath;
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"[ERROR] CSV 文件不存在: {path}");
            return false;
        }

        try
        {
            _songInfos = new List<SongInfo>();
            var lines = File.ReadAllLines(path);
            bool isFirst = true;
            foreach (var line in lines)
            {
                if (isFirst) { isFirst = false; continue; } // skip header
                if (string.IsNullOrWhiteSpace(line)) continue;

                // 格式: 曲名.作者,id
                // 从最后一个逗号分割（因为曲名中可能含有逗号）
                var lastComma = line.LastIndexOf(',');
                if (lastComma < 0) continue;

                var name = line[..lastComma];
                var rawId = line[(lastComma + 1)..].Trim();

                if (int.TryParse(rawId, out var id))
                {
                    _songInfos.Add(new SongInfo
                    {
                        Id = id,
                        RawId = rawId,
                        Name = name
                    });
                }
            }

            Console.WriteLine($"[DONE] 共加载 {_songInfos.Count} 条曲目信息。");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] 加载 CSV 失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>通过 ID 查找歌曲名称</summary>
    public string? GetSongNameById(int id)
    {
        return _songInfos?.FirstOrDefault(s => s.Id == id)?.Name;
    }

    /// <summary>通过名称模糊查找歌曲（返回所有匹配）</summary>
    public List<SongInfo> FindSongsByName(string keyword)
    {
        if (_songInfos == null) return new List<SongInfo>();
        return _songInfos
            .Where(s => s.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>通过 ID 精确查找歌曲信息</summary>
    public SongInfo? FindSongById(int id)
    {
        return _songInfos?.FirstOrDefault(s => s.Id == id);
    }

    // ==================== 缓存读写 ====================

    /// <summary>确保 tmp 目录存在</summary>
    public void EnsureTmpDir()
    {
        Directory.CreateDirectory(_settings.TmpDir);
    }

    /// <summary>写入歌曲分析缓存</summary>
    public void WriteSongCache(int songId, List<double> accs, double chartDiff, bool sort)
    {
        EnsureTmpDir();
        if (sort)
            accs.Sort();

        var cache = new SongCache { Accs = accs, ChartDiff = chartDiff };
        var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = false });
        var filePath = Path.Combine(_settings.TmpDir, $"tmp_{songId}.json");
        File.WriteAllText(filePath, json);
        Console.WriteLine($"[CACHE] 已保存 {accs.Count} 条 acc 到 {filePath}");
    }

    /// <summary>读取歌曲分析缓存</summary>
    public SongCache? ReadSongCache(int songId)
    {
        var filePath = Path.Combine(_settings.TmpDir, $"tmp_{songId}.json");
        if (!File.Exists(filePath)) return null;
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<SongCache>(json);
    }

    /// <summary>写入定数分析缓存</summary>
    public void WriteDiffCache(double diff, int sum, List<double> avgAccs, bool sort)
    {
        EnsureTmpDir();
        if (sort)
            avgAccs.Sort();

        var cache = new DiffCache { Sum = sum, AvgAccs = avgAccs };
        var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = false });
        // 定数文件名：把小数点替换为下划线
        var diffStr = diff.ToString(System.Globalization.CultureInfo.InvariantCulture).Replace('.', '_');
        var filePath = Path.Combine(_settings.TmpDir, $"tmp_{diffStr}.json");
        File.WriteAllText(filePath, json);
        Console.WriteLine($"[CACHE] 已保存到 {filePath}");
    }

    /// <summary>读取定数分析缓存</summary>
    public DiffCache? ReadDiffCache(double diff)
    {
        var diffStr = diff.ToString(System.Globalization.CultureInfo.InvariantCulture).Replace('.', '_');
        var filePath = Path.Combine(_settings.TmpDir, $"tmp_{diffStr}.json");
        if (!File.Exists(filePath)) return null;
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<DiffCache>(json);
    }

    /// <summary>清除所有缓存文件</summary>
    public void ClearCache()
    {
        if (!Directory.Exists(_settings.TmpDir))
        {
            Console.WriteLine("[CLEAN] 缓存目录不存在，无需清理。");
            return;
        }

        int count = 0;
        foreach (var file in Directory.GetFiles(_settings.TmpDir, "tmp_*.json"))
        {
            File.Delete(file);
            count++;
        }
        Console.WriteLine($"[CLEAN] 已删除 {count} 个缓存文件。");
    }
}
