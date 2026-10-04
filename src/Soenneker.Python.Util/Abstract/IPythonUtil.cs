using System;
using System.Diagnostics.Contracts;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Python.Util.Abstract;

/// <summary>
/// Locates and installs Python, executes scripts, and manages virtual environments, packages, and persistent sessions.
/// </summary>
public interface IPythonUtil
{
    /// <summary>
    /// Returns the absolute path to the Python interpreter resolved from <paramref name="pythonCommand"/>.
    /// </summary>
    /// <param name="pythonCommand">Command or launcher to invoke (e.g., <c>"python"</c>, <c>"python3"</c>, <c>"py -3"</c>). Defaults to <c>"python"</c>.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The absolute interpreter path reported by Python.</returns>
    [Pure]
    ValueTask<string> GetPythonPath(string pythonCommand = "python", CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures that an interpreter matching the requested major and minor version exists.
    /// </summary>
    /// <param name="minVersion">Required major/minor version (for example, <c>"3.11"</c>).</param>
    /// <param name="installIfMissing">Whether to invoke the platform package manager when no matching interpreter is found.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The full path to the matching interpreter.</returns>
    ValueTask<string> EnsureInstalled(string minVersion = "3.11", bool installIfMissing = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes the platform-appropriate package manager to install the specified Python version.
    /// </summary>
    /// <param name="min">Version object describing the major/minor release to install (for example, 3.11).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that completes when the package-manager command finishes.</returns>
    ValueTask TryInstall(Version min, CancellationToken cancellationToken = default);

    /// <summary>Runs an interpreter through Process.Util with individually escaped arguments, returning stdout only.
    /// Nonzero exit codes throw with stderr diagnostics. Cancellation and timeout kill the process tree and drain output.</summary>
    /// <param name="pythonPath">Interpreter executable path, without launcher arguments.</param>
    /// <param name="arguments">Individual arguments without shell quoting.</param>
    /// <param name="timeout">Execution timeout, or null to wait indefinitely.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    ValueTask<string> Run(string pythonPath, IEnumerable<string> arguments, TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>Runs Python source from a temporary UTF-8 file, which is removed afterward.
    /// Uses the stdout, failure, timeout, and cancellation behavior of Run.</summary>
    ValueTask<string> RunScript(string pythonPath, string script, IEnumerable<string>? arguments = null,
        TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Starts a persistent line-based Python session through Process.Util. The caller must dispose the session.
    /// Source runs once; Python objects remain loaded between exchanges. Reserve stdout for newline-delimited responses.
    /// Cancellation kills the process tree. Stderr is retained for failure diagnostics.</summary>
    ValueTask<IPythonSession> StartSession(string pythonPath, string script, CancellationToken cancellationToken = default);

    /// <summary>Creates a venv if its interpreter is missing and returns the platform-specific interpreter path.
    /// Callers sharing a directory must coordinate concurrent creation and package installation.</summary>
    ValueTask<string> EnsureVirtualEnvironment(string pythonPath, string directory, TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>Installs package specifications using the interpreter's pip. Pip options are not accepted as packages.</summary>
    /// <param name="pythonPath">Interpreter whose environment receives the packages.</param>
    /// <param name="packages">Package specifications such as python-doctr==1.1.0.</param>
    /// <param name="indexUrl">Optional absolute HTTPS package index.</param>
    /// <param name="timeout">Installation timeout, or null to wait indefinitely.</param>
    /// <param name="cancellationToken">Cancels installation.</param>
    ValueTask InstallPackages(string pythonPath, IEnumerable<string> packages, string? indexUrl = null,
        TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}
