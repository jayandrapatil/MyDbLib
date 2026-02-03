using MyDbLib.Api.Interfaces;
using MyDbLib.Api.Models;
using System;
using System.Collections.Generic;
using System.Data.Common;
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

        #region RAW EXECUTION

        public Task<int> ExecuteAsync(string sql, object parameters = null)
        {
            return _driver.ExecuteInternalAsync(
                sql, parameters, _connection, _transaction);
        }

        public int Execute(string sql, object parameters = null)
        {
            return _driver.ExecuteInternal(
                sql, parameters, _connection, _transaction);
        }

        public Task<IReadOnlyList<T>> QueryAsync<T>(
            string sql, object parameters = null) where T : new()
        {
            return _driver.QueryInternalAsync<T>(
                sql, parameters, _connection, _transaction);
        }

        public IReadOnlyList<T> Query<T>(
            string sql, object parameters = null) where T : new()
        {
            return _driver.QueryInternal<T>(
                sql, parameters, _connection, _transaction);
        }

        public Task<IReadOnlyList<Dictionary<string, object>>> QueryAsync(
            string sql, object parameters = null)
        {
            return _driver.QueryInternalAsync(
                sql, parameters, _connection, _transaction);
        }

        public IReadOnlyList<Dictionary<string, object>> Query(
            string sql, object parameters = null)
        {
            return _driver.QueryInternal(
                sql, parameters, _connection, _transaction);
        }

        public Task<T?> QuerySingleAsync<T>(
            string sql, object parameters = null) where T : new()
        {
            return _driver.QuerySingleInternalAsync<T>(
                sql, parameters, _connection, _transaction);
        }

        public T? QuerySingle<T>(
            string sql, object parameters = null) where T : new()
        {
            var list = Query<T>(sql, parameters);
            return list.Count == 0 ? default : list[0];
        }

        public Task<int> InsertAndGetIdAsync(string sql, object parameters = null)
        {
            return _driver.InsertAndGetIdInternalAsync(
                sql, parameters, _connection, _transaction);
        }

        public int InsertAndGetId(string sql, object parameters = null)
        {
            return _driver.InsertAndGetIdInternal(
                sql, parameters, _connection, _transaction);
        }
        #endregion

        #region TRANSACTION CONTROL

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
                try { _transaction.Rollback(); }
                catch { /* best effort */ }
            }

            _transaction.Dispose();
            _connection.Dispose();
        }

        #endregion
    }
}
