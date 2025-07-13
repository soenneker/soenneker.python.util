using Soenneker.Extensions.ValueTask;
using Soenneker.Python.Util.Abstract;
using Soenneker.Utils.Json;
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

    public PythonUtil(IProcessUtil processUtil, ILogger<PythonUtil> logger)
    {
        _processUtil = processUtil;
        _logger = logger;
    }

    public async ValueTask<string> GetPythonPath(string pythonCommand = "python", CancellationToken cancellationToken = default)
    {
        string result = await _processUtil.StartAndGetOutput(pythonCommand, "-c \"import sys; print(sys.executable)\"", cancellationToken: cancellationToken)
                                          .NoSync();

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
            await TryInstall(required, cancellationToken);

            if (await TryLocate(required, cancellationToken).NoSync() is { } installed)
                return installed;
        }

        throw new InvalidOperationException($"Python {version} not found.");
    }

    private async ValueTask<string?> TryLocate(Version required, CancellationToken ct)
    {
        string[] commands = OperatingSystem.IsWindows() ? ["python", "py -3", "python3"] : ["python3", "python"];

        foreach (string cmd in commands)
            if (await Probe(cmd, required, ct).NoSync() is { } found)
                return found;

#if WINDOWS
        if (ProbeRegistry(required, out string? reg))
            return reg;
#endif
        return null;
    }

    private async ValueTask<string?> Probe(string command, Version target, CancellationToken ct)
    {
        Split(command, out string file, out string extra);

        const string script = "-c \"import json, sys, platform;" + "print(json.dumps([sys.executable, platform.python_version()]))\"";

        string json;
        try
        {
            json = await _processUtil.StartAndGetOutput(file, $"{extra} {script}", cancellationToken: ct).NoSync();
        }
        catch
        {
            return null; // executable not found
        }

        try
        {
            string[] data = JsonUtil.Deserialize<string[]>(json)!;
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

        foreach (RegistryKey hive in new[] {Registry.CurrentUser, Registry.LocalMachine})
        {
            using RegistryKey? baseKey = hive.OpenSubKey(root);
            if (baseKey is null) continue;

            foreach (string tag in baseKey.GetSubKeyNames())
            {
                if (!Version.TryParse(tag[..3], out Version? v) || !MatchMajorMinor(v, target))
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

    public async ValueTask TryInstall(Version version, CancellationToken ct = default)
    {
        string ver = $"{version.Major}.{version.Minor}";

        if (RuntimeUtil.IsLinux())
        {
            // Debian/Ubuntu; adapt for yum/dnf/zypper if needed.
            await _processUtil.BashRun($"sudo apt-get -qq update && sudo apt-get -y install python{ver}", workingDir: "", cancellationToken: ct);
        }
        else if (RuntimeUtil.IsWindows())
        {
            await _processUtil.StartAndGetOutput("winget", $"install --silent --exact --id Python.Python.{ver}", cancellationToken: ct).NoSync();
        }
        else if (RuntimeUtil.IsMacOs())
        {
            await _processUtil.StartAndGetOutput("brew", $"install python@{ver}", cancellationToken: ct).NoSync();
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