using System.Collections.Concurrent;
using System.Text.Json;

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

    private static List<Config> _configs = [];
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };
    private static readonly string _configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
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
        Console.WriteLine("Press [enter] to exit.");
        Console.ReadLine();
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
