using MyDbLib.Api;
using System;

namespace MyDbLib.Core.Factories
{
    /// <summary>
    /// Describes a database driver registration (name + driver type).
    /// Used internally by the DbDriverFactory.
    /// </summary>
    public sealed class DbDriverRegistration
    {
        public string Name { get; }
        public Func<IServiceProvider, IDbDriver> Factory { get; }

        public DbDriverRegistration(string name, Func<IServiceProvider, IDbDriver> factory)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }
    }
}
