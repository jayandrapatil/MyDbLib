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
        private readonly IReadOnlyDictionary<string, Func<IServiceProvider, IDbDriver>> _factories;

        public DbDriverFactory(
            IServiceProvider provider,
            IEnumerable<DbDriverRegistration> registrations)
        {
            _provider = provider;

            _factories = registrations.ToDictionary(
                r => r.Name,
                r => r.Factory,
                StringComparer.OrdinalIgnoreCase);
        }

        public IDbDriver Get(string name)
        {
            if (!_factories.TryGetValue(name, out var factory))
                throw new DbLibException($"Database '{name}' is not registered.");

            return factory(_provider); // always a new driver
        }
    }
}
