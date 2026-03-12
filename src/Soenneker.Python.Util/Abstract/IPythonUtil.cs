using System;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Python.Util.Abstract;

/// <summary>
/// A utility library for python related operations
/// </summary>
public interface IPythonUtil
{
    /// <summary>
    /// Returns the absolute path to the Python interpreter resolved from <paramref name="pythonCommand"/>.
    /// </summary>
    /// <param name="pythonCommand">
    /// Command or launcher to invoke (e.g., <c>"python"</c>, <c>"python3"</c>, <c>"py -3"</c>).  
    /// Defaults to <c>"python"</c>.
    /// </param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    [Pure]
    ValueTask<string> GetPythonPath(string pythonCommand = "python", CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures that an interpreter at least <paramref name="minVersion"/> exists.
    /// </summary>
    /// <param name="minVersion">Minimum acceptable version (e.g., <c>"3.11"</c>). Default is <c>"3.11"</c>.</param>
    /// <param name="installIfMissing">Attempt to install via winget / brew / apt if not found.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Full path to the interpreter that satisfies the requirement.</returns>
    ValueTask<string> EnsureInstalled(string minVersion = "3.11", bool installIfMissing = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes the platform-appropriate package manager to install the specified Python version.
    /// </summary>
    /// <param name="min">Version object describing the major/minor release to install (e.g., 3.11).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    ValueTask TryInstall(Version min, CancellationToken cancellationToken = default);
}