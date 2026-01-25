using MyDbLib.Api;
using MyDbLib.Api.Exceptions;
using MyDbLib.Api.Interfaces;
using MyDbLib.Api.Models;
using MyDbLib.Core.Resilience;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MyDbLib.Core.Base
{
    public abstract class DbDriverBase : IDbDriver
    {
        private static readonly Regex TableNameRegex = new Regex("^[A-Za-z0-9_]+(\\.[A-Za-z0-9_]+)?$", RegexOptions.Compiled);
        protected string ConnectionString { get; }

        protected IRetryPolicy RetryPolicy { get; }

        protected DbDriverBase(string connectionString, IRetryPolicy retryPolicy)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new DbLibException("Connection string cannot be empty.");

            ConnectionString = connectionString;

            RetryPolicy = retryPolicy
                ?? throw new ArgumentNullException(nameof(retryPolicy));
        }

        // ADO.NET
        // 1. DbConnection(abstract)
        // 2. SqlConnection, MySqlConnection(concrete)
        // 3. Open() is already implemented in base workflow
        // this is in-line with Microsoft’s own design.

        // Final takeaway (lock this in)
        // Abstract only what must vary.
        // Implement what must be consistent.

        // this is the Template Method Pattern.
        // Why CreateConnection() is the only abstract one
        // Because it is the only provider-specific operation
        // Each DB has its own DbConnection
        // Only the connection creation varies hence it is abstract
        protected abstract DbConnection CreateConnection();

        // this is NOT abstract because, Opening is same for all DBs
        protected async Task<DbConnection> OpenAsync()
        {
            try
            {
                var conn = CreateConnection();
                await conn.OpenAsync();
                return conn;
            }
            catch (Exception ex)
            {
                throw new DbLibException("Failed to open connection.", ex);
            }
        }

        protected DbConnection Open()
        {
            try
            {
                var conn = CreateConnection();
                conn.Open();
                return conn;
            }
            catch (Exception ex)
            {
                throw new DbLibException("Failed to open connection.", ex);
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using (var conn = CreateConnection())
                {
                    await conn.OpenAsync();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public bool TestConnection()
        {
            try
            {
                using (var conn = CreateConnection())
                {
                    conn.Open();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }


        private static void ValidateTableName(string table)
        {
            if (string.IsNullOrWhiteSpace(table))
                throw new DbLibException("Table name cannot be empty.");

            if (!TableNameRegex.IsMatch(table))
                throw new DbLibException("Invalid table name.");
        }


        // this is NOT abstract because, Opening is same for all DBs
        private DbCommand CreateCommand(string sql, DbConnection connection)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new DbLibException("SQL cannot be empty.");

            if (connection == null)
                throw new DbLibException("Connection is null.");

            var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;
            return cmd;
        }

        protected virtual void AddParameters(DbCommand command, object parameters)
        {
            if (parameters == null)
                return;

            foreach (var prop in parameters.GetType().GetProperties())
            {
                var param = command.CreateParameter();
                param.ParameterName = "@" + prop.Name;
                param.Value = prop.GetValue(parameters) ?? DBNull.Value;

                command.Parameters.Add(param);
            }
        }

        protected virtual void AddParameters(DbCommand command, object parameters, string prefix)
        {
            if (parameters == null)
                return;

            foreach (var prop in parameters.GetType().GetProperties())
            {
                var param = command.CreateParameter();
                param.ParameterName = "@" + prefix + prop.Name;
                param.Value = prop.GetValue(parameters) ?? DBNull.Value;
                command.Parameters.Add(param);
            }
        }

        public async Task<IDbTransactionScope> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            var connection = await OpenAsync();
            var transaction = connection.BeginTransaction(isolationLevel);
            return new DbTransactionScope(this, connection, transaction);
        }

        public IDbTransactionScope BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            var connection = Open();
            var transaction = connection.BeginTransaction(isolationLevel);
            return new DbTransactionScope(this, connection, transaction);
        }


        public async Task<DbCommandResult> ExecuteAsync(
            string sql,
            object parameters = null)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new DbLibException("SQL cannot be empty.");

            try
            {
                return await RetryPolicy.ExecuteAsync(async () =>
                {
                    using (var connection = await OpenAsync())
                    {
                        var affected = await ExecuteInternalAsync(
                            sql,
                            parameters,
                            connection,
                            transaction: null);

                        return new DbCommandResult
                        {
                            Success = true,
                            AffectedRecords = affected
                        };
                    }
                });
            }
            catch (DbException ex)
            {
                return new DbCommandResult
                {
                    Success = false,
                    ErrorCode = ex.ErrorCode.ToString(),
                    ErrorMessage = ex.Message
                };
            }
            catch (Exception ex)
            {
                return new DbCommandResult
                {
                    Success = false,
                    ErrorCode = "GENERAL_ERROR",
                    ErrorMessage = ex.Message
                };
            }
        }

        public DbCommandResult Execute(string sql, object parameters = null)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new DbLibException("SQL cannot be empty.");

            try
            {
                return RetryPolicy.Execute(() =>
                {
                    using (var connection = Open())
                    {
                        var affected = ExecuteInternal(
                            sql,
                            parameters,
                            connection,
                            transaction: null);

                        return new DbCommandResult
                        {
                            Success = true,
                            AffectedRecords = affected
                        };
                    }
                });
            }
            catch (DbException ex)
            {
                return new DbCommandResult
                {
                    Success = false,
                    ErrorCode = ex.ErrorCode.ToString(),
                    ErrorMessage = ex.Message
                };
            }
            catch (Exception ex)
            {
                return new DbCommandResult
                {
                    Success = false,
                    ErrorCode = "GENERAL_ERROR",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<IReadOnlyList<Dictionary<string, object>>> QueryAsync(string sql, object parameters = null)
        {
            return await RetryPolicy.ExecuteAsync(async () =>
            {
                using (var connection = await OpenAsync()) // Creates a DbConnection, Opens it, Returns it, Connection is now open
                using (var command = CreateCommand(sql, connection))
                {
                    if (parameters != null)
                        AddParameters(command, parameters);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        return MapToDictionaryList(reader);
                    }
                }
            });
        }

        public IReadOnlyList<Dictionary<string, object>> Query(string sql, object parameters = null)
        {
            using (var connection = Open())
            using (var command = CreateCommand(sql, connection))
            {
                if (parameters != null)
                    AddParameters(command, parameters);

                using (var reader = command.ExecuteReader())
                {
                    return MapToDictionaryList(reader);
                }
            }
        }

        private static List<Dictionary<string, object>> MapToDictionaryList(DbDataReader reader)
        {
            var result = new List<Dictionary<string, object>>();
            while (reader.Read())
            {
                var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);

                result.Add(row);
            }
            return result;
        }
        public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql,object parameters = null) where T : new()
        {
            return await RetryPolicy.ExecuteAsync(async () =>
            {
                using (var connection = await OpenAsync())
                using (var command = CreateCommand(sql, connection))
                {
                    if (parameters != null)
                        AddParameters(command, parameters);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        return MapToList<T>(reader);
                    }
                }
            });
        }

        public IReadOnlyList<T> Query<T>(string sql, object parameters = null) where T : new()
        {
            using (var connection = Open())
            using (var command = CreateCommand(sql, connection))
            {
                if (parameters != null)
                    AddParameters(command, parameters);

                using (var reader = command.ExecuteReader())
                {
                    return MapToList<T>(reader);
                }
            }
        }

        private static List<T> MapToList<T>(DbDataReader reader)
            where T : new()
        {
            var result = new List<T>();

            var properties = typeof(T)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

            while (reader.Read())
            {
                var obj = new T();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);

                    if (!properties.TryGetValue(columnName, out var prop))
                        continue;

                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);

                    if (value == null)
                    {
                        if (Nullable.GetUnderlyingType(prop.PropertyType) != null)
                            prop.SetValue(obj, null);
                        // else ignore for non-nullable value types
                    }
                    else
                    {
                        prop.SetValue(obj, Convert.ChangeType(
                            value,
                            Nullable.GetUnderlyingType(prop.PropertyType)
                                ?? prop.PropertyType));
                    }

                }

                result.Add(obj);
            }

            return result;
        }

        public async Task<T?> QuerySingleAsync<T>(string sql,object? parameters = null) where T : new()
        {
            return await RetryPolicy.ExecuteAsync(async () =>
            {
                using (var connection = await OpenAsync())
                using (var command = CreateCommand(sql, connection))
                {
                    if (parameters != null)
                        AddParameters(command, parameters);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                            return default; // null

                        return Map<T>(reader);
                    }
                }
            });
        }

        public T? QuerySingle<T>(string sql, object parameters = null) where T : new()
        {
            using (var connection = Open())
            using (var command = CreateCommand(sql, connection))
            {
                if (parameters != null)
                    AddParameters(command, parameters);

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        return default;

                    return Map<T>(reader);
                }
            }
        }

        protected T Map<T>(DbDataReader reader) where T : new()
        {
            var obj = new T();

            var properties = typeof(T)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);

                if (!properties.TryGetValue(columnName, out var prop))
                    continue;

                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                if (value == null)
                {
                    if (Nullable.GetUnderlyingType(prop.PropertyType) != null)
                        prop.SetValue(obj, null);
                    // else ignore for non-nullable value types
                }
                else
                {
                    prop.SetValue(obj, Convert.ChangeType(
                        value,
                        Nullable.GetUnderlyingType(prop.PropertyType)
                            ?? prop.PropertyType));
                }

            }

            return obj;
        }

        // Template Method Pattern
        protected abstract string BuildInsertSql(
            string table,
            IReadOnlyList<string> columns);

        protected abstract string BuildInsertAndGetIdSql(
            string table,
            IReadOnlyList<string> columns);

        protected abstract string BuildUpdateSql(
            string table,
            IReadOnlyList<string> setColumns,
            IReadOnlyList<string> whereColumns
        );

        protected abstract string BuildDeleteSql(
            string table,
            IReadOnlyList<string> whereColumns
        );


        // part of Template Method Pattern
        // BuildInsertSql and BuildInsertAndGetIdSql used here are abstract methods defined above
        // because the algorithm is identical and Only the SQL fragment varies
        // Template Method Pattern is used here
        // BuildInsertCommand is variable (generic) here
        public async Task<int> InsertAndGetIdAsync(string table, object data)
        {
            return await RetryPolicy.ExecuteAsync(async () =>
            {
                ValidateTableName(table);

                var columns = GetPropertyNames(data, "Insert data cannot be empty.");
                var sql = BuildInsertAndGetIdSql(table, columns);
                //var (sql, parameters) = BuildInsertCommand(table, data, returnId: true);

                using (var connection = await OpenAsync())
                using (var command = CreateCommand(sql, connection))
                {
                    AddParameters(command, data);
                    var result = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            });
        }

        public int InsertAndGetId(string table, object data)
        {
            ValidateTableName(table);

            var columns = GetPropertyNames(data, "Insert data cannot be empty.");
            var sql = BuildInsertAndGetIdSql(table, columns);

            using (var connection = Open())
            using (var command = CreateCommand(sql, connection))
            {
                AddParameters(command, data);
                var result = command.ExecuteScalar();
                return Convert.ToInt32(result);
            }
        }


        private static IReadOnlyList<string> GetPropertyNames(object obj, string errorMessage)
        {
            if (obj == null)
                throw new DbLibException(errorMessage);

            var props = obj.GetType().GetProperties();
            if (props.Length == 0)
                throw new DbLibException(errorMessage);

            return props.Select(p => p.Name).ToList();
        }

        public async Task InsertAsync(string table, object parameters)
        {
            await RetryPolicy.ExecuteAsync(async () =>
            {
                ValidateTableName(table);
                var columns = GetPropertyNames(parameters, "Insert data cannot be empty.");
                var sql = BuildInsertSql(table, columns);

                using (var connection = await OpenAsync())
                using (var command = CreateCommand(sql, connection))
                {
                    if (parameters != null)
                        AddParameters(command, parameters);

                    await command.ExecuteNonQueryAsync();
                }
            });
        }

        public void Insert(string table, object data)
        {
            ValidateTableName(table);

            var columns = GetPropertyNames(data, "Insert data cannot be empty.");
            var sql = BuildInsertSql(table, columns);

            using (var connection = Open())
            using (var command = CreateCommand(sql, connection))
            {
                AddParameters(command, data);
                command.ExecuteNonQuery();
            }
        }

        public async Task<int> UpdateAsync(string table, object data, object where)
        {
            return await RetryPolicy.ExecuteAsync(async () =>
            {
                ValidateTableName(table);

                var setColumns = GetPropertyNames(data, "Update data cannot be null or empty.");
                var whereColumns = GetPropertyNames(where, "WHERE clause is required for UPDATE.");

                var sql = BuildUpdateSql(table, setColumns, whereColumns);

                using (var connection = await OpenAsync())
                using (var command = CreateCommand(sql, connection))
                {
                    AddParameters(command, data);
                    AddParameters(command, where, "w_");

                    return await command.ExecuteNonQueryAsync();
                }
            });
        }

        public int Update(string table, object data, object where)
        {
            ValidateTableName(table);

            var setColumns = GetPropertyNames(data, "Update data cannot be null or empty.");
            var whereColumns = GetPropertyNames(where, "WHERE clause is required for UPDATE.");

            var sql = BuildUpdateSql(table, setColumns, whereColumns);

            using (var connection = Open())
            using (var command = CreateCommand(sql, connection))
            {
                AddParameters(command, data);
                AddParameters(command, where, "w_");

                return command.ExecuteNonQuery();
            }
        }

        public async Task<int> DeleteAsync(string table, object where)
        {
            return await RetryPolicy.ExecuteAsync(async () =>
            {
                ValidateTableName(table);

                var whereColumns = GetPropertyNames(where, "WHERE clause is required for DELETE.");

                var sql = BuildDeleteSql(table, whereColumns);

                using (var connection = await OpenAsync())
                using (var command = CreateCommand(sql, connection))
                {
                    AddParameters(command, where);

                    return await command.ExecuteNonQueryAsync();
                }
            });
        }

        public int Delete(string table, object where)
        {
            ValidateTableName(table);

            var whereColumns = GetPropertyNames(where, "WHERE clause is required for DELETE.");
            var sql = BuildDeleteSql(table, whereColumns);

            using (var connection = Open())
            using (var command = CreateCommand(sql, connection))
            {
                AddParameters(command, where);
                return command.ExecuteNonQuery();
            }
        }

        internal async Task<IReadOnlyList<T>> QueryInternalAsync<T>(string sql, object parameters, DbConnection connection, DbTransaction transaction) where T : new()
        {
            using (var command = CreateCommand(sql, connection))
            {
                command.Transaction = transaction;

                if (parameters != null)
                    AddParameters(command, parameters);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    return MapToList<T>(reader);
                }
            }
        }

        internal IReadOnlyList<T> QueryInternal<T>(string sql, object parameters, DbConnection connection, DbTransaction transaction) where T : new()
        {
            using (var command = CreateCommand(sql, connection))
            {
                command.Transaction = transaction;

                if (parameters != null)
                    AddParameters(command, parameters);

                using (var reader = command.ExecuteReader())
                {
                    return MapToList<T>(reader);
                }
            }
        }

        internal async Task<IReadOnlyList<Dictionary<string, object>>> QueryInternalAsync(string sql, object parameters, DbConnection connection, DbTransaction transaction)
        {
            using (var command = CreateCommand(sql, connection))
            {
                command.Transaction = transaction;

                if (parameters != null)
                    AddParameters(command, parameters);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    return MapToDictionaryList(reader);
                }
            }
        }
        
        internal IReadOnlyList<Dictionary<string, object>> QueryInternal(string sql, object parameters, DbConnection connection, DbTransaction transaction)
        {
            using (var command = CreateCommand(sql, connection))
            {
                command.Transaction = transaction;

                if (parameters != null)
                    AddParameters(command, parameters);

                using (var reader = command.ExecuteReader())
                {
                    return MapToDictionaryList(reader);
                }
            }
        }

        internal async Task<T?> QuerySingleInternalAsync<T>(string sql, object parameters, DbConnection connection, DbTransaction transaction) where T : new()
        {
            var list = await QueryInternalAsync<T>(sql, parameters, connection, transaction);

            return list.Count == 0 ? default : list[0];
        }
        internal T? QuerySingleInternal<T>(string sql, object parameters, DbConnection connection, DbTransaction transaction) where T : new()
        {
            var list = QueryInternal<T>(
                sql,
                parameters,
                connection,
                transaction);

            return list.Count == 0 ? default : list[0];
        }

        internal async Task InsertInternalAsync(string table, object parameters, DbConnection connection, DbTransaction transaction)
        {
            ValidateTableName(table);
            var columns = GetPropertyNames(parameters, "Insert data cannot be empty.");
            var sql = BuildInsertSql(table, columns);

            using (var command = CreateCommand(sql, connection))
            {
                command.Transaction = transaction;
                if (parameters != null)
                    AddParameters(command, parameters);

                await command.ExecuteNonQueryAsync();
            }
        }

        internal void InsertInternal(string table, object parameters, DbConnection connection, DbTransaction transaction)
        {
            ValidateTableName(table);
            var columns = GetPropertyNames(parameters, "Insert data cannot be empty.");
            var sql = BuildInsertSql(table, columns);

            using (var command = CreateCommand(sql, connection))
            {
                command.Transaction = transaction;
                if (parameters != null)
                    AddParameters(command, parameters);

                command.ExecuteNonQuery();
            }
        }

        internal async Task<int> ExecuteInternalAsync(string sql, object parameters, DbConnection connection, DbTransaction transaction)
        {
            using (var command = CreateCommand(sql, connection))
            {
                command.Transaction = transaction;

                if (parameters != null)
                    AddParameters(command, parameters);

                return await command.ExecuteNonQueryAsync();
            }
        }

        internal int ExecuteInternal(string sql, object parameters, DbConnection connection, DbTransaction transaction)
        {
            using (var command = CreateCommand(sql, connection))
            {
                command.Transaction = transaction;

                if (parameters != null)
                    AddParameters(command, parameters);

                return command.ExecuteNonQuery();
            }
        }

        internal async Task<int> InsertAndGetIdInternalAsync(string table, object data, DbConnection connection, DbTransaction transaction)
        {
            ValidateTableName(table);
            var columns = GetPropertyNames(data, "Insert data cannot be empty.");
            var sql = BuildInsertAndGetIdSql(table, columns);

            using (var command = CreateCommand(sql, connection))
            {
                command.Transaction = transaction;
                AddParameters(command, data);

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
        }

        internal int InsertAndGetIdInternal(string table, object data, DbConnection connection, DbTransaction transaction)
        {
            ValidateTableName(table);
            var columns = GetPropertyNames(data, "Insert data cannot be empty.");
            var sql = BuildInsertAndGetIdSql(table, columns);

            using (var command = CreateCommand(sql, connection))
            {
                command.Transaction = transaction;
                AddParameters(command, data);

                var result = command.ExecuteScalar();
                return Convert.ToInt32(result);
            }
        }

        internal async Task<int> UpdateInternalAsync(string table, object data, object where, DbConnection connection, DbTransaction transaction)
        {
            ValidateTableName(table);

            var setColumns = GetPropertyNames(data, "Update data cannot be null or empty.");
            var whereColumns = GetPropertyNames(where, "WHERE clause is required for UPDATE.");

            var sql = BuildUpdateSql(table, setColumns, whereColumns);

            using (var command = CreateCommand(sql, connection))
            {
                command.Transaction = transaction;
                AddParameters(command, data);
                AddParameters(command, where, "w_");

                return await command.ExecuteNonQueryAsync();
            }
        }

        internal int UpdateInternal(string table, object data, object where, DbConnection connection, DbTransaction transaction)
        {
            ValidateTableName(table);

            var setColumns = GetPropertyNames(data, "Update data cannot be null or empty.");
            var whereColumns = GetPropertyNames(where, "WHERE clause is required for UPDATE.");

            var sql = BuildUpdateSql(table, setColumns, whereColumns);

            using (var command = CreateCommand(sql, connection))
            {
                command.Transaction = transaction;
                AddParameters(command, data);
                AddParameters(command, where, "w_");

                return command.ExecuteNonQuery();
            }
        }

        internal async Task<int> DeleteInternalAsync(string table, object where, DbConnection connection, DbTransaction transaction)
        {
            ValidateTableName(table);

            var whereColumns = GetPropertyNames(where, "WHERE clause is required for DELETE.");

            var sql = BuildDeleteSql(table, whereColumns);

            using (var command = CreateCommand(sql, connection))
            {
                command.Transaction = transaction;
                AddParameters(command, where);

                return await command.ExecuteNonQueryAsync();
            }
        }

        internal int DeleteInternal(string table, object where, DbConnection connection, DbTransaction transaction)
        {
            ValidateTableName(table);

            var whereColumns = GetPropertyNames(where, "WHERE clause is required for DELETE.");

            var sql = BuildDeleteSql(table, whereColumns);

            using (var command = CreateCommand(sql, connection))
            {
                command.Transaction = transaction;
                AddParameters(command, where);

                return command.ExecuteNonQuery();
            }
        }

    }
}
