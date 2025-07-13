using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Python.Util.Abstract;

/// <summary>
/// A utility library for python related operations
/// </summary>
public interface IPythonUtil
{
    [Pure]
    ValueTask<string> GetPythonPath(string pythonCommand = "python", CancellationToken cancellationToken = default);
}
