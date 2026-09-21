using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;

namespace CheckInReminder;

internal static class BundledMediaTools
{
    private static readonly SemaphoreSlim Gate = new(1);
    private static string? directory;
    internal static async Task<string> GetDirectoryAsync(CancellationToken token)
    {
        await Gate.WaitAsync(token);
        try
        {
            if (directory is not null) return directory;
            using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("CheckInReminder.MediaTools.zip")
                ?? throw new InvalidOperationException("此开发构建未包含视频处理组件，请使用完整试用版。");
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(resource, token))[..16];
            var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CheckInReminder", "media-tools", hash);
            Directory.CreateDirectory(cache);
            resource.Position = 0;
            using var zip = new ZipArchive(resource);
            foreach (var entry in zip.Entries)
            {
                token.ThrowIfCancellationRequested();
                if (entry.Name != entry.FullName || entry.Name.Length == 0) throw new InvalidDataException("视频组件包无效。");
                var target = Path.Combine(cache, entry.Name);
                if (File.Exists(target) && new FileInfo(target).Length == entry.Length) continue;
                var temp = target + ".tmp-" + Guid.NewGuid().ToString("N");
                try
                {
                    using (var source = entry.Open())
                    await using (var output = File.Create(temp)) await source.CopyToAsync(output, token);
                    File.Move(temp, target, true);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
            directory = cache;
            return cache;
        }
        finally { Gate.Release(); }
    }

    internal static async Task<string> RunAsync(string tool, IEnumerable<string> arguments, CancellationToken token)
    {
        var folder = await GetDirectoryAsync(token);
        var info = new ProcessStartInfo(Path.Combine(folder, tool + ".exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, WorkingDirectory = folder };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = info };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(stdout, stderr);
            token.ThrowIfCancellationRequested();
            throw new InvalidDataException("素材处理超时，请换用更短或更小的素材。");
        }
        var error = await stderr;
        if (process.ExitCode != 0) throw new InvalidDataException("无法读取此素材，请检查格式或文件是否损坏。" + (error.Length > 0 ? "\n" + error[..Math.Min(error.Length, 400)] : ""));
        return await stdout;
    }
}
