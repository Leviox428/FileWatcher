using Renci.SshNet;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using static Org.BouncyCastle.Math.EC.ECCurve;

class Program
{
    class Config
    {
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public List<string>? FileExtensions { get; set; }
        public bool RemoveBeforeCopy { get; set; } = false;
        public int DelayInSeconds { get; set; } = 1;
    }

    class FtpConfig
    {
        public string Address { get; set; } = string.Empty;
        public int Port { get; set; } = 22;
        public string Name { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    class FtpTarget
    {
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public bool ZipFiles { get; set; } = false;
        public string ZipExtension { get; set; } = ".zip";
        public List<string>? FileExtensions { get; set; }

    }

    private static List<Config> _configs = [];
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };
    private static readonly string _configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
    private static readonly string _ftpConfigFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ftpConfig.json");
    private static readonly string _ftpTargetsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ftpTargets.json");
    private static readonly ConcurrentDictionary<string, DateTime> _lastProcessed = new();
    private static readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(500);
    private static readonly List<FileSystemWatcher> _watchers = [];

    static void Main()
    {
        LoadConfig();

        foreach (var config in _configs)
        {
            if (!Directory.Exists(config.SourcePath) || !Directory.Exists(config.DestinationPath))
            {
                Console.WriteLine($"Invalid paths in config: {config.SourcePath} -> {config.DestinationPath}");
                continue;
            }

            Console.WriteLine($"Watching folder: {config.SourcePath}");

            var watcher = new FileSystemWatcher
            {
                Path = config.SourcePath,
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
                Filter = "*.*",
                EnableRaisingEvents = true,
                InternalBufferSize = 64 * 1024
            };

            watcher.Changed += (s, e) => OnChanged(e, config);
            watcher.Created += (s, e) => OnChanged(e, config);
            watcher.Renamed += (s, e) => OnRenamed(e, config);
            watcher.Error += (s, e) =>
            {
                Console.WriteLine($"FileSystemWatcher error on {config.SourcePath}: {e.GetException()?.Message}");
            };
            _watchers.Add(watcher);
        }
        PrintHelp();
        RunCmdLoop();
    }

    static void RunCmdLoop()
    {
        while (true)
        {
            string? input = Console.ReadLine();

            if (input == null)
                break;

            input = input.Trim();

            if (string.IsNullOrEmpty(input))
                continue;

            if (input.Equals("--release", StringComparison.OrdinalIgnoreCase))
            {
                Console.Write("Are you sure you want to release files to the SFTP server? [Y/N]: ");
                string? confirm = Console.ReadLine()?.Trim();
                if (confirm != null && confirm.Equals("Y", StringComparison.OrdinalIgnoreCase))
                {
                    ExecuteRelease();
                }
                else
                {
                    Console.WriteLine("Release cancelled.");
                }
            }
            else if (input.Equals("--help", StringComparison.OrdinalIgnoreCase))
            {
                PrintHelp();
            }
            else if (input.Equals("--quit", StringComparison.OrdinalIgnoreCase) || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                Environment.Exit(0);
                break;
            }
            else
            {
                Console.WriteLine($"Unknown command: '{input}'. Type --help for available commands.");
            }
        }
    }
    static void PrintHelp()
    {
        Console.WriteLine("Commands:");
        Console.WriteLine("  --release : Upload files to the FTP server(ftpConfing.json) from ftpTargets.json");
        Console.WriteLine("  --help    : Show help menu");
        Console.WriteLine("  --quit    : Exit the application");
    }

    static void ExecuteRelease()
    {
        FtpConfig? ftpConfig = LoadFtpConfig();
        if (ftpConfig == null) return;

        List<FtpTarget>? targets = LoadFtpTargets();
        if (targets == null) return;

        Console.WriteLine($"Connecting to {ftpConfig.Address}:{ftpConfig.Port}...");

        try
        {
            using var sftp = new SftpClient(ftpConfig.Address, ftpConfig.Port, ftpConfig.Name, ftpConfig.Password);
            sftp.Connect();
            Console.WriteLine("Connected.");

            foreach (var target in targets)
            {
                UploadTarget(sftp, target);
            }

            sftp.Disconnect();
            Console.WriteLine("Release complete. Disconnected.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SFTP error: {ex.Message}");
        }
    }

    static void UploadTarget(SftpClient sftp, FtpTarget target)
    {
        if (!Directory.Exists(target.SourcePath))
        {
            Console.WriteLine($"Source path does not exist: {target.SourcePath}");
            return;
        }

        if (target.ZipFiles)
        {
            UploadAsZip(sftp, target);
        }
        else
        {
            UploadDirectory(sftp, target.SourcePath, target.DestinationPath, target.FileExtensions);
        }
    }

