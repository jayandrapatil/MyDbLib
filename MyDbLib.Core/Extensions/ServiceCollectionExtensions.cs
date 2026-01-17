using Microsoft.Extensions.DependencyInjection;
using MyDbLib.Api;
using MyDbLib.Api.Interfaces;
using MyDbLib.Core.Factories;
using MyDbLib.Core.Resilience;
using System;
using System.Collections.Generic;

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
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // Retry policy (shared, provider-agnostic)
            // Whenever someone asks for IRetryPolicy, give them a RetryPolicy object
            // DI stores this entry:
            //          ServiceType: IRetryPolicy
            //          Lifetime: Singleton
            //          Factory: () => new RetryPolicy(3, 200ms)
            //          Instance: null(not created yet)
            services.AddSingleton<IRetryPolicy>(_ =>
                new RetryPolicy(
                    maxRetries: 3,
                    delay: TimeSpan.FromMilliseconds(200)
                )
            );

            // Register the central factory
            // DI stores: IDbDriverFactory → DbDriverFactory
            // No object created yet, Only metadata is stored
            services.AddSingleton<IDbDriverFactory, DbDriverFactory>();

            return services;
        }
    }
}
