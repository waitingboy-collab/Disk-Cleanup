# DiskCleaner — Core + Console + GUI (Windows)

Решението има 3 проекта:

- **DiskCleaner.Core** — библиотека с цялата логика (сканиране, чистене, DISM). Никакъв
  Console/GUI код тук — само методи и `Log` събитие, за да може да се ползва еднакво от
  конзолната и графичната програма.
- **DiskCleaner.Console** — старата конзолна версия, вече е тънка обвивка над Core.
- **DiskCleaner.Gui** — нова графична (WinForms) версия: списък с чекбоксове по категория,
  прогрес бар, лог и бутони "Сканирай" / "Изчисти избраните" / "Дълбоко чистене (DISM)".

## Какво чисти

- Temp файлове на потребителя (`%TEMP%`) — само по-стари от 1 ден
- Кеш на Windows Explorer (thumbnails), Windows Error Reporting
- Кеш на Chrome / Edge (Cache, Code Cache, GPUCache, Service Worker кеш) / Firefox
- Стар IE / Edge legacy кеш (INetCache), DirectX Shader кеш (D3DSCache, NVIDIA, AMD)
- Crash dump файлове на потребителя, Кошчето (Recycle Bin)
- **С администраторски права:** Windows\Temp, кеш на Windows Update, Delivery Optimization,
  Prefetch, системни crash dump-ове, стари CBS логове
- **Най-дълбоко ниво (отделен бутон/потвърждение):** `DISM /StartComponentCleanup` — маха
  стари версии на системни компоненти (WinSxS), обикновено освобождава 1-5 GB допълнително

## Изисквания

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) за компилиране

## Как да компилираш

От папката `DiskCleanerSolution` (в `cmd` или PowerShell):

```
dotnet build DiskCleaner.sln -c Release
```

- Конзолен .exe: `DiskCleaner.Console\bin\Release\net8.0\DiskCleaner.exe`
- Графичен .exe: `DiskCleaner.Gui\bin\Release\net8.0-windows\DiskCleanerGui.exe`

### За самостоятелен .exe (без нужда от инсталиран .NET runtime):

```
dotnet publish DiskCleaner.Gui\DiskCleaner.Gui.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish-gui
dotnet publish DiskCleaner.Console\DiskCleaner.Console.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish-console
```

Или отвори `DiskCleaner.sln` директно във Visual Studio и build-ни оттам.

## Употреба

### GUI (`DiskCleanerGui.exe`)
1. Натисни **"Сканирай"** — сканира всички категории и показва размер/брой файлове.
2. Отметни/размахни кои категории искаш (по подразбиране всички непразни са отметнати).
3. Натисни **"Изчисти избраните"** — пита за потвърждение, после трие.
4. Ако не си стартирал като администратор, горе има бутон **"Рестартирай като администратор"**
   за пълен достъп (Windows Temp, Update кеш, DISM).
5. Бутонът **"Дълбоко чистене (DISM)"** е активен само с администраторски права.

### Конзола (`DiskCleaner.exe`)

```
DiskCleaner.exe                # сканира, показва резултат, пита за потвърждение
DiskCleaner.exe --dry-run       # само сканира, нищо не трие
DiskCleaner.exe --yes           # трие без да пита (за автоматизация)
```

## Безопасност

- Temp файловете се трият само ако са по-стари от 1 ден, за да не пипа файлове в активна употреба.
- Заключени файлове просто се пропускат — програмата не спира.
- Достъп до защитени подпапки (напр. `INetCache\Content.IE5`) се обработва поотделно — една
  недостъпна папка не къса цялото обхождане.
- Нищо не се трие без изрично потвърждение (освен с `--yes` в конзолата).