    static void UploadAsZip(SftpClient sftp, FtpTarget target)
    {
        string folderName = new DirectoryInfo(target.SourcePath).Name;
        string zipFileName = folderName + target.ZipExtension;
        string tempZipPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            using (var zipStream = File.Create(tempZipPath))
            using (var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                foreach (string filePath in Directory.GetFiles(target.SourcePath))
                {
                    if (FilterExtension(Path.GetFileName(filePath), target.FileExtensions))
                    {
                        zipArchive.CreateEntryFromFile(filePath, Path.GetFileName(filePath));
                    }
                }
            }

            string remotePath = target.DestinationPath + "/" + zipFileName;
            Console.WriteLine($"Uploading {zipFileName} to {remotePath}...");
            using (var fileStream = File.OpenRead(tempZipPath))
            {
                sftp.UploadFile(fileStream, remotePath, true);
            }
            Console.WriteLine($"Uploaded: {zipFileName}");
        }
        finally
        {
            if (File.Exists(tempZipPath))
                File.Delete(tempZipPath);
        }
    }
    static bool FilterExtension(string fileName, List<string>? fileExtensions)
    {
        if (fileExtensions != null && fileExtensions.Count > 0)
        {
            string fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
            return fileExtensions.Contains(fileExtension);
        }
        return true;
    }

    static void UploadDirectory(SftpClient sftp, string localDir, string remoteDir, List<string>? fileExtensions)
    {
        foreach (string filePath in Directory.GetFiles(localDir))
        {
            string fileName = Path.GetFileName(filePath);
            if (!FilterExtension(fileName, fileExtensions)) continue;

            string remotePath = remoteDir.TrimEnd('/') + "/" + fileName;
            Console.WriteLine($"Uploading {fileName} to {remotePath}...");
            using var fileStream = File.OpenRead(filePath);
            sftp.UploadFile(fileStream, remotePath, true);
            Console.WriteLine($"Uploaded: {fileName}");

        }

        foreach (string subDir in Directory.GetDirectories(localDir))
        {
            string dirName = Path.GetFileName(subDir);
            string remoteSubDir = remoteDir.TrimEnd('/') + "/" + dirName;
            UploadDirectory(sftp, subDir, remoteSubDir, fileExtensions);
        }
    }


    static FtpConfig? LoadFtpConfig()
    {
        if (!File.Exists(_ftpConfigFile))
        {
            var defaultConfig = new FtpConfig
            {
                Address = "sftp.example.com",
                Port = 0,
                Name = "username",
                Password = "password"
            };
            File.WriteAllText(_ftpConfigFile, JsonSerializer.Serialize(defaultConfig, _jsonSerializerOptions));
            Console.WriteLine("Created default ftpConfig.json — please edit it and try again.");
            return null;
        }

        var config = JsonSerializer.Deserialize<FtpConfig>(File.ReadAllText(_ftpConfigFile));
        if (config == null)
        {
            Console.WriteLine("Failed to parse ftpConfig.json.");
            return null;
        }
        return config;
    }

    static List<FtpTarget>? LoadFtpTargets()
    {
        if (!File.Exists(_ftpTargetsFile))
        {
            var defaultTargets = new List<FtpTarget>
            {
                new()
                {
                    SourcePath = "C:\\Path\\To\\Dest",
                    DestinationPath = "/destination",
                    ZipFiles = false,
                    ZipExtension = ".zip",
                    FileExtensions = new List<string> { ".txt", ".log" }, // example filters

                }
            };
            File.WriteAllText(_ftpTargetsFile, JsonSerializer.Serialize(defaultTargets, _jsonSerializerOptions));
            Console.WriteLine("Created default ftpTargets.json — please edit it and try again.");
            return null;
        }

        var targets = JsonSerializer.Deserialize<List<FtpTarget>>(File.ReadAllText(_ftpTargetsFile));
        if (targets == null)
        {
            Console.WriteLine("Failed to parse ftpTargets.json.");
            return null;
        }
        return targets;
    }

