using MyDbLib.Api;
using MyDbLib.Api.Exceptions;
using MyDbLib.Api.Interfaces;
using MyDbLib.Api.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        protected DbDriverBase(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new DbLibException("Connection string cannot be empty.");

            ConnectionString = connectionString;
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

        // this is NOT abstract because, Opening is same for all DBs
        private DbCommand CreateCommand(string sql, DbConnection connection)
        {
            if (connection == null)
                throw new DbLibException("Connection is null.");

            var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;
            return cmd;
        }

        protected virtual void AddParameters(DbCommand command, object parameters)
        {
            foreach (var prop in parameters.GetType().GetProperties())
            {
                var param = command.CreateParameter();
                param.ParameterName = "@" + prop.Name;
                param.Value = prop.GetValue(parameters) ?? DBNull.Value;

                command.Parameters.Add(param);
            }
        }

        public async Task<IDbTransactionScope> BeginTransactionAsync(
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            var connection = await OpenAsync();
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

                    prop.SetValue(obj, value);
                }

                result.Add(obj);
            }

            return result;
        }

        public async Task<T?> QuerySingleAsync<T>(string sql,object? parameters = null) where T : new()
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
                prop.SetValue(obj, value);
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
        private (string Sql, object Parameters) BuildInsertCommand(
            string table,
            object data,
            bool returnId)
        {
            if (string.IsNullOrWhiteSpace(table))
                throw new DbLibException("Table name cannot be empty.");

            if (data == null)
                throw new DbLibException("Insert data cannot be null.");

            var props = data.GetType().GetProperties();
            if (props.Length == 0)
                throw new DbLibException("Insert data has no properties.");

            var columns = props.Select(p => p.Name).ToList();

            var sql = returnId
                ? BuildInsertAndGetIdSql(table, columns)
                : BuildInsertSql(table, columns);

            return (sql, data);
        }

        // Template Method Pattern is used here
        // BuildInsertCommand is variable (generic) here
        public async Task<int> InsertAndGetIdAsync(
            string table,
            object data)
        {
            var (sql, parameters) = BuildInsertCommand(table, data, returnId: true);

            using (var connection = await OpenAsync())
            using (var command = CreateCommand(sql, connection))
            {
                AddParameters(command, parameters);

                var result = await command.ExecuteScalarAsync();
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

        public async Task<int> UpdateAsync(
            string table,
            object data,
            object where)
        {
            if (string.IsNullOrWhiteSpace(table))
                throw new DbLibException("Table name cannot be empty.");

            var setColumns = GetPropertyNames(data, "Update data cannot be null or empty.");
            var whereColumns = GetPropertyNames(where, "WHERE clause is required for UPDATE.");

            if (setColumns.Intersect(whereColumns, StringComparer.OrdinalIgnoreCase).Any())
                throw new DbLibException("SET and WHERE columns must not overlap.");

            var sql = BuildUpdateSql(table, setColumns, whereColumns);

            using (var connection = await OpenAsync())
            using (var command = CreateCommand(sql, connection))
            {
                AddParameters(command, data);
                AddParameters(command, where);

                return await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<int> DeleteAsync(
            string table,
            object where)
        {
            if (string.IsNullOrWhiteSpace(table))
                throw new DbLibException("Table name cannot be empty.");

            var whereColumns = GetPropertyNames(where, "WHERE clause is required for DELETE.");

            var sql = BuildDeleteSql(table, whereColumns);

            using (var connection = await OpenAsync())
            using (var command = CreateCommand(sql, connection))
            {
                AddParameters(command, where);

                return await command.ExecuteNonQueryAsync();
            }
        }

        internal async Task<IReadOnlyList<T>> QueryInternalAsync<T>(
            string sql,
            object parameters,
            DbConnection connection,
            DbTransaction transaction)
            where T : new()
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

        internal async Task<IReadOnlyList<Dictionary<string, object>>> QueryInternalAsync(
            string sql,
            object parameters,
            DbConnection connection,
            DbTransaction transaction)
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

        internal async Task<T?> QuerySingleInternalAsync<T>(
            string sql,
            object parameters,
            DbConnection connection,
            DbTransaction transaction)
            where T : new()
        {
            var list = await QueryInternalAsync<T>(
                sql,
                parameters,
                connection,
                transaction);

            return list.Count == 0 ? default : list[0];
        }


        public async Task InsertAsync(string table, object data)
        {
            using (var connection = await OpenAsync())
            {
                await InsertInternalAsync(
                    table,
                    data,
                    connection,
                    transaction: null);
            }
        }

        internal async Task InsertInternalAsync(
            string table,
            object data,
            DbConnection connection,
            DbTransaction transaction)
        {
            var columns = GetPropertyNames(data, "Insert data cannot be empty.");
            var sql = BuildInsertSql(table, columns);

            await ExecuteInternalAsync(sql, data, connection, transaction);
        }

        internal async Task<int> ExecuteInternalAsync(
            string sql,
            object parameters,
            DbConnection connection,
            DbTransaction transaction)
        {
            using (var command = CreateCommand(sql, connection))
            {
                command.Transaction = transaction;

                if (parameters != null)
                    AddParameters(command, parameters);

                return await command.ExecuteNonQueryAsync();
            }
        }

        internal async Task<int> InsertAndGetIdInternalAsync(
            string table,
            object data,
            DbConnection connection,
            DbTransaction transaction)
        {
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

        internal async Task<int> UpdateInternalAsync(
            string table,
            object data,
            object where,
            DbConnection connection,
            DbTransaction transaction)
        {
            if (string.IsNullOrWhiteSpace(table))
                throw new DbLibException("Table name cannot be empty.");

            var setColumns = GetPropertyNames(data, "Update data cannot be null or empty.");
            var whereColumns = GetPropertyNames(where, "WHERE clause is required for UPDATE.");

            if (setColumns.Intersect(whereColumns, StringComparer.OrdinalIgnoreCase).Any())
                throw new DbLibException("SET and WHERE columns must not overlap.");

            var sql = BuildUpdateSql(table, setColumns, whereColumns);

            using (var command = CreateCommand(sql, connection))
            {
                command.Transaction = transaction;
                AddParameters(command, data);
                AddParameters(command, where);

                return await command.ExecuteNonQueryAsync();
            }
        }

        internal async Task<int> DeleteInternalAsync(
            string table,
            object where,
            DbConnection connection,
            DbTransaction transaction)
        {
            if (string.IsNullOrWhiteSpace(table))
                throw new DbLibException("Table name cannot be empty.");

            var whereColumns = GetPropertyNames(where, "WHERE clause is required for DELETE.");

            var sql = BuildDeleteSql(table, whereColumns);

            using (var command = CreateCommand(sql, connection))
            {
                command.Transaction = transaction;
                AddParameters(command, where);

                return await command.ExecuteNonQueryAsync();
            }
        }

    }
}
