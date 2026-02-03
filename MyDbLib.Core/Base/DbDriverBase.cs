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

        protected virtual string IdentitySelectSql => "SELECT SCOPE_IDENTITY();";

        protected DbDriverBase(string connectionString, IRetryPolicy retryPolicy)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new DbLibException("Connection string cannot be empty.");

            ConnectionString = connectionString;
            RetryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        }

        #region SAFETY
        protected async Task<T> SafeAsync<T>(Func<Task<T>> action)
        {
            try
            {
                return await RetryPolicy.ExecuteAsync(action);
            }
            catch (DbException ex)
            {
                throw new DbLibException($"Database operation failed: {ex.Message}", ex);
            }
        }

        protected T Safe<T>(Func<T> action)
        {
            try
            {
                return RetryPolicy.Execute(action);
            }
            catch (DbException ex)
            {
                throw new DbLibException($"Database operation failed: {ex.Message}", ex);
            }
        }

        #endregion
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

            // Case 1: Dictionary<string, object>
            if (parameters is IDictionary<string, object> dict)
            {
                foreach (var kv in dict)
                {
                    var p = command.CreateParameter();
                    p.ParameterName = "@" + kv.Key;
                    p.Value = kv.Value ?? DBNull.Value;
                    command.Parameters.Add(p);
                }
                return;
            }

            // Case 2: Anonymous / POCO object
            foreach (var prop in parameters.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
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

        #region RAW EXECUTION (SAFE + RETRY)

        public Task<DbCommandResult> ExecuteAsync(string sql, object parameters = null)
        {
            return SafeRawAsync(async () =>
            {
                using var conn = await OpenAsync();
                return await ExecuteInternalAsync(sql, parameters, conn, null);
            });
        }

        public DbCommandResult Execute(string sql, object parameters = null)
        {
            return SafeRaw(() =>
            {
                using var conn = Open();
                return ExecuteInternal(sql, parameters, conn, null);
            });
        }

        public async Task<int> InsertAndGetIdAsync(string sql, object parameters = null)
        {
            return await SafeAsync(async () =>
            {
                sql = ReplaceIdentity(sql);

                using var conn = await OpenAsync();
                using var cmd = CreateCommand(sql, conn);
                AddParameters(cmd, parameters);
                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            });
        }

        public int InsertAndGetId(string sql, object parameters = null)
        {
            return Safe(() =>
            {
                sql = ReplaceIdentity(sql);

                using var conn = Open();
                using var cmd = CreateCommand(sql, conn);
                AddParameters(cmd, parameters);
                var result = cmd.ExecuteScalar();
                return Convert.ToInt32(result);
            });
        }
        #endregion

        #region QUERY (SAFE + RETRY)
        public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object parameters = null) where T : new()
        {
            return await SafeAsync(async () =>
            {
                using var conn = await OpenAsync();
                using var cmd = CreateCommand(sql, conn);
                AddParameters(cmd, parameters);
                using var reader = await cmd.ExecuteReaderAsync();
                return MapToList<T>(reader);
            });
        }

        public IReadOnlyList<T> Query<T>(string sql, object parameters = null) where T : new()
        {
            return Safe(() =>
            {
                using var conn = Open();
                using var cmd = CreateCommand(sql, conn);
                AddParameters(cmd, parameters);
                using var reader = cmd.ExecuteReader();
                return MapToList<T>(reader);
            });
        }

        public async Task<IReadOnlyList<Dictionary<string, object>>> QueryAsync(string sql, object parameters = null)
        {
            return await SafeAsync(async () =>
            {
                using var conn = await OpenAsync();
                using var cmd = CreateCommand(sql, conn);
                AddParameters(cmd, parameters);
                using var reader = await cmd.ExecuteReaderAsync();
                return MapToDictionaryList(reader);
            });
        }

        public IReadOnlyList<Dictionary<string, object>> Query(string sql, object parameters = null)
        {
            return Safe(() =>
            {
                using var conn = Open();
                using var cmd = CreateCommand(sql, conn);
                AddParameters(cmd, parameters);
                using var reader = cmd.ExecuteReader();
                return MapToDictionaryList(reader);
            });
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

        #region INTERNAL TX (NO RETRY)

        internal async Task<int> ExecuteInternalAsync(string sql, object parameters, DbConnection conn, DbTransaction tx)
        {
            try
            {
                using var cmd = CreateCommand(sql, conn);
                cmd.Transaction = tx;
                AddParameters(cmd, parameters);
                return await cmd.ExecuteNonQueryAsync();
            }
            catch (DbException ex)
            {
                throw new DbLibException(
                    $"Database transaction execute failed: {ex.Message}", ex);
            }
        }

        internal int ExecuteInternal(string sql, object parameters, DbConnection conn, DbTransaction tx)
        {
            try
            {
                using var cmd = CreateCommand(sql, conn);
                cmd.Transaction = tx;
                AddParameters(cmd, parameters);
                return cmd.ExecuteNonQuery();
            }
            catch (DbException ex)
            {
                throw new DbLibException(
                    $"Database transaction execute failed: {ex.Message}", ex);
            }
        }

        internal async Task<IReadOnlyList<T>> QueryInternalAsync<T>(string sql, object parameters, DbConnection conn, DbTransaction tx) where T : new()
        {
            try
            {
                using var cmd = CreateCommand(sql, conn);
                cmd.Transaction = tx;
                AddParameters(cmd, parameters);
                using var reader = await cmd.ExecuteReaderAsync();
                return MapToList<T>(reader);
            }
            catch (DbException ex)
            {
                throw new DbLibException(
                    $"Database transaction execute failed: {ex.Message}", ex);
            }
        }

        internal IReadOnlyList<T> QueryInternal<T>(string sql, object parameters, DbConnection conn, DbTransaction tx) where T : new()
        {
            try
            {
                using var cmd = CreateCommand(sql, conn);
                cmd.Transaction = tx;
                AddParameters(cmd, parameters);
                using var reader = cmd.ExecuteReader();
                return MapToList<T>(reader);
            }
            catch (DbException ex)
            {
                throw new DbLibException(
                    $"Database transaction execute failed: {ex.Message}", ex);
            }
        }

        internal async Task<IReadOnlyList<Dictionary<string, object>>> QueryInternalAsync(string sql, object parameters, DbConnection conn, DbTransaction tx)
        {
            try
            {
                using var cmd = CreateCommand(sql, conn);
                cmd.Transaction = tx;
                AddParameters(cmd, parameters);
                using var reader = await cmd.ExecuteReaderAsync();
                return MapToDictionaryList(reader);
            }
            catch (DbException ex)
            {
                throw new DbLibException(
                    $"Database transaction execute failed: {ex.Message}", ex);
            }
        }

        internal IReadOnlyList<Dictionary<string, object>> QueryInternal(string sql, object parameters, DbConnection conn, DbTransaction tx)
        {
            try
            {
                using var cmd = CreateCommand(sql, conn);
                cmd.Transaction = tx;
                AddParameters(cmd, parameters);
                using var reader = cmd.ExecuteReader();
                return MapToDictionaryList(reader);
            }
            catch (DbException ex)
            {
                throw new DbLibException(
                    $"Database transaction execute failed: {ex.Message}", ex);
            }
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

        internal async Task<int> InsertAndGetIdInternalAsync(string sql, object parameters, DbConnection conn, DbTransaction tx)
        {
            try
            {
                sql = ReplaceIdentity(sql);

                using var cmd = CreateCommand(sql, conn);
                cmd.Transaction = tx;
                AddParameters(cmd, parameters);
                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (DbException ex)
            {
                throw new DbLibException(
                    $"Database transaction execute failed: {ex.Message}", ex);
            }
        }

        internal int InsertAndGetIdInternal(string sql, object parameters, DbConnection conn, DbTransaction tx)
        {
            try
            {
                sql = ReplaceIdentity(sql);

                using var cmd = CreateCommand(sql, conn);
                cmd.Transaction = tx;
                AddParameters(cmd, parameters);
                var result = cmd.ExecuteScalar();
                return Convert.ToInt32(result);
            }
            catch (DbException ex)
            {
                throw new DbLibException(
                    $"Database transaction execute failed: {ex.Message}", ex);
            }
        }
        #endregion

        #region RAW SAFE HELPERS

        protected DbCommandResult SafeRaw(Func<int> action)
        {
            try
            {
                var affected = RetryPolicy.Execute(action);
                return DbCommandResult.Ok(affected);
            }
            catch (DbException ex)
            {
                return DbCommandResult.Fail(
                    ex.ErrorCode.ToString(),
                    ex.Message
                );
            }
            catch (Exception ex)
            {
                return DbCommandResult.Fail(
                    "General Error",
                    ex.Message
                );
            }
        }

        protected async Task<DbCommandResult> SafeRawAsync(Func<Task<int>> action)
        {
            try
            {
                var affected = await RetryPolicy.ExecuteAsync(action);
                return DbCommandResult.Ok(affected);
            }
            catch (DbException ex)
            {
                return DbCommandResult.Fail(
                    ex.ErrorCode.ToString(),
                    ex.Message
                );
            }
            catch (Exception ex)
            {
                return DbCommandResult.Fail(
                    "General Error",
                    ex.Message
                );
            }
        }

        #endregion


        #region MAPPERS

        private static List<Dictionary<string, object>> MapToDictionaryList(DbDataReader reader)
        {
            var list = new List<Dictionary<string, object>>();

            while (reader.Read())
            {
                var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var val = reader.GetValue(i);
                    row[reader.GetName(i)] = val == DBNull.Value ? null : val;
                }

                list.Add(row);
            }

            return list;
        }

        private static List<T> MapToList<T>(DbDataReader reader) where T : new()
        {
            var list = new List<T>();

            var props = typeof(T)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToArray();

            var colMap = Enumerable.Range(0, reader.FieldCount)
                .ToDictionary(
                    i => reader.GetName(i),
                    i => i,
                    StringComparer.OrdinalIgnoreCase);

            while (reader.Read())
            {
                var obj = new T();

                foreach (var prop in props)
                {
                    if (!colMap.TryGetValue(prop.Name, out var index))
                        continue;

                    var val = reader.GetValue(index);
                    if (val == DBNull.Value) continue;

                    prop.SetValue(obj, Convert.ChangeType(val, prop.PropertyType));
                }

                list.Add(obj);
            }

            return list;
        }

        private string ReplaceIdentity(string sql)
        {
            return sql.Replace("{IDENTITY}", IdentitySelectSql);
        }
        #endregion
    }
}
