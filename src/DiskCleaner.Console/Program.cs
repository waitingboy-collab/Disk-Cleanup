using System;
using System.Linq;
using DiskCleaner.Core;

namespace DiskCleaner.ConsoleApp
{
    internal static class Program
    {
        private static bool _yes;
        private static bool _dryRun;

        private static int Main(string[] args)
        {
            int exitCode = 0;
            try
            {
                exitCode = Run(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("=== ВЪЗНИКНА ГРЕШКА ===");
                Console.WriteLine(ex.ToString());
                exitCode = 1;
            }

            if (!args.Contains("--yes") && !args.Contains("-y") && !Console.IsInputRedirected)
            {
                Console.WriteLine();
                Console.Write("Натисни ENTER, за да излезеш...");
                Console.ReadLine();
            }

            return exitCode;
        }

        private static int Run(string[] args)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.WriteLine("Тази програма работи само под Windows.");
                return 1;
            }

            Console.OutputEncoding = System.Text.Encoding.UTF8;
            _yes = args.Contains("--yes") || args.Contains("-y");
            _dryRun = args.Contains("--dry-run") || args.Contains("-d");

            var engine = new CleanerEngine();

            PrintHeader(engine.IsAdmin);

            var categories = engine.BuildCategories();

            Console.WriteLine("Сканиране...\n");
            foreach (var cat in categories)
                engine.Scan(cat);

            PrintScanResults(categories, engine.IsAdmin);

            long totalBytes = categories.Sum(c => c.ScannedBytes);
            if (totalBytes == 0)
            {
                Console.WriteLine("\nНяма какво да се чисти. Компютърът вече е чист. ✔");
                return 0;
            }

            if (_dryRun)
            {
                Console.WriteLine("\n(режим --dry-run: нищо не е изтрито)");
                return 0;
            }

            if (!_yes)
            {
                Console.Write($"\nДа изтрия горните файлове и да освободя ~{CleanerEngine.FormatSize(totalBytes)}? [y/N]: ");
                var answer = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(answer) || !answer.Trim().ToLower().StartsWith("y"))
                {
                    Console.WriteLine("Прекратено от потребителя. Нищо не е изтрито.");
                    return 0;
                }
            }

            Console.WriteLine("\nИзтриване...\n");
            foreach (var cat in categories)
                engine.Clean(cat);

            PrintCleanResults(categories, engine.IsAdmin);

            OfferDeepSystemCleanup(engine);

            return 0;
        }

        private static void PrintHeader(bool isAdmin)
        {
            Console.WriteLine("=======================================");
            Console.WriteLine("   DiskCleaner - почистване на диска");
            Console.WriteLine("=======================================");
            Console.WriteLine(isAdmin
                ? "Стартирано с администраторски права (пълно почистване)."
                : "Стартирано без администраторски права.\n" +
                  "Някои категории (Windows Temp, Update кеш) ще бъдат пропуснати.\n" +
                  "За пълно почистване стартирайте като администратор.");
            if (_dryRun) Console.WriteLine("Режим: DRY RUN (само показва, не трие нищо).");
            Console.WriteLine();
        }

        private static void PrintScanResults(System.Collections.Generic.List<CleanCategory> categories, bool isAdmin)
        {
            Console.WriteLine($"{"Категория",-40} {"Файлове",10} {"Размер",12}");
            Console.WriteLine(new string('-', 64));
            foreach (var cat in categories)
            {
                if (cat.IsRecycleBin)
                {
                    Console.WriteLine($"{cat.Name,-40} {"?",10} {"(изпразва се)",12}");
                    continue;
                }
                if (cat.RequiresAdmin && !isAdmin)
                {
                    Console.WriteLine($"{cat.Name,-40} {"-",10} {"(нужни права)",12}");
                    continue;
                }
                Console.WriteLine($"{cat.Name,-40} {cat.ScannedFiles,10} {CleanerEngine.FormatSize(cat.ScannedBytes),12}");
            }
            Console.WriteLine(new string('-', 64));
            long total = categories.Sum(c => c.ScannedBytes) - categories.Count(c => c.IsRecycleBin);
            Console.WriteLine($"{"Общо (без Кошчето)",-40} {"",10} {CleanerEngine.FormatSize(Math.Max(total, 0)),12}");
        }

        private static void PrintCleanResults(System.Collections.Generic.List<CleanCategory> categories, bool isAdmin)
        {
            Console.WriteLine("Резултат:");
            Console.WriteLine(new string('-', 64));
            long totalFreed = 0;
            int totalErrors = 0;
            foreach (var cat in categories)
            {
                if (cat.IsRecycleBin)
                {
                    Console.WriteLine($"{cat.Name,-40} изпразнено");
                    continue;
                }
                if (cat.RequiresAdmin && !isAdmin) continue;

                Console.WriteLine($"{cat.Name,-40} {CleanerEngine.FormatSize(cat.FreedBytes),12}  ({cat.FreedFiles} файла" +
                                   (cat.Errors > 0 ? $", {cat.Errors} пропуснати" : "") + ")");
                totalFreed += cat.FreedBytes;
                totalErrors += cat.Errors;
            }
            Console.WriteLine(new string('-', 64));
            Console.WriteLine($"Освободено пространство: {CleanerEngine.FormatSize(totalFreed)}");
            if (totalErrors > 0)
                Console.WriteLine($"({totalErrors} файла бяха пропуснати - вероятно се ползват от друга програма)");
            Console.WriteLine("\nГотово. ✔");
        }

        private static void OfferDeepSystemCleanup(CleanerEngine engine)
        {
            if (!engine.IsAdmin)
            {
                Console.WriteLine("\nЗа най-дълбоко чистене (WinSxS компонентно хранилище, чрез DISM)\n" +
                                   "стартирай програмата като администратор.");
                return;
            }

            if (_dryRun) return;

            Console.WriteLine("\nИма и по-дълбоко ниво: почистване на старите версии на системни\n" +
                               "компоненти (WinSxS) чрез вградения Windows DISM инструмент.\n" +
                               "Обикновено освобождава допълнителни 1-5 GB. Отнема няколко минути\n" +
                               "и не може да се отмени (старите версии за 'uninstall на updates' се трият).");

            if (!_yes)
            {
                Console.Write("Да пусна ли DISM /StartComponentCleanup сега? [y/N]: ");
                var answer = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(answer) || !answer.Trim().ToLower().StartsWith("y"))
                {
                    Console.WriteLine("Пропуснато.");
                    return;
                }
            }

            Console.WriteLine("\nСтартирам DISM (може да отнеме няколко минути)...\n");
            var result = engine.RunDismCleanup();
            if (!result.Started)
                Console.WriteLine($"Неуспешно стартиране на DISM: {result.Error}");
            else
                Console.WriteLine(result.ExitCode == 0
                    ? "DISM почистването приключи успешно."
                    : $"DISM приключи с код {result.ExitCode}.");
        }
    }
}
