using MyDbLib.Api.Interfaces;
using MyDbLib.Api.Models;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace MyDbLib.Api
{
    public interface IDbDriver
    {
        Task<DbCommandResult> ExecuteAsync(string sql, object parameters = null);

        Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object parameters = null) where T : new();

        Task<IReadOnlyList<Dictionary<string, object>>> QueryAsync(string sql, object parameters = null);

        Task<T?> QuerySingleAsync<T>(string sql, object parameters = null) where T : new();

        Task InsertAsync(string table, object data);

        Task<int> InsertAndGetIdAsync(string table, object data);

        Task<int> UpdateAsync(string table, object data, object where);

        Task<int> DeleteAsync(string table, object where);

        Task<IDbTransactionScope> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);
    }
}