    static void LoadConfig()
    {
        if (!File.Exists(_configFile))
        {
            Console.WriteLine("Config file not found. Creating default config.json...");

            var defaultConfig = new List<Config>
            {
                new()
                {
                    SourcePath = "C:\\Path\\To\\Source1",
                    DestinationPath = "C:\\Path\\To\\Dest1",
                    FileExtensions = new List<string> { ".txt", ".log" }, // example filters
                    RemoveBeforeCopy = false,
                    DelayInSeconds = 1
                },
                new()
                {
                    SourcePath = "C:\\Path\\To\\Source2",
                    DestinationPath = "C:\\Path\\To\\Dest2",
                    RemoveBeforeCopy = false,
                    DelayInSeconds = 1
                    //no filter all files are copied

                },
            };

            string newJson = JsonSerializer.Serialize(defaultConfig, _jsonSerializerOptions);
            File.WriteAllText(_configFile, newJson);
            Console.WriteLine("Please edit config.json with your actual paths and restart the app.");
            Environment.Exit(0); // Exit after creating config
        }

        string json = File.ReadAllText(_configFile);
        List<Config>? loaded = JsonSerializer.Deserialize<List<Config>>(json);
        if (loaded == null)
        {
            Console.WriteLine("Failed to load config.");
            Environment.Exit(1);
        }

        _configs = loaded;
    }

    private static void OnChanged(FileSystemEventArgs e, Config config)
    {
        if (IsDebounced(e.FullPath, config)) return;
        try
        {
            if (e.ChangeType is WatcherChangeTypes.Changed or WatcherChangeTypes.Created)
            {
                CopyFile(e.FullPath, config);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in OnChanged: {ex.Message}");
        }
    }

    private static void OnRenamed(RenamedEventArgs e, Config config)
    {
        if (IsDebounced(e.FullPath, config)) return;
        try
        {
            if (e.ChangeType is WatcherChangeTypes.Renamed)
            {
                DeleteFileFromDestination(e.OldFullPath, config);
                CopyFile(e.FullPath, config);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in OnRenamed: {ex.Message}");
        }
    }

    private static bool IsDebounced(string path, Config config)
    {
        var now = DateTime.UtcNow;
        string key = GetDebounceKey(path, config);

        if (_lastProcessed.TryGetValue(key, out var last))
        {
            if ((now - last) < _debounceInterval)
                return true;
        }

        _lastProcessed[key] = now;
        return false;
    }

    private static void CopyFile(string sourceFilePath, Config config)
    {
        if (!File.Exists(sourceFilePath)) return;

        // Extension filter check
        if (config.FileExtensions != null && config.FileExtensions.Count > 0)
        {
            string fileExtension = Path.GetExtension(sourceFilePath).ToLowerInvariant();
            if (!config.FileExtensions.Contains(fileExtension))
            {
                return;
            }
        }

        string relativePath = Path.GetRelativePath(config.SourcePath, sourceFilePath);
        string destFilePath = Path.Combine(config.DestinationPath, relativePath);
        string? destDir = Path.GetDirectoryName(destFilePath);

        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        string currentTime = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"Change detected: {sourceFilePath} {currentTime}");

        bool wasDelayedOnce = false;
        const int maxRetries = 5;
        const int delayBetweenRetriesMs = 500;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (config.RemoveBeforeCopy && File.Exists(destFilePath))
                {
                    File.Delete(destFilePath);
                }

                // Delay to avoid copying file while still locked by another process
                if (!wasDelayedOnce)
                {
                    Thread.Sleep(config.DelayInSeconds * 1000);
                    wasDelayedOnce = true;
                }

                File.Copy(sourceFilePath, destFilePath, true);
                Console.WriteLine($"Copied: {relativePath} to {config.DestinationPath}");
                Console.WriteLine();
                _lastProcessed[GetDebounceKey(sourceFilePath, config)] = DateTime.UtcNow;
                return;
            }
            catch (IOException ioEx)
            {
                Console.WriteLine($"IO error while copying: {ioEx.Message}");
                Thread.Sleep(delayBetweenRetriesMs); // wait and retry
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error copying file: {ex.Message}");
                break; // non-retryable error
            }
        }
        Console.WriteLine($"Failed to copy: {relativePath} after {maxRetries} attempts.");
    }

    private static void DeleteFileFromDestination(string oldSourceFilePath, Config config)
    {
        try
        {
            string relativePath = Path.GetRelativePath(config.SourcePath, oldSourceFilePath);
            string destFilePath = Path.Combine(config.DestinationPath, relativePath);

            if (File.Exists(destFilePath))
            {
                File.Delete(destFilePath);
                Console.WriteLine($"Deleted old renamed file: {relativePath}");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting old file: {ex.Message}");
        }
    }

    private static string GetDebounceKey(string path, Config config) => $"{config.SourcePath}|{path}";
}
