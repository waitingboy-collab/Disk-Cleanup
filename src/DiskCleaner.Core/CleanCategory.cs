using System.Collections.Generic;
using System.Linq;

namespace DiskCleaner.Core
{
    /// <summary>
    /// Едно "място" за почистване - папка (или няколко) с боклучави файлове.
    /// Никакъв Console/GUI код тук - само данни и състояние, за да може
    /// както конзолната, така и графичната програма да го ползват еднакво.
    /// </summary>
    public class CleanCategory
    {
        public string Name { get; }
        public List<string> Paths { get; }
        public bool RequiresAdmin { get; }
        public bool IsRecycleBin { get; }
        // Ако е зададено - трият се само файлове по-стари от MinAgeDays (по-безопасно за Temp)
        public int? MinAgeDays { get; }

        public long ScannedBytes;
        public int ScannedFiles;
        public long FreedBytes;
        public int FreedFiles;
        public int Errors;

        public CleanCategory(string name, IEnumerable<string> paths, bool requiresAdmin = false,
            bool isRecycleBin = false, int? minAgeDays = null)
        {
            Name = name;
            Paths = paths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            RequiresAdmin = requiresAdmin;
            IsRecycleBin = isRecycleBin;
            MinAgeDays = minAgeDays;
        }

        /// <summary>Нулира резултатите от предишно сканиране/чистене (за повторно сканиране).</summary>
        public void ResetScan()
        {
            ScannedBytes = 0;
            ScannedFiles = 0;
        }

        public void ResetClean()
        {
            FreedBytes = 0;
            FreedFiles = 0;
            Errors = 0;
        }
    }
}
