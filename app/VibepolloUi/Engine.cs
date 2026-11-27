using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace VibepolloUi;

static class Engine
{
    public static string? FindScript()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string? best = null;
        var bestVer = new Version(0, 0);
        var exeDir = AppContext.BaseDirectory;
        var portableScript = Path.Combine(exeDir, "vibepollo-updater");
        var portablePy = Path.Combine(exeDir, "vibepollo-updater.py");
        var repoScript = Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", "vibepollo-updater"));
        foreach (var path in new[]
        {
            Environment.GetEnvironmentVariable("VIBEPOLLO_UPDATER"),
            portableScript,
            portablePy,
            repoScript,
            Path.Combine(home, "src", "vibepollo-updater", "vibepollo-updater"),
            Path.Combine(local, "vibepollo-updater", "bin", "vibepollo-updater.py"),
        })
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;
            var ver = ScriptVersion(path);
            if (best is null || ver > bestVer)
            {
                best = path;
                bestVer = ver;
            }
        }
        return best;
    }

    static Version ScriptVersion(string path)
    {
        try
        {
            foreach (var line in File.ReadLines(path).Take(80))
            {
                const string key = "__version__ = \"";
                var i = line.IndexOf(key, StringComparison.Ordinal);
                if (i < 0)
                    continue;
                var rest = line[(i + key.Length)..];
                var end = rest.IndexOf('"');
                if (end <= 0)
                    continue;
                return Version.TryParse(rest[..end], out var ver) ? ver : new Version(0, 0);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        return new Version(0, 0);
    }

    public static string Python()
    {
        foreach (var name in OperatingSystem.IsWindows()
                     ? new[] { "python.exe", "py.exe" }
                     : new[] { "python3", "python" })
        {
            var found = Which(name);
            if (found is not null)
                return found;
        }
        return OperatingSystem.IsWindows() ? "python.exe" : "python3";
    }

    public static (int code, string stdout, string stderr) Run(
        IReadOnlyList<string> args,
        int timeoutMs,
        Action<string>? onStdout = null)
    {
        var script = FindScript() ?? throw new FileNotFoundException("vibepollo-updater script was not found");
        var psi = new ProcessStartInfo
        {
            FileName = Python(),
            WorkingDirectory = Path.GetDirectoryName(script) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (Path.GetFileName(psi.FileName).Equals("py.exe", StringComparison.OrdinalIgnoreCase))
            psi.ArgumentList.Add("-3");
        psi.ArgumentList.Add("-I");
        psi.ArgumentList.Add(script);
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (stdout) stdout.AppendLine(e.Data);
            onStdout?.Invoke(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (stderr) stderr.AppendLine(e.Data);
        };
        if (!proc.Start())
            throw new InvalidOperationException("could not start " + psi.FileName);
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        if (!proc.WaitForExit(timeoutMs))
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException("vibepollo-updater timed out");
        }
        proc.WaitForExit();
        return (proc.ExitCode, stdout.ToString(), stderr.ToString());
    }

    public static JsonDocument Json(int timeoutMs = 20000)
    {
        var (code, stdout, stderr) = Run(new[] { "--json" }, timeoutMs);
        if (code != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
        return JsonDocument.Parse(stdout);
    }

    static string? Which(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var full = Path.Combine(dir.Trim(), name);
            if (File.Exists(full))
                return full;
        }
        return null;
    }
}
