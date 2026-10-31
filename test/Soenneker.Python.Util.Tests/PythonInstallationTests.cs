using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Soenneker.Python.Util.Abstract;
using Soenneker.Python.Util.Registrars;

namespace Soenneker.Python.Util.Tests;

public sealed class PythonInstallationTests
{
    [Test]
    public async Task LinuxInstallationCanBeRediscoveredAndCreateVenvWithPip(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux())
            return;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPythonUtilAsSingleton();
        await using ServiceProvider provider = services.BuildServiceProvider();
        var python = provider.GetRequiredService<IPythonUtil>();

        string interpreter = await python.EnsureInstalled("3.12", cancellationToken: cancellationToken);
        string rediscovered = await python.EnsureInstalled("3.12", installIfMissing: false, cancellationToken: cancellationToken);
        await Assert.That(rediscovered).IsEqualTo(interpreter);
        string version = await python.Run(interpreter, ["-c", "import sys; print(f'{sys.version_info.major}.{sys.version_info.minor}')"],
            cancellationToken: cancellationToken);
        await Assert.That(version.Trim()).IsEqualTo("3.12");

        string directory = Directory.CreateTempSubdirectory("python-installation-test-").FullName;
        try
        {
            string venv = await python.EnsureVirtualEnvironment(interpreter, Path.Combine(directory, "venv with spaces"),
                TimeSpan.FromMinutes(1), cancellationToken);
            string pip = await python.Run(venv, ["-m", "pip", "--version"], cancellationToken: cancellationToken);
            await Assert.That(pip).Contains("pip ");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
