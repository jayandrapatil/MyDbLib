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

        // RAW SQL
        public Task<int> ExecuteAsync(string sql, object parameters = null)
        {
            return _driver.ExecuteInternalAsync(sql, parameters, _connection, _transaction);
        }

        public int Execute(string sql, object parameters = null)
        {
            return _driver.ExecuteInternal(sql, parameters, _connection, _transaction);
        }

        public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object parameters = null) where T : new()
        {
            return _driver.QueryInternalAsync<T>(sql, parameters, _connection, _transaction);
        }

        public IReadOnlyList<T> Query<T>(string sql, object parameters = null) where T : new()
        {
            return _driver.QueryInternal<T>(sql, parameters, _connection, _transaction);
        }

        public Task<IReadOnlyList<Dictionary<string, object>>> QueryAsync(string sql, object parameters = null)
        {
            return _driver.QueryInternalAsync(sql, parameters, _connection, _transaction);
        }

        public IReadOnlyList<Dictionary<string, object>> Query(string sql, object parameters = null)
        {
            return _driver.QueryInternal(sql, parameters, _connection, _transaction);
        }

        public Task<T?> QuerySingleAsync<T>(string sql, object parameters = null) where T : new()
        {
            return _driver.QuerySingleInternalAsync<T>(sql, parameters, _connection, _transaction);
        }
        public T? QuerySingle<T>(string sql, object parameters = null) where T : new()
        {
            var list = Query<T>(sql, parameters);
            return list.Count == 0 ? default : list[0];
        }

        #region Async CRUD HELPERS
        public Task InsertAsync(string table, object data)
        {
            return _driver.InsertInternalAsync(table, data, _connection, _transaction);
        }

        public Task<int> InsertAndGetIdAsync(string table, object data)
        {
            return _driver.InsertAndGetIdInternalAsync(table, data, _connection, _transaction);
        }

        public Task<int> UpdateAsync(string table, object data, object where)
        {
            return _driver.UpdateInternalAsync(table, data, where, _connection, _transaction);
        }

        public Task<int> DeleteAsync(string table, object where)
        {
            return _driver.DeleteInternalAsync(table, where, _connection, _transaction);
        }
        #endregion

        #region Sync CRUD HELPERS
        public void Insert(string table, object data)
        {
            _driver.InsertInternal(table, data, _connection, _transaction);
        }

        public int InsertAndGetId(string table, object data)
        {
            return _driver.InsertAndGetIdInternal(table, data, _connection, _transaction);
        }

        public int Update(string table, object data, object where)
        {
            return _driver.UpdateInternal(table, data, where, _connection, _transaction);
        }

        public int Delete(string table, object where)
        {
            return _driver.DeleteInternal(table, where, _connection, _transaction);
        }
        #endregion


        #region Async TRANSACTION CONTROL
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
        #endregion

        #region Sync TRANSACTION CONTROL
        public void Commit()
        {
            if (_completed) return;

            _transaction.Commit();
            _completed = true;
        }

        public void Rollback()
        {
            if (_completed) return;

            _transaction.Rollback();
            _completed = true;
        }
        #endregion

        #region DISPOSE SAFETY
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
        #endregion
    }
}
