using System;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Python.Util.Abstract;

/// <summary>
/// Locates a requested Python major/minor version and can install it through a platform package manager.
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
}
