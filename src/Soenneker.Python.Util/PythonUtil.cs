using Soenneker.Utils.File.Abstract;
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
using Soenneker.Utils.File.Download.Abstract;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

#if WINDOWS
using Microsoft.Win32;
using System.Runtime.Versioning;
#endif

namespace Soenneker.Python.Util;

public sealed partial class PythonUtil : IPythonUtil
{
    private readonly IProcessUtil _processUtil;
    private readonly ILogger<PythonUtil> _logger;
    private readonly IDirectoryUtil _directoryUtil;

    private readonly IFileUtil _fileUtil;
    private readonly IFileDownloadUtil _fileDownloadUtil;

    public PythonUtil(IProcessUtil processUtil, ILogger<PythonUtil> logger, IDirectoryUtil directoryUtil, IFileUtil fileUtil,
        IFileDownloadUtil fileDownloadUtil)
    {
        _fileDownloadUtil = fileDownloadUtil;
        _fileUtil = fileUtil;
        _processUtil = processUtil;
        _logger = logger;
        _directoryUtil = directoryUtil;
    }

    public async ValueTask<string> GetPythonPath(string pythonCommand = "python", CancellationToken cancellationToken = default)
    {
        string file;
        string launcherArguments;

        if (await _fileUtil.Exists(pythonCommand, cancellationToken).NoSync())
        {
            file = pythonCommand;
            launcherArguments = string.Empty;
        }
        else
        {
            Split(pythonCommand, out file, out launcherArguments);
        }

        string arguments = string.IsNullOrWhiteSpace(launcherArguments)
            ? "-c \"import sys; print(sys.executable)\""
            : $"{launcherArguments} -c \"import sys; print(sys.executable)\"";

        string result = await _processUtil.StartAndGetOutput(
            file,
            arguments,
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
            : [$"python{required.Major}.{required.Minor}", "python3", "python"];

        foreach (string cmd in commands)
        {
            if (await Probe(cmd, required, ct).NoSync() is { } found)
                return found;
        }

#if WINDOWS
        if (OperatingSystem.IsWindows() && await ProbeRegistry(required, ct).NoSync() is { } reg)
            return reg;
#endif
        if (OperatingSystem.IsLinux())
            return await ProbeManagedPython(required, ct).NoSync();

        return null;
    }

    private async ValueTask<string?> ProbeHostedToolCache(Version target, CancellationToken cancellationToken)
    {
        string root = Environment.GetEnvironmentVariable("RUNNER_TOOL_CACHE")
                      ?? Environment.GetEnvironmentVariable("AGENT_TOOLSDIRECTORY")
                      ?? (OperatingSystem.IsWindows() ? @"C:\hostedtoolcache\windows" : "/opt/hostedtoolcache");

        string pythonRoot = Path.Combine(root, "Python");
        if (!(await _directoryUtil.Exists(pythonRoot, cancellationToken)))
            return null;

        List<string> verDirs = await _directoryUtil.GetAllDirectories(pythonRoot, cancellationToken);
        foreach (string verDir in verDirs)
        {
            if (!Version.TryParse(Path.GetFileName(verDir), out Version? v) || !MatchMajorMinor(v, target))
                continue;

            string architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
            string candidate = OperatingSystem.IsWindows()
                ? Path.Combine(verDir, architecture, "python.exe")
                : Path.Combine(verDir, architecture, "bin", "python3");
            if (await _fileUtil.Exists(candidate, cancellationToken).NoSync())
                if (await Probe(candidate, target, cancellationToken).NoSync() is { } found)
                    return found;
        }

        return null;
    }

    private async ValueTask<string?> Probe(string command, Version target, CancellationToken ct)
    {
        string file = command;
        string extra = string.Empty;
        if (!Path.IsPathFullyQualified(command))
            Split(command, out file, out extra);

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
        catch when (!ct.IsCancellationRequested)
        {
            return null; // executable not found
        }

        try
        {
            string[] data = JsonUtil.Deserialize<string[]>(json, LibraryJsonContext.Get<string[]>())!;

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
    [SupportedOSPlatform("windows")]
    private async ValueTask<string?> ProbeRegistry(Version target, CancellationToken cancellationToken)
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

                if (await _fileUtil.Exists(candidate, cancellationToken).NoSync())
                {
                    return candidate;
                }
            }
        }

        return null;
    }
#endif

    public async ValueTask TryInstall(Version version, CancellationToken cancellationToken = default)
    {
        var ver = $"{version.Major}.{version.Minor}";

        if (RuntimeUtil.IsLinux())
        {
            await InstallManagedPython(version, cancellationToken).NoSync();
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
        else
        {
            throw new PlatformNotSupportedException("Automatic Python installation is not supported on this operating system.");
        }
    }

    private static string ManagedUvPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Soenneker", "Python", "uv", "uv");

    private async ValueTask<string?> FindUv(CancellationToken cancellationToken)
    {
        foreach (string candidate in new[] { ManagedUvPath, "uv" })
        {
            if (await _processUtil.CommandExistsAndRuns(candidate, "--version", TimeSpan.FromSeconds(3), cancellationToken).NoSync())
                return candidate;
        }

        return null;
    }

    private async ValueTask<string?> ProbeManagedPython(Version version, CancellationToken cancellationToken)
    {
        string? uv = await FindUv(cancellationToken).NoSync();
        if (uv is null)
            return null;

        try
        {
            // Discovery must never download Python, including when installIfMissing is false.
            string path = await Run(uv,
                ["python", "find", $"{version.Major}.{version.Minor}", "--managed-python", "--no-python-downloads", "--no-config"],
                TimeSpan.FromSeconds(10), cancellationToken).NoSync();
            return await Probe(path.Trim(), version, cancellationToken).NoSync();
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async ValueTask InstallManagedPython(Version version, CancellationToken cancellationToken)
    {
        string? uv = await FindUv(cancellationToken).NoSync();
        if (uv is null)
        {
            uv = ManagedUvPath;
            string installDirectory = Path.GetDirectoryName(uv)!;
            string temporaryDirectory = await _directoryUtil.CreateTempDirectory(cancellationToken).NoSync();
            string installer = Path.Combine(temporaryDirectory, "install.sh");
            try
            {
                _logger.LogInformation("Installing uv to {InstallDirectory} for managed Python installation.", installDirectory);
                using var downloadTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                downloadTimeout.CancelAfter(TimeSpan.FromMinutes(2));
                string? downloaded = await _fileDownloadUtil.DownloadWithRetry("https://astral.sh/uv/install.sh",
                    filePath: installer, maxRetryAttempts: 3, cancellationToken: downloadTimeout.Token).NoSync();
                downloadTimeout.Token.ThrowIfCancellationRequested();
                if (downloaded is null)
                    throw new InvalidOperationException("Failed to download the uv installer.");
                // Install without sudo, shell profile changes, or reliance on a refreshed PATH.
                await Run("env", [$"UV_UNMANAGED_INSTALL={installDirectory}", "sh", installer],
                    TimeSpan.FromMinutes(5), cancellationToken).NoSync();
            }
            finally
            {
                await _directoryUtil.DeleteIfExists(temporaryDirectory, CancellationToken.None).NoSync();
            }
        }

        await Run(uv, ["python", "install", $"{version.Major}.{version.Minor}", "--no-config"],
            TimeSpan.FromMinutes(10), cancellationToken).NoSync();
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
