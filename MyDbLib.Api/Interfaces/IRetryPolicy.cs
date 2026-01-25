using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyDbLib.Api.Interfaces
{
    /// <summary>
    /// Defines a retry abstraction for transient failures.
    /// This is DB-agnostic and execution-focused.
    /// </summary>
    public interface IRetryPolicy
    {
        /// <summary>
        /// Executes an async action with retry support.
        /// </summary>
        Task ExecuteAsync(Func<Task> action);

        /// <summary>
        /// Executes an async action with retry support and returns a value.
        /// </summary>
        Task<T> ExecuteAsync<T>(Func<Task<T>> action);

        T Execute<T>(Func<T> action);
    }
}
