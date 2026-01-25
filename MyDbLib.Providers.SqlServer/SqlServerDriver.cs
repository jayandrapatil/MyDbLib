using MyDbLib.Api.Interfaces;
using MyDbLib.Core.Base;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;

namespace MyDbLib.Providers.SqlServer
{
    /// <summary>
    /// SQL Server implementation of DbDriverBase.
    /// </summary>
    public sealed class SqlServerDriver : DbDriverBase
    {
        public static int InstanceCount = 0;
        public SqlServerDriver(string connectionString, IRetryPolicy retryPolicy) : base(connectionString, retryPolicy)
        {
            // below is NOT required, but added this to verify the constructed in called only ONCE
            // even though we have added deliberate multiple 'factory.Get("SQLServer");' in Program.cs
            InstanceCount++;
            Console.WriteLine("SqlServerDriver created");
        }

        protected override DbConnection CreateConnection()
        {
            return new SqlConnection(ConnectionString);
        }

        protected override string BuildInsertSql(
            string table,
            IReadOnlyList<string> columns)
        {
            var colList = string.Join(", ", columns);
            var paramList = string.Join(", ", columns.Select(c => "@" + c));

            return $"INSERT INTO {table} ({colList}) VALUES ({paramList});";
        }

        protected override string BuildInsertAndGetIdSql(
            string table,
            IReadOnlyList<string> columns)
        {
            var colList = string.Join(", ", columns);
            var paramList = string.Join(", ", columns.Select(c => "@" + c));

            return $@"
                    INSERT INTO {table} ({colList})
                    VALUES ({paramList});
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";
        }

        // Works for MySQL, MariaDB, SQL Server, Postgres
        protected override string BuildUpdateSql(
            string table,
            IReadOnlyList<string> setColumns,
            IReadOnlyList<string> whereColumns)
        {
            if (setColumns == null || setColumns.Count == 0)
                throw new ArgumentException("SET columns cannot be empty.", nameof(setColumns));

            if (whereColumns == null || whereColumns.Count == 0)
                throw new ArgumentException("WHERE columns cannot be empty.", nameof(whereColumns));

            var setClause = string.Join(", ", setColumns.Select(c => $"{c} = @{c}"));

            var whereClause = string.Join(" AND ", whereColumns.Select(c => $"{c} = @w_{c}"));

            return $"UPDATE {table} SET {setClause} WHERE {whereClause};";
        }

        // Works for MySQL, MariaDB, SQL Server, Postgres
        protected override string BuildDeleteSql(
            string table,
            IReadOnlyList<string> whereColumns)
        {
            if (whereColumns == null || whereColumns.Count == 0)
                throw new ArgumentException("WHERE columns cannot be empty.", nameof(whereColumns));

            var whereClause = string.Join(
                " AND ",
                whereColumns.Select(c => $"{c} = @{c}")
            );

            return $"DELETE FROM {table} WHERE {whereClause};";
        }
    }
}
