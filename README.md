# TiHiY Control Center v0.1

Реальний GitHub-ready проєкт для збірки Windows-програми через GitHub Actions без Visual Studio на ПК.

## Що вже є
- Avalonia UI / .NET 8
- Відкриття прикладного Star Citizen XML з `assets/profiles/layout_300126_exported.xml`
- Перегляд `deviceoptions` / осей
- Перегляд усіх rebind-біндів
- Backup XML
- Apply TiHiY Defaults для T.16000M
- Export XML на Desktop
- GitHub Actions для збірки Windows portable `.exe`

## Як завантажити на GitHub
1. Розпакуй ZIP.
2. У репозиторії GitHub натисни `uploading an existing file`.
3. Перетягни ВМІСТ папки, не сам ZIP.
4. Натисни `Commit changes`.
5. Перейди у вкладку `Actions`.
6. Запусти `Build Windows Portable`.
7. Після завершення відкрий run і скачай artifact `TiHiY-Control-Center-win-x64`.
8. Розпакуй artifact і запусти `TiHiY.ControlCenter.exe`.

## Важливо
Це перша робоча база. Тут ще немає live-тестера фізичних кнопок джойстика. Це буде наступний етап.
