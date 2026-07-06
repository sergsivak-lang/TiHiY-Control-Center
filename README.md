# TiHiY-DED Control Center

Перший GitHub-ready реліз конфігуратора для Star Citizen HOSAM профілю.

## Що вже є

- Avalonia UI / .NET 8
- Відкриття XML Star Citizen
- Перегляд усіх біндів
- Редагування Deadzone/Saturation для T.16000M: X, Y, RotZ, Slider
- Backup XML
- Save As для сумісного XML
- GitHub Actions збірка Windows portable `.exe` без Visual Studio

## Як отримати `.exe` без Visual Studio

1. Створи репозиторій на GitHub.
2. Завантаж усі файли з цього ZIP у репозиторій.
3. Відкрий вкладку **Actions**.
4. Запусти workflow **Build Windows Portable**.
5. Після завершення відкрий job → **Artifacts**.
6. Скачай `TiHiY-Control-Center-win-x64.zip`.
7. Розпакуй і запускай `TiHiY Control Center.exe`.

## Як користуватись програмою

1. Натисни **Відкрити XML**.
2. Обери `layout_300126_exported.xml` або свій актуальний XML.
3. Натисни **Backup XML**.
4. За потреби натисни **Apply TiHiY Defaults**.
5. Натисни **Зберегти як...**.
6. Імпортуй збережений XML у Star Citizen.

Дивись `docs/IMPORT_TO_STAR_CITIZEN.md`.
