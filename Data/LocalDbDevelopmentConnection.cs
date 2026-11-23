using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace NutriTrackAI.Data;

public static class LocalDbDevelopmentConnection
{
    // Start through the installed LocalDB utility, then resolve the current pipe.
    // Never fall back to the SqlClient auto-start API after utility failure.
    public static async Task<string> ResolveAsync(string connectionString, bool development,
        Func<string, Task<string?>>? lookup = null, Func<string, Task>? start = null)
    {
        if (!development || !OperatingSystem.IsWindows()) return connectionString;
        var builder = new SqlConnectionStringBuilder(connectionString);
        const string prefix = @"(localdb)\";
        if (!builder.DataSource.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return connectionString;
        var instance = builder.DataSource[prefix.Length..];
        var readInfo = lookup ?? ReadInstanceInfoAsync;
        var output = await readInfo(instance);
        var pipe = ParsePipe(output);
        if (pipe == null)
        {
            await (start ?? StartInstanceAsync)(instance);
            // info from before startup has no pipe. Obtain the NEW instance info.
            output = await readInfo(instance);
            pipe = ParsePipe(output);
            if (pipe == null)
                throw new InvalidOperationException($"LocalDB '{instance}' chưa cung cấp named pipe sau khi khởi động. Kiểm tra bằng SqlLocalDB info {instance}; không xóa hoặc tạo lại database.");
        }
        builder.DataSource = pipe;
        return builder.ConnectionString;
    }

    public static string? ParsePipe(string? output)
    {
        var match = Regex.Match(output ?? "", @"np:\\\\\.\\pipe\\LOCALDB#[a-z0-9]+\\tsql\\query", RegexOptions.IgnoreCase);
        return match.Success ? match.Value : null;
    }

    private static async Task<string?> ReadInstanceInfoAsync(string instance)
    {
        var (exitCode, output, _) = await RunUtilityAsync("info", instance);
        return exitCode == 0 ? output : null;
    }

    private static async Task StartInstanceAsync(string instance)
    {
        var (exitCode, output, error) = await RunUtilityAsync("start", instance);
        if (exitCode != 0)
            throw new InvalidOperationException($"Không khởi động được LocalDB '{instance}' bằng SqlLocalDB start. Chi tiết: {error.Trim()} {output.Trim()}");
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunUtilityAsync(string command, string instance)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft SQL Server");
        if (!Directory.Exists(root)) throw new InvalidOperationException("Chưa tìm thấy SQL Server LocalDB trên máy.");
        var executable = Directory.GetDirectories(root).OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(p => Path.Combine(p, "Tools", "Binn", "SqlLocalDB.exe")).FirstOrDefault(File.Exists);
        if (executable == null) throw new InvalidOperationException("Chưa tìm thấy SqlLocalDB.exe. Hãy kiểm tra cài đặt SQL Server LocalDB.");
        try
        {
            using var process = new Process { StartInfo = new ProcessStartInfo(executable) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            } };
            process.StartInfo.ArgumentList.Add(command);
            process.StartInfo.ArgumentList.Add(instance);
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                process.Kill();
                throw new TimeoutException($"SqlLocalDB {command} quá thời gian chờ. Kiểm tra instance '{instance}' và Windows Application log.");
            }
            return (process.ExitCode, await stdout, await stderr);
        }
        catch (Win32Exception ex) { throw new InvalidOperationException("Không chạy được SqlLocalDB.exe trong phiên Windows hiện tại.", ex); }
        catch (UnauthorizedAccessException ex) { throw new InvalidOperationException("Phiên Windows hiện tại không có quyền truy cập SqlLocalDB.exe.", ex); }
    }
}
