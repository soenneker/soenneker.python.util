using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Python.Util.Abstract;
using Soenneker.Utils.Process.Dtos;

namespace Soenneker.Python.Util;

public sealed partial class PythonUtil
{
    public async ValueTask<string> Run(string pythonPath, IEnumerable<string> arguments, TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pythonPath);
        ArgumentNullException.ThrowIfNull(arguments);
        if (timeout.HasValue && (timeout.Value <= TimeSpan.Zero || timeout.Value.TotalMilliseconds > uint.MaxValue - 1))
            throw new ArgumentOutOfRangeException(nameof(timeout));
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout.HasValue)
            execution.CancelAfter(timeout.Value);
        var stdout = new ConcurrentQueue<string>();
        var stderr = new ConcurrentQueue<string>();
        using System.Diagnostics.Process? process = await _processUtil.StartDetached(new ProcessStartDto
        {
            FileName = pythonPath, ArgumentList = arguments.ToArray(), Log = false,
            EnvironmentVariables = PythonEnvironmentVariables(),
            OutputCallback = stdout.Enqueue, ErrorCallback = stderr.Enqueue
        }, execution.Token).ConfigureAwait(false);
        if (process is null)
            throw new InvalidOperationException($"Could not start Python interpreter '{pythonPath}'.");
        // Process.Util handles cancellation. Drain its callbacks before disposing the process handle.
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (execution.IsCancellationRequested)
            throw new TimeoutException($"Python execution exceeded {timeout}.");
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Python exited with code {process.ExitCode}.{Environment.NewLine}{string.Join(Environment.NewLine, stderr.TakeLast(40))}");
        return string.Join(Environment.NewLine, stdout);
    }

    public async ValueTask<string> RunScript(string pythonPath, string script, IEnumerable<string>? arguments = null,
        TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        string directory = Directory.CreateTempSubdirectory("soenneker-python-").FullName;
        string path = Path.Combine(directory, "script.py");
        try
        {
            await File.WriteAllTextAsync(path, script, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            return await Run(pythonPath, new[] { "-u", path }.Concat(arguments ?? []), timeout, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(directory);
        }
    }

    public ValueTask<IPythonSession> StartSession(string pythonPath, string script, CancellationToken cancellationToken = default)
        => PythonSession.Start(_processUtil, pythonPath, script, cancellationToken);

    public async ValueTask<string> EnsureVirtualEnvironment(string pythonPath, string directory, TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        cancellationToken.ThrowIfCancellationRequested();
        string interpreter = Path.Combine(Path.GetFullPath(directory), OperatingSystem.IsWindows() ? "Scripts" : "bin",
            OperatingSystem.IsWindows() ? "python.exe" : "python");
        if (!File.Exists(interpreter))
            await Run(pythonPath, ["-m", "venv", Path.GetFullPath(directory)], timeout, cancellationToken).ConfigureAwait(false);
        if (!File.Exists(interpreter))
            throw new InvalidOperationException($"Virtual environment creation did not produce '{interpreter}'.");
        return interpreter;
    }

    public async ValueTask InstallPackages(string pythonPath, IEnumerable<string> packages, string? indexUrl = null,
        TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packages);
        string[] specifications = packages.ToArray();
        if (specifications.Length == 0 || specifications.Any(p => string.IsNullOrWhiteSpace(p) || p.StartsWith('-') || p.Contains('\0')))
            throw new ArgumentException("Provide package specifications; pip options are not accepted.", nameof(packages));
        var arguments = new List<string> { "-m", "pip", "install", "--disable-pip-version-check" };
        if (indexUrl is not null)
        {
            if (!Uri.TryCreate(indexUrl, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new ArgumentException("The package index must be an absolute HTTPS URL.", nameof(indexUrl));
            arguments.Add("--index-url");
            arguments.Add(indexUrl);
        }
        arguments.AddRange(specifications);
        await Run(pythonPath, arguments, timeout, cancellationToken).ConfigureAwait(false);
    }

    internal static Dictionary<string, string> PythonEnvironmentVariables() => new()
    {
        ["PYTHONUTF8"] = "1", ["PYTHONIOENCODING"] = "utf-8"
    };
}
