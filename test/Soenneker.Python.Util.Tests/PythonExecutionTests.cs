using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Soenneker.Python.Util.Abstract;
using Soenneker.Python.Util.Registrars;

namespace Soenneker.Python.Util.Tests;

public sealed class PythonExecutionTests
{
    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPythonUtilAsSingleton();
        return services.BuildServiceProvider();
    }

    private static async ValueTask<string> Interpreter(IPythonUtil python)
    {
        if (Environment.GetEnvironmentVariable("DOCTR_TEST_PYTHON") is { Length: > 0 } path)
            return path;
        return (await python.Run(OperatingSystem.IsWindows() ? "py" : "python3",
            OperatingSystem.IsWindows() ? ["-3.11", "-c", "import sys; print(sys.executable)"] : ["-c", "import sys; print(sys.executable)"],
            TimeSpan.FromSeconds(10))).Trim();
    }

    [Test]
    public async Task ScriptArgumentsRoundTripAndStderrDoesNotPolluteStdout()
    {
        await using ServiceProvider provider = CreateProvider();
        var python = provider.GetRequiredService<IPythonUtil>();
        string interpreter = await Interpreter(python);
        string[] arguments = ["", "a b", "résumé 中文", "quote\"inside", "C:\\path with spaces\\", "$(literal); & |", "line\nbreak"];
        string output = await python.RunScript(interpreter, "import json, sys; print('diagnostic', file=sys.stderr); print(json.dumps(sys.argv[1:]))",
            arguments, TimeSpan.FromSeconds(10));
        string[] actual = JsonSerializer.Deserialize<string[]>(output)!;
        for (int i = 0; i < arguments.Length; i++)
            await Assert.That(actual[i]).IsEqualTo(arguments[i]);
    }

    [Test]
    public async Task ScriptFailureIncludesStderrAndTemporaryScriptIsRemoved()
    {
        await using ServiceProvider provider = CreateProvider();
        var python = provider.GetRequiredService<IPythonUtil>();
        string marker = Path.GetTempFileName();
        try
        {
            try
            {
                await python.RunScript(await Interpreter(python),
                    "import pathlib, sys; pathlib.Path(sys.argv[1]).write_text(__file__, encoding='utf-8'); print('expected failure', file=sys.stderr); sys.exit(7)",
                    [marker], TimeSpan.FromSeconds(10));
                throw new Exception("Expected a failed Python execution.");
            }
            catch (InvalidOperationException error)
            {
                await Assert.That(error.Message).Contains("code 7");
                await Assert.That(error.Message).Contains("expected failure");
            }
            await Assert.That(File.Exists(await File.ReadAllTextAsync(marker))).IsFalse();
        }
        finally { File.Delete(marker); }
    }

    [Test]
    public async Task SessionRetainsStateAndSerializesConcurrentExchanges()
    {
        await using ServiceProvider provider = CreateProvider();
        var python = provider.GetRequiredService<IPythonUtil>();
        const string script = "import sys\ncount = 0\nfor line in sys.stdin:\n count += 1\n print(str(count) + ':' + line.strip(), flush=True)\n";
        await using IPythonSession session = await python.StartSession(await Interpreter(python), script);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        string[] responses = await Task.WhenAll(session.Exchange("first", timeout.Token).AsTask(), session.Exchange("second", timeout.Token).AsTask());
        await Assert.That(responses[0]).IsEqualTo("1:first");
        await Assert.That(responses[1]).IsEqualTo("2:second");
    }

    [Test]
    public async Task TimeoutKillsProcessAndCleansScript()
    {
        await using ServiceProvider provider = CreateProvider();
        var python = provider.GetRequiredService<IPythonUtil>();
        string marker = Path.GetTempFileName();
        try
        {
            try
            {
                await python.RunScript(await Interpreter(python),
                    "import pathlib, sys, time; pathlib.Path(sys.argv[1]).write_text(__file__, encoding='utf-8'); time.sleep(60)",
                    [marker], TimeSpan.FromSeconds(1));
                throw new Exception("Expected a timeout.");
            }
            catch (TimeoutException) { }
            await Assert.That(File.Exists(await File.ReadAllTextAsync(marker))).IsFalse();
        }
        finally { File.Delete(marker); }
    }
}
