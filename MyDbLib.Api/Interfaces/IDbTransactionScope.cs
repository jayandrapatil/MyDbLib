using MyDbLib.Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyDbLib.Api.Interfaces
{
    public interface IDbTransactionScope : IDisposable
    {
        // RAW execution only
        Task<int> ExecuteAsync(string sql, object parameters = null);

        Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object parameters = null) where T : new();

        Task<IReadOnlyList<Dictionary<string, object>>> QueryAsync(string sql, object parameters = null);

        Task<T?> QuerySingleAsync<T>(string sql, object parameters = null) where T : new();

        // INSERT + ID
        Task<int> InsertAndGetIdAsync(string sql, object parameters = null);
        int InsertAndGetId(string sql, object parameters = null);

        // Transaction
        Task CommitAsync();
        Task RollbackAsync();

        // Sync
        int Execute(string sql, object parameters = null);

        IReadOnlyList<T> Query<T>(string sql, object parameters = null) where T : new();

        IReadOnlyList<Dictionary<string, object>> Query(string sql, object parameters = null);

        T? QuerySingle<T>(string sql, object parameters = null) where T : new();

        void Commit();
        void Rollback();
    }
}
