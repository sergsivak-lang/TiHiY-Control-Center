using Microsoft.Win32;
using TiHiY.SystemOptimizer.Models;

namespace TiHiY.SystemOptimizer.Core;

public sealed class RecommendationEngine
{
    public IReadOnlyList<OptimizationItem> Analyze(SystemSnapshot snapshot)
    {
        var list = new List<OptimizationItem>();

        AddRegistryRecommendation(
            list,
            id: "game-mode",
            title: "Ігровий режим Windows",
            goodSummary: "Уже ввімкнено — Windows віддає більше уваги грі під час запуску.",
            changeSummary: "Увімкне Ігровий режим Windows, щоб фонові процеси менше заважали грі.",
            details: "Що зробить програма: увімкне стандартний Ігровий режим Windows. Це не розганяє ПК і не вимикає захист Windows — лише просить систему віддавати пріоритет активній грі.",
            path: @"SOFTWARE\Microsoft\GameBar",
            name: "AutoGameModeEnabled",
            recommended: 1,
            hive: RegistryHive.CurrentUser,
            restart: false);

        AddRegistryRecommendation(
            list,
            id: "game-dvr",
            title: "Фоновий запис Xbox",
            goodSummary: "Фоновий запис Xbox уже вимкнений і не витрачає ресурси у фоні.",
            changeSummary: "Вимкне фоновий запис Xbox Game Bar. OBS та інші програми запису це не вимикає.",
            details: "Що зробить програма: вимкне лише фоновий запис Xbox Game Bar у Windows. OBS, NVIDIA App, Discord та інші окремі програми запису не змінюються.",
            path: @"System\GameConfigStore",
            name: "GameDVR_Enabled",
            recommended: 0,
            hive: RegistryHive.CurrentUser,
            restart: false);

        AddRegistryRecommendation(
            list,
            id: "hags",
            title: "Апаратне планування GPU",
            goodSummary: "Уже ввімкнено — Windows може передавати частину планування безпосередньо GPU.",
            changeSummary: "Увімкне апаратне планування GPU. Після зміни потрібне перезавантаження ПК.",
            details: "Що зробить програма: увімкне Hardware-Accelerated GPU Scheduling у Windows. Для застосування потрібне перезавантаження. Якщо на конкретному ПК з'являться проблеми, зміну можна буде відкотити з резервної копії.",
            path: @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
            name: "HwSchMode",
            recommended: 2,
            hive: RegistryHive.LocalMachine,
            restart: true);

        AddRegistryRecommendation(
            list,
            id: "mouse-accel",
            title: "Прискорення миші",
            goodSummary: "Прискорення Windows уже вимкнено — рух миші більш передбачуваний.",
            changeSummary: "Вимкне прискорення миші Windows для більш рівномірного руху та прицілювання.",
            details: "Що зробить програма: вимкне стандартне прискорення миші Windows (MouseSpeed і пороги). DPI, налаштування самої миші та чутливість у грі не змінюються.",
            path: @"Control Panel\Mouse",
            name: "MouseSpeed",
            recommended: 0,
            hive: RegistryHive.CurrentUser,
            restart: false);

        var balanced = snapshot.PowerPlan.Contains("Balanced", StringComparison.OrdinalIgnoreCase)
            || snapshot.PowerPlan.Contains("Збаланс", StringComparison.OrdinalIgnoreCase);

        list.Add(new OptimizationItem
        {
            Id = "power-plan",
            Title = "План живлення Windows",
            Summary = balanced
                ? "Уже використовується збалансований план — процесор сам піднімає частоти, коли це потрібно."
                : "Перемкне Windows на збалансований план живлення без ручного обмеження частот процесора.",
            Details = "Що зробить програма: виконає стандартну команду Windows powercfg /setactive SCHEME_BALANCED. Налаштування BIOS, PBO, Curve Optimizer та EXPO не змінюються.",
            CurrentValue = snapshot.PowerPlan,
            RecommendedValue = "Windows Balanced",
            Level = balanced ? RecommendationLevel.Good : RecommendationLevel.Recommended,
            Selected = !balanced,
            RequiresRestart = false
        });

        if (snapshot.StarCitizenFound && snapshot.StarCitizenShaderCaches.Count > 0)
        {
            var cacheCount = snapshot.StarCitizenShaderCaches.Count;
            list.Add(new OptimizationItem
            {
                Id = "sc-shaders",
                Title = "Star Citizen — кеш шейдерів",
                Summary = $"Знайдено старий кеш шейдерів ({cacheCount}). Можна безпечно очистити перед наступним запуском гри.",
                Details = "Що зробить програма: закриє дію, якщо Star Citizen зараз запущений, а після підтвердження видалить лише знайдені папки кешу шейдерів Star Citizen. Файли гри, налаштування керування та USER.cfg не видаляються. Після очищення гра створить кеш заново; перший запуск може мати короткі підфризи, поки шейдери перебудовуються.",
                CurrentValue = $"Кеш знайдено: {cacheCount}",
                RecommendedValue = "Очистити кеш",
                Level = RecommendationLevel.Recommended,
                Selected = true,
                RequiresRestart = false
            });
        }

        return list;
    }

    private static void AddRegistryRecommendation(
        List<OptimizationItem> list,
        string id,
        string title,
        string goodSummary,
        string changeSummary,
        string details,
        string path,
        string name,
        int recommended,
        RegistryHive hive,
        bool restart)
    {
        object? value = null;
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(path);
            value = key?.GetValue(name);
        }
        catch
        {
            // If a setting cannot be read, show it as not configured instead of failing the full scan.
        }

        var good = value is not null && Convert.ToString(value) == recommended.ToString();
        list.Add(new OptimizationItem
        {
            Id = id,
            Title = title,
            Summary = good ? goodSummary : changeSummary,
            Details = details,
            CurrentValue = value?.ToString() ?? "Не задано",
            RecommendedValue = recommended.ToString(),
            Level = good ? RecommendationLevel.Good : RecommendationLevel.Recommended,
            Selected = !good,
            RequiresRestart = restart
        });
    }
}
