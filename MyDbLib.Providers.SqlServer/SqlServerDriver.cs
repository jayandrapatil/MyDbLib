using MyDbLib.Api.Interfaces;
using MyDbLib.Core.Base;
using System;
using System.Data.Common;
using System.Data.SqlClient;

namespace MyDbLib.Providers.SqlServer
{
    /// <summary>
    /// SQL Server implementation of DbDriverBase.
    /// </summary>
    public sealed class SqlServerDriver : DbDriverBase
    {
        public static int InstanceCount = 0;

        protected override string IdentitySelectSql => "SELECT CAST(SCOPE_IDENTITY() AS INT);";

        public SqlServerDriver(
            string connectionString,
            IRetryPolicy retryPolicy)
            : base(connectionString, retryPolicy)
        {
            InstanceCount++;
            Console.WriteLine("SqlServerDriver created");
        }

        protected override DbConnection CreateConnection()
        {
            return new SqlConnection(ConnectionString);
        }
    }
}
