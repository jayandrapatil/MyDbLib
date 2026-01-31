using MyDbLib.Api.Interfaces;
using MyDbLib.Api.Models;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace MyDbLib.Api
{
    public interface IDbDriver
    {
        #region ASYNC API (RAW)

        Task<DbCommandResult> ExecuteAsync(string sql, object parameters = null);

        Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object parameters = null) where T : new();

        Task<IReadOnlyList<Dictionary<string, object>>> QueryAsync(string sql, object parameters = null);

        Task<T?> QuerySingleAsync<T>(string sql, object parameters = null) where T : new();

        Task<int> InsertAndGetIdAsync(string sql, object parameters = null);

        Task<IDbTransactionScope> BeginTransactionAsync(
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);

        #endregion

        #region SYNC API (RAW)

        DbCommandResult Execute(string sql, object parameters = null);

        IReadOnlyList<T> Query<T>(string sql, object parameters = null) where T : new();

        IReadOnlyList<Dictionary<string, object>> Query(string sql, object parameters = null);

        T? QuerySingle<T>(string sql, object parameters = null) where T : new();

        int InsertAndGetId(string sql, object parameters = null);

        IDbTransactionScope BeginTransaction(
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);

        #endregion
    }
}
