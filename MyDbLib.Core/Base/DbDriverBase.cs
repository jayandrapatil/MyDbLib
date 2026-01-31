using MyDbLib.Api;
using MyDbLib.Api.Exceptions;
using MyDbLib.Api.Interfaces;
using MyDbLib.Api.Models;
using MyDbLib.Core.Resilience;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace MyDbLib.Core.Base
{
    public abstract class DbDriverBase : IDbDriver
    {
        protected string ConnectionString { get; }
        protected IRetryPolicy RetryPolicy { get; }

        protected DbDriverBase(string connectionString, IRetryPolicy retryPolicy)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new DbLibException("Connection string cannot be empty.");

            ConnectionString = connectionString;
            RetryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        }

        protected abstract DbConnection CreateConnection();

        protected async Task<DbConnection> OpenAsync()
        {
            var conn = CreateConnection();
            await conn.OpenAsync();
            return conn;
        }

        protected DbConnection Open()
        {
            var conn = CreateConnection();
            conn.Open();
            return conn;
        }

        protected DbCommand CreateCommand(string sql, DbConnection connection)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new DbLibException("SQL cannot be empty.");

            var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;
            return cmd;
        }

        protected virtual void AddParameters(DbCommand command, object parameters)
        {
            if (parameters == null) return;

            foreach (var prop in parameters.GetType().GetProperties())
            {
                var p = command.CreateParameter();
                p.ParameterName = "@" + prop.Name;
                p.Value = prop.GetValue(parameters) ?? DBNull.Value;
                command.Parameters.Add(p);
            }
        }

        #region TRANSACTIONS

        public async Task<IDbTransactionScope> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            var conn = await OpenAsync();
            var tx = conn.BeginTransaction(isolationLevel);
            return new DbTransactionScope(this, conn, tx);
        }

        public IDbTransactionScope BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            var conn = Open();
            var tx = conn.BeginTransaction(isolationLevel);
            return new DbTransactionScope(this, conn, tx);
        }

        #endregion

        #region RAW EXECUTION (RETRY ONLY HERE)

        public async Task<DbCommandResult> ExecuteAsync(string sql, object parameters = null)
        {
            return await RetryPolicy.ExecuteAsync(async () =>
            {
                using var conn = await OpenAsync();
                var affected = await ExecuteInternalAsync(sql, parameters, conn, null);
                return DbCommandResult.Ok(affected);
            });
        }

        public DbCommandResult Execute(string sql, object parameters = null)
        {
            return RetryPolicy.Execute(() =>
            {
                using var conn = Open();
                var affected = ExecuteInternal(sql, parameters, conn, null);
                return DbCommandResult.Ok(affected);
            });
        }

        public async Task<int> InsertAndGetIdAsync(string sql, object parameters = null)
        {
            using var conn = await OpenAsync();
            using var cmd = CreateCommand(sql, conn);
            AddParameters(cmd, parameters);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public int InsertAndGetId(string sql, object parameters = null)
        {
            using var conn = Open();
            using var cmd = CreateCommand(sql, conn);
            AddParameters(cmd, parameters);
            var result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }

        #endregion

        #region QUERY (RAW)

        public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object parameters = null) where T : new()
        {
            using var conn = await OpenAsync();
            using var cmd = CreateCommand(sql, conn);
            AddParameters(cmd, parameters);
            using var reader = await cmd.ExecuteReaderAsync();
            return MapToList<T>(reader);
        }

        public IReadOnlyList<T> Query<T>(string sql, object parameters = null) where T : new()
        {
            using var conn = Open();
            using var cmd = CreateCommand(sql, conn);
            AddParameters(cmd, parameters);
            using var reader = cmd.ExecuteReader();
            return MapToList<T>(reader);
        }

        public async Task<IReadOnlyList<Dictionary<string, object>>> QueryAsync(string sql, object parameters = null)
        {
            using var conn = await OpenAsync();
            using var cmd = CreateCommand(sql, conn);
            AddParameters(cmd, parameters);
            using var reader = await cmd.ExecuteReaderAsync();
            return MapToDictionaryList(reader);
        }

        public IReadOnlyList<Dictionary<string, object>> Query(string sql, object parameters = null)
        {
            using var conn = Open();
            using var cmd = CreateCommand(sql, conn);
            AddParameters(cmd, parameters);
            using var reader = cmd.ExecuteReader();
            return MapToDictionaryList(reader);
        }

        public async Task<T?> QuerySingleAsync<T>(string sql, object parameters = null) where T : new()
        {
            var list = await QueryAsync<T>(sql, parameters);
            return list.Count == 0 ? default : list[0];
        }

        public T? QuerySingle<T>(string sql, object parameters = null) where T : new()
        {
            var list = Query<T>(sql, parameters);
            return list.Count == 0 ? default : list[0];
        }

        #endregion

        #region INTERNAL TX EXECUTION

        internal async Task<int> ExecuteInternalAsync(string sql, object parameters, DbConnection conn, DbTransaction tx)
        {
            using var cmd = CreateCommand(sql, conn);
            cmd.Transaction = tx;
            AddParameters(cmd, parameters);
            return await cmd.ExecuteNonQueryAsync();
        }

        internal int ExecuteInternal(string sql, object parameters, DbConnection conn, DbTransaction tx)
        {
            using var cmd = CreateCommand(sql, conn);
            cmd.Transaction = tx;
            AddParameters(cmd, parameters);
            return cmd.ExecuteNonQuery();
        }

        internal async Task<IReadOnlyList<T>> QueryInternalAsync<T>(string sql, object parameters, DbConnection conn, DbTransaction tx) where T : new()
        {
            using var cmd = CreateCommand(sql, conn);
            cmd.Transaction = tx;
            AddParameters(cmd, parameters);
            using var reader = await cmd.ExecuteReaderAsync();
            return MapToList<T>(reader);
        }

        internal IReadOnlyList<T> QueryInternal<T>(string sql, object parameters, DbConnection conn, DbTransaction tx) where T : new()
        {
            using var cmd = CreateCommand(sql, conn);
            cmd.Transaction = tx;
            AddParameters(cmd, parameters);
            using var reader = cmd.ExecuteReader();
            return MapToList<T>(reader);
        }

        internal async Task<IReadOnlyList<Dictionary<string, object>>> QueryInternalAsync(string sql, object parameters, DbConnection conn, DbTransaction tx)
        {
            using var cmd = CreateCommand(sql, conn);
            cmd.Transaction = tx;
            AddParameters(cmd, parameters);
            using var reader = await cmd.ExecuteReaderAsync();
            return MapToDictionaryList(reader);
        }

        internal IReadOnlyList<Dictionary<string, object>> QueryInternal(string sql, object parameters, DbConnection conn, DbTransaction tx)
        {
            using var cmd = CreateCommand(sql, conn);
            cmd.Transaction = tx;
            AddParameters(cmd, parameters);
            using var reader = cmd.ExecuteReader();
            return MapToDictionaryList(reader);
        }

        internal async Task<T?> QuerySingleInternalAsync<T>(string sql, object parameters, DbConnection conn, DbTransaction tx) where T : new()
        {
            var list = await QueryInternalAsync<T>(sql, parameters, conn, tx);
            return list.Count == 0 ? default : list[0];
        }

        internal T? QuerySingleInternal<T>(string sql, object parameters, DbConnection conn, DbTransaction tx) where T : new()
        {
            var list = QueryInternal<T>(sql, parameters, conn, tx);
            return list.Count == 0 ? default : list[0];
        }

        #endregion

        #region MAPPERS

        private static List<Dictionary<string, object>> MapToDictionaryList(DbDataReader reader) { /* same as before */ return null; }

        private static List<T> MapToList<T>(DbDataReader reader) where T : new() { /* same as before */ return null; }

        #endregion
    }
}
