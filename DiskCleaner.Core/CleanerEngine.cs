using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace DiskCleaner.Core
{
    /// <summary>
    /// Резултат от опит за DISM почистване на компонентното хранилище (WinSxS).
    /// </summary>
    public record DismResult(bool Started, int? ExitCode, string? Error);

    /// <summary>
    /// Цялата логика по сканиране/чистене на диска. Нито ред Console.WriteLine или
    /// Console.ReadLine тук - вместо това всичко се съобщава чрез Log събитието и
    /// връщани стойности, за да може да се ползва еднакво от конзолна и графична програма.
    /// </summary>
    public class CleanerEngine
    {
        /// <summary>Извиква се при всяко съобщение за прогрес (текст за лог/статус лента).</summary>
        public event Action<string>? Log;

        public bool IsAdmin { get; }

        public CleanerEngine()
        {
            IsAdmin = OperatingSystem.IsWindows() && IsRunningAsAdmin();
        }

        private void RaiseLog(string message) => Log?.Invoke(message);

        // --- P/Invoke за изпразване на Кошчето ---
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        private static bool IsRunningAsAdmin()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public List<CleanCategory> BuildCategories()
        {
            string userTemp = Path.GetTempPath();
            string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var list = new List<CleanCategory>
            {
                new CleanCategory("Временни файлове (Temp)", new[] { userTemp }, minAgeDays: 1),

                new CleanCategory("Кеш на Windows Explorer (thumbnails)",
                    new[] { Path.Combine(localAppData, "Microsoft", "Windows", "Explorer") }),

                new CleanCategory("Доклади за грешки (Windows Error Reporting)",
                    new[]
                    {
                        Path.Combine(localAppData, "Microsoft", "Windows", "WER", "ReportArchive"),
                        Path.Combine(localAppData, "Microsoft", "Windows", "WER", "ReportQueue"),
                    }),

                new CleanCategory("Кеш на Google Chrome",
                    new[]
                    {
                        Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Cache"),
                        Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Code Cache"),
                        Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "GPUCache"),
                        Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Service Worker", "CacheStorage"),
                        Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Service Worker", "ScriptCache"),
                    }),

                new CleanCategory("Кеш на Microsoft Edge",
                    new[]
                    {
                        Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Cache"),
                        Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Code Cache"),
                        Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "GPUCache"),
                        Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Service Worker", "CacheStorage"),
                        Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Service Worker", "ScriptCache"),
                    }),

                new CleanCategory("Кеш на Mozilla Firefox", GetFirefoxCacheFolders()),

                new CleanCategory("Стар Internet Explorer / Edge legacy кеш (INetCache)",
                    new[] { Path.Combine(localAppData, "Microsoft", "Windows", "INetCache") }),

                new CleanCategory("DirectX Shader кеш (GPU)",
                    new[]
                    {
                        Path.Combine(localAppData, "D3DSCache"),
                        Path.Combine(localAppData, "NVIDIA", "DXCache"),
                        Path.Combine(localAppData, "NVIDIA", "GLCache"),
                        Path.Combine(localAppData, "AMD", "DxCache"),
                        Path.Combine(localAppData, "AMD", "DxcCache"),
                    }),

                new CleanCategory("Crash dump файлове", new[] { Path.Combine(localAppData, "CrashDumps") }),

                new CleanCategory("Кошче (Recycle Bin)", Array.Empty<string>(), isRecycleBin: true),

                // Изискват администраторски права:
                new CleanCategory("Windows Temp (системни временни файлове)",
                    new[] { Path.Combine(windir, "Temp") }, requiresAdmin: true, minAgeDays: 1),

                new CleanCategory("Кеш на Windows Update",
                    new[] { Path.Combine(windir, "SoftwareDistribution", "Download") }, requiresAdmin: true),

                new CleanCategory("Delivery Optimization кеш",
                    new[] { Path.Combine(windir, "SoftwareDistribution", "DeliveryOptimization") }, requiresAdmin: true),

                new CleanCategory("Prefetch (Windows регенерира автоматично)",
                    new[] { Path.Combine(windir, "Prefetch") }, requiresAdmin: true),

                new CleanCategory("Системни crash dump-ове (Minidump / LiveKernelReports)",
                    new[]
                    {
                        Path.Combine(windir, "Minidump"),
                        Path.Combine(windir, "LiveKernelReports"),
                    }, requiresAdmin: true),

                new CleanCategory("Стари Windows Update / CBS логове",
                    new[] { Path.Combine(windir, "Logs", "CBS") }, requiresAdmin: true),
            };

            return list;
        }

        private static IEnumerable<string> GetFirefoxCacheFolders()
        {
            string profilesRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Mozilla", "Firefox", "Profiles");

            var result = new List<string>();
            if (Directory.Exists(profilesRoot))
            {
                foreach (var profileDir in Directory.GetDirectories(profilesRoot))
                {
                    var cache = Path.Combine(profileDir, "cache2");
                    if (Directory.Exists(cache))
                        result.Add(cache);
                }
            }
            return result;
        }

        public void Scan(CleanCategory cat)
        {
            cat.ResetScan();

            if (cat.IsRecycleBin)
            {
                // Символичен маркер, за да участва в общата сума / UI показва "(изпразва се)".
                cat.ScannedBytes = 1;
                cat.ScannedFiles = 0;
                return;
            }

            if (cat.RequiresAdmin && !IsAdmin)
            {
                RaiseLog($"{cat.Name}: пропуснато (нужни са администраторски права)");
                return;
            }

            foreach (var path in cat.Paths)
            {
                if (!Directory.Exists(path)) continue;
                foreach (var file in EnumerateFilesSafe(path))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        if (cat.MinAgeDays.HasValue)
                        {
                            var age = DateTime.Now - info.LastWriteTime;
                            if (age.TotalDays < cat.MinAgeDays.Value) continue;
                        }
                        cat.ScannedBytes += info.Length;
                        cat.ScannedFiles++;
                    }
                    catch { /* файл недостъпен - пропускаме при сканиране */ }
                }
            }

            RaiseLog($"{cat.Name}: {cat.ScannedFiles} файла, {FormatSize(cat.ScannedBytes)}");
        }

        public void Clean(CleanCategory cat)
        {
            cat.ResetClean();

            if (cat.IsRecycleBin)
            {
                try
                {
                    uint flags = SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND;
                    SHEmptyRecycleBin(IntPtr.Zero, null, flags);
                    RaiseLog("Кошче: изпразнено");
                }
                catch (Exception ex)
                {
                    cat.Errors++;
                    RaiseLog($"Кошче: грешка ({ex.Message})");
                }
                return;
            }

            if (cat.RequiresAdmin && !IsAdmin)
            {
                RaiseLog($"{cat.Name}: пропуснато (нужни са администраторски права)");
                return;
            }

            foreach (var path in cat.Paths)
            {
                if (!Directory.Exists(path)) continue;

                foreach (var file in EnumerateFilesSafe(path))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        if (cat.MinAgeDays.HasValue)
                        {
                            var age = DateTime.Now - info.LastWriteTime;
                            if (age.TotalDays < cat.MinAgeDays.Value) continue;
                        }
                        long size = info.Length;
                        info.Delete();
                        cat.FreedBytes += size;
                        cat.FreedFiles++;
                    }
                    catch
                    {
                        // файлът е заключен от друга програма или няма права - пропускаме, не спираме
                        cat.Errors++;
                    }
                }

                TryRemoveEmptySubfolders(path);
            }

            RaiseLog($"{cat.Name}: освободени {FormatSize(cat.FreedBytes)}" +
                     (cat.Errors > 0 ? $" ({cat.Errors} пропуснати файла)" : ""));
        }

        /// <summary>
        /// Обхожда файловете в root ръчно, ниво по ниво. Directory.EnumerateFiles(...,
        /// AllDirectories) е "мързелив" итератор - ако някоя подпапка е защитена
        /// (Access Denied), хвърля изключение по средата на обхождането и къса
        /// целия резултат. Тук прескачаме само проблемната подпапка.
        /// </summary>
        private static IEnumerable<string> EnumerateFilesSafe(string root)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(root);
            }
            catch
            {
                yield break;
            }

            foreach (var f in files)
                yield return f;

            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(root);
            }
            catch
            {
                yield break;
            }

            foreach (var dir in subDirs)
            {
                foreach (var f in EnumerateFilesSafe(dir))
                    yield return f;
            }
        }

        private static IEnumerable<string> EnumerateDirectoriesSafe(string root)
        {
            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(root);
            }
            catch
            {
                yield break;
            }

            foreach (var dir in subDirs)
            {
                yield return dir;
                foreach (var nested in EnumerateDirectoriesSafe(dir))
                    yield return nested;
            }
        }

        private static void TryRemoveEmptySubfolders(string root)
        {
            foreach (var dir in EnumerateDirectoriesSafe(root).OrderByDescending(d => d.Length))
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        Directory.Delete(dir);
                }
                catch { /* игнорираме - не е критично */ }
            }
        }

        /// <summary>
        /// Най-дълбокото ниво: маха стари версии на системни компоненти (WinSxS) чрез
        /// вградения Windows DISM инструмент. Изисква администраторски права и отнема
        /// няколко минути. Извикващият (Console/GUI) решава дали и кога да го предложи.
        /// </summary>
        public DismResult RunDismCleanup()
        {
            if (!IsAdmin)
                return new DismResult(false, null, "Нужни са администраторски права.");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "dism.exe",
                    Arguments = "/Online /Cleanup-Image /StartComponentCleanup",
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    CreateNoWindow = true,
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit();
                return new DismResult(true, proc?.ExitCode, null);
            }
            catch (Exception ex)
            {
                return new DismResult(false, null, ex.Message);
            }
        }

        public static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }
            return $"{size:0.##} {units[unit]}";
        }
    }
}
