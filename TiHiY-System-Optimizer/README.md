# TiHiY System Optimizer

TiHiY System Optimizer — простий Windows-застосунок, який аналізує ПК, пояснює рекомендовані зміни людською мовою та застосовує лише те, що користувач підтвердив.

## v0.4

- адаптивний темний інтерфейс для звичайного користувача;
- аналіз CPU, GPU, RAM, Windows і плану живлення;
- безпечні рекомендації Windows з поясненням «що змінить»;
- пошук RSI Launcher, Star Citizen, OBS Studio, Discord і SteelSeries GG / Sonar;
- ручний вибір папки Star Citizen, якщо автопошук не спрацював; шлях перевіряється через `Bin64\StarCitizen.exe` і зберігається для наступних запусків;
- Star Citizen: LIVE / PTU / EPTU / TECH-PREVIEW, USER.cfg, мовна настройка, кількість активних параметрів і кеш шейдерів;
- окрема сторінка «Очищення гри» з аналізом обсягу перед видаленням;
- глибоке безпечне очищення: кеш шейдерів Star Citizen, тимчасові Cache / Code Cache / GPUCache RSI Launcher, Game.log / crash logs;
- додатково, лише за окремим вибором користувача: глобальний DirectX shader cache Windows та NVIDIA/AMD GPU cache;
- `USER.cfg`, Controls/Mappings, профілі керування, `Data.p4k`, виконувані файли гри, Cookies/авторизація RSI Launcher, бібліотека ігор та маніфести не видаляються;
- окремий «netcode cache» не вигадується: DNS, Winsock та мережеві параметри Windows програма не скидає, оскільки це не є кешем Star Citizen і не покращує netcode;
- кнопка відкриття RSI Launcher для використання офіційного Verify; TiHiY не імітує недокументований механізм Verify і не переписує `Data.p4k`;
- сторінка «Стрім»: читання активного профілю OBS, полотна, вихідної роздільної здатності, FPS, енкодера, режиму виводу та частоти аудіо;
- визначення, чи запущені OBS, Discord, SteelSeries GG і процеси Sonar;
- OBS / Discord / Sonar аналізуються лише для читання — їх налаштування автоматично не змінюються;
- точна резервна копія попередніх значень реєстру й плану живлення;
- скасування останніх змін;
- self-contained single EXE для Windows x64.

## Безпека

Програма не змінює BIOS, PBO, Curve Optimizer або EXPO. Перед змінами Windows створюється резервна копія. Глибоке очищення блокується, якщо Star Citizen запущений; кеш RSI Launcher не очищається, поки лаунчер працює. Перед видаленням показуються вибрані категорії та орієнтовний обсяг.

## Перевірка збірки

GitHub Actions:

- компілює .NET 8 Windows Forms застосунок;
- перевіряє, що результат — рівно один автономний EXE;
- запускає автоматичний self-test ручного шляху Star Citizen та реального видалення тестового shader cache;
- запускає застосунок і створює UI snapshot-перевірки, включно з «Очищення гри» на 1040×700 і 1280×800.

Резервні копії: `%ProgramData%\TiHiY\SystemOptimizer\Backups`.
Журнали очищення: `%ProgramData%\TiHiY\SystemOptimizer\CleanupLogs`.
