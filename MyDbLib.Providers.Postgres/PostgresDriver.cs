using MyDbLib.Core.Base;
using MyDbLib.Api.Interfaces;
using Npgsql;
using System.Data.Common;

namespace MyDbLib.Providers.Postgres
{
    public sealed class PostgresDriver : DbDriverBase
    {
        public static int InstanceCount;

        protected override string IdentitySelectSql
            => "SELECT LASTVAL();"; // or: RETURNING id (see note below)

        public PostgresDriver(string connectionString, IRetryPolicy retry)
            : base(connectionString, retry)
        {
            InstanceCount++;
        }

        protected override DbConnection CreateConnection()
            => new NpgsqlConnection(ConnectionString);
    }
}
