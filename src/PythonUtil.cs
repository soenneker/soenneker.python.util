using Soenneker.Python.Util.Abstract;
using Soenneker.Utils.Process.Abstract;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.Task;

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
        var processStartInfo = new ProcessStartInfo
        {
            FileName = pythonCommand,
            Arguments = "-c \"import sys; print(sys.executable)\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = processStartInfo;
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken).NoSync();
        await process.WaitForExitAsync(cancellationToken).NoSync();

        return output.Trim();
    }
}