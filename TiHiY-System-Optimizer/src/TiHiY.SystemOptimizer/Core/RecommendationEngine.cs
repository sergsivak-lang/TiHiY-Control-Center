using Microsoft.Win32;
using TiHiY.SystemOptimizer.Models;
namespace TiHiY.SystemOptimizer.Core;
public sealed class RecommendationEngine
{
    public IReadOnlyList<OptimizationItem> Analyze(SystemSnapshot snapshot)
    {
        var list = new List<OptimizationItem>();
        Add(list,"game-mode","Ігровий режим Windows","Допомагає Windows віддавати більше ресурсів грі.",@"SOFTWARE\Microsoft\GameBar","AutoGameModeEnabled",1,RegistryHive.CurrentUser,false);
        Add(list,"game-dvr","Фоновий запис Xbox","Вимкнення прихованого фонового запису може зменшити зайве навантаження.",@"System\GameConfigStore","GameDVR_Enabled",0,RegistryHive.CurrentUser,false);
        Add(list,"hags","Апаратне планування GPU","Для сучасної NVIDIA рекомендовано ввімкнути. Потрібне перезавантаження.",@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers","HwSchMode",2,RegistryHive.LocalMachine,true);
        Add(list,"mouse-accel","Прискорення миші","Вимкнення робить прицілювання передбачуванішим.",@"Control Panel\Mouse","MouseSpeed",0,RegistryHive.CurrentUser,false);
        var balanced = snapshot.PowerPlan.Contains("Balanced",StringComparison.OrdinalIgnoreCase)||snapshot.PowerPlan.Contains("Збаланс",StringComparison.OrdinalIgnoreCase);
        list.Add(new OptimizationItem { Id="power-plan", Title="План живлення", Summary=balanced?"Налаштовано правильно.":"Є безпечна рекомендація.", Details="Для Ryzen 9000X3D рекомендовано Windows Balanced: він зберігає швидкий буст і не тримає зайву напругу.", CurrentValue=snapshot.PowerPlan, RecommendedValue="Balanced", Level=balanced?RecommendationLevel.Good:RecommendationLevel.Recommended, Selected=!balanced });
        return list;
    }
    private static void Add(List<OptimizationItem> list,string id,string title,string details,string path,string name,int recommended,RegistryHive hive,bool restart)
    {
        object? value=null; try { using var b=RegistryKey.OpenBaseKey(hive,RegistryView.Registry64); using var k=b.OpenSubKey(path); value=k?.GetValue(name); } catch { }
        var good=value is not null && Convert.ToString(value)==recommended.ToString();
        list.Add(new OptimizationItem { Id=id,Title=title,Summary=good?"Налаштовано правильно.":"Є безпечна рекомендація.",Details=details,CurrentValue=value?.ToString()??"Не задано",RecommendedValue=recommended.ToString(),Level=good?RecommendationLevel.Good:RecommendationLevel.Recommended,Selected=!good,RequiresRestart=restart });
    }
}
