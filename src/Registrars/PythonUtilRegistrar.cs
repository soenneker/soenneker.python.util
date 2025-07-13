using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.Python.Util.Abstract;
using Soenneker.Utils.Process.Registrars;

namespace Soenneker.Python.Util.Registrars;

/// <summary>
/// A utility library for python related operations
/// </summary>
public static class PythonUtilRegistrar
{
    /// <summary>
    /// Adds <see cref="IPythonUtil"/> as a singleton service. <para/>
    /// </summary>
    public static IServiceCollection AddPythonUtilAsSingleton(this IServiceCollection services)
    {
        services.AddProcessUtilAsSingleton().TryAddSingleton<IPythonUtil, PythonUtil>();

        return services;
    }

    /// <summary>
    /// Adds <see cref="IPythonUtil"/> as a scoped service. <para/>
    /// </summary>
    public static IServiceCollection AddPythonUtilAsScoped(this IServiceCollection services)
    {
        services.AddProcessUtilAsScoped().TryAddScoped<IPythonUtil, PythonUtil>();

        return services;
    }
}
