using Soenneker.Python.Util.Abstract;
using Soenneker.Utils.Process.Abstract;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Python.Util;

/// <inheritdoc cref="IPythonUtil"/>
public sealed class PythonUtil : IPythonUtil
{
    private readonly IProcessUtil _processUtil;

    public PythonUtil(IProcessUtil processUtil)
    {
        _processUtil = processUtil;
    }

    public async ValueTask<string> GetPythonPath(string pythonCommand = "python", CancellationToken cancellationToken = default)
    {
        string result = await _processUtil.StartAndGetOutput(pythonCommand, "-c \"import sys; print(sys.executable)\"", cancellationToken: cancellationToken).NoSync();

        return result.Trim();
    }
}