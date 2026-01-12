using Microsoft.Extensions.DependencyInjection;
using MyDbLib.Core.Factories;
using System;

namespace MyDbLib.Providers.MySql
{
    /// <summary>
    /// Registers MySQL provider for MyDbLib.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMyDbLibMySql(
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

            // Register MySQL driver
            services.AddSingleton<MySqlDriver>(_ =>
                new MySqlDriver(connectionString)
            );

            // Register metadata for Core factory
            services.AddSingleton(
                new DbDriverRegistration(name, typeof(MySqlDriver))
            );

            return services;
        }
    }
}
