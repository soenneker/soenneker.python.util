using System;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Python.Util.Abstract;

/// <summary>A persistent Python process communicating with newline-delimited input and output.</summary>
public interface IPythonSession : IAsyncDisposable
{
    /// <summary>Serializes exchanges, writes one input line, and reads one output line.
    /// Cancellation while an exchange is active closes the session so delayed output cannot reach another caller.
    /// A cancellation while queued leaves the active exchange untouched. Embedded CR/LF in input is not accepted.</summary>
    ValueTask<string> Exchange(string input, CancellationToken cancellationToken = default);
}
