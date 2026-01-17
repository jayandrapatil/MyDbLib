using Microsoft.Extensions.DependencyInjection;
using MyDbLib.Api.Interfaces;
using MyDbLib.Core.Factories;
using System;

namespace MyDbLib.Providers.SqlServer
{
    /// <summary>
    /// Registers SQL Server provider for MyDbLib.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMyDbLibSqlServer(
            this IServiceCollection services,
            string name,
            string connectionString)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Database name is required.", nameof(name));

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string is required.", nameof(connectionString));

            // Register SQL Server driver
            services.AddSingleton<SqlServerDriver>(sp =>
            {
                // Driver is created, stores RetryPolicy in base class and returned to factory.Get()
                var retryPolicy = sp.GetRequiredService<IRetryPolicy>();
                return new SqlServerDriver(connectionString, retryPolicy);
            });

            // Register metadata for the Core factory
            services.AddSingleton(
                new DbDriverRegistration(name, typeof(SqlServerDriver))
            );

            return services;
        }
    }
}
