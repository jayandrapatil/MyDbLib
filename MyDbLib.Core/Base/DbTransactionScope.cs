using MyDbLib.Api.Interfaces;
using MyDbLib.Api.Models;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyDbLib.Core.Base
{
    internal sealed class DbTransactionScope : IDbTransactionScope
    {
        private readonly DbDriverBase _driver;
        private readonly DbConnection _connection;
        private readonly DbTransaction _transaction;
        private bool _completed;

        public DbTransactionScope(
            DbDriverBase driver,
            DbConnection connection,
            DbTransaction transaction)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        }

        // -----------------------------
        // RAW SQL
        // -----------------------------
        public Task<int> ExecuteAsync(
            string sql,
            object parameters = null)
        {
            return _driver.ExecuteInternalAsync(
                sql,
                parameters,
                _connection,
                _transaction);
        }

        public Task<IReadOnlyList<T>> QueryAsync<T>(
            string sql,
            object parameters = null)
            where T : new()
        {
            return _driver.QueryInternalAsync<T>(
                sql,
                parameters,
                _connection,
                _transaction);
        }

        public Task<IReadOnlyList<Dictionary<string, object>>> QueryAsync(
            string sql,
            object parameters = null)
        {
            return _driver.QueryInternalAsync(
                sql,
                parameters,
                _connection,
                _transaction);
        }

        public Task<T?> QuerySingleAsync<T>(
            string sql,
            object parameters = null)
            where T : new()
        {
            return _driver.QuerySingleInternalAsync<T>(
                sql,
                parameters,
                _connection,
                _transaction);
        }

        // -----------------------------
        // CRUD HELPERS
        // -----------------------------
        public Task InsertAsync(string table, object data)
        {
            return _driver.InsertInternalAsync(
                table,
                data,
                _connection,
                _transaction);
        }

        public Task<int> InsertAndGetIdAsync(string table, object data)
        {
            return _driver.InsertAndGetIdInternalAsync(
                table,
                data,
                _connection,
                _transaction);
        }

        public Task<int> UpdateAsync(string table, object data, object where)
        {
            return _driver.UpdateInternalAsync(
                table,
                data,
                where,
                _connection,
                _transaction);
        }

        public Task<int> DeleteAsync(string table, object where)
        {
            return _driver.DeleteInternalAsync(
                table,
                where,
                _connection,
                _transaction);
        }

        // -----------------------------
        // TRANSACTION CONTROL
        // -----------------------------
        public Task CommitAsync()
        {
            if (_completed) return Task.CompletedTask;

            _transaction.Commit();
            _completed = true;
            return Task.CompletedTask;
        }

        public Task RollbackAsync()
        {
            if (_completed) return Task.CompletedTask;

            _transaction.Rollback();
            _completed = true;
            return Task.CompletedTask;
        }

        // -----------------------------
        // DISPOSE SAFETY
        // -----------------------------
        public void Dispose()
        {
            if (!_completed)
            {
                try
                {
                    _transaction.Rollback();
                }
                catch
                {
                    // swallow — rollback best effort
                }
            }

            _transaction.Dispose();
            _connection.Dispose();
        }
    }
}
