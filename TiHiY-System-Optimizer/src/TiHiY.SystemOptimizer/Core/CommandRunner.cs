using System.Diagnostics;
namespace TiHiY.SystemOptimizer.Core;
internal static class CommandRunner
{
    public static async Task<(int ExitCode, string Output, string Error)> RunAsync(string fileName, string arguments)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo { FileName = fileName, Arguments = arguments, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await output, await error);
    }
}
