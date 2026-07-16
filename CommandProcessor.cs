namespace analysaves;

/// <summary>
/// 命令解析与路由处理器
/// </summary>
public class CommandProcessor
{
    private readonly Settings _settings;
    private readonly DataLoader _loader;
    private bool _running = true;

    // 分析模式状态
    private enum AnalysisMode { None, Song, Diff }
    private AnalysisMode _mode = AnalysisMode.None;
    private int _currentSongId;         // 存档中的 songId（listId * 4 + offset）
    private string _currentSongName = "";
    private string _currentSongPrompt = "";
    private string _currentDiffLabel = ""; // EZ/HD/IN/AT
    private double _currentDiff;
    private string _currentDiffPrompt = ""; // 定数提示符文本
    private List<double>? _currentData;

    // 导出会话
    private string? _outFilePath;
    private string? _outTitle;

    // 难度偏移
    private static readonly Dictionary<string, int> DiffOffset = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EZ"] = 0, ["HD"] = 1, ["IN"] = 2, ["AT"] = 3
    };

    public CommandProcessor(Settings settings, DataLoader loader)
    {
        _settings = settings;
        _loader = loader;
    }

    // ==================== 主循环 ====================

    public void Run()
    {
        Console.WriteLine("\n\n\n");
        Console.WriteLine("Analysaves v0.1.1");
        Console.WriteLine("o(( > ω < ))o");
        Console.WriteLine("输入 help 查看帮助，输入 #exit 退出");

        while (_running)
        {
            Console.Write(GetPrompt());
            var input = Console.ReadLine();
            if (input == null) break; // EOF

            var trimmed = input.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            ProcessCommand(trimmed);
            Console.WriteLine("\n");
        }
    }

    private string GetPrompt()
    {
        return _mode switch
        {
            AnalysisMode.Song => _currentSongPrompt,
            AnalysisMode.Diff => $"{_currentDiffPrompt}>>  ",
            _ => ">> "
        };
    }

    // ==================== 命令路由 ====================

    private void ProcessCommand(string input)
    {
        // 全局退出
        if (input == "#exit")
        {
            _running = false;
            Console.WriteLine("(￣﹃￣)");
            return;
        }

        // 模式内命令
        if (_mode == AnalysisMode.Song)
        {
            ProcessSongModeCommand(input);
            return;
        }
        if (_mode == AnalysisMode.Diff)
        {
            ProcessDiffModeCommand(input);
            return;
        }

        // 主模式命令
        var parts = Tokenize(input);
        if (parts.Count == 0) return;

        var cmd = parts[0];

        switch (cmd)
        {
            case "help":
                ShowHelp();
                break;
            case "set":
                HandleSet(parts);
                break;
            case "clear":
                _loader.ClearCache();
                break;
            case "reset":
                _loader.ClearCache();
                _settings.Reset();
                Console.WriteLine("[INFO] 已恢复默认设置并清除缓存");
                break;
            case "status":
                HandleStatus();
                break;
            case "ana":
                HandleAna(parts);
                break;
            default:
                Console.WriteLine($"[ERROR] 未知命令: {cmd}，输入 help 可以查看帮助噢~");
                break;
        }
    }

    // ==================== 主模式命令 ====================

    private void ShowHelp()
    {
        Console.WriteLine(@"
===== 帮助 =====
环境设置:
  set save-path [PATH]    设置存档路径（无参数时从 save/ 目录交互选择）
  set out-path <PATH>     设置导出路径（默认 export/out.txt）
  set depth <int>|max     设置检索深度（默认 max=全部存档）
  set songlist [PATH]     设置曲目列表路径（无参数时从 songlist/ 目录交互选择）
  set feat <double...>    设置特征值列表（0~100），如: set feat 98 99 99.5
  clear                   清除所有缓存文件
  reset                   重置所有设置并清除缓存

分析:
  status                  输出读入的存档个数
  ana song -id <id> <EZ|HD|IN|AT> [-nosort]      通过 ID 和难度进入歌曲分析模式
  ana song -name <关键词> <EZ|HD|IN|AT> [-nosort] 通过歌名和难度进入歌曲分析模式
  ana diff <定数> [-nosort]        进入定数分析模式（支持区间如 17-17.3）

系统:
  help                    显示本帮助
  #exit                   退出系统
");
    }

    private void HandleSet(List<string> parts)
    {
        if (parts.Count < 2)
        {
            Console.WriteLine("[ERROR] set 需要更多参数。用法: set <key> [value]");
            return;
        }

        var key = parts[1];

        // 无 value 的情况 → 交互选择器（仅 save-path / songlist 支持）
        if (parts.Count == 2)
        {
            switch (key)
            {
                case "save-path":
                    InteractivePickFile(_settings.SaveDir, "*.json", "存档",
                        path => {
                            _settings.SavePath = path;
                            Console.WriteLine($"[SET] save-path = {path}");
                            Console.WriteLine("正在重新加载存档...");
                            _loader.LoadSaves();
                        });
                    return;
                case "songlist":
                    InteractivePickFile(_settings.SongListDir, "*.csv", "曲目列表",
                        path => {
                            _settings.SongListPath = path;
                            Console.WriteLine($"[SET] 曲目列表 = {path}");
                            Console.WriteLine("正在重新加载曲目列表...");
                            _loader.LoadCsv();
                        });
                    return;
                default:
                    Console.WriteLine($"[ERROR] set {key} 需要指定值。");
                    return;
            }
        }

        // 有 value 的情况
        var value = string.Join(" ", parts.Skip(2));

        switch (key)
        {
            case "save-path":
                if (!value.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("[WARNING] 存档文件应为 .json 格式。");
                }
                _settings.SavePath = value;
                Console.WriteLine($"[SET] save-path = {value}");
                Console.WriteLine("正在重新加载存档...");
                _loader.LoadSaves();
                break;

            case "out-path":
                _settings.OutPath = value;
                Console.WriteLine($"[SET] out-path = {value}");
                break;

            case "depth":
                if (value.Equals("max", StringComparison.OrdinalIgnoreCase))
                {
                    _settings.Depth = null;
                    Console.WriteLine("[SET] depth = max（全部存档）");
                }
                else if (int.TryParse(value, out var d) && d >= 1)
                {
                    _settings.Depth = d;
                    Console.WriteLine($"[SET] depth = {d}");
                }
                else
                {
                    Console.WriteLine("[ERROR] depth 必须是正整数或 max。");
                }
                break;

            case "songlist":
                _settings.SongListPath = value;
                Console.WriteLine($"[SET] 曲目列表 = {value}");
                Console.WriteLine("正在重新加载曲目列表...");
                _loader.LoadCsv();
                break;

            case "feat":
                {
                    var thresholds = new List<double>();
                    foreach (var token in parts.Skip(2))
                    {
                        if (double.TryParse(token,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var t))
                        {
                            if (t >= 0 && t <= 100)
                                thresholds.Add(t);
                            else
                                Console.WriteLine($"[WARNING] 特征值 {t} 超出范围 0~100，已忽略。");
                        }
                    }
                    thresholds.Sort();
                    _settings.FeatThresholds = thresholds;
                    Console.WriteLine($"[SET] feat = [{string.Join(", ", thresholds)}]");
                }
                break;

            default:
                Console.WriteLine($"[ERROR] 未知设置项: {key}。可用: save-path, out-path, depth, songlist, feat");
                break;
        }
    }

    // ==================== 交互式文件选择器 ====================

    /// <summary>
    /// 分页浏览目录下文件，用户按编号选择。
    /// </summary>
    private void InteractivePickFile(string directory, string pattern, string label, Action<string> onSelected)
    {
        var files = Settings.DiscoverAll(directory, pattern);
        if (files.Count == 0)
        {
            Console.WriteLine($"[INFO] 目录 {directory}/ 下没有匹配 {pattern} 的文件。");
            return;
        }

        int page = 0;
        const int pageSize = 10;
        int totalPages = (files.Count - 1) / pageSize + 1;

        while (true)
        {
            int start = page * pageSize;
            int end = Math.Min(start + pageSize, files.Count);

            Console.WriteLine($"\n当前默认目录 {Path.GetFullPath(directory)}\\ 下包含 ({page + 1}/{totalPages} 页):");
            for (int i = start; i < end; i++)
            {
                Console.WriteLine($"  [{i - start}] {Path.GetFileName(files[i])}");
            }

            Console.Write($"\n请输入数字编号选择{label}文件，-/+ 翻页，直接回车取消: ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input))
            {
                Console.WriteLine("已取消，继续使用当前设置。");
                return;
            }

            if (input == "+")
            {
                if (page < totalPages - 1) page++;
                else Console.WriteLine("已经是最后一页。");
                continue;
            }
            if (input == "-")
            {
                if (page > 0) page--;
                else Console.WriteLine("已经是第一页。");
                continue;
            }

            if (int.TryParse(input, out var idx) && idx >= 0 && idx < (end - start))
            {
                int realIdx = start + idx;
                onSelected(files[realIdx]);
                return;
            }

            Console.WriteLine("[ERROR] 无效输入，请输入数字编号、-/+ 或直接回车。");
        }
    }

    private void HandleStatus()
    {
        var saves = _loader.GetSaves();
        if (saves == null || saves.Count == 0)
        {
            Console.WriteLine("[INFO] 未检测到有效的存档。请先使用 set save-path 设置正确的存档路径。");
            return;
        }
        int depth = _settings.Depth ?? Math.Min(saves.Count, 1000);
        Console.WriteLine($"已读入 {saves.Count} 个存档，当前检索深度: {(_settings.Depth == null ? $"默认(min(全部, 1000)={depth})" : depth.ToString())}");
    }

    private void HandleAna(List<string> parts)
    {
        if (parts.Count < 3)
        {
            Console.WriteLine("[ERROR] ana 需要更多参数。用法:");
            Console.WriteLine("  ana song -id <id> <EZ|HD|IN|AT> [-nosort]");
            Console.WriteLine("  ana song -name <关键词> <EZ|HD|IN|AT> [-nosort]");
            Console.WriteLine("  ana diff <定数> [-nosort]  (支持区间: ana diff 17-17.3)");
            return;
        }

        var subCmd = parts[1];

        if (subCmd == "song")
        {
            HandleAnaSong(parts);
        }
        else if (subCmd == "diff")
        {
            HandleAnaDiff(parts);
        }
        else
        {
            Console.WriteLine($"[ERROR] 未知分析类型: {subCmd}。可用: song, diff");
        }
    }

    // ==================== ana song ====================

    private void HandleAnaSong(List<string> parts)
    {
        // 解析: ana song <value> <EZ|HD|IN|AT> [-nosort]
        // <value> = -id <id> 或 -name <关键词>
        bool noSort = parts.Contains("-nosort");
        bool byName = parts.Contains("-name");
        bool byId = parts.Contains("-id");

        if (!byName && !byId)
        {
            Console.WriteLine("[ERROR] ana song 需要指定 -id 或 -name。");
            return;
        }
        if (byName && byId)
        {
            Console.WriteLine("[ERROR] -id 和 -name 不能同时使用。");
            return;
        }

        // 提取 list ID 或关键词
        int valueIndex;
        if (byId) valueIndex = parts.IndexOf("-id") + 1;
        else valueIndex = parts.IndexOf("-name") + 1;

        if (valueIndex >= parts.Count || parts[valueIndex].StartsWith('-'))
        {
            Console.WriteLine("[ERROR] 缺少参数值。");
            return;
        }

        // 提取难度标签（EZ/HD/IN/AT），排除已识别的 token
        string? diffLabel = null;
        foreach (var p in parts)
        {
            if (DiffOffset.ContainsKey(p))
            {
                diffLabel = p.ToUpper();
                break;
            }
        }
        if (diffLabel == null)
        {
            Console.WriteLine("[ERROR] ana song 必须指定难度: EZ, HD, IN, AT");
            Console.WriteLine("  用法: ana song -id <id> <EZ|HD|IN|AT> [-nosort]");
            Console.WriteLine("  用法: ana song -name <关键词> <EZ|HD|IN|AT> [-nosort]");
            return;
        }
        int offset = DiffOffset[diffLabel];

        SongInfo? targetSong = null;
        int listId;

        if (byId)
        {
            if (!int.TryParse(parts[valueIndex], out listId))
            {
                Console.WriteLine("[ERROR] ID 必须是整数。");
                return;
            }
            targetSong = _loader.FindSongById(listId);
            if (targetSong == null)
            {
                Console.WriteLine($"[WARNING] 未在 CSV 中找到 ID={listId} 的歌曲。将用 ID 继续分析。");
                _currentSongName = $"ID:{listId}";
            }
            else
            {
                _currentSongName = targetSong.Name;
            }
        }
        else // byName
        {
            var keyword = parts[valueIndex];
            var matches = _loader.FindSongsByName(keyword);
            if (matches.Count == 0)
            {
                Console.WriteLine($"[ERROR] 未找到包含 \"{keyword}\" 的歌曲。");
                return;
            }
            else if (matches.Count == 1)
            {
                targetSong = matches[0];
                listId = targetSong.Id;
                _currentSongName = targetSong.Name;
                Console.WriteLine($"匹配到: {targetSong.Name}, {targetSong.RawId}");
            }
            else
            {
                Console.WriteLine($"找到 {matches.Count} 首匹配歌曲:");
                for (int i = 0; i < matches.Count; i++)
                {
                    Console.WriteLine($"  [{i}] {matches[i].Name}, {matches[i].RawId}");
                }
                Console.Write("请输入序号选择 (或按 Enter 取消): ");
                var choice = Console.ReadLine();
                if (!int.TryParse(choice, out var idx) || idx < 0 || idx >= matches.Count)
                {
                    Console.WriteLine("已取消。");
                    return;
                }
                targetSong = matches[idx];
                listId = targetSong.Id;
                _currentSongName = targetSong.Name;
            }
        }

        // 计算存档中的 songId = listId * 4 + 难度偏移
        int saveSongId = listId * 4 + offset;
        _currentSongId = saveSongId;
        _currentDiffLabel = diffLabel;

        // ---- 优先检查缓存 ----
        var cachedSong = _loader.ReadSongCache(saveSongId);
        if (cachedSong != null)
        {
            Console.WriteLine($"[CACHE] 从缓存加载 {cachedSong.Accs.Count} 条记录。");
            if (!noSort)
                cachedSong.Accs.Sort();

            _mode = AnalysisMode.Song;
            _currentData = cachedSong.Accs;
            _currentSongPrompt = $"{_currentSongName}.{listId} [{diffLabel}{cachedSong.ChartDiff}]>>  ";
            Console.WriteLine($"已进入歌曲分析模式。共 {cachedSong.Accs.Count} 条记录。");
            return;
        }

        // 加载数据（缓存未命中）
        var saves = _loader.GetSavesWithDepth();
        if (saves.Count == 0)
        {
            Console.WriteLine("[ERROR] 没有可用的存档数据。请先检查存档路径和 status。");
            return;
        }

        // 提取 acc
        Console.WriteLine($"正在从 {saves.Count} 个存档中提取 \"{_currentSongName}\" ({diffLabel}, listID={listId}, saveID={saveSongId}) 的 acc...");
        var accs = AnalysisEngine.ExtractSongAccs(saves, saveSongId);

        if (accs.Count == 0)
        {
            Console.WriteLine($"[INFO] 在 {saves.Count} 个存档中未找到 saveID={saveSongId} ({_currentSongName} {diffLabel})。");
            return;
        }

        // 获取该谱面的定数（从第一个匹配的存档中取）
        double chartDiff = 0;
        foreach (var save in saves)
        {
            var rec = save.Songs.FirstOrDefault(s => s.SongId == saveSongId);
            if (rec != null) { chartDiff = rec.Difficulty; break; }
        }

        // 排序（默认升序）
        if (!noSort)
            accs.Sort();

        // 保存缓存
        _loader.WriteSongCache(saveSongId, accs, chartDiff, sort: false);

        // 进入歌曲分析模式
        _mode = AnalysisMode.Song;
        _currentData = accs;
        _currentSongPrompt = $"{_currentSongName}.{listId} [{diffLabel}{chartDiff}]>>  ";

        Console.WriteLine($"已进入歌曲分析模式。共 {accs.Count} 条记录。");
    }

    // ==================== ana diff ====================

    private void HandleAnaDiff(List<string> parts)
    {
        bool noSort = parts.Contains("-nosort");

        // 提取定数参数（parts[2]）
        int diffIndex = 2;
        if (diffIndex >= parts.Count || (parts[diffIndex].StartsWith('-') && !parts[diffIndex].Contains('.')))
        {
            Console.WriteLine("[ERROR] ana diff 需要指定定数。用法: ana diff <定数> [-nosort] 或 ana diff <从>-<到> [-nosort]");
            return;
        }

        var diffArg = parts[diffIndex];

        // 判断区间还是单值
        List<double> diffs;
        string promptLabel;

        if (diffArg.Contains('-') && diffArg.Count(c => c == '-') == 1)
        {
            // 区间: "17-17.3"
            var rangeParts = diffArg.Split('-');
            if (!double.TryParse(rangeParts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var from) ||
                !double.TryParse(rangeParts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var to))
            {
                Console.WriteLine("[ERROR] 区间格式错误。用法: ana diff <从>-<到>");
                return;
            }
            if (from > to) { (from, to) = (to, from); }

            diffs = new List<double>();
            for (double d = from; d <= to + 0.0001; d = Math.Round(d + 0.1, 1))
                diffs.Add(d);

            promptLabel = $"{from}-{to}";
        }
        else
        {
            if (!double.TryParse(diffArg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var single))
            {
                Console.WriteLine("[ERROR] 定数必须是数字或区间（如 17-17.3）。");
                return;
            }
            diffs = new List<double> { single };
            promptLabel = single.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        // 加载存档（缓存未命中时需要）
        var saves = _loader.GetSavesWithDepth();
        if (saves.Count == 0)
        {
            Console.WriteLine("[ERROR] 没有可用的存档数据。请先检查存档路径和 status。");
            return;
        }

        // 对每个定数：缓存优先，否则提取
        var allAvgAccs = new List<double>();
        int totalSongs = 0;
        int cachedCount = 0;
        int scannedCount = 0;

        foreach (var d in diffs)
        {
            var cached = _loader.ReadDiffCache(d);
            if (cached != null)
            {
                totalSongs += cached.Sum;
                allAvgAccs.AddRange(cached.AvgAccs);
                cachedCount++;
            }
            else
            {
                Console.WriteLine($"正在提取定数 {d}...");
                var (sum, avgAccs) = AnalysisEngine.ExtractDiffAccs(saves, d);
                totalSongs += sum;
                allAvgAccs.AddRange(avgAccs);
                if (sum > 0)
                    _loader.WriteDiffCache(d, sum, avgAccs, sort: false);
                scannedCount++;
            }
        }

        if (totalSongs == 0)
        {
            Console.WriteLine($"[INFO] 在 {saves.Count} 个存档中未找到定数范围 {promptLabel} 的歌曲。");
            return;
        }

        if (cachedCount > 0)
            Console.WriteLine($"[CACHE] {cachedCount} 个定数从缓存加载，{scannedCount} 个定数重新提取。");

        // 排序
        if (!noSort)
            allAvgAccs.Sort();

        // 进入定数分析模式
        _mode = AnalysisMode.Diff;
        _currentDiff = diffs[0];
        _currentDiffPrompt = promptLabel;
        _currentData = allAvgAccs;

        Console.WriteLine($"已进入定数分析模式。{promptLabel}: 共 {totalSongs} 首曲目记录，{allAvgAccs.Count} 条存档平均acc。");
    }

    // ==================== 歌曲分析模式子命令 ====================

    private void ProcessSongModeCommand(string input)
    {
        if (_currentData == null) { _mode = AnalysisMode.None; return; }

        var parts = Tokenize(input);
        if (parts.Count == 0) return;

        bool export = parts.Remove("-exp");
        var cmd = parts[0];

        if (export && cmd != "exit")
            BeginOutSession(_currentSongPrompt.Replace(">>  ", "").Trim());

        switch (cmd)
        {
            case "avg":
                CmdAvg(_currentData, export);
                break;
            case "med":
                CmdMed(_currentData, export);
                break;
            case "above":
                CmdAbove(parts, _currentData, export);
                break;
            case "below":
                CmdBelow(parts, _currentData, export);
                break;
            case "feat":
                CmdFeat(_currentData, export);
                break;
            case "exit":
                EndOutSession();
                _mode = AnalysisMode.None;
                _currentData = null;
                Console.WriteLine("已退出歌曲分析模式。缓存文件保留。");
                break;
            default:
                Console.WriteLine($"[ERROR] 未知命令: {cmd}。可用: avg, med, above, below, feat, exit");
                break;
        }
    }

    // ==================== 定数分析模式子命令 ====================

    private void ProcessDiffModeCommand(string input)
    {
        if (_currentData == null) { _mode = AnalysisMode.None; return; }

        var parts = Tokenize(input);
        if (parts.Count == 0) return;

        bool export = parts.Remove("-exp");
        var cmd = parts[0];

        if (export && cmd != "exit")
            BeginOutSession($"定数: {_currentDiffPrompt}");

        switch (cmd)
        {
            case "avg":
                CmdAvg(_currentData, export);
                break;
            case "med":
                CmdMed(_currentData, export);
                break;
            case "above":
                CmdAbove(parts, _currentData, export);
                break;
            case "below":
                CmdBelow(parts, _currentData, export);
                break;
            case "feat":
                CmdFeat(_currentData, export);
                break;
            case "exit":
                EndOutSession();
                _mode = AnalysisMode.None;
                _currentData = null;
                Console.WriteLine("已退出定数分析模式。缓存文件保留。");
                break;
            default:
                Console.WriteLine($"[ERROR] 未知命令: {cmd}。可用: avg, med, above, below, feat, exit");
                break;
        }
    }

    // ==================== 统计命令实现 ====================

    private void CmdAvg(List<double> data, bool export)
    {
        if (data.Count == 0)
        {
            Output("无数据。", export);
            return;
        }

        double avg = AnalysisEngine.Average(data);
        var (count, ratio) = AnalysisEngine.AboveOrEqual(data, avg);

        Output($"平均值: {avg:F6}, {ratio:F2}%({count} 个) 大于均值", export);
    }

    private void CmdMed(List<double> data, bool export)
    {
        if (data.Count == 0)
        {
            Output("无数据。", export);
            return;
        }

        double med = AnalysisEngine.Median(data);

        // 需要排序的列表来计算位置
        var sorted = new List<double>(data);
        sorted.Sort();
        double posPercent = AnalysisEngine.MedianPositionPercent(sorted);

        Output($"中位数: {med:F6}", export);
        Output($"中位数位置: {posPercent:F2}% (总共 {data.Count} 条)", export);
    }

    private void CmdAbove(List<string> parts, List<double> data, bool export)
    {
        if (data.Count == 0) { Output("无数据。", export); return; }
        if (parts.Count < 2) { Output("[ERROR] above 需要指定数值。用法: above <double> [-num] [-exp]", export); return; }

        if (!double.TryParse(parts[1],
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var threshold))
        {
            Output("[ERROR] 参数必须是数字。", export);
            return;
        }

        bool showNum = parts.Contains("-num");
        var (count, ratio) = AnalysisEngine.StrictlyAbove(data, threshold);

        if (showNum)
            Output($"acc > {threshold}: {count} 个 (总计 {data.Count} 条)", export);
        else
            Output($"acc > {threshold}: {ratio:F2}% ({count}/{data.Count})", export);
    }

    private void CmdBelow(List<string> parts, List<double> data, bool export)
    {
        if (data.Count == 0) { Output("无数据。", export); return; }
        if (parts.Count < 2) { Output("[ERROR] below 需要指定数值。用法: below <double> [-num] [-exp]", export); return; }

        if (!double.TryParse(parts[1],
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var threshold))
        {
            Output("[ERROR] 参数必须是数字。", export);
            return;
        }

        bool showNum = parts.Contains("-num");
        var (count, ratio) = AnalysisEngine.StrictlyBelow(data, threshold);

        if (showNum)
            Output($"acc < {threshold}: {count} 个 (总计 {data.Count} 条)", export);
        else
            Output($"acc < {threshold}: {ratio:F2}% ({count}/{data.Count})", export);
    }

    private void CmdFeat(List<double> data, bool export)
    {
        if (_settings.FeatThresholds.Count == 0)
        {
            Output("[INFO] 未设置特征值。请先用 set feat <value...> 设置。", export);
            return;
        }
        if (data.Count == 0) { Output("无数据。", export); return; }

        foreach (var t in _settings.FeatThresholds)
        {
            var (count, ratio) = AnalysisEngine.StrictlyAbove(data, t);
            Output($"acc > {t}: {ratio:F2}% ({count}/{data.Count})", export);
        }
    }

    // ==================== 导出 ====================

    private void BeginOutSession(string title)
    {
        if (_outTitle == title) return;

        if (_outTitle != null)
        {
            AppendToFile("======");
            AppendToFile("");
        }

        if (_outFilePath == null)
        {
            var now = DateTime.Now;
            var name = $"{now:HHmmss-yyyyMMdd}.txt";
            var dir = Path.GetDirectoryName(_settings.OutPath) ?? "export";
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            _outFilePath = Path.Combine(dir, name);
        }

        _outTitle = title;
        AppendToFile("======");
        AppendToFile(title);
    }

    private void EndOutSession()
    {
        if (_outTitle != null)
        {
            AppendToFile("======");
            AppendToFile("");
            _outTitle = null;
        }
    }

    private void Output(string line, bool export)
    {
        Console.WriteLine(line);
        if (export) AppendToFile(line);
    }

    private void AppendToFile(string line)
    {
        if (_outFilePath == null) return;
        try
        {
            File.AppendAllText(_outFilePath, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] 导出失败: {ex.Message}");
        }
    }

    // ==================== 工具方法 ====================

    /// <summary>
    /// 简单分词：按空格分割，但保留引号内的内容
    /// </summary>
    private static List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        bool inQuote = false;
        var current = new System.Text.StringBuilder();

        foreach (char c in input)
        {
            if (c == '"')
            {
                inQuote = !inQuote;
            }
            else if (char.IsWhiteSpace(c) && !inQuote)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }
        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens;
    }
}
