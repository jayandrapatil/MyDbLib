using Microsoft.Extensions.DependencyInjection;
using MyDbLib.Api;
using MyDbLib.Core.Factories;

namespace MyDbLib.Core.Extensions
{
    /// <summary>
    /// Registers core MyDbLib services.
    /// Must be called ONCE.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMyDbLibCore(
            this IServiceCollection services)
        {
            // Register the central factory
            services.AddSingleton<IDbDriverFactory, DbDriverFactory>();

            return services;
        }
    }
}
