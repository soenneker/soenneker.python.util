[![](https://img.shields.io/nuget/v/soenneker.python.util.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.python.util/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.python.util/build-and-test.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.python.util/actions/workflows/build-and-test.yml)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.python.util/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.python.util/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.python.util.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.python.util/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.python.util/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.python.util/actions/workflows/codeql.yml)

# Soenneker.Python.Util

Locates a specific Python major/minor version and can install it through the host's package manager.

## Installation

```bash
dotnet add package Soenneker.Python.Util
```

## Registration

```csharp
using Soenneker.Python.Util.Registrars;

services.AddPythonUtilAsSingleton();
```

## Locate an interpreter

```csharp
using Soenneker.Python.Util.Abstract;

IPythonUtil python = serviceProvider.GetRequiredService<IPythonUtil>();

string path = await python.EnsureInstalled(
    minVersion: "3.12",
    installIfMissing: false,
    cancellationToken: cancellationToken);
```

Despite the parameter's compatibility name, the version is matched by exact major/minor: `3.12` accepts any Python 3.12 patch release, not Python 3.13.

The lookup checks common launchers, the GitHub/Azure hosted-tool cache on Windows, and the Windows Python registry. It returns the interpreter path reported by the matching installation.

## Resolve a command directly

```csharp
string defaultPython = await python.GetPythonPath("python", cancellationToken);
string windowsPython = await python.GetPythonPath("py -3", cancellationToken);
string explicitPython = await python.GetPythonPath(
    @"C:\Python312\python.exe",
    cancellationToken);
```

`GetPythonPath` runs the supplied interpreter or launcher and returns `sys.executable`; it does not enforce a version.

## Install when missing

Set `installIfMissing: true`, or call `TryInstall(new Version(3, 12), cancellationToken)`, to invoke the platform package manager:

- Windows: `winget`, falling back to Chocolatey.
- macOS: Homebrew.
- Linux: `apt-get` through `sudo`.

Installation changes the machine and may require elevated permissions, accepted package sources, and network access. Prefer `installIfMissing: false` in application code unless machine provisioning is explicitly intended. Cancellation stops the active process call but does not roll back package-manager changes already made.
