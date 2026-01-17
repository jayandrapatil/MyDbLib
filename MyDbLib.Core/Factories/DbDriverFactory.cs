using Microsoft.Extensions.DependencyInjection;
using MyDbLib.Api;
using MyDbLib.Api.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MyDbLib.Core.Factories
{
    public sealed class DbDriverFactory : IDbDriverFactory
    {
        private readonly IServiceProvider _provider;
        private readonly IReadOnlyDictionary<string, Type> _drivers;

        public DbDriverFactory(
        IServiceProvider provider,
        IEnumerable<DbDriverRegistration> registrations)
        {
            _provider = provider;

            _drivers = registrations.ToDictionary(
                r => r.Name,
                r => r.DriverType,
                StringComparer.OrdinalIgnoreCase);
        }

        public IDbDriver Get(string name)
        {
            if (!_drivers.TryGetValue(name, out var type))
                throw new DbLibException($"Database '{name}' is not registered.");

            return (IDbDriver)_provider.GetRequiredService(type);
        }
    }
}
