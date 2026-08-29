[![](https://img.shields.io/nuget/v/soenneker.python.util.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.python.util/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.python.util/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.python.util/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.python.util.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.python.util/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.python.util/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.python.util/actions/workflows/codeql.yml)

# Soenneker.Python.Util

A utility library for python related operations.

## Install

```bash
dotnet add package Soenneker.Python.Util
```

## Quick start

```csharp
using Soenneker.Python.Util.Registrars;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
var result = services.AddPythonUtilAsSingleton();
```

Adds `IPythonUtil` as a singleton service.

## What you get

- `IPythonUtil` — A utility library for python related operations.
- `PythonUtilRegistrar` — A utility library for python related operations.

## API at a glance

| API | What it does | Result / important behavior |
| --- | --- | --- |
| `IPythonUtil.GetPythonPath(pythonCommand, cancellationToken)` | Returns the absolute path to the Python interpreter resolved from `pythonCommand`. | A task whose result is the text returned by get Python Path. |
| `IPythonUtil.EnsureInstalled(minVersion, installIfMissing, cancellationToken)` | Ensures that an interpreter at least `minVersion` exists. | Full path to the interpreter that satisfies the requirement. |
| `IPythonUtil.TryInstall(min, cancellationToken)` | Invokes the platform-appropriate package manager to install the specified Python version. | A task that completes when the try install operation is complete. |
| `PythonUtilRegistrar.AddPythonUtilAsSingleton(services)` | Adds `IPythonUtil` as a singleton service. | The same service collection, so additional registrations can be chained. |
| `PythonUtilRegistrar.AddPythonUtilAsScoped(services)` | Adds `IPythonUtil` as a scoped service. | The same service collection, so additional registrations can be chained. |

## Practical notes

- Cancellation stops pending work; it does not undo work that has already completed.
