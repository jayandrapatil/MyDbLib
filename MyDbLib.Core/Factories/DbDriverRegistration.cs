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
        public Type DriverType { get; }

        public DbDriverRegistration(string name, Type driverType)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DriverType = driverType ?? throw new ArgumentNullException(nameof(driverType));
        }
    }
}
