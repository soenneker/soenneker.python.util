using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.Python.Util.Abstract;
using Soenneker.Utils.Directory.Registrars;
using Soenneker.Utils.Process.Registrars;

namespace Soenneker.Python.Util.Registrars;

/// <summary>
/// Registers the Python utility and its process and directory dependencies.
/// </summary>
public static class PythonUtilRegistrar
{
    /// <summary>
    /// Adds <see cref="IPythonUtil"/> as a singleton service.
    /// </summary>
    /// <param name="services">Service collection that receives the registration.</param>
    /// <returns>The same service collection, so additional registrations can be chained.</returns>
    public static IServiceCollection AddPythonUtilAsSingleton(this IServiceCollection services)
    {
        services.AddDirectoryUtilAsSingleton().AddProcessUtilAsSingleton().TryAddSingleton<IPythonUtil, PythonUtil>();

        return services;
    }

    /// <summary>
    /// Adds <see cref="IPythonUtil"/> as a scoped service.
    /// </summary>
    /// <param name="services">Service collection that receives the registration.</param>
    /// <returns>The same service collection, so additional registrations can be chained.</returns>
    public static IServiceCollection AddPythonUtilAsScoped(this IServiceCollection services)
    {
        services.AddDirectoryUtilAsScoped().AddProcessUtilAsScoped().TryAddScoped<IPythonUtil, PythonUtil>();

        return services;
    }
}
