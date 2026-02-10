using Soenneker.Extensions.ValueTask;
using Soenneker.Python.Util.Abstract;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.Json;
using System.Collections.Generic;
using Soenneker.Utils.Process.Abstract;
using Soenneker.Utils.Runtime;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#if WINDOWS
using Microsoft.Win32;
#endif

namespace Soenneker.Python.Util;

/// <inheritdoc cref="IPythonUtil"/>
public sealed class PythonUtil : IPythonUtil
{
    private readonly IProcessUtil _processUtil;
    private readonly ILogger<PythonUtil> _logger;
    private readonly IDirectoryUtil _directoryUtil;

    public PythonUtil(IProcessUtil processUtil, ILogger<PythonUtil> logger, IDirectoryUtil directoryUtil)
    {
        _processUtil = processUtil;
        _logger = logger;
        _directoryUtil = directoryUtil;
    }

    public async ValueTask<string> GetPythonPath(string pythonCommand = "python", CancellationToken cancellationToken = default)
    {
        string result = await _processUtil.StartAndGetOutput(
            pythonCommand,
            "-c \"import sys; print(sys.executable)\"",
            "",
            TimeSpan.FromSeconds(3),
            cancellationToken
        ).NoSync();

        return result.Trim();
    }

    public async ValueTask<string> EnsureInstalled(string version = "3.11", bool installIfMissing = true, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Ensuring Python {Version} is installed.", version);

        if (!Version.TryParse(version, out Version? required))
            throw new ArgumentException($"Bad version string \"{version}\".", nameof(version));

        // 1️⃣  Lookup
        if (await TryLocate(required, cancellationToken).NoSync() is { } path)
            return path;

        // 2️⃣  Optional install
        if (installIfMissing)
        {
            await TryInstall(required, cancellationToken).NoSync();

            if (await TryLocate(required, cancellationToken).NoSync() is { } installed)
                return installed;
        }

        throw new InvalidOperationException($"Python {version} not found.");
    }

    private async ValueTask<string?> TryLocate(Version required, CancellationToken ct)
    {
        if (RuntimeUtil.IsWindows())
            if (await ProbeHostedToolCache(required, ct).NoSync() is { } cached)
                return cached;

        string[] commands = OperatingSystem.IsWindows()
            ?
            [
                $"py -{required.Major}.{required.Minor}",
                "python",
                "python3",
                "py -3"
            ]
            : ["python3", "python"];

        foreach (string cmd in commands)
        {
            if (await Probe(cmd, required, ct).NoSync() is { } found)
                return found;
        }

#if WINDOWS
        if (ProbeRegistry(required, out string? reg))
            return reg;
#endif
        return null;
    }

    private async ValueTask<string?> ProbeHostedToolCache(Version target, CancellationToken cancellationToken)
    {
        string root = Environment.GetEnvironmentVariable("AGENT_TOOLSDIRECTORY") ?? @"C:\hostedtoolcache\windows";

        string pythonRoot = Path.Combine(root, "Python");
        if (!(await _directoryUtil.Exists(pythonRoot, cancellationToken)))
            return null;

        List<string> verDirs = await _directoryUtil.GetAllDirectories(pythonRoot, cancellationToken);
        foreach (string verDir in verDirs)
        {
            if (!Version.TryParse(Path.GetFileName(verDir), out Version? v) || !MatchMajorMinor(v, target))
                continue;

            string candidate = Path.Combine(verDir, "x64", "python.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private async ValueTask<string?> Probe(string command, Version target, CancellationToken ct)
    {
        Split(command, out string file, out string extra);

        const string script = "-c \"import json, sys, platform;" + "print(json.dumps([sys.executable, platform.python_version()]))\"";

        string json;
        try
        {
            json = await _processUtil.StartAndGetOutput(
                file,
                $"{extra} {script}",
                "",
                TimeSpan.FromSeconds(3),
                ct
            ).NoSync();
        }
        catch
        {
            return null; // executable not found
        }

        try
        {
            string[] data = JsonUtil.Deserialize<string[]>(json)!;

            if (OperatingSystem.IsWindows() &&
                data[0].IndexOf(@"\AppData\Local\Microsoft\WindowsApps\", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return null;
            }

            Version v = Version.Parse(data[1]);

            return MatchMajorMinor(v, target) ? data[0] : null;
        }
        catch
        {
            return null; // parsing failed
        }
    }

#if WINDOWS
    private static bool ProbeRegistry(Version target, out string? path)
    {
        const string root = @"SOFTWARE\Python\PythonCore";

        foreach (RegistryKey hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using RegistryKey? baseKey = hive.OpenSubKey(root);
            if (baseKey is null) continue;

            foreach (string tag in baseKey.GetSubKeyNames())
            {
                string prefix = tag.Length >= 4 ? tag[..4] : tag;

                if (!Version.TryParse(prefix, out Version? v) || !MatchMajorMinor(v, target))
                    continue;

                using RegistryKey? ip = baseKey.OpenSubKey($@"{tag}\InstallPath");
                string candidate = Path.Combine(ip?.GetValue(null)?.ToString() ?? "", "python.exe");

                if (File.Exists(candidate))
                {
                    path = candidate;
                    return true;
                }
            }
        }

        path = null;
        return false;
    }
#endif

    public async ValueTask TryInstall(Version version, CancellationToken cancellationToken = default)
    {
        var ver = $"{version.Major}.{version.Minor}";

        if (RuntimeUtil.IsLinux())
        {
            // Debian/Ubuntu; adapt for yum/dnf/zypper if needed.
            await _processUtil.BashRun($"sudo apt-get -qq update && sudo apt-get -y install python{ver}", workingDir: "", cancellationToken: cancellationToken);
        }
        else if (RuntimeUtil.IsWindows())
        {
            if (await _processUtil.CommandExistsAndRuns("winget", "--version", TimeSpan.FromSeconds(3), cancellationToken).NoSync())
            {
                await _processUtil.StartAndGetOutput(
                    "winget",
                    $"install --exact --id Python.Python.{ver} " +
                    "--silent --disable-interactivity " +
                    "--accept-source-agreements --accept-package-agreements " +
                    "--source winget",
                    "",
                    TimeSpan.FromMinutes(5),
                    cancellationToken
                ).NoSync();
            }
            else if (await _processUtil.CommandExistsAndRuns("choco", "--version", TimeSpan.FromSeconds(3), cancellationToken).NoSync())
            {
                await _processUtil.StartAndGetOutput(
                    "choco",
                    $"install python --version {ver}.0 -y --no-progress",
                    "",
                    TimeSpan.FromMinutes(5),
                    cancellationToken
                ).NoSync();
            }
            else
            {
                throw new InvalidOperationException("Neither winget nor Chocolatey is available to install Python on this runner.");
            }
        }
        else if (RuntimeUtil.IsMacOs())
        {
            await _processUtil.StartAndGetOutput(
                "brew",
                $"install python@{ver}",
                "",
                TimeSpan.FromMinutes(10),
                cancellationToken
            ).NoSync();
        }
    }

    private static bool MatchMajorMinor(Version found, Version target) =>
        found.Major == target.Major && found.Minor == target.Minor;

    private static void Split(string cmd, out string file, out string extra)
    {
        int i = cmd.IndexOf(' ');
        if (i < 0)
        {
            file = cmd;
            extra = string.Empty;
        }
        else
        {
            file = cmd[..i];
            extra = cmd[(i + 1)..];
        }
    }
}