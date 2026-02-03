using MyDbLib.Api.Interfaces;
using MyDbLib.Core.Base;
using MySql.Data.MySqlClient;
using System;
using System.Data.Common;

namespace MyDbLib.Providers.MySql
{
    /// <summary>
    /// MySQL implementation of DbDriverBase.
    /// </summary>
    public sealed class MySqlDriver : DbDriverBase
    {
        public static int InstanceCount = 0;

        protected override string IdentitySelectSql => "SELECT LAST_INSERT_ID();";

        public MySqlDriver(
            string connectionString,
            IRetryPolicy retryPolicy)
            : base(connectionString, retryPolicy)
        {
            InstanceCount++;
            Console.WriteLine("MySQLDriver created");
        }

        protected override DbConnection CreateConnection()
        {
            return new MySqlConnection(ConnectionString);
        }
    }
}
