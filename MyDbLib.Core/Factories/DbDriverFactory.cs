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

        public DbDriverFactory(IServiceProvider provider)
        {
            _provider = provider;
        }

        public IDbDriver Get(string name)
        {
            var registrations = _provider
                .GetServices<DbDriverRegistration>()
                .ToList();

            var registration = registrations
                .FirstOrDefault(r => r.Name == name);

            if (registration == null)
                throw new DbLibException($"Database '{name}' is not registered.");

            return (IDbDriver)_provider.GetRequiredService(registration.DriverType);
        }
    }
}
