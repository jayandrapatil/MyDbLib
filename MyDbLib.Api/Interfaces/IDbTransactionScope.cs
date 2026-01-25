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
        #region Async methods
        Task<int> ExecuteAsync(string sql, object parameters = null);

        Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object parameters = null) where T : new();

        Task<IReadOnlyList<Dictionary<string, object>>> QueryAsync(string sql, object parameters = null);

        Task<T?> QuerySingleAsync<T>(string sql, object parameters = null) where T : new();

        Task InsertAsync(string table, object data);

        Task<int> InsertAndGetIdAsync(string table, object data);

        Task<int> UpdateAsync(string table, object data, object where);

        Task<int> DeleteAsync(string table, object where);

        Task CommitAsync();
        Task RollbackAsync();
        #endregion

        #region Sync methods
        int Execute(string sql, object parameters = null);

        IReadOnlyList<T> Query<T>(string sql, object parameters = null) where T : new();

        IReadOnlyList<Dictionary<string, object>> Query(string sql, object parameters = null);

        T? QuerySingle<T>(string sql, object parameters = null) where T : new();

        void Insert(string table, object data);

        int InsertAndGetId(string table, object data);

        int Update(string table, object data, object where);

        int Delete(string table, object where);

        void Commit();
        void Rollback();
        #endregion
    }
}
