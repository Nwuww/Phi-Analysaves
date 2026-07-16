namespace analysaves;

class Program
{
    static void Main(string[] args)
    {
        var settings = new Settings();
        var loader = new DataLoader(settings);

        Console.WriteLine("初始化...");
        Console.WriteLine($"[INFO] 工作目录: {Directory.GetCurrentDirectory()}");

        // ---- 自动发现存档文件 ----
        var saveDirFull = Path.GetFullPath(settings.SaveDir);
        var allSaves = Settings.DiscoverAll(settings.SaveDir, "*.json");
        if (allSaves.Count > 1)
            Console.WriteLine($"[INFO] {saveDirFull}\\ 下有 {allSaves.Count} 个存档文件");
        if (allSaves.Count > 0)
        {
            settings.SavePath = allSaves[0];
            Console.WriteLine($"[INFO] 加载存档: {allSaves[0]}");
        }
        else
        {
            Console.WriteLine($"[WARNING] {saveDirFull}\\ 下未检测到 *.json 存档文件");
        }

        int saveCount = loader.LoadSaves();
        if (saveCount < 0)
        {
            Console.WriteLine("[WARNING] 默认存档加载失败。请使用 set save-path 设置正确的存档路径。");
        }

        // ---- 自动发现曲目列表 ----
        var songlistDirFull = Path.GetFullPath(settings.SongListDir);
        var allCsvs = Settings.DiscoverAll(settings.SongListDir, "*.csv");
        if (allCsvs.Count > 1)
            Console.WriteLine($"[INFO] {songlistDirFull}\\ 下有 {allCsvs.Count} 个曲目列表文件");
        if (allCsvs.Count > 0)
        {
            settings.SongListPath = allCsvs[0];
            Console.WriteLine($"[INFO] 加载曲目列表: {allCsvs[0]}");
        }
        else
        {
            Console.WriteLine($"[WARNING] {songlistDirFull}\\ 下未检测到 *.csv 曲目列表");
        }

        if (!loader.LoadCsv())
        {
            Console.WriteLine("[WARNING] CSV 加载失败，-name 搜索将不可用。");
        }

        // 进入命令循环
        var processor = new CommandProcessor(settings, loader);
        processor.Run();
    }
}
